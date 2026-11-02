using Microsoft.EntityFrameworkCore;
using MicroZen.Api.Context;
using MicroZen.Api.Entities.AuthCredentials;
using static Xunit.Assert;

namespace MicroZen.Core.Api.Test.Context;

/// <summary>
/// Value-level encryption round-trip for the OAuth2 client secret (issue #56).
///
/// <para>
/// Existing tests prove the <c>EncryptionProvider</c> is sound
/// (<c>Security/Encryption/EncryptionProviderTests.cs</c>) and that the model
/// attaches the converter to the right columns
/// (<c>Security/Encryption/Extensions/ModelBuilderExtensionTests.cs</c>). Neither
/// pushes a real secret value through the converter wired to
/// <see cref="OAuth2ClientCredentials.OAuth2ClientSecret"/> and checks that the
/// stored form is actually ciphertext. That gap is exactly how the inverted-guard
/// bug (#45) sailed through green.
/// </para>
///
/// <para>
/// The EF Core InMemory provider does not run value converters — it stores CLR
/// objects directly — so a <c>SaveChangesAsync</c>/read cannot observe ciphertext.
/// Instead these tests resolve the <c>EncryptionConverter</c> the built model
/// attaches to the secret column and drive a value through its
/// <c>ConvertToProvider</c> (write/store) and <c>ConvertFromProvider</c> (read)
/// delegates — the exact transform a real database column performs, with no DB.
/// </para>
///
/// <para>
/// Regression anchor: with the #45 guard inverted, the converter would attach to
/// the wrong columns — <see cref="OAuth2ClientCredentials.OAuth2ClientSecret"/>
/// would have no converter (so <see cref="EncryptedSecret_StoredForm_IsNotReadablePlaintext"/>
/// fails its not-null assert) while the unencrypted
/// <see cref="OAuth2ClientCredentials.Environment"/> would gain one (so
/// <see cref="UnencryptedField_StaysReadablePlaintext"/> fails). So this file
/// documents and catches the original bug at the value level.
/// </para>
///
/// <para>
/// A true persisted-column raw-bytes assertion needs a provider that honors value
/// converters (real Npgsql/PostgreSQL or Testcontainers); that heavier integration
/// test is outside the self-contained InMemory suite (<c>AGENTS.md</c>) and is
/// deferred out of #56 by design.
/// </para>
///
/// <para>
/// Conventions mirror <c>Context/MicroZenContextTests.cs</c>: setup in the
/// constructor with the <c>EncryptionKey</c> env var set, <see cref="IDisposable"/>
/// teardown, and GIVEN/WHEN/THEN comments.
/// </para>
/// </summary>
public class OAuth2ClientCredentialsEncryptionTests : IDisposable
{
	private const string Secret = "super-secret-value";

	private readonly DbContextOptions<MicroZenContext> _options;

	public OAuth2ClientCredentialsEncryptionTests()
	{
		// Required by MicroZenContext.OnModelCreating (column encryption).
		Environment.SetEnvironmentVariable("EncryptionKey", "1234567890");

		// InMemory only builds the model here; assertions never rely on it
		// honoring value converters.
		_options = new DbContextOptionsBuilder<MicroZenContext>()
			.UseInMemoryDatabase(databaseName: $"OAuth2ClientCredentialsEncryptionTests_{Guid.NewGuid():N}")
			.Options;
	}

	[Fact]
	public void EncryptedSecret_StoredForm_IsNotReadablePlaintext()
	{
		// GIVEN the converter the built model attaches to the secret column.
		using var context = new MicroZenContext(_options);
		var secretProperty = context.Model
			.FindEntityType(typeof(OAuth2ClientCredentials))!
			.FindProperty(nameof(OAuth2ClientCredentials.OAuth2ClientSecret));
		NotNull(secretProperty);

		var converter = secretProperty!.GetValueConverter();
		NotNull(converter);

		// WHEN the secret is pushed through the write/store direction.
		var stored = (string?)converter!.ConvertToProvider(Secret);

		// THEN the stored form is ciphertext, not the readable plaintext, and the
		// plaintext does not appear inside it (guards against a no-op converter).
		NotNull(stored);
		NotEqual(Secret, stored);
		DoesNotContain(Secret, stored!);
	}

	[Fact]
	public void EncryptedSecret_RoundTrips_RestoresPlaintext()
	{
		// GIVEN the converter attached to the secret column.
		using var context = new MicroZenContext(_options);
		var converter = context.Model
			.FindEntityType(typeof(OAuth2ClientCredentials))!
			.FindProperty(nameof(OAuth2ClientCredentials.OAuth2ClientSecret))!
			.GetValueConverter();
		NotNull(converter);

		// WHEN the secret is converted to the provider form and back.
		var back = converter!.ConvertFromProvider(converter.ConvertToProvider(Secret));

		// THEN the original plaintext is restored, so "not readable" means
		// encrypted, not corrupted or lost.
		Equal(Secret, back);
	}

	[Fact]
	public void UnencryptedField_StaysReadablePlaintext()
	{
		// GIVEN a non-[EncryptColumn] string on the same entity.
		using var context = new MicroZenContext(_options);
		var environmentProperty = context.Model
			.FindEntityType(typeof(OAuth2ClientCredentials))!
			.FindProperty(nameof(OAuth2ClientCredentials.Environment));
		NotNull(environmentProperty);

		// WHEN / THEN it carries no converter, so the readable field is never
		// transformed and stays plaintext.
		Null(environmentProperty!.GetValueConverter());
	}

	public void Dispose()
	{
		GC.SuppressFinalize(this);
		Environment.SetEnvironmentVariable("EncryptionKey", null);
	}
}
