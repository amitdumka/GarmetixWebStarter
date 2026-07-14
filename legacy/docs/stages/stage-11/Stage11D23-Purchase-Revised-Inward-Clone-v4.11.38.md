# Stage 11D-23 Purchase Revised Inward Clone (v4.11.38)

Build: `GARMETIX-11D23-20260625-4138`

## Purpose

A purchase invoice can contain many item rows. If one item, quantity, rate, MRP, GST, discount, or HSN is wrong, forcing the user to recreate the whole inward manually is not practical.

## Implemented

- Added **Revise** action on Purchase Inward rows.
- Revise opens `/purchase/new?copyFrom=<invoiceId>`.
- New Purchase Inward page loads the original invoice receipt and pre-fills:
  - Vendor
  - Supplier invoice reference with `-REV` suffix
  - Supplier invoice date
  - Due date
  - Freight
  - All item lines with barcode, product name, HSN, qty, cost, MRP, discount, GST
- New inward number is still server-generated.
- Original purchase invoice is not changed or cancelled automatically.
- After the revised inward is saved and checked, Admin/Owner can cancel the old invoice to reverse old stock/accounting.

## Safety

This is safer than full in-place item edit because stock ledger, vendor payable, GST and accounting remain traceable.
