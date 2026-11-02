using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using MicroZen.Api.Context;
using MicroZen.Grpc.V1;
using LinqKit;
using MicroZen.Api.Entities;

namespace MicroZen.Api.Services;

/// <summary>
/// The Organizations gRPC/REST service.
/// </summary>
/// <param name="db"><see cref="MicroZenContext"/></param>
[Authorize]
public class OrganizationsService(MicroZenContext db) : OrganizationsGrpcService.OrganizationsGrpcServiceBase
{
	/// <inheritdoc />
	// TODO - Add [Policy(typeof(Organization), Permission.Manage)] attribute to block access if user is not in the organization for this client
	public override async Task<OrganizationMessage> GetOrganization(GetOrganizationRequest request, ServerCallContext context) =>
		// FirstOrDefaultAsync (not FindAsync): the global DeletedOn == null query filter
		// is only applied to composable LINQ queries. FindAsync is a key lookup that
		// short-circuits on the change tracker and bypasses the filter, so a soft-deleted
		// org would still be returned here. Query instead so #55 is actually fixed.
		(await db.Organizations.FirstOrDefaultAsync(o => o.Id == request.Id))?.ToMessage() ??
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
			// FirstOrDefaultAsync (not FindAsync) so the global DeletedOn == null filter
			// applies consistently with the AnyAsync gate above. FindAsync is a key lookup
			// that bypasses query filters; keeping both on the same filtered query path
			// prevents the gate and the load from ever disagreeing about visibility.
			var organization = await db.Organizations.FirstOrDefaultAsync(o => o.Id == request.Id);
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
		if (organization is null)
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
		// The org being re-enabled is, by definition, soft-deleted, so the global
		// DeletedOn == null query filter would hide it. Opt out with IgnoreQueryFilters()
		// so the undelete path can load it. FindAsync does not compose with
		// IgnoreQueryFilters, so this uses a filtered-out FirstOrDefaultAsync instead.
		var organization = await db.Organizations
			.IgnoreQueryFilters()
			.FirstOrDefaultAsync(o => o.Id == request.Id);
		if (organization is null)
			throw new RpcException(new Status(StatusCode.NotFound,
				$"No Organization found for Id {request.Id}"));
		organization.DeletedOn = null;
		await db.SaveChangesAsync();

		return organization.ToMessage();
	}
}
