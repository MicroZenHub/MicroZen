using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace MicroZen.Auth.Cognito.Flows;

/// <summary>
/// Rejects tokens that were not issued to a client MicroZen allows.
/// </summary>
/// <remarks>
/// Cognito ID tokens name the client in <c>aud</c>; access tokens (including client-credentials
/// tokens) have no <c>aud</c> and name it in <c>client_id</c>. Standard audience validation cannot
/// handle both, so it stays off and this check runs after the token is validated.
/// </remarks>
internal static class AllowedClients
{
	public static void Require(JwtBearerOptions options, Func<string?, IEnumerable<string>> allowedClientIdsFor)
	{
		options.Events = new JwtBearerEvents
		{
			OnTokenValidated = context =>
			{
				var allowed = allowedClientIdsFor(context.SecurityToken.Issuer).ToHashSet(StringComparer.Ordinal);
				if (!IsAllowed(context.Principal, allowed))
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
