using Microsoft.EntityFrameworkCore;
using MicroZen.Api.Context;
using static MicroZen.Api.Security.APIKeys.APIKeyGenerator;

namespace MicroZen.Api.Security.APIKeys;

/// <summary>
/// Interface for API Key validation
/// </summary>
public interface IAPIKeyValidation
{
	/// <summary>
	/// Check if the API Key is valid
	/// </summary>
	/// <param name="apiKey">The API Key to be validated</param>
	/// <returns></returns>
	Task<bool> IsValidAPIKey(string apiKey);
}

/// <summary>
/// Validator to ensure the API Key is valid
/// </summary>
/// <param name="context"><see cref="MicroZenContext"/></param>
public class APIKeyValidator(MicroZenContext context) : IAPIKeyValidation
{
	/// <inheritdoc />
	public async Task<bool> IsValidAPIKey(string apiKey)
	{
		if (!HasValidHashAndPrefix(apiKey)) return false;
		return await context.ClientAPIKeys.FirstOrDefaultAsync(clientAPIKey =>
			clientAPIKey.ApiKey == apiKey &&
			(
				clientAPIKey.ExpiresOn == null ||
				clientAPIKey.ExpiresOn > DateTime.UtcNow
			) && clientAPIKey.DeletedOn == null) != null;
	}

	/// <summary>
	/// Validates if the API key hash is a valid hash
	/// </summary>
	/// <param name="apiKey">The API Key to validate the hash for</param>
	/// <returns></returns>
	public static bool HasValidHashAndPrefix(string apiKey)
	{
		var parts = apiKey.Split('_');
		if (parts is not ["mcz", _, _])
			return false;

		return CreateCRC32(parts[1]) == parts[2];
	}
}
