## v4.11.4 Stage 11B-2 Fingerprint Bridge Simulator

- Current version: 4.11.4 / `GARMETIX-11B-20260621-4114`.
- Added simulator health, capture, identify and enroll routes under `/api/attendance/device-bridge/simulator/*`.
- Added simulator controls to `/attendance/device-bridge` so success and controlled failure handshakes can be tested before real SDK work.
- Simulator events write sanitized Message Logs with audit references.
- Kept `fingerprintBridgeEnabled` false until hardware/vendor SDK is selected and approved.
- Raw fingerprint image, minutiae and template storage in Garmetix remain disallowed.

## Recently Completed Stage 10

- v4.11.3: Stage 11B Fingerprint Bridge Contract with adapter candidates, local bridge endpoints, implementation checklist, blockers and privacy guardrails.
- v4.11.2: Stage 11A Physical Tablet Rehearsal with physical Android tablet readiness, lookup, online punch, offline queue, sync and audit checks.
- v4.11.1: Stage 11A Android Build Hardening with `Application.CreateWindow`, APK/AAB build profile and SQLite advisory visibility.
- v4.11.0: Stage 11A MAUI Android Attendance Kiosk Shell with native app scaffold, SQLite pending punch queue, mobile status page and kiosk API contract.
- v4.10.31: Stage 10M Production Rehearsal Tracker for live-data store-day run sheets, blocking checks, issue buckets and go/no-go evidence before Stage 11.
- v4.10.30: Stage 10L Production Support Pack for failed save, failed print, backup warning, email/share failure and hosted API mismatch drills.
- v4.10.29: Stage 10K Production Operator Acceptance checklist for daily store opening, billing, cash closing, purchase, accounting, HR/payroll, backup and support rehearsal.
- v4.10.28: Import/Export transfer guard for hosted-safe upload/download URLs and sanitized CSV filenames.
- v4.10.27: Payroll PDF download guard for salary payment slips and safe SPAY filenames.
- v4.10.26: Voucher PDF download guard for hosted/tunneled deployments and voucher acceptance rules.
- v4.10.25: Petty Cash PDF pagination guard for full A5 transaction details.
- v4.10.24: Sale invoice acceptance guard for the dedicated full-page invoice workflow.
- v4.10.23: Dashboard shell single vertical scroller with preserved horizontal table scroll.
- v4.10.22: Notification routing, read-state and badge reduction.
- v4.10.21: System Info compact header.
- v4.10.20: Oracle status on System Health.
- v4.10.19: Company helper text wrapping and action grouping.
- v4.10.18: Sale invoice Manager fallback and compact save action.
- v4.10.17: Real Excel-compatible CSV import/export engine for setup, products, customers, vendors, stock opening, billing, purchase, HR, payroll, attendance, vouchers, petty cash and access.

### Current Acceptance Commands

```bash
npm.cmd run build
dotnet test backend/Garmetix.Api.Tests/Garmetix.Api.Tests.csproj -c Release
python scripts/validation/current-release-checks.py
```

### Next Recommended Roadmap

1. Select fingerprint hardware/vendor SDK and confirm whether the bridge runs on Windows, Android, or both.
2. Stage 11B-3 Selected vendor adapter for capture/identify/enroll after SDK approval.
3. Wire kiosk punch flow to require fingerprint match for configured stores.
4. Stage 11C Face recognition/liveness proof of concept after consent, retention and privacy controls.
5. Stage 11D mobile/device deployment packaging after kiosk shell and fingerprint bridge are accepted.
6. Stage 12A SaaS/super-admin plan if multi-company hosted licensing is needed.

### Future Attendance/Mobile Items Kept For Later

- Android APK/AAB build rehearsal.
- Physical tablet kiosk test.
- Real face recognition.
- Face liveness detection.
- Fingerprint vendor SDK bridge.
- Raw biometric storage remains disallowed.
