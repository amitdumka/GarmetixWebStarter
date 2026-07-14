# Stage 10B - Excel Import / Export Center v4.10.12

Version: `4.10.12`  
Stage: `Stage 10B Excel Import Export Center`  
Build: `GARMETIX-10B-20260620-4112`

## Purpose

Stage 10B fixes the `/production-final-acceptance` page so it does not become unusable when one readiness API returns an error, then upgrades the existing Import / Export page into an Excel-compatible CSV Import / Export Center.

## Production Final Acceptance hotfix

- `/api/stage10a/final-acceptance` and `/api/stage10a/final-acceptance/checklist` now return safe degraded JSON instead of throwing a page-breaking 500 when the manifest/checklist builder hits an unexpected issue.
- `/production-final-acceptance` now uses `Promise.allSettled` so one failed check is shown as a warning while the rest of the page still loads.

## Excel Import / Export Center

- Existing `/import-export` page is now labelled **Excel Import / Export Center**.
- Added `/api/import-export/center` for dashboard counts and safe workflow guidance.
- Added `/api/import-export/health` for acceptance checks.
- Added Attendance punch export/template/import support.
- Import workflow remains safe: download template, fill in Excel, save as CSV, validate without commit, then commit only when there are zero row errors.

## Added validation

- `scripts/validation/stage10b-import-export-center-check.py`
- `scripts/linux/stage10b-import-export-center-drill.sh`
- Test Automation manifest item: `STAGE10B_IMPORT_EXPORT_CENTER`
