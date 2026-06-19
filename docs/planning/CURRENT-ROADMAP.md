## Stage 9D Attendance Payroll Integration Foundation / v4.10.4

- Current version: 4.10.4 / `GARMETIX-9D-20260619-4104`.
- Current package: Stage 9C Face Photo Review and Attendance Approval Foundation.
- Completed: manager review queue for kiosk photo proofs, approve/reject/flag status, regularization linkage, retention visibility, schema repair, route access and acceptance drill.
- Still intentionally pending: real AI face recognition, liveness detection, fingerprint bridge, MAUI kiosk app and payroll auto-deduction.

## Next recommended package

Stage 9D should focus on Attendance Device Bridge/Fingerprint Planning or Stage 9D Attendance Payroll Integration Foundation, depending on hardware readiness.

## Previous package: Stage 9B Kiosk Photo Proof / v4.10.1

- Previous version: 4.10.1 / `GARMETIX-9B-20260619-4101`.
- Previous package: Stage 9B Kiosk Photo Proof and Offline Sync Foundation.

### Completed

- Attendance Core models, APIs, shifts, policies, devices, regularization and monthly summary from Stage 9A.
- Web/PWA kiosk route `/attendance/kiosk`.
- Kiosk readiness endpoint.
- Browser camera photo capture UI.
- Photo proof upload endpoint and private storage metadata.
- Offline pending queue in browser local storage.
- Kiosk sync batch audit table and monitor page.
- Kiosk monitor route `/attendance/kiosk-monitor`.

### Not included yet

- Native .NET MAUI Android kiosk app.
- Full offline SQLite queue in native app.
- Real face recognition/liveness.
- Fingerprint vendor SDK bridge.
- Automatic payroll deduction/posting from attendance.

### Recommended next package

`Stage 9C - Face Verification Review Foundation v4.10.4`

Scope:

- Manager review workflow for captured face/photo proof.
- Approve/reject photo proof attendance.
- Mismatch/impersonation flag.
- Photo proof retention controls.
- Employee consent visibility.
- Still no real AI face matching.

Alternative if hardware priority is higher:

`Stage 9C - Device Bridge Planning v4.10.4`

Scope:

- Fingerprint bridge contract.
- Device event import API.
- Vendor device mapping.
- No SDK-specific implementation yet.
