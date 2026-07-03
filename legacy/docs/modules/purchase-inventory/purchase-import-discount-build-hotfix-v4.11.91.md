# v4.11.91 Purchase Import Discount Build Hotfix

This hotfix fixes the API build error introduced in v4.11.90.

## Fixed

- Restored `BuildColumnProductName` and helper `JoinNonEmpty` in `PurchaseInvoiceImportService.cs`.
- Keeps the S.K APPARELS/Tally column parser and discount reconciliation logic from v4.11.90.

## Build error fixed

```text
CS0103: The name 'BuildColumnProductName' does not exist in the current context
```

## Test

Run:

```bash
docker compose up --build
```

Then retry importing the S.K APPARELS invoice and verify line discounts and totals.
