using MicroZen.Api.Entities;

namespace MicroZen.Api.Security.APIKeys;

/// <summary>
/// Query helpers for <see cref="ClientAPIKey"/> lookups.
/// </summary>
public static class ClientAPIKeyQueryExtensions
{
	/// <summary>
	/// Filters a <see cref="ClientAPIKey"/> query to only keys that are currently
	/// valid: not expired (<see cref="ClientAPIKey.ExpiresOn"/> is null or in the
	/// future) and not deleted (DeletedOn is null).
	/// This is the single source of truth for API key validity so resolve and
	/// mutation lookups cannot drift from <see cref="APIKeyValidator.IsValidAPIKey"/>.
	/// </summary>
	/// <param name="source">The <see cref="ClientAPIKey"/> query to filter.</param>
	/// <returns>The source filtered to valid keys only.</returns>
	public static IQueryable<ClientAPIKey> WhereValid(this IQueryable<ClientAPIKey> source) =>
		source.Where(clientAPIKey =>
			(
				clientAPIKey.ExpiresOn == null ||
				clientAPIKey.ExpiresOn > DateTime.UtcNow
			) && clientAPIKey.DeletedOn == null);
}
