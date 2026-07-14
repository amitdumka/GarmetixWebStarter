# Stage 12B.7 - POS Day Open Close

Version: 5.12.7

## Scope

This stage connects the modular POS Day Open and Day Close pages to the existing ASP.NET Core `store-day` API. It does not change backend endpoints, database schema, or the legacy Store Operations page.

## Added

- POS Day Open page:
  - store/date status loading,
  - opening balance,
  - cash denomination entry,
  - Open / Update Day API call,
  - Holiday / Closed Day API call,
  - quick navigation to sale when entries are allowed.
- POS Day Close page:
  - store/date status loading,
  - physical cash denomination entry,
  - book cash and difference display,
  - editable petty cash preview values,
  - Confirm Close Day API call,
  - opening mismatch confirmation handling,
  - Reopen Day and Delete Close correction actions,
  - petty cash PDF print action.
- Shared POS store-day utility for cash denomination and petty cash calculations.

## Existing API Used

- `GET /api/store-day/status`
- `POST /api/store-day/open`
- `POST /api/store-day/holiday`
- `POST /api/store-day/close`
- `POST /api/store-day/reopen`
- `POST /api/store-day/delete-close`
- `GET /api/petty-cash-sheets/{id}/pdf`

## How To Test

1. Open `/day-open`, select a store/date, enter opening cash, and click Open / Update Day.
2. Confirm `/sale` becomes available when the API returns entry allowed.
3. Open `/day-close`, select the same store/date, review book cash, enter counted notes, and close the day.
4. If the API reports opening mismatch, confirm the checkbox and close again.
5. Print the petty cash PDF after closing.
6. Use Reopen or Delete Close only for correction testing.

## Notes

- Day Open and Day Close intentionally reuse the same store-day API as the legacy app, so the backend remains unified.
- Petty cash values can be edited before close, matching the legacy preview workflow.
