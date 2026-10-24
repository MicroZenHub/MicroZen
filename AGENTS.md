# AGENTS.md

Portable, thin front door for coding agents working in the MicroZen repo. It
lists prerequisites, the exact build/test/lint/run commands for both stacks, the
PR/CI gate, and key conventions.

For deeper detail, read the steering files under `.kiro/steering/`, which are the
single source of truth and must not be contradicted here:

- `.kiro/steering/product.md` - product and architecture overview of both stacks.
- `.kiro/steering/structure.md` - repository layout and conventions.
- `.kiro/steering/tech.md` - authoritative build/test/lint/run commands.

MicroZen is an Nx monorepo with two stacks in a single Nx project graph: an
Angular 17 + Ionic/Capacitor front-end (`core/ts/dashboard`, Nx project
`core-dashboard`) and a .NET 10 C# back-end (`core/csharp/MicroZen.Core.sln`).
The `@nx-dotnet/core` plugin ties the .NET solution into the same Nx graph.

## Prerequisites

- [Node.js](https://nodejs.org/en/download/)
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (the solution targets `net10.0`)
- [Docker](https://docs.docker.com/get-docker/) (used to run the PostgreSQL database)

Install JS dependencies from the repo root with `npm ci`. The only npm script is
`prepare` (`nx g @nx-dotnet/core:restore`), which runs on install; TypeScript
tasks go through `npx nx ...` and .NET tasks through the `dotnet` CLI. There is
no `npm run build` or `npm run test`.

## TypeScript / Angular stack (Nx, project `core-dashboard`)

- Serve the dev app: `npx nx serve core-dashboard`
- Build: `npx nx build core-dashboard` (all affected: `npx nx affected -t build`)
- Unit tests (Jest): `npx nx test core-dashboard` (or `npx nx affected -t test`)
- Lint (ESLint): `npx nx lint core-dashboard` (or `npx nx affected -t lint`)
- End-to-end (Cypress): `npx nx e2e core-dashboard-e2e`
- Format check / write: `npx nx format:check` / `npx nx format:write`

Capacitor is exposed as Nx targets on `core-dashboard` (`add`, `copy`, `open`,
`run`, `sync`, `update`), each with `ios` and `android` configurations, for
example `npx nx run core-dashboard:sync:ios`. See `.kiro/steering/tech.md`.

## .NET / C# stack (dotnet CLI, solution `core/csharp/MicroZen.Core.sln`)

- Restore: `dotnet restore core/csharp/MicroZen.Core.sln`
- Build: `dotnet build core/csharp/MicroZen.Core.sln`
- Test (xUnit): `dotnet test core/csharp/MicroZen.Core.sln`
- Run the API: `dotnet run --project core/csharp/MicroZen.Api`
  - `http` profile: `http://localhost:5187`
  - `https` profile: `https://localhost:7070`
  - The API needs a PostgreSQL database named `MicroZenDb` (typically run via
    Docker). On startup it applies EF Core migrations and seeds development data.

The .NET projects target `net10.0`, so a matching **.NET 10 SDK** is required.
Do not change the csproj `TargetFramework` values.

## PR / CI gate

CI (`.github/workflows/ci.yml`) runs on pushes to `main` and on pull requests
and is the authoritative full-gate verification. Two jobs:

- `main` (Node 20): checkout, `setup-node`, `npm ci`, `nrwl/nx-set-shas`, then
  `npx nx format:check` and `npx nx affected -t lint test build`.
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

## Nx Cloud authentication

The Nx Cloud access token comes from the `NX_CLOUD_ACCESS_TOKEN` environment
variable; it is not committed to `nx.json`. In CI it is sourced from a GitHub
Actions secret of the same name. For local Nx Cloud usage, export that variable
in your shell. Never commit the token value to the repository.

## Key conventions

- **Prettier** with `singleQuote: true`.
- **ESLint** via `@nx` using `@nx/enforce-module-boundaries`; respect project
  boundaries and do not add cross-project imports that violate it.
- **`.editorconfig`**: 2-space indentation by default; `*.cs` uses **tab**
  indentation with `indent_size = 4`; `*.md` keeps trailing whitespace.
- **Nx generator defaults**: `scss` styles, Jest for unit tests, Cypress for
  e2e, component/app `prefix` is `MicroZen`.
- **C# central package management**: declare versions in
  `core/csharp/Directory.Packages.props` and use version-less
  `PackageReference` entries in csproj; do not pin versions in csproj.
- **gRPC contracts** live in `MicroZen.Api/Protos/*.proto`; **EF Core
  migrations** live in `MicroZen.Api/Migrations`; sensitive DB columns are
  encrypted via `EncryptColumnAttribute`.

See `.kiro/steering/structure.md` for the full conventions reference.
