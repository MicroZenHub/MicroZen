using System.Threading;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using MicroZen.Api.Context;
using MicroZen.Api.Entities;
using MicroZen.Grpc.V1;
using static Xunit.Assert;
// The service class and its generated gRPC base share the simple name "ClientsGrpcService"
// (MicroZen.Api.Services vs MicroZen.Grpc.V1); alias the concrete service under test.
using ClientsGrpcService = MicroZen.Api.Services.ClientsGrpcService;

namespace MicroZen.Core.Api.Test.Services;

/// <summary>
/// Tests for <see cref="ClientsGrpcService.GetManyClients"/> paging behaviour (issue #51).
///
/// Follows the reference shape in <c>Context/MicroZenContextTests.cs</c>: a unique
/// in-memory database per fixture, the <c>EncryptionKey</c> set in the constructor and
/// cleared in <see cref="Dispose"/>, and GIVEN / WHEN / THEN comments.
///
/// After the #51 fix:
/// - <c>request.Page</c> is a 1-based page number (default 1).
/// - <c>request.Skip</c> is the page size (default 10).
/// - The offset is <c>(page - 1) * pageSize</c>, so pages never overlap.
/// - <c>NextUrl</c> carries <c>page={page+1}</c> and <c>skip={pageSize}</c>, and is absent on the last page.
/// - <c>Total</c> is set on every response.
/// </summary>
public class ClientsGrpcServiceTests : IDisposable
{
	private const int TotalClients = 25;
	private const int PageSize = 10;

	private readonly DbContextOptions<MicroZenContext> _options;

	public ClientsGrpcServiceTests()
	{
		// Required by MicroZenContext.OnModelCreating (column encryption).
		Environment.SetEnvironmentVariable("EncryptionKey", "1234567890");

		// A unique database name isolates this fixture from other test classes.
		_options = new DbContextOptionsBuilder<MicroZenContext>()
			.UseInMemoryDatabase(databaseName: $"ClientsGrpcServiceTests_{Guid.NewGuid():N}")
			.Options;
	}

	/// <summary>
	/// Seeds <see cref="TotalClients"/> API clients with zero-padded, name-ordered
	/// names ("Client 01" .. "Client 25") so <c>OrderBy(c =&gt; c.Name)</c> is deterministic.
	/// </summary>
	private async Task SeedClientsAsync()
	{
		await using var context = new MicroZenContext(_options);
		for (var i = 1; i <= TotalClients; i++)
		{
			context.Clients.Add(new Client
			{
				Name = $"Client {i:D2}",
				Type = ClientType.Api,
				Description = $"Seeded client {i:D2}",
			});
		}
		await context.SaveChangesAsync();
	}

	private static ManyClientsRequest Request(int page, int skip) =>
		new() { Type = ClientType.Api, Page = page, Skip = skip };

	[Fact]
	public async Task GetManyClients_Page1_ReturnsFirstWindowWithNextUrl()
	{
		// GIVEN 25 seeded API clients.
		await SeedClientsAsync();
		await using var context = new MicroZenContext(_options);
		var service = new ClientsGrpcService(context);

		// WHEN the first page is requested with a page size of 10.
		var response = await service.GetManyClients(Request(page: 1, skip: PageSize), new TestServerCallContext());

		// THEN the first 10 clients by name are returned, Total is 25, and NextUrl points to page 2.
		Equal(PageSize, response.Clients.Count);
		Equal(TotalClients, response.Total);
		Equal(
			new[] { "Client 01", "Client 02", "Client 03", "Client 04", "Client 05", "Client 06", "Client 07", "Client 08", "Client 09", "Client 10" },
			response.Clients.Select(c => c.Name));
		NotNull(response.NextUrl);
		Contains("page=2", response.NextUrl);
		Contains("skip=10", response.NextUrl);
	}

