using Microsoft.Extensions.Configuration;

namespace MicroZen.OAuth2.Providers.Cognito.Config;

/// <summary>
/// POCO representing the necessary config values for Cognito ID token auth.
/// </summary>
public struct CognitoTokenConfig
{
	/// <summary>
	/// Constructor for CognitoTokenConfig
	/// </summary>
	/// <param name="configurationSection">The IConfiguration section for either Cognito UserTokenConfig or ServerToServerTokenConfig:</param>
	/// <exception cref="ArgumentNullException">Will throw ArgumentNullException if ClientIds, Region, or UserPoolId are null</exception>
	public CognitoTokenConfig(IConfigurationSection configurationSection)
	{
		ClientIds = configurationSection.GetValue<string>("ClientIds")?.Split() ?? throw new ArgumentNullException(nameof(ClientIds));
		ClientSecrets = configurationSection.GetValue<string>("ClientSecrets")?.Split();
		Region = configurationSection.GetValue<string>("Region") ?? throw new ArgumentNullException(nameof(Region));
		UserPoolId = configurationSection.GetValue<string>("UserPoolId") ?? throw new ArgumentNullException(nameof(UserPoolId));
	}

	/// <summary>
	/// The client IDs belonging to the Cognito User Pool clients that are allowed to access the app
	/// </summary>
	public IEnumerable<string> ClientIds { get; set; }

	/// <summary>
	/// The client secrets of the Cognito User Pool clients (optional - depending on flow)
	/// </summary>
	public IEnumerable<string>? ClientSecrets { get; set; }

	/// <summary>
	/// The region of the Cognito User Pool
	/// </summary>
	public string Region { get; set; }

	/// <summary>
	/// The ID of the Cognito User Pool
	/// </summary>
	public string UserPoolId { get; set; }
}
