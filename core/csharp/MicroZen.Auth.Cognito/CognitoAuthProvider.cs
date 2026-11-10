using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MicroZen.Auth.Cognito.Flows;
using MicroZen.Core;
using MicroZen.Core.Definitions;

namespace MicroZen.Auth.Cognito;

/// <summary>
/// The MicroZen auth provider for AWS Cognito
/// </summary>
public class CognitoAuthProvider : IMicroZenAuthProvider
{
	/// <inheritdoc />
	public string Name => "cognito";

	/// <summary>
	/// Configures Cognito ID bearer token auth according to the rules outlined at:
	/// https://docs.aws.amazon.com/cognito/latest/developerguide/amazon-cognito-user-pools-using-tokens-verifying-a-jwt.html
	/// </summary>
	/// <inheritdoc />
	public void AddAuthentication(AuthenticationBuilder builder, IServiceCollection services, OAuth2GrantType[] grantTypes)
	{
		// RefreshToken is exchanged at the Cognito token endpoint by the client; the tokens it returns
		// are user-pool tokens validated exactly like Authorization Code tokens, so it shares that handler.
		if (grantTypes.Contains(OAuth2GrantType.AuthorizationCode) || grantTypes.Contains(OAuth2GrantType.AuthorizationCodeWithPKCE) || grantTypes.Contains(OAuth2GrantType.RefreshToken))
		{
			builder.AddAuthorizationCodeJwtBearer(services);
		}
		else if (grantTypes.Contains(OAuth2GrantType.ClientCredentials))
		{
			builder.AddClientCredentialsJwtBearer(services);
		}
		else if (grantTypes.Contains(OAuth2GrantType.DeviceAuthorization))
		{
			builder.AddDeviceAuthorizationJwtBearer(services);
		}
		else
		{
			throw new NotImplementedException("The OAuth2 Grant Types '" + string.Join(",", grantTypes.Select(x => x.ToString())) + "' are not supported by the MicroZen Cognito provider.");
		}
	}
}

/// <summary>
/// Registration entry points for the MicroZen AWS Cognito provider
/// </summary>
public static class CognitoServiceCollectionExtensions
{
	/// <summary>
	/// Adds MicroZen core services and AWS Cognito JWT bearer authentication/authorization
	/// </summary>
	/// <param name="services"><see cref="IServiceCollection"/></param>
	/// <param name="policies">
	///		<see cref="Dictionary{TKey,TValue}"/> - TKey = <see cref="string"/>, TValue = <see cref="AuthorizationPolicy"/>
	/// </param>
	/// <param name="grantTypes"><see cref="OAuth2GrantType" /> params</param>
	public static void AddMicroZenCognito(this IServiceCollection services, Dictionary<string, AuthorizationPolicy>? policies = null, params OAuth2GrantType[] grantTypes)
		=> services.AddMicroZenAuth(new CognitoAuthProvider(), policies, grantTypes);

	/// <summary>
	/// Use JWT Bearer token authentication and OAuth2 Authorization
	/// </summary>
	/// <remarks>This method must be called <b><em>after</em></b> app.UseCors()</remarks>
	/// <param name="app">The <see cref="IApplicationBuilder"/> to add the middleware to</param>
	public static void UseMicroZenCognito(this IApplicationBuilder app) => app.UseMicroZenAuth();
}
