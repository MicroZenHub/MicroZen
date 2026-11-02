using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using MicroZen.Api.Context;
using MicroZen.Api.Entities;
using MicroZen.Api.Services;
using MicroZen.Grpc.V1;
using static Xunit.Assert;

namespace MicroZen.Core.Api.Test.Services;

/// <summary>
/// Tests for should-fix #55: a global EF Core query filter
/// (<c>DeletedOn == null</c>) on every <see cref="MicroZen.Api.Entities.Shared.BaseEntity{TMessage}"/>
/// type so soft-deleted rows are excluded from normal reads, the
/// <c>IgnoreQueryFilters()</c> opt-out still returns them, and
/// <see cref="OrganizationsService.EnableOrganization"/> can still undelete.
///
/// Follows the backend test conventions (<c>MicroZenContextTests</c>,
/// <c>OrganizationsServiceTests</c>): EF Core InMemory provider, a
/// self-supplied <c>EncryptionKey</c>, unique database name per fixture, and
/// GIVEN / WHEN / THEN phases. InMemory honors global query filters, so no
/// PostgreSQL is needed.
/// </summary>
public class SoftDeleteQueryFilterTests : IDisposable
{
	private readonly DbContextOptions<MicroZenContext> _options;

	public SoftDeleteQueryFilterTests()
	{
		// Required by MicroZenContext.OnModelCreating (column encryption).
		Environment.SetEnvironmentVariable("EncryptionKey", "1234567890");

		// A unique database name isolates this fixture from other test classes.
		_options = new DbContextOptionsBuilder<MicroZenContext>()
			.UseInMemoryDatabase(databaseName: $"SoftDeleteQueryFilterTests_{Guid.NewGuid():N}")
			.Options;
	}

	[Fact]
	public async Task GetOrganization_SoftDeletedOrg_ThrowsNotFound_And_GetOrganizations_ExcludesIt()
	{
		// GIVEN one active org and one soft-deleted org.
		int activeId, deletedId;
		await using (var seed = new MicroZenContext(_options))
		{
			var active = new Organization { Name = "Active Org" };
			var deleted = new Organization { Name = "Deleted Org", DeletedOn = DateTime.UtcNow };
			seed.Organizations.AddRange(active, deleted);
			await seed.SaveChangesAsync();
			activeId = active.Id;
			deletedId = deleted.Id;
		}

		await using var db = new MicroZenContext(_options);
		var service = new OrganizationsService(db);

		// WHEN reading the soft-deleted org by id, THEN it is NotFound.
		var ex = await ThrowsAsync<RpcException>(() =>
			service.GetOrganization(new GetOrganizationRequest { Id = deletedId }, TestServerCallContext.Create()));
		Equal(StatusCode.NotFound, ex.StatusCode);

		// AND the active org still reads back.
		var activeResult = await service.GetOrganization(
			new GetOrganizationRequest { Id = activeId }, TestServerCallContext.Create());
		Equal(activeId, activeResult.Id);

		// AND the list read returns only the active org.
		var list = await service.GetOrganizations(new MultipleOrganizationsRequest(), TestServerCallContext.Create());
		var single = Single(list.Organizations);
		Equal(activeId, single.Id);
	}

	[Fact]
	public async Task GetClient_SoftDeletedClient_ThrowsNotFound_And_GetManyClients_ExcludesIt_And_TotalCountsActiveOnly()
	{
		// GIVEN one active and one soft-deleted client of the same type.
		int activeId, deletedId;
		await using (var seed = new MicroZenContext(_options))
		{
			var active = new Client { Name = "Active Client", Type = ClientType.Api, OrganizationId = 1, Description = "active" };
			var deleted = new Client { Name = "Deleted Client", Type = ClientType.Api, OrganizationId = 1, Description = "deleted", DeletedOn = DateTime.UtcNow };
			seed.Clients.AddRange(active, deleted);
			await seed.SaveChangesAsync();
			activeId = active.Id;
			deletedId = deleted.Id;
		}

		await using var db = new MicroZenContext(_options);
		var service = new ClientsGrpcService(db);

		// WHEN reading the soft-deleted client by id, THEN it is NotFound.
		var ex = await ThrowsAsync<RpcException>(() =>
			service.GetClient(new ClientRequest { Id = deletedId }, TestServerCallContext.Create()));
		Equal(StatusCode.NotFound, ex.StatusCode);

		// AND the active client still reads back.
		var activeResult = await service.GetClient(new ClientRequest { Id = activeId }, TestServerCallContext.Create());
		Equal(activeId, activeResult.Id);

		// AND GetManyClients returns only the active client with Total counting active rows only.
		var many = await service.GetManyClients(
			new ManyClientsRequest { Type = ClientType.Api }, TestServerCallContext.Create());
		Equal(1, many.Total);
		var single = Single(many.Clients);
		Equal(activeId, single.Id);
	}

