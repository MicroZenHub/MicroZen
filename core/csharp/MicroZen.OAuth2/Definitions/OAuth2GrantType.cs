namespace MicroZen.OAuth2.Definitions;

/// <summary>
/// OAuth2 Grant Types.
///
/// This type declares the full canonical grant-type set (eight members), aligned
/// by name to the gRPC/proto <c>GrantType</c> enum. Only four members are
/// <b>implemented</b> at runtime (Authorization Code, Authorization Code with
/// PKCE, Refresh Token, Client Credentials — all via AWS Cognito). The remaining members are
/// <b>declared</b> so a credential can be modeled, but at wire-up time they still
/// reach the existing <see cref="System.NotImplementedException"/> path in the
/// Cognito provider. Reconciling the enums does not change what works.
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
	/// OAuth2 Device Authorization Grant Type (RFC 8628). Declared, not implemented.
	/// </summary>
	public static readonly OAuth2GrantType DeviceAuthorization = new OAuth2GrantType("device_authorization");

	/// <summary>
	/// OAuth2 Implicit Grant Type. Declared, not implemented (discouraged by OAuth 2.1).
	/// </summary>
	public static readonly OAuth2GrantType Implicit = new OAuth2GrantType("implicit");

	/// <summary>
	/// OAuth2 Hybrid Grant Type. Declared, not implemented.
	/// </summary>
	public static readonly OAuth2GrantType Hybrid = new OAuth2GrantType("hybrid");

	/// <summary>
	/// OAuth2 Resource Owner Password Grant Type. Declared, not implemented (discouraged).
	/// </summary>
	public static readonly OAuth2GrantType ResourceOwnerPassword = new OAuth2GrantType("password");

	/// <summary>
	/// OAuth2 Refresh Token Grant Type. Implemented (AWS Cognito).
	/// </summary>
	public static readonly OAuth2GrantType RefreshToken = new OAuth2GrantType("refresh_token");
}
