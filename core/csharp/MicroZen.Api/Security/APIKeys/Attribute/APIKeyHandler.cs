using Microsoft.AspNetCore.Authorization;
using MicroZen.Api.Definitions;

namespace MicroZen.Api.Security.APIKeys.Attribute;

/// <inheritdoc />
public class APIKeyHandler(IHttpContextAccessor httpContextAccessor, IAPIKeyValidation apiKeyValidation) : AuthorizationHandler<APIKeyRequirement>
{
	/// <inheritdoc />
	protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, APIKeyRequirement requirement)
	{
		var apiKey = httpContextAccessor.HttpContext?.Request.Headers[Constants.APIKeyHeader].ToString();
		if (string.IsNullOrWhiteSpace(apiKey))
		{
			context.Fail();
			return;
		}
		if (!await apiKeyValidation.IsValidAPIKey(apiKey))
		{
			context.Fail();
			return;
		}
		context.Succeed(requirement);
	}
}
