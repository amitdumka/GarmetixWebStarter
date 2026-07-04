# Stage 14B.6 HR Attendance Device Kiosk Readiness

Version: 6.0.14

## Scope

This stage promotes HR attendance device and kiosk routes from placeholder screens to operator-ready readiness consoles.

## Added

- `/attendance/devices`
  - Lists registered attendance devices.
  - Registers new kiosk devices using existing store scope from `/api/stores`.
  - Shows the generated one-time device token immediately after registration.
  - Revokes devices behind a typed `REVOKE DEVICE` confirmation.
- `/attendance/kiosk`
  - Checks kiosk device readiness through `/api/attendance/kiosk/readiness`.
  - Runs safe bootstrap and employee lookup checks.
  - Keeps actual punch submission out of the modular UI until the physical kiosk acceptance passes.
- `/attendance/kiosk-monitor`
  - Loads photo proof review summary, proof rows and kiosk sync batches.
- `/attendance/mobile-kiosk`
  - Displays the MAUI Android kiosk build profile, routes, safety rules and offline queue contract.
- `/attendance/mobile-kiosk-rehearsal`
  - Displays physical tablet rehearsal prerequisites, phases, pass criteria and blockers.
- `/attendance/device-bridge`
  - Displays Mantra/fingerprint bridge status, privacy rules and implementation blockers.
  - Runs simulator capture, identify and enroll only after typing `SIMULATOR`.

## Guardrails

- No schema change.
- No database backup required for this code-only checkpoint.
- Real fingerprint matching remains disabled until the Mantra SDK/service and consent flow pass acceptance.
- Raw biometric storage remains blocked.
- Device registration/revoke and simulator drills are guarded UI actions and rely on existing backend authorization.
- Kiosk punch submission remains hidden in modular HR until physical kiosk acceptance is complete.

## Validation

Run:

```powershell
npm.cmd run modular:hr:device-bridge-readiness
npm.cmd run modular:hr:parity-baseline
npm.cmd run modular:check
npm.cmd --prefix frontend\modular run build:hr
```

## Deployment Cadence

This is checkpoint 2 after the last `.127` deployment. Deployment is deferred until checkpoint 3 unless a risk or user request requires it earlier.

## Next

Stage 14B.7 should focus on payroll approval evidence, salary slip draft readiness and salary payment handoff while keeping actual salary voucher posting behind an explicit live gate.
