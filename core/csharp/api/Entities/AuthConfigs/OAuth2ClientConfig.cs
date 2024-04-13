using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroZen.Data.Security.Encryption.Attributes;

namespace MicroZen.Data.Entities;

public class OAuth2ClientConfig
{
  public int Id { get; set; }
  public GrantType? OAuth2GrantType { get; set; }
  [EncryptColumn]
  public string OAuth2ClientId { get; set; } = string.Empty;
  [EncryptColumn]
  public string? OAuth2ClientSecret { get; set; }
  [EncryptColumn]
  public string? AllowedScopes { get; set; }
  public bool? RequirePkce { get; set; }
}

public enum GrantType
{
  AuthorizationCode = 0,
  AuthorizationCodeWithPkce = 1,
  ClientCredentials = 2,
  DeviceAuthorization = 3,
  Implicit = 4,
  Hybrid = 5,
  ResourceOwnerPassword = 6
}

public class OAuth2ClientConfigConfig : IEntityTypeConfiguration<OAuth2ClientConfig>
{
  public void Configure(EntityTypeBuilder<OAuth2ClientConfig> builder)
  {
	builder.ToTable("OAuth2ClientConfigs");
	builder.HasKey(c => c.Id);
	builder.Property(c => c.OAuth2GrantType).IsRequired();
	builder.Property(c => c.OAuth2ClientId).IsRequired();
	builder.Property(c => c.OAuth2ClientSecret).IsRequired(false);
	builder.Property(c => c.AllowedScopes).IsRequired(false);
	builder.Property(c => c.RequirePkce).IsRequired(false);
  }
}
