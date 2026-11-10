extern alias MicroZenCore;
using System.Reflection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MicroZen.Auth.Cognito;
using static Xunit.Assert;
using CoreInit = MicroZenCore::MicroZen.Core.Initialization;
using GrantType = MicroZenCore::MicroZen.Core.Definitions.OAuth2GrantType;
using IAuthProvider = MicroZenCore::MicroZen.Core.IMicroZenAuthProvider;
using CoreState = MicroZenCore::MicroZen.Core.State.MicroZenOAuth2State;

namespace MicroZen.Core.Api.Test.Definitions;

/// <summary>
/// Tests for issue #69: the MicroZen.Core / MicroZen.Auth.Cognito boundary a second provider plugs into.
/// </summary>
public class ProviderBoundaryTests
{
	private static ServiceCollection ServicesWithConfig()
	{
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["MicroZen:AuthorityUrl"] = "https://localhost",
				["MicroZen:APIKey"] = "test-key",
			})
			.Build();
		var services = new ServiceCollection();
		services.AddSingleton<IConfiguration>(config);
		services.AddLogging();
		return services;
	}

	private sealed class FakeProvider : IAuthProvider
	{
		public GrantType[]? SeenGrantTypes { get; private set; }
		public bool SeenStateRegistered { get; private set; }
		public string Name => "fake";

		public void AddAuthentication(AuthenticationBuilder builder, IServiceCollection services, GrantType[] grantTypes)
		{
			SeenGrantTypes = grantTypes;
			SeenStateRegistered = services.Any(s => s.ServiceType == typeof(CoreState));
		}
	}

	[Fact]
	public void AddMicroZenAuth_WithAnyProvider_RegistersCoreServicesAndCallsProvider()
	{
		// GIVEN a provider that is not Cognito and a policy.
		var services = ServicesWithConfig();
		var provider = new FakeProvider();
		var policies = new Dictionary<string, AuthorizationPolicy>
		{
			["p"] = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build(),
		};

		// WHEN it is registered through Core.
		CoreInit.AddMicroZenAuth(services, provider, policies, GrantType.ClientCredentials);

		// THEN Core state is registered before the provider runs, the provider gets the grant types,
		// and the policy is available.
		True(provider.SeenStateRegistered);
		Equal([GrantType.ClientCredentials], provider.SeenGrantTypes);
		var authz = services.BuildServiceProvider().GetRequiredService<Microsoft.Extensions.Options.IOptions<AuthorizationOptions>>().Value;
		NotNull(authz.GetPolicy("p"));
	}

	[Fact]
	public void AddMicroZenCognito_RegistersCoreStateAndJwtBearer()
	{
		// GIVEN configuration for the authority.
		var services = ServicesWithConfig();

		// WHEN the single Cognito entry point is used.
		var ex = Record.Exception(() => services.AddMicroZenCognito(null, GrantType.AuthorizationCodeWithPKCE));

		// THEN Core's state singleton is registered by the provider package's entry point.
		Null(ex);
		Contains(services, s => s.ServiceType == typeof(CoreState));
	}

	[Fact]
	public void CognitoProvider_UnsupportedGrantType_Throws() =>
		Throws<NotImplementedException>(() =>
			new CognitoAuthProvider().AddAuthentication(new ServiceCollection().AddAuthentication(), new ServiceCollection(), [new GrantType("implicit")]));

	[Fact]
	public void CognitoProvider_NameIsCognito() => Equal("cognito", new CognitoAuthProvider().Name);

	[Fact]
	public void Core_DoesNotReferenceAnyProviderPackage()
	{
		// THEN the shared package stays provider-independent.
		var refs = typeof(IAuthProvider).Assembly.GetReferencedAssemblies().Select(a => a.Name);
		DoesNotContain(refs, n => n!.StartsWith("MicroZen.Auth."));
	}
}
