# Stage 8I Package 9 - Store Day Opening / Closing + Cash Details (v4.9.8)

This package implements store-wise day opening and day closing.

## Added

New API:

- `GET /api/store-day/status`
- `GET /api/store-day/book-summary`
- `POST /api/store-day/open`
- `POST /api/store-day/close`
- `POST /api/store-day/holiday`

New page:

- `Store Day Open / Close`

## Store Day Opening

Each store can open a day with:

- Date
- Opening balance
- Cash denomination details

Opening balance defaults from previous closing / petty cash sheet.

## Store Day Closing

Day closing:

- Calculates book cash from store transactions.
- Takes physical cash denomination details.
- Creates/updates Cash Details.
- Creates/updates Day Closing.
- Creates/updates Petty Cash Sheet.
- Provides Petty Cash print link.

## Store Holiday / Closed Day

For non-operational store dates:

- Carries forward previous closing balance.
- Creates opening and closing.
- Creates petty cash sheet with zero movement.
- Blocks entries for that date.

## Mandatory Entry Guard

For Store Manager and Billing/Sales users only:

- Day must be opened before daily entries.
- Entries are blocked after day closing.
- Admin, Owner, Accountant and CA users are not blocked by this guard.

Guarded entry areas include billing, sales invoices, purchase, vouchers, cash vouchers, stock operations, tailoring and petty cash.
