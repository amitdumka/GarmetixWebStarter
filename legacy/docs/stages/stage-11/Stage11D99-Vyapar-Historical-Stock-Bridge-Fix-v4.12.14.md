# Stage 11D-99 — Vyapar Historical Stock Bridge Fix

Version: v4.12.14  
Base: v4.12.13 Stage 11D-98 Vyapar Import History Clear Fix

## Problem fixed

Vyapar Sale Import confirm could fail with:

```text
System.ArgumentException: Insufficient ledger stock. Available quantity is 0.
```

This happened when importing historical sale invoices where the current barcode existed but the stock ledger had no available quantity on the sale date. Preview could mark the barcode as matched based on current stock, but confirm posted the stock-out on the historical invoice date and the ledger correctly rejected it.

## Fix

- Preview now checks stock availability as of the invoice date.
- Confirm now checks historical ledger availability before stock-out.
- If `Bridge insufficient/historical stock` is enabled, confirm posts a controlled stock bridge movement immediately before the sale date.
- If bridge is not enabled, confirm returns a clear operator message instead of an unhandled server exception.

## Main files changed

```text
backend/Garmetix.Api/SaleImport/VyaparSaleImportService.cs
frontend/garmetix-web/pages/billing/vyapar-import.vue
backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs
frontend/garmetix-web/utils/appVersion.ts
frontend/garmetix-web/package.json
backend/Garmetix.Api/Garmetix.Api.csproj
README.md
```

## Operator note

For historical Vyapar imports, enable:

```text
Bridge insufficient/historical stock
```

when the original opening stock/import stock history was not present in Garmetix before the sale invoice date.

## Next part

Stage 11D-100 — Vyapar Sale Import Final QA Report and Live Import Fixes.
