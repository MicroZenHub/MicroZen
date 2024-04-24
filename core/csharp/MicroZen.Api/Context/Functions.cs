using Microsoft.EntityFrameworkCore;

namespace MicroZen.Data.Context;

/// <summary>
/// Various helper and extension functions for the MicroZen Context.
/// </summary>
public static class Functions
{
	/// <summary>
	/// Migrate the database on app start.
	/// </summary>
	/// <param name="app">The <see cref="WebApplication"/></param>
	/// <returns></returns>
	public static WebApplication MigrateOnStart(this WebApplication app)
	{
		using var scope = app.Services.CreateScope();
		var services = scope.ServiceProvider;

		var microZenContext = services.GetRequiredService<MicroZenContext>();
		if (microZenContext.Database.GetPendingMigrations().Any()) microZenContext.Database.Migrate();

		return app;
	}
}
