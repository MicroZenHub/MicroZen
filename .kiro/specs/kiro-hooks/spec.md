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

1. **On file save** (scoped to changed project): run `npx nx format:write` and the
   affected lint (`npx nx affected -t lint`) so formatting/lint stay clean.
2. **Pre-commit / pre-PR**: run the affected test gate
   (`npx nx affected -t lint test build`) and, where relevant,
   `dotnet test core/csharp/MicroZen.Core.sln`.
3. Keep hooks **fast and scoped** — prefer `nx affected` over full-repo runs; do
   not run the .NET suite on every TS save or vice versa.
4. Document the hooks (what triggers them, what they run) in `.kiro/steering/` or
   `AGENTS.md`, consistent with the commands already documented there.

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

- Saving a TS file triggers format+affected-lint for that project only; the pre-PR hook runs the gate and blocks on failure.
