using Microsoft.AspNetCore.Builder;

namespace MicroZen.Core;

/// <summary>
/// Class for utilizing MicroZen auth services
/// </summary>
public static class Utilization
{
	/// <summary>
	/// Adds JWT Bearer authentication and authorization middleware
	/// </summary>
	/// <remarks>This method must be called <b><em>after</em></b> app.UseCors()</remarks>
	/// <param name="app"><see cref="IApplicationBuilder"/></param>
	public static void UseMicroZenAuth(this IApplicationBuilder app)
	{
		app.UseAuthentication();
		app.UseAuthorization();
	}
}
