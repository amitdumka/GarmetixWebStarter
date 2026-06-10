# Stage 6A Runtime Bugfix — Onboarding Execution Strategy

## Problem

During first-company onboarding, the API failed after the duplicate company/GSTIN/user checks with:

```text
System.InvalidOperationException: The configured execution strategy 'NpgsqlRetryingExecutionStrategy' does not support user-initiated transactions.
Use the execution strategy returned by 'DbContext.Database.CreateExecutionStrategy()' to execute all the operations in the transaction as a retriable unit.
```

## Cause

`ClientOnboardingService.OnboardAsync` opened a manual transaction with `BeginTransactionAsync`. Inside that transaction it also performed EF Core queries while PostgreSQL retry execution strategy was enabled. EF Core requires the whole manual transaction unit to run inside `db.Database.CreateExecutionStrategy().ExecuteAsync(...)`.

## Fix Applied

`ClientOnboardingService.OnboardAsync` now wraps the transaction and all onboarding writes/queries inside:

```csharp
var strategy = db.Database.CreateExecutionStrategy();
return await strategy.ExecuteAsync(async () =>
{
    await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
    // create Company, StoreGroup, Store, employees, users, salesman, basic masters
    await db.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);
    return response;
});
```

## Related Preventive Fix

`AfssDefaultSeederService.SeedAsync` also used a manual transaction and several EF queries. It has been patched with the same execution-strategy pattern so Admin → AF/SS seeding does not hit the same runtime error.

## Scope

- No database schema changes.
- No UI contract changes.
- No DTO changes.
- Existing onboarding page and AF/SS page URLs remain unchanged.

## Files changed

- `backend/Garmetix.Api/Onboarding/ClientOnboardingService.cs`
- `backend/Garmetix.Api/Seeds/AfssDefaultSeederService.cs`
