# Spec: Representative reference tests (Angular/Jest + xUnit)

**Tracking issue:** #6 (part of epic #3, Tier 1)
**Branch:** `spec/issue-6-reference-tests`

## Problem

AI agents pattern-match hard against existing code. A couple of clear, idiomatic
reference tests teach an agent the project's testing style faster and more
reliably than prose. The repo should carry at least one exemplary test per stack.

## Goal

Provide small, well-commented, copy-ready reference tests in both stacks that
pass under the standard gate and follow the documented conventions.

## Requirements

1. **Angular/Jest** (project `core-dashboard`, under `core/ts/dashboard/src`):
   - One unit test demonstrating the preferred style (component or service),
     using the existing Jest + `jest-preset-angular` setup.
   - Clear arrange/act/assert structure and comments explaining the pattern.
2. **.NET xUnit** (project `MicroZen.Api.Test`, under `core/csharp/MicroZen.Api.Test`):
   - One test exercising a real path — e.g. a gRPC service from
     `MicroZen.Api/Services` (`ClientsService`, `OrganizationsService`, ...) or an
     EF/`MicroZenContext` interaction.
   - Demonstrate setup/teardown (fixtures) and meaningful assertions.
   - If a DB is required, align with the test-gate (#5) approach (Testcontainers
     or documented test DB); otherwise keep it self-contained.
3. Follow conventions in `.kiro/steering/structure.md` (SCSS, prefixes, module
   boundaries, C# `.editorconfig` tab/size-4).

## Design notes

- These are templates, not coverage drivers — keep them minimal and legible.
- Name/locate them so they are easy to find as examples (a short comment noting
  "reference example" is fine).

## Tasks

- [ ] Add one Angular/Jest reference test for `core-dashboard`.
- [ ] Add one xUnit reference test in `MicroZen.Api.Test` exercising a real path.
- [ ] Confirm both pass under `nx test core-dashboard` and `dotnet test`.

## Acceptance criteria

- Both tests pass under the standard gate.
- Tests follow existing conventions and read as copy-ready examples.

## Verification

- `npx nx test core-dashboard` and `dotnet test core/csharp/MicroZen.Core.sln` both pass including the new tests.
