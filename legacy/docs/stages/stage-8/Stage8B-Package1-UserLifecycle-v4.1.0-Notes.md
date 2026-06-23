# Stage 8B Package 1 - User Lifecycle Hardening

Version: 4.1.0
Build: `GARMETIX-8B-20260614-4100`
Date: 2026-06-14

## Completed

- Added active/inactive account status to the user model, API contracts, token claims, access register, and startup schema repair.
- Blocked inactive accounts during login and on every authenticated API request so deactivation takes effect immediately.
- Removed the user-facing Admin flag and derive it only from the selected Admin role.
- Added dedicated activate/deactivate and administrative password-reset endpoints.
- Revoked outstanding password-reset tokens after an administrative password change.
- Protected the signed-in user from self-deactivation/deletion and protected the final active admin.
- Added explicit Message Log security events for user create, update, activation, deactivation, password reset, and deletion.
- Updated Roles & Users with status metrics, status badges, dedicated actions, and a wide user form.

## Preserved Behavior

- Existing company, store-group, store, and operation scopes remain supported.
- Owner/Admin access to the Roles & Users workspace remains unchanged.
- Edit and delete actions continue to use the existing global authorization policies.

## Next

Centralize the role permission matrix, expose it in the Access workspace, and add automated role acceptance tests.
