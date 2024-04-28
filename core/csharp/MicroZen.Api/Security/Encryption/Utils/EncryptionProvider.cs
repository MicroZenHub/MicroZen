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

	/// <inheritdoc />
	public string Encrypt(string dataToEncrypt)
	{
		if (string.IsNullOrEmpty(key))
			throw new ArgumentNullException(EncryptionKeyVarName, "Please initialize your encryption key.");

		if (string.IsNullOrEmpty(dataToEncrypt))
			return string.Empty;

		var iv = new byte[16];
		byte[] array;

		using (var aes = Aes.Create())
		{
			aes.Key = Encoding.UTF8.GetBytes(key);
			aes.IV = iv;

			var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
			using (var memoryStream = new MemoryStream())
			{
				using (var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
				{
					using (var streamWriter = new StreamWriter(cryptoStream))
					{
						streamWriter.Write(dataToEncrypt);
					}
					array = memoryStream.ToArray();
				}
			}
		}
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

		var iv = new byte[16];

		using var aes = Aes.Create();
		aes.Key = Encoding.UTF8.GetBytes(key);
		aes.IV = iv;
		var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

		var buffer = Convert.FromBase64String(dataToDecrypt);
		using var memoryStream = new MemoryStream(buffer);
		using var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read);
		using var streamReader = new StreamReader(cryptoStream);
		return streamReader.ReadToEnd();
	}
}
