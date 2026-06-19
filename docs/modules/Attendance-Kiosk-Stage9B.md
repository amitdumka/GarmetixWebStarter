# Attendance Kiosk Stage 9B

Stage 9B adds a web kiosk foundation for store attendance.

## Pages

- `/attendance/kiosk` - web/PWA kiosk screen.
- `/attendance/kiosk-monitor` - photo proof and offline sync audit monitor.
- `/attendance/devices` - register kiosk device and copy one-time device token.

## Kiosk flow

1. Register device from `/attendance/devices`.
2. Copy Device ID and Device Token to `/attendance/kiosk`.
3. Check readiness.
4. Search employee by code/mobile/name.
5. Start camera and capture photo proof.
6. Punch Check In / Check Out / Auto.
7. If live save fails, punch is queued in browser local storage.
8. Use Sync Pending to retry.

## Backend APIs

- `POST /api/attendance/kiosk/readiness`
- `POST /api/attendance/kiosk/photo-proof`
- `POST /api/attendance/kiosk/punch`
- `POST /api/attendance/kiosk/sync-pending`
- `GET /api/attendance/photo-proofs`
- `GET /api/attendance/sync-batches`

## Storage

Photo proof files are stored privately under:

```text
/app/attendance-photo-proof
```

Docker maps this to:

```text
./attendance-photo-proof:/app/attendance-photo-proof
```

## Configuration

```text
ATTENDANCE_PHOTO_PROOF_MAX_BYTES=1500000
ATTENDANCE_PHOTO_PROOF_RETENTION_DAYS=180
```

## Later packages

- Stage 9C: face verification review/liveness placeholder or device bridge planning.
- Stage 9D: fingerprint bridge foundation.
- Later: attendance payroll deduction/posting.
