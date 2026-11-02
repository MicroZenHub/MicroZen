# Must-Fix #46 — Fixed all-zero IV / weak key derivation

GitHub issue: [MicroZenHub/MicroZen#46](https://github.com/MicroZenHub/MicroZen/issues/46) (Must Fix, P0)

Target file: `core/csharp/MicroZen.Api/Security/Encryption/Utils/EncryptionProvider.cs`

## 1. Problem (confirmed against actual code)

`EncryptionProvider` encrypts every `[EncryptColumn]` string via an EF Core
`ValueConverter` (`EncryptionConverter` → `ModelBuilderExtension.UseEncryption`).
Two concrete defects in `EncryptionProvider.cs`:

**Fixed all-zero IV.** `Encrypt` (line 44) and `Decrypt` (line 70) both build the
IV the same way:

```csharp
var iv = new byte[16];
```

A `new byte[16]` in C# is all zeros, and it is never changed before
`aes.IV = iv;`. With a constant key and a constant IV, AES-CBC is deterministic:
the same plaintext always yields the same ciphertext. An attacker who can read
the database can tell which clients share an OAuth secret and build a
plaintext→ciphertext dictionary. This is not real encryption.

**Raw-bytes key, no validation.** The key is used as the raw UTF-8 bytes of the
configured string, in both methods:

```csharp
aes.Key = Encoding.UTF8.GetBytes(key);
```

`Aes.Create()` requires a key of exactly 16, 24, or 32 bytes. Nothing validates
the configured value, so a key of any other length throws at the first
encrypt/decrypt. The test fixtures use `"1234567890"` (10 bytes) — proof the
current value is not even a legal AES key and only works because tests do not
exercise a real round trip through a populated AES key here.

**Doc claim.** `README.md:17` advertises "AES-512 encrypting". AES has no 512-bit
variant (max 256). The README line is wrong and should be corrected to AES-256.

## 2. Fix

All changes are inside `EncryptionProvider.cs`. Keep tab indentation and the
existing primary-constructor `EncryptionProvider(string key)` shape.

**a. Derive the AES key with a KDF.** Replace `Encoding.UTF8.GetBytes(key)` with
a SHA-256 hash of the configured value, giving a fixed 32-byte (AES-256) key
regardless of the configured string length. Compute it once (e.g. a `private`
readonly field or a small helper) and reuse in both methods:

```csharp
private byte[] DeriveKey() => SHA256.HashData(Encoding.UTF8.GetBytes(key));
```

This also removes the length-validation problem: any non-empty configured string
now produces a legal key. Keep the existing empty-key guard
(`string.IsNullOrEmpty(key)` → `ArgumentNullException`).

**b. Random per-operation IV, prepended to ciphertext.** In `Encrypt`, generate a
fresh 16-byte IV per call and write it to the front of the output buffer before
the ciphertext, then base64 the whole thing:

```csharp
using var aes = Aes.Create();
aes.Key = DeriveKey();
aes.GenerateIV();
var iv = aes.IV;

using var memoryStream = new MemoryStream();
memoryStream.Write(iv, 0, iv.Length); // prepend IV
var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
using (var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
using (var streamWriter = new StreamWriter(cryptoStream))
{
	streamWriter.Write(dataToEncrypt);
}
return Convert.ToBase64String(memoryStream.ToArray());
```

In `Decrypt`, read the IV back off the front of the buffer, then decrypt the
remainder:

```csharp
var buffer = Convert.FromBase64String(dataToDecrypt);
if (buffer.Length < 16)
	throw new FormatException("Ciphertext is too short to contain an IV.");

using var aes = Aes.Create();
aes.Key = DeriveKey();
var iv = new byte[16];
Array.Copy(buffer, 0, iv, 0, 16);
aes.IV = iv;

var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
using var memoryStream = new MemoryStream(buffer, 16, buffer.Length - 16);
using var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read);
using var streamReader = new StreamReader(cryptoStream);
return streamReader.ReadToEnd();
```

Preserve the existing `string.IsNullOrEmpty(dataToEncrypt/dataToDecrypt)` →
`string.Empty` short-circuits.

**c. Input validation.** Keep the empty-key `ArgumentNullException`. Add the
short-ciphertext `FormatException` guard shown above so a truncated or
pre-IV-format value fails loudly instead of throwing an opaque crypto error.

**Wire-format change (breaking).** Old stored values are raw ciphertext with no
prepended IV and were encrypted with the raw-bytes key. New `Decrypt` expects
`[16-byte IV][ciphertext]` and a SHA-256-derived key. **Old ciphertext will not
decrypt under the new code** — both the key and the layout changed. See Risk for
how to handle existing data.

## 3. Test plan

Add a unit test class for `EncryptionProvider` (e.g.
`MicroZen.Api.Test/Security/Encryption/EncryptionProviderTests.cs`, xUnit,
matching the existing test style). Cover:

1. **Non-deterministic ciphertext.** Encrypt the same plaintext twice with the
   same provider/key; assert the two base64 results are **not equal** (random IV
   per op).
2. **Round trip.** `Decrypt(Encrypt(x)) == x` for a representative string
   (including non-ASCII and empty-string → empty-string).
3. **Key length independence.** A short configured key (e.g. `"1234567890"`) and
   a long one both encrypt/decrypt without throwing (KDF normalizes length).
4. **Malformed input.** A base64 string shorter than 16 bytes throws
   `FormatException`.

Gate (from `AGENTS.md`), all must pass:

```
npx nx format:check
npx nx affected -t lint test build
dotnet test core/csharp/MicroZen.Core.sln
```

`scripts/check.sh` runs all three in order. .NET tests use the EF Core InMemory
provider and set their own `EncryptionKey`, so no PostgreSQL is needed.

## 4. Risk / blast radius

- **Data migration.** Encrypted columns are `OAuth2ClientCredentials`
  (`OAuth2ClientId`, `OAuth2ClientSecret`, `AllowedScopes`) and `OrganizationUser`
  (`UserId`, `FirstName`, `LastName`, `Email`). Any rows written under the old
  scheme become undecryptable after this change. For a pre-production / dev DB the
  practical path is a reset + re-seed (`scripts/seed-db.sh --reset`). If any
  environment holds real data, a one-time migration must read each
  `[EncryptColumn]` value with the **old** logic (raw-bytes key, zero IV) and
  rewrite it with the **new** logic before deploying; otherwise reads fail.
  Confirm with the owner whether any non-dev environment has live encrypted rows.
- **Interaction with #45 (same area).** Issue
  [#45](https://github.com/MicroZenHub/MicroZen/issues/45) fixes an **inverted**
  marker check in `ModelBuilderExtension.cs`
  (`if (attributes?.Length != 0)` encrypts the wrong columns): today the three
  `[EncryptColumn]` OAuth fields are stored in **plaintext** and unmarked fields
  are encrypted. Sequencing matters:
  - If #45 lands first, the correct columns start getting encrypted under the
    still-broken zero-IV scheme, then #46 changes the scheme again — two format
    changes over the same data.
  - Prefer landing **#45 and #46 together** (or #46 first, then #45) so encrypted
    columns are only ever written once under the final correct scheme, minimizing
    migration passes. Call this out in the PR.
- **Key stability.** The SHA-256 KDF is deterministic for a fixed configured
  value, so rotating the `EncryptionKey` config value re-breaks existing data by
  design (expected — key rotation needs its own migration). No per-value salt is
  introduced, to keep the converter a pure `string → string` with no extra
  column.

## 5. Acceptance

A pull request into `epic/must-have-fixes` that:

- Implements the fix in `EncryptionProvider.cs` (random per-op IV prepended +
  read back, SHA-256 KDF, input validation) and corrects the README AES-512 line.
- Adds the `EncryptionProvider` unit tests above, including the proof that two
  encryptions of the same plaintext differ and the encrypt→decrypt round trip.
- Documents the wire-format break and the existing-data handling in the PR
  description, and notes the #45 sequencing.
- Has **green CI checks** (`main` and `dotnet` jobs) on the PR.
