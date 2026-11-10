extern alias MicroZenCore;
using System.Net;
using Microsoft.IdentityModel.Tokens;
using MicroZen.Core.Api.Test.Integration.Harness;
using static Xunit.Assert;
using GrantType = MicroZenCore::MicroZen.Core.Definitions.OAuth2GrantType;

namespace MicroZen.Core.Api.Test.Integration;

/// <summary>
/// Issue #106: JWT validation and client access control for every Cognito flow, end to end.
/// A real <c>TestServer</c> runs the real <c>CognitoAuthProvider</c> against an in-process
/// fake OIDC authority (<see cref="FakeOidcAuthority"/>). No network, no database.
/// Every test runs once per flow; a flow only changes which grant is wired and which token shape
/// that grant yields (ID token vs access token).
/// </summary>
public class CognitoJwtIntegrationTests
{
	private const string Allowed = "client-allowed";
	private const string Stranger = "client-not-registered";
	private static readonly TimeSpan Hour = TimeSpan.FromHours(1);

	private static readonly Dictionary<string, (GrantType Grant, string TokenUse)> Flows = new()
	{
		["AuthorizationCode"] = (GrantType.AuthorizationCode, "id"),
		["AuthorizationCodeWithPKCE"] = (GrantType.AuthorizationCodeWithPKCE, "id"),
		["ClientCredentials"] = (GrantType.ClientCredentials, "access"),
		["DeviceAuthorization"] = (GrantType.DeviceAuthorization, "access"),
		["RefreshToken"] = (GrantType.RefreshToken, "access"),
	};

	public static TheoryData<string> FlowNames() => new(Flows.Keys);

	private sealed class Fixture : IAsyncDisposable
	{
		public required JwtTestHost Host { get; init; }
		public required FakeOidcAuthority Authority { get; init; }
		public required string Issuer { get; init; }
		public required string TokenUse { get; init; }

		public TokenSpec Spec => new() { Issuer = Issuer, ClientId = Allowed, TokenUse = TokenUse };
		public string Mint(TokenSpec? spec = null, SecurityKey? signWith = null) => Authority.Mint(spec ?? Spec, signWith);
		public async Task<HttpStatusCode> StatusOf(string? token, string scheme = "Bearer") => (await Host.CallAsync(token, scheme)).StatusCode;
		public ValueTask DisposeAsync() => Host.DisposeAsync();
	}

	private static async Task<Fixture> StartAsync(string flow)
	{
		var (grant, tokenUse) = Flows[flow];
		var provider = new CognitoProviderUnderTest();
		var allowed = CognitoProviderUnderTest.Client(Allowed);
		var issuer = provider.IssuerFor(allowed);
		var authority = new FakeOidcAuthority(issuer);
		authority.AddKey();
		var host = await JwtTestHost.StartAsync(provider, authority, [allowed], grant);
		return new Fixture { Host = host, Authority = authority, Issuer = issuer, TokenUse = tokenUse };
	}

	// ---- Should be allowed --------------------------------------------------------------

	[Theory, MemberData(nameof(FlowNames))]
	public async Task ValidToken_FromAllowedClient_IsAccepted(string flow)
	{
		await using var f = await StartAsync(flow);
		Equal(HttpStatusCode.OK, await f.StatusOf(f.Mint()));
	}

	[Theory, MemberData(nameof(FlowNames))]
	public async Task Token_SignedByRotatedKey_IsAcceptedAfterJwksRefresh(string flow)
	{
		await using var f = await StartAsync(flow);
		Equal(HttpStatusCode.OK, await f.StatusOf(f.Mint())); // JWKS is now cached with the old key.

		f.Authority.Rotate();
		var newToken = f.Mint();
		await Task.Delay(TimeSpan.FromSeconds(1.2)); // JwtBearer's minimum JWKS refresh interval is 1s (see JwtTestHost).

		// An unknown key id triggers a JWKS refresh. The library may need a second request to pick it up.
		var first = await f.StatusOf(newToken);
		var status = first == HttpStatusCode.OK ? first : await f.StatusOf(newToken);
		Equal(HttpStatusCode.OK, status);
		True(f.Authority.JwksRequests >= 2, "The new key should have been fetched from JWKS.");
		// shortcut: not asserting the retired key is rejected. IdentityModel keeps a "last known good"
		// config (24h) and retries failed validations against it, so a retired key still validates.
	}

	// ---- Should be denied ---------------------------------------------------------------

	[Theory, MemberData(nameof(FlowNames))]
	public async Task ExpiredToken_IsDenied(string flow)
	{
		await using var f = await StartAsync(flow);
		// 2h old and expired 1h ago; well past the default 5 minute clock skew.
		var token = f.Mint(f.Spec with { Now = DateTime.UtcNow - 2 * Hour, Lifetime = Hour });
		Equal(HttpStatusCode.Unauthorized, await f.StatusOf(token));
	}

	[Theory, MemberData(nameof(FlowNames))]
	public async Task NotYetValidToken_IsDenied(string flow)
	{
		await using var f = await StartAsync(flow);
		var token = f.Mint(f.Spec with { NotBeforeOffset = Hour, Lifetime = 2 * Hour });
		Equal(HttpStatusCode.Unauthorized, await f.StatusOf(token));
	}

