using Microsoft.Extensions.DependencyInjection;
using MicroZen.OAuth2.Providers;
using MicroZen.OAuth2.Providers.Cognito;

namespace MicroZen.OAuth2;

public static class Initializers
{
	public static void AddMicroZenOAuth2<TServiceProvider>(this IServiceCollection services, TServiceProvider serviceProvider) where TServiceProvider : IAuthServiceProvider
	{
		switch (services.GetType().FullName)
		{
			case nameof(CognitoAuthServiceProvider):
				services.AddTransient<IAuthServiceProvider, CognitoAuthServiceProvider>();
				services.AddTransient<MicroZenAuthProvider<CognitoAuthServiceProvider>>();
				break;
			default:
				throw new NotSupportedException(
					$"{serviceProvider.GetType().FullName} is not a valid service provider");
		}
	}
}
