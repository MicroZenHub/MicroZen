using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MicroZen.Core.Authorization;

/// <summary>
/// Requires the token's space-delimited <c>scope</c> claim to contain every scope in <see cref="Scopes"/>.
/// </summary>
/// <param name="Scopes">The scopes a token must carry</param>
public sealed record ScopeRequirement(IReadOnlyCollection<string> Scopes) : IAuthorizationRequirement;

/// <summary>
/// Evaluates <see cref="ScopeRequirement"/>. Fails closed: a missing or short <c>scope</c> claim never succeeds.
/// </summary>
/// <remarks>
/// <b>ID-token exemption (explicit):</b> a token whose <c>token_use</c> claim is exactly <c>id</c> is not scope-checked,
/// because Cognito ID tokens (authorization code flows) never carry a <c>scope</c> claim. Any other value, or no
/// <c>token_use</c> claim at all, is treated as an access token and must carry the scopes. Which <c>token_use</c>
/// values are accepted at all is decided by token validation, not here.
/// </remarks>
public sealed class ScopeHandler : AuthorizationHandler<ScopeRequirement>
{
	/// <summary>The <c>token_use</c> value of an ID token, which is exempt from the scope check.</summary>
	public const string IdTokenUse = "id";

	/// <inheritdoc />
	protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ScopeRequirement requirement)
	{
		if (context.User.FindFirst("token_use")?.Value == IdTokenUse)
		{
			context.Succeed(requirement);
			return Task.CompletedTask;
		}

		var granted = context.User.FindAll("scope")
			.SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
			.ToHashSet(StringComparer.Ordinal);
		if (requirement.Scopes.All(granted.Contains))
			context.Succeed(requirement);

		return Task.CompletedTask;
	}
}

/// <summary>
/// Registers the scope policy as the default authorization policy.
/// </summary>
public static class ScopeAuthorization
{
	/// <summary>Configuration key holding the required scopes (space or comma separated).</summary>
	public const string RequiredScopesKey = "MicroZen:RequiredScopes";

	/// <summary>Scope required when <see cref="RequiredScopesKey"/> is not set.</summary>
	public const string DefaultScope = "api/read";

	/// <summary>
	/// Makes "authenticated + carries the required scopes" the default policy, so every <c>[Authorize]</c> endpoint enforces it.
	/// </summary>
	/// <param name="services"><see cref="IServiceCollection"/></param>
	/// <param name="configuration">Source of <see cref="RequiredScopesKey"/></param>
	public static IServiceCollection AddMicroZenScopePolicy(this IServiceCollection services, IConfiguration configuration)
	{
		var scopes = (configuration[RequiredScopesKey] ?? "")
			.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		if (scopes.Length == 0) scopes = [DefaultScope]; // blank never means "no scope required"

		services.AddSingleton<IAuthorizationHandler, ScopeHandler>();
		return services.AddAuthorization(o => o.DefaultPolicy = new AuthorizationPolicyBuilder()
			.RequireAuthenticatedUser()
			.AddRequirements(new ScopeRequirement(scopes))
			.Build());
	}
}
