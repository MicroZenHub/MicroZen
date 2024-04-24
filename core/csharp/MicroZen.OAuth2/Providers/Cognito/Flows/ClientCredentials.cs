using System.Reactive.Linq;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using MicroZen.OAuth2.State;

namespace MicroZen.OAuth2.Providers.Cognito.Flows;

/// <summary>
/// Handles all code related to the OAuth2 ClientCredentials grant type
/// </summary>
public static class ClientCredentials
{
	/// <summary>
	/// Adds the JWT Bearer token authentication for the OAuth2 "client_credentials" grant type
	/// </summary>
	/// <param name="builder"><see cref="AuthenticationBuilder"/></param>
	/// <param name="services"><see cref="IServiceCollection" /></param>
	public static void AddClientCredentialsJwtBearer(this AuthenticationBuilder builder, IServiceCollection services)
	{
		var oAuth2Service = services.BuildServiceProvider().GetRequiredService<MicroZenState<OAuth2State>>();

		oAuth2Service
			.State
			.Take(1)
			.Subscribe((state) =>
			{
				builder.AddJwtBearer(options =>
				{
					var cognitoIdpUrl = $"https://cognito-idp.{state.Region}.amazonaws.com/{state.UserPoolId}";
					options.TokenValidationParameters = new TokenValidationParameters
					{
						ValidIssuer = cognitoIdpUrl,
						ValidAudiences = state.ClientIds ?? [],
						IssuerSigningKeys = state.ClientSecrets?.Select(clientSecret => new SymmetricSecurityKey(Encoding.UTF8.GetBytes(clientSecret))) ?? [],
						ValidateIssuerSigningKey = true,
						ValidateIssuer = true,
						ValidateLifetime = true,
						ValidateAudience = false,
					};
					options.MetadataAddress = cognitoIdpUrl + "/.well-known/openid-configuration";
				});
			});
	}
}
