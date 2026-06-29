# Stage 13C.1 HR Payroll Readiness Foundation

Version: 5.13.19
Branch: Version5

## Scope

Stage 13C starts the HR/payroll attendance hardening lane. This slice adds a repeatable readiness check for the modular HR app routes and the shared ASP.NET API endpoints used by attendance, payroll review, salary payment preview, attendance devices, fingerprint bridge status, face/liveness status, payslips, and salary payments.

The check is non-mutating by default and in live mode. It only uses GET requests. It does not rebuild monthly attendance, post payroll review rows, generate payslips, create salary payment vouchers, enroll biometrics, or call device capture APIs.

## Commands

Dry run from the repository root:

```powershell
npm.cmd run modular:hr:payroll-readiness
```

Live local API check with an HR/payroll-capable token:

```powershell
[Environment]::SetEnvironmentVariable('GARMETIX_SMOKE_AUTH_TOKEN', '<token>', 'Process')
npm.cmd run modular:hr:payroll-readiness -- --live --require-token --strict-permissions
```

Public tunnel check:

```powershell
[Environment]::SetEnvironmentVariable('GARMETIX_SMOKE_AUTH_TOKEN', '<token>', 'Process')
npm.cmd run modular:hr:payroll-readiness -- --live --mode=public --require-token --strict-permissions
```

Use `--year=2026 --month=6` to check a specific salary month.

## Safety Rules

- One shared ASP.NET Core API remains the only backend.
- One PostgreSQL database remains the only database.
- No production domain is hardcoded in app code; smoke hosts remain centralized in `modular/scripts/smoke-routes.mjs`.
- No password, SSH credential, token, or tunnel secret is stored in the script.
- Permission failures are warnings by default so a generic token can still confirm route presence. Use `--strict-permissions` for HR acceptance.
- Missing routes and server errors fail the check because they indicate a broken contract.

## Covered API Surface

- `GET /api/attendance/today`
- `GET /api/attendance/monthly`
- `GET /api/attendance/payroll-summary`
- `GET /api/attendance/payroll-review`
- `GET /api/attendance/salary-payment-candidates`
- `GET /api/attendance/devices`
- `GET /api/attendance/device-bridge/status`
- `GET /api/attendance/face-liveness/status`
- `GET /api/payroll/payslips/recent`
- `GET /api/salary-payments`

## Next Follow-Ups

- Stage 13C.2: add HR attendance contract checks for monthly attendance, payroll review, salary draft and salary payment DTO field expectations.
- Stage 13C.3: add safe browser acceptance for HR routes on 14 inch laptop layouts.
- Stage 13C.4: add Mantra/fingerprint bridge readiness docs and simulator checks without raw biometric storage.
- Stage 13C.5: add controlled live payroll preview validation without creating vouchers.
