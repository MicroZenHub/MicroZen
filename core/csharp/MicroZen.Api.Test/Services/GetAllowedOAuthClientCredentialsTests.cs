using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using MicroZen.Api.Context;
using MicroZen.Api.Entities;
using MicroZen.Api.Entities.AuthCredentials;
using MicroZen.Grpc.V1;
using static Xunit.Assert;
using ClientsGrpcService = MicroZen.Api.Services.ClientsGrpcService;

namespace MicroZen.Core.Api.Test.Services;

/// <summary>
/// Tests for <see cref="ClientsGrpcService.GetAllowedOAuthClientCredentials"/>,
/// covering issue #53: the method must return only the requested client's
/// allowed OAuth2 credentials, not every client's across the database.
///
/// Before the fix, <c>SelectMany(c =&gt; c.AllowedClientOAuth2Credentials)</c>
/// flattened every client's allowed-credential rows and ignored
/// <c>request.Id</c>, a cross-tenant leak; the dead <c>is null</c> check on the
/// <c>ToListAsync()</c> result was unreachable. The fix loads the single
/// requested client with <c>FirstOrDefaultAsync(c =&gt; c.Id == request.Id)</c>
/// and throws <see cref="StatusCode.NotFound"/> when it is missing, matching the
/// sibling <c>AuthorizeClients</c>/<c>DisallowClients</c> methods.
///
/// Follows the repo's InMemory test convention (see
/// <c>Context/MicroZenContextTests.cs</c>): EF Core InMemory provider, the
/// <c>EncryptionKey</c> environment variable set in the constructor and cleared
/// in <see cref="Dispose"/>, a write context / fresh read context round-trip,
/// and the shared <see cref="TestServerCallContext"/> double.
/// </summary>
public class GetAllowedOAuthClientCredentialsTests : IDisposable
{
	private readonly DbContextOptions<MicroZenContext> _options;

	public GetAllowedOAuthClientCredentialsTests()
	{
		// Required by MicroZenContext.OnModelCreating (column encryption).
		Environment.SetEnvironmentVariable("EncryptionKey", "1234567890");

		// A unique database name isolates this fixture from other test classes.
		_options = new DbContextOptionsBuilder<MicroZenContext>()
			.UseInMemoryDatabase(databaseName: $"GetAllowedOAuthClientCredentialsTests_{Guid.NewGuid():N}")
			.Options;
	}

	private static Client NewClient(string name) => new()
	{
		Name = name,
		Type = ClientType.Api,
		OrganizationId = 1,
		Description = name
	};

	private static OAuth2ClientCredentials NewCredential(string oauthClientId) => new()
	{
		OAuth2GrantType = GrantType.ClientCredentials,
		OAuth2ClientId = oauthClientId,
		// Non-null: OAuth2Credentials.ToMessage() maps this into a protobuf
		// string field (ClientSecret), which rejects null values.
		OAuth2ClientSecret = $"{oauthClientId}-secret"
	};

	[Fact]
	public async Task GetAllowedOAuthClientCredentials_ReturnsOnlyRequestedClientsSet()
	{
		// GIVEN two clients, each with its own distinct allowed credentials.
		int clientAId;
		await using (var writeContext = new MicroZenContext(_options))
		{
			var clientA = NewClient("Client A");
			clientA.AllowedClientOAuth2Credentials.Add(NewCredential("a-cred-1"));
			clientA.AllowedClientOAuth2Credentials.Add(NewCredential("a-cred-2"));

			var clientB = NewClient("Client B");
			clientB.AllowedClientOAuth2Credentials.Add(NewCredential("b-cred-1"));

			writeContext.Clients.Add(clientA);
			writeContext.Clients.Add(clientB);
			await writeContext.SaveChangesAsync();
			clientAId = clientA.Id;
		}

		// WHEN asking for Client A's allowed credentials.
		await using var readContext = new MicroZenContext(_options);
		var service = new ClientsGrpcService(readContext);
		var response = await service.GetAllowedOAuthClientCredentials(
			new ClientRequest { Id = clientAId }, TestServerCallContext.Create());

		// THEN only Client A's credentials come back, never Client B's.
		Equal(2, response.Credentials.Count);
		var clientIds = response.Credentials.Select(c => c.ClientId).ToList();
		Contains("a-cred-1", clientIds);
		Contains("a-cred-2", clientIds);
		DoesNotContain("b-cred-1", clientIds);
	}

	[Fact]
	public async Task GetAllowedOAuthClientCredentials_ClientWithNoAllowedCredentials_ReturnsEmpty()
	{
		// GIVEN a client that exists but has no allowed credentials.
		int clientId;
		await using (var writeContext = new MicroZenContext(_options))
		{
			var client = NewClient("Lonely Client");
			writeContext.Clients.Add(client);
			await writeContext.SaveChangesAsync();
			clientId = client.Id;
		}

		// WHEN asking for its allowed credentials.
		await using var readContext = new MicroZenContext(_options);
		var service = new ClientsGrpcService(readContext);
		var response = await service.GetAllowedOAuthClientCredentials(
			new ClientRequest { Id = clientId }, TestServerCallContext.Create());

		// THEN an empty (non-error) result is returned.
		Empty(response.Credentials);
	}

	[Fact]
	public async Task GetAllowedOAuthClientCredentials_ClientNotFound_ThrowsNotFound()
	{
		// GIVEN an empty database (no client with the requested Id).
		await using var readContext = new MicroZenContext(_options);
		var service = new ClientsGrpcService(readContext);

		// WHEN asking for a client that does not exist.
		var exception = await ThrowsAsync<RpcException>(() =>
			service.GetAllowedOAuthClientCredentials(
				new ClientRequest { Id = 999 }, TestServerCallContext.Create()));

		// THEN a NotFound RpcException is thrown.
		Equal(StatusCode.NotFound, exception.StatusCode);
	}

	public void Dispose()
	{
		GC.SuppressFinalize(this);
		Environment.SetEnvironmentVariable("EncryptionKey", null);
	}
}
