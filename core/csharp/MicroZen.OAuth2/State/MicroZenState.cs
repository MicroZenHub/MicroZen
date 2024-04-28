using System.Reactive.Linq;
using System.Reactive.Subjects;
using Grpc.Net.Client;
using Microsoft.Extensions.Configuration;
using MicroZen.OAuth2.Config;

namespace MicroZen.OAuth2.State;

/// <summary>
/// Represents the state of the MicroZen OAuth2 library
/// </summary>
public class MicroZenState<TState> : IDisposable
{
	private readonly List<IDisposable> _subscriptions = [];
	private readonly Subject<TState?> _oauth2Subject = new();

	/// <summary>
	/// Gets the current OAuth2State as an Observable
	/// </summary>
	public IObservable<TState?> State => _oauth2Subject.AsObservable();

	/// <summary>
	/// Initializes a new instance of the <see cref="MicroZenState{TState}"/> class.
	/// </summary>
	public MicroZenState(IConfiguration configuration)
	{
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
			// Log the exception
			// Log.Error(ex, "Error fetching MicroZen Clients");
		}
	}

	private async Task<OAuth2State?> FetchMicroZenClients(string apiUrl)
	{
		var channel = GrpcChannel.ForAddress(apiUrl);
		Console.WriteLine("Pretend Hitting an Http Endpoint or Grpc Method");
		return null;
		// Fetch the MicroZen Clients
		// var clients = await _clientService.GetClients();
		// var state = new OAuth2State
		// {
		// 	ClientIds = clients.Select(c => c.ClientId),
		// 	ClientSecrets = clients.Select(c => c.ClientSecret)
		// };
		// _oauth2Subject.OnNext(state);
	}

	private IObservable<OAuth2State?> PingMicroZenServer(string apiUrl, int minutes) =>
		Observable.Timer(TimeSpan.Zero, TimeSpan.FromMinutes(minutes))
			.Select(_ => Observable.FromAsync(() =>
				FetchMicroZenClients(apiUrl)))
			.Switch();

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
