using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroZen.Api.Entities.Shared;
using MicroZen.Grpc.Entities;

namespace MicroZen.Api.Entities;

/// <summary>
/// The Organization entity.
/// </summary>
[EntityTypeConfiguration(typeof(OrganizationConfig))]
public class Organization : BaseEntity<OrganizationMessage>
{
	/// <inheritdoc />
	public Organization() { }

	/// <summary>
	/// Constructor for converting an OrganizationMessage to an Organization entity.
	/// </summary>
	public Organization(OrganizationMessage message)
	{
		Name = message.Name;
		Description = message.Description.Length > 0 ? message.Description : null;
		AvatarUrl = message.AvatarUrl.Length > 0 ? message.AvatarUrl : null;
		WebsiteUrl = message.WebsiteUrl.Length > 0 ? message.WebsiteUrl : null;
	}
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
		builder.HasMany<Client>(o => o.Clients).WithOne(c => c.Organization).HasForeignKey(c => c.OrganizationId).OnDelete(DeleteBehavior.SetNull);
		builder.HasMany(ou => ou.OrganizationUsers)
			.WithMany(o => o.Organizations)
			.UsingEntity(
				"OrganizationUserOrganization",
				o => o.HasOne(typeof(OrganizationUser)).WithMany().HasForeignKey("OrganizationUserId").HasPrincipalKey(nameof(OrganizationUser.Id)),
				ou => ou.HasOne(typeof(Organization)).WithMany().HasForeignKey("OrganizationId").HasPrincipalKey(nameof(Organization.Id)),
				ouo => ouo.HasKey("OrganizationId", "OrganizationUserId")
			);
	}
}
