# Purchase Import Final Closure - v4.12.39

Stage 11D-124 Purchase Import Final Closure + Move Outside Module

## What changed

- Added `GET /api/purchase-import/final-closure-status`.
- Added a visible **Purchase Import final closure** card on Purchase → Import Acceptance.
- The card shows a binary **Complete / Not Complete** status using real import data, with `completionMode` explaining whether it is blocked or only awaiting operator QA.
- Added final closeout checklist covering real-invoice Pass evidence, untested posted imports, correction queue, draft queue, parser history, protected proof storage and backup/restore coverage.
- Added visible known limitations and operator rules so the module can be closed without hiding manual review requirements.
- Added next outside-module candidates so work can move out of Purchase Import in a controlled way.
- Fixed a duplicate table-row closing tag in the Import Acceptance recent imports table.

## Closure status rules

`Complete` is shown only when no blocking or warning item remains in the closeout checklist.

`Not Complete` is shown when any blocker or warning remains, such as no real supplier invoice marked Pass, a posted import still needing acceptance, a Correction Required queue, a NeedsReview draft queue, or missing posted proof protection.

## Known limitations now shown in-app

- OCR/parser accuracy still depends on supplier invoice scan quality.
- Posted imports cannot be directly undone/deleted; correction must go through purchase revision/return/reversal.
- Unusual supplier layouts may need vendor learning or manual draft correction.
- PostgreSQL dump alone is not enough; purchase-import proof files must be backed up and restored.
- Restore drill still requires an operator test on a disposable/test system.
- Accountant review remains required for GST input eligibility, supplier credit/debit notes and statutory filing decisions.

## Test checklist

1. Run `docker compose up --build`.
2. Open Purchase → Import Acceptance.
3. Confirm the new **Purchase Import final closure** card loads.
4. Confirm the status shows Complete / Not Complete / Operator QA Pending based on data.
5. Confirm known limitations and operator rules are visible.
6. Confirm Parser QA, Backup/Restore checklist and Correction/Revision Plan still load.
7. Open a posted import and click Plan; direct undo must remain blocked.
8. Run `./scripts/linux/create-database-backup-now.sh .env.production` and confirm proof archive is created when proof files exist.
9. Restore on a test system and confirm supplier proof opens from a posted purchase inward.

## Recommended next outside module

Move to one of these based on production urgency:

1. Vyapar Sale Import final summary/reconciliation.
2. Attendance/Payroll real-month validation and correction audit history.
3. Day Book export/source-page anchors.
4. Print/PDF final evidence on real invoices.
5. Accounting/GST validation after real imports.
