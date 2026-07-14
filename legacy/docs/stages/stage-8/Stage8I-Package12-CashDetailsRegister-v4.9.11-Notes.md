# Stage 8I Package 12 - Cash Details Register + Notes/Coin History (v4.9.11)

This package adds a dedicated Cash Details page and API.

## Added page

```text
Accounting → Cash Details
```

## Added API

- `GET /api/cash-details`
- `GET /api/cash-details/history`
- `GET /api/cash-details/{id}`
- `POST /api/cash-details`
- `PUT /api/cash-details/{id}`
- `DELETE /api/cash-details/{id}`

## Features

- List cash details by store/date/source.
- Add manual cash-flow denomination record.
- Edit cash denomination notes/coins.
- Delete manual rows.
- View notes/coin history totals.
- Filter by source:
  - `ManualCashFlow`
  - `DayOpening`
  - `DayClosing`
  - `StoreHoliday`
  - `CashVerification`

## Linked day records

If cash detail is linked to Day Opening:

- Editing cash notes updates linked `DayBegin.OpeningBalance`.

If cash detail is linked to Day Closing:

- Editing cash notes updates linked `DayEnd.ClosingBalance`.
- Generated `PettyCashSheet.CashInHand` is updated when applicable.

Delete is blocked for linked opening/closing rows to avoid broken day records. To remove a day close, use Store Day → Reopen Day / Delete Close.
