namespace MicroZen.OAuth2.Definitions;

/// <summary>
/// MicroZen Supported Providers
/// </summary>
/// <param name="value">The provider to use</param>
public class MicroZenProvider(string value)
{
	/// <inheritdoc />
	public override string ToString() => value;

	/// <summary>
	/// MicroZen Provider for AWS Cognito
	/// </summary>
	public static readonly MicroZenProvider Cognito = new MicroZenProvider("cognito");
}
