using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroZen.Grpc.Entities;

namespace MicroZen.Data.Entities;

/// <summary>
/// The Organization entity.
/// </summary>
public class Organization : BaseEntity<OrganizationMessage>
{
	/// <summary>
	/// The Organization unique identifier.
	/// </summary>
	public int Id { get; set; }

	/// <summary>
	/// The Organization name.
	/// </summary>
	public required string Name { get; set; }

	/// <summary>
	/// The Organization description.
	/// </summary>
	public string? Description { get; set; }

	/// <summary>
	/// The Organization avatar URL.
	/// </summary>
	public string? AvatarUrl { get; set; }

	/// <summary>
	/// The Organization website URL.
	/// </summary>
	public string? WebsiteUrl { get; set; }

	/// <summary>
	/// The Organization's assigned users.
	/// </summary>
	public virtual ICollection<OrganizationUser> OrganizationUsers { get; set; } = new List<OrganizationUser>();

	/// <summary>
	/// The Organization's clients.
	/// </summary>
	public virtual ICollection<Client> Clients { get; set; } = new List<Client>();

	/// <inheritdoc />
	public override OrganizationMessage ToMessage() =>
		new OrganizationMessage
		{
			Id = Id,
			Name = Name,
			Description = Description ?? string.Empty,
			AvatarUrl = AvatarUrl ?? string.Empty,
			WebsiteUrl = WebsiteUrl ?? string.Empty
		};
}

/// <inheritdoc />
public class OrganizationConfig : IEntityTypeConfiguration<Organization>
{
	/// <inheritdoc />
	public void Configure(EntityTypeBuilder<Organization> builder)
	{
		builder.ToTable("Organizations");
		builder.HasKey(o => o.Id);
		builder.Property(o => o.Name).HasMaxLength(100).IsRequired();
		builder.Property(o => o.Description).HasMaxLength(300).IsRequired(false);
		builder.Property(o => o.AvatarUrl).HasMaxLength(250).IsRequired(false);
		builder.Property(o => o.WebsiteUrl).HasMaxLength(250).IsRequired(false);
		builder.HasMany<OrganizationUser>(o => o.OrganizationUsers).WithOne(ou => ou.Organization).HasForeignKey(ou => ou.OrganizationId).OnDelete(DeleteBehavior.SetNull);
		builder.HasMany<Client>(o => o.Clients).WithOne(c => c.Organization).HasForeignKey(c => c.OrganizationId).OnDelete(DeleteBehavior.SetNull);
	}
}
