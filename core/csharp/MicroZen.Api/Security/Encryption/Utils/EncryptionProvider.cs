using System.Security.Cryptography;
using System.Text;

namespace MicroZen.Data.Security.Encryption.Utils;

/// <summary>
/// Interface for encryption and decryption.
/// </summary>
public interface IEncryptionProvider
{
	/// <summary>
	/// Encrypts the data.
	/// </summary>
	/// <param name="dataToEncrypt">The data which will be encrypted.</param>
	/// <returns>string</returns>
	string Encrypt(string dataToEncrypt);

	/// <summary>
	/// Decrypts the data.
	/// </summary>
	/// <param name="dataToDecrypt">The data which will be decrypted.</param>
	/// <returns>string</returns>
	string Decrypt(string dataToDecrypt);
}

/// <inheritdoc />
public class EncryptionProvider(string key) : IEncryptionProvider
{
	/// <summary>
	/// The encryption key Variable name.
	/// <remarks>Must be set as config value of "EncryptionKey"</remarks>
	/// </summary>
	public const string EncryptionKeyVarName = "EncryptionKey";

	/// <summary>
	/// The size, in bytes, of the AES initialization vector prepended to the ciphertext.
	/// </summary>
	private const int IvSize = 16;

	/// <summary>
	/// Derives a fixed 32-byte (AES-256) key from the configured string via SHA-256.
	/// This normalizes any non-empty configured value to a legal AES key length.
	/// </summary>
	/// <returns>A 32-byte key suitable for AES-256.</returns>
	private byte[] DeriveKey() => SHA256.HashData(Encoding.UTF8.GetBytes(key));

	/// <inheritdoc />
	public string Encrypt(string dataToEncrypt)
	{
		if (string.IsNullOrEmpty(key))
			throw new ArgumentNullException(EncryptionKeyVarName, "Please initialize your encryption key.");

		if (string.IsNullOrEmpty(dataToEncrypt))
			return string.Empty;

		using var aes = Aes.Create();
		aes.Key = DeriveKey();
		aes.GenerateIV();
		var iv = aes.IV;

		var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
		using var memoryStream = new MemoryStream();
		memoryStream.Write(iv, 0, iv.Length); // prepend IV
		using (var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
		using (var streamWriter = new StreamWriter(cryptoStream))
		{
			streamWriter.Write(dataToEncrypt);
		}
		var array = memoryStream.ToArray();

		var result = Convert.ToBase64String(array);
		return result;
	}

	/// <inheritdoc />
	public string Decrypt(string dataToDecrypt)
	{
		if (string.IsNullOrEmpty(key))
			throw new ArgumentNullException(EncryptionKeyVarName, "Please initialize your encryption key.");

		if (string.IsNullOrEmpty(dataToDecrypt))
			return string.Empty;

		var buffer = Convert.FromBase64String(dataToDecrypt);
		if (buffer.Length < IvSize)
			throw new FormatException("Ciphertext is too short to contain an IV.");

		using var aes = Aes.Create();
		aes.Key = DeriveKey();
		var iv = new byte[IvSize];
		Array.Copy(buffer, 0, iv, 0, IvSize);
		aes.IV = iv;
		var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

		using var memoryStream = new MemoryStream(buffer, IvSize, buffer.Length - IvSize);
		using var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read);
		using var streamReader = new StreamReader(cryptoStream);
		return streamReader.ReadToEnd();
	}
}
