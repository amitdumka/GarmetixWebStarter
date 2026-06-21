## v4.10.24 Stage 10J Sale Invoice Acceptance Guard

- Current version: 4.10.24 / `GARMETIX-10J-20260620-4124`.
- Added a sale-invoice acceptance guard to current release validation.
- The guard protects the dedicated `/billing/new` full-page workflow, draft persistence, mobile customer lookup, Manager salesman fallback, compact controls, payment bank safety, first-print behavior and modern/legacy menu access.

## Recently Completed Stage 10J Polish

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

1. Stage 10J final bug sweep: run user-reported production flows module by module, starting with sale invoice, petty cash, vouchers, payroll and import/export.
2. Stage 10K production operator acceptance: create a guided page/checklist for daily store operations, billing, cash closing, payroll, backup and restore.
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
