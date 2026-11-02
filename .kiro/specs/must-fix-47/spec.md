# Spec: Fix #47 — Clients, Organizations, and OrganizationUsers services are unauthenticated

Issue: MicroZenHub/MicroZen #47 (Must Fix, P0)
Target merge: `epic/must-have-fixes`
Scope: `core/csharp/MicroZen.Api` — this is a .NET-only spec. Do not change `TargetFramework` (net10.0).

> This is a SPEC. No application code, branches, or git actions are produced by this document.

---

## 1. Problem (confirmed against actual code)

Three gRPC services in `core/csharp/MicroZen.Api/Services/` carry **no** `[Authorize]`
attribute, so every method is reachable with no authentication. Confirmed by reading
the source.

### Exposed surface

**`ClientsGrpcService.cs`** — `class ClientsGrpcService(MicroZenContext db)` has no
`[Authorize]`. Every RPC is open:

- `GetManyClients` — list all clients of any type, paged.
- `GetClient` — read any client by id, **including its `OAuth2Credentials`** (OAuth2
  client id + secret) and `AllowedClientOAuth2Credentials`.
- `GetAllowedOAuthClientCredentials` — dump allowed OAuth2 credentials.
- `GetClientIdFromApiKey` — resolve a client id from a raw API key.
- `CreateClient` — create a client in **any** `OrganizationId` the caller names.
- `UpdateClient` — mutate any client and its OAuth2 credentials.
- `AuthorizeClients` / `DisallowClients` — grant/revoke OAuth2 credential links on any client.
- `DeleteClient` — soft-delete any client.

**`OrganizationsService.cs`** — `class OrganizationsService(MicroZenContext db)` has no
`[Authorize]`. Open RPCs:

- `GetOrganization` / `GetOrganizations` — read any/all organizations.
- `UpsertOrganization` — create or overwrite any organization.
- `DisableOrganization` / `EnableOrganization` — soft-delete or restore any organization.

**`OrganizationUsersService.cs`** — `class OrganizationUsersService(MicroZenContext db)`
(namespace `MicroZen.Core.Api.Services`) has no `[Authorize]`. Open RPC:

- `GetOrganizationUsers` — read every user (name, email) of any organization by id.

### Reference pattern — the one service that is annotated

`ClientApiKeysService.cs` is the existing pattern to copy:

```csharp
[Authorize]
public class ClientApiKeysService(MicroZenContext db) : ClientApiKeysGrpcService.ClientApiKeysGrpcServiceBase
```

A bare `[Authorize]` (no policy name) applies the authorization default policy, which
`AddMicroZenOAuth2` sets to `RequireAuthenticatedUser()`.

### The TODO comments (verbatim) that confirm tenant checks were never wired up

In `ClientsGrpcService.cs`:

```csharp
// TODO - Add [Policy(typeof(Client), Permission.Read)] attribute to block access if user is not in the organization for this client
// TODO - Add [Policy(typeof(Client), Permission.Create)] attribute to block access if user is not in the organization for this client
// TODO - Add [Policy(typeof(Client), Permission.Update)] attribute to block access if user is not in the organization for this client
// TODO - Add [Policy(typeof(Client), Permission.Manage)] attribute to block access if user is not in the organization for this client
// TODO - Add [Policy(typeof(Client), Permission.Delete)] attribute to block access if user is not in the organization for this client
// TODO - Assign client.DeletedBy from User ID claims
```

In `OrganizationsService.cs`:

```csharp
// TODO - Add [Policy(typeof(Organization), Permission.Manage)] attribute to block access if user is not in the organization for this client
```

In `OrganizationUsersService.cs`:

```csharp
// TODO - Add [Policy(typeof(OrganizationUsers), Permission.Read)] attribute to block access if user is not in the organization for this client
```

(`ClientApiKeysService` also has `// TODO: Get UserID from Claims ...` comments — the
same missing claims plumbing.)

### Secondary finding — `[Authorize]` is currently not even enforced (must be fixed here)

`Program.cs` calls `builder.Services.AddMicroZenOAuth2(...)` (which registers the JWT
auth handler and the authorization services) but **never calls the matching middleware**.
There is no `app.UseMicroZenOAuth2(...)` and no `app.UseAuthentication()` /
`app.UseAuthorization()` in the request pipeline. The middleware wiring lives in
`MicroZen.OAuth2/Providers/Cognito/CognitoAuthServiceProvider.cs::UseCognitoOAuth2` (which
calls `UseAuthentication()` then `UseAuthorization()`), exposed via
`MicroZen.OAuth2/Utilization.cs::UseMicroZenOAuth2`, and it is not invoked.

