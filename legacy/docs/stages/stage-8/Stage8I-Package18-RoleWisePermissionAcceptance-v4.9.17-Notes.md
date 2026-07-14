# Stage 8I Package 18 — Role-wise Permission Acceptance v4.9.17

## Purpose

Close production permission acceptance gaps by making role coverage and route expectations visible in the app.

## Acceptance

- Run `python3 scripts/validation/permission-role-acceptance-check.py`.
- Login as Admin and open `/permission-final-acceptance`.
- Confirm each required role has an active test user.
- Confirm store-scoped roles have company/store scope assigned.
- Test allowed and blocked routes for each production role before go-live.
