using Grpc.Core;
using MicroZen.Grpc.Entities;

namespace MicroZen.Core.Api.Services;

/// <inheritdoc />
public class ClientsService : Clients.ClientsBase
{
	/// <inheritdoc />
	public override Task<Client> GetClient(ClientRequest request, ServerCallContext context)
	{
		return base.GetClient(request, context);
	}
}
