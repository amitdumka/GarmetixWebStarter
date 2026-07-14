# Stage 11D-89 — Vyapar Sale Import API Build Fix — v4.12.04

Base: v4.12.03 Stage 11D-88 Vyapar Sale Import Preview.

## Problem fixed

Docker API publish failed with:

```text
VyaparSaleImportService.cs(704,24): error CS0104: 'ProductCategory' is an ambiguous reference between 'Garmetix.Core.Enums.ProductCategory' and 'Garmetix.Core.Models.Inventory.ProductCategory'
```

## Fix

`VyaparSaleImportService.cs` now aliases the inventory model:

```csharp
using InventoryProductCategory = Garmetix.Core.Models.Inventory.ProductCategory;
```

and uses `InventoryProductCategory` for the category creation helper and new category instance.

This keeps the generated enum namespace available for existing enum references while removing ambiguity for EF inventory category entities.

## Scope

No database schema change.
No sale import behavior change.
No frontend logic change except version metadata.

## Validate

```bash
python3 scripts/validation/stage11d89-vyapar-sale-import-api-build-fix-check.py
docker compose build --no-cache api
```
