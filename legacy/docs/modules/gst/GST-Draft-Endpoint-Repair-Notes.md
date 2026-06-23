# GST Draft Endpoint Repair

## Problem
Older PostgreSQL Docker volumes could still miss the `GstReturnDrafts` and `GstReturnAuditEntries` tables even when startup repair was present. The visible runtime error was:

```text
42P01: relation "GstReturnDrafts" does not exist
```

This occurred when `/api/gst-returns/drafts` queried the draft table before the table existed.

## Fix
A targeted GST draft storage repair now runs inside every GST draft endpoint before the endpoint queries or writes draft data:

- `GET /api/gst-returns/drafts`
- `GET /api/gst-returns/drafts/{id}`
- `POST /api/gst-returns/drafts`
- `PUT /api/gst-returns/drafts/{id}`
- `DELETE /api/gst-returns/drafts/{id}`
- `POST /api/gst-returns/drafts/{id}/filed`
- `GET /api/gst-returns/drafts/{id}/audit`
- `GET /api/gst-returns/drafts/{id}/json`
- `GET /api/gst-returns/drafts/{id}/excel`
- `POST /api/gst-returns/drafts/{id}/accounting-posting`

The repair uses idempotent PostgreSQL commands:

- `CREATE TABLE IF NOT EXISTS "GstReturnDrafts"`
- `CREATE TABLE IF NOT EXISTS "GstReturnAuditEntries"`
- `ALTER TABLE ... ADD COLUMN IF NOT EXISTS`
- `CREATE INDEX IF NOT EXISTS`

## Notes
Do not delete the PostgreSQL volume unless you want to erase existing data. Rebuild and restart the containers after replacing the source.
