## v4.11.0 Stage 11A MAUI Android Attendance Kiosk Shell

- Current version: 4.11.0 / `GARMETIX-11A-20260621-4110`.
- Added native MAUI Android attendance kiosk shell under `apps/Garmetix.AttendanceKiosk`.
- Added local SQLite `pending_punches` queue contract for offline attendance punches.
- Added `/attendance/mobile-kiosk` web status page and Attendance API status/offline-contract endpoints.
- Added current-release validation for Stage 11A shell files, route, menu, access, version and kiosk contract coverage.

## Recently Completed Stage 10

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

1. Stage 11A Android build and hardware rehearsal: install MAUI Android workload, build APK/AAB, test on a physical tablet with registered kiosk device and offline/online sync.
2. Stage 11B Fingerprint device bridge after hardware/vendor SDK selection.
3. Stage 11C Face recognition/liveness proof of concept after consent, retention and privacy controls.
4. Stage 11D mobile/device deployment packaging after kiosk shell is accepted.
5. Stage 12A SaaS/super-admin plan if multi-company hosted licensing is needed.

### Future Attendance/Mobile Items Kept For Later

- Android APK/AAB build rehearsal.
- Physical tablet kiosk test.
- Real face recognition.
- Face liveness detection.
- Fingerprint vendor SDK bridge.
- Raw biometric storage remains disallowed.
