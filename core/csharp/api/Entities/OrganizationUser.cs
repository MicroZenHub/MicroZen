using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroZen.Data.Security.Encryption.Attributes;
using MicroZen.Grpc.Entities;

namespace MicroZen.Data.Entities;

public class OrganizationUser : BaseEntity<OrganizationUserMessage>
{
	public int Id { get; set; }
	[EncryptColumn]
	public Guid UserId { get; set; }
	[EncryptColumn]
	public string? FirstName { get; set; }
	[EncryptColumn]
	public string? LastName { get; set; }
	[EncryptColumn]
	public required string Email { get; set; }
	public required int OrganizationId { get; set; }
	public virtual required Organization Organization { get; set; }

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

public class OrganizationUserConfig : IEntityTypeConfiguration<OrganizationUser>
{
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
