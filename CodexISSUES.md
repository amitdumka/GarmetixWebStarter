# Codex Issues - Garmetix Web

Use this file for every bug/error raised by the user. Mark an item `[x]` only after the fix is included in a generated ZIP.

## Fixed

- [x] Docker publish compile error in `PasswordResetTokenService.cs` caused by missing parentheses around base64 padding switch expression. Fixed in `AllRemainingCore-CompileFix`.
- [x] Docker publish compile errors in `WorkspaceScope.cs`, `SetupEndpoints.cs`, and `PurchaseEndpoints.cs`. Fixed enum qualification, unassigned local variable, and missing vendor payment method.
- [x] API restart loop caused by EF Core `PendingModelChangesWarning` during startup auto-migration. Added runtime migration-warning handling and schema-drift repair.
- [x] Existing PostgreSQL volume missing Customer/Vendor GSTIN verification columns. Added idempotent startup repair for party GSTIN columns.
- [x] Sales Return and Purchase Return menu options not visible. Added `/sales-return` and `/purchase-return` pages plus menu/dashboard links.
- [x] Existing PostgreSQL volume missing GST draft, commercial note, customer advance, and loyalty tables. Added idempotent schema repair for these tables.
- [x] Debit Note and Credit Note entry were combined on one page/modal. Split into dedicated list/new/edit routes for Debit Notes and Credit Notes.
- [x] Customer loyalty handling needed a Customer module. Added `/customers` module and refined `/loyalty` customer ledger/adjustment flow.
- [x] Audit page could fail when `GstReturnDrafts` table was missing in older volumes. Added defensive schema repair before audit queries and made startup repair run even when auto-migration is disabled.

## Open

- [ ] Developer machine validation still pending: `dotnet publish`, Nuxt production build, clean Docker install, permissions matrix, backup/restore, and fresh migration test.
