using System.Reactive.Linq;
using System.Reactive.Subjects;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
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
	private BehaviorSubject<OAuth2State?> _oauth2Subject = new BehaviorSubject<OAuth2State?>(null);
	private readonly ILogger<MicroZenOAuth2State> _logger;
	private readonly Clients.ClientsClient _client;

	/// <summary>
	/// Gets the current OAuth2State as an Observable
	/// </summary>
	public IObservable<OAuth2State?> State => _oauth2Subject.AsObservable();

	/// <summary>
	/// Initializes a new instance of the <see cref="MicroZenOAuth2State"/> class.
	/// </summary>
	public MicroZenOAuth2State(IConfiguration configuration, ILogger<MicroZenOAuth2State> logger, Clients.ClientsClient client)
	{
		_client = client;
		_logger = logger;
		var microZenConfig = configuration.GetSection("MicroZen").Get<MicroZenAppConfig>();
		if(microZenConfig is null)
			throw new ArgumentNullException(nameof(microZenConfig), "MicroZenAppConfig is null. Please confirm that you have properly entered the required configuration settings in appsettings.json or as an Environment Variable.");
		try
		{
			_subscriptions.Add(
				PingMicroZenServer(microZenConfig).Subscribe()
			);
		}
		catch (Exception ex)
		{
			// ReSharper disable once TemplateIsNotCompileTimeConstantProblem
			_logger.LogError(ex.Message, ex.InnerException?.Message, ex.StackTrace);
		}
	}

	private IObservable<OAuth2State?> PingMicroZenServer(MicroZenAppConfig appConfig) =>
		Observable.Timer(TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(appConfig.Interval))
			.Select(_ =>
				Observable.FromAsync(_ =>
					FetchMicroZenClientId(appConfig.APIKey)
				)
				.Catch<Int32Value?, RpcException>(_ => Observable.Empty<Int32Value?>())
				.SkipWhile(clientId => clientId is null)
			)
			.Switch()
			.Select(clientId =>
				Observable.FromAsync(() =>
					FetchMicroZenClients(clientId!.Value)))
			.Switch();

	private async Task<Int32Value?> FetchMicroZenClientId(string apiKey)
	{
		_logger.LogInformation("Fetching MicroZen Client ID");
		return await _client.GetClientIdFromApiKeyAsync(new StringValue() { Value = apiKey });
	}

	private async Task<OAuth2State?> FetchMicroZenClients(int clientId)
	{
		_logger.LogInformation("Fetching MicroZen Clients");
		var request = new ClientRequest()
		{
			Id = clientId
		};
		try
		{
			var response = await _client.GetAllowedOAuthClientCredentialsAsync(request);
			if (response is null)
				throw new RpcException(new Status(StatusCode.NotFound, "No clients allowed to authenticate"));
			var currentState = new OAuth2State();
			currentState.ClientCredentials = response.Credentials.Select(c => c).ToList();
			_oauth2Subject.OnNext(currentState);
			_logger.LogInformation($"{currentState.ClientCredentials.Count()} MicroZen Clients successfully fetched");
			return currentState;
		}
		catch (RpcException rpcException) when (rpcException is
			                                        { StatusCode: StatusCode.NotFound or StatusCode.Unavailable })
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
		catch (Exception ex)
		{
			_logger.LogError(ex.Message, ex.InnerException?.Message, ex.StackTrace);
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
