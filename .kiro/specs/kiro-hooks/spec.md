# Spec: Kiro hooks (.kiro/hooks/)

**Tracking issue:** #7 (part of epic #3, Tier 2)
**Branch:** `spec/issue-7-kiro-hooks`

## Problem

Quality gates (format, lint, test) are only enforced if someone remembers to run
them. Kiro hooks let the repo run automation on events (file save, pre-commit,
pre-PR), keeping agent output clean automatically instead of relying on memory.

## Goal

Add a small set of fast, scoped hooks under `.kiro/hooks/` that run the project's
real, verified commands on the right events, and document them.

## Requirements

1. **On C# file save** (`fileEdited` scoped to `core/csharp/**/*.cs`): run
   `npx nx format:write` and the affected lint (`npx nx affected -t lint`) so
   formatting/lint stay clean after .NET edits.
2. **Manual pre-PR gate** (`userTriggered`): run the affected lint/test/build
   pipeline (`npx nx format:check && npx nx affected -t lint test build`) plus the
   self-contained .NET test suite (`dotnet test core/csharp/MicroZen.Core.sln`).
3. Keep hooks **fast and scoped**: prefer `nx affected` over full-repo runs, keep
   the save hook limited to C# edits, and make the heavier .NET gate opt-in rather
   than running it on every save.
4. Document the hooks (what triggers them, what they run) in `.kiro/steering/` or
   `AGENTS.md`, consistent with the commands already documented there.

   TS/frontend coverage is out of scope: the `core/ts` frontend has moved to a
   separate private repo, so these hooks are .NET-oriented only.

## Design notes

- Reference only the real commands verified in `AGENTS.md` / `tech.md`.
- Hooks must not embed secrets and must not dump the environment.
- Consider making the heavier pre-PR gate opt-in if it would be too slow to run
  on every commit.

## Tasks

- [ ] Add `.kiro/hooks/` with a save-triggered format+lint hook.
- [ ] Add a pre-commit/pre-PR test-gate hook.
- [ ] Ensure hooks are scoped (affected-only) and fast.
- [ ] Document hooks in steering/AGENTS.md.

## Acceptance criteria

- Hooks are valid and trigger on their intended events.
- Hooks reference the real, verified commands from `AGENTS.md` / `tech.md`.

## Verification

- Saving a C# file under `core/csharp/**/*.cs` triggers `npx nx format:write` and
  `npx nx affected -t lint`, scoped to affected projects only.
- The manual pre-PR gate hook (`userTriggered`) runs
  `npx nx format:check && npx nx affected -t lint test build && dotnet test core/csharp/MicroZen.Core.sln`
  and blocks (exits non-zero) on the first failing step.
