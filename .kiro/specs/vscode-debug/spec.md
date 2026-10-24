# Spec: VS Code debug configs (.vscode/launch.json)

**Tracking issue:** #11 (part of epic #3, Tier 3)
**Branch:** `spec/issue-11-vscode-debug`

## Problem

There is a `.vscode/extensions.json` but no debug configuration, so new
contributors have to hand-build launch setups for the API and the dashboard.
Ready-made configs lower onboarding friction.

## Goal

Add `.vscode/launch.json` (and supporting `tasks.json` if needed) with valid
configs to debug both stacks, referencing real projects and ports.

## Requirements

1. **.NET API** debug config for `MicroZen.Api`:
   - Launch via the existing profiles (http `http://localhost:5187`,
     https `https://localhost:7070` per `launchSettings.json`).
   - A `tasks.json` build task (`dotnet build core/csharp/MicroZen.Core.sln`) as
     the preLaunchTask if required.
2. **Angular dashboard** config for `core-dashboard`:
   - Serve/debug the app (either a Chrome launch attached to the dev server, or a
     task that runs `npx nx serve core-dashboard`).
3. Keep consistent with `.vscode/extensions.json`; do not remove existing entries.

## Design notes

- Reference exact project paths and the real ports; verify against
  `core/csharp/MicroZen.Api/Properties/launchSettings.json`.
- Favor configs that "just work" after `npm ci` + `dotnet restore` + the dev DB (#4).

## Tasks

- [ ] Add `.vscode/launch.json` with .NET API and Angular dashboard configs.
- [ ] Add `.vscode/tasks.json` if preLaunch build/serve tasks are needed.
- [ ] Verify JSON validity and that paths/ports match the projects.

## Acceptance criteria

- Launch configs are valid JSON and reference real projects/ports.

## Verification

- Opening the Run and Debug panel lists both configs; they build/serve and attach without manual edits.
