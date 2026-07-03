# Stage 11D-97 — Vyapar Paid Zero Balance Discount Fix

Version: `4.12.12`
Build code: `GARMETIX-11D97-20260629-4212`
Base: `v4.12.11 Stage 11D-96 Vyapar Bank Column Mapping Fix`

## Problem fixed

Some Vyapar sale invoices have:

- `Payment Status = Paid`
- `Balance Due` / `Balance = 0`
- bank/POS/UPI payment columns total lower than the bill total

In Vyapar this can happen when the remaining difference is effectively bill discount/settlement discount. Garmetix must not import this as customer due.

## New rule

When the importer sees `Payment Status = Paid` and zero balance:

1. The invoice is treated as fully paid.
2. Payment rows still come from the actual Vyapar bank/POS/UPI columns.
3. If payment-column total is lower than bill total, the difference is imported as bill-level discount.
4. GST/taxable totals are recalculated proportionally across invoice items.
5. `SalesInvoices.BillDiscountAmount` and `SalesInvoices.DiscountAmount` store the adjustment.
6. `SalesInvoices.BillAmount` becomes the adjusted payable/paid amount.
7. No false due balance is created.

## Example

Vyapar bill total: `10,000`
Payment columns total: `9,500`
Payment Status: `Paid`
Balance: `0`

Imported result:

- Bill discount: `500`
- Bill amount: `9,500`
- Paid amount: `9,500`
- Status: `Paid`

## Files changed

- `backend/Garmetix.Api/SaleImport/VyaparSaleImportService.cs`
- `backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs`
- `frontend/garmetix-web/utils/appVersion.ts`
- `frontend/garmetix-web/package.json`
- `backend/Garmetix.Api/Garmetix.Api.csproj`
- `README.md`

## Validation

Run:

```bash
python3 scripts/validation/stage11d97-vyapar-paid-zero-balance-discount-fix-check.py
docker compose build --no-cache
docker compose up -d
```

## Next part

Stage 11D-98 should be Vyapar live import QA after deploying this package and uploading the real sale files.
