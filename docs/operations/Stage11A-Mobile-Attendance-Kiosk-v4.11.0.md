# Stage 11A Mobile Attendance Kiosk v4.11.0

Build: `GARMETIX-11A-20260621-4110`

Stage 11A starts the native Android attendance kiosk app without changing the production web attendance workflow. The app shell is kept in `apps/Garmetix.AttendanceKiosk` and uses the existing attendance kiosk API contract.

## App Path

- `apps/Garmetix.AttendanceKiosk/Garmetix.AttendanceKiosk.csproj`
- Target framework: `net10.0-android`
- UI entry page: `apps/Garmetix.AttendanceKiosk/Views/KioskShellPage.cs`
- API client: `apps/Garmetix.AttendanceKiosk/Services/KioskApiClient.cs`
- Offline queue: `apps/Garmetix.AttendanceKiosk/Services/OfflinePunchQueue.cs`

## Web And API Status

- Page: `/attendance/mobile-kiosk`
- Status API: `GET /api/attendance/mobile-kiosk/status`
- Offline contract API: `GET /api/attendance/mobile-kiosk/offline-contract`

The status page lists the mobile shell files, kiosk API routes, SQLite queue contract, safety rules and acceptance checks so the Android build can be rehearsed later without guessing the contract.

## SQLite Offline Queue

Local database: `garmetix-kiosk-queue.db`

Table: `pending_punches`

Columns:

- `id`
- `employeeCode`
- `punchType`
- `punchedAtUtc`
- `storeId`
- `deviceId`
- `deviceToken`
- `photoProofBase64`
- `notes`
- `attempts`
- `lastError`
- `createdAtUtc`

Failed punch submissions are queued locally. Sync sends pending rows to `/api/attendance/kiosk/sync-pending` and marks rows as synced only after the server confirms success.

## Acceptance Checks

- Configure API base, device id and device token on the tablet.
- Run readiness against `/api/attendance/kiosk/readiness`.
- Search an employee using `/api/attendance/kiosk/lookup-employee`.
- Submit check-in/check-out online.
- Disable network, submit a punch and confirm it is queued locally.
- Restore network, run sync and confirm the pending queue is cleared.

## Not Included Yet

- Android APK/AAB build and signing rehearsal.
- Fingerprint hardware bridge.
- Face recognition or liveness verification.
- Raw biometric template storage.

Raw biometric storage remains disallowed. Future device integrations should store only consent-aware proof references and audit metadata unless a separate privacy review approves otherwise.
