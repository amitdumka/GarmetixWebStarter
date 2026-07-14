# Stage 4E — Controlled Repair Tools

Package: `Garmetix-Stage4E-ControlledRepair-v1.3.zip`

## Goal

Stage 4D added data consistency detection. Stage 4E adds controlled repair actions so admin users can preview and apply safe cleanup operations for selected data issues.

The repair design is intentionally conservative:

- no repair runs silently,
- every action has a preview endpoint,
- medium/high risk actions require explicit confirmation,
- every response returns the affected entity, field, before value and after value,
- row limits are capped to 500 per run,
- the original Stage 4D issue checker remains unchanged and can be run before and after repair.

## Backend changes

Added files:

- `backend/Garmetix.Api/Validation/DataRepairDtos.cs`
- `backend/Garmetix.Api/Validation/DataConsistencyRepairEndpoints.cs`

Updated:

- `backend/Garmetix.Api/Program.cs`

New endpoints:

- `GET /api/data-consistency/repairs/actions`
- `POST /api/data-consistency/repairs/preview`
- `POST /api/data-consistency/repairs/apply`

All repair endpoints require admin access. The apply endpoint also requires edit authorization.

## Repair actions added

### Low risk

- `BACKFILL_SALE_ITEM_SNAPSHOTS`
  - fixes missing sale item product name, HSN and unit snapshots.
- `BACKFILL_PURCHASE_ITEM_SNAPSHOTS`
  - fixes missing purchase item product name, HSN and unit snapshots.
- `NORMALIZE_COMMERCIAL_NOTE_ADJUSTMENTS`
  - clamps adjusted amount and updates `IsAdjusted`.
- `NORMALIZE_CUSTOMER_ADVANCE_BALANCES`
  - clamps adjusted amount and recalculates available amount.

### Medium risk

- `RECALCULATE_SALE_GST_HEADERS`
  - recalculates sale invoice GST/totals from stored item rows.
- `RECALCULATE_PURCHASE_GST_HEADERS`
  - recalculates purchase invoice GST/totals from stored item rows plus freight.
- `SYNC_SALE_PAID_AMOUNT`
  - syncs sale invoice `PaidAmount` from `InvoicePayment` rows.

### High risk

- `MERGE_DUPLICATE_DOCUMENT_SEQUENCES`
  - keeps one sequence row per company/store/document/date and soft-deletes duplicate rows.
- `REBUILD_STOCK_QTY_FROM_LEDGER`
  - rebuilds `Stock.PurchaseQty` and `Stock.SoldQty` from `StockMovement` totals. Use only after confirming the movement ledger is complete.

## Frontend changes

Updated:

- `frontend/garmetix-web/pages/data-consistency/index.vue`
- `frontend/garmetix-web/components/AppShell.vue`

The existing Data Consistency screen now includes a **Controlled Repair Tools** section with:

- repair action selector,
- risk badge,
- row limit,
- operator reason note,
- preview button,
- apply button,
- confirmation checkbox,
- before/after change table.

Sidebar label changed from **Data Consistency** to **Consistency & Repair**.

## Recommended usage

1. Open `/data-consistency` as admin.
2. Run checks.
3. Choose a repair action related to the reported check code.
4. Preview first.
5. Apply only after reviewing the before/after table.
6. Run checks again.
7. Export CSV for records if needed.

## Not included yet

- automatic journal-entry balancing repair, because that can affect ledgers and should be handled with formal accountant-reviewed reversal/adjustment vouchers.
- duplicate invoice-number renumbering, because invoice number changes can affect printed GST/legal documents.
- purchase paid amount sync, because purchase payment allocation and debit notes need extra rules for overpayment and return adjustments.
