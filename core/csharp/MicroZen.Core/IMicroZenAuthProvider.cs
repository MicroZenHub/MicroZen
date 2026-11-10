using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using MicroZen.Core.Definitions;

namespace MicroZen.Core;

/// <summary>
/// Contract a MicroZen.Auth.&lt;Provider&gt; package implements to plug into MicroZen.Core.
/// </summary>
/// <remarks>
/// Core owns everything provider-independent (authority gRPC client, polled client state,
/// JWT claim handling, authorization policies, middleware). A provider only decides how
/// the allowed client credentials and requested grant types become JWT bearer validation.
/// </remarks>
public interface IMicroZenAuthProvider
{
	/// <summary>
	/// Short provider name, e.g. "cognito".
	/// </summary>
	string Name { get; }

	/// <summary>
	/// Adds the provider's JWT bearer validation for the requested grant types.
	/// </summary>
	/// <param name="builder">The authentication builder created by Core.</param>
	/// <param name="services">The app's services; <see cref="State.MicroZenOAuth2State"/> is already registered.</param>
	/// <param name="grantTypes">The grant types the app wants to accept.</param>
	/// <exception cref="NotImplementedException">Thrown when the provider does not support the grant types.</exception>
	void AddAuthentication(AuthenticationBuilder builder, IServiceCollection services, OAuth2GrantType[] grantTypes);
}
