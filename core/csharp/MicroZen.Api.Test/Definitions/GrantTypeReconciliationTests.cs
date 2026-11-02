extern alias MicroZenOAuth2;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MicroZen.Api.Context;
using MicroZen.Api.Entities;
using MicroZen.Api.Entities.AuthCredentials;
using MicroZen.Grpc.V1;
using static Xunit.Assert;
// MicroZen.OAuth2 compiles the same client.proto as MicroZen.Api, so the test
// csproj aliases the transitive OAuth2 assembly to `MicroZenOAuth2` (see the
// AliasOAuth2GrpcTypes target) to keep its MicroZen.Grpc.V1.* copies out of the
// global namespace. Reach the package's string-valued grant-type class through
// that same extern alias.
using PackageGrantType = MicroZenOAuth2::MicroZen.OAuth2.Definitions.OAuth2GrantType;
using MicroZenOAuth2::MicroZen.OAuth2.Providers.Cognito;

namespace MicroZen.Core.Api.Test.Definitions;

/// <summary>
/// Tests for issue #68 — reconcile grant-type enums to the canonical eight-member
/// set (backend side: proto <see cref="GrantType"/> + package
/// <c>OAuth2GrantType</c>).
///
/// Follows the reference shape in <c>Context/MicroZenContextTests.cs</c>: a unique
/// in-memory database per fixture, <c>EncryptionKey</c> set in the constructor and
/// cleared in <see cref="Dispose"/>, and GIVEN / WHEN / THEN comments.
///
/// The proto <see cref="GrantType"/> numbers are PERSISTED (the
/// <c>OAuth2ClientConfigs.OAuth2GrantType</c> integer column) and sent on the wire,
/// so numbers 0–6 must never move and <c>RefreshToken</c> is appended as 7. The
/// package <c>OAuth2GrantType</c> is a string-valued class (never persisted); it is
/// aligned to the canonical set by name.
/// </summary>
public class GrantTypeReconciliationTests : IDisposable
{
	private readonly DbContextOptions<MicroZenContext> _options;

	public GrantTypeReconciliationTests()
	{
		// Required by MicroZenContext.OnModelCreating (column encryption).
		Environment.SetEnvironmentVariable("EncryptionKey", "1234567890");

		// A unique database name isolates this fixture from other test classes.
		_options = new DbContextOptionsBuilder<MicroZenContext>()
			.UseInMemoryDatabase(databaseName: $"GrantTypeReconciliationTests_{Guid.NewGuid():N}")
			.Options;
	}

	// --- Wire/number stability (locks the persisted integer values) ---------

	[Theory]
	[InlineData(GrantType.AuthorizationCode, 0)]
	[InlineData(GrantType.AuthorizationCodeWithPkce, 1)]
	[InlineData(GrantType.ClientCredentials, 2)]
	[InlineData(GrantType.DeviceAuthorization, 3)]
	[InlineData(GrantType.Implicit, 4)]
	[InlineData(GrantType.Hybrid, 5)]
	[InlineData(GrantType.ResourceOwnerPassword, 6)]
	[InlineData(GrantType.RefreshToken, 7)]
	public void ProtoGrantType_HasCanonicalNumber(GrantType member, int expectedNumber)
	{
		// GIVEN a canonical proto GrantType member.
		// WHEN its integer value is read (this is the value persisted in the DB column).
		// THEN it equals the canonical number — pinning 0–6 against reordering and RefreshToken at 7.
		Equal(expectedNumber, (int)member);
	}

	[Fact]
	public void ProtoGrantType_HasExactlyEightCanonicalMembers()
	{
		// GIVEN the proto GrantType enum.
		// WHEN its declared members are enumerated.
		var members = Enum.GetValues<GrantType>();

		// THEN exactly the canonical eight are present (guards against a stray addition/removal).
		Equal(8, members.Length);
		Contains(GrantType.RefreshToken, members);
	}

