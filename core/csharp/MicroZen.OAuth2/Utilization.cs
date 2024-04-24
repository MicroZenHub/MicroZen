using Microsoft.AspNetCore.Builder;
using MicroZen.OAuth2.Definitions;
using MicroZen.OAuth2.Providers.Cognito;

namespace MicroZen.OAuth2;

/// <summary>
/// Class for utilizing MicroZen OAuth2 services
/// </summary>
public static class Utilization
{
	/// <summary>
	/// Use MicroZen OAuth2 services and MicroZenState
	/// </summary>
	/// <param name="app"><see cref="IApplicationBuilder"/></param>
	/// <param name="serviceProvider"><see cref="MicroZenProvider"/></param>
	/// <exception cref="NotSupportedException">Thrown when </exception>
	public static void UseMicroZenOAuth2(this IApplicationBuilder app, MicroZenProvider serviceProvider)
	{
		if (serviceProvider == MicroZenProvider.Cognito)
		{
			app.UseCognitoOAuth2();
		}
		else
		{
			throw new NotSupportedException(
				$"{serviceProvider.GetType().FullName} is not a valid service provider");
		}
	}
}
