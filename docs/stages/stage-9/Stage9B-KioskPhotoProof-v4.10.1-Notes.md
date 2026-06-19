# Stage 9B - Kiosk Photo Proof v4.10.1

Version: `4.10.1`  
Stage: `Stage 9B Kiosk Photo Proof`  
Release: `Kiosk Photo Proof and Offline Sync Foundation`  
Build code: `GARMETIX-9B-20260619-4101`

## Goal

Stage 9B builds on Stage 9A Attendance Core by adding the first usable kiosk experience inside the web app and a privacy-safe photo proof workflow.

## Included

- Web/PWA kiosk page at `/attendance/kiosk`.
- Kiosk readiness check using registered device ID and token.
- Employee lookup from kiosk device scope.
- Camera capture in browser using HTTPS/localhost camera permission.
- Photo proof upload endpoint: `POST /api/attendance/kiosk/photo-proof`.
- Private photo-proof storage path with Docker volume.
- Photo proof metadata table: `AttendancePhotoProofs`.
- Offline sync batch audit table: `AttendanceKioskSyncBatches`.
- Kiosk monitor page at `/attendance/kiosk-monitor`.
- Acceptance drill: `scripts/linux/attendance-kiosk-photo-proof-drill.sh`.

## Not included

- Real face recognition.
- Face liveness detection.
- Raw fingerprint image storage.
- Fingerprint vendor SDK or bridge service.
- Payroll auto deduction.
- Native .NET MAUI Android kiosk app.

## Privacy note

The photo captured in Stage 9B is only evidence/proof for manager review. It is not biometric matching. Real biometric enrollment, liveness, and fingerprint matching must be implemented later with consent, retention, access control, audit, and deletion policies.
