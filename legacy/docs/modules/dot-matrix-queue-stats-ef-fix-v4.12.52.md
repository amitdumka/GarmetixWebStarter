# DotMatrix Queue Stats EF Fix — v4.12.52

This patch fixes the DotMatrix admin page runtime error caused by an EF Core translation failure in the queue stats endpoint.

The endpoint now:

1. Applies store/security filtering in SQL.
2. Counts the fixed statuses `Pending`, `Printing`, `Printed`, `Failed`, and `Skipped` using simple EF-safe `CountAsync` filters.

No printer deployment changes are required for this patch.
