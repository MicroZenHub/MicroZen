using Grpc.Core;
using MicroZen.Grpc.Entities;

namespace MicroZen.Core.Api.Services;

/// <inheritdoc />
public class OrganizationsService : Organizations.OrganizationsBase
{

	/// <inheritdoc />
	public override Task<Organization> GetOrganization(GetOrganizationRequest request, ServerCallContext context)
	{
		return base.GetOrganization(request, context);
	}
}
