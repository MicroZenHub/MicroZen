#!/usr/bin/env bash
#
# seed-db.sh - (re)seed a local MicroZenDb with the built-in development data.
#
# The MicroZen.Api process seeds development data on startup via
# app.MigrateAndSeedDevDataOnStart() (core/csharp/MicroZen.Api/Context/Functions.cs).
# That path only runs when ALL of the following are true:
#   1. the hosting environment is Development (ASPNETCORE_ENVIRONMENT=Development),
#   2. the SeedDevData configuration flag is true, and
#   3. the Organizations table is empty (the idempotency guard).
#
# This script sets conditions 1 and 2 for a single `dotnet run`, so you do not
# have to remember the flags. Because of the empty-table guard, seeding is a
# no-op once data exists; pass --reset to drop and recreate the Compose Postgres
# volume first so the data is seeded fresh.
#
# Usage:
#   ./scripts/seed-db.sh            # start the API with seeding enabled
#   ./scripts/seed-db.sh --reset    # reset the Compose DB first, then seed
#
# Prerequisites (see AGENTS.md / .kiro/steering/tech.md):
#   - .NET 10 SDK on PATH
#   - A reachable PostgreSQL "MicroZenDb" (e.g. `docker compose up -d`), with the
#     ConnectionStrings__MicroZenDb connection string available to the API
#     (for example exported from your .env file).
#
# All seeded values are PLACEHOLDERS only; the script introduces no secrets.

set -euo pipefail

# Run from the repository root regardless of where the script is invoked.
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

reset=false
for arg in "$@"; do
  case "$arg" in
    --reset) reset=true ;;
    -h | --help)
      sed -n '2,30p' "${BASH_SOURCE[0]}"
      exit 0
      ;;
    *)
      echo "seed-db.sh: unknown argument '$arg' (use --reset or --help)" >&2
      exit 2
      ;;
  esac
done

if [ "$reset" = true ]; then
  echo ">>> Resetting the Compose PostgreSQL volume (docker compose down -v)"
  docker compose down -v
  echo ">>> Starting PostgreSQL (docker compose up -d)"
  docker compose up -d
fi

echo ">>> Running MicroZen.Api with migrations + dev-data seeding enabled"
echo "    (ASPNETCORE_ENVIRONMENT=Development, SeedDevData=true)"
echo "    Stop the API with Ctrl+C once it has started and seeded."

ASPNETCORE_ENVIRONMENT=Development \
  SeedDevData=true \
  dotnet run --project core/csharp/MicroZen.Api
