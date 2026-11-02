# Must-Fix #45 — Encryption inverted (plaintext secrets, garbled fields)

**Issue:** MicroZenHub/MicroZen #45 (Must Fix, P0)
**Target branch for the PR:** `epic/must-have-fixes`

## 1. Problem

Column encryption is applied to the wrong properties. Fields marked
`[EncryptColumn]` (OAuth2 client secret, client ID, scopes) are stored in
**plaintext**, while unmarked string fields are silently encrypted and read back
as unreadable base64.

Root cause is an inverted guard in the EF Core model-building hook.

`core/csharp/MicroZen.Api/Security/Encryption/Extensions/ModelBuilderExtension.cs`,
lines 31–36:

```csharp
if (property.ClrType != typeof(string) || IsDiscriminator(property))
    continue;
var attributes = property.PropertyInfo?.GetCustomAttributes(typeof(EncryptColumnAttribute), false);
if (attributes?.Length != 0)
    property.SetValueConverter(encryptionConverter);
```

The `?.` on `attributes` only guards a `null` `PropertyInfo`, which is already
excluded one line earlier by `IsDiscriminator`. So in practice `attributes` is
never `null` here, and `attributes?.Length != 0` reduces to `Length != 0`.

The intent is "attach the `EncryptionConverter` only to `[EncryptColumn]`
properties." But the issue reports the opposite result in the real database —
marked secrets in plaintext, unmarked fields garbled — which is the behavior of
a predicate that treats the **absence** of the marker as the trigger. The fix is
to make the predicate an explicit, unambiguous "marker is present" test rather
than relying on the `Length != 0` form, which is easy to misread and is what the
issue's suggested one-line fix replaces.

**Confirmed facts against live code:**

- Only `string` columns are ever considered (line 32); the `[EncryptColumn]` on
  `OrganizationUser.UserId` (a `Guid`) is already a no-op.
- `[EncryptColumn]` string properties in the model:
  - `Entities/AuthCredentials/OAuth2ClientCredentials.cs`: `OAuth2ClientId`,
    `OAuth2ClientSecret`, `AllowedScopes`
  - `Entities/OrganizationUser.cs`: `FirstName`, `LastName`, `Email`
- The converter (`Converters/EncryptionConverter.cs`) wraps
  `IEncryptionProvider.Encrypt`/`Decrypt` (AES, `Utils/EncryptionProvider.cs`).
- The hook is invoked once from `Context/MicroZenContext.cs`
  (`OnModelCreating` → `modelBuilder.UseEncryption(...)`).

## 2. Fix

Make the predicate an unambiguous "encrypt only when the marker is present"
test, per the issue's suggested fix.

**File:** `core/csharp/MicroZen.Api/Security/Encryption/Extensions/ModelBuilderExtension.cs`, line 35.

Before:

```csharp
				var attributes = property.PropertyInfo?.GetCustomAttributes(typeof(EncryptColumnAttribute), false);
				if (attributes?.Length != 0)
					property.SetValueConverter(encryptionConverter);
```

After:

```csharp
				var attributes = property.PropertyInfo?.GetCustomAttributes(typeof(EncryptColumnAttribute), false);
				if (attributes is { Length: > 0 })
					property.SetValueConverter(encryptionConverter);
```

One line changes. `attributes is { Length: > 0 }` is `true` only when the array
is non-null **and** holds at least one `EncryptColumnAttribute` — i.e. the
property carries the marker. Unmarked properties (empty array) and the excluded
`null` case both fall through and stay unencrypted.

Keep the file's existing style: tab indentation (`*.cs` uses tabs,
`indent_size = 4` per `.editorconfig`), no other edits.

## 3. Test plan

### Unit test (new)

**Important provider constraint:** the test project references only
`Microsoft.EntityFrameworkCore.InMemory`, and the InMemory provider **ignores
value converters**. A save/read round-trip therefore cannot prove encryption. The
test must assert the mapping decision directly — which properties have the
`EncryptionConverter` attached after `UseEncryption` runs.

