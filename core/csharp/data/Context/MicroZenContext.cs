using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

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

      var config = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
        .Build();
      var connectionString = config.GetConnectionString("MicroZenContext");
      optionsBuilder.UseNpgsql(connectionString);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
	    modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
}
