using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using LinqKit;
using Microsoft.EntityFrameworkCore;
using MicroZen.Api.Context;
using MicroZen.Api.Entities;
using MicroZen.Api.Entities.AuthCredentials;
using MicroZen.Grpc.Entities;

namespace MicroZen.Api.Services;

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
			.Include(c => c.OAuth2Credentials)
			.Include(c => c.AllowedClientOAuth2Credentials)
			.Where(predicate)
			.OrderBy(c => c.Name)
			.Skip(skip)
			.Take(page)
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
			.Include(c => c.AllowedClientOAuth2Credentials)
			.FirstOrDefaultAsync(c => c.Id == request.Id)
		)?.ToMessage() ??
		throw new RpcException(new Status(StatusCode.NotFound, "Client not found."));

	/// <inheritdoc />
	/// <exception cref="RpcException">StatusCode.NotFound - Client not found or has no allowed clients.</exception>
	// TODO - Add [Policy(typeof(Client), Permission.Read)] attribute to block access if user is not in the organization for this client
	public override async Task<MultipleClientCredentials> GetAllowedOAuthClientCredentials(ClientRequest request, ServerCallContext context)
	{
		var allowedCredentials = await db.Clients
			.Include(c => c.OAuth2Credentials)
			.Include(c => c.AllowedClientOAuth2Credentials)
			.SelectMany(c => c.AllowedClientOAuth2Credentials)
			.ToListAsync();

		if (allowedCredentials is null)
			throw new RpcException(new Status(StatusCode.NotFound, "No Allowed Credentials not found."));

		var response = new MultipleClientCredentials();
		response.Credentials.AddRange(allowedCredentials.Select(c => c.ToMessage()).ToList());
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
			OAuth2Credentials = request.OauthCredentials.Select(oAuthClientCredentials => new OAuth2ClientCredentials()
			{
				OAuth2GrantType = oAuthClientCredentials.GrantType,
				OAuth2ClientId = oAuthClientCredentials.ClientId,
				OAuth2ClientSecret = oAuthClientCredentials.ClientSecret,
				AllowedScopes = oAuthClientCredentials.AllowedScopes,
				RequirePkce = oAuthClientCredentials.RequirePkce
			}).ToArray()
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
			var credentialsToUpdate = request.OauthCredentials.Select(oauthCredentials =>
				new OAuth2ClientCredentials()
				{
					OAuth2ClientId = oauthCredentials.ClientId,
					OAuth2ClientSecret = oauthCredentials.ClientSecret,
					OAuth2GrantType = oauthCredentials.GrantType,
					AllowedScopes = oauthCredentials.AllowedScopes,
					RequirePkce = oauthCredentials.RequirePkce,
				}).Where(oauth2Credentials =>
					client.OAuth2Credentials.Any(existingCreds =>
						existingCreds.ClientId == oauth2Credentials.ClientId
					)
				).ToArray();
			var credentialsToAdd = request.OauthCredentials.Select(oauthCredentials =>
				new OAuth2ClientCredentials()
				{
					OAuth2ClientId = oauthCredentials.ClientId,
					OAuth2ClientSecret = oauthCredentials.ClientSecret,
					OAuth2GrantType = oauthCredentials.GrantType,
					AllowedScopes = oauthCredentials.AllowedScopes,
					RequirePkce = oauthCredentials.RequirePkce,
				}).Where(oauthCredentials =>
				client.OAuth2Credentials.Any(existingCredentials =>
					existingCredentials.ClientId == oauthCredentials.ClientId
				)
			).ToArray();
			foreach (var credentials in credentialsToUpdate)
			{
				var existingCredentials = client.OAuth2Credentials.First(c => c.OAuth2ClientId == credentials.OAuth2ClientId);
				existingCredentials.OAuth2ClientSecret = credentials.OAuth2ClientSecret;
				existingCredentials.OAuth2GrantType = credentials.OAuth2GrantType;
				existingCredentials.AllowedScopes = credentials.AllowedScopes;
				existingCredentials.RequirePkce = credentials.RequirePkce;
			}
			foreach (var credential in credentialsToAdd)
			{
				client.OAuth2Credentials.Add(credential);
			}
		}

		db.Update(client);
		await db.SaveChangesAsync();

		return client.ToMessage();
	}


	/// <inheritdoc />
	// TODO - Add [Policy(typeof(Client), Permission.Manage)] attribute to block access if user is not in the organization for this client
	public override async Task<AuthCredentialsAllowResponse> AllowAuthCredentials(AuthCredentialsAllowRequest request, ServerCallContext context)
	{
		switch (request.Type)
		{
			case CredentialsType.Oauth2:
				var client = await db.Clients.FindAsync(request.ClientId);
				var allowedClientAuthCredentials = await db.OAuth2ClientCredentials.FindAsync(request.Id);
				if (allowedClientAuthCredentials is null )
					throw new RpcException(new Status(StatusCode.NotFound, "Auth Credentials not found."));
				if (client is null)
					throw new RpcException(new Status(StatusCode.NotFound, "Client not found."));
				client.AllowedClientOAuth2Credentials.Add(allowedClientAuthCredentials);
				await db.SaveChangesAsync();
				return new AuthCredentialsAllowResponse()
				{
					Id = allowedClientAuthCredentials.Id,
					ClientId = client.Id,
					Type = CredentialsType.Oauth2
				};
			default:
				throw new ArgumentOutOfRangeException();
		}
	}

	/// <inheritdoc />
	// TODO - Add [Policy(typeof(Client), Permission.Manage)] attribute to block access if user is not in the organization for this client
	public override async Task<AuthCredentialsAllowResponse> DisallowAuthCredentials(AuthCredentialsAllowRequest request, ServerCallContext context)
	{
		var client = await db.Clients.Include(c => c.AllowedClientOAuth2Credentials).FirstOrDefaultAsync(c => c.Id == request.ClientId);
		if (client is null)
			throw new RpcException(new Status(StatusCode.NotFound, "Client not found."));
		var oauthCredentials = client.AllowedClientOAuth2Credentials?.FirstOrDefault(cred => cred.Id == request.Id);
		if (oauthCredentials is null || client.AllowedClientOAuth2Credentials is null)
			throw new RpcException(new Status(StatusCode.NotFound, "Auth Credentials not found."));
		client.AllowedClientOAuth2Credentials.Remove(oauthCredentials);
		await db.SaveChangesAsync();
		return new AuthCredentialsAllowResponse()
		{
			Id = client.Id,
			ClientId = client.Id,
			Type = CredentialsType.Oauth2
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
