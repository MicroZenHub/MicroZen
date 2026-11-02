using Microsoft.EntityFrameworkCore;
using MicroZen.Api.Context;
using MicroZen.Api.Entities;
using MicroZen.Api.Entities.AuthCredentials;
using MicroZen.Grpc.V1;
using static Xunit.Assert;
// The service class and its generated gRPC base share the simple name "ClientsGrpcService"
// (MicroZen.Api.Services vs MicroZen.Grpc.V1); alias the concrete service under test.
using ClientsGrpcService = MicroZen.Api.Services.ClientsGrpcService;

namespace MicroZen.Core.Api.Test.Services;

/// <summary>
/// Tests for <see cref="ClientsGrpcService.UpdateClient"/> credential reconciliation (issue #54).
///
/// Follows the reference shape in <c>Context/MicroZenContextTests.cs</c>: a unique
/// in-memory database per fixture, the <c>EncryptionKey</c> set in the constructor and
/// cleared in <see cref="Dispose"/>, and GIVEN / WHEN / THEN comments. The test drives
/// the real <see cref="ClientsGrpcService.UpdateClient"/> end to end so the add-vs-update
/// fix is exercised through the production code path.
///
/// Before the #54 fix, UpdateClient split incoming credentials into two identical
/// projections (both used <c>Any(...)</c> instead of <c>!Any(...)</c> for the add set)
/// and matched on the integer <c>ClientId</c> FK (always 0 on the projection) instead
/// of <c>OAuth2ClientId</c>. New credentials were therefore silently dropped.
/// </summary>
public class ClientsGrpcServiceUpdateClientTests : IDisposable
{
	private const string ExistingOAuth2ClientId = "existing-id";
	private const string BrandNewOAuth2ClientId = "brand-new-id";

	private readonly DbContextOptions<MicroZenContext> _options;

	public ClientsGrpcServiceUpdateClientTests()
	{
		// Required by MicroZenContext.OnModelCreating (column encryption).
		Environment.SetEnvironmentVariable("EncryptionKey", "1234567890");

		// A unique database name isolates this fixture from other test classes.
		_options = new DbContextOptionsBuilder<MicroZenContext>()
			.UseInMemoryDatabase(databaseName: $"ClientsGrpcServiceUpdateClientTests_{Guid.NewGuid():N}")
			.Options;
	}

	/// <summary>
	/// Persists a client that already owns exactly one OAuth2 credential with
	/// <see cref="ExistingOAuth2ClientId"/> and returns the generated client id.
	/// </summary>
	private async Task<int> SeedClientWithOneCredentialAsync()
	{
		await using var context = new MicroZenContext(_options);
		var client = new Client
		{
			Name = "Reconcile Client",
			Type = ClientType.Api,
			Description = "Seeded for update reconciliation",
			OAuth2Credentials = new List<OAuth2ClientCredentials>
			{
				new()
				{
					OAuth2ClientId = ExistingOAuth2ClientId,
					OAuth2ClientSecret = "original-secret",
					OAuth2GrantType = GrantType.ClientCredentials,
					AllowedScopes = "read",
					RequirePkce = false,
				},
			},
		};
		context.Clients.Add(client);
		await context.SaveChangesAsync();
		return client.Id;
	}

	private static OAuthClientCredentials Credential(string clientId, string secret, string scopes) =>
		new()
		{
			ClientId = clientId,
			ClientSecret = secret,
			GrantType = GrantType.ClientCredentials,
			AllowedScopes = scopes,
			RequirePkce = false,
		};

