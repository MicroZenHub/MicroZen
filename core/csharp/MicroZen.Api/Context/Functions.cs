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
	public static void MigrateAndSeedDevDataOnStart(this WebApplication app)
	{
		using var scope = app.Services.CreateScope();
		var services = scope.ServiceProvider;

		var microZenContext = services.GetRequiredService<MicroZenContext>();
		if (microZenContext.Database.GetPendingMigrations().Any())
			microZenContext.Database.Migrate();
		if (!app.Environment.IsDevelopment() || !app.Configuration.GetValue<bool>("SeedDevData")) return;
		if (microZenContext.Organizations.Any()) return;
		microZenContext.Add(
			new Organization()
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
						APIKeys = [
							new ClientAPIKey()
							{
								Id = new Guid("0bbc9369-dc40-4952-baa5-6f1e6dde43cc"),
								ApiKey = "mcz_82d5fd9daf7a4bdeba989c50a7415e1f_aceff126",
								ClientId = 1,
								ExpiresOn = DateTime.UtcNow.AddHours(12)
							}
						]
					}
				],
			}
		);
		microZenContext.SaveChanges();
	}
}
