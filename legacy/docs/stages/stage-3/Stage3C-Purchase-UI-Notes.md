# Garmetix Stage 3C — Purchase UI Completion Notes

Package: `Garmetix-Stage3C-PurchaseUI-v0.7.zip`

## Goal

Stage 3C continues from the fixed Stage 3B build package and completes the next purchase-inward UI layer. The focus is purchase-side data capture and visibility, not partial purchase return yet.

## Stage 3B implementation check

Before Stage 3C changes, the Stage 3B fixed package was inspected and the billing implementation was present:

- Billing page contains customer profile/credit/loyalty controls.
- Billing page contains salesman selection.
- Billing page contains split payment rows and adjustment payment rows.
- Billing backend contains billing options, customer search/profile endpoints, salesman validation, and multi-payment handling.
- Infrastructure contains `Salesmen` DbSet and schema/table support.
- Quick setup seeds a default `Manager` salesman.

So Stage 3B should be treated as implemented in source. It still needs your Docker build/runtime verification on your machine after the earlier compile-fix package.

## Backend changes

Updated `backend/Garmetix.Api/Purchase/PurchaseDtos.cs`:

- `PurchaseInwardRequest` now accepts `VendorId` for existing vendor selection.
- `PurchaseInwardRequest` already had `SupplierInvoiceDate` and `DueDate`; Stage 3C connects them to UI.
- `PurchaseInwardItemRequest` now accepts:
  - `HsnCode`
  - `ProductUnit`
  - `ProductType`
  - `ProductGroup`
- Recent purchase invoice rows now include:
  - `SupplierInvoiceDate`
  - `DueDate`
  - `VendorId`
- Purchase receipt now includes:
  - `SupplierInvoiceDate`
  - `DueDate`
  - `VendorId`
  - expanded item snapshot data
  - purchase payment history
- Purchase receipt item now includes:
  - HSN
  - Unit
  - CGST
  - SGST
  - IGST

Updated `backend/Garmetix.Api/Purchase/PurchaseEndpoints.cs`:

- `/api/purchase/lookup-options` now returns:
  - vendors
  - units
  - product types
  - product groups
  - active product categories
  - subcategories with category linkage
- Existing vendor selection is supported in purchase inward creation.
- New-product creation from purchase inward now uses submitted product type and product group instead of hardcoded apparel-only defaults.
- Existing products can be updated with HSN, unit, product type, and product group during inward.
- Purchase receipt response now returns payment history from `PurchasePayments`.

## Frontend changes

Updated `frontend/garmetix-web/pages/purchase/index.vue`:

### Vendor picker

- Added existing vendor selector.
- Selecting a vendor fills vendor name, mobile, and GSTIN.
- Vendor summary shows bill amount, paid amount, and balance.

### Supplier invoice dates

- Added supplier invoice date input.
- Added due date input.
- These values are sent to the backend purchase inward endpoint.

### Item snapshot fields

- Added HSN input.
- Added Unit dropdown.
- Added Product Type dropdown.
- Added Product Group dropdown.
- Cart table now shows HSN/unit.
- Payload sends HSN/unit/product type/product group per line.

### Receipt improvements

- Receipt modal shows supplier invoice date and due date.
- Receipt item table shows HSN, unit, and GST split.
- Receipt modal shows purchase payment history.

## Not included in Stage 3C

The following remain for the next stages:

- Partial item-wise purchase return / vendor debit note flow.
- Freight GST treatment and landed-cost allocation.
- Sequence-safe purchase invoice/inward numbers.
- Stock row locking/concurrency protection.
- Full accounting split by payment mode.

## Recommended next stage

Move to **Stage 3D — Partial Purchase Return**:

- Select purchase invoice.
- Select item lines to return.
- Validate return quantity against remaining inward/current stock.
- Post selected stock outward movements.
- Create vendor debit note / ITC reversal draft.
- Update vendor payable and purchase payment allocation safely.