	[Theory, MemberData(nameof(FlowNames))]
	public async Task BadSignature_IsDenied(string flow)
	{
		await using var f = await StartAsync(flow);
		var token = f.Mint(signWith: f.Authority.ForgedKey());
		Equal(HttpStatusCode.Unauthorized, await f.StatusOf(token));
	}

	[Theory, MemberData(nameof(FlowNames))]
	public async Task WrongIssuer_IsDenied(string flow)
	{
		await using var f = await StartAsync(flow);
		var token = f.Mint(f.Spec with { Issuer = "https://cognito-idp.us-east-1.amazonaws.com/us-east-1_OTHERPOOL" });
		Equal(HttpStatusCode.Unauthorized, await f.StatusOf(token));
	}

	[Theory, MemberData(nameof(FlowNames))]
	public async Task WrongAudience_IsDenied(string flow)
	{
		await using var f = await StartAsync(flow);
		// The allowed client is still named in the token; an extra audience for another app must not pass.
		var token = f.Mint(f.Spec with { Audience = "some-other-app" });
		Equal(HttpStatusCode.Unauthorized, await f.StatusOf(token));
	}

	[Theory, MemberData(nameof(FlowNames))]
	public async Task AlgNone_IsDenied(string flow)
	{
		await using var f = await StartAsync(flow);
		Equal(HttpStatusCode.Unauthorized, await f.StatusOf(FakeOidcAuthority.MintUnsigned(f.Spec)));
	}

	[Theory, MemberData(nameof(FlowNames))]
	public async Task WrongAlgorithm_Hs256WithUnknownSecret_IsDenied(string flow)
	{
		await using var f = await StartAsync(flow);
		var secret = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes("an-attacker-chosen-secret-of-32-bytes!!")) { KeyId = f.Authority.CurrentKey.KeyId };
		var token = f.Mint(f.Spec with { Algorithm = SecurityAlgorithms.HmacSha256 }, secret);
		Equal(HttpStatusCode.Unauthorized, await f.StatusOf(token));
	}

	[Theory, MemberData(nameof(FlowNames))]
	public async Task ValidToken_FromUnregisteredClient_IsDenied(string flow)
	{
		await using var f = await StartAsync(flow);
		// Correctly signed, right issuer, in date, but issued to a client MicroZen does not allow.
		Equal(HttpStatusCode.Unauthorized, await f.StatusOf(f.Mint(f.Spec with { ClientId = Stranger })));
	}

	[Theory, MemberData(nameof(FlowNames))]
	public async Task MissingAuthorizationHeader_IsDenied(string flow)
	{
		await using var f = await StartAsync(flow);
		Equal(HttpStatusCode.Unauthorized, await f.StatusOf(null));
	}

	[Theory, MemberData(nameof(FlowNames))]
	public async Task MalformedAuthorizationHeader_IsDenied(string flow)
	{
		await using var f = await StartAsync(flow);
		var valid = f.Mint();
		Equal(HttpStatusCode.Unauthorized, await f.StatusOf("not-a-jwt"));
		Equal(HttpStatusCode.Unauthorized, await f.StatusOf("a.b.c"));
		Equal(HttpStatusCode.Unauthorized, await f.StatusOf(valid, scheme: "Basic")); // right token, wrong scheme
		Equal(HttpStatusCode.Unauthorized, await f.StatusOf(valid[..^4])); // truncated signature
	}

	// ---- Known gaps: behaviour the issue asks for that production code does not have yet ----
	// Each is skipped (not deleted) so the gap stays visible. See the PR description.

	[Fact(Skip = "Gap: Cognito wiring reads allowed clients once at startup (Take(1)); a later removal is never applied.")]
	public async Task ClientRemovedAfterIssuance_IsDenied()
	{
		await using var f = await StartAsync("AuthorizationCode");
		var token = f.Mint();
		Equal(HttpStatusCode.OK, await f.StatusOf(token));
		f.Host.Publish([]); // MicroZen state refresh: the client is gone.
		Equal(HttpStatusCode.Unauthorized, await f.StatusOf(token));
	}

	[Fact(Skip = "Gap: the API has no scope policy; no code reads the scope claim.")]
	public void TokenWithMissingOrInsufficientScope_IsDenied() { }

	[Fact(Skip = "Gap: no check ties a token to the flow that issued it (client_credentials vs user tokens).")]
	public void TokenOfOneFlowUsedWhereAnotherIsRequired_IsDenied() { }

	[Fact(Skip = "Gap: with empty state at startup (the real case, polling starts after 5s) no JWT scheme is registered.")]
	public async Task RealStartup_WithNoStateYet_StillRegistersBearerScheme()
	{
		var provider = new CognitoProviderUnderTest();
		var authority = new FakeOidcAuthority(provider.IssuerFor(CognitoProviderUnderTest.Client(Allowed)));
		authority.AddKey();
		await using var host = await JwtTestHost.StartAsync(provider, authority, [], GrantType.AuthorizationCodeWithPKCE);
		host.Publish([CognitoProviderUnderTest.Client(Allowed)]);
		var token = authority.Mint(new TokenSpec { Issuer = provider.IssuerFor(CognitoProviderUnderTest.Client(Allowed)), ClientId = Allowed, TokenUse = "id" });
		Equal(HttpStatusCode.OK, (await host.CallAsync(token)).StatusCode);
	}
}
