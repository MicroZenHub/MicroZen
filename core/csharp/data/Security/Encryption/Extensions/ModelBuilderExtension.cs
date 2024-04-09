using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using MicroZen.Data.Security.Encryption.Attributes;
using MicroZen.Data.Security.Encryption.Converters;
using MicroZen.Data.Security.Encryption.Interfaces;

namespace MicroZen.Data.Security.Encryption.Extensions;

public static class ModelBuilderExtension
{
	public static void UseEncryption(this ModelBuilder modelBuilder, IEncryptionProvider encryptionProvider)
	{
		if (modelBuilder is null)
			throw new ArgumentNullException(nameof(modelBuilder), "There is not ModelBuilder object.");
		if (encryptionProvider is null)
			throw new ArgumentNullException(nameof(encryptionProvider), "You should create encryption provider.");

		var encryptionConverter = new EncryptionConverter(encryptionProvider);
		foreach (var entityType in modelBuilder.Model.GetEntityTypes())
		{
			foreach (var property in entityType.GetProperties())
			{
				if (property.ClrType != typeof(string) || IsDiscriminator(property))
					continue;
				var attributes = property.PropertyInfo?.GetCustomAttributes(typeof(EncryptColumnAttribute), false);
				if(attributes?.Length != 0)
					property.SetValueConverter(encryptionConverter);
			}
		}

	}

	private static bool IsDiscriminator(IMutableProperty property) => property.Name == "Discriminator" || property.PropertyInfo == null;
}
