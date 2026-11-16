using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace MicroZen.Auth.Cognito.Flows;

/// <summary>
/// Ties a token to the kind of flow the API is wired for, so a user token cannot stand in for a
/// machine token and the reverse. Fails closed: a missing or unexpected claim rejects the token.
/// </summary>
/// <remarks>
/// Cognito client-credentials tokens are <c>access</c> tokens whose <c>sub</c> is the client id (there is no user).
/// User-flow tokens are <c>id</c> or <c>access</c> tokens whose <c>sub</c> is the user.
/// </remarks>
internal static class TokenUse
{
	/// <summary>Adds the check after any <c>OnTokenValidated</c> handler already set (e.g. <see cref="AllowedClients"/>).</summary>
	public static void Require(JwtBearerOptions options, bool machineToken)
	{
		var previous = options.Events.OnTokenValidated;
		options.Events.OnTokenValidated = async context =>
		{
			await previous(context);
			if (context.Result?.Failure is null && !IsExpectedKind(context.Principal, machineToken))
				context.Fail(machineToken
					? "Only client-credentials (machine) access tokens are allowed for this API."
					: "Only user tokens (id or access, with a user subject) are allowed for this API.");
		};
	}

	private static bool IsExpectedKind(ClaimsPrincipal? principal, bool machineToken)
	{
		var tokenUse = principal?.FindFirst("token_use")?.Value;
		// The JWT handler may map "sub" to NameIdentifier, so accept both.
		var sub = (principal?.FindFirst("sub") ?? principal?.FindFirst(ClaimTypes.NameIdentifier))?.Value;
		var clientId = principal?.FindFirst("client_id")?.Value;
		var isMachineSubject = string.IsNullOrEmpty(sub) || (clientId is not null && sub == clientId);

		return machineToken
			? tokenUse == "access" && isMachineSubject
			: (tokenUse == "id" || tokenUse == "access") && !isMachineSubject;
	}
}
