## v4.10.29 Stage 10K Production Operator Acceptance

- Current version: 4.10.29 / `GARMETIX-10K-20260620-4129`.
- Added a production operator acceptance page for daily store opening, billing, cash closing, purchase, voucher/accounting, HR/payroll, backup and support rehearsal.
- Added `/api/stage10k/operator-acceptance` and `/api/stage10k/operator-acceptance/checklist` for the guided acceptance contract.
- Added current-release validation for Stage 10K route, menu, access, version and checklist coverage.

## Recently Completed Stage 10J Polish

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

1. Stage 10K operator rehearsal hardening: run the checklist on a real store-day dataset and fix any save/print/access issue found.
2. Stage 10L production support pack: add operator-friendly troubleshooting drill pages for failed save, failed print, backup warning, email failure and tunnel/API mismatch.
3. Stage 11A MAUI/Android Attendance Kiosk shell with local SQLite offline queue.
4. Stage 11B Fingerprint device bridge after hardware/vendor SDK selection.
5. Stage 11C Face recognition/liveness proof of concept after consent, retention and privacy controls.

### Future Attendance/Mobile Items Kept For Later

- MAUI Android kiosk app.
- Local SQLite offline queue.
- Real face recognition.
- Face liveness detection.
- Fingerprint vendor SDK bridge.
- Raw biometric storage remains disallowed.
