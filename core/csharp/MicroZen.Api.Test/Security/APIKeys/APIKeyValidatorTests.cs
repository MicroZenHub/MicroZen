using Microsoft.EntityFrameworkCore;
using MicroZen.Api.Security.APIKeys;
using MicroZen.Data.Context;
using MicroZen.Data.Entities;
using static Xunit.Assert;
using static MicroZen.Api.Security.APIKeys.APIKeyGenerator;

namespace MicroZen.Core.Api.Test.Security.APIKeys;

public class APIKeyValidatorTests : IDisposable
{
	private readonly DbContextOptions<MicroZenContext> _options;

	public APIKeyValidatorTests()
	{
		Environment.SetEnvironmentVariable("EncryptionKey", "1234567890");
		_options = new DbContextOptionsBuilder<MicroZenContext>()
			.UseInMemoryDatabase(databaseName: "APIKeyValidatorTests")
			.Options;
	}

	[Fact]
	public async Task IsValidAPIKey_NoHash_ReturnsFalse()
	{
		var badApiKey = "mcz_12345678901234567890";

		await using var context = new MicroZenContext(_options);
		context.ClientAPIKeys.Add(new ClientAPIKey() { ClientId = 1, ApiKey = badApiKey });
		await context.SaveChangesAsync();

		var validator = new APIKeyValidator(context);

		// WHEN
		var result = await validator.IsValidAPIKey(badApiKey);

		// THEN
		False(result);
	}

	[Fact]
	public async Task IsValidAPIKey_InvalidHash_ReturnsFalse()
	{
		// GIVEN
		var badApiKey = "mcz_1234567890_1234567890_abcdWrongHash";

		await using var context = new MicroZenContext(_options);
		context.ClientAPIKeys.Add(new ClientAPIKey() { ClientId = 1, ApiKey = badApiKey });
		await context.SaveChangesAsync();

		var validator = new APIKeyValidator(context);

		// WHEN
		var result = await validator.IsValidAPIKey(badApiKey);

		// THEN
		False(result);
	}

	[Fact]
	public async Task IsValidAPIKey_InvalidPrefix_ReturnsFalse()
	{
		// GIVEN
		var key = $"{Guid.NewGuid():N}";
		var missingApiKey = $"abc_{key}_{CreateCRC32(key)}";

		await using var context = new MicroZenContext(_options);
		context.ClientAPIKeys.Add(new ClientAPIKey() { ClientId = 1, ApiKey = missingApiKey });
		await context.SaveChangesAsync();

		var validator = new APIKeyValidator(context);

		// WHEN
		var result = await validator.IsValidAPIKey(missingApiKey);

		// THEN
		False(result);
	}

	[Fact]
	public async Task IsValidAPIKey_ValidHashButNotInDb_ReturnsFalse()
	{
		// GIVEN
		var missingApiKey = GenerateAPIKey();

		await using var context = new MicroZenContext(_options);
		context.ClientAPIKeys.Add(new ClientAPIKey() { ClientId = 1, ApiKey = GenerateAPIKey() });
		await context.SaveChangesAsync();

		var validator = new APIKeyValidator(context);

		// WHEN
		var result = await validator.IsValidAPIKey(missingApiKey);

		// THEN
		False(result);
	}

	[Fact]
	public async Task IsValidAPIKey_InDbButExpired_ReturnsFalse()
	{
		// GIVEN
		var apiKey = GenerateAPIKey();

		await using var context = new MicroZenContext(_options);
		context.ClientAPIKeys.Add(new ClientAPIKey() { ClientId = 1, ApiKey = apiKey, ExpiresOn = new DateTime(1979, 1, 1)});
		await context.SaveChangesAsync();

		var validator = new APIKeyValidator(context);

		// WHEN
		var result = await validator.IsValidAPIKey(apiKey);

		// THEN
		False(result);
	}

	[Fact]
	public async Task IsValidAPIKey_InDbButDeleted_ReturnsFalse()
	{
		// GIVEN
		var apiKey = GenerateAPIKey();

		await using var context = new MicroZenContext(_options);
		context.ClientAPIKeys.Add(new ClientAPIKey() { ClientId = 1, ApiKey = apiKey, DeletedBy = Guid.NewGuid(), DeletedOn = new DateTime(1970, 1, 1)});
		await context.SaveChangesAsync();

		var validator = new APIKeyValidator(context);

		// WHEN
		var result = await validator.IsValidAPIKey(apiKey);

		// THEN
		False(result);
	}

	[Fact]
	public async Task IsValidAPIKey_ValidHashInDb_ReturnsTrue()
	{
		// GIVEN
		var apiKey = "mcz_d058179e201049dbbad01aa68de8bc4c_e9ea32a6";

		await using var context = new MicroZenContext(_options);
		context.ClientAPIKeys.Add(new ClientAPIKey() { ClientId = 1, ApiKey = apiKey });
		await context.SaveChangesAsync();

		var validator = new APIKeyValidator(context);

		// WHEN
		var result = await validator.IsValidAPIKey(apiKey);

		// THEN
		True(result);
	}

	public void Dispose()
	{
		GC.SuppressFinalize(this);
		Environment.SetEnvironmentVariable("EncryptionKey", null);
	}
}