	[Fact]
	public async Task DisableOrganization_ThenReads_NoLongerSeeTheOrg()
	{
		// GIVEN one active org.
		int orgId;
		await using (var seed = new MicroZenContext(_options))
		{
			var active = new Organization { Name = "To Disable" };
			seed.Organizations.Add(active);
			await seed.SaveChangesAsync();
			orgId = active.Id;
		}

		await using var db = new MicroZenContext(_options);
		var service = new OrganizationsService(db);

		// WHEN the org is disabled (soft-deleted).
		await service.DisableOrganization(new DisableOrganizationRequest { Id = orgId }, TestServerCallContext.Create());

		// THEN reading it by id is NotFound.
		var ex = await ThrowsAsync<RpcException>(() =>
			service.GetOrganization(new GetOrganizationRequest { Id = orgId }, TestServerCallContext.Create()));
		Equal(StatusCode.NotFound, ex.StatusCode);

		// AND the list read no longer includes it.
		var list = await service.GetOrganizations(new MultipleOrganizationsRequest(), TestServerCallContext.Create());
		Empty(list.Organizations);
	}

	[Fact]
	public async Task DeleteClient_ThenGetClient_IsNotFound()
	{
		// GIVEN one active client.
		int clientId;
		await using (var seed = new MicroZenContext(_options))
		{
			var active = new Client { Name = "To Delete", Type = ClientType.Api, OrganizationId = 1, Description = "active" };
			seed.Clients.Add(active);
			await seed.SaveChangesAsync();
			clientId = active.Id;
		}

		await using var db = new MicroZenContext(_options);
		var service = new ClientsGrpcService(db);

		// WHEN the client is deleted (soft-deleted).
		await service.DeleteClient(new DeleteClientRequest { Id = clientId }, TestServerCallContext.Create());

		// THEN reading it by id is NotFound.
		var ex = await ThrowsAsync<RpcException>(() =>
			service.GetClient(new ClientRequest { Id = clientId }, TestServerCallContext.Create()));
		Equal(StatusCode.NotFound, ex.StatusCode);
	}

	[Fact]
	public async Task EnableOrganization_UndeletesSoftDeletedOrg_ViaIgnoreQueryFiltersOptOut()
	{
		// GIVEN a soft-deleted org (hidden by the global filter).
		int orgId;
		await using (var seed = new MicroZenContext(_options))
		{
			var deleted = new Organization { Name = "Disabled Org", DeletedOn = DateTime.UtcNow };
			seed.Organizations.Add(deleted);
			await seed.SaveChangesAsync();
			orgId = deleted.Id;
		}

		await using var db = new MicroZenContext(_options);
		var service = new OrganizationsService(db);

		// WHEN enabling the org, THEN it succeeds (the IgnoreQueryFilters opt-out loads it)
		// and returns the org instead of throwing NotFound.
		var result = await service.EnableOrganization(
			new EnableOrganizationRequest { Id = orgId }, TestServerCallContext.Create());
		Equal(orgId, result.Id);

		// AND DeletedOn is now cleared, so it reads back as active.
		await using var verify = new MicroZenContext(_options);
		var reloaded = await verify.Organizations.FirstAsync(o => o.Id == orgId);
		Null(reloaded.DeletedOn);

		// AND a normal read now surfaces it again.
		var afterService = new OrganizationsService(verify);
		var afterResult = await afterService.GetOrganization(
			new GetOrganizationRequest { Id = orgId }, TestServerCallContext.Create());
		Equal(orgId, afterResult.Id);
	}

	[Fact]
	public async Task IgnoreQueryFilters_DirectQuery_SeesDeletedRows_WhileNormalQueryExcludesThem()
	{
		// GIVEN one active and one soft-deleted org.
		await using (var seed = new MicroZenContext(_options))
		{
			seed.Organizations.AddRange(
				new Organization { Name = "Active Org" },
				new Organization { Name = "Deleted Org", DeletedOn = DateTime.UtcNow });
			await seed.SaveChangesAsync();
		}

		await using var db = new MicroZenContext(_options);

		// WHEN counting with the global filter applied, THEN only the active row counts.
		Equal(1, await db.Organizations.CountAsync());

		// AND the IgnoreQueryFilters opt-out still sees both rows.
		Equal(2, await db.Organizations.IgnoreQueryFilters().CountAsync());
	}

	public void Dispose()
	{
		GC.SuppressFinalize(this);
		Environment.SetEnvironmentVariable("EncryptionKey", null);
	}
}
