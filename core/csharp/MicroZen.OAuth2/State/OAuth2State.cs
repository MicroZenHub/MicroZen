namespace MicroZen.OAuth2.State;

/// <summary>
/// Represents the state of the MicroZen OAuth2 library
/// </summary>
public struct OAuth2State
{
	/// <summary>
	/// The client IDs belonging to the Cognito User Pool clients that are allowed to access the app
	/// </summary>
	public IEnumerable<string> ClientIds { get; set; }

	/// <summary>
	/// The client secrets of the Cognito User Pool clients (optional - depending on flow)
	/// </summary>
	public IEnumerable<string>? ClientSecrets { get; set; }

	/// <summary>
	/// The AWS region where the Cognito User Pool is located
	/// </summary>
	public string Region { get; set; }

	/// <summary>
	/// The User Pool ID of the Cognito User Pool
	///	</summary>
	public string UserPoolId { get; set; }
}
