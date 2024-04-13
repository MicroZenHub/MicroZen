namespace MicroZen.Data.Security.Encryption.Interfaces;

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
