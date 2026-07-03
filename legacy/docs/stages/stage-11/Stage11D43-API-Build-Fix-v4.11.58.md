# Stage 11D-43 API Build Fix — v4.11.58

## Base

This package uses **v4.11.57 Stage 11D-42 Final PDF Print Acceptance Evidence** as the base.

## Problem fixed

Docker API publish failed with:

```text
InvoiceReplacementEndpoints.cs(338,17): error CS1061: 'PurchaseInvoice' does not contain a definition for 'PaidAmount'
```

The error came from the Stage 11D-41 invoice replacement audit snapshot. The current domain model has `PaidAmount` on sales `Invoice`, but purchase paid amount is not stored as a direct `PurchaseInvoice.PaidAmount` property. Purchase paid amount is calculated elsewhere from purchase payment/accounting rows.

## Changes

- Removed the invalid `invoice.PaidAmount` reference from `Snapshot(PurchaseInvoice invoice)`.
- Kept purchase replacement audit snapshots with compatible fields:
  - invoice id
  - supplier invoice number
  - inward number
  - inward date
  - invoice status
  - bill amount
  - vendor name
  - company/store/store-group
  - original invoice link
- Added a safe fallback for sale pending replacement party name:
  - `revised.CustomerName ?? "Customer"`
- Updated API/frontend version metadata to **4.11.58**.

## Database impact

No new migration.
No new table.
No data conversion.

## Expected result

The API should publish past the previous `PurchaseInvoice.PaidAmount` compile error.

Run on deployment machine:

```bash
docker compose build --no-cache api
docker compose up -d
```

If the web build was only cancelled because the API build failed, rebuild the full stack after this fix:

```bash
docker compose build --no-cache
docker compose up -d
```
