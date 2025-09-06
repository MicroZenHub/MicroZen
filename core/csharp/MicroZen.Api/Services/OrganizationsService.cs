using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using MicroZen.Api.Context;
using MicroZen.Grpc.Entities;
using LinqKit;
using MicroZen.Api.Entities;

namespace MicroZen.Api.Services;

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

	/// <inheritdoc />
	// TODO - Add [Policy(typeof(Organization), Permission.Manage)] attribute to block access if user is not in the organization for this client
	public override async Task<MultipleOrganizationsResponse> GetOrganizations(MultipleOrganizationsRequest request, ServerCallContext context)
	{
		var predicate = PredicateBuilder.New<Organization>(true);
		if (request.SearchTerm is not null)
			predicate.And(o => o.Name.Contains(request.SearchTerm, StringComparison.InvariantCultureIgnoreCase));
		var organizations = await db.Organizations
			.Where(predicate)
			.Select(o => o.ToMessage())
			.ToListAsync();
		var response = new MultipleOrganizationsResponse();
		response.Organizations.AddRange(organizations);
		return response;
	}

	/// <inheritdoc />
	public override async Task<OrganizationMessage> UpsertOrganization(OrganizationMessage request, ServerCallContext context)
	{
		if (await db.Organizations.AnyAsync(o => o.Id == request.Id))
		{
			var organization = await db.Organizations.FindAsync(request.Id);
			if (organization is null)
				throw new RpcException(new Status(StatusCode.NotFound, $"No Organization found for Id {request.Id}"));
			organization.Name = request.Name;
			organization.Description = request.Description;
			organization.AvatarUrl = request.AvatarUrl;
			organization.WebsiteUrl = request.WebsiteUrl;
			organization.ModifiedOn = DateTime.UtcNow;
			await db.SaveChangesAsync();
			return organization.ToMessage();
		}
		else
		{
			var organization = new Organization(request)
			{
				Id = -1,
				Name = request.Name,
				CreatedOn = DateTime.UtcNow,
				ModifiedOn = DateTime.UtcNow
			};
			await db.Organizations.AddAsync(organization);
			await db.SaveChangesAsync();
			return organization.ToMessage();
		}
	}

	/// <inheritdoc />
	public override async Task<DisableOrganizationResponse> DisableOrganization(DisableOrganizationRequest request, ServerCallContext context)
	{
		var organization = await db.Organizations.FindAsync(request.Id);
		if(organization is null)
			throw new RpcException(new Status(StatusCode.NotFound,
				$"No Organization found for Id {request.Id}"));
		organization.DeletedOn = DateTime.UtcNow;
		await db.SaveChangesAsync();

		return new DisableOrganizationResponse()
		{
			Id = request.Id,
			DisabledOn = organization.DeletedOn.Value.ToTimestamp()
		};
	}

	/// <inheritdoc />
	public override async Task<OrganizationMessage> EnableOrganization(EnableOrganizationRequest request, ServerCallContext context)
	{
		var organization = await db.Organizations.FindAsync(request.Id);
		if(organization is null)
			throw new RpcException(new Status(StatusCode.NotFound,
				$"No Organization found for Id {request.Id}"));
		organization.DeletedOn = null;
		await db.SaveChangesAsync();

		return organization.ToMessage();
	}
}
