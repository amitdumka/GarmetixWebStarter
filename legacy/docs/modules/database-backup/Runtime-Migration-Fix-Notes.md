# Runtime Migration Fix Notes

This package fixes the Docker/API restart loop caused by EF Core raising `PendingModelChangesWarning` during startup auto-migration.

Changes made:

- Synchronized the EF model snapshot with the GST return draft/audit entities.
- Configured EF warnings so `PendingModelChangesWarning` is logged instead of promoted to an unhandled startup exception.
- Applied the same warning behavior to the design-time DbContext factory used by `dotnet ef`.

Why this was needed:

The API was reaching PostgreSQL successfully, but startup auto-migration crashed before the web API could start because the snapshot lagged behind the current model.

After updating, rebuild and restart Docker:

```bash
docker compose down
docker compose up --build
```

If you are using an existing database volume, keep it unless you intentionally want a fresh database.
