# Purchase Transaction Retry Hotfix v4.11.79

Version: 4.11.79  
Stage: Stage 11D-64 Purchase Transaction Retry Hotfix

## Problem fixed

Purchase inward save failed with HTTP 500 when EF Core attempted a query inside an explicit transaction after the global Npgsql retry execution strategy was enabled.

Runtime error:

```text
The configured execution strategy 'NpgsqlRetryingExecutionStrategy' does not support user-initiated transactions.
Use the execution strategy returned by 'DbContext.Database.CreateExecutionStrategy()' to execute all the operations in the transaction as a retriable unit.
```

The stack trace pointed to:

```text
POST /api/purchase/inward
Garmetix.Api.Purchase.PurchaseEndpoints.GetOrCreateVendorAsync
Garmetix.Api.Purchase.PurchaseEndpoints.CreateInwardAsync
```

## Root cause

`backend/Garmetix.Infrastructure/DependencyInjection.cs` configured:

```csharp
postgres.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
```

That switches EF Core to `NpgsqlRetryingExecutionStrategy`. The current Garmetix codebase has many explicit EF transaction blocks across purchase, sale billing, stock operation, import/export, payroll, settlement, and other posting modules. With the retrying strategy enabled globally, those transaction blocks must all be wrapped with `DbContext.Database.CreateExecutionStrategy()`.

Because the app already depends on explicit transactions for financial correctness, enabling retry globally without refactoring every transaction endpoint causes save failures.

## Fix applied

Removed global `EnableRetryOnFailure` from the Npgsql configuration and restored the default non-retrying Npgsql execution strategy:

```csharp
.UseNpgsql(connectionString)
```

This restores support for the existing explicit transaction flow used by purchase inward and other financial posting endpoints.

## Why this approach

This is the safest immediate production fix because it fixes the failing purchase inward save and also avoids the same transaction error in other modules that use explicit transactions.

A future technical-debt task can reintroduce retry support by refactoring every explicit transaction block to run inside `Database.CreateExecutionStrategy().ExecuteAsync(...)` as one retriable unit.

## Files changed

- `backend/Garmetix.Infrastructure/DependencyInjection.cs`
- `backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs`
- `backend/Garmetix.Api/Garmetix.Api.csproj`
- `frontend/garmetix-web/utils/appVersion.ts`
- `frontend/garmetix-web/package.json`
- `frontend/garmetix-web/package-lock.json`

## Validation to run on server

```bash
cd backend/Garmetix.Api
dotnet build
```

```bash
cd frontend/garmetix-web
npm install
npm run build
```

Then test:

1. Create purchase inward with no payment.
2. Create purchase inward with cash payment.
3. Create purchase inward with UPI/bank payment and bank account selected.
4. Confirm stock ledger and vendor balance update.
5. Confirm purchase receipt opens.
