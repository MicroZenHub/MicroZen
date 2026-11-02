# Spec: #79 — Per-organization tenant/ownership authorization for Clients, Organizations, OrganizationUsers

Issue: MicroZenHub/MicroZen [#79](https://github.com/MicroZenHub/MicroZen/issues/79)
Follows: #47 (adds `[Authorize]` + enforces the auth middleware). This spec is the
tenant-isolation follow-up that #47 §2c deferred.
Target merge: `epic/must-have-fixes`
Scope: `core/csharp/MicroZen.Api` — .NET-only. Do not change `TargetFramework` (net10.0).

> This is a SPEC. No application code, branches, or git actions are produced by this document.

---

## 1. Problem

After #47, the four services require an **authenticated** caller, but authentication is
not authorization. Any authenticated caller can still read or modify **any** organization's
data — there is no check that the caller belongs to the organization that owns the
resource. A user authenticated for org A can read, update, or delete org B's clients
(including their OAuth2 client id + secret), org B's organization record, and org B's user
list (names, emails).

This is exactly the gap the in-code TODOs describe. They remain in the source after #47 and
are the pointer to this work.

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

(`ClientApiKeysService` carries the matching `// TODO: Get UserID from Claims ...` gap — the
same missing claims plumbing this spec builds.)

---

## 2. Design

### 2.1 Identity → organization mapping (confirmed against entity code)

- The JWT subject is the OAuth (Cognito) provider user id.
- `OrganizationUser.UserId` is a `Guid` and is documented in the entity as aligning with the
  OAuth provider ID. This is the join from the token's subject to a MicroZen user row.
- `OrganizationUser` ↔ `Organization` is **many-to-many** via the join table
  `OrganizationUserOrganization` (keys `OrganizationUserId`, `OrganizationId`), configured in
  both `OrganizationUserConfig` and `OrganizationConfig`.
- `Client.OrganizationId` ties a client to exactly one organization
  (`Organization.HasMany(o => o.Clients).WithOne(c => c.Organization)` with
  `OnDelete(DeleteBehavior.SetNull)`, so `OrganizationId` is nullable).

### 2.2 The caller's organization set

Given the caller Guid resolved from the token (`callerId`), the set of organization ids the
caller may act within:

```csharp
var callerOrgIds = await db.OrganizationUsers
    .Where(ou => ou.UserId == callerId)
    .SelectMany(ou => ou.Organizations.Select(o => o.Id))
    .ToListAsync();
```

(`UserId` is a `Guid`, so this is a plain equality query — see §3c for why no decryption is
needed on this column.)

### 2.3 Resource rules (client vs organization vs user)

For each RPC, resolve the resource's owning organization id, then require it to be in
`callerOrgIds`. Deny with gRPC `PermissionDenied` when it is not. Use `NotFound` where the
resource does not exist, and **do not leak existence across tenants** — a resource in another
org should read as `PermissionDenied` (or `NotFound` if the chosen convention is to hide
existence; pick one and apply it uniformly).

- **Client resources** (`ClientsGrpcService`): owning org = `client.OrganizationId`. Applies
  to `GetClient`, `UpdateClient`, `DeleteClient`, `AuthorizeClients`, `DisallowClients`,
  `GetAllowedOAuthClientCredentials`, `GetClientIdFromApiKey`. For `CreateClient`, require the
  **requested** `request.OrganizationId` to be in `callerOrgIds` (a caller may not create a
  client in an org they do not belong to). For `GetManyClients`, filter the result set to
  `callerOrgIds` rather than returning all clients.
- **Organization resources** (`OrganizationsService`): owning org = the organization id
  itself. `GetOrganization`, `UpsertOrganization` (update branch), `DisableOrganization`,
  `EnableOrganization` require that id in `callerOrgIds`. `GetOrganizations` filters to
  `callerOrgIds`. For `UpsertOrganization` **create** branch (new org), there is no owning
  org yet — decide and document: either disallow org creation through this path for ordinary
  callers, or, on create, link the new org to the caller's `OrganizationUser`. Recommend
  linking the creating user as a member in the same transaction so they retain access.
- **Organization-user resources** (`OrganizationUsersService.GetOrganizationUsers`): owning
  org = `request.OrganizationId`; require it in `callerOrgIds`.

---

## 3. Fix plan

Three pieces, sequenced **(a) → (b) → (c-check) → wire RPCs**. (a) and (b) are the core;
(c) is a verification/guard because of the encryption attribute, not a large build (see 3c).

### (a) Claims-reading convention — resolve the caller Guid

There is currently **no** code anywhere that reads claims (grep for `ClaimTypes` /
`FindFirst` / `NameIdentifier` in `MicroZen.Api` returns nothing). Establish one convention
and use it everywhere.

- Add a small service, e.g. `ICallerIdentity` with `Guid GetCallerId(ServerCallContext
context)`, implemented over `context.GetHttpContext().User`. Register it in `Program.cs`
  DI (scoped).
- The Cognito JWT handler clears the default inbound claim map
  (`JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear()` in
  `CognitoAuthServiceProvider`), so claims arrive under their raw names. Resolve the subject
  from the raw `sub` claim (Cognito user sub). Decide and document in one place which claim
  carries the Guid that matches `OrganizationUser.UserId` (`sub`), and parse it with
  `Guid.TryParse`. If the value is missing or unparsable, fail closed with
  `Unauthenticated` / `PermissionDenied`.
- This also unblocks the `// TODO: Assign ...DeletedBy / ModifiedBy from User ID claims`
  comments; populate those audit fields from `GetCallerId` where present.

### (b) Resource / permission policy mechanism

The `[Policy(typeof(T), Permission.X)]` attribute the TODOs describe **does not exist** —
there is no such attribute type, no `Permission` enum, and no resource-policy infrastructure
in the repo. Build an equivalent. gRPC method-level `[Authorize(Policy=...)]` cannot by
itself see the request body (the resource id), so a pure declarative ASP.NET policy is
insufficient for per-resource ownership. Recommended approach:

- Introduce a `Permission` enum (`Read, Create, Update, Delete, Manage`) and a
  `TenantAuthorizer` service with methods like
  `Task EnsureClientAccessAsync(int clientId, Permission p, ServerCallContext ctx)`,
  `EnsureOrganizationAccessAsync(int orgId, ...)`,
  `EnsureCreateInOrganizationAsync(int orgId, ...)`, plus an
  `IQueryable<T> ScopeToCaller<T>(...)` helper for the list endpoints. Each method resolves
  `callerOrgIds` (§2.2), resolves the resource's owning org (§2.3), and throws
  `RpcException(PermissionDenied)` when the caller is not a member.
- Call it at the top of each RPC (keeping the existing `[Authorize]` from #47 as the coarse
  gate). This is the minimal mechanism that enforces ownership against the request body.
- Optionally mirror the TODO shape with a real `[Policy(typeof(Client), Permission.Read)]`
  attribute that defers to the same `TenantAuthorizer` via a gRPC interceptor, but the
  interceptor still needs the resource id from the request — so the per-RPC service-method
  call is the dependable path and the attribute is sugar on top. Recommend shipping the
  service-method calls first; the attribute is optional polish.

### (c) Encryption-aware lookup path for `OrganizationUser.UserId`

Verified facts about the encryption layer:

- `ModelBuilderExtension.UseEncryption` attaches `EncryptionConverter` **only to `string`
  properties** (`if (property.ClrType != typeof(string) ... continue;`). `UserId` is a
  `Guid`, so despite its `[EncryptColumn]` attribute the converter is **not** applied — the
  `UserId` column is stored in the clear and the §2.2 equality query works directly, with no
  decryption needed. This is the lookup path to use.
- `EncryptionProvider` uses AES with a **fixed all-zero IV**, making encryption
  deterministic: equal plaintext → equal ciphertext. So equality lookups on the _string_
  encrypted columns (`Email`, names) are technically possible by encrypting the search term,
  but that is weak crypto and not needed here.
- Therefore: resolve the caller by `UserId` (Guid, unencrypted) — **do not** key tenant
  lookups off `Email` or other encrypted string columns. If a future change moves `UserId`
  to an encrypted string, this lookup breaks; call that out so it is not silently regressed.
- Dependency: the fixed-IV / encrypted-column weakness is the subject of #45/#46. Because
  this spec's lookup uses the unencrypted `Guid` `UserId`, #79 is **not functionally blocked**
  by #45/#46 for correctness. But if #45/#46 change how encrypted columns round-trip or the
  `EncryptColumn`/converter behavior (e.g. start encrypting `UserId`), coordinate: land them
  first and retest, since any change to deterministic encryption or the converter's type
  filter would affect the lookup path assumptions above.

---

## 4. Test plan

Build on the integration harness #47 introduces
(`WebApplicationFactory<Program>`, EF Core InMemory, self-contained `EncryptionKey`). Seed
two organizations (A and B), an `OrganizationUser` in A only, and a client owned by each.
Issue test bearer tokens whose subject matches the seeded `OrganizationUser.UserId`.

Cross-tenant denial (caller is a member of org A only):

- `GetClient` / `UpdateClient` / `DeleteClient` / `AuthorizeClients` / `DisallowClients` /
  `GetAllowedOAuthClientCredentials` on **org B's** client → `PermissionDenied`.
- `CreateClient` with `OrganizationId` = **B** → `PermissionDenied`.
- `GetManyClients` → returns only org A's clients (org B's are filtered out).
- `GetOrganization(B)` / `UpsertOrganization(B)` / `DisableOrganization(B)` /
  `EnableOrganization(B)` → `PermissionDenied`.
