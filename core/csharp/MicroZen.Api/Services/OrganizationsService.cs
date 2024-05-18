using Grpc.Core;
using MicroZen.Data.Context;
using MicroZen.Grpc.Entities;

namespace MicroZen.Core.Api.Services;

/// <summary>
/// The Organizations gRPC/REST service.
/// </summary>
/// <param name="db"><see cref="MicroZenContext"/></param>
public class OrganizationsService(MicroZenContext db) : Organizations.OrganizationsBase
{
	/// <inheritdoc />
	// TODO - Add [Policy(typeof(Organization), Permission.Manage)] attribute to block access if user is not in the organization for this client
	public override async Task<OrganizationMessage> GetOrganization(GetOrganizationRequest request, ServerCallContext context) =>
		(await db.Organizations.FindAsync(request.Id))?.ToMessage() ??
		throw new RpcException(new Status(StatusCode.NotFound, "Organization not found"));
}
