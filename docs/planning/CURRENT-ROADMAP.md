## v4.10.15 Stage 10H Runtime Bug Fix Pack

- Added menu coverage for every concrete Nuxt page in both modern and legacy sidebars.
- Added direct create-page links for New Sale Invoice, New Customer, New Debit Note and New Credit Note.
- Added validation so future pages cannot be added without sidebar/menu discovery.


## v4.10.13 Stage 10 Complete Final Acceptance

Stage 10 is completed with barcode print final acceptance, GST/e-Invoice production readiness, Google Drive backup sync foundation, audit trail/change-history final acceptance, and a combined Stage 10 final acceptance gate.

New pages: `/barcode-final-acceptance`, `/gst-production`, `/google-drive-backup`, `/audit-trail-final`, `/stage10-final-acceptance`.

## Stage 10B Excel Import Export Center / v4.10.12

- Current version: 4.10.12 / `GARMETIX-10B-20260620-4112`.
- Fixed `/production-final-acceptance` to fail safely instead of breaking the page when one acceptance check errors.
- Upgraded `/import-export` into an Excel-compatible CSV Import / Export Center.
- Added attendance punch template/export/import support.
- Next recommended package: Stage 10C Barcode Print Final Acceptance.

## Stage 10B Excel Import Export Center / v4.10.12

- Current version: 4.10.12 / `GARMETIX-10B-20260620-4112`.
- Stage 9 attendance and Stage 9K Today&apos;s Dashboard are complete.
- Stage 10A adds a consolidated production final acceptance gate before new major module work.
- The final gate checks build/deploy, database upgrade, core business flows, attendance/payroll, Today&apos;s Dashboard, security and recovery.

### Current acceptance commands

```bash
python3 scripts/validation/current-release-checks.py
export GARMETIX_SMOKE_USER='admin'
export GARMETIX_SMOKE_PASSWORD='your-admin-password'
./scripts/linux/stage10a-production-final-acceptance-drill.sh .env.production
```

### Next recommended roadmap

1. Stage 10B - Excel Import / Export Center.
2. Stage 10C - Barcode Print Final Acceptance.
3. Stage 10D - GST and E-Invoice Production Integration.
4. Stage 10E - Google Drive Backup Sync.
5. Stage 10F - Audit Trail and Change History.
6. Stage 11A - MAUI Android Attendance Kiosk App shell with local SQLite offline queue.
7. Stage 11B - Fingerprint Device Bridge after hardware/vendor SDK selection.
8. Stage 11C - Face recognition/liveness proof of concept after consent/retention controls.

### Future attendance/mobile items kept for later

- MAUI Android kiosk app.
- Local SQLite offline queue.
- Real face recognition.
- Face liveness detection.
- Fingerprint vendor SDK bridge.
- Raw biometric storage remains disallowed.
