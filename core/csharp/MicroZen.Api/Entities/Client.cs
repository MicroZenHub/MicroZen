using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroZen.Grpc.Entities;

namespace MicroZen.Data.Entities;

/// <summary>
/// The Application Client entity.
/// </summary>
public class Client : BaseEntity<ClientMessage>
{
  /// <summary>
  /// The Client unique identifier.
  /// </summary>
  public int Id { get; set; }

  /// <summary>
  /// The Client name.
  /// </summary>
  public required string Name { get; set; }

  /// <summary>
  /// The type the client is.
  /// <example>Server=0, Web=1, Desktop=2, Mobile=3</example>
  /// <returns><see cref="ClientType"/></returns>
  /// </summary>
  public required ClientType Type { get; set; }

  /// <summary>
  /// The Client description.
  /// </summary>
  public string? Description { get; set; }

  /// <summary>
  /// The ID of the Organization the client belongs to.
  /// </summary>
  public int OrganizationId { get; set; }

  /// <summary>
  /// The OAuth2 configuration for the client (optional).
  /// </summary>
  public virtual OAuth2ClientCredentials? OAuth2Credentials { get; set; }

  /// <summary>
  /// The Organization the client belongs to.
  /// </summary>
  public virtual Organization? Organization { get; set; }

  /// <summary>
  /// The clients allowed to access this client.
  /// </summary>
  public virtual ICollection<Client> AllowedClients { get; set; } = new List<Client>();

  /// <summary>
  /// The API Keys associated with the client.
  /// </summary>
  public virtual ICollection<ClientAPIKey> APIKeys { get; set; } = new List<ClientAPIKey>();

  /// <inheritdoc />
  public override ClientMessage ToMessage() =>
	  new ClientMessage
	  {
		  Id = Id,
		  Name = Name,
		  Description = Description,
		  Type = Type,
		  OrganizationId = OrganizationId,
		  Oauth2Credentials = OAuth2Credentials?.ToMessage()
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
    builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
    builder.Property(c => c.Type).IsRequired();
    builder.Property(c => c.Description).HasMaxLength(500).IsRequired(false);
    builder.HasOne<Organization>(c => c.Organization).WithMany(o => o.Clients).HasForeignKey(c => c.OrganizationId);
    builder.HasOne(c => c.OAuth2Credentials).WithOne().HasForeignKey<OAuth2ClientCredentials>(c => c.Id);
    builder
	    .HasMany(c => c.AllowedClients)
	    .WithMany()
	    .UsingEntity(entityTypeBuilder =>
		    entityTypeBuilder.ToTable("ClientAllowedClients"));
  }
}
