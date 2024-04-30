using MicroZen.Grpc.Entities;

namespace MicroZen.OAuth2.State;

/// <summary>
/// Represents the state of the MicroZen OAuth2 library
/// </summary>
public struct OAuth2State()
{
	/// <summary>
	/// The credentials of the OAuth2 clients that are allowed to access the app
	/// </summary>
	public IEnumerable<OAuth2Credentials> ClientCredentials { get; set; } = [];
}
