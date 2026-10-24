# Spec: Dependency hygiene (Dependabot/Renovate)

**Tracking issue:** #12 (part of epic #3, Tier 3)
**Branch:** `spec/issue-12-dependency-hygiene`

## Problem

Dependencies drift and accrue vulnerabilities. Agents then work against stale,
possibly-insecure versions. Automated update PRs keep npm and NuGet packages
current with minimal manual effort.

## Goal

Add automated dependency management covering all three ecosystems in the repo,
with sensible scheduling and grouping to limit PR noise.

## Requirements

1. Add `.github/dependabot.yml` covering:
   - **npm** ecosystem at the repo root (`package.json` / `package-lock.json`).
   - **NuGet** ecosystem under `core/csharp` (the .NET projects; central package
     management via `Directory.Packages.props` if present).
   - **github-actions** ecosystem (`.github/workflows`).
2. Use a weekly schedule and group minor/patch updates to reduce noise.
3. (Alternative) If Renovate is preferred, add `renovate.json` instead and
   document the choice; pick exactly one tool.

## Design notes

- Watch out for ecosystem churn: pin a reasonable `open-pull-requests-limit`.
- Keep the Angular/Nx packages grouped so related bumps land together (they are
  version-locked in this repo).

## Tasks

- [ ] Add `.github/dependabot.yml` for npm, NuGet, and github-actions.
- [ ] Configure weekly schedule, grouping, and PR limits.
- [ ] Document the choice (Dependabot vs Renovate) briefly.

## Acceptance criteria

- Config is valid and recognized by GitHub; covers npm, NuGet, and Actions.

## Verification

- GitHub Insights > Dependency graph > Dependabot shows the configured ecosystems with no config errors.
