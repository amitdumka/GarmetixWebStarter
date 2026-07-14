# Stage 11D-104 — Purchase/Vendor Payment Reconciliation Repair Tools

Version: **v4.12.19**
Base: **v4.12.18 Stage 11D-103 Purchase/Vendor Payment Reconciliation Hardening**

## Purpose

Stage 11D-103 fixed the vendor payment edit/delete calculation path. Stage 11D-104 adds an admin live-support layer so existing/live data can be checked and repaired without manual SQL editing.

## Added API endpoints

```txt
GET  /api/purchase/payments/reconciliation?take=50
POST /api/purchase/payments/reconciliation/repair
```

Both endpoints require Admin authorization.

## Reconciliation report checks

- Vendor `Paid` stored total vs active non-deleted `PurchasePayments`.
- Purchase invoice status/payment mode vs active payment totals.
- Deleted vendor payment rows whose linked voucher remains active.

## Repair action

The repair endpoint:

1. recalculates `Vendors.Paid` from active purchase payments;
2. recalculates linked purchase invoice status and payment mode;
3. soft-deletes active vouchers linked to deleted vendor payments;
4. soft-deletes linked bank transactions, bank statement lines, cheque logs, journal entries and journal lines.

No hard delete is performed.

## Runtime commands

```bash
python3 scripts/validation/stage11d104-purchase-vendor-payment-reconciliation-repair-tools-check.py
./scripts/runtime/stage11d104-purchase-vendor-payment-reconciliation-api-check.sh /opt/garmetix/current
```

## Next part

**Stage 11D-105 — Purchase/Vendor Payment Live Error Fixes**

Use this after testing v4.12.19 on real data. It should fix any build/runtime issue from the new reconciliation endpoints or the purchase/vendor payment pages.