Consequence: without the middleware, `[Authorize]` is inert. Adding `[Authorize]` to the
three services does nothing until the pipeline runs authentication/authorization. So the
minimal safe fix is **both**: add the attributes **and** add the middleware to `Program.cs`.
Fixing the attributes alone would leave the hole open and give a false sense of safety.

---

## 2. Fix

### 2a. Enforce the auth middleware (prerequisite, in `Program.cs`)

The remark on `UseCognitoOAuth2` requires it be called **after** `app.UseCors()`. Add the
middleware after the existing `app.UseCors();` line, before the gRPC service mappings:

```csharp
app.UseCors();
app.UseMicroZenOAuth2(MicroZenProvider.Cognito); // add — enforces [Authorize]
```

Add `using MicroZen.OAuth2;` / `using MicroZen.OAuth2.Definitions;` if not already present
(both are already imported in `Program.cs`).

### 2b. Add `[Authorize]` to each of the three services

Copy the `ClientApiKeysService` pattern exactly — bare `[Authorize]` on the class, with
`using Microsoft.AspNetCore.Authorization;` added to each file:

```csharp
using Microsoft.AspNetCore.Authorization;
// ...
[Authorize]
public class ClientsGrpcService(MicroZenContext db) : MicroZen.Grpc.V1.ClientsGrpcService.ClientsGrpcServiceBase
```

```csharp
using Microsoft.AspNetCore.Authorization;
// ...
[Authorize]
public class OrganizationsService(MicroZenContext db) : OrganizationsGrpcService.OrganizationsGrpcServiceBase
```

```csharp
using Microsoft.AspNetCore.Authorization;
// ...
[Authorize]
public class OrganizationUsersService(MicroZenContext db) : OrganizationUsersGrpcService.OrganizationUsersGrpcServiceBase
```

A bare `[Authorize]` applies the default policy (`RequireAuthenticatedUser()`), which is
what closes the hole: an unauthenticated caller is rejected before any method runs.

### 2c. Tenant / ownership check — scope and recommendation

**Identity → organization mapping (how a caller maps to an org):**

- The JWT subject is the OAuth provider user id. `OrganizationUser.UserId` (a `Guid`) is
  documented in the entity as aligning with the OAuth provider ID.
- `OrganizationUser` ↔ `Organization` is many-to-many via the `OrganizationUserOrganization`
  join table.
- `Client.OrganizationId` ties a client to its organization.
- So an authenticated caller's set of organizations is:
  `db.OrganizationUsers.Where(ou => ou.UserId == callerGuid).SelectMany(ou => ou.Organizations)`.
  A caller may operate on a client only if `client.OrganizationId` is in that set; on an
  organization only if its id is in that set.

**Current blockers to implementing the check now:**

1. No code anywhere reads claims from the `ServerCallContext` / `HttpContext`
   (grep for `ClaimTypes` / `FindFirst` / `NameIdentifier` returns nothing in the API). The
   claim name holding the user Guid is not yet established in code.
2. The `[Policy(typeof(T), Permission.X)]` attribute the TODOs describe does not exist —
   there is no such attribute type, no `Permission` enum, and no policy-per-resource
   infrastructure in the repo.
3. `OrganizationUser.UserId` is an `[EncryptColumn]` field, so a lookup by `UserId` needs a
   query path that accounts for encryption-at-rest (equality lookups on encrypted columns
   are not free).

**Recommendation — minimal safe change now, tenant check as a tracked follow-up:**

Close the authentication hole in this PR (2a + 2b): require an authenticated caller on all
four services and actually run the middleware. This stops anonymous read/delete of clients,
organizations, OAuth2 secrets, and user PII — the P0 in #47.

Do **not** attempt the full per-resource tenant/ownership authorization in this PR. It
needs a claims-reading convention, a resource-policy design, and an encrypted-column lookup
path that are each their own unit of work and would widen the blast radius. Instead:

- Keep the existing `// TODO - Add [Policy(...)]` comments in place (they still describe the
  gap accurately) and leave `UserId`-from-claims plumbing to the follow-up.
- Create one follow-up issue: "Per-organization tenant/ownership authorization for Clients,
  Organizations, OrganizationUsers" capturing the mapping above, the resource-policy design,
  and the claims convention. Reference it from #47.

This is the minimal change that closes the auth hole described in #47 without shipping a
half-built authorization model.

---

## 3. Test plan

Mirror the local CI gate from `AGENTS.md`:

- `npx nx format:check`
- `npx nx affected -t lint test build`
- `dotnet test core/csharp/MicroZen.Core.sln`

(`scripts/check.sh` runs all three in order.)

