using Microsoft.AspNetCore.Mvc;

namespace MicroZen.Api.Security.Policies;

/// <inheritdoc />
public class PolicyAttribute : TypeFilterAttribute
{
	/// <inheritdoc />
	public PolicyAttribute(Type type, Permission permission) : base(typeof(PolicyFilter<>))
	{
		Arguments = [ type, permission ];
	}
}
