# v4.11.88 Purchase Import Tally Parser Build Hotfix

## Fix

Stage 11D-73 fixes the API publish failure introduced in the Tally/column invoice parser slice.

Error fixed:

```text
PurchaseInvoiceImportService.cs(...): error CS0165: Use of unassigned local variable 'skipHeaderLines'
```

The item-table detection code now initializes the multi-line table-header skip counter before the combined header-detection condition.

## Scope

This is a build hotfix only. The v4.11.87 parser behavior remains intact:

- Tally-style/column invoice parsing
- S.K APPARELS style garment table parsing
- invoice number/date extraction improvements
- parser diagnostics and vendor learning support

## Test

```bash
docker compose up --build
```

Then upload the S.K APPARELS invoice again from `Purchase → Import Supplier Invoice`.
