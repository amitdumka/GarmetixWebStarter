# Stage 11D-21 Vendor Endpoint and Schema Drift Fix - v4.11.36

Build: `GARMETIX-11D21-20260625-4136`

## Fixed

- Removed duplicate generic `MapCrud<Vendor>` mapping for `/api/vendors` so the custom vendor master endpoint is the only handler. This prevents ambiguous endpoint/runtime 500 errors when opening Purchase → Vendors or Brand supplier dropdowns.
- Updated database schema drift repair to stop trying to `ALTER TABLE "PurchaseInvoiceItems"` because in the current model purchase invoice items are stored in `InvoiceItems` with discriminator, and some existing databases have `PurchaseInvoiceItems` only as a compatibility view.
- Keeps the v4.11.35 inventory advanced filters.

## Verify after deploy

- Open Purchase → Vendors.
- Open Inventory → Brands.
- Confirm Message Log no longer shows `ALTER action ADD COLUMN cannot be performed on relation "PurchaseInvoiceItems"`.
