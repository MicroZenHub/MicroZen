using System.ComponentModel.DataAnnotations;
using Google.Protobuf.WellKnownTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MicroZen.Grpc.Entities;
using static MicroZen.Api.Security.APIKeys.APIKeyGenerator;

namespace MicroZen.Data.Entities;

/// <summary>
/// The ClientAPIKey entity
/// </summary>
public class ClientAPIKey : BaseEntity<ClientApiKeyMessage>
{
	/// <summary>
	/// The ID of the ClientAPIKey
	/// </summary>
	public Guid Id { get; set; } = Guid.Empty;

	/// <summary>
	/// The API Key
	/// </summary>
	[MaxLength(128)]
	public string ApiKey { get; set; } = GenerateAPIKey();

	/// <summary>
	/// The date the API Key will expire
	/// </summary>
	public DateTime? ExpiresOn { get; set; }

	/// <summary>
	/// The ID of the Client associated with the API Key
	/// </summary>
	public required int ClientId { get; set; }

	/// <summary>
	/// The Client associated with the API Key
	/// </summary>
	public virtual Client? Client { get; set; }

	/// <inheritdoc />
	public override ClientApiKeyMessage ToMessage()
	{
		return new ClientApiKeyMessage
		{
			Id = Id.ToString(),
			ApiKey = ApiKey,
			ExpiresOn = ExpiresOn.HasValue ? ExpiresOn.Value.ToTimestamp() : new Google.Protobuf.WellKnownTypes.Timestamp() { Seconds = 1, Nanos = 1 } ,
			ClientId = ClientId
		};
	}
}

/// <inheritdoc />
public class ClientAPIKeyConfig : IEntityTypeConfiguration<ClientAPIKey>
{
	/// <inheritdoc />
	public void Configure(EntityTypeBuilder<ClientAPIKey> builder)
	{
		builder.ToTable("ClientAPIKeys");
		builder.HasKey(c => c.Id);
		builder.HasIndex(c => c.ApiKey).IsUnique();
		builder.Property(c => c.ApiKey).IsRequired();
		builder.Property(c => c.ClientId).IsRequired();
		builder.Property(c => c.ExpiresOn).IsRequired(false);
		builder.HasOne(c => c.Client).WithMany(c => c.APIKeys).HasForeignKey(c => c.ClientId);
	}
}
