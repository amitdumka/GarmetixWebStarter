# Stage 11D-106 — Day Book Date Navigation

Version: v4.12.21  
Base: v4.12.20 Stage 11D-105 Day Book Pending TODO Register

## Purpose

Make Day Book work like a daily accounting book: choose any date, use **−** to go to previous date, and use **+** to go to next date.

## Implemented

- Day Book now defaults to **Single date** instead of a generic Today preset.
- Added `Book date` input on `/day-book`.
- Added **−** previous-date button.
- Added **+** next-date button.
- Added Today shortcut that resets Book date to today.
- Backend supports exact single-day queries using `datePreset=date&date=YYYY-MM-DD`.
- Existing Today/Yesterday/Month/Last Month/Year/Month-Year/Custom filters remain available.
- Existing transaction type/search/pagination filters remain available.

## Files changed

- `backend/Garmetix.Api/DayBook/DayBookEndpoints.cs`
- `frontend/garmetix-web/pages/day-book/index.vue`
- version/app-info files
- validation script

## Next part

Stage 11D-107 — Day Book Source Page Deep-Link Fixes.

Make source pages auto-open the exact transaction drawer when launched from Day Book links.