### New tests proving unauthenticated calls are rejected

The existing `MicroZen.Api.Test` project (xUnit, net10.0) has only unit tests and no
integration harness. Add an integration test fixture using
`Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>` against the real pipeline
(so the `UseMicroZenOAuth2` middleware is exercised). To host `Program` from a top-level
statements file, expose it to the test project (e.g. add `public partial class Program { }`
at the end of `Program.cs`, or an `InternalsVisibleTo`). The factory should use the EF Core
InMemory provider and an `EncryptionKey` the same way the existing tests are self-contained
(no PostgreSQL), per `AGENTS.md`.

For each of the four services (`ClientsGrpcService`, `OrganizationsService`,
`OrganizationUsersService`, and a regression test for `ClientApiKeysService`):

1. **Unauthenticated call is rejected.** Call a representative RPC over the gRPC-JSON
   transcoding endpoint with no bearer token; assert HTTP `401` (gRPC `Unauthenticated`).
   Cover at least one read and one mutating RPC per service, including:
   - `GetClient` / `CreateClient` / `DeleteClient`
   - `GetOrganization` / `UpsertOrganization` / `DisableOrganization`
   - `GetOrganizationUsers`
2. **Authenticated call passes the authorization gate.** With a valid test-issued bearer
   token (authentication scheme stubbed in the test host so a known principal is present),
   assert the same RPCs are no longer rejected at the auth layer (status is not 401; a
   `NotFound`/business result is acceptable — the point is auth passed).
3. **Middleware regression guard.** A test that fails if `UseAuthentication`/
   `UseAuthorization` are absent from the pipeline — i.e. an unauthenticated call to any
   `[Authorize]` service returns 401, not 200. This is the guard against the secondary
   finding in §1 silently regressing.

### Pass criteria

- All three gate steps pass locally and in CI.
- New auth tests pass; existing unit tests still pass.

---

## 4. Risk / blast radius

- **Any existing caller that relied on no-auth will break** with `401`. This is the intended
  P0 fix, but every client of these four services must now send a valid bearer token.
  Specifically affected:
  - The `GetClientIdFromApiKey` RPC was previously anonymous; any internal component
    resolving a client id from an API key must now authenticate. Check whether the OAuth2
    token-issuing / API-key paths call this without a user token.
  - `GetManyClients` / `GetOrganizations` dashboards or scripts that polled anonymously.
- **Seed/dev flow:** `MigrateAndSeedDevDataOnStart` and `scripts/seed-db.sh` are unaffected
  (they run server-side, not through the gRPC services), but local manual testing of these
  endpoints now requires a token.
- **Secondary-finding scope:** adding `UseMicroZenOAuth2` activates authentication for the
  **entire** app pipeline, including `ClientApiKeysService` which already had `[Authorize]`
  but was never actually enforced. Any caller of `ClientApiKeysService` that worked before
  only because the attribute was inert will now be challenged. This is correct but is a
  behavior change beyond the three services named in #47 — call it out in the PR description.
- **gRPC reflection / health checks / Swagger:** `MapGrpcReflectionService`,
  `MapGrpcHealthChecksService`, and the Swagger UI are not `[Authorize]`-gated by this change;
  confirm health checks still respond for liveness probes after the middleware is added.
- **Tests/clients to update:** add the integration tests above; audit any consumer in
  `MicroZen.OAuth2` (the `ClientsGrpcService` gRPC client registered in `Initialization.cs`
  adds an `X-API-Key` call credential but not necessarily a bearer token) and update it to
  carry an authenticated token where it calls the now-protected services.

---

## 5. Acceptance

- `[Authorize]` present on `ClientsGrpcService`, `OrganizationsService`, and
  `OrganizationUsersService`, matching the `ClientApiKeysService` pattern.
- `Program.cs` invokes the auth middleware (`app.UseMicroZenOAuth2(MicroZenProvider.Cognito)`
  after `UseCors()`), so `[Authorize]` is actually enforced.
- New integration tests prove unauthenticated calls to all four services return `401`
  (gRPC `Unauthenticated`) and authenticated calls pass the auth gate; the middleware
  regression guard is in place.
- Local gate passes: `npx nx format:check`, `npx nx affected -t lint test build`,
  `dotnet test core/csharp/MicroZen.Core.sln`.
- A follow-up issue for the per-organization tenant/ownership check exists and is linked
  from #47; the `// TODO - Add [Policy(...)]` comments remain as the in-code pointer.
- PR opened into `epic/must-have-fixes` with all CI checks green.
  Note: as of this spec, no `epic/must-have-fixes` branch exists yet in the repo — it must
  be created (or confirmed) before the PR can target it.
```
