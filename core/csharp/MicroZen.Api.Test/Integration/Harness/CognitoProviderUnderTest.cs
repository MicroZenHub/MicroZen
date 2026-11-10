extern alias MicroZenCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using MicroZen.Auth.Cognito;
using Credentials = MicroZenCore::MicroZen.Grpc.V1.OAuth2Credentials;
using GrantType = MicroZenCore::MicroZen.Core.Definitions.OAuth2GrantType;

namespace MicroZen.Core.Api.Test.Integration.Harness;

/// <summary>AWS Cognito: issuer is the user pool URL, as built by <see cref="CognitoAuthProvider"/>.</summary>
public sealed class CognitoProviderUnderTest : IAuthProviderUnderTest
{
	public const string Region = "us-east-1";
	public const string UserPoolId = "us-east-1_TESTPOOL";

	public string Name => "cognito";

	public string IssuerFor(Credentials registration) =>
		$"https://cognito-idp.{registration.Region}.amazonaws.com/{registration.UserPoolId}";

	public void AddAuthentication(AuthenticationBuilder builder, IServiceCollection services, GrantType[] grantTypes) =>
		new CognitoAuthProvider().AddAuthentication(builder, services, grantTypes);

	public static Credentials Client(string clientId, string secret = "0123456789abcdef0123456789abcdef") => new()
	{
		ClientId = clientId,
		ClientSecret = secret,
		UserPoolId = UserPoolId,
		Region = Region,
	};
}
