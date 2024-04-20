using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using MicroZen.Data.Context;
using MicroZen.Grpc.Entities;

namespace MicroZen.Core.Api.Services;

/// <summary>
/// The Clients gRPC/REST service.
/// </summary>
/// <param name="db"><see cref="MicroZenContext"/></param>
public class ClientsService(MicroZenContext db) : Clients.ClientsBase
{
	/// <inheritdoc />
	/// <exception cref="RpcException">Status.NotFound - Client not found.</exception>
	public override async Task<ClientMessage> GetClient(ClientRequest request, ServerCallContext context) =>
		(await db.Clients.FindAsync(request.Id))?.ToMessage() ??
		throw new RpcException(new Status(StatusCode.NotFound, "Client not found."));

	/// <inheritdoc />
	/// <exception cref="RpcException">StatusCode.NotFound - Client not found or has no allowed clients.</exception>
	public override async Task<MultipleClientCredentials> GetAllowedOAuthClientCredentials(ClientRequest request, ServerCallContext context)
	{
		var client = await db.Clients
			.Include(c => c.OAuth2Config)
			.Include(c => c.AllowedClients).ThenInclude(client => client.OAuth2Config)
			.FirstOrDefaultAsync(x => x.Id == request.Id);

		if (client is null)
			throw new RpcException(new Status(StatusCode.NotFound, "Client not found."));
		if (client.AllowedClients is null || client.AllowedClients.Count == 0)
			throw new RpcException(new Status(StatusCode.NotFound, "Client has no allowed clients."));

		var response = new MultipleClientCredentials();
		response.Credentials.AddRange(client.AllowedClients.Select(x => new ClientCredentials
		{
			ClientId = x.OAuth2Config?.OAuth2ClientId ?? string.Empty,
			ClientSecret = x.OAuth2Config?.OAuth2ClientSecret ?? string.Empty
		}).ToArray());
		return response;
	}
}
