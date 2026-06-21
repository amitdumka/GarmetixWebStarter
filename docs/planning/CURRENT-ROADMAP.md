## v4.10.31 Stage 10M Production Rehearsal Tracker

- Current version: 4.10.31 / `GARMETIX-10M-20260620-4131`.
- Added a Production Rehearsal page for live-data store-day run sheets before Stage 11 mobile/device work.
- Added `/api/stage10m/production-rehearsal` and `/api/stage10m/production-rehearsal/run-sheet` with phases, blocking checks and issue buckets.
- Added current-release validation for Stage 10M route, menu, access, version and rehearsal coverage.

## Recently Completed Stage 10

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

1. Stage 10M live-data rehearsal fixes: run `/production-rehearsal` on a real store-day dataset and fix any blocking issue found.
2. Stage 11A MAUI/Android Attendance Kiosk shell with local SQLite offline queue.
3. Stage 11B Fingerprint device bridge after hardware/vendor SDK selection.
4. Stage 11C Face recognition/liveness proof of concept after consent, retention and privacy controls.
5. Stage 11D mobile/device deployment packaging after kiosk shell is accepted.

### Future Attendance/Mobile Items Kept For Later

- MAUI Android kiosk app.
- Local SQLite offline queue.
- Real face recognition.
- Face liveness detection.
- Fingerprint vendor SDK bridge.
- Raw biometric storage remains disallowed.
