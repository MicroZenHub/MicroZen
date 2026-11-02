# Must-Fix #48 — Expired/deleted API keys still match

**Issue:** #48 (Must Fix, P0)
**Repo:** MicroZenHub/MicroZen
**Target branch:** `epic/must-have-fixes`

## 1. Problem

`GetClientIdFromApiKey` and the two API-key mutation lookups match a key by its
value alone. They apply neither the `ExpiresOn` nor the `DeletedOn` guard that
`APIKeyValidator.IsValidAPIKey` applies. So a revoked or expired key still
resolves to its client, and a disabled key can be re-enabled.

Confirmed against the actual code.

### Current queries (the bug)

`MicroZen.Api/Services/ClientsGrpcService.cs` — `GetClientIdFromApiKey`:

```csharp
var clientId = (await db.ClientAPIKeys
	.Select(clientApiKey => new { clientApiKey.ClientId, clientApiKey.ApiKey })
	.FirstOrDefaultAsync(x => x.ApiKey == request.Value))?.ClientId;
return new Int32Value() { Value = clientId ?? 0 };
```

`MicroZen.Api/Services/ClientApiKeysService.cs` — `UpdateClientApiKey`:

```csharp
var existingClientAPIKey = await db.ClientAPIKeys.FirstOrDefaultAsync(x => x.ApiKey == request.ApiKey);
```

`MicroZen.Api/Services/ClientApiKeysService.cs` — `DisableClientApiKey`:

```csharp
var existingClientAPIKey = await db.ClientAPIKeys.FirstOrDefaultAsync(x => x.ApiKey == request.ApiKey);
```

None filter on `ExpiresOn` or `DeletedOn`.

### The correct predicate (to copy)

`MicroZen.Api/Security/APIKeys/Validate/APIKeyValidator.cs` — `IsValidAPIKey`:

```csharp
return await context.ClientAPIKeys.FirstOrDefaultAsync(clientAPIKey =>
	clientAPIKey.ApiKey == apiKey &&
	(
		clientAPIKey.ExpiresOn == null ||
		clientAPIKey.ExpiresOn > DateTime.UtcNow
	) && clientAPIKey.DeletedOn == null) != null;
```

### Behavior that complicates a blanket fix

- `DisableClientApiKey` sets `ExpiresOn = UtcNow` and `DeletedOn = UtcNow`. The
  issue's "re-enable" path is: call `UpdateClientApiKey` on that same key value
  with a future `ExpiresOn`. Because the update lookup has no `DeletedOn` guard,
  it finds the disabled key and clears its expiry — re-enabling a key that was
  revoked. The fix must make `UpdateClientApiKey` refuse to find a deleted key.
- `GetClientIdFromApiKey` is the resolve path and must match the full validity
  predicate (both expiry and deletion), same as `IsValidAPIKey`.

## 2. Fix

Apply the `IsValidAPIKey` validity predicate to all three lookups so only a
live, non-expired, non-deleted key resolves or can be mutated.

The validity condition, applied per lookup:

```csharp
clientAPIKey.ApiKey == request.Value &&      // or request.ApiKey
(clientAPIKey.ExpiresOn == null || clientAPIKey.ExpiresOn > DateTime.UtcNow) &&
clientAPIKey.DeletedOn == null
```

### Recommended approach: shared query helper (not a global query filter)

Add one `IQueryable<ClientAPIKey>` extension that encapsulates the validity
predicate, and use it in all three lookups plus `IsValidAPIKey`. For example a
static `ClientAPIKeyQueryExtensions.WhereValid(this IQueryable<ClientAPIKey>)`
in `MicroZen.Api/Security/APIKeys` (or `Entities`), returning the source filtered
by the expiry + `DeletedOn == null` condition. Each call site then reads:

```csharp
await db.ClientAPIKeys.WhereValid().FirstOrDefaultAsync(x => x.ApiKey == request.Value)
```

**Why the helper over an EF global query filter:**

- A global query filter on `ClientAPIKey` is implicit and applies to **every**
  query on the entity, including `RequestClientApiKey` reads and any future
  reporting/admin query. It is easy to forget it is there.
