using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MicroZen.OAuth2.Config;
using MicroZen.OAuth2.Definitions;
using MicroZen.OAuth2.Providers.Cognito;
using MicroZen.OAuth2.State;
using static MicroZen.OAuth2.Definitions.MicroZenProvider;

namespace MicroZen.OAuth2;

/// <summary>
/// Class for initializing MicroZen OAuth2 services
/// </summary>
public static class Initialization
{
	/// <summary>
	/// Adds the MicroZen OAuth2 service provider and core MicroZen services to the service collection
	/// </summary>
	/// <param name="services"><see cref="IServiceCollection"/></param>
	/// <param name="serviceProvider">The <see cref="MicroZenProvider"/> that our app should utilize</param>
	/// <param name="policies"><see cref="Dictionary{TKey,TValue}"/></param>
	/// <param name="grantTypes"><see cref="OAuth2GrantType" /> params</param>
	/// <exception cref="NotSupportedException">Thrown when </exception>
	public static void AddMicroZenOAuth2(this IServiceCollection services, MicroZenProvider serviceProvider, Dictionary<string,AuthorizationPolicy>? policies = null, params OAuth2GrantType[] grantTypes)
	{
		services.AddSingleton<MicroZenOAuth2State>();
		if (serviceProvider == Cognito)
		{
				services.AddAWSCognitoMicroZenOAuth2(policies, grantTypes);
		}
		else
		{
			throw new NotSupportedException(
				$"{serviceProvider.GetType().FullName} is not a valid service provider");
		}
	}
}
