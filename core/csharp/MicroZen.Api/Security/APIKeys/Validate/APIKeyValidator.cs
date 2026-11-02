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
		// Cheap malformed-key pre-filter (CRC32 checksum + prefix); NOT a security
		// check. The DB lookup below is the real authorization gate (see #48).
		if (!HasValidFormatAndPrefix(apiKey)) return false;
		return await context.ClientAPIKeys.WhereValid().FirstOrDefaultAsync(clientAPIKey =>
			clientAPIKey.ApiKey == apiKey) != null;
	}

	/// <summary>
	/// Cheap format pre-filter: checks the "mcz" prefix and that the trailing
	/// segment is the correct CRC32 checksum of the key body. This is a
	/// typo/corruption guard only — CRC32 is a checksum, NOT a signature, and
	/// provides no security (it is trivially recomputable, so a key is forgeable
	/// to pass this check). Authorization is the DB-backed key lookup in
	/// <see cref="IsValidAPIKey"/> (a matching, non-expired, non-deleted key; see
	/// issue #48), which is the real gate. This method only rejects malformed keys
	/// early to avoid a DB round-trip.
	/// </summary>
	/// <param name="apiKey">The API key whose format/prefix to check.</param>
	/// <returns>True if the prefix is "mcz" and the CRC32 checksum segment matches.</returns>
	public static bool HasValidFormatAndPrefix(string apiKey)
	{
		var parts = apiKey.Split('_');
		if (parts is not ["mcz", _, _])
			return false;

		return CreateCRC32(parts[1]) == parts[2];
	}
}