	[Fact]
	public async Task UpdateClient_AddsNewCredentialAndUpdatesExisting()
	{
		// GIVEN a persisted client with one existing OAuth2 credential.
		var clientId = await SeedClientWithOneCredentialAsync();

		// WHEN UpdateClient is called with the existing credential (changed secret/scopes)
		// plus one brand-new credential.
		var request = new ClientMessage
		{
			Id = clientId,
			Name = "Reconcile Client",
			Type = ClientType.Api,
			Description = "Seeded for update reconciliation",
		};
		request.OauthCredentials.Add(Credential(ExistingOAuth2ClientId, "updated-secret", "read write"));
		request.OauthCredentials.Add(Credential(BrandNewOAuth2ClientId, "new-secret", "admin"));

		await using (var actContext = new MicroZenContext(_options))
		{
			var service = new ClientsGrpcService(actContext);
			await service.UpdateClient(request, TestServerCallContext.Create());
		}

		// THEN reading the client back through a fresh context shows both credentials:
		// the existing one updated in place and the brand-new one inserted.
		await using var readContext = new MicroZenContext(_options);
		var stored = await readContext.Clients
			.Include(c => c.OAuth2Credentials)
			.SingleAsync(c => c.Id == clientId);

		NotNull(stored.OAuth2Credentials);
		Equal(2, stored.OAuth2Credentials!.Count);

		var existing = Single(stored.OAuth2Credentials, c => c.OAuth2ClientId == ExistingOAuth2ClientId);
		Equal("updated-secret", existing.OAuth2ClientSecret);
		Equal("read write", existing.AllowedScopes);

		var brandNew = Single(stored.OAuth2Credentials, c => c.OAuth2ClientId == BrandNewOAuth2ClientId);
		Equal("new-secret", brandNew.OAuth2ClientSecret);
		Equal("admin", brandNew.AllowedScopes);
		NotEqual(0, brandNew.Id);
	}

	[Fact]
	public async Task UpdateClient_WithOnlyExistingCredential_AddsNothing()
	{
		// GIVEN a persisted client with one existing OAuth2 credential.
		var clientId = await SeedClientWithOneCredentialAsync();

		// WHEN UpdateClient is called with only that existing credential (updated).
		var request = new ClientMessage
		{
			Id = clientId,
			Name = "Reconcile Client",
			Type = ClientType.Api,
		};
		request.OauthCredentials.Add(Credential(ExistingOAuth2ClientId, "rotated-secret", "read"));

		await using (var actContext = new MicroZenContext(_options))
		{
			var service = new ClientsGrpcService(actContext);
			await service.UpdateClient(request, TestServerCallContext.Create());
		}

		// THEN the count stays at one and the existing credential was updated, not duplicated.
		await using var readContext = new MicroZenContext(_options);
		var stored = await readContext.Clients
			.Include(c => c.OAuth2Credentials)
			.SingleAsync(c => c.Id == clientId);

		NotNull(stored.OAuth2Credentials);
		var onlyCredential = Single(stored.OAuth2Credentials!);
		Equal("rotated-secret", onlyCredential.OAuth2ClientSecret);
	}

	[Fact]
	public async Task UpdateClient_ReturnsMessageReflectingBothCredentials()
	{
		// GIVEN a persisted client with one existing OAuth2 credential.
		var clientId = await SeedClientWithOneCredentialAsync();

		// WHEN UpdateClient is called with the existing plus a brand-new credential.
		var request = new ClientMessage
		{
			Id = clientId,
			Name = "Reconcile Client",
			Type = ClientType.Api,
		};
		request.OauthCredentials.Add(Credential(ExistingOAuth2ClientId, "updated-secret", "read"));
		request.OauthCredentials.Add(Credential(BrandNewOAuth2ClientId, "new-secret", "admin"));

		await using var actContext = new MicroZenContext(_options);
		var service = new ClientsGrpcService(actContext);
		var response = await service.UpdateClient(request, TestServerCallContext.Create());

		// THEN the returned message reflects both credentials (one updated, one added).
		// Note: ClientMessage.ToMessage() maps each OAuthClientCredentials.ClientId
		// from the integer FK (OAuth2ClientCredentials.ClientId), not OAuth2ClientId,
		// so the count is the reliable signal here; the identity-mapping quirk in
		// ToMessage is pre-existing and out of scope for issue #54.
		Equal(2, response.OauthCredentials.Count);
	}

	public void Dispose()
	{
		GC.SuppressFinalize(this);
		Environment.SetEnvironmentVariable("EncryptionKey", null);
	}
}
