# Must-Fix #51 — GetManyClients paging defaults are wrong and NextUrl double-counts

- **Issue:** #51 (Must Fix, P0)
- **Repo:** MicroZenHub/MicroZen
- **File:** `core/csharp/MicroZen.Api/Services/ClientsGrpcService.cs` (`GetManyClients`, lines ~30–47)
- **Proto:** `core/csharp/MicroZen.Api/Protos/client.proto` (`ManyClientsRequest`, `ManyClientsResponse`)
- **Scope:** paging only. The search-term bug in the same method is tracked by #50 and is **out of scope** here.

## 1. Problem (confirmed against actual code)

The proto defines the request paging fields as (field numbers shown):

```proto
message ManyClientsRequest {
  ClientType type = 1;
  google.protobuf.StringValue searchTerm = 2;
  // How many clients to return. Defaults to 10 if none specified.
  int32 page = 3;
  // How many clients to skip. Defaults to 0 if none specified.
  int32 skip = 4;
}
```

So the generated C# message exposes `request.Page` (int) and `request.Skip` (int). There is **no page-size field**. The response field is `nextUrl` (`google.protobuf.StringValue`), surfaced in C# as `response.NextUrl`.

The current method does this:

```csharp
var page = request.Page > 0 ? request.Page : 10;
var skip = request.Skip > 0 ? request.Skip : 0;
var total = await db.Clients.Where(predicate).CountAsync();
var clients = await db.Clients
    .Include(c => c.OAuth2Credentials)
    .Include(c => c.AllowedClientOAuth2Credentials)
    .Where(predicate)
    .OrderBy(c => c.Name)
    .Skip(skip)
    .Take(page)
    .Select(c => c.ToMessage())
    .ToListAsync();
var anyMoreClients = await db.Clients.Where(predicate).Take(page + 1).Skip(skip).AnyAsync();
if (anyMoreClients)
{
    response.NextUrl = $"clients?page={request.Page + 1}&skip={request.Skip}&searchTerm={request.SearchTerm}&type={request.Type.ToString().ToUpper()}";
    response.Total = total;
}
```

What is wrong:

- **`page` is used as a page SIZE, not a page number.** The local `page` defaults to `10` and is fed straight into `.Take(page)`. So "page" controls how many rows come back, not which window.
- **`skip` is a raw row offset that is never derived from the page.** `.Skip(skip)` uses `request.Skip` directly, so the window offset is decoupled from the page the caller is on.
- **`NextUrl` increments `request.Page` but not `skip`.** It emits `page={request.Page + 1}` while repeating `skip={request.Skip}` unchanged. The next call therefore takes `Take(page+1-ish)` rows from the **same** `skip` offset, so consecutive pages overlap and `Total` is only meaningful when there is a next page.
- **`anyMoreClients` is a no-op guard.** `.Take(page + 1).Skip(skip).AnyAsync()` only asks "does any row exist in this window?", not "is there a row _beyond_ the current page?" — it does not detect the real end of the list.

Net effect: callers paging with the documented `page`/`skip` fields get overlapping and incorrect windows, and `NextUrl` points at a window that re-serves rows already returned.

## 2. Fix

Separate **page number** from **page size**, compute the offset from them, and build `NextUrl` from the same variables.

The proto has no page-size field. **Do not change the proto.** Reuse the two existing fields with clear, documented meanings:

- `request.Page` = **page number**, 1-based. Default to `1` when `<= 0`.
- `request.Skip` = **page size** (rows per page). Default to a constant `DefaultPageSize = 10` when `<= 0`. This keeps the wire contract and field numbers unchanged while giving each field a single, consistent meaning inside the method.

> Rationale for reusing `skip` as page size: it is the only other int field, it already defaults to the same 10 the old code used for the take, and repurposing it avoids a proto change (breaking-change risk, regen, review). The field's proto comment ("How many clients to skip") becomes stale — update the comment text only (no field add/rename/renumber) so the doc matches behaviour. Flag the comment edit in the PR.

Target shape (match existing tab indentation and style):