- `DisableClientApiKey` genuinely needs to read keys that are not yet deleted but
  may be expired, and an admin/audit flow may legitimately need to see deleted
  keys. A global filter forces scattered `IgnoreQueryFilters()` calls at those
  sites, which is error-prone and inverts the safe default (you must remember to
  re-include the guard mentally everywhere).
- A helper is explicit at each call site, matches the existing style where the
  predicate is written inline, and keeps the single source of truth without
  hidden global behavior.

Keep `IsValidAPIKey` as the behavioral reference and, ideally, route it through
the same helper so the three lookups and the validator cannot drift again.

### Call-site changes

- `GetClientIdFromApiKey`: filter the `ClientAPIKeys` query with the helper
  before `FirstOrDefaultAsync(x => x.ApiKey == request.Value)`.
- `UpdateClientApiKey`: filter with the helper before
  `FirstOrDefaultAsync(x => x.ApiKey == request.ApiKey)`. A deleted/expired key
  then yields `null`, so the existing `NotFound` path fires and re-enable is
  blocked.
- `DisableClientApiKey`: filter with the helper before the lookup, so disabling
  an already-deleted/expired key returns `NotFound` rather than silently
  re-stamping `DeletedOn`.

Match existing code style: tabs (`*.cs` uses tab indent, `indent_size = 4`),
expression-bodied members where the current code uses them, inline predicate
shape consistent with `IsValidAPIKey`.

## 3. Test plan

Add xUnit tests following the existing `APIKeyValidatorTests` pattern (InMemory
provider, `EncryptionKey` env var set in the ctor, GIVEN/WHEN/THEN comments).
Place service tests alongside the existing service/security tests.

`GetClientIdFromApiKey` (ClientsGrpcService):

- expired key → returns `0` (no client resolved).
- deleted key (`DeletedOn` set) → returns `0`.
- valid key → returns the real `ClientId`.

`UpdateClientApiKey` (ClientApiKeysService):

- deleted key → throws `RpcException` `NotFound` (re-enable blocked).
- expired key → throws `RpcException` `NotFound`.
- valid key → updates `ExpiresOn` and returns the key.

`DisableClientApiKey` (ClientApiKeysService):

- already-deleted key → throws `RpcException` `NotFound`.
- valid key → sets `DeletedOn`/`ExpiresOn` and returns the key.

Regression:

- Keep the existing `APIKeyValidatorTests` green (valid, expired, deleted,
  bad-hash, bad-prefix, not-in-db cases) — proves the shared helper preserves
  current validator behavior.

Gate (per `AGENTS.md`), all must pass:

- `npx nx format:check`
- `npx nx affected -t lint test build`
- `dotnet test core/csharp/MicroZen.Core.sln`

`scripts/check.sh` runs all three in order.

## 4. Risk / blast radius

- **Shared helper (recommended):** blast radius is the three touched lookups plus
  the validator if routed through it. Explicit and local; no query behaves
  differently unless it opts in by calling the helper. Low risk.
- **EF global query filter (alternative):** affects **every** query on
  `ClientAPIKey` across the whole app, present and future, including
  `RequestClientApiKey` and any admin/audit read. Call sites that must see
  expired or deleted keys would need `IgnoreQueryFilters()`; forgetting one
  flips behavior silently. Higher, repo-wide blast radius — this is the main
  reason the helper is preferred.
- Behavioral change to callers of `GetClientIdFromApiKey`: a previously-resolving
  expired/deleted key now resolves to `0`. This is the intended security fix, but
  any downstream caller treating `0` as a valid client id should be checked.
- No schema or migration change: `ExpiresOn`/`DeletedOn` columns already exist.

## 5. Acceptance

- Spec file exists at `.kiro/specs/must-fix-48/spec.md`.
- Implementation PR into `epic/must-have-fixes` has green CI checks (the
  `main` Nx job and the `dotnet` job both pass), covering format, lint, build,
  and the full test suite including the new tests above.
