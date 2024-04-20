namespace MicroZen.OAuth2.Definitions;

/// <summary>
/// OAuth2 Grant Types
/// </summary>
/// <param name="value"></param>
public class OAuth2GrantType(string value)
{
	/// <inheritdoc />
	public override string ToString() => value;

	/// <summary>
	/// OAuth2 Authorization Code Grant Type
	/// </summary>
	public static readonly OAuth2GrantType AuthorizationCode = new OAuth2GrantType("authorization_code");

	/// <summary>
	/// OAuth2 Authorization Code Grant Type with PKCE
	/// </summary>
	public static readonly OAuth2GrantType AuthorizationCodeWithPKCE = new OAuth2GrantType("authorization_code_pkce");

	/// <summary>
	/// OAuth2 Client Credentials Grant Type
	/// </summary>
	public static readonly OAuth2GrantType ClientCredentials = new OAuth2GrantType("client_credentials");

	/// <summary>
	/// OAuth2 Device Code Grant Type
	/// </summary>
	public static readonly OAuth2GrantType DeviceCode = new OAuth2GrantType("device_code");

	/// <summary>
	/// OAuth2 Refresh Token Grant Type
	/// </summary>
	public static readonly OAuth2GrantType RefreshToken = new OAuth2GrantType("refresh_token");
}
