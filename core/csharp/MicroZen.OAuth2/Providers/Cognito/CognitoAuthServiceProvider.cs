using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MicroZen.OAuth2.Definitions;

namespace MicroZen.OAuth2.Providers.Cognito;

/// <summary>
/// The OAuth2 MicroZen service Provider for AWS Cognito
/// </summary>
public static class CognitoAuthServiceProvider
{
	/// <summary>
	/// Configures Cognito ID bearer token auth according to the rules outlined at:
	/// https://docs.aws.amazon.com/cognito/latest/developerguide/amazon-cognito-user-pools-using-tokens-verifying-a-jwt.html
	/// </summary>
	/// <param name="services">(IServiceCollection) - The App's Services.</param>
	/// <param name="grantTypes">(bool) - Which OAuth2 GrantTypes should be used for authentication</param>
	public static void AddOAuth2Authentication(this IServiceCollection services, params OAuth2GrantType[] grantTypes)
	{
		JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
		var configuration = services.BuildServiceProvider().GetRequiredService<IConfiguration>();
		var environment = services.BuildServiceProvider().GetRequiredService<IWebHostEnvironment>();

		services
			.AddAuthentication(JwtBearerDefaults.AuthenticationScheme);

		if(grantTypes.Contains(OAuth2GrantType.AuthorizationCode))
		{
		}
		if(grantTypes.Contains(OAuth2GrantType.AuthorizationCodeWithPKCE))
		{
		}
		if(grantTypes.Contains(OAuth2GrantType.ClientCredentials))
		{
		}
		if(grantTypes.Contains(OAuth2GrantType.DeviceCode))
		{
		}
		if(grantTypes.Contains(OAuth2GrantType.RefreshToken))
		{
		}
	}

	public static void AddOAuth2Authorization(this IServiceCollection services, OAuth2GrantType grantType, Dictionary<string,AuthorizationPolicy>? policies = null)
	{
		throw new NotImplementedException();
	}

	/// <summary>
	/// Use JWT Bearer token authentication and OAuth2 Authorization
	/// </summary>
	/// <param name="app"></param>
	public static void UseOAuth2(this WebApplication app)
	{
		app.UseAuthentication();
		app.UseAuthorization();
	}
}
