extern alias MicroZenCore;
using Microsoft.Extensions.DependencyInjection;
using MicroZen.Auth.Cognito;
using PackageGrantType = MicroZenCore::MicroZen.Core.Definitions.OAuth2GrantType;

namespace MicroZen.Core.Api.Test.Definitions;

/// <summary>
/// Wires only the Cognito provider's JWT bearer validation (no gRPC client or config),
/// so tests can supply their own <c>MicroZenOAuth2State</c>.
/// </summary>
internal static class CognitoWiring
{
	public static void Wire(IServiceCollection services, params PackageGrantType[] grantTypes)
		=> new CognitoAuthProvider().AddAuthentication(services.AddAuthentication(), services, grantTypes);
}