Add `MicroZen.Api.Test/Security/Encryption/Extensions/ModelBuilderExtensionTests.cs`
(the empty `Security/Encryption/Extensions/` folder already exists in the test
project). Match `Context/MicroZenContextTests.cs` conventions: xUnit, setup in
the constructor, `EncryptionKey` env var set, `IDisposable` teardown,
GIVEN/WHEN/THEN comments.

Assertions over the built model (`MicroZenContext.Model` with the InMemory
options, or a minimal `ModelBuilder` with `UseEncryption` applied):

1. **Marked string columns get the converter.** For `OAuth2ClientCredentials`
   (`OAuth2ClientId`, `OAuth2ClientSecret`, `AllowedScopes`) and
   `OrganizationUser` (`FirstName`, `LastName`, `Email`), the property's
   `GetValueConverter()` is a non-null `EncryptionConverter`.
2. **Unmarked string columns do not.** For a string column with no
   `[EncryptColumn]` (e.g. `Organization.Name`, `Organization.WebsiteUrl`),
   `GetValueConverter()` is `null`.
3. **Non-string columns are untouched.** A key/int column
   (e.g. `Organization.Id`) has no converter.

A regression-style check: assert the *count* of converted properties equals the
number of `[EncryptColumn]`-marked string properties, so a future inversion fails
immediately.

### Full gate (CI parity)

Run the repo gate locally, in order (per `AGENTS.md`):

```bash
npx nx format:check
npx nx affected -t lint test build
dotnet test core/csharp/MicroZen.Core.sln
```

`scripts/check.sh` runs all three and exits on first failure. The .NET tests use
the EF Core InMemory provider and set their own `EncryptionKey`, so no Postgres
is required. CI (`.github/workflows/ci.yml`) runs the same two jobs (`main` via
Nx, `dotnet` via `dotnet test`) and is the authoritative gate.

## 4. Risk / blast radius

- **Single call site.** `UseEncryption` is invoked only from
  `MicroZenContext.OnModelCreating`. The change affects which columns get the
  `EncryptionConverter` for the whole context.
- **This fix changes which columns are encrypted at rest.** After the fix:
  - Marked columns (`OAuth2ClientId/Secret`, `AllowedScopes`, user
    `FirstName/LastName/Email`) will be **written encrypted** going forward.
  - Previously-unmarked columns stop being encrypted.
- **Data-migration concern (must be called out in the PR).** Any data already in
  a real Postgres database was written under the *old* (inverted) behavior:
  - Marked columns were stored **plaintext** → after the fix, EF will try to
    `Decrypt` them on read. Decrypting plaintext throws / yields garbage
    (`Convert.FromBase64String` or AES failure).
  - Unmarked columns were stored **encrypted (base64)** → after the fix they are
    read as-is and surface the base64 blob.
  - **Resolution for any non-empty environment:** a one-time data migration is
    required — decrypt the previously-encrypted unmarked columns back to
    plaintext and encrypt the previously-plaintext marked columns, using the same
    `EncryptionKey`. This spec does not implement that migration; it must be
    tracked as a follow-up before deploying to any environment holding real data.
  - **Dev/test impact is nil:** seeded dev data is placeholder and reset via
    `scripts/seed-db.sh --reset`; tests use a fresh InMemory DB each run.
- **No API/proto surface change.** gRPC contracts and entity shapes are
  unchanged; only persistence-layer value conversion is corrected.

## 5. Acceptance

- A PR into `epic/must-have-fixes` contains the one-line fix in
  `ModelBuilderExtension.cs` and the new encryption-mapping unit test.
- CI on the PR is green: both the `main` (Nx `format:check` +
  `affected -t lint test build`) and `dotnet` (`dotnet test
  core/csharp/MicroZen.Core.sln`) jobs pass.
- The new unit test proves: marked string columns get the `EncryptionConverter`,
  unmarked columns and non-string columns do not.
- The PR description calls out the data-migration concern from §4 for any
  environment holding real data.
