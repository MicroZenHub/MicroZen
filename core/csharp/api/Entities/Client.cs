using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroZen.Grpc.Entities;

namespace MicroZen.Data.Entities;

public class Client : BaseEntity<ClientMessage>
{
  public int Id { get; set; }
  public required string Name { get; set; }
  public required ClientType Type { get; set; }
  public string? Description { get; set; }
  public virtual OAuth2ClientConfig? OAuth2Config { get; set; }
  public virtual ICollection<Client> AllowedClients { get; set; } = new List<Client>();

  public override ClientMessage ToMessage() =>
	  new ClientMessage
	  {
		  Id = Id,
		  Name = Name,
		  Description = Description,
		  Type = Type
	  };
}

/// <inheritdoc />
public class ClientConfig : IEntityTypeConfiguration<Client>
{
	/// <inheritdoc />
	public void Configure(EntityTypeBuilder<Client> builder)
  {
    builder.ToTable("Clients");
    builder.HasKey(c => c.Id);
    builder.Property(c => c.Name).IsRequired();
    builder.Property(c => c.Type).IsRequired();
    builder.Property(c => c.Description).IsRequired(false);
    builder.HasOne(c => c.OAuth2Config).WithOne().HasForeignKey<OAuth2ClientConfig>(c => c.Id);
    builder.HasMany<Client>(c => c.AllowedClients).WithMany().UsingEntity(j => j.ToTable("ClientAllowedClients"));
  }
}
