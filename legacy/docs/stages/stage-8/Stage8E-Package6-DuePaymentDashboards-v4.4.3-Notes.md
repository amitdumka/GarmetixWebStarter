# Stage 8E Package 6 - Due and Payment Dashboards (v4.4.3)

Build code: `GARMETIX-8E-20260617-4430`

## Scope

This package completes the remaining Stage 8E dashboard analytics item by adding business-facing receivable, payable, cash/payment and store-group comparison views.

## Delivered

- Customer dues are calculated from sales invoices where bill amount exceeds paid amount.
- Vendor dues are calculated from purchase invoices less posted purchase payments.
- Cash/payment summary combines invoice collections, purchase payments and accounting vouchers by payment mode.
- Store-group comparison combines sales, purchase, customer due, vendor due, cash in/out, net cash and stock value.
- Business dashboard export includes the new due, payment and comparison tables.

## Operational notes

- The Mac mini deployment default remains `RESET_DATABASE_ON_DEPLOY=false`.
- This stage adds analytics only; no schema reset or fresh migration change is required.
