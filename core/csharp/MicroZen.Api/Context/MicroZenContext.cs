using System.Reflection;
using Microsoft.EntityFrameworkCore;
using MicroZen.Data.Security.Encryption.Utils;
using MicroZen.Data.Entities;
using MicroZen.Data.Security.Encryption.Extensions;
using static MicroZen.Data.Context.Variables;

namespace MicroZen.Data.Context;

/// <inheritdoc />
public class MicroZenContext : DbContext
{
	/// <inheritdoc />
	public MicroZenContext() { }

    /// <inheritdoc />
    public MicroZenContext(DbContextOptions<MicroZenContext> options) : base(options) { }

    /// <summary>
    /// The Clients DbSet.
    /// </summary>
    public DbSet<Client> Clients => Set<Client>();

    /// <summary>
    /// The ClientAPIKeys DbSet.
    /// </summary>
    public DbSet<ClientAPIKey> ClientAPIKeys => Set<ClientAPIKey>();

    /// <summary>
    /// The Organizations DbSet.
    /// </summary>
    public DbSet<Organization> Organizations => Set<Organization>();

    /// <summary>
    /// The OrganizationUsers DbSet.
    /// </summary>
    public DbSet<OrganizationUser> OrganizationUsers => Set<OrganizationUser>();

    /// <inheritdoc />
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
      if (optionsBuilder.IsConfigured)
        return;

      var config = Config();
      var connectionString = config.GetConnectionString(MicroZenContextName);
      optionsBuilder.UseNpgsql(connectionString);
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
	    modelBuilder.UseEncryption(new EncryptionProvider(
		    Config()[EncryptionProvider.EncryptionKeyVarName] ??
		    throw new ArgumentNullException(EncryptionProvider.EncryptionKeyVarName, "Please initialize your encryption key.")));
	    modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }

    private static IConfigurationRoot Config() =>
	    new ConfigurationBuilder()
		    .SetBasePath(Directory.GetCurrentDirectory())
		    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
		    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
		    .AddEnvironmentVariables()
		    .Build();
}
