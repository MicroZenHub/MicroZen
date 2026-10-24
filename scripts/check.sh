#!/usr/bin/env bash
#
# check.sh - run the full MicroZen test + lint gate locally, in the same order
# CI enforces it (.github/workflows/ci.yml). Exits non-zero on the first
# failing step so the self-correction loop sees the same result as CI.
#
# Usage:
#   ./scripts/check.sh
#
# Prerequisites (see AGENTS.md / .kiro/steering/tech.md):
#   - Node.js with dependencies installed (`npm ci` at the repo root)
#   - .NET 10 SDK on PATH
#
# The .NET tests (MicroZen.Api.Test) use the EF Core InMemory provider and set
# their own EncryptionKey, so they are self-contained and need no PostgreSQL.

set -euo pipefail

# Run from the repository root regardless of where the script is invoked.
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

run_step() {
  echo ""
  echo ">>> $*"
  "$@"
}

# 1. Formatting (matches CI's `nx format:check`).
run_step npx nx format:check

# 2. Lint, test, build for affected TypeScript/Angular projects.
#    Locally `nx affected` needs a base ref; `--base=main` mirrors CI's
#    nx-set-shas behaviour. Override with NX_BASE if you need a different ref.
run_step npx nx affected -t lint test build --base="${NX_BASE:-main}"

# 3. .NET unit tests (xUnit) for the whole solution, in Release to match CI's
#    `dotnet test ... --configuration Release`. Run standalone (no preceding
#    build), so this restores and builds in Release as needed.
run_step dotnet test core/csharp/MicroZen.Core.sln --configuration Release

echo ""
echo "All gate checks passed."
