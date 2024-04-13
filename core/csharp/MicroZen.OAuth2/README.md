![microzen_logo.png](../../../assets/microzen_color_logo.png)
# About MicroZen
Introducing **MicroZen**, a revolutionary Microservice Management tool designed to streamline the complexities of modern software architectures.

With an intuitive graphical user interface (GUI) and simplicity at its core, MicroZen empowers developers and architects to effortlessly catalogue microservices and micro-frontend applications, offering unparalleled visibility and control over the intricacies of their systems.

From health checks to comprehensive mapping of application and API interactions, MicroZen provides a centralized platform for orchestrating seamless authentication, service mapping, and uptime observability within distributed architectures.

# MicroZen.OAuth2 Package
The `MicroZen.OAuth2` package provides support for secure authentication processes. The MicroZen.OAuth2 package seamlessly manages permissions for your microservices by dynamically retrieving and authenticating allowed client IDs and secrets for multiple OAuth2 flows at runtime.

Our approach will ensure that you will never need to make code changes to add a new client or auth flow again. This results in better uptime and observability of your microservices.

# Getting Started
To get started with MicroZen.OAuth2, follow these steps:
1. Sign up for a MicroZen account at [https://microzen.io](https://microzen.io) (or you can run our API on your own servers as well by following the instructions provided in this repository).
2. Create your first MicroZen client, specifying the type as an OAuth2 Server Resource.
3. Click the "Get Client Key" button to generate an API key for your client.
4. Install the `MicroZen.OAuth2` NuGet package in your project.
5. Add the following configuration variables either to your `appsettings.json` files or to an environment variable:

`appsettings.json` format
```json
{
  "MicroZen": {
    "AuthorityUrl": "https://authority.microzen.io/{YOUR_ORGANIZATION_ID}",
    "ClientKey": "YOUR_CLIENT_KEY"
  }
}
```

Environment variable format
```bash
MICROZEN_AUTHORITY_URL="https://authority.microzen.io/{YOUR_ORGANIZATION_ID}"
MICROZEN_CLIENT_KEY="YOUR_CLIENT_KEY"
```
6. In your project's `Program.cs` file add the following code to configure the `MicroZen.OAuth2` service:

```csharp
using MicroZen.OAuth2;

var builder = WebApplication.CreateBuilder(args);
[...]
builder.AddMicroZenOAuth2(OAuth2Flows.ClientCredentials);

var app = builder.Build();
[...]
app.AddCors();
// Order matters here - Must come after AddCors
app.UseMicroZenOAuth2();
[...]
```

> _**Note**_: There is no need to call either of the following methods: `app.UseAuthentication()` or `app.UseAuthorization()`
> These are both handled by the `AddMicroZenOAuth2Authorization()` method

> _**Note**_: You can replace `OAuth2Flows.ClientCredentials` with any of the supported OAuth2 flows.
