# MicroZen Ecosystem Steering

This is the shared, cross-repository steering document for MicroZen. The same
file is kept in both repositories so each one is self-describing:

- **`MicroZen`** (this repo) - the .NET backend: gRPC API, data layer, and the
  reusable auth client package(s). Public repo `MicroZenHub/MicroZen`.
- **`MicroZen.Web`** - the Angular dashboard that catalogues services and
  authorizes connections. Private repo `MicroZenHub/MicroZen.Web`.

Repo-specific build/run/test detail lives in each repo's own steering
(`product.md`, `structure.md`, `tech.md`). This document is the single source of
truth for **product intent, the provider model, and the roadmap**. When this
document and a repo-local doc disagree about intent or roadmap, this one wins;
when they disagree about a command or a path, the repo-local `tech.md`/
`structure.md` wins.

> Accuracy note: every "current state" claim below was checked against the code
> on the `docs/steering-ecosystem` branch of both repos. "Roadmap" and "gap"
> items are **not** implemented yet - do not describe them as working.

## 1. What MicroZen is

MicroZen helps teams manage microservice-to-microservice authentication. It has
two halves that are easy to confuse, so keep them distinct:

1. **Drop-in auth packages** (the backend's `MicroZen.OAuth2` library today).
   A service installs the package, points it at its MicroZen authority, and the
   package dynamically fetches the client IDs/secrets that are allowed to call
   it and wires up token validation. The goal: **add a new allowed client or
   auth flow without changing or redeploying the service's code.**
2. **A dashboard** (`MicroZen.Web`) to catalogue services and micro-frontends,
   visualize how they connect, and authorize connections between them.

The product intent is to be **provider-agnostic**: the same drop-in experience
across multiple identity providers, offered as **per-provider packages** so a
consumer pulls in only the provider they use.

## 2. Two separate auth concerns (read this before touching auth)

These are different and must not be conflated in code or docs:

| Concern                    | What it authenticates                       | Where it lives today                                      | Provider today                                              |
| -------------------------- | ------------------------------------------- | --------------------------------------------------------- | ----------------------------------------------------------- |
| **Dashboard user auth**    | A human logging into the MicroZen dashboard | `MicroZen.Web` -> `SupabaseAuthService`                   | **Supabase** (email/password + Google, GitHub social login) |
| **Catalogued client auth** | A microservice calling another microservice | `MicroZen` -> `MicroZen.OAuth2` package + API `X-API-Key` | **AWS Cognito** (the only wired provider in the package)    |

So the dashboard's own login runs on Supabase; the auth _flows MicroZen manages
for its users' services_ run (today) against AWS Cognito. The business has moved
from Cognito to Supabase, so **Supabase is a first-class MicroZen provider**, not
a temporary dashboard-only choice. The provider model and roadmap below treat
Supabase as a peer of Cognito, Entra, Auth0, and Google.

## 3. Provider model

### Current state

- The backend package `MicroZen.OAuth2` supports exactly **one** provider:
  **AWS Cognito**. `MicroZenProvider` defines only `Cognito`; any other value
  throws `NotSupportedException`
  (`core/csharp/MicroZen.OAuth2/Initialization.cs`,
  `Providers/Cognito/CognitoAuthServiceProvider.cs`).
- The backend **data model is already ahead of the package**. The gRPC
  `Provider` enum (`MicroZen.Api/Protos/client.proto`) and the dashboard's
  `MicroZenProvider` enum both model **AWS, Azure, Google**. These are stored and
  displayed, but only AWS Cognito has a working auth implementation.
- Supabase appears as a provider only for _dashboard login_ today; it is **not**
  yet a `MicroZenProvider`/`Provider` value in the catalogued-client model.

### Target state (roadmap)

Split the single package into a reusable core plus one package per provider so
consumers import only what they need:

- **`MicroZen.Core`** - shared, provider-independent code (state, config, gRPC
  client to the MicroZen authority, grant-type definitions, JWT plumbing).
- **`MicroZen.Auth.Cognito`** - AWS Cognito provider (extracted from today's
  `MicroZen.OAuth2/Providers/Cognito`).
- **`MicroZen.Auth.Supabase`** - Supabase provider. First-class, given the
  business move to Supabase.
- **`MicroZen.Auth.Entra`** - Microsoft Azure Entra.
- **`MicroZen.Auth.Auth0`** - Auth0 / Okta.
- **`MicroZen.Auth.Google`** - Google Auth.
- Further providers follow the same `MicroZen.Auth.<Provider>` pattern.

Today's single `MicroZen.OAuth2` package is the **starting point and tracked
tech debt**; the `MicroZen.Core` + `MicroZen.Auth.<Provider>` split is the
target. Do not add new providers into the monolithic package - new provider work
should drive the split.

### Provider rollout order (intended)

1. AWS Cognito (done - the baseline).
2. Supabase (first-class; business priority).
3. Microsoft Azure Entra.
4. Auth0 / Okta.
5. Google Auth.
6. Others, same pattern.

## 4. Supported auth flows and gaps

Grant types are modeled in three places that currently **disagree**, which is
itself a gap to close:

- Package enum `OAuth2GrantType` (`MicroZen.OAuth2/Definitions/OAuth2GrantType.cs`):
  AuthorizationCode, AuthorizationCodeWithPKCE, ClientCredentials, DeviceCode,
  RefreshToken.
- gRPC/data enum `GrantType` (`MicroZen.Api/Protos/client.proto`) and the
  dashboard enum `MicroZenOAuth2GrantType`: AuthorizationCode,
  AuthorizationCodeWithPkce, ClientCredentials, DeviceAuthorization,
  RefreshToken. Implicit (4), Hybrid (5) and ResourceOwnerPassword (6) were
  formally dropped (OAuth2 security BCP / out of scope); the numbers and names
  are `reserved` in the proto.

### What actually works (AWS Cognito)

Only two handlers are implemented
(`MicroZen.OAuth2/Providers/Cognito/CognitoAuthServiceProvider.cs`,
`Providers/Cognito/Flows/`):

- **Authorization Code** and **Authorization Code with PKCE** (same handler).
- **Client Credentials**.

Any other grant type reaches a `NotImplementedException` in
`AddOAuth2JwtAuthentication`. The API itself is wired with
**Authorization Code with PKCE** (`MicroZen.Api/Program.cs`).

### Documented gaps (intend to file issues and build out)

These are **modeled but not implemented**. Each should become a tracked issue:

| Grant type                         | Modeled in      | Implemented? |
| ---------------------------------- | --------------- | ------------ |
| Device Code / Device Authorization | package + proto | No           |
| Refresh Token                      | package only    | No           |

Additional known gaps:

- **Grant-type enums are inconsistent** across the package, the proto, and the
  dashboard (DeviceCode vs DeviceAuthorization; RefreshToken only in the package;
  dashboard still lists the dropped Implicit/Hybrid/ResourceOwnerPassword). Reconcile them.
- **The dashboard offers flows the backend rejects.** The grant-type picker
  (`MicroZen.Web` `libs/clients/src/lib/models/grant-type.model.ts`) marks
  Authorization Code, PKCE, Client Credentials, Implicit, and Resource Owner
  Password (both now dropped from the backend) as "available in AWS Cognito", but the package only implements the
  first three. A user can save a credential whose flow the package cannot honor.
- **Only AWS Cognito has any flow implementation.** Supabase/Entra/Auth0/Google
  have no flow handlers yet.

## 5. Dependency auto-patching (roadmap, not built)

A planned capability, separate from auth: monitor dependencies and open PRs into
**GitHub or GitLab** to auto-patch packages, driven by configurable update
strategies, across these package ecosystems:

- **.NET** (NuGet)
- **Node** (npm)
- **Python** (pip/poetry)
- **Go** (Go modules)

None of this exists in either repo today. Document it as roadmap only.

## 6. Multi-language client packages (roadmap)

The product intent is a drop-in package "in multiple languages." Today only the
**.NET** package exists (`MicroZen.OAuth2`). Planned client packages, so
non-.NET services get the same drop-in experience:

- **Node** client package.
- **Python** client package.
- **Go** client package.

These are roadmap. (Note: the same four ecosystems appear in the auto-patching
feature above, but these are two distinct efforts - client SDKs vs. dependency
patching.)

## 7. How the two repos connect at runtime

- The dashboard (`MicroZen.Web`) calls the .NET API over HTTP/JSON for
  **clients** and **organizations** (`ClientHttpService`,
  `OrganizationHttpService`) at `environment.apiUrl`
  (`https://localhost:7070` in development).
- The .NET API exposes gRPC with JSON transcoding; the dashboard uses the
  transcoded JSON endpoints.
- Dashboard **user login** does not go through the .NET API - it goes to
  Supabase directly.
- See `MicroZen` `docs/architecture.md` for the backend request-flow diagram.
  (That diagram currently describes the dashboard as an external Cognito/PKCE
  client and labels it "Angular 17 + Ionic/Capacitor"; both are stale - the
  dashboard is Angular 21 with no Ionic and logs in via Supabase. Treat this
  ecosystem doc as authoritative on that point.)

## 8. Security posture (differences to be aware of)

- **Nx Cloud token.** The backend (`MicroZen`) deliberately keeps its Nx Cloud
  token out of `nx.json` and sources it from the `NX_CLOUD_ACCESS_TOKEN` secret.
  The frontend (`MicroZen.Web`) currently commits an `nxCloudAccessToken` in
  `nx.json`, and it decodes to a **read-write** token. This is a mismatch worth
  closing: a committed read-write token should be rotated and moved to a secret.
- **Supabase anon key.** `MicroZen.Web` commits the Supabase **anon** key in
  `libs/environment/src/environment.development.ts`. Anon keys are public by
  design, but Row-Level Security must be the real guard; do not rely on the key
  being secret.

## 9. Glossary

- **Authority** - the MicroZen API instance a package talks to (`AuthorityUrl`)
  to fetch allowed client credentials.
- **Client** - a catalogued service/app (API, Web, Desktop, Mobile) in MicroZen.
- **Provider** - an identity platform (Cognito, Supabase, Entra, Auth0, Google).
- **Catalogued client auth** vs **dashboard user auth** - see section 2.
