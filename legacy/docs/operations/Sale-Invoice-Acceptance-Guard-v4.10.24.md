# Sale Invoice Acceptance Guard v4.10.24

This package adds a static release gate for the sale invoice workflow so future UI or backend changes do not accidentally break the full-page billing flow.

## Protected Behavior

- `/billing/new` remains a dedicated full page inside `AppShell`, not a modal or narrow side drawer.
- Draft data is saved, restored and cleared after successful save.
- Customer lookup stays mobile-first and can create a customer automatically with the invoice.
- Manager is selected/fallback-created as the default salesman when a store has no active salesman master.
- Item, discount, payment and save controls remain compact for laptop-width screens.
- Non-cash payments require bank/reference context.
- A newly saved invoice starts authenticated server-PDF printing, then the page resets for the next invoice.
- Modern and legacy navigation both expose **New Sale Invoice**.

## Validation

```bash
python scripts/validation/sale-invoice-acceptance-check.py
python scripts/validation/current-release-checks.py
```
