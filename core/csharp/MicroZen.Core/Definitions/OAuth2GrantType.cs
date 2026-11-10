namespace MicroZen.Core.Definitions;

/// <summary>
/// OAuth2 Grant Types.
///
/// This type declares the canonical five-member grant-type set, aligned by name
/// to the gRPC/proto <c>GrantType</c> enum. All five members are implemented via
/// AWS Cognito: Authorization Code, Authorization Code with PKCE, Client
/// Credentials, Device Authorization, and Refresh Token.
/// </summary>
/// <param name="value">The OAuth2 grant-type string value.</param>
public class OAuth2GrantType(string value)
{
	/// <inheritdoc />
	public override string ToString() => value;

	/// <summary>
	/// OAuth2 Authorization Code Grant Type. Implemented (AWS Cognito).
	/// </summary>
	public static readonly OAuth2GrantType AuthorizationCode = new("authorization_code");

	/// <summary>
	/// OAuth2 Authorization Code Grant Type with PKCE. Implemented (AWS Cognito).
	/// </summary>
	public static readonly OAuth2GrantType AuthorizationCodeWithPKCE = new OAuth2GrantType("authorization_code_pkce");

	/// <summary>
	/// OAuth2 Client Credentials Grant Type. Implemented (AWS Cognito).
	/// </summary>
	public static readonly OAuth2GrantType ClientCredentials = new OAuth2GrantType("client_credentials");

	/// <summary>
	/// OAuth2 Device Authorization Grant Type (RFC 8628). Implemented (AWS Cognito).
	/// </summary>
	public static readonly OAuth2GrantType DeviceAuthorization = new OAuth2GrantType("device_authorization");

	/// <summary>
	/// OAuth2 Refresh Token Grant Type. Implemented (AWS Cognito).
	/// </summary>
	public static readonly OAuth2GrantType RefreshToken = new OAuth2GrantType("refresh_token");
}
