using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MicroZen.Core.Config;
using MicroZen.Core.Definitions;
using MicroZen.Core.State;
using MicroZen.Grpc.V1;

namespace MicroZen.Core;

/// <summary>
/// Class for initializing MicroZen auth services. Provider packages call this from their own entry point.
/// </summary>
public static class Initialization
{
	/// <summary>
	/// Adds the MicroZen core services (authority gRPC client + state) and the given provider's JWT bearer authentication.
	/// </summary>
	/// <param name="services"><see cref="IServiceCollection"/></param>
	/// <param name="provider">The <see cref="IMicroZenAuthProvider"/> that validates tokens</param>
	/// <param name="policies">Optional named authorization policies</param>
	/// <param name="grantTypes"><see cref="OAuth2GrantType" /> params</param>
	/// <exception cref="ArgumentNullException">Thrown when the MicroZen configuration section is missing</exception>
	public static void AddMicroZenAuth(this IServiceCollection services, IMicroZenAuthProvider provider, Dictionary<string, AuthorizationPolicy>? policies = null, params OAuth2GrantType[] grantTypes)
	{
		services.AddMicroZenCore();

		JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
		var builder = services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme);
		provider.AddAuthentication(builder, services, grantTypes);

		var defaultAuthPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
		services.AddAuthorization(options =>
		{
			if (policies is null) return;

			foreach (var policy in policies)
				options.AddPolicy(policy.Key, policy.Value);

			options.DefaultPolicy = defaultAuthPolicy;
		});
	}

	/// <summary>
	/// Adds the provider-independent MicroZen services: the authority gRPC client and <see cref="MicroZenOAuth2State"/>.
	/// </summary>
	/// <param name="services"><see cref="IServiceCollection"/></param>
	/// <exception cref="ArgumentNullException">Thrown when the MicroZen configuration section is missing</exception>
	public static void AddMicroZenCore(this IServiceCollection services)
	{
		var configuration = services.BuildServiceProvider().GetRequiredService<IConfiguration>();
		var microZenConfig = configuration.GetSection("MicroZen").Get<MicroZenAppConfig>();
		services.AddGrpcClient<ClientsGrpcService.ClientsGrpcServiceClient>(o =>
		{
			if (microZenConfig is null)
				throw new ArgumentNullException(
					nameof(microZenConfig),
					"MicroZenAppConfig is null. Please confirm that you have properly entered the required configuration settings in appsettings.json or as an Environment Variable.");
			o.Address = new Uri(microZenConfig.AuthorityUrl);
		})
		.AddCallCredentials((context, metadata) =>
		{
			if (!string.IsNullOrEmpty(microZenConfig?.APIKey))
			{
				metadata.Add("X-API-Key", microZenConfig.APIKey);
			}

			return Task.CompletedTask;
		});
		services.AddSingleton<MicroZenOAuth2State>();
	}
}
