using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MicroZen.Data.Entities;
using MicroZen.Data.Security.Encryption.Extensions;
using MicroZen.Data.Security.Encryption.Utils;

namespace MicroZen.Data.Context;

public class MicroZenContext : DbContext
{
	public const string MicroZenContextName = "MicroZenContext";
    public MicroZenContext() { }

    public MicroZenContext(DbContextOptions<MicroZenContext> options) : base(options) { }

    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationUser> OrganizationUsers => Set<OrganizationUser>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
      if (optionsBuilder.IsConfigured)
        return;

      var config = Config();
      var connectionString = config.GetConnectionString(MicroZenContextName);
      optionsBuilder.UseNpgsql(connectionString);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
	    modelBuilder.UseEncryption(new EncryptionProvider(
		    Config()["EncryptionKey"] ??
		    throw new ArgumentNullException("EncryptionKey", "Please initialize your encryption key.")));
	    modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }

    private static IConfigurationRoot Config() =>
	    new ConfigurationBuilder()
		    .SetBasePath(Directory.GetCurrentDirectory())
		    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
		    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
		    .Build();
}
