# Spec: Add CODEOWNERS

**Tracking issue:** #10 (part of epic #3, Tier 2)
**Branch:** `spec/issue-10-codeowners`

## Problem

There is no ownership mapping, so reviews aren't auto-routed and it isn't obvious
who owns which area. As the repo and contributor base grow, CODEOWNERS makes this
explicit and improves review turnaround.

## Goal

Add a valid `CODEOWNERS` file mapping the main top-level areas to owners, using
placeholder handles/teams the maintainer can edit.

## Requirements

1. Add `.github/CODEOWNERS` (GitHub recognizes `.github/`, repo root, or `docs/`).
2. Map at least these areas to placeholder owners/teams:
   - `core/ts/` -> front-end owners
   - `core/csharp/` -> back-end owners
   - `.github/` and CI config -> maintainers/devops
   - `.kiro/` and docs (`README.md`, `AGENTS.md`, `docs/`) -> docs owners
3. Use clearly-placeholder handles (e.g. `@MicroZenHub/frontend`) so the
   maintainer knows to replace them.

## Design notes

- Later, more specific patterns override earlier ones in CODEOWNERS; order rules
  from general to specific.
- Placeholder teams must be replaced with real teams/users to take effect; note
  this in the file as a comment.

## Tasks

- [ ] Add `.github/CODEOWNERS` with area-to-owner mappings.
- [ ] Use placeholder handles and a comment explaining they must be replaced.

## Acceptance criteria

- Valid CODEOWNERS recognized by GitHub.
- Covers the main top-level areas with sensible placeholders.

## Verification

- GitHub shows no CODEOWNERS syntax errors; opening a PR touching an area requests the mapped owner (once real teams exist).
