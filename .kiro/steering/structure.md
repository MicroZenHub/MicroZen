# Repository Structure & Conventions

## Directory layout

Root (workspace-level config and tooling):

- `nx.json`, `package.json`, `tsconfig.base.json` - Nx workspace and TS config.
- `jest.config.ts`, `jest.preset.js` - Jest aggregation and preset.
- `.eslintrc.json`, `.eslintignore` - ESLint config.
- `.prettierrc`, `.prettierignore` - Prettier config.
- `.editorconfig` - editor/indentation rules.
- `.github/workflows/ci.yml` - CI pipeline.
- `.config/dotnet-tools.json` - local .NET tool manifest.
- `README.md`, `LICENSE` (Apache-2.0).

Front-end:

- `core/ts/dashboard` - Angular 17 + Ionic/Capacitor app, Nx project
  `core-dashboard`. Source under `src/app` (app module, routes, home page);
  theme in `src/theme/variables.scss`. Note: `core/ts/dashboard/project.json`
  references paths as `core/dashboard/...` (without `/ts`) internally; this is
  existing state and is intentionally left as-is.
- `core/ts/dashboard-e2e` - Cypress end-to-end tests (Nx project
  `core-dashboard-e2e`).

Backend - `core/csharp` holds `MicroZen.Core.sln` with three projects:

- `MicroZen.Api` - the gRPC API, with subfolders:
  - `Context/` - EF Core `DbContext` and related.
  - `Migrations/` - EF Core migrations.
  - `Protos/` - `.proto` gRPC service and message contracts.
  - `Entities/` - EF Core entity types.
  - `Services/` - gRPC service implementations.
  - `Security/APIKeys` - API key generation, validation, and the auth handler.
  - `Security/Encryption` - column encryption for sensitive data.
- `MicroZen.OAuth2` - reusable OAuth2/OpenID client library (`Config/`,
  `Providers/`, `State/`, `Definitions/`).
- `MicroZen.Api.Test` - xUnit tests.

## TypeScript / Angular conventions

- **Prettier** with `singleQuote: true` (`.prettierrc`).
- **ESLint** via `@nx` using the `@nx/enforce-module-boundaries` rule
  (`.eslintrc.json`). Respect project boundaries; do not add cross-project
  imports that violate it.
- **Nx generator defaults** (`nx.json` `generators`): Angular applications and
  components use `scss` styles, Jest is the unit test runner, Cypress is the
  e2e runner, and the component/app `prefix` is `MicroZen`.

## C# conventions

- `Nullable` and `ImplicitUsings` are enabled in the csproj files.
- **Central package management**: versions are declared centrally in
  `core/csharp/Directory.Packages.props` (`ManagePackageVersionsCentrally` is
  true). When adding a dependency, add the `PackageVersion` there and a
  version-less `PackageReference` in the csproj; do not pin versions in csproj.
- **gRPC contracts** live in `MicroZen.Api/Protos/*.proto`; services are
  implemented under `Services/` and mapped in `MicroZen.Api/Program.cs`.
- **EF Core migrations** live in `MicroZen.Api/Migrations`.
- **Sensitive DB columns** are encrypted via the `EncryptColumnAttribute`.

## Formatting / `.editorconfig`

`.editorconfig` applies these rules:

- Default (`[*]`): UTF-8 charset, space indentation with `indent_size = 2`,
  insert a final newline, trim trailing whitespace.
- `[*.cs]`: **tab** indentation with `indent_size = 4`.
- `[*.md]`: trailing whitespace is **not** trimmed (`trim_trailing_whitespace =
false`), and `max_line_length` is off.

## Toolchain caveat

The .NET stack targets `net10.0` (see all three csproj and
`core/csharp/Directory.Packages.props`), so a **.NET 10 SDK** is required to
build or test the C# projects (the `README.md` Prerequisites list the .NET 10
SDK accordingly). Treat the net10.0 target as authoritative and do not edit the
csproj `TargetFramework` values here.
