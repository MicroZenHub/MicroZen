using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using MicroZen.Core.Api.Test.Auth;
using static Xunit.Assert;

namespace MicroZen.Core.Api.Test.Cors;

/// <summary>
/// Tests proving #52 is closed: the API's CORS policy is the explicit, named
/// allow-all policy (origins/methods/headers) and — critically — does NOT allow
/// credentials. ASP.NET Core forbids combining <c>AllowAnyOrigin()</c> with
/// <c>AllowCredentials()</c> and throws during CORS startup, so a cleanly
/// building host plus <c>SupportsCredentials == false</c> is the guard against a
/// future edit adding credentials (which would crash the app).
///
/// The host is built via <see cref="MicroZenApiFactory"/>, which runs the real
/// <c>Program</c> pipeline (with the named CORS policy registered the same way
/// the app registers it), so these tests exercise the actual configuration.
/// </summary>
public class CorsPolicyTests(MicroZenApiFactory factory) : IClassFixture<MicroZenApiFactory>
{
	/// <summary>
	/// Name of the policy registered in <c>Program.cs</c> via <c>AddPolicy</c>.
	/// </summary>
	private const string PolicyName = "AllowAllOrigins";

	[Fact]
	public void Host_Starts_WithCorsPolicyRegistered_AndDoesNotThrow()
	{
		// GIVEN the real Program pipeline is hosted (CORS registered as in the app).
		// WHEN the host is built and a request is served.
		// (Building the client forces the host — and the CORS startup validation —
		// to run; the forbidden AllowAnyOrigin + AllowCredentials combination would
		// throw here.)
		using var client = factory.CreateClient();

		// THEN the service provider resolves the CORS policy provider without throwing.
		var provider = factory.Services.GetService<ICorsPolicyProvider>();

		// A resolvable provider plus a cleanly started host proves the constraint
		// (no AllowCredentials with AllowAnyOrigin) is honored.
		NotNull(provider);
		_ = client; // host started successfully.
	}

	[Fact]
	public async Task Policy_IsAllowAll_AndCredentialFree()
	{
		// GIVEN the host hosting the real Program pipeline.
		// (Force the host to build so the policy provider is available.)
		using var _ = factory.CreateClient();
		var provider = factory.Services.GetRequiredService<ICorsPolicyProvider>();

		// WHEN the named allow-all policy is resolved.
		var policy = await provider.GetPolicyAsync(new DefaultHttpContext(), PolicyName);

		// THEN the policy exists and is allow-all with credentials OFF.
		NotNull(policy);
		True(policy!.AllowAnyOrigin, "Policy must allow any origin (product decision).");
		True(policy.AllowAnyMethod, "Policy must allow any method.");
		True(policy.AllowAnyHeader, "Policy must allow any header.");
		// The critical assertion: credentials must stay OFF. AllowAnyOrigin +
		// SupportsCredentials is illegal in ASP.NET Core and crashes at startup; this
		// guards against a future edit adding AllowCredentials().
		False(policy.SupportsCredentials, "Policy must NOT allow credentials (illegal with AllowAnyOrigin).");
	}

	[Fact]
	public async Task Response_CarriesAllowAllOrigin_AndNoAllowCredentialsHeader()
	{
		// GIVEN an authenticated caller and a cross-origin request.
		var client = factory.CreateClient();
		client.DefaultRequestHeaders.Add(MicroZenApiFactory.AuthenticateHeader, "true");

		using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/client/1");
		request.Headers.Add("Origin", "https://some-third-party.example");

		// WHEN the request is sent through the real pipeline (UseCors applies the policy).
		var response = await client.SendAsync(request);

		// THEN the response advertises allow-all origin and no credentials header.
		NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
		True(
			response.Headers.TryGetValues("Access-Control-Allow-Origin", out var allowOrigin),
			"Expected Access-Control-Allow-Origin on a cross-origin response.");
		Contains("*", allowOrigin!);
		False(
			response.Headers.Contains("Access-Control-Allow-Credentials"),
			"Allow-all policy must NOT emit Access-Control-Allow-Credentials.");
	}
}
