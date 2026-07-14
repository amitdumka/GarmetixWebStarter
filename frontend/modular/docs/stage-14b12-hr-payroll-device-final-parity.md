# Stage 14B.12 - HR Payroll Device Final Parity

Version: 6.0.21

## Scope

- Replaced the modular payroll summary page with full payroll operations:
  - recent payslips with CSV/PDF/email/WhatsApp handoff
  - salary structures create, edit and delete
  - salary payments create, edit, delete and PDF handoff
  - salary payment preview using advance recovery, previous due, outstanding and round-off fields
  - monthly payslip generation through the existing guarded backend endpoint
- Replaced remaining HR placeholders:
  - biometric enrollment save/revoke with consent and reference-only storage
  - photo proof review with approve/reject/flag/regularization actions
  - face liveness readiness and simulator proof/verify drills
- Extended HR smoke route ownership to include `/hr-benefits`, `/attendance`, `/attendance/policies`, `/attendance/biometric-enrollment`, `/attendance/photo-review` and `/attendance/face-liveness`.

## Safety

- Backend and database schema were not changed.
- Live write actions remain behind explicit UI intent:
  - biometric enrollment requires `ENROLL`
  - photo proof review requires `REVIEW`
  - device pages keep existing confirmation guards
- Payroll payment writes use the existing backend validation and PDF endpoints.
- No database backup is required for this checkpoint because no schema or seed/data migration was introduced.

## Validation

- `npm run modular:hr:payroll-device-final-parity`
- `npm run modular:check`
- `npm --prefix frontend/modular run build:hr`

## Next

After deploy acceptance on `.127`, resume Books Stage 14C.2: voucher and ledger parity with Indian accounting checks, print/download handoff and bank-ledger background rules.
