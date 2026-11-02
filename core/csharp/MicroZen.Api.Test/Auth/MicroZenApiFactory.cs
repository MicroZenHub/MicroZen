using System.Linq;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MicroZen.Api.Context;

namespace MicroZen.Core.Api.Test.Auth;

/// <summary>
/// Hosts the real <c>Program</c> pipeline (including the <c>UseMicroZenOAuth2</c>
/// middleware wired for #47) so the auth tests exercise authentication/authorization
/// end to end. Postgres is swapped for the EF Core InMemory provider and the Cognito
/// JWT scheme is replaced by a deterministic <see cref="TestAuthHandler"/> that lets a
/// test decide, per request, whether a principal is present.
/// </summary>
public class MicroZenApiFactory : WebApplicationFactory<Program>
{
	/// <summary>
	/// A request carrying this header is treated as authenticated; its absence means
	/// anonymous, so the <c>[Authorize]</c> default policy rejects the call with 401.
	/// </summary>
	public const string AuthenticateHeader = "X-Test-Authenticated";

	private readonly string _databaseName = $"MicroZenApiFactory_{Guid.NewGuid():N}";

	public MicroZenApiFactory()
	{
		// AddMicroZenOAuth2 reads IConfiguration while the host is being built — before
		// ConfigureTestServices runs — so these must be visible as soon as
		// builder.Configuration exists. Environment variables are read by the default
		// configuration and are present from process start.
		// Required by MicroZenContext column encryption.
		Environment.SetEnvironmentVariable("EncryptionKey", "1234567890");
		// A syntactically valid URL is enough; no network call is made during these tests.
		Environment.SetEnvironmentVariable("MicroZen__AuthorityUrl", "https://localhost:7070");
		Environment.SetEnvironmentVariable("MicroZen__APIKey", "mcz_test_0000000000000000000000000000000_00000000");
	}

	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		// A non-Development environment skips MigrateAndSeedDevDataOnStart's seeding path.
		builder.UseEnvironment("Testing");

		builder.ConfigureTestServices(services =>
		{
			// Replace the Npgsql-backed DbContext registrations with the InMemory provider so
			// the tests are self-contained (no Postgres), matching the existing test pattern.
			RemoveDbContextRegistrations(services);

			services.AddDbContext<DbContext, MicroZenContext>(options =>
				options.UseInMemoryDatabase(_databaseName));
			services.AddScoped(sp => (MicroZenContext)sp.GetRequiredService<DbContext>());

			// Swap the Cognito JWT scheme for a deterministic test scheme and make it the
			// default, so the [Authorize] default policy (RequireAuthenticatedUser) resolves
			// against TestAuthHandler.
			services.AddAuthentication(TestAuthHandler.SchemeName)
				.AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
			services.PostConfigure<AuthenticationOptions>(options =>
			{
				options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
				options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
				options.DefaultScheme = TestAuthHandler.SchemeName;
			});
		});
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			Environment.SetEnvironmentVariable("EncryptionKey", null);
			Environment.SetEnvironmentVariable("MicroZen__AuthorityUrl", null);
			Environment.SetEnvironmentVariable("MicroZen__APIKey", null);
		}
		base.Dispose(disposing);
	}

	private static void RemoveDbContextRegistrations(IServiceCollection services)
	{
		foreach (var descriptor in services
			.Where(d =>
				d.ServiceType == typeof(DbContextOptions)
				|| d.ServiceType == typeof(DbContextOptions<MicroZenContext>)
				|| d.ServiceType == typeof(MicroZenContext)
				|| d.ServiceType == typeof(DbContext)
				// The provider-specific options configuration (Npgsql) is keyed by an
				// internal type; match the whole DbContextOptions configuration surface.
				|| (d.ServiceType.IsGenericType
					&& d.ServiceType.GetGenericTypeDefinition().Name.StartsWith("IDbContextOptionsConfiguration")))
			.ToList())
		{
			services.Remove(descriptor);
		}
	}
}

/// <summary>
/// Authentication handler used only by the test host. It authenticates a request when the
/// <see cref="MicroZenApiFactory.AuthenticateHeader"/> header is present and otherwise reports
/// no result, which drives the <c>[Authorize]</c> default policy to challenge with 401.
/// </summary>
public class TestAuthHandler(
	IOptionsMonitor<AuthenticationSchemeOptions> options,
	ILoggerFactory logger,
	UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
	public const string SchemeName = "Test";

	protected override Task<AuthenticateResult> HandleAuthenticateAsync()
	{
		if (!Request.Headers.ContainsKey(MicroZenApiFactory.AuthenticateHeader))
			return Task.FromResult(AuthenticateResult.NoResult());

		var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "test-user") };
		var identity = new ClaimsIdentity(claims, SchemeName);
		var principal = new ClaimsPrincipal(identity);
		var ticket = new AuthenticationTicket(principal, SchemeName);
		return Task.FromResult(AuthenticateResult.Success(ticket));
	}
}
