namespace MicroZen.Data.Security.Encryption.Interfaces;

public interface IEncryptionProvider
{
	string Encrypt(string dataToEncrypt);
	string Decrypt(string dataToDecrypt);
}
