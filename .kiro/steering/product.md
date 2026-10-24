# Product & Architecture Overview

## What MicroZen is

MicroZen is a microservice and micro-frontend management tool. It gives
developers and architects a graphical catalogue of their services and front-end
apps, plus visibility and control over how those pieces interact. Core
capabilities (grounded in `README.md` and the source):

- A graphical catalogue of microservices and micro-frontend apps (served by
  the dashboard client described below).
- Application and API mapping to visualize interactions between apps and APIs.
- Health checks tracking for services (gRPC health checks on the API).
- OAuth2 / OpenID Connect support for authentication, managing client IDs and
  secrets across multiple OAuth2 flows.
- API-key based authorization alongside JWT bearer authentication.
- AES encryption of sensitive data stored in the database.

For a runtime view of how the API, auth, and data layers fit together (with a
request-flow diagram), see [`docs/architecture.md`](../../docs/architecture.md).

## Monorepo shape

This repository holds the .NET backend under `core/csharp`. It is still an Nx
18.2.3 workspace: the `@nx-dotnet/core` plugin (configured in `nx.json`) wraps
the .NET solution so the build, test, and format tasks run through the Nx
project graph. There is no in-repo front-end.

### Dashboard client (separate repository)

The graphical dashboard UI is **not** part of this repository. It lives in a
separate private repo and consumes the MicroZen API as an external client.
Earlier revisions of this workspace carried an Angular 17 + Ionic/Capacitor app
under `core/ts/dashboard` (with `core/ts/dashboard-e2e` for Cypress e2e); that
code has been removed from this repo and moved to the private dashboard repo.

### Backend: `core/csharp`

The .NET solution is `core/csharp/MicroZen.Core.sln` and contains three
projects:

- **MicroZen.Api** - the gRPC API. gRPC services are generated from the
  `.proto` contracts in `MicroZen.Api/Protos/` and mapped in `Program.cs`. It
  uses EF Core with PostgreSQL via Npgsql, exposes gRPC-JSON transcoding plus a
  Swagger UI, and secures endpoints with API keys (`Security/APIKeys`) and
  OAuth2 / JWT bearer auth. Sensitive database columns are encrypted
  (`Security/Encryption`).
- **MicroZen.OAuth2** - a reusable OAuth2 / OpenID Connect client library,
  packaged as a NuGet package and referenced by the API.
- **MicroZen.Api.Test** - the xUnit test project (assembly name
  `MicroZen.Core.Api.Test`).

## Toolchain caveat

The .NET projects target `net10.0` (the `TargetFramework` in all three csproj
files, with .NET 10 package versions in `core/csharp/Directory.Packages.props`),
so building or testing the C# stack requires a matching **.NET 10 SDK** (as the
`README.md` Prerequisites state). Do not change the csproj `TargetFramework`
values.
