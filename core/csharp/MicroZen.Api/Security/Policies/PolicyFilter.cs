using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MicroZen.Api.Security.Policies;

public class PolicyFilter<TPolicy>(Type resourceType, Permission permission, IServiceProvider services) : IAsyncAuthorizationFilter
{
	/// <inheritdoc />
	public async Task OnAuthorizationAsync(AuthorizationFilterContext filterContext)
	{
		// The below nonsense is due to not being able to use Generics in attributes.
		// Find Handler for Resource and Invoke it
		var handlerType = typeof(IPolicy<>);
		Type[] handlerTypeArgs = [resourceType];
		var constructedHandlerType = handlerType.MakeGenericType(handlerTypeArgs);
		var handler = services.GetService(constructedHandlerType);
		var authorizeMethod = handler?.GetType().GetMethod("IsAllowed");
		var result = authorizeMethod?.Invoke(handler, [permission] );

		if ((bool)result == false) filterContext.Result = new ForbidResult();
	}
}
