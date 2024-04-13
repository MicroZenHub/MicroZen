using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MicroZen.Data.Security.Encryption.Interfaces;
namespace MicroZen.Data.Security.Encryption.Converters;

internal sealed class EncryptionConverter(IEncryptionProvider encryptionProvider, ConverterMappingHints? mappingHints = null) :
	ValueConverter<string, string>(x =>
	encryptionProvider.Encrypt(x), x => encryptionProvider.Decrypt(x), mappingHints);
