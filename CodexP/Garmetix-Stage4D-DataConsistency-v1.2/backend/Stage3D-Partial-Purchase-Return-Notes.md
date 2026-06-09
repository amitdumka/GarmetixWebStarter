# Stage 3D — Partial Purchase Return

Base package: `Garmetix-Stage3C-PurchaseUI-v0.7.zip`

## Goal

Replace the purchase return page's full-invoice-only flow with item-wise partial purchase returns.

## Backend changes

### New API contracts

Added in `backend/Garmetix.Api/Purchase/PurchaseDtos.cs`:

- `ReturnablePurchaseInvoiceDto`
- `ReturnablePurchaseItemDto`
- `PartialPurchaseReturnRequest`
- `PartialPurchaseReturnItemRequest`
- `PartialPurchaseReturnResponse`

### New endpoints

Added in `backend/Garmetix.Api/Purchase/PurchaseEndpoints.cs`:

- `GET /api/purchase/invoices/{id}/returnable`
- `POST /api/purchase/invoices/{id}/partial-return`

The returnable endpoint returns each purchase item with purchased quantity, already-returned quantity, returnable quantity, HSN, unit, unit taxable amount, unit tax amount, and unit gross amount.

The partial-return endpoint validates selected quantities, validates available stock, reduces `Stock.PurchaseQty`, posts `StockMovement` rows with `MovementType = PurchaseReturnOut`, creates a vendor debit note, adjusts vendor bill amount, and sets purchase invoice status to `PartiallyRefunded` or `Refunded`.

### Debit note support

Extended `CommercialEndpoints.CreateDebitNoteFromPurchaseReturnAsync` with an overload that accepts partial taxable/tax/amount values instead of always using the full purchase invoice value.

### Accounting support

Added `AccountingPostingService.PostPurchaseReturnAsync`.

The accounting entry posts:

- Debit vendor ledger for return amount.
- Credit purchase ledger for taxable return value.
- Credit input GST ledger for GST/ITC reversal.

### Safety rule

The existing full-cancel endpoint remains available for full unreversed purchases, but now blocks full cancel if the purchase invoice already has item-wise return status (`PartiallyRefunded` or `Refunded`). This avoids double stock/accounting reversal.

## Frontend changes

Updated `frontend/garmetix-web/pages/purchase-return/index.vue`:

- Loads returnable items before opening the return modal.
- Shows purchased, already returned, and returnable quantity per item.
- Accepts return quantity per item.
- Calculates selected quantity, taxable amount, tax amount, and return amount live.
- Supports "Return All Available" and "Clear" actions.
- Calls the new partial return endpoint instead of the old full-cancel endpoint.

## Remaining future hardening

- Store a dedicated `PurchaseReturn` / `PurchaseReturnItem` table for stronger audit and print history.
- Add debit-note PDF link directly from purchase return success screen.
- Add supplier refund receipt workflow for cases where vendor owes money after return.
- Add stock row concurrency token/locking before production.
