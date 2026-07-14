# Stage 11D-103 — Purchase/Vendor Payment Reconciliation Hardening

Version: **v4.12.18**  
Base: **v4.12.17 Stage 11D-102 Purchase/Vendor Payment Live QA Fixes**

## Purpose

This stage hardens the vendor payment edit/delete flow added in Stage 11D-100 to prevent drift between:

- `PurchasePayments`
- `Vendors.Paid`
- `PurchaseInvoices.InvoiceStatus`
- linked vouchers
- linked bank/statement/cheque artifacts
- linked journal entries

## Changes

### 1. Vendor paid total recalculation

Vendor payment edit/delete no longer relies only on incremental math. After correction, the API recalculates `Vendors.Paid` from active, non-deleted `PurchasePayments` rows.

### 2. Purchase invoice status recalculation

After a vendor payment edit/delete, linked purchase invoice status is recalculated from active payment rows:

- no active payment => `Pending`
- partial active payment => `PartiallyPaid`
- paid amount >= bill amount => `Paid`

### 3. Cash/non-cash cleanup

When a vendor payment is edited from non-cash to cash, old bank transaction, bank statement line, and cheque log artifacts linked to that voucher number are soft-deleted so stale bank rows do not remain active.

### 4. Editable/clearable payment references

Vendor payment edit now allows reference number, payment details and remarks to be cleared or corrected, instead of preserving old values when the new field is blank.

## Files changed

- `backend/Garmetix.Api/Purchase/PurchaseEndpoints.cs`
- `backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs`
- `frontend/garmetix-web/utils/appVersion.ts`
- `frontend/garmetix-web/package.json`
- `backend/Garmetix.Api/Garmetix.Api.csproj`
- `README.md`

## Validation

Run:

```bash
python3 scripts/validation/stage11d103-purchase-vendor-payment-reconciliation-hardening-check.py
```

Deploy:

```bash
docker compose build --no-cache
docker compose up -d
./scripts/runtime/stage11d103-purchase-vendor-payment-reconciliation-check.sh /opt/garmetix/current
```

## Next part

Stage 11D-104 — Purchase/Vendor Payment Live Error Fixes.

Use this next stage for any real build/runtime error after testing:

- purchase inward date
- vendor payment filter/pagination
- vendor payment edit
- vendor payment delete
- vendor paid total
- purchase invoice status
- voucher/bank/journal reversal
