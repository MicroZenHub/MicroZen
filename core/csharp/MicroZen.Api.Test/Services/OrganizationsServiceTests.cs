using System.Threading;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using MicroZen.Api.Context;
using MicroZen.Api.Services;
using MicroZen.Grpc.V1;
using static Xunit.Assert;

namespace MicroZen.Core.Api.Test.Services;

/// <summary>
/// Tests for <see cref="OrganizationsService"/>, covering must-fix #49: the
/// create branch of <c>UpsertOrganization</c> must let the database assign the
/// key instead of forcing <c>Id = -1</c>, so more than one organization can be
/// created without a primary-key collision.
///
/// Follows the backend test conventions from
/// <c>MicroZenContextTests</c>: EF Core InMemory provider, a self-supplied
/// <c>EncryptionKey</c>, and GIVEN / WHEN / THEN phases.
/// </summary>
public class OrganizationsServiceTests : IDisposable
{
	private readonly DbContextOptions<MicroZenContext> _options;

	public OrganizationsServiceTests()
	{
		// Required by MicroZenContext.OnModelCreating (column encryption).
		Environment.SetEnvironmentVariable("EncryptionKey", "1234567890");

		// A unique database name isolates this fixture from other test classes.
		_options = new DbContextOptionsBuilder<MicroZenContext>()
			.UseInMemoryDatabase(databaseName: $"OrganizationsServiceTests_{Guid.NewGuid():N}")
			.Options;
	}

	[Fact]
	public async Task UpsertOrganization_CreatesTwoOrganizations_WithDistinctGeneratedIds()
	{
		// GIVEN a service over a fresh in-memory context and two create requests.
		await using var db = new MicroZenContext(_options);
		var service = new OrganizationsService(db);

		var first = new OrganizationMessage
		{
			Name = "First Org",
			Description = "The first organization",
			AvatarUrl = "https://example.com/first.png",
			WebsiteUrl = "https://first.example.com"
		};
		var second = new OrganizationMessage
		{
			Name = "Second Org",
			Description = "The second organization",
			AvatarUrl = "https://example.com/second.png",
			WebsiteUrl = "https://second.example.com"
		};

		// WHEN both are created in sequence through UpsertOrganization.
		var firstResult = await service.UpsertOrganization(first, TestServerCallContext.Instance);
		var secondResult = await service.UpsertOrganization(second, TestServerCallContext.Instance);

		// THEN the first result carries a real generated key, not the -1 sentinel.
		NotEqual(-1, firstResult.Id);
		NotEqual(0, firstResult.Id);

		// AND the second create succeeds (no primary-key collision) with its own id.
		NotEqual(-1, secondResult.Id);
		NotEqual(0, secondResult.Id);

		// AND the two organizations have distinct database-assigned ids.
		NotEqual(firstResult.Id, secondResult.Id);

		// AND both rows persist.
		Equal(2, await db.Organizations.CountAsync());

		// AND each returned message carries the input field values.
		Equal("First Org", firstResult.Name);
		Equal("The first organization", firstResult.Description);
		Equal("https://example.com/first.png", firstResult.AvatarUrl);
		Equal("https://first.example.com", firstResult.WebsiteUrl);

		Equal("Second Org", secondResult.Name);
		Equal("The second organization", secondResult.Description);
		Equal("https://example.com/second.png", secondResult.AvatarUrl);
		Equal("https://second.example.com", secondResult.WebsiteUrl);
	}

	public void Dispose()
	{
		GC.SuppressFinalize(this);
		Environment.SetEnvironmentVariable("EncryptionKey", null);
	}

	/// <summary>
	/// Minimal <see cref="ServerCallContext"/> stub. <c>UpsertOrganization</c>
	/// does not read the call context, so an inert instance is enough to drive
	/// the gRPC service method directly.
	/// </summary>
	private sealed class TestServerCallContext : ServerCallContext
	{
		public static readonly TestServerCallContext Instance = new();

		protected override string MethodCore => string.Empty;
		protected override string HostCore => string.Empty;
		protected override string PeerCore => string.Empty;
		protected override DateTime DeadlineCore => DateTime.MaxValue;
		protected override Metadata RequestHeadersCore => new();
		protected override CancellationToken CancellationTokenCore => CancellationToken.None;
		protected override Metadata ResponseTrailersCore => new();
		protected override Status StatusCore { get; set; }
		protected override WriteOptions? WriteOptionsCore { get; set; }
		protected override AuthContext AuthContextCore => new(string.Empty, new Dictionary<string, List<AuthProperty>>());

		protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options) =>
			throw new NotSupportedException();

		protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) =>
			Task.CompletedTask;
	}
}