- `GetOrganizations` → only org A.
- `GetOrganizationUsers(OrganizationId = B)` → `PermissionDenied`.

Owning-org access (same caller, org A resources):

- The same RPCs against org A's resources succeed (or return a legitimate business result
  such as `NotFound` for a missing id), proving the check does not over-block.

Identity edge cases:

- Token with missing/unparsable `sub` → fails closed (`PermissionDenied`/`Unauthenticated`).
- Caller Guid not present in any `OrganizationUser` → empty org set → all resource access
  denied.

Full CI gate (from `AGENTS.md`), via `scripts/check.sh`, in order:

- `npx nx format:check`
- `npx nx affected -t lint test build`
- `dotnet test core/csharp/MicroZen.Core.sln`

New tests pass; existing unit and #47 auth tests still pass.

---

## 5. Risk / blast radius

- **Widest change of the set.** Touches every RPC in all three services plus new DI
  (`ICallerIdentity`, `TenantAuthorizer`) and new claims/authorization plumbing. A mistake
  here can either over-block (legitimate callers get `PermissionDenied`) or under-block
  (tenant leak persists) — both are serious, so the cross-tenant tests in §4 are the gate.
- **Depends on #47 landing first.** This spec assumes `[Authorize]` is present and the auth
  middleware is actually enforced (the #47 secondary finding). Without #47, there is no
  authenticated principal to read a `sub` claim from, and this work is inert.
- **Coordinate with #45/#46 (encryption).** The tenant lookup keys off the unencrypted
  `Guid` `UserId`, so correctness is not blocked. But #45/#46 change encryption behavior;
  if they alter the `EncryptColumn` converter, the deterministic-IV scheme, or move `UserId`
  to an encrypted string, re-verify §3c. Land #45/#46 first where they touch the same
  converter/column behavior.
- **Behavior change for existing callers.** Any authenticated client that previously relied
  on reaching another org's data (e.g. an admin/service token used broadly) will now be
  denied. Identify service-to-service callers — notably the `ClientsGrpcService` gRPC client
  registered in `MicroZen.OAuth2/Initialization.cs`, which adds an `X-API-Key` credential;
  confirm its calls either carry a principal in an org or are exempted deliberately.
- **`UpsertOrganization` create branch** is a design decision point (§2.3): the chosen rule
  (disallow vs auto-link creator) changes who can create orgs. Document and test whichever is
  chosen.
- **Existence disclosure:** decide `PermissionDenied` vs `NotFound` for cross-tenant ids and
  apply uniformly, so the API does not leak which ids exist in other tenants.

---

## 6. Acceptance

- A claims-reading convention (`ICallerIdentity` or equivalent) resolves the caller Guid
  from the token and is used everywhere a caller identity is needed; audit fields
  (`DeletedBy`/`ModifiedBy`) populated where the matching TODOs sit.
- A resource/permission mechanism (the `TenantAuthorizer` service + `Permission` enum, the
  real equivalent of the TODO `[Policy(typeof(T), Permission.X)]`) enforces per-resource
  ownership on every RPC in `ClientsGrpcService`, `OrganizationsService`, and
  `OrganizationUsersService`; list endpoints are scoped to the caller's org set.
- Lookups key off the unencrypted `Guid` `OrganizationUser.UserId`; no tenant decision
  depends on an encrypted string column.
- Integration tests prove a caller in org A cannot read/modify org B's clients,
  organizations, or users, and that the owning-org caller still can; identity edge cases
  fail closed.
- Local gate passes: `npx nx format:check`, `npx nx affected -t lint test build`,
  `dotnet test core/csharp/MicroZen.Core.sln` (`scripts/check.sh`).
- The in-code `// TODO - Add [Policy(...)]` comments are resolved (replaced by the real
  enforcement) rather than left dangling.
- PR opened into `epic/must-have-fixes` with all CI checks green, stacked on / after #47.

```

```
