using Microsoft.EntityFrameworkCore;
using MicroZen.Data.Context;
using static Xunit.Assert;
using static MicroZen.Api.Security.APIKeys.APIKeyGenerator;
using static MicroZen.Api.Security.APIKeys.APIKeyValidator;

namespace MicroZen.Core.Api.Test.Security.APIKeys;

public class APIKeyGeneratorTests
{
	private readonly DbContextOptions<MicroZenContext> _options;

	public APIKeyGeneratorTests()
	{
		Environment.SetEnvironmentVariable("EncryptionKey", "1234567890");
		_options = new DbContextOptionsBuilder<MicroZenContext>()
			.UseInMemoryDatabase(databaseName: "APIKeyValidatorTests")
			.Options;
	}

	[Fact]
	public void GenerateAPIKey_ReturnsValidAPIKey()
	{
		// GIVEN
		var apiKey = GenerateAPIKey();

		// WHEN
		var result = HasValidHashAndPrefix(apiKey);

		// THEN
		True(result);
	}

	[Fact]
	public void CreateCRC32_ReturnsValidHash()
	{
		// GIVEN
		var key = Guid.NewGuid().ToString("N");

		// WHEN
		var result = CreateCRC32(key);

		// THEN
		NotNull(result);
		Equal(8, result.Length);
		Equal(CreateCRC32(key), result);
	}
}
