using Microsoft.EntityFrameworkCore;
using MicroZen.Api.Context;
using MicroZen.Api.Entities;
using static Xunit.Assert;

namespace MicroZen.Core.Api.Test.Context;

/// <summary>
/// REFERENCE EXAMPLE — xUnit test exercising a real path.
///
/// Copy this shape when adding backend tests. It drives a genuine
/// EF Core / <see cref="MicroZenContext"/> interaction end to end: an
/// <see cref="Organization"/> entity is persisted, read back, and mapped to its
/// gRPC message via <c>ToMessage()</c>. It runs under <c>dotnet test</c>.
///
/// Conventions demonstrated here:
/// - Setup lives in the constructor (an xUnit fixture runs once per test), and
///   teardown lives in <see cref="Dispose"/> via <see cref="IDisposable"/>.
/// - The in-memory provider is passed through <see cref="DbContextOptions{T}"/>
///   so <c>MicroZenContext.OnConfiguring</c> short-circuits and never touches
///   Postgres — tests stay deterministic and free of external dependencies.
/// - The <c>EncryptionKey</c> environment variable is set because
///   <c>OnModelCreating</c> wires up column encryption.
/// - GIVEN / WHEN / THEN comments mark the arrange / act / assert phases.
/// </summary>
public class MicroZenContextTests : IDisposable
{
	private readonly DbContextOptions<MicroZenContext> _options;

	public MicroZenContextTests()
	{
		// Required by MicroZenContext.OnModelCreating (column encryption).
		Environment.SetEnvironmentVariable("EncryptionKey", "1234567890");

		// A unique database name isolates this fixture from other test classes.
		_options = new DbContextOptionsBuilder<MicroZenContext>()
			.UseInMemoryDatabase(databaseName: $"MicroZenContextTests_{Guid.NewGuid():N}")
			.Options;
	}

	[Fact]
	public async Task Organization_IsPersistedAndQueryable()
	{
		// GIVEN a new Organization entity.
		var organization = new Organization
		{
			Name = "Acme Corp",
			Description = "A reference organization",
			WebsiteUrl = "https://example.com"
		};

		// WHEN it is saved and read back through a fresh context.
		await using (var writeContext = new MicroZenContext(_options))
		{
			writeContext.Organizations.Add(organization);
			await writeContext.SaveChangesAsync();
		}

		await using var readContext = new MicroZenContext(_options);
		var stored = await readContext.Organizations.SingleAsync(o => o.Name == "Acme Corp");

		// THEN the entity round-trips with a generated key and its properties.
		NotEqual(0, stored.Id);
		Equal("Acme Corp", stored.Name);
		Equal("A reference organization", stored.Description);
		Equal("https://example.com", stored.WebsiteUrl);
	}

	[Fact]
	public async Task Organization_ToMessage_MapsToGrpcMessage()
	{
		// GIVEN a persisted Organization.
		await using (var writeContext = new MicroZenContext(_options))
		{
			writeContext.Organizations.Add(new Organization { Name = "Mapped Org" });
			await writeContext.SaveChangesAsync();
		}

		await using var readContext = new MicroZenContext(_options);
		var stored = await readContext.Organizations.SingleAsync(o => o.Name == "Mapped Org");

		// WHEN it is converted to its gRPC message.
		var message = stored.ToMessage();

		// THEN the message carries the entity's values (null fields map to empty strings).
		Equal(stored.Id, message.Id);
		Equal("Mapped Org", message.Name);
		Equal(string.Empty, message.Description);
		Equal(string.Empty, message.AvatarUrl);
		Equal(string.Empty, message.WebsiteUrl);
	}

	public void Dispose()
	{
		GC.SuppressFinalize(this);
		Environment.SetEnvironmentVariable("EncryptionKey", null);
	}
}
