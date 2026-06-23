# Stage 12B.6 - POS Held Bills

Version: 5.12.6

## Scope

This stage keeps held bills inside the modular POS frontend as a browser-local draft queue. It does not change the backend API, accounting posting, legacy app, or PostgreSQL schema.

## Added

- `Hold Bill` action on the POS sale page.
- `F9` sale shortcut to park the current cart.
- Browser-local held bill storage under `garmetix.pos.held-bills.v1`.
- Hold Bills page with:
  - customer/mobile/item search,
  - held bill cards,
  - resume action,
  - remove action,
  - clear-all action.
- Resume writes the held draft back to the existing sale draft key and opens `/sale`.

## How To Test

1. Open `/sale`, add one or more products to the cart.
2. Click `Hold Bill` or press `F9`.
3. Open `/hold-bills`.
4. Confirm the held bill appears with customer, quantity, and total.
5. Click `Resume`.
6. Confirm the sale page reloads the parked cart and the held bill is removed from the queue.

## Notes

- Held bills are intentionally local to the POS browser. They are not posted to accounting or inventory until the operator saves the sale invoice.
- Backend persisted held bills can be added later if multi-counter sharing is required.
