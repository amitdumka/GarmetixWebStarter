# Attendance Core Stage 9A

Stage 9A adds the foundation for Garmetix attendance without committing to face-recognition or fingerprint hardware yet.

## Admin pages

- `/attendance` - dashboard and manual punch
- `/attendance/today` - daily status
- `/attendance/monthly` - month grid and recalculation
- `/attendance/shifts` - shift setup
- `/attendance/policies` - rules and duplicate punch window
- `/attendance/devices` - kiosk device registration/revocation
- `/attendance/biometric-enrollment` - privacy-safe placeholder
- `/attendance/regularization` - correction approval queue
- `/attendance/payroll-summary` - payroll review output only

## Kiosk API base

- `POST /api/attendance/kiosk/bootstrap`
- `POST /api/attendance/kiosk/lookup-employee`
- `POST /api/attendance/kiosk/punch`
- `POST /api/attendance/kiosk/sync-pending`

Registered kiosks must use `DeviceId + DeviceToken`. Revoked devices cannot punch.

## Not included in Stage 9A

- .NET MAUI kiosk app project
- real face recognition
- fingerprint matching or device SDK
- raw fingerprint image storage
- automatic payroll deduction/posting
