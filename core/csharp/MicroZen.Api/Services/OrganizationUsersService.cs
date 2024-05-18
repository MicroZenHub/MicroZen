using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using MicroZen.Data.Context;
using MicroZen.Grpc.Entities;

namespace MicroZen.Core.Api.Services;

/// <summary>
/// The Organization Users gRPC/REST service.
/// </summary>
/// <param name="db"><see cref="MicroZenContext"/></param>
public class OrganizationUsersService(MicroZenContext db) : OrganizationUsers.OrganizationUsersBase
{
	/// <inheritdoc />
	// TODO - Add [Policy(typeof(OrganizationUsers), Permission.Read)] attribute to block access if user is not in the organization for this client
	public override async Task<GetOrganizationUsersResponse> GetOrganizationUsers(GetOrganizationUsersRequest request, ServerCallContext context)
	{
		var response = new GetOrganizationUsersResponse();
		var users = await db.OrganizationUsers
			.Include(ou => ou.Organizations)
			.Where(ou => ou.Organizations.Any(o => o.Id == request.OrganizationId))
			.Select(ou => ou.ToMessage()).ToListAsync();
		response.Users.AddRange(users);
		return response;
	}
}
