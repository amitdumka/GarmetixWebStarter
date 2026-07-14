# v4.11.90 Purchase Import Discount Reconciliation

## Purpose
Supplier purchase invoices may show discount in different ways:

- discount percentage only
- discount amount only
- discount percentage plus additional discount amount
- discount applied on basic amount before GST
- final line amount printed after GST

This stage improves purchase invoice import so scanned line totals reconcile with the supplier invoice instead of losing the discount column.

## What changed

- Garment/Tally column parser now keeps the printed rate as cost price before discount.
- Line discount is calculated as gross basic amount minus printed taxable/basic line amount.
- Unit discount and line discount are populated on import draft lines.
- Parser review message shows captured discount amount and percentage.
- If the invoice header discount is already fully represented by parsed line discounts, header discount is cleared from the draft so posting is not blocked by duplicate discount distribution.
- Manual reparse uses the same discount reconciliation logic.

## Example: S.K APPARELS line

Invoice row:

```text
Qty 6, Rate 171.00, Gross 1026.00, Discount 5%, GST 5%, Amount Without GST 974.70
```

Imported draft line:

```text
Cost price: 171.00
Line discount: 51.30
Unit discount: 8.55
Taxable amount: 974.70
GST mode: Exclusive
GST: 5%
```

This keeps the supplier discount visible and still posts through purchase inward using the existing tax-inclusive posting adapter.

## Test checklist

1. Upload S.K APPARELS invoice.
2. Confirm line 1 imports with cost 171.00 and line discount 51.30.
3. Confirm line 2 imports with cost 1395.00 and line discount 627.75.
4. Confirm header discount does not block posting when line discounts already total the scanned header discount.
5. Confirm calculated bill matches scanned grand total within round-off tolerance.
6. Post inward and verify stock, vendor balance, GST and accounting.
