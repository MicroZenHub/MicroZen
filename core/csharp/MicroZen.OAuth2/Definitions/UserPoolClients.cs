namespace MicroZen.OAuth2.Definitions;

/// <summary>
/// Represents the credentials of the OAuth2 clients that are allowed to access the app grouped by UserPoolId and Region
/// </summary>
public record UserPoolClients
{
	/// <summary>
	/// The UserPoolId of the Cognito User Pool
	/// </summary>
	public required string UserPoolId { get; init; }

	/// <summary>
	/// The AWS Region of the Cognito User Pool
	/// </summary>
	public required string Region { get; init; }

	/// <summary>
	/// The ClientIds of the OAuth2 clients that are allowed to access the app
	/// </summary>
	public required string[] ClientIds { get; init; }


	/// <summary>
	/// The ClientSecrets of the OAuth2 clients that are allowed to access the app
	/// </summary>
	public required string[] ClientSecrets { get; init; }
}
