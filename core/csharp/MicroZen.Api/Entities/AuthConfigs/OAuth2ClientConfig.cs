using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroZen.Data.Security.Encryption.Attributes;

namespace MicroZen.Data.Entities;

/// <summary>
/// The OAuth2 Client Configuration entity.
/// </summary>
public class OAuth2ClientConfig
{
	/// <summary>
	///	The OAuth2 Client Configuration unique identifier.
	/// </summary>
	public int Id { get; set; }

	/// <summary>
	/// The OAuth2 Grant Type.
	/// </summary>
	public GrantType? OAuth2GrantType { get; set; }

	/// <summary>
	/// The OAuth2 Client ID.
	/// </summary>
	[EncryptColumn]
	public string OAuth2ClientId { get; set; } = string.Empty;

	/// <summary>
	/// The OAuth2 Client Secret (optional).
	/// </summary>
	[EncryptColumn]
	public string? OAuth2ClientSecret { get; set; }

	/// <summary>
	/// The OAuth2 Allowed Scopes (optional).
	/// </summary>
	[EncryptColumn]
	public string? AllowedScopes { get; set; }

	/// <summary>
	/// Whether to require PKCE for the OAuth2 Client.
	/// <value>false</value>
	/// </summary>
	public bool? RequirePkce { get; set; }
}

/// <summary>
/// The OAuth2 Grant Types.
/// </summary>
public enum GrantType
{
  /// <summary>
  /// Authorization Code Grant
  /// </summary>
  AuthorizationCode = 0,

  /// <summary>
  /// Authorization Code Grant with PKCE
  /// </summary>
  AuthorizationCodeWithPkce = 1,

  /// <summary>
  /// Client Credentials Grant
  /// </summary>
  ClientCredentials = 2,

  /// <summary>
  /// Device Authorization Grant
  /// </summary>
  DeviceAuthorization = 3,

  /// <summary>
  /// Implicit Grant
  /// </summary>
  Implicit = 4,

  /// <summary>
  /// Hybrid Grant
  /// </summary>
  Hybrid = 5,

  /// <summary>
  /// Resource Owner Password Grant
  /// </summary>
  ResourceOwnerPassword = 6
}

/// <summary>
/// The OAuth2 Client Configuration entity configuration.
/// </summary>
public class OAuth2ClientConfigConfig : IEntityTypeConfiguration<OAuth2ClientConfig>
{
	/// <inheritdoc />
	public void Configure(EntityTypeBuilder<OAuth2ClientConfig> builder)
  {
	builder.ToTable("OAuth2ClientConfigs");
	builder.HasKey(c => c.Id);
	builder.Property(c => c.OAuth2GrantType).IsRequired();
	builder.Property(c => c.OAuth2ClientId).HasMaxLength(300).IsRequired();
	builder.Property(c => c.OAuth2ClientSecret).HasMaxLength(1000).IsRequired(false);
	builder.Property(c => c.AllowedScopes).HasMaxLength(300).IsRequired(false);
	builder.Property(c => c.RequirePkce).IsRequired(false);
  }
}
