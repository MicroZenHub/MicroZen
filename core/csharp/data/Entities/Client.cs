using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MicroZen.Data.Entities;

public class Client : BaseEntity
{
  public int Id { get; set; }
  public required string Name { get; set; }
  // public required ClientType Type { get; set; }
  public string? Description { get; set; }
  public virtual OAuth2ClientConfig? OAuth2Config { get; set; }
  public virtual ICollection<Client> AllowedClients { get; set; } = new List<Client>();
}

public class ClientConfig : IEntityTypeConfiguration<Client>
{
  public void Configure(EntityTypeBuilder<Client> builder)
  {
    builder.ToTable("Clients");
    builder.HasKey(c => c.Id);
    builder.Property(c => c.Name).IsRequired();
    // builder.Property(c => c.Type).IsRequired();
    builder.Property(c => c.Description).IsRequired(false);
    builder.HasOne(c => c.OAuth2Config).WithOne().HasForeignKey<OAuth2ClientConfig>(c => c.Id);
    builder.HasMany<Client>(c => c.AllowedClients).WithMany().UsingEntity(j => j.ToTable("ClientAllowedClients"));
  }
}
