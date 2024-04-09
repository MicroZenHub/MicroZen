using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace MicroZen.Data.Entities;

public class Organization : BaseEntity
{
	public int Id { get; set; }
	public required string Name { get; set; }
	public string? Description { get; set; }
	public string? AvatarUrl { get; set; }
	public string? WebsiteUrl { get; set; }
	public virtual ICollection<OrganizationUser> OrganizationUsers { get; set; } = new List<OrganizationUser>();
	public virtual ICollection<Client> Clients { get; set; } = new List<Client>();
}

public class OrganizationConfig : IEntityTypeConfiguration<Organization>
{
	public void Configure(EntityTypeBuilder<Organization> builder)
	{
		builder.ToTable("Organizations");
		builder.HasKey(o => o.Id);
		builder.Property(o => o.Name).IsRequired();
		builder.Property(o => o.Description).IsRequired(false);
		builder.Property(o => o.AvatarUrl).IsRequired(false);
		builder.Property(o => o.WebsiteUrl).IsRequired(false);
		builder.HasMany<OrganizationUser>(o => o.OrganizationUsers).WithOne(ou => ou.Organization).HasForeignKey(ou => ou.OrganizationId).OnDelete(DeleteBehavior.SetNull);
		builder.HasMany<Client>(o => o.Clients).WithMany().UsingEntity(j => j.ToTable("OrganizationClients"));
	}
}
