# Spec: Architecture doc + diagram (docs/architecture.md)

**Tracking issue:** #8 (part of epic #3, Tier 2)
**Branch:** `spec/issue-8-architecture-doc`

## Problem

The steering files describe structure and conventions, but there is no
request-flow map. An agent making a cross-cutting change (touching the dashboard,
the API, auth, and the data layer) has to reconstruct how the pieces fit by
reading code. A concise architecture doc with a diagram fixes that.

## Goal

Add `docs/architecture.md` that accurately maps the runtime architecture and
includes at least one request-flow diagram, cross-linked from steering.

## Requirements

1. Describe the components and how they interact:
   - **Front-end:** Angular 17 + Ionic/Capacitor `core-dashboard` (`core/ts/dashboard`).
   - **API:** `MicroZen.Api` exposing **gRPC with JSON transcoding** and Swagger;
     services include `ClientsService`, `OrganizationsService`,
     `OrganizationUsersService`, `ClientApiKeysService`, plus gRPC health checks
     and reflection (see `Program.cs`).
   - **Auth:** OAuth2/OpenID via `MicroZen.OAuth2` (`AddMicroZenOAuth2(...)`,
     Cognito provider, Authorization Code + PKCE) and the API-key auth policy
     (`APIKeyRequirement`, JWT bearer).
   - **Data:** EF Core `MicroZenContext` over PostgreSQL (Npgsql); migrations in
     `MicroZen.Api/Migrations`; dev data seeded on startup.
2. Include **at least one diagram** (Mermaid preferred, ASCII acceptable) showing
   the request flow: dashboard -> API (gRPC/transcoding) -> auth -> DB.
3. Cross-link the doc from `.kiro/steering/product.md`.

## Design notes

- Keep it accurate to the real service/class names; verify against the code
  before writing. Do not invent endpoints.
- Favor one clear diagram plus short prose over exhaustive detail.

## Tasks

- [ ] Write `docs/architecture.md` covering FE, API/gRPC, auth, and data layers.
- [ ] Add at least one request-flow diagram (Mermaid or ASCII).
- [ ] Link the doc from `.kiro/steering/product.md`.

## Acceptance criteria

- Doc accurately reflects the real services and auth flow.
- At least one flow diagram is present; it is linked from steering.

## Verification

- Names/flows in the doc match `Program.cs`, `MicroZen.Api/Services`, and `MicroZen.OAuth2`.
