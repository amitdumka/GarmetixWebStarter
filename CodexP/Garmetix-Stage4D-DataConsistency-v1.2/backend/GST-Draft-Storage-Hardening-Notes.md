# GST Draft Storage Hardening

This patch fixes a repeated runtime issue where `/api/gst-returns/drafts` could query `GstReturnDrafts` before the table existed in older Docker PostgreSQL volumes.

Changes:

- Added `DatabaseSchemaRepairService.RepairGstReturnStorageAsync`.
- The GST draft storage repair now runs as a small dedicated SQL repair, separate from the large general schema repair.
- Startup calls the dedicated GST repair before the broader repair.
- GST draft endpoints call the dedicated repair before querying.
- The draft list endpoint retries once if PostgreSQL still reports missing relation `42P01`.

Use after extracting:

```bash
docker compose down
docker compose build --no-cache api
docker compose up
```

If Docker Desktop reuses an old image, run `docker compose build --no-cache api` before `docker compose up`.
