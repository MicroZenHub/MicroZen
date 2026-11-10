extern alias MicroZenCore;
using Grpc.Net.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using static Xunit.Assert;
using PackageGrantType = MicroZenCore::MicroZen.Core.Definitions.OAuth2GrantType;

using OAuth2State = MicroZenCore::MicroZen.Core.State.MicroZenOAuth2State;
using OAuth2Client = MicroZenCore::MicroZen.Grpc.V1.ClientsGrpcService.ClientsGrpcServiceClient;

namespace MicroZen.Core.Api.Test.Definitions;

/// <summary>
/// Tests for issue #63: the Cognito provider wires the Device Authorization grant.
/// </summary>
public class DeviceAuthorizationFlowTests
{
	[Fact]
	public void AddAWSCognitoMicroZenOAuth2_DeviceAuthorization_WiresJwtBearerWithoutThrowing()
	{
		// GIVEN the services MicroZenOAuth2State needs (no network call is made at wire-up).
		var config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["MicroZen:AuthorityUrl"] = "http://localhost",
				["MicroZen:APIKey"] = "test-key",
			})
			.Build();
		var services = new ServiceCollection();
		services.AddSingleton<IConfiguration>(config);
		services.AddSingleton<Microsoft.Extensions.Logging.ILogger<OAuth2State>>(NullLogger<OAuth2State>.Instance);
		services.AddSingleton(new OAuth2Client(GrpcChannel.ForAddress("http://localhost")));
		services.AddSingleton<OAuth2State>();

		// WHEN the Cognito provider is configured with DeviceAuthorization only.
		var ex = Record.Exception(() => CognitoWiring.Wire(services, PackageGrantType.DeviceAuthorization));

		// THEN it no longer reaches the NotImplementedException path.
		Null(ex);
	}
}
