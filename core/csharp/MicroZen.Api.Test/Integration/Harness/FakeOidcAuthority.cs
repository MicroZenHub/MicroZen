using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using JwtHandler = System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler;

namespace MicroZen.Core.Api.Test.Integration.Harness;

/// <summary>
/// What a minted test token should look like. Every field maps to one thing token validation checks,
/// so a test changes exactly one field to prove exactly one rule.
/// </summary>
public sealed record TokenSpec
{
	public required string Issuer { get; init; }
	public required string ClientId { get; init; }
	/// <summary>"id" tokens carry the client in <c>aud</c>; "access" tokens carry it in <c>client_id</c> (Cognito shape).</summary>
	public string TokenUse { get; init; } = "access";
	public string? Audience { get; init; }
	public string? Scope { get; init; } = "api/read";
	public string? Subject { get; init; } = "user-1";
	public DateTime Now { get; init; } = DateTime.UtcNow;
	public TimeSpan Lifetime { get; init; } = TimeSpan.FromMinutes(10);
	/// <summary>Offset of <c>nbf</c> from now. Negative = already valid.</summary>
	public TimeSpan NotBeforeOffset { get; init; } = TimeSpan.FromMinutes(-1);
	public string Algorithm { get; init; } = SecurityAlgorithms.RsaSha256;
}

/// <summary>
/// A local, in-process OIDC authority: serves discovery + JWKS from an <see cref="HttpMessageHandler"/>
/// (plugged into JwtBearer's backchannel) and mints signed tokens. No network, no ports.
/// </summary>
public sealed class FakeOidcAuthority(string issuer)
{
	private readonly object _gate = new();
	private readonly List<RsaSecurityKey> _keys = [];
	private int _next;

	public string Issuer { get; } = issuer;

	/// <summary>Number of times JWKS was fetched. Lets tests see that a cache refresh really happened.</summary>
	public int JwksRequests { get; private set; }

	public HttpMessageHandler Handler => new AuthorityHandler(this);

	/// <summary>Adds a new signing key and returns it. The newest key signs new tokens.</summary>
	public RsaSecurityKey AddKey()
	{
		lock (_gate)
		{
			var key = new RsaSecurityKey(RSA.Create(2048)) { KeyId = $"key-{++_next}" };
			_keys.Add(key);
			return key;
		}
	}

	/// <summary>Key rotation: new key published; the old one is dropped from JWKS.</summary>
	public RsaSecurityKey Rotate()
	{
		lock (_gate) _keys.Clear();
		return AddKey();
	}

	public RsaSecurityKey CurrentKey { get { lock (_gate) return _keys[^1]; } }

	/// <summary>A key the authority never publishes, with the same <c>kid</c> as the real one (forged signature).</summary>
	public RsaSecurityKey ForgedKey() => new(RSA.Create(2048)) { KeyId = CurrentKey.KeyId };

	public string Mint(TokenSpec spec, SecurityKey? signWith = null)
	{
		var claims = new Dictionary<string, object> { ["token_use"] = spec.TokenUse };
		if (spec.Subject is not null) claims["sub"] = spec.Subject;
		if (spec.TokenUse == "access") claims["client_id"] = spec.ClientId;
		if (spec.Scope is not null) claims["scope"] = spec.Scope;

		var descriptor = new SecurityTokenDescriptor
		{
			Issuer = spec.Issuer,
			Audience = spec.Audience ?? (spec.TokenUse == "id" ? spec.ClientId : null),
			IssuedAt = spec.Now,
			NotBefore = spec.Now + spec.NotBeforeOffset,
			Expires = spec.Now + spec.Lifetime,
			Claims = claims,
			SigningCredentials = new SigningCredentials(signWith ?? CurrentKey, spec.Algorithm),
		};
		// Expired tokens need Expires < now; the handler rejects Expires <= NotBefore, so keep nbf below both.
		if (descriptor.Expires <= descriptor.NotBefore) descriptor.NotBefore = descriptor.Expires - TimeSpan.FromMinutes(1);
		return new JwtHandler().CreateEncodedJwt(descriptor);
	}

	/// <summary>An unsigned token with <c>alg=none</c>.</summary>
	public static string MintUnsigned(TokenSpec spec)
	{
		static string B64(object o) => Base64UrlEncoder.Encode(JsonSerializer.Serialize(o));
		var now = new DateTimeOffset(spec.Now);
		var payload = new Dictionary<string, object?>
		{
			["iss"] = spec.Issuer,
			["client_id"] = spec.ClientId,
			["token_use"] = spec.TokenUse,
			["scope"] = spec.Scope,
			["sub"] = spec.Subject,
			["nbf"] = now.Add(spec.NotBeforeOffset).ToUnixTimeSeconds(),
			["exp"] = now.Add(spec.Lifetime).ToUnixTimeSeconds(),
		};
		return $"{B64(new { alg = "none", typ = "JWT" })}.{B64(payload)}.";
	}

	private string Discovery() => JsonSerializer.Serialize(new
	{
		issuer = Issuer,
		jwks_uri = Issuer + "/.well-known/jwks.json",
		id_token_signing_alg_values_supported = new[] { "RS256" },
	});

	private string Jwks()
	{
		lock (_gate)
		{
			JwksRequests++;
			var set = new JsonWebKeySet();
			foreach (var key in _keys)
				set.Keys.Add(JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(key.Rsa.ExportParameters(false)) { KeyId = key.KeyId }));
			return JsonSerializer.Serialize(new { keys = set.Keys.Select(k => new { kty = k.Kty, kid = k.Kid, use = "sig", alg = "RS256", n = k.N, e = k.E }) });
		}
	}

	private sealed class AuthorityHandler(FakeOidcAuthority authority) : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var url = request.RequestUri!.ToString();
			var body = url == authority.Issuer + "/.well-known/openid-configuration" ? authority.Discovery()
				: url == authority.Issuer + "/.well-known/jwks.json" ? authority.Jwks()
				: null;
			return Task.FromResult(body is null
				? new HttpResponseMessage(HttpStatusCode.NotFound)
				: new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
		}
	}
}
