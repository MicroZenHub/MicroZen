extern alias MicroZenCore;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Reactive.Subjects;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using CoreState = MicroZenCore::MicroZen.Core.State.MicroZenOAuth2State;
using CoreOAuth2State = MicroZenCore::MicroZen.Core.State.OAuth2State;
using CoreClient = MicroZenCore::MicroZen.Grpc.V1.ClientsGrpcService.ClientsGrpcServiceClient;
using Credentials = MicroZenCore::MicroZen.Grpc.V1.OAuth2Credentials;
using GrantType = MicroZenCore::MicroZen.Core.Definitions.OAuth2GrantType;

namespace MicroZen.Core.Api.Test.Integration.Harness;

/// <summary>
/// The seam a provider package (Cognito today, Supabase in #70) implements so the same
/// token cases run against it. A new provider adds one implementation of this and one test class.
/// </summary>
public interface IAuthProviderUnderTest
{
	string Name { get; }

	/// <summary>The <c>iss</c> the provider expects for the given client registration.</summary>
	string IssuerFor(Credentials registration);

	/// <summary>Registers the provider's JWT validation exactly as the real entry point does.</summary>
	void AddAuthentication(AuthenticationBuilder builder, IServiceCollection services, GrantType[] grantTypes);
}

/// <summary>
/// A real <c>TestServer</c> running the real provider validation, with one protected endpoint
/// and a <see cref="FakeOidcAuthority"/> behind the JWT backchannel.
/// </summary>
public sealed class JwtTestHost : IAsyncDisposable
{
	public const string ProtectedPath = "/protected";

	private readonly WebApplication _app;
	private readonly CoreState _state;

	public HttpClient Client { get; }

	private JwtTestHost(WebApplication app, CoreState state)
	{
		_app = app;
		_state = state;
		Client = app.GetTestClient();
	}

	/// <summary>
	/// Starts a host that accepts <paramref name="grantTypes"/> for the clients in <paramref name="registrations"/>.
	/// </summary>
	public static async Task<JwtTestHost> StartAsync(
		IAuthProviderUnderTest provider,
		FakeOidcAuthority authority,
		IEnumerable<Credentials> registrations,
		params GrantType[] grantTypes)
	{
		var builder = WebApplication.CreateBuilder();
		builder.WebHost.UseTestServer();
		builder.Logging.ClearProviders(); builder.Logging.AddFilter("Microsoft.AspNetCore.Authentication", LogLevel.Debug);
		builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
		{
			["MicroZen:AuthorityUrl"] = "http://localhost",
			["MicroZen:APIKey"] = "test-key",
		});

		// Core state is normally filled by polling the MicroZen authority over gRPC. Tests fill it directly.
		var state = new CoreState(
			builder.Configuration,
			NullLogger<CoreState>.Instance,
			new CoreClient(GrpcChannel.ForAddress("http://localhost")));
		if (registrations.Any()) Publish(state, registrations);
		builder.Services.AddSingleton(state);

		System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
		provider.AddAuthentication(builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme), builder.Services, grantTypes);

		// Send discovery + JWKS requests to the in-process authority. Nothing else about validation is touched.
		builder.Services.ConfigureAll<JwtBearerOptions>(o =>
		{
			o.BackchannelHttpHandler = authority.Handler;
			o.RefreshInterval = TimeSpan.FromSeconds(1); // lowest the library allows; makes key rotation testable
		});
		builder.Services.AddAuthorization(o =>
			o.DefaultPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

		var app = builder.Build();
		app.UseAuthentication();
		app.UseAuthorization();
		app.MapGet(ProtectedPath, () => "ok").RequireAuthorization();
		await app.StartAsync();
		return new JwtTestHost(app, state);
	}

	/// <summary>Replaces the allowed-client list, as a successful poll of the MicroZen authority would.</summary>
	public void Publish(IEnumerable<Credentials> registrations) => Publish(_state, registrations);

	private static void Publish(CoreState state, IEnumerable<Credentials> registrations)
	{
		var subject = (BehaviorSubject<CoreOAuth2State?>)typeof(CoreState)
			.GetField("_oauth2Subject", BindingFlags.Instance | BindingFlags.NonPublic)!
			.GetValue(state)!;
		subject.OnNext(new CoreOAuth2State { ClientCredentials = registrations.ToList() });
	}

	public Task<HttpResponseMessage> CallAsync(string? bearerToken, string scheme = "Bearer")
	{
		using var request = new HttpRequestMessage(HttpMethod.Get, ProtectedPath);
		if (bearerToken is not null)
			request.Headers.Authorization = new AuthenticationHeaderValue(scheme, bearerToken);
		return Client.SendAsync(request);
	}

	public async ValueTask DisposeAsync()
	{
		Client.Dispose();
		await _app.StopAsync();
		await _app.DisposeAsync();
		_state.Dispose();
	}
}
