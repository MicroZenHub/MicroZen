using System.Diagnostics.CodeAnalysis;
using System.Reactive.Linq;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using MicroZen.OAuth2.Definitions;
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
		var oAuth2Service = services.BuildServiceProvider().GetRequiredService<MicroZenOAuth2State>();

		oAuth2Service
			.State
			.Take(1)
			.Subscribe((state) =>
			{
				if (state == null) return;

				var clientsGroupedByUserPoolId = ClientsGroupedByUserPoolId(state);

				foreach (var regionUserPoolClients in clientsGroupedByUserPoolId)
				{
					AddJwtBearer(builder, regionUserPoolClients);
				}
			});
	}

	private static void AddJwtBearer(AuthenticationBuilder builder, UserPoolClients regionUserPoolClients)
	{
		builder.AddJwtBearer(options =>
		{
			var cognitoIdpUrl = $"https://cognito-idp.{regionUserPoolClients.Region}.amazonaws.com/{regionUserPoolClients.UserPoolId}";
			options.TokenValidationParameters = new TokenValidationParameters
			{
				ValidIssuer = cognitoIdpUrl,
				ValidAudiences = regionUserPoolClients.ClientIds,
				IssuerSigningKeys = regionUserPoolClients.ClientSecrets.Select(clientSecret => new SymmetricSecurityKey(Encoding.UTF8.GetBytes(clientSecret))).ToArray(),
				ValidateIssuerSigningKey = true,
				ValidateIssuer = true,
				ValidateLifetime = true,
				ValidateAudience = false,
			};
			options.MetadataAddress = cognitoIdpUrl + "/.well-known/openid-configuration";
		});
	}

	private static List<UserPoolClients> ClientsGroupedByUserPoolId([DisallowNull] OAuth2State? state)
	{
		return state.Value.ClientCredentials.GroupBy(
			oAuth2Credentials => new { oAuth2Credentials.UserPoolId, oAuth2Credentials.Region },
			oAuth2Credentials => new { oAuth2Credentials.ClientId, oAuth2Credentials.ClientSecret },
			(regionUserPoolId, clientCredentialGroups) =>
			{
				var clientCredentials = clientCredentialGroups.ToList();
				return new UserPoolClients
				(
					UserPoolId: regionUserPoolId.UserPoolId,
					Region: regionUserPoolId.Region,
					ClientIds: clientCredentials.Select(x => x.ClientId).ToArray(),
					ClientSecrets: clientCredentials.Select(x => x.ClientSecret).ToArray()
				);
			}).ToList();
	}
}
