using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using LinqKit;
using Microsoft.EntityFrameworkCore;
using MicroZen.Data.Context;
using MicroZen.Data.Entities;
using MicroZen.Grpc.Entities;

namespace MicroZen.Core.Api.Services;

/// <summary>
/// The Clients gRPC/REST service.
/// </summary>
/// <param name="db"><see cref="MicroZenContext"/></param>
public class ClientsService(MicroZenContext db) : Clients.ClientsBase
{
	/// <inheritdoc />
	public override async Task<ManyClientsResponse> GetManyClients(ManyClientsRequest request, ServerCallContext context)
	{
		var predicate = PredicateBuilder.New<Client>(true);
		predicate.And(c => c.Type == request.Type);
		if (request.SearchTerm is not null)
		{
			predicate.And(c =>
				EF.Functions.ILike(c.Name, $"%{request}%"));
		}

		var response = new ManyClientsResponse();
		var page = request.Page > 0 ? request.Page : 10;
		var skip = request.Skip > 0 ? request.Skip : 0;
		var total = await db.Clients.Where(predicate).CountAsync();
		var clients = await db.Clients
			.Where(predicate)
			.Take(page)
			.Skip(skip)
			.OrderBy(c => c.Name)
			.Select(c => c.ToMessage())
			.ToListAsync();
		var anyMoreClients = await db.Clients.Where(predicate).Take(page + 1).Skip(skip).AnyAsync();
		if (anyMoreClients)
		{
			response.NextUrl = $"clients?page={request.Page + 1}&skip={request.Skip}&searchTerm={request.SearchTerm}&type={request.Type.ToString().ToUpper()}";
			response.Total = total;
		}
		response.Clients.AddRange(clients);
		return response;
	}

	/// <inheritdoc />
	/// <exception cref="RpcException">Status.NotFound - Client not found.</exception>
	// TODO - Add [Policy(typeof(Client), Permission.Read)] attribute to block access if user is not in the organization for this client
	public override async Task<ClientMessage> GetClient(ClientRequest request, ServerCallContext context) =>
		(await db.Clients
			.Include(c => c.OAuth2Credentials)
			.FirstOrDefaultAsync(c => c.Id == request.Id)
		)?.ToMessage() ??
		throw new RpcException(new Status(StatusCode.NotFound, "Client not found."));

	/// <inheritdoc />
	/// <exception cref="RpcException">StatusCode.NotFound - Client not found or has no allowed clients.</exception>
	// TODO - Add [Policy(typeof(Client), Permission.Read)] attribute to block access if user is not in the organization for this client
	public override async Task<MultipleClientCredentials> GetAllowedOAuthClientCredentials(ClientRequest request, ServerCallContext context)
	{
		var client = await db.Clients
			.Include(c => c.OAuth2Credentials)
			.Include(c => c.AllowedClients)
			.ThenInclude(client => client.OAuth2Credentials)
			.FirstOrDefaultAsync(x => x.Id == request.Id);

		if (client is null)
			throw new RpcException(new Status(StatusCode.NotFound, "Client not found."));
		if (client.AllowedClients is null || client.AllowedClients.Count == 0)
			 throw new RpcException(new Status(StatusCode.NotFound, "Client has no allowed clients."));

		var response = new MultipleClientCredentials();
		response.Credentials.AddRange(client.AllowedClients.Select(allowedClient =>
			new OAuth2Credentials()
			{
				ClientId = allowedClient.OAuth2Credentials?.OAuth2ClientId ?? string.Empty,
				ClientSecret = allowedClient.OAuth2Credentials?.OAuth2ClientSecret ?? string.Empty,
				Type = allowedClient.Type
			}).ToArray());
		return response;
	}

	// TODO - Add [Policy(typeof(Client), Permission.Read)] attribute to block access if user is not in the organization for this client
	/// <inheritdoc />
	public override async Task<Int32Value> GetClientIdFromApiKey(StringValue request, ServerCallContext context)
	{
		var clientId = (await db.ClientAPIKeys
			.Select(clientApiKey => new { clientApiKey.ClientId, clientApiKey.ApiKey })
			.FirstOrDefaultAsync(x => x.ApiKey == request.Value))?.ClientId;
		return new Int32Value() { Value = clientId ?? 0 };
	}

