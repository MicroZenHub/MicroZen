using Google.Protobuf.WellKnownTypes;
using Microsoft.EntityFrameworkCore;
using MicroZen.Api.Context;
using MicroZen.Api.Entities;
using MicroZen.Api.Services;
using static Xunit.Assert;
using static MicroZen.Api.Security.APIKeys.APIKeyGenerator;

namespace MicroZen.Core.Api.Test.Services;

public class GetClientIdFromApiKeyTests : IDisposable
{
	private readonly DbContextOptions<MicroZenContext> _options;

	public GetClientIdFromApiKeyTests()
	{
		Environment.SetEnvironmentVariable("EncryptionKey", "1234567890");
		_options = new DbContextOptionsBuilder<MicroZenContext>()
			.UseInMemoryDatabase(databaseName: $"GetClientIdFromApiKeyTests_{Guid.NewGuid():N}")
			.Options;
	}

	[Fact]
	public async Task GetClientIdFromApiKey_ExpiredKey_ReturnsZero()
	{
		// GIVEN an expired API key
		var apiKey = GenerateAPIKey();
		await using var context = new MicroZenContext(_options);
		context.ClientAPIKeys.Add(new ClientAPIKey() { ClientId = 42, ApiKey = apiKey, ExpiresOn = new DateTime(1979, 1, 1) });
		await context.SaveChangesAsync();
		var service = new ClientsGrpcService(context);

		// WHEN the client id is resolved from the key
		var result = await service.GetClientIdFromApiKey(new StringValue() { Value = apiKey }, null!);

		// THEN no client is resolved
		Equal(0, result.Value);
	}

	[Fact]
	public async Task GetClientIdFromApiKey_DeletedKey_ReturnsZero()
	{
		// GIVEN a deleted API key
		var apiKey = GenerateAPIKey();
		await using var context = new MicroZenContext(_options);
		context.ClientAPIKeys.Add(new ClientAPIKey() { ClientId = 42, ApiKey = apiKey, DeletedBy = Guid.NewGuid(), DeletedOn = new DateTime(1970, 1, 1) });
		await context.SaveChangesAsync();
		var service = new ClientsGrpcService(context);

		// WHEN the client id is resolved from the key
		var result = await service.GetClientIdFromApiKey(new StringValue() { Value = apiKey }, null!);

		// THEN no client is resolved
		Equal(0, result.Value);
	}

	[Fact]
	public async Task GetClientIdFromApiKey_ValidKey_ReturnsClientId()
	{
		// GIVEN a valid API key
		var apiKey = GenerateAPIKey();
		await using var context = new MicroZenContext(_options);
		context.ClientAPIKeys.Add(new ClientAPIKey() { ClientId = 42, ApiKey = apiKey });
		await context.SaveChangesAsync();
		var service = new ClientsGrpcService(context);

		// WHEN the client id is resolved from the key
		var result = await service.GetClientIdFromApiKey(new StringValue() { Value = apiKey }, null!);

		// THEN the real client id is returned
		Equal(42, result.Value);
	}

	public void Dispose()
	{
		GC.SuppressFinalize(this);
		Environment.SetEnvironmentVariable("EncryptionKey", null);
	}
}
