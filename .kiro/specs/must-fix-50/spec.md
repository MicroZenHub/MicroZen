# Spec: Must Fix #50 — Client search filters whole request object

**Issue:** GitHub MicroZenHub/MicroZen #50 (Must Fix, P0)
**File:** `core/csharp/MicroZen.Api/Services/ClientsGrpcService.cs`
**Method:** `GetManyClients(ManyClientsRequest request, ServerCallContext context)`

## 1. Problem

Confirmed against the real code. The name filter interpolates the entire
request message, not the search term:

```csharp
if (request.SearchTerm is not null)
{
	predicate.And(c =>
		EF.Functions.ILike(c.Name, $"%{request}%"));
}
```

`$"%{request}%"` calls `request.ToString()` on the whole
`ManyClientsRequest` message. That produces the protobuf debug string of the
message, so `ILike` compares every client name against that garbage string.
No real client name matches it, so every search returns nothing.

The field is defined in `core/csharp/MicroZen.Api/Protos/client.proto`:

```proto
message ManyClientsRequest {
  ClientType type = 1;
  // The search term to filter for Clients by
  google.protobuf.StringValue searchTerm = 2;
  ...
}
```

`searchTerm` is a `google.protobuf.StringValue` (field 2). The generated C#
property is `request.SearchTerm` of type `string?` (the wrapper maps to a
nullable string). The guard `request.SearchTerm is not null` is already
correct; only the interpolation target is wrong.

## 2. Fix

Interpolate the search-term field instead of the whole message:

```csharp
if (request.SearchTerm is not null)
{
	predicate.And(c =>
		EF.Functions.ILike(c.Name, $"%{request.SearchTerm}%"));
}
```

### Empty / null search term behavior

- **Null** (`request.SearchTerm is null`): wrapper field was not set. The
  existing `is not null` guard skips the predicate, so the filter is not
  applied and **all** clients of the requested type are returned. Keep this
  behavior.
- **Empty string** (`request.SearchTerm == ""`): the guard passes, and
  `ILike(c.Name, "%%")` matches **all** names. So an empty search term also
  returns **all** clients of the type. This is the chosen behavior: empty
  search = no name filtering, same as null. It matches the natural meaning of
  an empty search box and needs no extra code.

Chosen behavior, stated plainly: **a missing or empty search term matches all
clients of the requested type; a non-empty term matches names containing it
(case-insensitive).** The one-line change above delivers this; no extra
empty-string handling is required.

## 3. Test plan

Add xUnit tests for `ClientsGrpcService.GetManyClients` using the EF Core
InMemory provider (the suite already uses InMemory and sets its own
`EncryptionKey`, so tests are self-contained and need no PostgreSQL). Match the
existing test style and project layout under `core/csharp`.

Seed clients of the same `ClientType` with distinct names, e.g.
`"Acme Portal"`, `"Acme Admin"`, `"Beta Service"`.

1. **Matching term returns the right clients.** Request with
   `SearchTerm = "Acme"` returns exactly the two `Acme*` clients and not
   `"Beta Service"`.
2. **Case-insensitive match.** `SearchTerm = "acme"` returns the same two
   clients (confirms `ILike` case-insensitivity).
3. **Non-matching term returns none.** `SearchTerm = "Zzz"` returns zero
   clients.
4. **Null search term returns all.** `SearchTerm` unset returns all clients of
   the type (guards against a regression that would always filter).

Verification gate (mirror CI, in order):

- `npx nx format:check`
- `npx nx affected -t lint test build`
- `dotnet test core/csharp/MicroZen.Core.sln`

`scripts/check.sh` runs all three in order. The build and full test suite must
pass.

## 4. Risk / blast radius

- **One-line behavioral change** in a single method; no signature, proto, or
  schema change. Low risk.
- **Same file has paging issues tracked separately in #51.** Note the existing
  paging bugs in this method (`page` defaults to `10` via `request.Page > 0 ? request.Page : 10`
  but is used as a page _size_ in `Take(page)`; `NextUrl` uses `request.Page + 1`
  with no page default). **This spec does NOT touch paging.** It changes only
  the `ILike` interpolation argument on the search-term filter line.
- **Coordinate with #51** so the two fixes do not conflict: both edit
  `GetManyClients`. Keep this change limited to the single `ILike(...)` line so
  the #51 paging change applies cleanly, and vice versa. If both land close
  together, rebase whichever merges second.

## 5. Acceptance

- `.kiro/specs/must-fix-50/spec.md` exists (this file).
- The PR implementing this fix targets the `epic/must-have-fixes` branch and
  has green CI checks: `npx nx format:check`, `npx nx affected -t lint test build`,
  and `dotnet test core/csharp/MicroZen.Core.sln` all pass, including the new
  search tests above.
