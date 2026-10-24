# Build, Test, Lint & Run Commands

This is the authoritative command reference for both stacks. The TypeScript side
is driven through Nx; the .NET side is driven through the `dotnet` CLI against
`core/csharp/MicroZen.Core.sln`. The only npm script is `prepare`
(`nx g @nx-dotnet/core:restore`); there is no `npm run build` or `npm run test`.

## TypeScript / Angular (Nx, project `core-dashboard`)

- Install JS dependencies (repo root): `npm ci`
  - This runs the `prepare` script, which invokes the `@nx-dotnet/core` restore.
- Serve the dev app: `npx nx serve core-dashboard`
- Build: `npx nx build core-dashboard` (or all affected: `npx nx affected -t build`)
- Unit tests (Jest): `npx nx test core-dashboard` (or `npx nx affected -t test`)
- Lint (ESLint): `npx nx lint core-dashboard` (or `npx nx affected -t lint`)
- End-to-end (Cypress): `npx nx e2e core-dashboard-e2e`
- Format check / write: `npx nx format:check` / `npx nx format:write`

### Capacitor targets

Capacitor is exposed as Nx targets on `core-dashboard` (see
`core/ts/dashboard/project.json`): `add`, `copy`, `open`, `run`, `sync`, and
`update`, each with `ios` and `android` configurations. For example:

- `npx nx run core-dashboard:sync:ios`
- `npx nx run core-dashboard:add:android`
- `npx nx run core-dashboard:open:ios`

## .NET (dotnet CLI, solution `core/csharp/MicroZen.Core.sln`)

- Restore: `dotnet restore core/csharp/MicroZen.Core.sln`
- Build: `dotnet build core/csharp/MicroZen.Core.sln`
- Test (xUnit): `dotnet test core/csharp/MicroZen.Core.sln`
- Run the API: `dotnet run --project core/csharp/MicroZen.Api`
  - `http` profile: `http://localhost:5187`
  - `https` profile: `https://localhost:7070` (per
    `MicroZen.Api/Properties/launchSettings.json`).
  - The API needs a PostgreSQL connection named `MicroZenDb`, typically run via
    Docker. On startup it applies EF Core migrations and seeds development data
    (`app.MigrateAndSeedDevDataOnStart()` in `Program.cs`).

### Local database (Docker Compose)

A `docker-compose.yml` at the repo root runs PostgreSQL (pinned
`postgres:16`, named volume, healthcheck) for the `MicroZenDb` database. Copy
the example environment file and start the database before running the API:

- `cp .env.example .env` (then adjust the placeholder values; `.env` is
  gitignored and must never contain committed secrets)
- `docker compose up -d`
- `dotnet run --project core/csharp/MicroZen.Api`

`.env.example` lists every variable needed to start the API: the `POSTGRES_*`
values consumed by Compose, the `ConnectionStrings__MicroZenDb` connection
string (the connection string name is `MicroZenDb`, matching
`static MicroZen.Data.Context.Variables.MicroZenConnectionString`), and the
`MicroZen__*` OAuth2/Cognito settings read by
`AddMicroZenOAuth2(MicroZenProvider.Cognito, ...)` in `Program.cs`
(`AuthorityUrl`, `APIKey`, and optional `Interval`).

## CI gate

CI (`.github/workflows/ci.yml`) runs on pushes to `main` and on pull requests
and is the authoritative full-gate verification. It has two jobs:

- `main` (Node 20): checkout (fetch-depth 0), `setup-node`, `npm ci`,
  `nrwl/nx-set-shas`, then `npx nx format:check` and
  `npx nx affected -t lint test build`.
- `dotnet` (.NET 10 via `actions/setup-dotnet`): `dotnet restore`,
  `dotnet build`, and `dotnet test core/csharp/MicroZen.Core.sln`.

## Local gate (run before opening a pull request)

Mirror the CI gate locally, in order, exiting on the first failure:

1. `npx nx format:check` (fix formatting with `npx nx format:write`)
2. `npx nx affected -t lint test build` (locally pass a base ref if needed,
   e.g. `--base=main`)
3. `dotnet test core/csharp/MicroZen.Core.sln`

`scripts/check.sh` runs all three steps in this order and exits non-zero on the
first failing step. After `npm ci` and with a .NET 10 SDK on `PATH`, run it from
a clean checkout with no additional manual setup:

```bash
./scripts/check.sh
```

### .NET tests are self-contained

`MicroZen.Api.Test` uses the EF Core **InMemory** provider
(`Microsoft.EntityFrameworkCore.InMemory`) and sets its own `EncryptionKey`
environment variable in the test fixtures, so `dotnet test` does **not** require
a live PostgreSQL database or any other external service. The CI `dotnet` job
therefore runs no Postgres service container.

## Nx Cloud authentication

The Nx Cloud access token is provided through the `NX_CLOUD_ACCESS_TOKEN`
environment variable; it is not committed to `nx.json`. The token is
**read-only**, so CI runs plain `npx nx format:check` rather than
`npx nx-cloud record -- nx format:check` (a read-only token cannot record a
run). The token is still supplied to the `npx nx affected` step so remote cache
**reads** work. In CI the value is sourced from a GitHub Actions secret of the
same name (`NX_CLOUD_ACCESS_TOKEN`). For local Nx Cloud usage, export that
variable in your shell. Never commit the token value to the repository.

## Toolchain caveat

The .NET projects target `net10.0` (`TargetFramework` in all three csproj, with
.NET 10 package versions in `core/csharp/Directory.Packages.props`), so building
and testing the C# stack requires a matching **.NET 10 SDK** (the `README.md`
Prerequisites list the .NET 10 SDK). Do not change the csproj `TargetFramework`
values.