	/// <inheritdoc />
	// TODO - Add [Policy(typeof(Client), Permission.Create)] attribute to block access if user is not in the organization for this client
	public override async Task<ClientMessage> CreateClient(ClientMessage request, ServerCallContext context)
	{
		var client = new Client()
		{
			Name = request.Name,
			Type = request.Type,
			Description = request.Description,
			CreatedOn = DateTime.UtcNow,
			ModifiedOn = DateTime.UtcNow,
			OrganizationId = request.OrganizationId,
			OAuth2Credentials = new OAuth2ClientCredentials()
			{
				OAuth2GrantType = request.Oauth2Credentials.GrantType,
				OAuth2ClientId = request.Oauth2Credentials.ClientId,
				OAuth2ClientSecret = request.Oauth2Credentials.ClientSecret,
				AllowedScopes = request.Oauth2Credentials.AllowedScopes,
				RequirePkce = request.Oauth2Credentials.RequirePkce
			}
		};
		await db.Clients.AddAsync(client);
		await db.SaveChangesAsync();
		return client.ToMessage();
	}

	/// <inheritdoc />
	/// <exception cref="RpcException"><see cref="StatusCode.NotFound"/> - Client not found.</exception>
	// TODO - Add [Policy(typeof(Client), Permission.Update)] attribute to block access if user is not in the organization for this client
	public override async Task<ClientMessage> UpdateClient(ClientMessage request, ServerCallContext context)
	{
		var client = await db.Clients.Include(c => c.OAuth2Credentials).FirstOrDefaultAsync(c => c.Id == request.Id);
		if (client is null)
			throw new RpcException(new Status(StatusCode.NotFound, "Client not found."));
		client.Name = request.Name;
		client.Type = request.Type;
		client.Description = request.Description;
		client.ModifiedOn = DateTime.UtcNow;
		if (client.OAuth2Credentials is not null)
		{
			client.OAuth2Credentials.OAuth2ClientId = request.Oauth2Credentials.ClientId;
			client.OAuth2Credentials.OAuth2ClientSecret = request.Oauth2Credentials.ClientSecret;
			client.OAuth2Credentials.OAuth2GrantType = request.Oauth2Credentials.GrantType;
			client.OAuth2Credentials.AllowedScopes = request.Oauth2Credentials.AllowedScopes;
			client.OAuth2Credentials.RequirePkce = request.Oauth2Credentials.RequirePkce;
		}

		db.Update(client);
		await db.SaveChangesAsync();

		return client.ToMessage();
	}


	/// <inheritdoc />
	// TODO - Add [Policy(typeof(Client), Permission.Manage)] attribute to block access if user is not in the organization for this client
	public override async Task<ClientAllowResponse> AllowClient(ClientAllowRequest request, ServerCallContext context)
	{
		var allowedClient = await db.Clients.FindAsync(request.AllowedClientId);
		var client = await db.Clients.FindAsync(request.Id);
		if (allowedClient is null || client is null)
			throw new RpcException(new Status(StatusCode.NotFound, "Client or Allowed Client not found."));
		client.AllowedClients.Add(allowedClient);
		await db.SaveChangesAsync();
		return new ClientAllowResponse()
		{
			Id = client.Id,
			AllowedClientId = allowedClient.Id
		};
	}

	/// <inheritdoc />
	// TODO - Add [Policy(typeof(Client), Permission.Manage)] attribute to block access if user is not in the organization for this client
	public override async Task<ClientAllowResponse> DisallowClient(ClientAllowRequest request, ServerCallContext context)
	{
		var allowedClient = await db.Clients.FindAsync(request.AllowedClientId);
		var client = await db.Clients.FindAsync(request.Id);
		if (allowedClient is null || client is null)
			throw new RpcException(new Status(StatusCode.NotFound, "Client or Allowed Client not found."));
		client.AllowedClients.Remove(allowedClient);
		await db.SaveChangesAsync();
		return new ClientAllowResponse()
		{
			Id = client.Id,
			AllowedClientId = allowedClient.Id
		};
	}

	/// <inheritdoc />
	/// <exception cref="RpcException"><see cref="StatusCode.NotFound"/> - Client not found.</exception>
	// TODO - Add [Policy(typeof(Client), Permission.Delete)] attribute to block access if user is not in the organization for this client
	public override async Task<DeleteClientResponse> DeleteClient(DeleteClientRequest request, ServerCallContext context)
	{
		var client = await db.Clients.FindAsync(request.Id);
		if (client is null)
			throw new RpcException(new Status(StatusCode.NotFound, "Client not found."));
		client.ModifiedOn = DateTime.UtcNow;
		client.DeletedOn = DateTime.UtcNow;
		// TODO - Assign client.DeletedBy from User ID claims
		db.Clients.Update(client);
		await db.SaveChangesAsync();

		return new DeleteClientResponse()
		{
			Id = request.Id,
			DeletedOn = client.DeletedOn.HasValue ?
				client.DeletedOn.Value.ToTimestamp() :
				new Google.Protobuf.WellKnownTypes.Timestamp() { Seconds = 0, Nanos = 0 }
		};
	}
}
