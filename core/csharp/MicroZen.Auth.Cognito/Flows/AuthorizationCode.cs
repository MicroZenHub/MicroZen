using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using MicroZen.Core.State;

namespace MicroZen.Auth.Cognito.Flows;

/// <summary>
/// Handles all code related to the OAuth2 AuthorizationCode or AuthorizationCodeWithPKCE grant types
/// </summary>
public static class AuthorizationCode
{
	/// <summary>
	/// Adds the JWT Bearer token authentication for the OAuth2 "authorization_code" grant type
	/// </summary>
	/// <param name="builder"><see cref="AuthenticationBuilder"/></param>
	/// <param name="services"><see cref="IServiceCollection" /></param>
	public static void AddAuthorizationCodeJwtBearer(this AuthenticationBuilder builder, IServiceCollection services)
	{
		var oAuth2State = services.BuildServiceProvider().GetRequiredService<MicroZenOAuth2State>();
		builder.AddJwtBearer(options =>
			LazyBearer.Configure(options, oAuth2State, _ => []));
	}
}
