# Stage 11A Physical Tablet Rehearsal v4.11.2

Build: `GARMETIX-11A-20260621-4112`

This package adds the operator-facing rehearsal checklist for testing the MAUI Android Attendance Kiosk on a real tablet.

## Page And API

- Page: `/attendance/mobile-kiosk-rehearsal`
- API: `GET /api/attendance/mobile-kiosk/rehearsal`

The page lists prerequisites, test phases, required evidence, pass criteria, blockers and the next step after passing.

## Rehearsal Scope

- Install and configure the Android APK.
- Run readiness with a registered kiosk device token.
- Search and select an active employee.
- Save one online punch.
- Queue one offline punch while network is disabled.
- Restore network and run Sync Pending.
- Review Kiosk Monitor and Message Logs.

## Go/No-Go Rule

Stage 11B fingerprint hardware bridge should not start until:

- One physical tablet passes readiness, online punch, offline queue and sync.
- Kiosk Monitor shows the expected device and punch evidence.
- Message Logs have no unexplained blocking errors.
- Fingerprint hardware/vendor SDK is selected.
- Raw biometric storage remains disallowed unless a separate privacy review changes that policy.

## Next After This

After the tablet rehearsal passes, freeze Stage 11A as the Android kiosk baseline. Then choose the fingerprint hardware/vendor SDK and implement Stage 11B as a device bridge, not as raw biometric storage inside the main app database.
