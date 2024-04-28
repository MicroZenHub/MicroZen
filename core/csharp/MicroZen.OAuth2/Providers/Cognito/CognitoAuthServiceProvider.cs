using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MicroZen.OAuth2.Definitions;
using MicroZen.OAuth2.Providers.Cognito.Flows;

namespace MicroZen.OAuth2.Providers.Cognito;

/// <summary>
/// The OAuth2 MicroZen service Provider for AWS Cognito
/// </summary>
public static class CognitoAuthServiceProvider
{
	/// <summary>
	/// Adds the necessary services for OAuth2 Authorization
	/// </summary>
	/// <param name="services"><see cref="IServiceCollection"/></param>
	/// <param name="policies">
	///		<see cref="Dictionary{TKey,TValue}"></see>
	///		<typeparam name="TKey"><see cref="string" /></typeparam>
	///		<typeparam name="TValue"><see cref="AuthorizationPolicy" /></typeparam>
	/// </param>
	/// <param name="grantTypes"><see cref="OAuth2GrantType" /> params</param>
	public static void AddAWSCognitoMicroZenOAuth2(this IServiceCollection services, Dictionary<string,AuthorizationPolicy>? policies = null, params OAuth2GrantType[] grantTypes)
	{
		AddOAuth2JwtAuthentication(services, grantTypes);
		AddOAuth2Authorization(services, policies);
	}

	/// <summary>
	/// Adds the necessary services for OAuth2 Authorization
	/// </summary>
	/// <param name="services"></param>
	/// <param name="grantTypes"></param>
	public static void AddAWSCognitoMicroZenOAuth2(this IServiceCollection services, params OAuth2GrantType[] grantTypes)
	{
		AddOAuth2JwtAuthentication(services, grantTypes);
		AddOAuth2Authorization(services);
	}

	/// <summary>
	/// Configures Cognito ID bearer token auth according to the rules outlined at:
	/// https://docs.aws.amazon.com/cognito/latest/developerguide/amazon-cognito-user-pools-using-tokens-verifying-a-jwt.html
	/// </summary>
	/// <param name="services">(IServiceCollection) - The App's Services.</param>
	/// <param name="grantTypes">(bool) - Which OAuth2 Flows should be used for authentication</param>
	private static void AddOAuth2JwtAuthentication(this IServiceCollection services, params OAuth2GrantType[] grantTypes)
	{
		JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

		var builder = services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme);

		if(grantTypes.Contains(OAuth2GrantType.AuthorizationCode) || grantTypes.Contains(OAuth2GrantType.AuthorizationCodeWithPKCE))
		{
			builder.AddAuthorizationCodeJwtBearer(services);
		}
		else if(grantTypes.Contains(OAuth2GrantType.ClientCredentials))
		{
			builder.AddClientCredentialsJwtBearer(services);
		}
		else
		{
			throw new NotImplementedException("The OAuth2 Grant Types '" + string.Join(",", grantTypes.Select(x => x.ToString())) + "' are not supported by the MicroZen Cognito provider.");
		}
	}

	private static void AddOAuth2Authorization(this IServiceCollection services, Dictionary<string,AuthorizationPolicy>? policies = null)
	{
		var defaultAuthPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();

		services.AddAuthorization(options =>
		{
			if (policies is null) return;

			foreach(var policy in policies)
				options.AddPolicy(policy.Key, policy.Value);

			options.DefaultPolicy = defaultAuthPolicy;
		});
	}

	/// <summary>
	/// Use JWT Bearer token authentication and OAuth2 Authorization
	/// </summary>
	/// <remarks>This method must be called <b><em>after</em></b> app.UseCors()</remarks>
	/// <param name="app">The <see cref="WebApplication"/> to add the middleware to</param>
	///
	public static void UseCognitoOAuth2(this IApplicationBuilder app)
	{
		app.UseAuthentication();
		app.UseAuthorization();
	}
}
