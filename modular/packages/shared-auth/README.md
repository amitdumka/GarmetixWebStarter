# @garmetix/shared-auth

Shared auth, token, role, and permission helpers for the Version5 modular frontends.

This package will gradually receive the reusable parts of `legacy/frontend/garmetix-web/composables/useAuth.ts`, `useWorkspace.ts`, and permission helpers from `useAccessControl.ts`.

Rules:

- Preserve the existing legacy auth behavior until modular apps reach parity.
- Keep storage keys and user/session contracts consistent across apps.
- Treat frontend permission checks as UI filtering only; backend authorization remains required.
- Plan for login-per-app first, then add backend-supported SSO later if needed.