	// --- Persistence round-trip (EF InMemory, same provider the tests use) ---

	[Theory]
	[InlineData(GrantType.AuthorizationCode)]
	[InlineData(GrantType.AuthorizationCodeWithPkce)]
	[InlineData(GrantType.ClientCredentials)]
	[InlineData(GrantType.DeviceAuthorization)]
	[InlineData(GrantType.Implicit)]
	[InlineData(GrantType.Hybrid)]
	[InlineData(GrantType.ResourceOwnerPassword)]
	[InlineData(GrantType.RefreshToken)]
	public async Task OAuth2ClientCredentials_GrantType_RoundTrips(GrantType grantType)
	{
		// GIVEN a Client with one OAuth2 credential carrying the given grant type.
		await using (var writeContext = new MicroZenContext(_options))
		{
			writeContext.Clients.Add(new Client
			{
				Name = $"Client {grantType}",
				Type = ClientType.Api,
				OAuth2Credentials = new List<OAuth2ClientCredentials>
				{
					new()
					{
						OAuth2GrantType = grantType,
						OAuth2ClientId = "client-id",
						Environment = "Test",
					},
				},
			});
			await writeContext.SaveChangesAsync();
		}

		// WHEN the credential is read back through a fresh context.
		await using var readContext = new MicroZenContext(_options);
		var stored = await readContext
			.Set<OAuth2ClientCredentials>()
			.SingleAsync(c => c.OAuth2ClientId == "client-id");

		// THEN the persisted grant type round-trips unchanged (confirms the new member stores/reads as its number).
		Equal(grantType, stored.OAuth2GrantType);
	}

	// --- Package API surface (aligned by name to the proto) ------------------

	[Fact]
	public void PackageOAuth2GrantType_DeclaresCanonicalMembers()
	{
		// GIVEN the package OAuth2GrantType class.
		// THEN it exposes the full canonical set, aligned by name to the proto.
		Equal("authorization_code", PackageGrantType.AuthorizationCode.ToString());
		Equal("authorization_code_pkce", PackageGrantType.AuthorizationCodeWithPKCE.ToString());
		Equal("client_credentials", PackageGrantType.ClientCredentials.ToString());
		Equal("device_authorization", PackageGrantType.DeviceAuthorization.ToString());
		Equal("implicit", PackageGrantType.Implicit.ToString());
		Equal("hybrid", PackageGrantType.Hybrid.ToString());
		Equal("password", PackageGrantType.ResourceOwnerPassword.ToString());
		Equal("refresh_token", PackageGrantType.RefreshToken.ToString());
	}

	[Fact]
	public void PackageOAuth2GrantType_ImplementedMembersUnchanged()
	{
		// GIVEN the three implemented flows (AWS Cognito).
		// THEN their string values are unchanged by the reconciliation.
		Equal("authorization_code", PackageGrantType.AuthorizationCode.ToString());
		Equal("authorization_code_pkce", PackageGrantType.AuthorizationCodeWithPKCE.ToString());
		Equal("client_credentials", PackageGrantType.ClientCredentials.ToString());
	}

	// --- Behavior unchanged (negative): declared-but-unimplemented still throws ---

	[Fact]
	public void WiringDeclaredButUnimplementedGrantType_StillThrowsNotImplemented()
	{
		// GIVEN a service collection wired with a declared-but-unimplemented grant type.
		var services = new ServiceCollection();

		// WHEN the Cognito provider is configured with DeviceAuthorization only.
		// THEN it still reaches the existing NotImplementedException path —
		// reconciliation declares the member but adds no runtime support.
		Throws<NotImplementedException>(() =>
			services.AddAWSCognitoMicroZenOAuth2(PackageGrantType.DeviceAuthorization));
	}

	public void Dispose()
	{
		GC.SuppressFinalize(this);
		Environment.SetEnvironmentVariable("EncryptionKey", null);
	}
}
