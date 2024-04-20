namespace MicroZen.Data.Security.Encryption.Attributes;

/// <inheritdoc />
/// Used to encrypt and decrypt a string column of an Entity before saving or selecting to/from the database
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class EncryptColumnAttribute : Attribute
{

}
