# Auth Client Package

Purpose: shared authentication/session primitives for all Garmetix frontends.

Planned extraction source:

- `legacy/frontend/garmetix-web/composables/useAuth.ts`
- `legacy/frontend/garmetix-web/middleware/auth.global.ts`
- `legacy/frontend/garmetix-web/composables/useAccessControl.ts`

Responsibilities:

- Store and restore user session.
- Expose token lookup for API clients.
- Normalize app roles.
- Support route permission checks.
- Prepare for future cross-subdomain SSO without blocking the first modular rollout.

Important: current auth uses localStorage, which is origin-scoped. Separate subdomains will not automatically share login until a secure cookie or token handoff is implemented.

