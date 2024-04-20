using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroZen.Data.Security.Encryption.Attributes;
using MicroZen.Grpc.Entities;

namespace MicroZen.Data.Entities;

/// <summary>
/// Represents a user that belongs to an organization.
/// </summary>
public class OrganizationUser : BaseEntity<OrganizationUserMessage>
{
	/// <summary>
	/// The unique identifier for the organization user.
	/// </summary>
	public int Id { get; set; }

	/// <summary>
	/// The unique Guid identifier for the user (aligns with the OAuth provider ID).
	/// </summary>
	[EncryptColumn]
	public Guid UserId { get; set; }

	/// <summary>
	/// The first name of the user.
	/// </summary>
	[EncryptColumn]
	public string? FirstName { get; set; }

	/// <summary>
	/// The last name of the user.
	/// </summary>
	[EncryptColumn]
	public string? LastName { get; set; }

	/// <summary>
	///	The email address of the user.
	/// </summary>
	[EncryptColumn]
	public required string Email { get; set; }

	/// <summary>
	/// The unique identifier for the organization this user belongs to.
	/// <seealso cref="Organization.Id">Organization.Id</seealso>
	/// </summary>
	public required int OrganizationId { get; set; }

	/// <summary>
	/// The organization this user belongs to.
	/// <seealso cref="Organization"/>
	/// </summary>
	public virtual required Organization Organization { get; set; }

	/// <inheritdoc />
	public override OrganizationUserMessage ToMessage() =>
		new OrganizationUserMessage
		{
			Id = Id,
			UserId = UserId.ToString(),
			FirstName = FirstName,
			LastName = LastName,
			Email = Email,
			OrganizationId = OrganizationId
		};
}

/// <inheritdoc />
public class OrganizationUserConfig : IEntityTypeConfiguration<OrganizationUser>
{
	/// <inheritdoc />
	public void Configure(EntityTypeBuilder<OrganizationUser> builder)
	{
		builder.ToTable("OrganizationUsers");
		builder.HasKey(ou => ou.Id);
		builder.Property(ou => ou.UserId).IsRequired();
		builder.Property(ou => ou.FirstName).IsRequired(false);
		builder.Property(ou => ou.LastName).IsRequired(false);
		builder.Property(ou => ou.Email).IsRequired();
		builder.HasOne<Organization>(ou => ou.Organization).WithMany(o => o.OrganizationUsers).HasForeignKey(ou => ou.OrganizationId);
	}
}
