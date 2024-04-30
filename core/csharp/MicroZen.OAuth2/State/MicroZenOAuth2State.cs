using System.Reactive.Linq;
using System.Reactive.Subjects;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MicroZen.Grpc.Entities;
using MicroZen.OAuth2.Config;

namespace MicroZen.OAuth2.State;

/// <summary>
/// Represents the state of the MicroZen OAuth2 library
/// </summary>
public class MicroZenOAuth2State : IDisposable
{
	private readonly List<IDisposable> _subscriptions = [];
	private readonly Subject<OAuth2State?> _oauth2Subject = new();
	private readonly ILogger<MicroZenOAuth2State> _logger;

	/// <summary>
	/// Gets the current OAuth2State as an Observable
	/// </summary>
	public IObservable<OAuth2State?> State => _oauth2Subject.AsObservable();

	/// <summary>
	/// Initializes a new instance of the <see cref="MicroZenOAuth2State"/> class.
	/// </summary>
	public MicroZenOAuth2State(IConfiguration configuration, ILogger<MicroZenOAuth2State> logger)
	{
		_logger = logger;
		var microZenConfig = configuration.GetSection("MicroZen").Get<MicroZenAppConfig>();
		if(microZenConfig is null)
			throw new ArgumentNullException(nameof(microZenConfig), "MicroZenAppConfig is null. Please confirm that you have properly entered the required configuration settings in appsettings.json or as an Environment Variable.");
		try
		{
			_subscriptions.Add(
				PingMicroZenServer(microZenConfig.AuthorityUrl, microZenConfig.Interval).Subscribe()
			);
		}
		catch (Exception ex)
		{
			// ReSharper disable once TemplateIsNotCompileTimeConstantProblem
			_logger.LogError(ex.Message, ex.InnerException?.Message, ex.StackTrace);
		}
	}

	private IObservable<OAuth2State?> PingMicroZenServer(string apiUrl, int minutes) =>
		Observable.Timer(TimeSpan.Zero, TimeSpan.FromMinutes(minutes))
			.Skip(1)
			.Select(_ => Observable.FromAsync(() =>
				FetchMicroZenClients(apiUrl)))
			.Switch();

	private async Task<OAuth2State?> FetchMicroZenClients(string apiUrl)
	{
		_logger.LogInformation("Fetching MicroZen Clients");
		var channel = GrpcChannel.ForAddress(apiUrl);
		var client = new Clients.ClientsClient(channel);
		var request = new ClientRequest();
		try
		{
			var response = await client.GetAllowedOAuthClientCredentialsAsync(request);
			if (response is null)
				throw new RpcException(new Status(StatusCode.NotFound, "No clients allowed to authenticate"));
			var currentState = new OAuth2State()
			{
				ClientCredentials = response.Credentials.ToArray()
			};
			_oauth2Subject.OnNext(currentState);
			_logger.LogInformation("MicroZen Clients successfully fetched");
			return currentState;
		}
		catch (RpcException rpcException) when (rpcException is { StatusCode: StatusCode.NotFound or StatusCode.Unavailable })
		{
			switch (rpcException.StatusCode)
			{
				case StatusCode.Unavailable:
					_logger.LogError("MicroZen Server is unavailable");
					break;
				case StatusCode.NotFound:
					_logger.LogWarning("No clients allowed to authenticate");
					break;
				default:
					_logger.LogError("An unknown error occurred while fetching MicroZen Clients");
					break;
			}
			_oauth2Subject.OnNext(null);
			return null;
		}
	}
	/// <inheritdoc />
	public void Dispose()
	{
		GC.SuppressFinalize(this);
		foreach (var disposable in _subscriptions)
		{
			disposable.Dispose();
		}
		_oauth2Subject.Dispose();
	}
}
