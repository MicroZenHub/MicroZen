using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroZen.Api.Entities.AuthCredentials;
using MicroZen.Api.Entities.Shared;
using MicroZen.Grpc.V1;

namespace MicroZen.Api.Entities;

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
	/// The URL of the associated Git repository for the client.
	/// </summary>
	public string? GitRepoUrl { get; set; }

	/// <summary>
	/// The URL of the icon image associated with the Git repository of the client.
	/// </summary>
	public string? GitRepoIconImageUrl { get; set; }

	/// <summary>
	/// The ID of the Organization the client belongs to.
	/// </summary>
	public int OrganizationId { get; set; }

	/// <summary>
	/// The OAuth2 configuration for the client (optional).
	/// </summary>
	public virtual ICollection<OAuth2ClientCredentials>? OAuth2Credentials { get; set; }

	/// <summary>
	/// The Organization the client belongs to.
	/// </summary>
	public virtual Organization? Organization { get; set; }

	/// <summary>
	/// The client OAuth2 Credentials allowed to access this client.
	/// </summary>
	public virtual ICollection<OAuth2ClientCredentials> AllowedClientOAuth2Credentials { get; set; } = new List<OAuth2ClientCredentials>();

	/// <summary>
	/// The API Keys associated with the client.
	/// </summary>
	public virtual ICollection<ClientAPIKey> APIKeys { get; set; } = new List<ClientAPIKey>();

	/// <summary>
	/// Reconciles an incoming set of OAuth2 credentials against the client's
	/// current credentials. Incoming credentials whose <c>OAuth2ClientId</c> is
	/// already present are updated in place; genuinely new ones are added. This
	/// is domain behaviour of the client (how it absorbs a new credential set),
	/// so it lives on the entity rather than in an API-layer helper, per the
	/// Grpc/HTTP API -> BLL -> Repo layering standard.
	/// </summary>
	/// <param name="incoming">
	/// The OAuth2 credentials carried on an update request. Each entry's
	/// <see cref="OAuthClientCredentials.ClientId"/> (the OAuth2 client identity)
	/// is matched against the existing <c>OAuth2ClientId</c> values.
	/// </param>
	public void ApplyOAuth2Credentials(IEnumerable<OAuthClientCredentials> incoming)
	{
		OAuth2Credentials ??= new List<OAuth2ClientCredentials>();

		foreach (var incomingCred in incoming)
		{
			// Match on OAuth2ClientId (the OAuth2 client identity that the request
			// carries in ClientId), NOT the integer ClientId FK to the parent.
			var existing = OAuth2Credentials
				.FirstOrDefault(c => c.OAuth2ClientId == incomingCred.ClientId);

			if (existing is not null)
			{
				// Already present -> update in place.
				existing.OAuth2ClientSecret = incomingCred.ClientSecret;
				existing.OAuth2GrantType = incomingCred.GrantType;
				existing.AllowedScopes = incomingCred.AllowedScopes;
				existing.RequirePkce = incomingCred.RequirePkce;
			}
			else
			{
				// Genuinely new -> add.
				OAuth2Credentials.Add(new OAuth2ClientCredentials
				{
					OAuth2ClientId = incomingCred.ClientId,
					OAuth2ClientSecret = incomingCred.ClientSecret,
					OAuth2GrantType = incomingCred.GrantType,
					AllowedScopes = incomingCred.AllowedScopes,
					RequirePkce = incomingCred.RequirePkce,
				});
			}
		}
	}

	/// <inheritdoc />
	public override ClientMessage ToMessage()
	{
		var message = new ClientMessage
		{
			Id = Id,
			Name = Name,
			Description = Description,
			Type = Type,
			OrganizationId = OrganizationId,
			GitRepoIconImageUrl = GitRepoIconImageUrl,
			GitRepoUrl = GitRepoUrl
		};
		message.OauthCredentials.AddRange(OAuth2Credentials?.Select(c => new OAuthClientCredentials
		{
			ClientId = c.ClientId.ToString(),
			ClientSecret = c.OAuth2ClientSecret,
			Environment = c.Environment,
			AllowedScopes = c.AllowedScopes,
			Provider = c.Provider
		}));
		return message;
	}
}

/// <inheritdoc />
[EntityTypeConfiguration(typeof(ClientConfig))]
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
		builder.HasMany<OAuth2ClientCredentials>(c => c.OAuth2Credentials).WithOne(o => o.Client).HasForeignKey(c => c.ClientId);
		builder
			.HasMany(c => c.AllowedClientOAuth2Credentials)
			.WithMany()
			.UsingEntity(entityTypeBuilder =>
				entityTypeBuilder.ToTable("ClientAllowedClients"));
	}
}
