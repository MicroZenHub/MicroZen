using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using MicroZen.Api.Context;
using MicroZen.Api.Entities;
using MicroZen.Grpc.Entities;

namespace MicroZen.Api.Services;

/// <summary>
/// The ClientApiKeys gRPC/REST service.
/// </summary>
/// <param name="db"><see cref="MicroZenContext"/></param>
[Authorize]
public class ClientApiKeysService(MicroZenContext db) : ClientApiKeys.ClientApiKeysBase
{
	/// <inheritdoc />
	public override async Task<RequestClientApiKeyResponse> RequestClientApiKey(RequestClientApiKeyRequest request, ServerCallContext context)
	{
		var clientApiKey = new ClientAPIKey()
		{
			ClientId = request.ClientId,
			ExpiresOn = request.ExpiresOn.ToDateTime(),
			ModifiedOn = DateTime.UtcNow,
			// TODO: Get UserID from Claims and populate CreatedBy if not APIAuth
			// TODO: Get UserID from Claims and populate ModifiedBy if not APIAuth
		};
		await db.ClientAPIKeys.AddAsync(clientApiKey);
		await db.SaveChangesAsync();

		return new RequestClientApiKeyResponse()
		{
			ApiKey = clientApiKey.ApiKey,
		};
	}

	/// <inheritdoc />
	/// <exception cref="RpcException"><see cref="StatusCode.NotFound"/> gRPC Exception thrown if API Key is not found</exception>
	public override async Task<UpdateClientApiKeyResponse> UpdateClientApiKey(UpdateClientApiKeyRequest request, ServerCallContext context)
	{
		var existingClientAPIKey = await db.ClientAPIKeys.FirstOrDefaultAsync(x => x.ApiKey == request.ApiKey);
		if (existingClientAPIKey is null)
			throw new RpcException(new Status(StatusCode.NotFound, "API Key not found"));
		existingClientAPIKey.ExpiresOn = request.ExpiresOn.ToDateTime();
		existingClientAPIKey.ModifiedOn = DateTime.UtcNow;
		// TODO: Get UserID from Claims and populate ModifiedBy if not APIAuth
		db.Update(existingClientAPIKey);
		await db.SaveChangesAsync();

		return new UpdateClientApiKeyResponse()
		{
			ApiKey = existingClientAPIKey.ApiKey,
			ExpiresOn = existingClientAPIKey.ExpiresOn != null ? existingClientAPIKey.ExpiresOn.Value.ToTimestamp() :
				new Google.Protobuf.WellKnownTypes.Timestamp() { Seconds = 1, Nanos = 1 },
		};
	}

	/// <inheritdoc />
	/// <exception cref="RpcException"><see cref="StatusCode.NotFound"/> gRPC Exception thrown if API Key is not found</exception>
	public override async Task<DisableClientApiKeyResponse> DisableClientApiKey(DisableClientApiKeyRequest request, ServerCallContext context)
	{
		var existingClientAPIKey = await db.ClientAPIKeys.FirstOrDefaultAsync(x => x.ApiKey == request.ApiKey);
		if (existingClientAPIKey is null)
			throw new RpcException(new Status(StatusCode.NotFound, "API Key not found"));
		existingClientAPIKey.ExpiresOn = DateTime.UtcNow;
		existingClientAPIKey.DeletedOn = DateTime.UtcNow;
		// TODO: Set DeletedBy once we can get the UserID from Claims
		db.Update(existingClientAPIKey);
		await db.SaveChangesAsync();
		return new DisableClientApiKeyResponse()
		{
			ApiKey = existingClientAPIKey.ApiKey,
			DisabledOn = existingClientAPIKey.DeletedOn.Value.ToTimestamp()
		};
	}
}
