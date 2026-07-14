# Stage 11D-138 — Shell Layout and Kiosk Monitor Fix — v4.12.53

## Scope
This stage closes the page-shell regression where selected direct routes opened without the Garmetix header/sidebar/navigation and adds a defensive fix for Attendance Kiosk Monitor API loading on live databases.

## Fixed
- Wrapped Dot Matrix Print in `AppShell`.
- Wrapped listed acceptance/GST/backup/audit/diagnostic pages in `AppShell`.
- Verified all Maintenance submenu pages now use `AppShell`.
- Carried Epson LX-310 linefeed bridge hotfix: Epson reset plus CRLF line endings before sending raw output to CUPS.
- Updated Dot Matrix page defaults to `EPSON_LX310`.
- Hardened `/api/attendance/photo-proofs` and `/api/attendance/sync-batches` for live schema drift.
- Made Attendance Kiosk Monitor load photo proofs and sync batches independently.

## Deployment
Use normal app rebuild/redeployment because this includes frontend and API changes.
