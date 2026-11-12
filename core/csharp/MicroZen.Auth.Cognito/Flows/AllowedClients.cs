using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using MicroZen.Core.State;

namespace MicroZen.Auth.Cognito.Flows;

/// <summary>
/// Rejects tokens that were not issued to a client MicroZen allows.
/// </summary>
/// <remarks>
/// Cognito ID tokens name the client in <c>aud</c>; access tokens (including client-credentials
/// tokens) have no <c>aud</c> and name it in <c>client_id</c>. Standard audience validation cannot
/// handle both, so it stays off and this check runs after the token is validated.
/// The allowed list is read from the latest MicroZen state on every request, so a removed or disabled
/// client loses access on the next poll. The poll interval is the maximum revocation delay.
/// An empty or missing list allows nothing (fail closed).
/// </remarks>
internal static class AllowedClients
{
	public static void Require(JwtBearerOptions options, MicroZenOAuth2State oAuth2State, string userPoolId, string region, IEnumerable<string> initialClientIds)
	{
		var allowed = initialClientIds.ToHashSet(StringComparer.Ordinal);
		oAuth2State.State.Subscribe(state =>
		{
			if (state == null) return;
			allowed = state.Value.ClientCredentials
				.Where(c => c.UserPoolId == userPoolId && c.Region == region)
				.Select(c => c.ClientId)
				.ToHashSet(StringComparer.Ordinal);
		});
		options.Events = new JwtBearerEvents
		{
			OnTokenValidated = context =>
			{
				if (!IsAllowed(context.Principal, Volatile.Read(ref allowed)))
					context.Fail("The token was not issued to a client that is allowed for this API.");
				return Task.CompletedTask;
			},
		};
	}

	// Every client claim the token carries must be allowed, and it must carry at least one.
	private static bool IsAllowed(ClaimsPrincipal? principal, HashSet<string> allowed)
	{
		var claims = principal?.FindAll("client_id").Concat(principal.FindAll("aud")).ToList() ?? [];
		return claims.Count > 0 && claims.All(c => allowed.Contains(c.Value));
	}
}
