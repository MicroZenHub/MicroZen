# Spec: Trusted, fast, locally-runnable test + lint gate

**Tracking issue:** #5 (part of epic #3, Tier 1)
**Branch:** `spec/issue-5-test-gate`

## Problem

Agents verify their own work by running the project's checks. CI
(`.github/workflows/ci.yml`) runs `npx nx-cloud record -- nx format:check` then
`npx nx affected -t lint test build`, and the .NET side is tested with
`dotnet test`. If an agent cannot reproduce that exact gate locally, or if it is
slow/flaky or has hidden external dependencies, the self-correction loop breaks.

## Goal

A single, documented, reliable sequence that reproduces the CI gate locally and
passes on a clean checkout — with any external dependency removed, containerized,
or clearly documented.

## Requirements

1. **Verify** these run locally on a clean checkout and document the exact commands:
   - `npx nx format:check`
   - `npx nx affected -t lint test build`
   - `dotnet test core/csharp/MicroZen.Core.sln`
2. **Resolve hidden dependencies:** determine whether `dotnet test` (project
   `MicroZen.Api.Test`) requires a live PostgreSQL. Note `MicroZen.Api.Test/appsettings.json`
   defines an `EncryptionKey`.
   - If it needs a DB: integrate Testcontainers (preferred) or document a
     disposable test database, consistent with the dev-environment work (#4).
   - If it does not: explicitly document that the .NET tests are self-contained.
3. **Optional convenience:** add `scripts/check.sh` (and/or an Nx target) that runs
   the full gate in order and exits non-zero on first failure.
4. Keep docs consistent with `AGENTS.md` and `.kiro/steering/tech.md`.

## Design notes

- Prefer Testcontainers over a hand-managed DB so the gate stays hermetic.
- Do not weaken CI; the local gate must match what CI enforces.
- If `nx affected` needs a base ref locally, document how (e.g. `--base=main`).

## Tasks

- [ ] Confirm each gate command runs on a clean checkout; capture exact invocations.
- [ ] Determine and resolve `dotnet test` external dependencies (Testcontainers or documented).
- [ ] Optionally add `scripts/check.sh` running the full gate.
- [ ] Update `AGENTS.md` / `tech.md` with the authoritative local gate sequence.

## Acceptance criteria

- One documented sequence reproduces the CI gate locally and passes on a clean checkout.
- Any test-time external dependency is removed, containerized, or clearly documented.

## Verification

- Fresh clone + `npm ci` + the documented sequence passes with no manual setup beyond what is written down.