```csharp
private const int DefaultPageSize = 10;

// ...
var page = request.Page > 0 ? request.Page : 1;          // 1-based page number
var pageSize = request.Skip > 0 ? request.Skip : DefaultPageSize;
var skip = (page - 1) * pageSize;

var total = await db.Clients.Where(predicate).CountAsync();
var clients = await db.Clients
    .Include(c => c.OAuth2Credentials)
    .Include(c => c.AllowedClientOAuth2Credentials)
    .Where(predicate)
    .OrderBy(c => c.Name)
    .Skip(skip)
    .Take(pageSize)
    .Select(c => c.ToMessage())
    .ToListAsync();

// There is a next page only if more rows exist beyond the current window.
var hasMore = skip + clients.Count < total;
response.Total = total;
if (hasMore)
{
    response.NextUrl =
        $"clients?page={page + 1}&skip={pageSize}&searchTerm={request.SearchTerm}&type={request.Type.ToString().ToUpper()}";
}
response.Clients.AddRange(clients);
return response;
```

Key points:

- Offset is now `(page - 1) * pageSize`, so page 1 skips 0 and page 2 skips `pageSize`.
- `.Take(pageSize)` returns a fixed window per page.
- `NextUrl` carries `page={page + 1}` and `skip={pageSize}` (the page size, consistent with the reused-field convention) so the next call lands on a non-overlapping window.
- `hasMore` compares `skip + returned-count` against `total`, so the last page emits no `NextUrl`.
- `Total` is set on every response, not only when there is a next page (the old code only set it inside the guard).
- Leave the `searchTerm` predicate exactly as it is — that bug belongs to #50.

## 3. Test plan

Add a test class (e.g. `Services/ClientsGrpcServiceTests.cs`) in `core/csharp/MicroZen.Api.Test`, matching the reference shape in `Context/MicroZenContextTests.cs`:

- In-memory provider via `DbContextOptionsBuilder<MicroZenContext>().UseInMemoryDatabase(...)` with a unique DB name per fixture.
- Set `EncryptionKey` in the constructor, clear it in `Dispose`.
- Use `static Xunit.Assert` and GIVEN/WHEN/THEN comments.
- Drive the real method: `new ClientsGrpcService(context).GetManyClients(request, context)`. A `ServerCallContext` is required; use a test double (e.g. `TestServerCallContext`) or the method ignores it — pass a minimal stub.

Seed enough clients of one `ClientType` to span multiple pages (e.g. 25 clients with distinct names so `OrderBy(c => c.Name)` is deterministic).

Cases:

1. **Page 1 returns the first window.** `Page=1, Skip=10` → returns the first 10 by name; `Total=25`; `NextUrl` is non-null and contains `page=2` and `skip=10`.
2. **Page 2 is non-overlapping and correct.** `Page=2, Skip=10` → returns clients 11–20 by name; none of them appear in the page-1 result (assert the two sets are disjoint).
3. **Last page emits no NextUrl.** `Page=3, Skip=10` → returns clients 21–25; `NextUrl` is null/empty; `Total=25`.
4. **Defaults applied.** `Page=0, Skip=0` → behaves as page 1 with the default size 10 (10 rows, offset 0).
5. **Total is always set** regardless of whether a next page exists.

Gate (mirror CI locally, in order — see `AGENTS.md`):

- `npx nx format:check`
- `npx nx affected -t lint test build`
- `dotnet test core/csharp/MicroZen.Core.sln`

`scripts/check.sh` runs all three. Tests use the EF Core InMemory provider and set their own `EncryptionKey`, so no PostgreSQL is needed.

## 4. Risk / blast radius

- **Single method, single file.** Change is confined to `GetManyClients` in `ClientsGrpcService.cs`. No other service method reads `page`/`skip`.
- **Semantic change to `skip`.** Callers that previously passed `skip` as a raw row offset will now get it treated as page size. This is a behaviour change on the wire contract, but the old behaviour was broken (overlapping windows), so there is no correct caller to preserve. Note it in the PR description.
- **Proto comment edit only.** Updating the `skip` field comment is a doc change, not a schema change — field numbers and types are untouched, so no client regen is forced. **No proto field is added, removed, or renumbered.** If the team prefers a true `pageSize` field instead of reusing `skip`, that is a larger proto change (regen + client coordination) and should be its own issue — called out here as the alternative, not done in this spec.
- **Out of scope:** the `searchTerm` interpolation bug (`$"%{request}%"`) in the same method is #50 and must not be touched by this change.

## 5. Acceptance

- `.kiro/specs/must-fix-51/spec.md` exists (this file).
- The implementation PR targets branch `epic/must-have-fixes`.
- The PR has green checks: `npx nx format:check`, `npx nx affected -t lint test build`, and `dotnet test core/csharp/MicroZen.Core.sln` all pass, including the new paging tests proving page 1 and page 2 return non-overlapping correct windows and `NextUrl` is correct.
