namespace MicroZen.Data.Entities;

public class OAuth2ClientConfig
{
  public int Id { get; set; }
  public GrantType? OAuth2GrantType { get; set; }
  public string OAuth2ClientId { get; set; } = string.Empty;
  public string? OAuth2ClientSecret { get; set; }
  public string? AllowedScopes { get; set; }
  public bool? RequirePkce { get; set; }
}

public enum GrantType
{
  AuthorizationCode = 0,
  AuthorizationCodeWithPkce = 1,
  ClientCredentials = 2,
  DeviceAuthorization = 3,
  Implicit = 4,
  Hybrid = 5,
  ResourceOwnerPassword = 6

}
