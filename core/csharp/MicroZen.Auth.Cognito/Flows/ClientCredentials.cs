using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using MicroZen.Core.State;

namespace MicroZen.Auth.Cognito.Flows;

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
		var oAuth2State = services.BuildServiceProvider().GetRequiredService<MicroZenOAuth2State>();
		builder.AddJwtBearer(options =>
			LazyBearer.Configure(options, oAuth2State, pool =>
				pool.ClientSecrets.Select(secret => new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)))));
	}
}
