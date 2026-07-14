# Stage 11D-137 — DotMatrix Queue Stats EF Fix — v4.12.52

## Fix

Resolved production 500 error when opening **Accounting → Dot Matrix Print**:

```text
GET /api/dot-matrix-print/queue/stats?storeId=...
System.InvalidOperationException: The LINQ expression ... GroupBy(...).Select(new DotMatrixQueueStatDto(...)) could not be translated.
```

The endpoint now counts the fixed queue statuses directly with simple `CountAsync` filters, avoiding the failing `GroupBy` projection entirely.

## Changed file

```text
backend/Garmetix.Api/DotMatrix/DotMatrixPrintEndpoints.cs
```

## Validation target

After deployment, opening **Accounting → Dot Matrix Print** should no longer fail at queue stats load.
