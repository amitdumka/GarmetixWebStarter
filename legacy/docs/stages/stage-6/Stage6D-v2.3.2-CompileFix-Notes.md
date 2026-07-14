# Stage 6D v2.3.2 Compile Fix Notes

Package: `Garmetix-Stage6D-NonGstGoods-v2.3.2-compilefix.zip`

## Issue reported

Docker publish failed with two compile errors in `backend/Garmetix.Api/NonGstGoods/NonGstGoodsEndpoints.cs`:

1. `CS0136`: local variable `stock` was declared inside `ResolveOrCreateNonGstStockAsync` where the same name was already used in an enclosing scope.
2. `CS0104`: `ProductCategory` was ambiguous between:
   - `Garmetix.Core.Enums.ProductCategory`
   - `Garmetix.Core.Models.Inventory.ProductCategory`

The warnings in `PurchaseEndpoints.cs` and `DataConsistencyEndpoints.cs` are nullable warnings only and do not block the Docker build.

## Fix applied

### 1. Stock variable shadowing

Changed the selected-stock variable in the `StockId` branch from `stock` to `selectedStock`:

```csharp
var selectedStock = await WorkspaceScope.ApplyTo(db.Stocks.Include(item => item.Product), context)
    .FirstOrDefaultAsync(item => item.Id == line.StockId.Value, cancellationToken);
```

This removes the C# local variable shadowing error while keeping the later `var stock = ...` lookup for create/reuse logic.

### 2. ProductCategory ambiguity

Added explicit alias:

```csharp
using InventoryProductCategory = Garmetix.Core.Models.Inventory.ProductCategory;
```

Changed the Non-GST category creation to:

```csharp
category = new InventoryProductCategory
{
    Id = Guid.NewGuid(),
    Name = "Non-GST Goods",
    ProductGroup = ProductGroup.Others,
    IsActive = true,
    CompanyId = companyId
};
```

This ensures the code uses the current inventory model class, not the obsolete enum.

## Version identity updated

- Version: `2.3.2`
- Stage: `Stage 6D`
- Release Name: `Non-GST Goods Compile Fix`
- Build Code: `GARMETIX-6D-20260610-232`

Updated in:

- `backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs`
- `frontend/garmetix-web/utils/appVersion.ts`

## Files changed

- `backend/Garmetix.Api/NonGstGoods/NonGstGoodsEndpoints.cs`
- `backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs`
- `frontend/garmetix-web/utils/appVersion.ts`
