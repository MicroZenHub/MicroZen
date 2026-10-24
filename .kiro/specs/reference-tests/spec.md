# Spec: Representative reference test (xUnit)

**Tracking issue:** #6 (part of epic #3, Tier 1)
**Branch:** `spec/issue-6-reference-tests`

## Problem

AI agents pattern-match hard against existing code. A clear, idiomatic
reference test teaches an agent the project's testing style faster and more
reliably than prose. The repo should carry at least one exemplary test for the
stack it maintains.

## Goal

Provide a small, well-commented, copy-ready reference test for the .NET stack
that passes under the standard gate and follows the documented conventions.

## Scope note

An Angular/Jest reference test was originally planned for the `core-dashboard`
project under `core/ts/dashboard/src`. That is now intentionally omitted:
`core/ts/dashboard` (and `core/ts/dashboard-e2e`) is being moved to a separate
private repository so the frontend can be sold independently of this project.
Everything under those paths is therefore out of scope for this deliverable, and
no reference test is added there. This spec covers only the .NET/xUnit stack.

## Requirements

1. **.NET xUnit** (project `MicroZen.Api.Test`, under `core/csharp/MicroZen.Api.Test`):
   - One test exercising a real path — e.g. a gRPC service from
     `MicroZen.Api/Services` (`ClientsService`, `OrganizationsService`, ...) or an
     EF/`MicroZenContext` interaction.
   - Demonstrate setup/teardown (fixtures) and meaningful assertions.
   - If a DB is required, align with the test-gate (#5) approach (Testcontainers
     or documented test DB); otherwise keep it self-contained.
2. Follow conventions in `.kiro/steering/structure.md` (C# `.editorconfig`
   tab/size-4).

## Design notes

- This is a template, not a coverage driver — keep it minimal and legible.
- Name/locate it so it is easy to find as an example (a short comment noting
  "reference example" is fine).

## Tasks

- [ ] Add one xUnit reference test in `MicroZen.Api.Test` exercising a real path.
- [ ] Confirm it passes under `dotnet test`.

## Acceptance criteria

- The xUnit reference test passes under the standard gate (`dotnet test`).
- The test follows existing conventions and reads as a copy-ready example.

## Verification

- `dotnet test core/csharp/MicroZen.Core.sln` passes including the new test.