	[Fact]
	public async Task GetManyClients_Page2_IsNonOverlappingWithPage1()
	{
		// GIVEN 25 seeded API clients.
		await SeedClientsAsync();
		await using var context = new MicroZenContext(_options);
		var service = new ClientsGrpcService(context);

		// WHEN page 1 and page 2 are both requested with a page size of 10.
		var page1 = await service.GetManyClients(Request(page: 1, skip: PageSize), new TestServerCallContext());
		var page2 = await service.GetManyClients(Request(page: 2, skip: PageSize), new TestServerCallContext());

		// THEN page 2 returns clients 11..20 and shares no client with page 1.
		Equal(PageSize, page2.Clients.Count);
		Equal(
			new[] { "Client 11", "Client 12", "Client 13", "Client 14", "Client 15", "Client 16", "Client 17", "Client 18", "Client 19", "Client 20" },
			page2.Clients.Select(c => c.Name));

		var page1Names = page1.Clients.Select(c => c.Name).ToHashSet();
		var page2Names = page2.Clients.Select(c => c.Name).ToHashSet();
		Empty(page1Names.Intersect(page2Names));

		NotNull(page2.NextUrl);
		Contains("page=3", page2.NextUrl);
		Contains("skip=10", page2.NextUrl);
	}

	[Fact]
	public async Task GetManyClients_LastPage_EmitsNoNextUrl()
	{
		// GIVEN 25 seeded API clients.
		await SeedClientsAsync();
		await using var context = new MicroZenContext(_options);
		var service = new ClientsGrpcService(context);

		// WHEN the final page (page 3, size 10) is requested.
		var response = await service.GetManyClients(Request(page: 3, skip: PageSize), new TestServerCallContext());

		// THEN clients 21..25 are returned, Total is 25, and there is no NextUrl.
		Equal(5, response.Clients.Count);
		Equal(TotalClients, response.Total);
		Equal(
			new[] { "Client 21", "Client 22", "Client 23", "Client 24", "Client 25" },
			response.Clients.Select(c => c.Name));
		True(string.IsNullOrEmpty(response.NextUrl));
	}

	[Fact]
	public async Task GetManyClients_WithoutPagingArguments_AppliesDefaults()
	{
		// GIVEN 25 seeded API clients.
		await SeedClientsAsync();
		await using var context = new MicroZenContext(_options);
		var service = new ClientsGrpcService(context);

		// WHEN no paging arguments are supplied (Page=0, Skip=0).
		var response = await service.GetManyClients(Request(page: 0, skip: 0), new TestServerCallContext());

		// THEN it behaves as page 1 with the default size 10 (10 rows from offset 0).
		Equal(PageSize, response.Clients.Count);
		Equal(TotalClients, response.Total);
		Equal(
			new[] { "Client 01", "Client 02", "Client 03", "Client 04", "Client 05", "Client 06", "Client 07", "Client 08", "Client 09", "Client 10" },
			response.Clients.Select(c => c.Name));
	}

	[Fact]
	public async Task GetManyClients_SetsTotalOnEveryResponse()
	{
		// GIVEN 25 seeded API clients.
		await SeedClientsAsync();
		await using var context = new MicroZenContext(_options);
		var service = new ClientsGrpcService(context);

		// WHEN each page is requested in turn.
		var page1 = await service.GetManyClients(Request(page: 1, skip: PageSize), new TestServerCallContext());
		var page2 = await service.GetManyClients(Request(page: 2, skip: PageSize), new TestServerCallContext());
		var lastPage = await service.GetManyClients(Request(page: 3, skip: PageSize), new TestServerCallContext());

		// THEN Total is set on every response, whether or not a next page exists.
		Equal(TotalClients, page1.Total);
		Equal(TotalClients, page2.Total);
		Equal(TotalClients, lastPage.Total);
	}

	public void Dispose()
	{
		GC.SuppressFinalize(this);
		Environment.SetEnvironmentVariable("EncryptionKey", null);
	}

	/// <summary>
	/// Minimal <see cref="ServerCallContext"/> stub. <see cref="ClientsGrpcService.GetManyClients"/>
	/// never reads the call context, so every member is a no-op default.
	/// </summary>
	private sealed class TestServerCallContext : ServerCallContext
	{
		protected override string MethodCore => string.Empty;
		protected override string HostCore => string.Empty;
		protected override string PeerCore => string.Empty;
		protected override DateTime DeadlineCore => DateTime.MaxValue;
		protected override Metadata RequestHeadersCore => new();
		protected override CancellationToken CancellationTokenCore => CancellationToken.None;
		protected override Metadata ResponseTrailersCore => new();
		protected override Status StatusCore { get; set; }
		protected override WriteOptions? WriteOptionsCore { get; set; }
		protected override AuthContext AuthContextCore => new(string.Empty, new Dictionary<string, List<AuthProperty>>());

		protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options) =>
			throw new NotSupportedException();

		protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) =>
			Task.CompletedTask;
	}
}
