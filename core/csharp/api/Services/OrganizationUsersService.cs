using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using MicroZen.Data.Context;
using MicroZen.Grpc.Entities;

namespace MicroZen.Core.Api.Services;

/// <summary>
/// The Organization Users gRPC/REST service.
/// <param name="db"><see cref="MicroZenContext"/></param>
/// </summary>
public class OrganizationUsersService(MicroZenContext db) : OrganizationUsers.OrganizationUsersBase
{
	/// <inheritdoc />
	public override async Task<GetOrganizationUsersResponse> GetOrganizationUsers(GetOrganizationUsersRequest request, ServerCallContext context)
	{
		var response = new GetOrganizationUsersResponse();
		var users = await db.OrganizationUsers
			.Where(ou => ou.OrganizationId == request.OrganizationId)
			.Select(ou => ou.ToMessage()).ToListAsync();
		response.Users.AddRange(users);
		return response;
	}
}
