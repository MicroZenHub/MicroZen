using Grpc.Core;
using MicroZen.Core.Api;

namespace MicroZen.Core.Api.Services;

/// <inheritdoc />
public class GreeterService(ILogger<GreeterService> logger) : Greeter.GreeterBase
{
  /// <inheritdoc />
  public override Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
  {
    logger.LogInformation("Saying hello to {Name}", request.Name);
    return Task.FromResult(new HelloReply
    {
        Message = "Hello " + request.Name
    });
  }
}
