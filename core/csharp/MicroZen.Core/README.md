![microzen_logo.png](../../../assets/microzen_color_logo.png)

# About MicroZen

Introducing **MicroZen**, a revolutionary Microservice Management tool designed to streamline the complexities of modern software architectures.

With an intuitive graphical user interface (GUI) and simplicity at its core, MicroZen empowers developers and architects to effortlessly catalogue microservices and micro-frontend applications, offering unparalleled visibility and control over the intricacies of their systems.

From health checks to comprehensive mapping of application and API interactions, MicroZen provides a centralized platform for orchestrating seamless authentication, service mapping, and uptime observability within distributed architectures.

# MicroZen Auth Packages

MicroZen auth ships as `MicroZen.Core` (shared, provider-independent code) plus one package per identity provider, e.g. `MicroZen.Auth.Cognito`. Install only the provider package you need; it brings `MicroZen.Core` with it. The packages seamlessly manages permissions for your microservices by dynamically retrieving and authenticating allowed client IDs and secrets for multiple OAuth2 flows at runtime.

Our approach will ensure that you will never need to make code changes to add a new client or auth flow again. This results in better uptime and observability of your microservices.

# Getting Started

To get started, follow these steps:

1. Sign up for a MicroZen account at [https://microzen.io](https://microzen.io) (or you can run our API on your own servers as well by following the instructions provided in this repository).
2. Create your first MicroZen client, specifying the type as an OAuth2 Server Resource.
3. Click the "Get Client Key" button to generate an API key for your client.
4. Install the provider NuGet package in your project (for AWS Cognito: `MicroZen.Auth.Cognito`).
5. Add the following configuration variables either to your `appsettings.json` files or to an environment variable:

`appsettings.json` format

```json
{
  "MicroZen": {
    "AuthorityUrl": "https://authority.microzen.io/{YOUR_ORGANIZATION_ID}",
    "APIKey": "YOUR_API_KEY",
    "Interval": 5
  }
}
```

> NOTE: Interval is optional (defaults to 5) and is measured in minutes

Environment variable format

```bash
MicroZen__AuthorityUrl="https://authority.microzen.io/{YOUR_ORGANIZATION_ID}"
MicroZen__APIKey="YOUR_API_KEY"
MicroZen__Interval="5"
```

> NOTE: Interval is optional (defaults to 5) and is measured in minutes

### Required scopes

The default authorization policy (applied to every `[Authorize]` endpoint) requires
an authenticated user whose space-delimited `scope` claim contains **every** scope
in `MicroZen:RequiredScopes`. A missing or insufficient scope returns **403**.

```json
{ "MicroZen": { "RequiredScopes": "api/read" } }
```

Env var: `MicroZen__RequiredScopes="api/read api/write"`. Separate multiple scopes with
spaces or commas. If unset or blank it defaults to `api/read` (it never means "no scope required").

> NOTE: Tokens with `token_use` = `id` (Cognito ID tokens from the authorization code
> flows) carry no `scope` claim and are exempt from this check. Every other token, including
> one with no `token_use` claim, must carry the scopes. Which `token_use` values are accepted
> at all is decided by token validation, not by this policy.

6. In your project's `Program.cs` file add the following code to configure the Cognito provider:

```csharp
using MicroZen.Auth.Cognito;
using MicroZen.Core.Definitions;

var builder = WebApplication.CreateBuilder(args);
[...]
// Select the OAuth2 grant type(s) to enable.
builder.Services.AddMicroZenCognito(
    grantTypes: OAuth2GrantType.AuthorizationCodeWithPKCE);

var app = builder.Build();
[...]
app.UseCors();
// Order matters here - must be called AFTER app.UseCors()
app.UseMicroZenCognito();
[...]
```

> _**Note**_: There is no need to call either `app.UseAuthentication()` or
> `app.UseAuthorization()`. Both are handled by `UseMicroZenCognito()`.

> _**Note**_: `AddMicroZenCognito` also accepts an optional
> `Dictionary<string, AuthorizationPolicy>` of named authorization policies
> before the `grantTypes` parameter.

> _**Supported today**_: the only provider package is `MicroZen.Auth.Cognito`, and
> the only implemented grant types are `AuthorizationCode`,
> `AuthorizationCodeWithPKCE`, `RefreshToken`, and `ClientCredentials`. Other providers and
> grant types are on the roadmap - see the MicroZen steering `ecosystem.md`.

# Provider Documentation

Below you can find information on getting setup with the specific OAuth2 Providers that MicroZen officially supports.

[AWS Cognito](../MicroZen.Auth.Cognito/README.md)

# Adding a provider

A provider package (`MicroZen.Auth.<Provider>`) references `MicroZen.Core`, implements `IMicroZenAuthProvider` (adds JWT bearer validation for the requested grant types), and exposes one `AddMicroZen<Provider>(...)` / `UseMicroZen<Provider>()` pair that delegates to `AddMicroZenAuth` / `UseMicroZenAuth`. See `MicroZen.Auth.Cognito` for the reference implementation.
