# AGENTS.md

Portable, thin front door for coding agents working in the MicroZen repo. It
lists prerequisites, the exact build/test/lint/run commands for the .NET stack,
the PR/CI gate, and key conventions.

For deeper detail, read the steering files under `.kiro/steering/`, which are the
single source of truth and must not be contradicted here:

- `.kiro/steering/product.md` - product and architecture overview.
- `.kiro/steering/structure.md` - repository layout and conventions.
- `.kiro/steering/tech.md` - authoritative build/test/lint/run commands.

MicroZen is a .NET-only repository: the .NET 10 C# back-end under
`core/csharp/MicroZen.Core.sln`. Nx is used only through the `@nx-dotnet/core`
plugin (configured in `nx.json`), which wraps the .NET solution so build, test,
lint, and format tasks run through the Nx project graph. There are no TypeScript
projects in this repo; the Angular/Ionic dashboard was moved to a separate
private repository.

## Prerequisites

- [Node.js](https://nodejs.org/en/download/)
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (the solution targets `net10.0`)
- [Docker](https://docs.docker.com/get-docker/) (used to run the PostgreSQL database)

Install JS dependencies from the repo root with `npm ci`. The only npm script is
`prepare` (`nx g @nx-dotnet/core:restore`), which runs on install; .NET tasks go
through the `dotnet` CLI, and Nx wraps those same tasks via `@nx-dotnet/core`.
There is no `npm run build` or `npm run test`.

## .NET / C# stack (dotnet CLI, solution `core/csharp/MicroZen.Core.sln`)

- Restore: `dotnet restore core/csharp/MicroZen.Core.sln`
- Build: `dotnet build core/csharp/MicroZen.Core.sln`
- Test (xUnit): `dotnet test core/csharp/MicroZen.Core.sln`
- Run the API: `dotnet run --project core/csharp/MicroZen.Api`
  - `http` profile: `http://localhost:5187`
  - `https` profile: `https://localhost:7070`
  - The API needs a PostgreSQL database named `MicroZenDb` (typically run via
    Docker). On startup it applies EF Core migrations and seeds development data.

Nx wraps these through `@nx-dotnet/core`, so `npx nx affected -t lint test build`
resolves to the .NET targets (there are no TypeScript projects to run).

### Development data seeding

`app.MigrateAndSeedDevDataOnStart()`
(`core/csharp/MicroZen.Api/Context/Functions.cs`) seeds one placeholder
Organization (with a Client, an OAuth2ClientCredentials row, and a ClientAPIKey)
**only** when the environment is `Development`, the `SeedDevData` flag is `true`,
and the `Organizations` table is empty. Enable it with
`SeedDevData=true dotnet run --project core/csharp/MicroZen.Api`. Re-seeding
needs an empty database: `scripts/seed-db.sh --reset` runs
`docker compose down -v && docker compose up -d` then starts the API with
seeding enabled. All seeded data is placeholder only. See
`.kiro/steering/tech.md` for the full field list and reset workflow.

The .NET projects target `net10.0`, so a matching **.NET 10 SDK** is required.
Do not change the csproj `TargetFramework` values.

## PR / CI gate

CI (`.github/workflows/ci.yml`) runs on pushes to `main` and on pull requests
and is the authoritative full-gate verification. Two jobs:

- `main` (Node 20): checkout, `setup-node`, `npm ci`, `nrwl/nx-set-shas`, then
  `npx nx format:check` and `npx nx affected -t lint test build`. Because there
  are no TypeScript projects, the affected targets resolve to the .NET build via
  `@nx-dotnet/core`.
- `dotnet` (.NET 10 via `actions/setup-dotnet`): `dotnet restore`,
  `dotnet build`, and `dotnet test core/csharp/MicroZen.Core.sln`.

Before opening a pull request, mirror the gate locally, in order:

- `npx nx format:check` (fix with `npx nx format:write`)
- `npx nx affected -t lint test build`
- `dotnet test core/csharp/MicroZen.Core.sln`

`scripts/check.sh` runs all three steps in order and exits non-zero on the first
failure. After `npm ci` and with a .NET 10 SDK on `PATH`, run `./scripts/check.sh`
from a clean checkout with no extra setup. The .NET tests use the EF Core
InMemory provider and set their own `EncryptionKey`, so they are self-contained
and need no PostgreSQL.

## Kiro hooks

Automation under `.kiro/hooks/` runs the real commands above on editor/workflow
events so the gates are enforced automatically. The hooks are fast and scoped
(`nx affected`, not full-repo), and the heavier full gate is opt-in. See
`.kiro/hooks/README.md` for details.

- `dotnet-save-format.kiro.hook`: on saving a C# file under `core/csharp/**/*.cs`,
  runs `npx nx format:write && npx nx affected -t lint`.
- `pre-pr-gate.kiro.hook`: manual (user-triggered) gate that runs
  `npx nx format:check && npx nx affected -t lint test build && dotnet test core/csharp/MicroZen.Core.sln`.

No hook embeds secrets or dumps the environment; the `NX_CLOUD_ACCESS_TOKEN`
value is never referenced in a hook.

## Nx Cloud authentication

The Nx Cloud access token comes from the `NX_CLOUD_ACCESS_TOKEN` environment
variable; it is not committed to `nx.json`. The token is **read-only**, so CI
runs plain `npx nx format:check` rather than
`npx nx-cloud record -- nx format:check` (a read-only token cannot record a
run). The token is still supplied to the `npx nx affected` step so remote cache
**reads** work. In CI it is sourced from a GitHub Actions secret of the same
name. For local Nx Cloud usage, export that variable in your shell. Never commit
the token value to the repository.

## Key conventions

- **Prettier** with `singleQuote: true`.
- **`.editorconfig`**: 2-space indentation by default; `*.cs` uses **tab**
  indentation with `indent_size = 4`; `*.md` keeps trailing whitespace.
- **C# central package management**: declare versions in
  `core/csharp/Directory.Packages.props` and use version-less
  `PackageReference` entries in csproj; do not pin versions in csproj.
- **gRPC contracts** live in `MicroZen.Api/Protos/*.proto`; **EF Core
  migrations** live in `MicroZen.Api/Migrations`; sensitive DB columns are
  encrypted via `EncryptColumnAttribute`.

See `.kiro/steering/structure.md` for the full conventions reference.
