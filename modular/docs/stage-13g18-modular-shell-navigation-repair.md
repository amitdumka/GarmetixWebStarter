# Stage 13G.18 - Modular Shell Navigation Repair

Version: 5.13.58

## Fixed

- Cross-app module switching now uses absolute app base paths instead of Nuxt in-app routing.
- Branch apps no longer generate nested URLs such as `/hr/books/`, `/hr/pos/` or `/pos/ai-sense/`.
- The top-right profile action now opens Back Office profile instead of app-local missing routes such as `/admin/profile`.
- The extra horizontal Back Office/POS/HR/AI Sense/Books/Admin toolbar was removed; the app switcher remains in the Garmetix sidebar header dropdown.
- The sidebar collapse sizing is tightened so collapse should remain icon-width on desktop.
- The sidebar footer Status menu now includes legacy-style operational actions:
  - System Health
  - Runtime Diagnostics
  - Backup Maintenance
  - Google Drive Backup
  - Production Readiness
  - Production Support
  - Oracle Sync
  - Message Logs
  - About Version

## Validation Plan

- Build all modular apps.
- Deploy SRP.
- Check every app root and representative cross-app paths.
- Verify public app HTML has no 500 text and app titles remain correct.
