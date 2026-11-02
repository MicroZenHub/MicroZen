using MicroZen.Data.Security.Encryption.Utils;
using static Xunit.Assert;

namespace MicroZen.Core.Api.Test.Security.Encryption;

public class EncryptionProviderTests
{
	private const string Key = "a-test-encryption-key";

	[Fact]
	public void Encrypt_SamePlaintextTwice_ProducesDifferentCiphertext()
	{
		// GIVEN
		var provider = new EncryptionProvider(Key);
		const string plaintext = "sensitive-value";

		// WHEN
		var first = provider.Encrypt(plaintext);
		var second = provider.Encrypt(plaintext);

		// THEN — random per-op IV makes the two results differ
		NotEqual(first, second);
	}

	[Theory]
	[InlineData("sensitive-value")]
	[InlineData("a much longer string with spaces and punctuation!?")]
	[InlineData("non-ASCII: café, naïve, 日本語, emoji 🚀")]
	public void EncryptThenDecrypt_RoundTrips(string plaintext)
	{
		// GIVEN
		var provider = new EncryptionProvider(Key);

		// WHEN
		var decrypted = provider.Decrypt(provider.Encrypt(plaintext));

		// THEN
		Equal(plaintext, decrypted);
	}

	[Fact]
	public void EncryptThenDecrypt_EmptyString_RoundTripsToEmpty()
	{
		// GIVEN
		var provider = new EncryptionProvider(Key);

		// WHEN
		var decrypted = provider.Decrypt(provider.Encrypt(string.Empty));

		// THEN
		Equal(string.Empty, decrypted);
	}

	[Theory]
	[InlineData("1234567890")] // 10 bytes — not a legal raw AES key length
	[InlineData("this-is-a-fairly-long-configured-encryption-key-value-123456")]
	public void RoundTrip_WorksRegardlessOfConfiguredKeyLength(string key)
	{
		// GIVEN — KDF normalizes any non-empty key to 32 bytes
		var provider = new EncryptionProvider(key);
		const string plaintext = "sensitive-value";

		// WHEN
		var decrypted = provider.Decrypt(provider.Encrypt(plaintext));

		// THEN
		Equal(plaintext, decrypted);
	}

	[Fact]
	public void Decrypt_CiphertextShorterThanIv_ThrowsFormatException()
	{
		// GIVEN — base64 that decodes to fewer than 16 bytes
		var provider = new EncryptionProvider(Key);
		var tooShort = Convert.ToBase64String(new byte[8]);

		// WHEN / THEN
		Throws<FormatException>(() => provider.Decrypt(tooShort));
	}

	[Fact]
	public void Encrypt_WithEmptyKey_ThrowsArgumentNullException()
	{
		// GIVEN
		var provider = new EncryptionProvider(string.Empty);

		// WHEN / THEN
		Throws<ArgumentNullException>(() => provider.Encrypt("value"));
	}
}
