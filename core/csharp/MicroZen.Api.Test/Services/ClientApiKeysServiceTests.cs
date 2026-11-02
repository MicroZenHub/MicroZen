using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using MicroZen.Api.Context;
using MicroZen.Api.Entities;
using MicroZen.Api.Services;
using MicroZen.Grpc.Entities;
using static Xunit.Assert;
using static MicroZen.Api.Security.APIKeys.APIKeyGenerator;

namespace MicroZen.Core.Api.Test.Services;

public class ClientApiKeysServiceTests : IDisposable
{
	private readonly DbContextOptions<MicroZenContext> _options;

	public ClientApiKeysServiceTests()
	{
		Environment.SetEnvironmentVariable("EncryptionKey", "1234567890");
		_options = new DbContextOptionsBuilder<MicroZenContext>()
			.UseInMemoryDatabase(databaseName: $"ClientApiKeysServiceTests_{Guid.NewGuid():N}")
			.Options;
	}

	// UpdateClientApiKey ---------------------------------------------------

	[Fact]
	public async Task UpdateClientApiKey_DeletedKey_ThrowsNotFound()
	{
		// GIVEN a deleted (revoked) API key
		var apiKey = GenerateAPIKey();
		await using var context = new MicroZenContext(_options);
		context.ClientAPIKeys.Add(new ClientAPIKey()
		{
			ClientId = 1,
			ApiKey = apiKey,
			ExpiresOn = DateTime.UtcNow,
			DeletedBy = Guid.NewGuid(),
			DeletedOn = DateTime.UtcNow,
		});
		await context.SaveChangesAsync();
		var service = new ClientApiKeysService(context);
		var request = new UpdateClientApiKeyRequest()
		{
			ApiKey = apiKey,
			ExpiresOn = DateTime.UtcNow.AddDays(30).ToTimestamp(),
		};

		// WHEN an update (re-enable) is attempted THEN it is rejected as NotFound
		var ex = await ThrowsAsync<RpcException>(() => service.UpdateClientApiKey(request, null!));
		Equal(StatusCode.NotFound, ex.StatusCode);
	}

	[Fact]
	public async Task UpdateClientApiKey_ExpiredKey_ThrowsNotFound()
	{
		// GIVEN an expired API key
		var apiKey = GenerateAPIKey();
		await using var context = new MicroZenContext(_options);
		context.ClientAPIKeys.Add(new ClientAPIKey()
		{
			ClientId = 1,
			ApiKey = apiKey,
			ExpiresOn = new DateTime(1979, 1, 1),
		});
		await context.SaveChangesAsync();
		var service = new ClientApiKeysService(context);
		var request = new UpdateClientApiKeyRequest()
		{
			ApiKey = apiKey,
			ExpiresOn = DateTime.UtcNow.AddDays(30).ToTimestamp(),
		};

		// WHEN an update is attempted THEN it is rejected as NotFound
		var ex = await ThrowsAsync<RpcException>(() => service.UpdateClientApiKey(request, null!));
		Equal(StatusCode.NotFound, ex.StatusCode);
	}

	[Fact]
	public async Task UpdateClientApiKey_ValidKey_UpdatesExpiry()
	{
		// GIVEN a valid API key
		var apiKey = GenerateAPIKey();
		await using var context = new MicroZenContext(_options);
		context.ClientAPIKeys.Add(new ClientAPIKey() { ClientId = 1, ApiKey = apiKey });
		await context.SaveChangesAsync();
		var service = new ClientApiKeysService(context);
		var newExpiry = DateTime.UtcNow.AddDays(30);
		var request = new UpdateClientApiKeyRequest()
		{
			ApiKey = apiKey,
			ExpiresOn = newExpiry.ToTimestamp(),
		};

		// WHEN the key is updated
		var response = await service.UpdateClientApiKey(request, null!);

		// THEN the key is returned and the new expiry is persisted
		Equal(apiKey, response.ApiKey);
		var stored = await context.ClientAPIKeys.FirstAsync(x => x.ApiKey == apiKey);
		NotNull(stored.ExpiresOn);
		Equal(newExpiry.ToTimestamp(), stored.ExpiresOn!.Value.ToTimestamp());
	}

	// DisableClientApiKey --------------------------------------------------

	[Fact]
	public async Task DisableClientApiKey_AlreadyDeletedKey_ThrowsNotFound()
	{
		// GIVEN an already-deleted API key
		var apiKey = GenerateAPIKey();
		await using var context = new MicroZenContext(_options);
		context.ClientAPIKeys.Add(new ClientAPIKey()
		{
			ClientId = 1,
			ApiKey = apiKey,
			ExpiresOn = DateTime.UtcNow,
			DeletedBy = Guid.NewGuid(),
			DeletedOn = DateTime.UtcNow,
		});
		await context.SaveChangesAsync();
		var service = new ClientApiKeysService(context);
		var request = new DisableClientApiKeyRequest() { ApiKey = apiKey };

		// WHEN a disable is attempted THEN it is rejected as NotFound (no re-stamp)
		var ex = await ThrowsAsync<RpcException>(() => service.DisableClientApiKey(request, null!));
		Equal(StatusCode.NotFound, ex.StatusCode);
	}

	[Fact]
	public async Task DisableClientApiKey_ValidKey_SetsDeletedOn()
	{
		// GIVEN a valid API key
		var apiKey = GenerateAPIKey();
		await using var context = new MicroZenContext(_options);
		context.ClientAPIKeys.Add(new ClientAPIKey() { ClientId = 1, ApiKey = apiKey });
		await context.SaveChangesAsync();
		var service = new ClientApiKeysService(context);
		var request = new DisableClientApiKeyRequest() { ApiKey = apiKey };

		// WHEN the key is disabled
		var response = await service.DisableClientApiKey(request, null!);

		// THEN the key is returned and DeletedOn / ExpiresOn are stamped
		Equal(apiKey, response.ApiKey);
		var stored = await context.ClientAPIKeys.FirstAsync(x => x.ApiKey == apiKey);
		NotNull(stored.DeletedOn);
		NotNull(stored.ExpiresOn);
	}

	public void Dispose()
	{
		GC.SuppressFinalize(this);
		Environment.SetEnvironmentVariable("EncryptionKey", null);
	}
}
