using System.Collections.Concurrent;
using System.Reactive.Linq;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using MicroZen.Core.State;

namespace MicroZen.Auth.Cognito.Flows;

/// <summary>
/// Registers ONE JWT bearer scheme at startup and reads the allowed user pools and clients from
/// <see cref="MicroZenOAuth2State"/> on every request. The state is empty until the first poll of the
/// MicroZen authority (5s after start), so it cannot be read at registration time. While it is empty,
/// no issuer is valid and every token is rejected (fail closed).
/// </summary>
internal static class LazyBearer
{
	public static string IssuerOf(UserPoolClients pool) =>
		$"https://cognito-idp.{pool.Region}.amazonaws.com/{pool.UserPoolId}";

	/// <summary>The user pools in the latest state; empty when there is no state.</summary>
	public static List<UserPoolClients> CurrentPools(MicroZenOAuth2State state)
	{
		// State is backed by a BehaviorSubject, so subscribing yields the latest value synchronously.
		var current = state.State.Take(1).Wait();
		if (current is not { } value) return [];
		return value.ClientCredentials.GroupBy(
			c => new { c.UserPoolId, c.Region },
			c => new { c.ClientId, c.ClientSecret },
			(pool, clients) =>
			{
				var list = clients.ToList();
				return new UserPoolClients(pool.UserPoolId, pool.Region,
					list.Select(x => x.ClientId).ToArray(), list.Select(x => x.ClientSecret).ToArray());
			}).ToList();
	}

	/// <summary>Configures <paramref name="options"/> to validate against the latest state on each request.</summary>
	/// <param name="options">The bearer options to configure.</param>
	/// <param name="state">The MicroZen state, read per request.</param>
	/// <param name="extraKeys">Keys accepted for a pool besides the ones its OIDC discovery document publishes.</param>
	public static void Configure(
		JwtBearerOptions options,
		MicroZenOAuth2State state,
		Func<UserPoolClients, IEnumerable<SecurityKey>> extraKeys)
	{
		var managers = new ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>>();

		UserPoolClients? PoolFor(string? issuer) =>
			CurrentPools(state).FirstOrDefault(p => string.Equals(IssuerOf(p), issuer, StringComparison.Ordinal));

		options.TokenValidationParameters = new TokenValidationParameters
		{
			IssuerValidator = (issuer, _, _) => PoolFor(issuer) is not null
				? issuer
				: throw new SecurityTokenInvalidIssuerException("The token issuer is not an allowed user pool."),
			IssuerSigningKeyResolver = (_, token, kid, _) =>
				PoolFor(token.Issuer) is { } pool
					? DiscoveredKeys(options, managers, pool, kid).Concat(extraKeys(pool))
					: [],
			ValidateIssuerSigningKey = true,
			ValidateIssuer = true,
			ValidateLifetime = true,
			ValidateAudience = false,
		};
		// The handler needs a configuration source; keys and issuers come from the resolver above, so it is empty.
		options.Configuration = new OpenIdConnectConfiguration();
		AllowedClients.Require(options, issuer => PoolFor(issuer)?.ClientIds ?? []);
	}

	// The signing keys a pool's OIDC discovery document publishes, cached per pool and refreshed on an unknown kid.
	private static IEnumerable<SecurityKey> DiscoveredKeys(
		JwtBearerOptions options,
		ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>> managers,
		UserPoolClients pool,
		string? kid)
	{
		var issuer = IssuerOf(pool);
		var manager = managers.GetOrAdd(issuer, _ => new ConfigurationManager<OpenIdConnectConfiguration>(
			issuer + "/.well-known/openid-configuration",
			new OpenIdConnectConfigurationRetriever(),
			// options.Backchannel is only built when MetadataAddress is set, so build the client here.
			new HttpDocumentRetriever(options.Backchannel ?? new HttpClient(options.BackchannelHttpHandler ?? new HttpClientHandler())
			{
				Timeout = options.BackchannelTimeout,
			})
			{ RequireHttps = options.RequireHttpsMetadata })
		{
			RefreshInterval = options.RefreshInterval,
			AutomaticRefreshInterval = options.AutomaticRefreshInterval,
		});
		// shortcut: blocks a thread while keys are fetched, because the key resolver is synchronous. Cached after the first call.
		var keys = manager.GetConfigurationAsync(CancellationToken.None).GetAwaiter().GetResult().SigningKeys;
		if (kid is null || keys.Any(k => k.KeyId == kid)) return keys;
		manager.RequestRefresh(); // key rotation: refetch (rate-limited by RefreshInterval)
		return manager.GetConfigurationAsync(CancellationToken.None).GetAwaiter().GetResult().SigningKeys;
	}
}
