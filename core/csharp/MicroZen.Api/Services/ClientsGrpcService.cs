using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using LinqKit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using MicroZen.Api.Context;
using MicroZen.Api.Entities;
using MicroZen.Api.Entities.AuthCredentials;
using MicroZen.Api.Security.APIKeys;
using MicroZen.Grpc.V1;

namespace MicroZen.Api.Services;

/// <summary>
/// The Clients gRPC/REST service.
/// </summary>
/// <param name="db"><see cref="MicroZenContext"/></param>
[Authorize]
public class ClientsGrpcService(MicroZenContext db) : MicroZen.Grpc.V1.ClientsGrpcService.ClientsGrpcServiceBase
{
	/// <summary>
	/// The default number of clients to return per page when the caller does not specify a page size.
	/// </summary>
	private const int DefaultPageSize = 10;

	/// <inheritdoc />
	public override async Task<ManyClientsResponse> GetManyClients(ManyClientsRequest request, ServerCallContext context)
	{
		var predicate = PredicateBuilder.New<Client>(true);
		predicate.And(c => c.Type == request.Type);
		if (!string.IsNullOrEmpty(request.SearchTerm))
		{
			// Case-insensitive name contains. Provider-agnostic on purpose:
			// Npgsql translates ToLower()/Contains() to SQL LOWER()/strpos, and
			// the InMemory provider the test suite uses evaluates it in memory.
			// (Replaces the Npgsql-only EF.Functions.ILike, which neither the
			// InMemory nor Sqlite provider can translate — see PR description.)
			var searchTerm = request.SearchTerm.ToLower();
			predicate.And(c => c.Name.ToLower().Contains(searchTerm));
		}

		var response = new ManyClientsResponse();
		var page = request.Page > 0 ? request.Page : 1; // 1-based page number
		var pageSize = request.Skip > 0 ? request.Skip : DefaultPageSize; // rows per page
		var skip = (page - 1) * pageSize;

		var total = await db.Clients.Where(predicate).CountAsync();
		var clients = await db.Clients
			.Include(c => c.OAuth2Credentials)
			.Include(c => c.AllowedClientOAuth2Credentials)
			.Where(predicate)
			.OrderBy(c => c.Name)
			.Skip(skip)
			.Take(pageSize)
			.Select(c => c.ToMessage())
			.ToListAsync();

		// There is a next page only if more rows exist beyond the current window.
		var hasMore = skip + clients.Count < total;
		response.Total = total;
		if (hasMore)
		{
			response.NextUrl =
				$"clients?page={page + 1}&skip={pageSize}&searchTerm={request.SearchTerm}&type={request.Type.ToString().ToUpper()}";
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
		var client = await db.Clients
			.Include(c => c.AllowedClientOAuth2Credentials)
			.FirstOrDefaultAsync(c => c.Id == request.Id);

		if (client is null)
			throw new RpcException(new Status(StatusCode.NotFound, "Client not found."));

		var response = new MultipleClientCredentials();
		response.Credentials.AddRange(
			client.AllowedClientOAuth2Credentials.Select(c => c.ToMessage()));
		return response;
	}

	// TODO - Add [Policy(typeof(Client), Permission.Read)] attribute to block access if user is not in the organization for this client
	/// <inheritdoc />
	public override async Task<Int32Value> GetClientIdFromApiKey(StringValue request, ServerCallContext context)
	{
		var clientId = (await db.ClientAPIKeys
			.WhereValid()
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
	public override async Task<AuthorizedClientsResponse> AuthorizeClients(AuthorizeClientsRequest request, ServerCallContext context)
	{
		var client = await db.Clients
			.Include(c => c.AllowedClientOAuth2Credentials)
			.FirstOrDefaultAsync(c => c.Id == request.Id);
		if (client is null)
			throw new RpcException(new Status(StatusCode.NotFound, "Client not found."));

		var authorizedCredentials = new List<OAuth2ClientCredentials>();
		foreach (var authorizedClient in request.AuthorizedClients)
		{
			switch (authorizedClient.Type)
			{
				case CredentialsType.Oauth2:
					var allowedClientAuthCredentials = await db.OAuth2ClientCredentials.FindAsync(authorizedClient.Id);
					if (allowedClientAuthCredentials is null)
						throw new RpcException(new Status(StatusCode.NotFound, "Auth Credentials not found."));
					client.AllowedClientOAuth2Credentials.Add(allowedClientAuthCredentials);
					authorizedCredentials.Add(allowedClientAuthCredentials);
					break;
				default:
					throw new RpcException(new Status(StatusCode.Unimplemented, "Unsupported credentials type."));
			}
		}

		await db.SaveChangesAsync();

		var response = new AuthorizedClientsResponse();
		response.AuthorizedClients.AddRange(authorizedCredentials.Select(credential => new AuthorizedClient
		{
			Id = credential.Id,
			ClientId = request.Id,
			Type = CredentialsType.Oauth2
		}));
		return response;
	}

	/// <inheritdoc />
	// TODO - Add [Policy(typeof(Client), Permission.Manage)] attribute to block access if user is not in the organization for this client
	public override async Task<EmptyResponse> DisallowClients(DisallowClientsRequest request, ServerCallContext context)
	{
		var client = await db.Clients
			.Include(c => c.AllowedClientOAuth2Credentials)
			.FirstOrDefaultAsync(c => c.Id == request.Id);
		if (client is null)
			throw new RpcException(new Status(StatusCode.NotFound, "Client not found."));

		foreach (var disallowedClient in request.DisallowedClients)
		{
			var oauthCredentials = client.AllowedClientOAuth2Credentials
				.FirstOrDefault(cred => cred.Id == disallowedClient.Id);
			if (oauthCredentials is not null)
				client.AllowedClientOAuth2Credentials.Remove(oauthCredentials);
		}

		await db.SaveChangesAsync();
		return new EmptyResponse();
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
