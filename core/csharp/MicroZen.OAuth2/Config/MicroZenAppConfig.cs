namespace MicroZen.OAuth2.Config;

/// <summary>
/// The MicroZen App Configuration
/// </summary>
public class MicroZenAppConfig
{
	/// <summary>
	/// The URL where this app's MicroZen Authority is located
	/// </summary>
	public required string AuthorityUrl { get; set; }

	/// <summary>
	/// The MicroZen APIKey for this app
	/// </summary>
	public required string APIKey { get; set; }

	/// <summary>
	/// The minute interval at which to ping the MicroZen server
	/// </summary>
	public int Interval { get; set; } = 5;
}
