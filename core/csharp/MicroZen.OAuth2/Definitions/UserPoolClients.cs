namespace MicroZen.OAuth2.Definitions;

/// <summary>
/// Represents the credentials of the OAuth2 clients that are allowed to access the app grouped by UserPoolId and Region
/// </summary>
/// <param name="UserPoolId"><see cref="string" /> - The User Pool Id</param>
/// <param name="Region"><see cref="string" /> - The AWS Region</param>
/// <param name="ClientIds">
///		<see cref="Array"/> of <see cref="string"/>
/// </param>
/// <param name="ClientSecrets"><see cref="Array"/> of <see cref="string"/> - The ClientSecrets of the OAuth2 clients that are allowed to access the app</param>
public record UserPoolClients(string UserPoolId, string Region, string[] ClientIds, string[] ClientSecrets);
