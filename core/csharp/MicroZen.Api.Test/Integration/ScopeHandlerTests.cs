extern alias MicroZenCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using static Xunit.Assert;
using MicroZenCore::MicroZen.Core.Authorization;

namespace MicroZen.Core.Api.Test.Integration;

/// <summary>Issue #109: the scope requirement and its configuration, without a host.</summary>
public class ScopeHandlerTests
{
	private static async Task<bool> Allowed(string[] required, params (string Type, string Value)[] claims)
	{
		var requirement = new ScopeRequirement(required);
		var user = new ClaimsPrincipal(new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value)), "test"));
		var context = new AuthorizationHandlerContext([requirement], user, null);
		await new ScopeHandler().HandleAsync(context);
		return context.HasSucceeded;
	}

	[Fact]
	public async Task NoTokenUseClaim_IsTreatedAsAccessToken_AndNeedsScope()
	{
		False(await Allowed(["api/read"]));
		False(await Allowed(["api/read"], ("scope", "api/write")));
		True(await Allowed(["api/read"], ("scope", "api/read")));
	}

	[Fact]
	public async Task OnlyExactIdTokenUse_IsExempt()
	{
		True(await Allowed(["api/read"], ("token_use", "id")));
		False(await Allowed(["api/read"], ("token_use", "ID")));
		False(await Allowed(["api/read"], ("token_use", "access")));
	}

	[Fact]
	public async Task EveryRequiredScope_IsNeeded()
	{
		False(await Allowed(["a", "b"], ("token_use", "access"), ("scope", "a")));
		True(await Allowed(["a", "b"], ("token_use", "access"), ("scope", "b  a")));
	}

	[Theory]
	[InlineData(null, "api/read")]
	[InlineData("", "api/read")]
	[InlineData("   ", "api/read")]
	[InlineData("a/x", "a/x")]
	[InlineData("a/x, a/y b/z", "a/x a/y b/z")]
	public async Task RequiredScopesConfig_IsParsed_AndBlankFallsBackToDefault(string? configured, string expected)
	{
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?> { [ScopeAuthorization.RequiredScopesKey] = configured })
			.Build();
		var services = new ServiceCollection().AddLogging().AddMicroZenScopePolicy(config);
		await using var provider = services.BuildServiceProvider();
		var policy = await provider.GetRequiredService<IAuthorizationPolicyProvider>().GetDefaultPolicyAsync();
		var requirement = Single(policy.Requirements.OfType<ScopeRequirement>());
		Equal(expected.Split(' '), requirement.Scopes);
	}
}
