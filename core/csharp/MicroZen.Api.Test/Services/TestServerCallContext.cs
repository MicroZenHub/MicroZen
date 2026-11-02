using System.Threading;
using Grpc.Core;

namespace MicroZen.Core.Api.Test.Services;

/// <summary>
/// Minimal <see cref="ServerCallContext"/> for exercising gRPC service methods
/// directly in unit tests. The service methods under test do not read anything
/// off the context, so every member is a no-op / default.
/// </summary>
internal sealed class TestServerCallContext : ServerCallContext
{
	private TestServerCallContext() { }

	public static TestServerCallContext Create() => new();

	protected override string MethodCore => string.Empty;
	protected override string HostCore => string.Empty;
	protected override string PeerCore => string.Empty;
	protected override DateTime DeadlineCore => DateTime.MaxValue;
	protected override Metadata RequestHeadersCore => new();
	protected override CancellationToken CancellationTokenCore => CancellationToken.None;
	protected override Metadata ResponseTrailersCore => new();
	protected override Status StatusCore { get; set; }
	protected override WriteOptions? WriteOptionsCore { get; set; }
	protected override AuthContext AuthContextCore => new(string.Empty, new Dictionary<string, List<AuthProperty>>());

	protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options) =>
		throw new NotSupportedException();

	protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
}
