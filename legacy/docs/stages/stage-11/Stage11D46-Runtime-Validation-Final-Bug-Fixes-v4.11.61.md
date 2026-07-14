# Stage 11D-46 Runtime Validation Final Bug Fixes — v4.11.61

Base: v4.11.60 Stage 11D-45 Sale Payment Split Admin Hard Delete Fix.

## Purpose

Stage 11D-46 packages the final deploy/runtime validation layer after the last sale-payment and hard-delete fixes. It does not change business rules from v4.11.60. It adds safe host-side validation so the next deployment can be checked before store users start working.

## Added

- `scripts/runtime/stage11d46-runtime-validation.sh`
- `scripts/runtime/stage11d46-sale-payment-db-check.sql`
- `scripts/validation/stage11d46-runtime-validation-check.py`

## Runtime checks

The runtime script checks:

1. static Stage 11D-46 package tokens
2. Docker Compose configuration
3. optional clean Docker build
4. API health endpoint
5. API app-info version must be `4.11.61`
6. frontend root page response
7. PostgreSQL readiness
8. sale invoice paid amount mismatch count
9. recent mixed-payment cash/non-cash allocation rows
10. cancelled sale invoices that may be reviewed for admin hard delete
11. Royalwood import validation if the Royalwood import package is installed

## Run commands

From deployed project root:

```bash
python3 scripts/validation/stage11d46-runtime-validation-check.py
./scripts/runtime/stage11d46-runtime-validation.sh --smoke /opt/garmetix/current
```

For full rebuild and validation:

```bash
./scripts/runtime/stage11d46-runtime-validation.sh --build /opt/garmetix/current
```

## Manual acceptance checklist

### Mixed payment

Create one sale invoice with:

- Cash: ₹6,000
- UPI: ₹1,100
- Bill total: ₹7,100

Expected:

- Day closing cash sale: ₹6,000
- Day closing non-cash sale: ₹1,100
- due: ₹0
- DB check should show one mixed-payment row with cash and non-cash split.

### Revise sale invoice

- revise an existing paid/mixed-payment sale invoice
- payment split should copy into revised invoice
- revised invoice should not become due unless new total exceeds copied payment
- old invoice remains active until replacement approval

### Invoice replacement approval

- revised invoice appears in Invoice Replacements
- approval cancels/reverses old invoice
- stock/accounting/customer balance reversal should be visible
- audit ledger should show requested/approved/completed events

### Admin hard delete

- only admin/owner sees the hard delete action
- only cancelled invoices are eligible
- exact invoice number confirmation is required
- linked revised/return/exchange invoices block hard delete
- audit entry is written before deletion

### Sale list filters

- default list is Today
- check Yesterday, This Month, Last Month, This Year, Month-Year and Custom
- check page size and previous/next

### PDF print evidence

- save evidence for Sale A4/A5, Purchase A4/A5, large invoice, amount box, footer, signature and page summary

### Royalwood import

- apply only after dry-run passes
- validate vendor, invoice `26-27/533`, 58 pieces and total ₹161,611.70

## Notes

No database migration is added in this stage.
