using Microsoft.EntityFrameworkCore;
using MicroZen.Api.Context;
using MicroZen.Api.Entities;
using MicroZen.Grpc.V1;
using static Xunit.Assert;
using ClientsService = MicroZen.Api.Services.ClientsGrpcService;

namespace MicroZen.Core.Api.Test.Services;

/// <summary>
/// Tests for <see cref="MicroZen.Api.Services.ClientsGrpcService.GetManyClients"/>
/// covering the search-term name filter fixed in Must Fix #50. The filter
/// originally interpolated the whole request message
/// (<c>$"%{request}%"</c>) instead of the search term, so every search matched
/// the protobuf debug string and returned nothing.
///
/// The fix also replaced the Npgsql-only <c>EF.Functions.ILike</c> with a
/// provider-agnostic, case-insensitive predicate
/// (<c>c.Name.ToLower().Contains(term.ToLower())</c>) so the behavior is
/// identical on PostgreSQL yet translatable/evaluable under the EF Core
/// InMemory provider this suite uses — no PostgreSQL required.
/// </summary>
public class ClientsGrpcServiceGetManyClientsTests : IDisposable
{
	private readonly DbContextOptions<MicroZenContext> _options;

	public ClientsGrpcServiceGetManyClientsTests()
	{
		// Required by MicroZenContext.OnModelCreating (column encryption).
		Environment.SetEnvironmentVariable("EncryptionKey", "1234567890");

		// A unique database name isolates this fixture from other test classes.
		_options = new DbContextOptionsBuilder<MicroZenContext>()
			.UseInMemoryDatabase(databaseName: $"GetManyClientsTests_{Guid.NewGuid():N}")
			.Options;
	}

	private async Task SeedAsync()
	{
		await using var ctx = new MicroZenContext(_options);
		// Description is non-null because Client.ToMessage() maps it into a
		// protobuf string field, which rejects null values.
		ctx.Clients.AddRange(
			new Client { Name = "Acme Portal", Type = ClientType.Api, OrganizationId = 1, Description = "Acme web portal" },
			new Client { Name = "Acme Admin", Type = ClientType.Api, OrganizationId = 1, Description = "Acme admin console" },
			new Client { Name = "Beta Service", Type = ClientType.Api, OrganizationId = 1, Description = "Beta backend service" });
		await ctx.SaveChangesAsync();
	}

	private async Task<ManyClientsResponse> GetManyAsync(ManyClientsRequest request)
	{
		await using var ctx = new MicroZenContext(_options);
		var service = new ClientsService(ctx);
		return await service.GetManyClients(request, TestServerCallContext.Create());
	}

	[Fact]
	public async Task GetManyClients_MatchingTerm_ReturnsMatchingClients()
	{
		// GIVEN three API clients, two named "Acme*".
		await SeedAsync();

		// WHEN searching for "Acme".
		var response = await GetManyAsync(new ManyClientsRequest
		{
			Type = ClientType.Api,
			SearchTerm = "Acme",
		});

		// THEN only the two Acme clients come back, not "Beta Service".
		Equal(2, response.Clients.Count);
		All(response.Clients, c => Contains("Acme", c.Name));
		DoesNotContain(response.Clients, c => c.Name == "Beta Service");
	}

	[Fact]
	public async Task GetManyClients_SearchTerm_IsCaseInsensitive()
	{
		// GIVEN the seeded clients.
		await SeedAsync();

		// WHEN searching with a lower-case term.
		var response = await GetManyAsync(new ManyClientsRequest
		{
			Type = ClientType.Api,
			SearchTerm = "acme",
		});

		// THEN the same two Acme clients match (case-insensitive).
		Equal(2, response.Clients.Count);
		All(response.Clients, c => Contains("Acme", c.Name));
	}

	[Fact]
	public async Task GetManyClients_NonMatchingTerm_ReturnsNone()
	{
		// GIVEN the seeded clients.
		await SeedAsync();

		// WHEN searching for a term no name contains.
		var response = await GetManyAsync(new ManyClientsRequest
		{
			Type = ClientType.Api,
			SearchTerm = "Zzz",
		});

		// THEN no clients come back.
		Empty(response.Clients);
	}

	[Fact]
	public async Task GetManyClients_EmptySearchTerm_ReturnsAllOfType()
	{
		// GIVEN the seeded clients.
		await SeedAsync();

		// WHEN the search term is an empty string.
		var response = await GetManyAsync(new ManyClientsRequest
		{
			Type = ClientType.Api,
			SearchTerm = string.Empty,
		});

		// THEN all three clients of the type are returned (empty = no filter).
		Equal(3, response.Clients.Count);
	}

	[Fact]
	public async Task GetManyClients_NullSearchTerm_ReturnsAllOfType()
	{
		// GIVEN the seeded clients.
		await SeedAsync();

		// WHEN no search term is set (SearchTerm wrapper unset / null).
		var response = await GetManyAsync(new ManyClientsRequest
		{
			Type = ClientType.Api,
		});

		// THEN all three clients of the type are returned (no name filtering).
		Equal(3, response.Clients.Count);
	}

	public void Dispose()
	{
		GC.SuppressFinalize(this);
		Environment.SetEnvironmentVariable("EncryptionKey", null);
	}
}
