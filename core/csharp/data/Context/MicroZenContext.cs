using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MicroZen.Data.Security.Encryption.Extensions;
using MicroZen.Data.Security.Encryption.Utils;

namespace MicroZen.Data.Context;

public partial class MicroZenContext : DbContext
{
    public MicroZenContext()
    {
    }

    public MicroZenContext(DbContextOptions<MicroZenContext> options)
        : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
      if (optionsBuilder.IsConfigured)
        return;

      var config = Config();
      var connectionString = config.GetConnectionString("MicroZenContext");
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
