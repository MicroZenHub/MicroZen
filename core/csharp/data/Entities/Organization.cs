namespace MicroZen.Data.Entities;

public class Organization : BaseEntity
{
	public int Id { get; set; }
	public required string Name { get; set; }
	public string? Description { get; set; }
	public string? AvatarUrl { get; set; }
	public string? WebsiteUrl { get; set; }
	public ICollection<string>? ContactEmails { get; set; }
}


