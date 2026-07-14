# Stage 14B.1 HR Parity Baseline

Version: 6.0.9

## Purpose

Start the HR modular parity lane from the Version6 base after the POS-first lane closure. This stage is non-mutating and focuses on route ownership, existing HR safety gates, and API path correctness.

## What This Stage Adds

- HR Version6 parity baseline gate.
- HR API path normalization so existing page calls like `api/attendance/today` do not become `/api/api/attendance/today` when the configured API base URL already ends with `/api`.
- Documentation for the HR lane entry point.

## Covered HR Areas

- Employee summary.
- Attendance today and monthly attendance.
- Payroll summary and payroll review.
- Salary draft and salary payment preview.
- Regularization review.
- Attendance devices, device bridge, biometric enrollment and face/liveness readiness.

## Commands

```powershell
npm.cmd run modular:hr:parity-baseline
npm.cmd run modular:hr:payroll-readiness
npm.cmd run modular:hr:stage13c-closure
npm.cmd --prefix frontend/modular run build:hr
```

## Safety

This stage does not create attendance, salary payments, payslips, vouchers or employee records. It only repairs frontend API path composition and adds validation.

## Deployment

This is the third checkpoint after the last `.127` deploy, so deploy is allowed by cadence after local HR build passes. No database backup is required because no backend schema or live data mutation is included.

## Next Stage

Stage 14B.2 should repair and verify HR salary payment preview behavior in the modular UI against the current backend contract, keeping actual payment generation behind explicit live-write gates.
