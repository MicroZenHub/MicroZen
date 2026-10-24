# Kiro hooks (.kiro/hooks/)

These hooks run the project's real, verified commands (see `.kiro/steering/tech.md`
and `AGENTS.md`) on editor/workflow events so quality gates are enforced
automatically instead of relying on memory. They are intentionally fast and
scoped: the save hooks use `nx affected` rather than full-repo runs, and the
heavier full gate is opt-in (manually triggered).

No hook embeds secrets or dumps the environment. The Nx Cloud token is sourced
from the `NX_CLOUD_ACCESS_TOKEN` environment variable (never committed); these
hooks do not reference it.

## Hooks

- `dotnet-save-format.kiro.hook`

  - Trigger: a C# file under `core/csharp/**/*.cs` is saved.
  - Action: `npx nx format:write && npx nx affected -t lint`.
  - Keeps the working tree formatted after .NET edits without running the full
    repo gate. Scoped to affected projects.

- `pre-pr-gate.kiro.hook`

  - Trigger: manual (user-triggered button "Run pre-PR gate").
  - Action: `npx nx format:check && npx nx affected -t lint test build && dotnet test core/csharp/MicroZen.Core.sln`.
  - Mirrors the CI gate before committing or opening a pull request. Opt-in
    because it is heavier than the save hooks; it blocks on the first failing
    step.

## Notes

- Commands mirror the local gate documented in `.kiro/steering/tech.md` (the
  authoritative command reference) and `AGENTS.md`.
- The .NET tests use the EF Core InMemory provider and set their own
  `EncryptionKey`, so the pre-PR gate needs no PostgreSQL.
- When running `nx affected` locally you may need to pass a base ref, for example
  `--base=main`.
