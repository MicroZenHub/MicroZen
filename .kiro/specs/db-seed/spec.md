# Spec: DB seed / sample data script

**Tracking issue:** #13 (part of epic #3, Tier 3)
**Branch:** `spec/issue-13-db-seed`

## Problem

Agents (and e2e/manual testing) benefit from realistic sample data. The API
already seeds dev data on startup (`app.MigrateAndSeedDevDataOnStart()` in
`core/csharp/MicroZen.Api/Program.cs`), but this path is not documented or easily
repeatable outside a fresh startup.

## Goal

Make dev-data seeding discoverable and repeatable, producing a usable local
`MicroZenDb` with representative data, consistent with the docker-compose
environment (#4).

## Requirements

1. Document the existing seed path (`MigrateAndSeedDevDataOnStart` and whatever it
   calls) so agents know what data exists and how it is produced.
2. Provide a repeatable way to (re)seed a local `MicroZenDb` with representative
   organizations, clients, and API keys for manual and e2e testing:
   - Either a script (e.g. `scripts/seed-db.sh` / an EF or dotnet command) or a
     documented flag/environment toggle.
   - Must work against the Compose Postgres from #4.
3. Use placeholder/non-sensitive data only.

## Design notes

- Reuse the existing seeding code rather than duplicating data definitions where
  possible; if extending it, keep it idempotent (safe to re-run).
- Align connection/config with the dev-environment spec (`MicroZenConnectionString`,
  `.env`).

## Tasks

- [ ] Document the existing dev-data seed path and the data it creates.
- [ ] Provide a repeatable (re)seed command/script for a local DB.
- [ ] Ensure it runs against the docker-compose Postgres and is idempotent.

## Acceptance criteria

- A documented command/path produces a usable seeded local database.
- Consistent with the dev-environment setup (#4); no sensitive data.

## Verification

- After `docker compose up` and running the seed, the local `MicroZenDb` contains the documented sample orgs/clients/API keys.
