# Stage 14B.10 HR Legacy Full Parity Audit

Version: `6.0.19`

Status: `complete`

## Why This Stage Exists

Stage 14B.9 closed the newer attendance/payroll hardening lane, but it did not fully cover the legacy HR module surface. The legacy system has two HR attendance models that must both exist in modular HR before Books resumes:

1. Older HR/payroll model: employee master, daily attendance, monthly attendance, salary structures, salary payments and payslips.
2. Newer attendance/kiosk model: shifts, shift rules, kiosk/device/biometric/photo proof/manual punch/regularization/payroll review/salary draft/salary payment flows.

Books parity is paused until this HR parity list is closed.

## Legacy HR Screens Checked

- `/hr`: employee master, daily attendance and older monthly attendance.
- `/payroll`: salary structures, salary payments, payslips, print/download/share handoff.
- `/payroll/finalization`: real-month validation, month-end payroll closure and hardened finalize-month posting.
- `/attendance/*`: kiosk attendance, devices, biometric enrollment, face liveness, mobile kiosk, photo review, regularization, monthly review, payroll summary/review, salary drafts, salary payments, shifts and shift rules.

## Implemented In This Checkpoint

- Added modular HR `/attendance/shift-rules`.
- Added modular HR `/payroll/finalization`.
- Added HR API helper support for `PUT`, `DELETE` and authenticated downloads.
- Added route registry ownership for Employee Shift Rules and Payroll Finalization.
- Added smoke route coverage for the new HR paths.
- Added `hr-legacy-parity-audit.mjs` so missing legacy HR parity routes are caught by validation.

## Remaining HR Parity Work Before Books

- Upgrade `/hr` from summary-only to full employee master CRUD with employee photo, ID card readiness, bank/document fields, attendance tab and monthly generation controls.
- Upgrade `/payroll` from recent-payslip export to full legacy payroll: salary structures CRUD, salary payment preview/save/edit/delete, payslip print/PDF/email/WhatsApp handoff.
- Replace `/attendance/shifts` placeholder with full shift setup CRUD.
- Verify biometric enrollment, photo review, kiosk monitor, device bridge and mobile kiosk pages against the latest legacy v4.12.69 behavior.
- Run HR browser acceptance on a 14-inch layout after the full employee/payroll forms are ported.

## Validation

Run:

```bash
npm run modular:hr:legacy-parity-audit
npm --prefix frontend/modular run build:hr
```

No database schema change is part of this checkpoint.
