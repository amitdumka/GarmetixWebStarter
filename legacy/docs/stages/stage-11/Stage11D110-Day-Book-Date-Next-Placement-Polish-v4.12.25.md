# Stage 11D-110 — Day Book Date Next Placement Polish

Version: v4.12.25  
Build: GARMETIX-11D110-20260630-4225  
Date: 2026-06-30

## Scope

This stage polishes the Day Book date controls after v4.12.24.

## Completed

- Kept the top **New +** quick-create button for creating transactions.
- Kept **− Previous** beside the Book date field.
- Moved **+ Next** to the action row immediately after **Today**.
- Removed the two explanatory Day Book badges/tags from the user screen:
  - Journal-hidden-by-default message.
  - Previous/Next Tally-style instruction message.
- Kept journal entries hidden by default with the **Show journal entries** checkbox still available.
- Kept formatted transaction detail tables instead of raw JSON.

## Manual QA

1. Open `/day-book`.
2. Confirm the top header still has **New +** for quick creation.
3. Confirm **Book date** shows **− Previous** and the date field only.
4. Confirm the action row shows **Apply**, **Today**, then **+ Next** immediately after **Today**.
5. Confirm the two explanatory badges/tags are not visible.
6. Confirm **Show journal entries** is still available and unchecked by default.
7. Open any row and confirm details show user-readable tables, not JSON.


Release check phrase: + Next immediately after Today; explanatory badges removed.

## Validation

Run:

```bash
python3 scripts/validation/current-release-checks.py
```
