using Microsoft.Extensions.DependencyInjection;
using System.Configuration;

namespace MicroZen.OAuth2.Providers;

/// <summary>
/// Provides MicroZen related services.
/// </summary>
public abstract class MicroZenAuthProvider<TServiceProvider>(TServiceProvider serviceProvider)
{
}
