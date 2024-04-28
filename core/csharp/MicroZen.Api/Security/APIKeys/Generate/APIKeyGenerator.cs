using System.IO.Hashing;
using static System.Text.Encoding;

namespace MicroZen.Api.Security.APIKeys;

/// <summary>
/// The API Key Generator
/// </summary>
public static class APIKeyGenerator
{
	/// <summary>
	/// Generate a new API Key
	/// </summary>
	/// <returns></returns>
	public static string GenerateAPIKey()
	{
		var key = Guid.NewGuid().ToString("N");
		return $"mcz_{key}_{CreateCRC32(key)}";
	}

	/// <summary>
	/// Create an CRC32 hash of the input string
	/// </summary>
	/// <param name="input"></param>
	/// <returns></returns>
	public static string CreateCRC32(string input)
	{
		var crc32 = new Crc32();
		var bytes = UTF8.GetBytes(input);
		crc32.Append(bytes);
		var checkSum = crc32.GetCurrentHash();
		// to Big Endian
		Array.Reverse(checkSum);
		return BitConverter.ToString(checkSum).Replace("-", "").ToLower();
	}
}
