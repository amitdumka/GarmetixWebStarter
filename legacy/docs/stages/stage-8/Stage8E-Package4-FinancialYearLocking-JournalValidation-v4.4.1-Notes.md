# Stage 8E Package 4 - Financial Year Locking and Journal Validation (v4.4.1)

Build code: `GARMETIX-8E-20260617-4410`

## Scope

- Financial year/period locks for company, optional store group and optional store.
- DbContext-level period lock enforcement for Sales, Purchase, Inventory, GST and Accounting dated records.
- Journal validation endpoint for unbalanced entries, zero-line entries, negative amounts and mixed debit/credit line errors.
- Frontend Financial Year Locks workspace with period lock form, lock history and journal validation summary.
- Mac mini deployment default changed to `RESET_DATABASE_ON_DEPLOY=false` in the ready-to-run environment file.

## Deployment note

`RESET_DATABASE_ON_DEPLOY=true` should now be used only temporarily and intentionally when wiping a test PostgreSQL volume. The shipped default is false to protect production data.
