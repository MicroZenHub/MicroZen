using Microsoft.EntityFrameworkCore;
using MicroZen.Api.Context;
using MicroZen.Api.Entities;
using MicroZen.Api.Entities.AuthCredentials;
using static Xunit.Assert;

namespace MicroZen.Core.Api.Test.Security.Encryption.Extensions;

/// <summary>
/// Verifies that <c>ModelBuilder.UseEncryption</c> attaches the
/// <c>EncryptionConverter</c> to exactly the string properties marked with
/// <c>[EncryptColumn]</c> — the fix for issue #45, where the guard was inverted.
///
/// The InMemory provider ignores value converters, so a save/read round-trip
/// cannot prove encryption. These tests instead assert the mapping decision
/// directly: which properties carry the converter on the built model.
///
/// Conventions mirror <c>Context/MicroZenContextTests.cs</c>: setup in the
/// constructor with the <c>EncryptionKey</c> env var set, <see cref="IDisposable"/>
/// teardown, and GIVEN/WHEN/THEN comments.
/// </summary>
public class ModelBuilderExtensionTests : IDisposable
{
	private readonly DbContextOptions<MicroZenContext> _options;

	public ModelBuilderExtensionTests()
	{
		// Required by MicroZenContext.OnModelCreating (column encryption).
		Environment.SetEnvironmentVariable("EncryptionKey", "1234567890");

		_options = new DbContextOptionsBuilder<MicroZenContext>()
			.UseInMemoryDatabase(databaseName: $"ModelBuilderExtensionTests_{Guid.NewGuid():N}")
			.Options;
	}

	/// <summary>
	/// The string properties that carry <c>[EncryptColumn]</c> in the model.
	/// Kept here so the regression count below reads against a single source.
	/// </summary>
	private static readonly (Type EntityType, string PropertyName)[] MarkedStringProperties =
	[
		(typeof(OAuth2ClientCredentials), nameof(OAuth2ClientCredentials.OAuth2ClientId)),
		(typeof(OAuth2ClientCredentials), nameof(OAuth2ClientCredentials.OAuth2ClientSecret)),
		(typeof(OAuth2ClientCredentials), nameof(OAuth2ClientCredentials.AllowedScopes)),
		(typeof(OrganizationUser), nameof(OrganizationUser.FirstName)),
		(typeof(OrganizationUser), nameof(OrganizationUser.LastName)),
		(typeof(OrganizationUser), nameof(OrganizationUser.Email)),
	];

	[Fact]
	public void MarkedStringColumns_GetEncryptionConverter()
	{
		// GIVEN a context whose model has been built with UseEncryption.
		using var context = new MicroZenContext(_options);
		var model = context.Model;

		// WHEN / THEN each [EncryptColumn] string property carries the converter.
		foreach (var (entityType, propertyName) in MarkedStringProperties)
		{
			var property = model.FindEntityType(entityType)!.FindProperty(propertyName);
			NotNull(property);

			var converter = property!.GetValueConverter();
			NotNull(converter);
			Equal("EncryptionConverter", converter!.GetType().Name);
		}
	}

	[Fact]
	public void UnmarkedStringColumns_HaveNoConverter()
	{
		// GIVEN the built model.
		using var context = new MicroZenContext(_options);
		var organization = context.Model.FindEntityType(typeof(Organization))!;

		// WHEN / THEN string columns without [EncryptColumn] are left unencrypted.
		Null(organization.FindProperty(nameof(Organization.Name))!.GetValueConverter());
		Null(organization.FindProperty(nameof(Organization.WebsiteUrl))!.GetValueConverter());
	}

	[Fact]
	public void NonStringColumns_HaveNoConverter()
	{
		// GIVEN the built model.
		using var context = new MicroZenContext(_options);
		var organization = context.Model.FindEntityType(typeof(Organization))!;

		// WHEN / THEN the int key column is never touched by the encryption hook.
		Null(organization.FindProperty(nameof(Organization.Id))!.GetValueConverter());
	}

	[Fact]
	public void ConvertedPropertyCount_EqualsMarkedStringPropertyCount()
	{
		// GIVEN the built model.
		using var context = new MicroZenContext(_options);

		// WHEN counting every property that has the encryption converter attached.
		var convertedCount = context.Model.GetEntityTypes()
			.SelectMany(e => e.GetProperties())
			.Count(p => p.GetValueConverter()?.GetType().Name == "EncryptionConverter");

		// THEN it matches the number of [EncryptColumn] string properties, so a
		// future inversion of the guard fails this assertion immediately.
		Equal(MarkedStringProperties.Length, convertedCount);
	}

	public void Dispose()
	{
		GC.SuppressFinalize(this);
		Environment.SetEnvironmentVariable("EncryptionKey", null);
	}
}
