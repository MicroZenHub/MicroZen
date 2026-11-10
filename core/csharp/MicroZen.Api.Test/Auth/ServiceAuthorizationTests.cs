using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using static Xunit.Assert;

namespace MicroZen.Core.Api.Test.Auth;

/// <summary>
/// Integration tests proving #47 is closed: the Clients, Organizations, and
/// OrganizationUsers gRPC services (plus a regression guard on ClientApiKeys) reject
/// unauthenticated callers with 401, while an authenticated caller passes the auth gate.
///
/// Calls go through the gRPC-JSON transcoding endpoints (see the google.api.http
/// annotations in the Protos) against the real <c>Program</c> pipeline, so the
/// <c>UseMicroZenCognito</c> middleware added in #47 is exercised. See
/// <see cref="MicroZenApiFactory"/> for how authentication is stubbed per request.
/// </summary>
public class ServiceAuthorizationTests(MicroZenApiFactory factory) : IClassFixture<MicroZenApiFactory>
{
	private HttpClient AnonymousClient() => factory.CreateClient();

	private HttpClient AuthenticatedClient()
	{
		var client = factory.CreateClient();
		client.DefaultRequestHeaders.Add(MicroZenApiFactory.AuthenticateHeader, "true");
		return client;
	}

	/// <summary>
	/// At least one read and one mutating RPC per protected service. Each case uses the
	/// HTTP route and method from the service's proto (google.api.http) transcoding option.
	/// </summary>
	public static TheoryData<string, HttpMethod, string> ProtectedEndpoints() => new()
	{
		// ClientsGrpcService — read + mutate.
		{ "GetClient", HttpMethod.Get, "/api/v1/client/1" },
		{ "CreateClient", HttpMethod.Post, "/api/v1/client" },
		{ "DeleteClient", HttpMethod.Delete, "/api/v1/client/1" },
		// OrganizationsService — read + mutate.
		{ "GetOrganization", HttpMethod.Get, "/api/v1/organization/1" },
		{ "UpsertOrganization", HttpMethod.Put, "/api/v1/organization" },
		{ "DisableOrganization", HttpMethod.Delete, "/api/v1/organization/1/disable" },
		// OrganizationUsersService — read.
		{ "GetOrganizationUsers", HttpMethod.Get, "/api/v1/users/organization/1" },
		// ClientApiKeysService — regression guard: it already had [Authorize] but the
		// middleware was never wired, so it was effectively open until #47.
		{ "RequestClientApiKey", HttpMethod.Post, "/api/v1/client-api-key" },
	};

	[Theory]
	[MemberData(nameof(ProtectedEndpoints))]
	public async Task UnauthenticatedCall_IsRejectedWith401(string rpc, HttpMethod method, string route)
	{
		// GIVEN an anonymous caller (no bearer / no authenticated principal).
		var client = AnonymousClient();

		// WHEN it invokes a protected RPC over the JSON-transcoding endpoint.
		var response = await Send(client, method, route);

		// THEN the request is rejected at the auth layer before any handler runs.
		// gRPC-JSON transcoding maps the Unauthenticated status to HTTP 401.
		Equal(HttpStatusCode.Unauthorized, response.StatusCode);
		_ = rpc; // named for test readability / failure output.
	}

	[Theory]
	[MemberData(nameof(ProtectedEndpoints))]
	public async Task AuthenticatedCall_PassesAuthGate(string rpc, HttpMethod method, string route)
	{
		// GIVEN an authenticated caller.
		var client = AuthenticatedClient();

		// WHEN it invokes the same protected RPC.
		var response = await Send(client, method, route);

		// THEN it is no longer rejected at the auth layer. A business result
		// (e.g. NotFound mapped to 404, or OK) is acceptable — the point is auth passed.
		NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
		_ = rpc;
	}

	/// <summary>
	/// Middleware regression guard for the secondary finding in #47: if
	/// UseAuthentication/UseAuthorization were dropped from the pipeline, an anonymous call
	/// to an <c>[Authorize]</c> service would return 200 instead of 401. This fails loudly
	/// in that case.
	/// </summary>
	[Fact]
	public async Task AnonymousCall_DoesNotReturnSuccess_MiddlewareRegressionGuard()
	{
		var client = AnonymousClient();

		var response = await Send(client, HttpMethod.Get, "/api/v1/client/1");

		Equal(HttpStatusCode.Unauthorized, response.StatusCode);
		False(response.IsSuccessStatusCode, "An anonymous call to an [Authorize] service must not succeed.");
	}

	private static async Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string route)
	{
		using var request = new HttpRequestMessage(method, route);
		if (method == HttpMethod.Post || method == HttpMethod.Put || method == HttpMethod.Delete)
			request.Content = JsonContent.Create(new { });
		return await client.SendAsync(request);
	}
}
