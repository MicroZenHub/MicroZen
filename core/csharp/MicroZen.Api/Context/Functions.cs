using Microsoft.EntityFrameworkCore;
using MicroZen.Data.Entities;
using MicroZen.Grpc.Entities;

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
	public static async Task MigrateAndSeedDevDataOnStart(this WebApplication app)
	{
		using var scope = app.Services.CreateScope();
		var services = scope.ServiceProvider;

		var microZenContext = services.GetRequiredService<MicroZenContext>();
		if ((await microZenContext.Database.GetPendingMigrationsAsync()).Any())
			await microZenContext.Database.MigrateAsync();
		if (app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("SeedDevData"))
		{
			microZenContext.Organizations.Update(new Organization()
			{
				Id = 1,
				Name = "MicroZen",
				Description = "The Zen of Microservices",
				AvatarUrl = "https://avatars.githubusercontent.com/u/165988785?s=400&u=5333fc798b08bfc8cf625c3c8ffdd81f95709f7a&v=4",
				WebsiteUrl = "https://micro-zen.com/",
				Clients = [
					new Client()
					{
						Id = 1,
						Name = "MicroZen API",
						Type = ClientType.Server,
						Description = "The MicroZen API",
					}
				]
			});
			await microZenContext.SaveChangesAsync();
		}
	}
}
