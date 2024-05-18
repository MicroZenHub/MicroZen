using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroZen.Data.Security.Encryption.Attributes;
using MicroZen.Grpc.Entities;

namespace MicroZen.Data.Entities;

/// <summary>
/// The OAuth2 Client Configuration entity.
/// </summary>
public class OAuth2ClientCredentials
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

	/// <summary>
	/// Converts the entity to a gRPC message.
	/// </summary>
	/// <returns></returns>
	public MicroZen.Grpc.Entities.OAuthClientCredentials ToMessage()
	{
		return new OAuthClientCredentials
		{
			Id = Id,
			GrantType = OAuth2GrantType ?? GrantType.ClientCredentials,
			ClientId = OAuth2ClientId,
			ClientSecret = OAuth2ClientSecret,
			AllowedScopes = AllowedScopes,
			RequirePkce = RequirePkce ?? false
		};
	}
}

/// <summary>
/// The OAuth2 Client Configuration entity configuration.
/// </summary>
public class OAuth2ClientConfigConfig : IEntityTypeConfiguration<OAuth2ClientCredentials>
{
	/// <inheritdoc />
	public void Configure(EntityTypeBuilder<OAuth2ClientCredentials> builder)
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
