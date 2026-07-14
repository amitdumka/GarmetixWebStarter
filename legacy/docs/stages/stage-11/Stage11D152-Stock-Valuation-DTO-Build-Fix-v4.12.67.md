# Stage 11D-152 Stock Valuation DTO Build Fix - v4.12.67

## Purpose

Fix the .NET publish failure caused by duplicate DTO type names inside `Garmetix.Api.Inventory`.

## Build error fixed

`StockValuationClosureEndpoints.cs` declared:

- `StockValuationSummaryDto`
- `StockValuationRowDto`

Those names already existed in `StockOperationDtos.cs`, causing CS0101/CS8863.

## Fix

Renamed only the closure endpoint DTOs to:

- `StockValuationClosureSummaryDto`
- `StockValuationClosureRowDto`

The frontend JSON contract is preserved because the DTO property names remain the same.

## Version

- Version: `4.12.67`
- Stage: `Stage 11D-152 Stock Valuation DTO Build Fix`
- Build code: `GARMETIX-11D152-20260703-4267`
