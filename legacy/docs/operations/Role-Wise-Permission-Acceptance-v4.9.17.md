# Role-wise Permission Acceptance v4.9.17

Stage 8I Package 18 adds explicit role-wise access acceptance so every production role can be tested before handover.

## Added

- `/api/permission-acceptance/status` now exposes:
  - active user coverage by role
  - effective permission matrix from `AccessPermissionMatrix`
  - route expectations for allowed and blocked pages
  - required role readiness count
- `/permission-final-acceptance` now displays:
  - role coverage
  - effective permission matrix
  - route acceptance checklist
  - manual final acceptance checklist
- Static validation added: `scripts/validation/permission-role-acceptance-check.py`.

## Roles to test

- Admin / Owner
- Store Manager
- Salesman / Billing
- Accountant / Remote Accountant
- HR
- Payroll
- Power User

For each role, test allowed pages and blocked pages. Blocked pages must show access denied and must not expose data.
