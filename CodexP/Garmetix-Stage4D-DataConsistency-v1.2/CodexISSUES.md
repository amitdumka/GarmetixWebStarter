# Codex Issues - Garmetix Web

Use this file for every bug/error raised by the user. Mark an item `[x]` only after the fix is included in a generated ZIP.

## Fixed

- [x] Docker publish failed in `AccountingPostingService.cs` because GST/accounting helper methods were missing (`BuildTaxLedgerRowAsync`, `ResolvePostingStoreAsync`, `GstAccountingReference`, `DeterministicGuid`). Restored helpers and deterministic GST settlement reference generation.
- [x] GST Returns draft list still failed on older PostgreSQL volumes with `relation "GstReturnDrafts" does not exist`. Added targeted GST draft storage repair inside all GST draft endpoints so the table is created before querying.
- [x] GST Returns draft list still failed with `relation "GstReturnDrafts" does not exist` even after prior startup repair. Added a dedicated GST storage repair method, startup call, and endpoint retry before querying drafts.
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

## FIXED - Database schema repair raw SQL FormatException

- Status: Fixed
- Area: Backend / Database schema repair
- Reported from Docker logs: `System.FormatException: Input string was not in a correct format. Failure to parse near offset 582. Expected an ASCII digit.`
- Root cause: `ExecuteSqlRawAsync` treats literal `{}` in SQL default strings as string-format placeholders. This made the repair fail before it could create tables such as `GstReturnDrafts` and `CommercialNotes`.
- Fix: Escaped SQL default JSON braces as `{{}}` in schema repair SQL so PostgreSQL still receives `{}` while EF does not parse it as a format item.

## FIXED - CommercialNotes table missing after repair failure

- Status: Fixed
- Area: Backend / Commercial Notes
- Symptom: `/api/commercial-notes` failed with `42P01: relation "CommercialNotes" does not exist`.
- Root cause: The schema repair stopped earlier due the raw SQL brace FormatException, so `CommercialNotes` and related commercial tables were never created in older Docker volumes.
- Fix: Same raw SQL brace fix allows the existing idempotent repair to create `CommercialNotes`, `CustomerAdvanceReceipts`, `LoyaltyPrograms`, and `LoyaltyPointLedgers`.


## FIXED - Oracle Sync v3 inbound ownership helpers missing

- Status: Fixed
- Area: Backend / Oracle Secondary Sync
- Symptom: Oracle ownership/apply code referenced helper methods such as `BuildOwnershipMatrix`, `ResolveOwnership`, `ShouldKeepLocalVersion`, and scalar copy helpers that were not present in the extracted v3 service file.
- Fix: Restored the missing helper methods while implementing Oracle Sync v4 readiness and trusted auto-apply controls.


## OPEN - Oracle external app test kit pending real credentials

- Status: Open
- Area: Oracle Secondary Sync
- Note: Code and UI now provide a safe external-app smoke test, but a real Oracle Cloud Free Tier wallet/connection and one external connected app still need to be tested on the developer environment.

## OPEN - Sale/Purchase/Inventory production hardening pending UI and concurrency work

- Status: Open
- Area: Sale / Purchase / Inventory
- Note: Model alignment and backend posting foundation has started, but full UI completion, sequence-safe invoice numbering, stock concurrency protection, stock adjustment/transfer/count, and item-wise purchase return UI remain pending.
