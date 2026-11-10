using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace MicroZen.Auth.Cognito.Flows;

/// <summary>
/// Handles all code related to the OAuth2 DeviceAuthorization (RFC 8628) grant type
/// </summary>
/// <remarks>
/// The device grant is a client-side token acquisition flow. The resource server only
/// validates the resulting Cognito-issued JWT (issuer, lifetime, audience client ids),
/// which is identical to the public-client Authorization Code validation, so it is reused.
/// </remarks>
public static class DeviceAuthorization
{
	/// <summary>
	/// Adds the JWT Bearer token authentication for the OAuth2 "device_authorization" grant type
	/// </summary>
	/// <param name="builder"><see cref="AuthenticationBuilder"/></param>
	/// <param name="services"><see cref="IServiceCollection" /></param>
	public static void AddDeviceAuthorizationJwtBearer(this AuthenticationBuilder builder, IServiceCollection services)
		=> builder.AddAuthorizationCodeJwtBearer(services);
}
