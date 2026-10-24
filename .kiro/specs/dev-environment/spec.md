# Spec: Reproducible dev environment (docker-compose + .env.example)

**Tracking issue:** #4 (part of epic #3, Tier 1)
**Branch:** `spec/issue-4-dev-environment`

## Problem

`MicroZen.Api` requires a PostgreSQL database named `MicroZenDb`. On startup it
applies EF Core migrations and seeds development data
(`app.MigrateAndSeedDevDataOnStart()` in `core/csharp/MicroZen.Api/Program.cs`),
connecting via Npgsql using the connection string named `MicroZenConnectionString`.
Today an agent (or new contributor) has to discover and stand this up manually,
which breaks the "run it to verify my work" loop that agents depend on.

## Goal

Make the local environment start with a single command and document every
variable needed, with no real secrets committed.

## Requirements

1. **`docker-compose.yml`** at the repo root that runs:
   - A PostgreSQL service exposing the `MicroZenDb` database, with credentials
     sourced from environment variables (see `.env.example`).
   - Optionally the API service itself, built from `core/csharp/MicroZen.Api`,
     wired to depend on Postgres being healthy. If running the API in Compose is
     impractical, document running it via `dotnet run` against the Compose Postgres.
2. **`.env.example`** at the repo root listing every required variable with
   placeholder values only:
   - Postgres: host, port, user, password, db name.
   - The `MicroZenConnectionString` connection string (or its component parts).
   - OAuth2/Cognito settings consumed by `AddMicroZenOAuth2(...)` in `Program.cs`
     (provider, client id/secret, authority/metadata URL) — placeholders only.
3. **Docs:** update the README QuickStart and/or `.kiro/steering/tech.md` to
   describe `docker compose up` and copying `.env.example` to `.env`. Keep
   consistent with existing steering wording.

## Design notes

- Use a pinned Postgres image tag (e.g. `postgres:16`), a named volume for
  persistence, and a healthcheck so dependents wait for readiness.
- Do NOT commit a real `.env` — only `.env.example`. Ensure `.env` is gitignored.
- Keep the connection string name exactly `MicroZenConnectionString` to match
  `static MicroZen.Data.Context.Variables.MicroZenConnectionString`.

## Tasks

- [ ] Add `docker-compose.yml` (Postgres + optional API).
- [ ] Add `.env.example` with all required placeholders.
- [ ] Ensure `.env` is in `.gitignore`.
- [ ] Update README/steering with the one-command setup.
- [ ] Verify the API connects, migrates, and seeds against the Compose Postgres.

## Acceptance criteria

- `docker compose up` brings up Postgres; the API can connect, run migrations, and seed.
- `.env.example` documents every variable needed to start the API (placeholders only).
- Docs updated and consistent with existing `.kiro/steering/`.

## Verification

- `cp .env.example .env`, `docker compose up -d`, then `dotnet run --project core/csharp/MicroZen.Api` succeeds and reaches a running state (http:5187 / https:7070).
