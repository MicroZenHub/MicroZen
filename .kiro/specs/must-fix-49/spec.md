# Must-Fix #49 — UpsertOrganization forces `Id = -1` on every new organization

GitHub issue: MicroZenHub/MicroZen #49 (Must Fix, P0)
File: `core/csharp/MicroZen.Api/Services/OrganizationsService.cs`

## 1. Problem

Confirmed against the real code. In `UpsertOrganization`, the create branch
(when no Organization matches `request.Id`) builds the new entity like this:

```csharp
var organization = new Organization(request)
{
    Id = -1,
    Name = request.Name,
    CreatedOn = DateTime.UtcNow,
    ModifiedOn = DateTime.UtcNow
};
await db.Organizations.AddAsync(organization);
await db.SaveChangesAsync();
return organization.ToMessage();
```

`Organization.Id` is an `int` primary key (`builder.HasKey(o => o.Id)` in
`OrganizationConfig`) with no `ValueGeneratedNever`, so EF Core treats it as a
store-generated identity key. Forcing `Id = -1` overrides that generated key.

- The first create inserts a row with `Id = -1`.
- The second create also sets `Id = -1`, so the insert hits the primary-key
  uniqueness violation. Only one organization can ever be created this way.
- It also throws away the auto-generated key the database would assign.

The create branch also re-sets `Name = request.Name`, which is redundant: the
`Organization(OrganizationMessage)` constructor already copies `Name`,
`Description`, `AvatarUrl`, and `WebsiteUrl` from the request.

## 2. Fix

In the create branch of `UpsertOrganization`, remove the `Id = -1` line so the
database assigns the key. Also drop the redundant `Name = request.Name` because
the constructor already copies it. Keep `CreatedOn` / `ModifiedOn`.

Change the initializer to:

```csharp
var organization = new Organization(request)
{
    CreatedOn = DateTime.UtcNow,
    ModifiedOn = DateTime.UtcNow
};
await db.Organizations.AddAsync(organization);
await db.SaveChangesAsync();
return organization.ToMessage();
```

Constructor confirmation — `Organization(OrganizationMessage message)` copies
the other fields:

```csharp
public Organization(OrganizationMessage message)
{
    Name = message.Name;
    Description = message.Description.Length > 0 ? message.Description : null;
    AvatarUrl = message.AvatarUrl.Length > 0 ? message.AvatarUrl : null;
    WebsiteUrl = message.WebsiteUrl.Length > 0 ? message.WebsiteUrl : null;
}
```

So after the fix the new entity still has Name/Description/AvatarUrl/WebsiteUrl
set, plus CreatedOn/ModifiedOn, and the DB supplies `Id`.

Style notes (match existing code): tab indentation for `*.cs`, keep the existing
`if`/`else` shape, do not touch the update branch.

## 3. Test plan

Add an xUnit test that creates TWO organizations through `UpsertOrganization`
and confirms both succeed with distinct database-assigned ids. Tests use the EF
Core InMemory provider and set their own `EncryptionKey` (self-contained, no
PostgreSQL), following the existing test pattern in the solution.

The test should assert:

1. First `UpsertOrganization` returns a message whose `Id` is the generated key
   (not `-1`).
2. Second `UpsertOrganization` with a different name succeeds (no PK violation).
3. The two returned ids differ, and `db.Organizations` holds two rows.
4. Name/Description/AvatarUrl/WebsiteUrl on each returned message match input.

Note: the EF InMemory provider does not enforce real identity generation the
same way PostgreSQL does, so the regression (duplicate `Id = -1` collision) is
best reproduced by asserting the two created organizations receive distinct ids
and both persist. After the fix the entities carry no caller-forced id, so EF
assigns distinct keys.

Place the test alongside the existing Organizations service tests in the test
project. If no Organizations service test class exists yet, add one following
the nearest existing service-test convention.

Full gate must pass, run in order (see `AGENTS.md`):

- `npx nx format:check`
- `npx nx affected -t lint test build`
- `dotnet test core/csharp/MicroZen.Core.sln`

`./scripts/check.sh` runs all three in order.

## 4. Risk / blast radius

- Small, localized change to one initializer in the create branch; the update
  branch is untouched.
- Return value: callers get `organization.ToMessage()`, whose `Id` is now the
  real database key instead of `-1`. Any client that previously relied on the
  sentinel `-1` coming back from a create would now get a valid id — this is the
  intended fix, not a regression. No code in the repo should depend on the `-1`
  sentinel; confirm during implementation with a search for `-1` usage tied to
  organization ids.
- No schema or migration change: the `Id` column is already a key with default
  store-generated behavior.
- No gRPC contract change: `OrganizationMessage.Id` stays an `int`.

## 5. Acceptance

- A pull request targeting `epic/must-have-fixes` contains the fix and the new
  test.
- The PR has green CI checks (the `main` Nx job and the `dotnet` job both pass).
- Two organizations can be created in sequence through `UpsertOrganization`
  without a primary-key violation, each with a distinct database-assigned id.
