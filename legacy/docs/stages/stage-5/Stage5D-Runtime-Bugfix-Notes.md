# Stage 5D — Runtime Bugfix Pass

## Trigger

Docker publish and the Nuxt build completed successfully, but opening the Stock Ops page raised a runtime API error from `StockOperationEndpoints.OptionsAsync`.

The failure was caused by the stock movement query ordering after projection into `StockMovementRowDto`:

```csharp
MovementQuery(context, db)
    .OrderByDescending(item => item.OnDate)
```

Because `MovementQuery` already projected into a DTO record, EF Core attempted to translate `OrderByDescending(new StockMovementRowDto(...).OnDate)` and failed.

## Fix applied

`StockOperationEndpoints.cs` was changed so ordering and `Take` happen on the `StockMovement` entity query before DTO projection.

Before:

```csharp
MovementQuery(context, db)
    .OrderByDescending(item => item.OnDate)
    .ThenByDescending(item => item.Id)
    .Take(25)
```

After:

```csharp
MovementQuery(context, db, 25)
```

And inside `MovementQuery`:

```csharp
WorkspaceScope.ApplyTo(db.StockMovements.AsNoTracking(), context)
    .Include(item => item.Product)
    .OrderByDescending(item => item.OnDate)
    .ThenByDescending(item => item.Id)
    .Take(take)
    .Select(item => new StockMovementRowDto(...))
```

## Expected result

- `/api/inventory/stock-operations/options` should no longer throw EF Core translation errors.
- `/stock-operations` page should load product/store options and recent stock movements.
- Existing adjustment, transfer, and physical-count logic is unchanged.

## Notes from your Docker log

- Backend `dotnet publish` completed successfully.
- Frontend `nuxt build` completed successfully.
- The main blocker was runtime LINQ translation on the Stock Ops options endpoint.
- A pending EF model-change migration warning still appears during startup, but the app continues and the existing runtime schema repair path runs. A formal EF migration consolidation is recommended later, but it was not the Stock Ops page crash.
