# Stage 11D-105 — Day Book + Pending TODO Register

Version: v4.12.20
Build code: GARMETIX-11D105-20260629-4220

## Summary

This stage adds a Tally-style Day Book and a formal pending TODO register for the remaining live QA and production-hardening work.

## Added

- New backend Day Book API:
  - `GET /api/day-book`
  - `GET /api/day-book/{documentType}/{id}`
- New frontend page:
  - `/day-book`
- Sidebar menu:
  - Accounting → Day Book
- Access-control entry for accounting roles.
- Pending TODO document:
  - `docs/todo/GARMETIX-PENDING-TODO-v4.12.20.md`

## Day Book behavior

The Day Book combines date-wise transaction rows from:

- Sale invoices
- Purchase inwards
- Accounting vouchers
- Cash vouchers
- Customer receipt payments
- Vendor purchase payments
- Journal entries

The default filter is Today so it does not load all transactions. It supports Today, Yesterday, Month, Last Month, Year, Month-Year and Custom range, plus type and text search filters.

Selecting a transaction opens a detail drawer directly from Day Book. The row also has an external-link action to jump to the source module route.

## Next part

Stage 11D-106 — Day Book Live QA + Source Page Deep-Link Fixes

This should test Day Book on live data and then improve source-page querystring behavior so `/vouchers?voucherId=...`, `/billing?invoiceId=...` and `/purchase?purchaseInvoiceId=...` auto-open the exact detail/edit drawer in their original modules.
