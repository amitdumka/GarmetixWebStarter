# Garmetix Issues

The current implementation order and non-defect enhancement work are maintained in `CURRENT-ROADMAP.md`.

Use this file for every bug/error raised by the user. Mark an item `[x]` only after the fix is included in a generated ZIP.

## Fixed

- [x] FAQ category filtering used a forbidden empty SelectItem value and could fail while rendering. It now uses an internal all-value sentinel that is converted only inside the page filter.
- [x] GST Returns converted selected invoice and accounting dates through UTC, which could shift them to the previous day in India. It now sends local calendar date-time values without UTC conversion.
- [x] GST Returns, Profile, About Garmetix, Contact Us, and Help and FAQ discarded load failures into transient messages. Package 16 retains sanitized errors with direct retry actions and loading states.
- [x] Profile and GST review text contained inconsistent separators and obsolete standalone-module guidance, while help pages exposed developer-oriented deployment details. Package 16 uses current business-facing copy and ASCII-safe separators.
- [x] Oracle Sync entity and direction filters used forbidden empty SelectItem values and could fail while rendering. They now use an internal all-value sentinel that is converted to null before API calls.
- [x] Production Readiness, Release Stabilization, Data Consistency, and Oracle Sync discarded load failures into transient notifications. Package 15 retains sanitized errors with direct retry actions and initial loading states.
- [x] Company, store-group, and store start/end dates could move to the previous day because Setup converted local dates through UTC. Setup now sends the selected local calendar date directly.
- [x] Core Admin pages discarded load failures into transient notifications, used narrow master forms, and exposed legacy implementation/source-file wording. Package 14 adds retained retry actions, wide forms, responsive tables/loading states, and business-facing copy.
- [x] Opening the Roles & Users form returned a Nuxt 500 because no-scope options used forbidden empty SelectItem values. Access scope selectors now use an explicit internal sentinel and convert it back to null in API payloads.
- [x] Product Master create/edit returned HTTP 500 because manually opened transactions ran outside the configured Npgsql retry execution strategy. Both operations now execute their full transaction through `CreateExecutionStrategy()`.
- [x] API exceptions, failed responses, write events, frontend messages, browser errors, and background-service logs were not consistently persisted. Central middleware, an authenticated client sink, browser handlers, and a bounded application logger queue now write them to Message Logs with sensitive-value redaction.
- [x] Message Logs returned a Nuxt 500 because filter options used empty SelectItem values. Filters now use explicit all-value sentinels and omit those values from API queries.
- [x] GST Reports returned HTTP 400 for a valid return period because report requests did not send the active company. Report and CSV queries now include the workspace company identifier.
- [x] Accounting Party/Bank Account forms could submit internal ledger identifiers through full EF entity payloads, bank transactions exposed internal Party linkage, and HR/accounting dates could shift through UTC. Dedicated requests, server-owned ledger synchronization, hidden party linkage, wide forms, and local date serialization now cover these workflows.
- [x] Salary payment save used the full EF entity payload, client-generated voucher numbers, UTC-shifted dates, and no authoritative advance/due pre-calculation. It now uses a dedicated request, local dates, server SPAY numbering, whole-rupee payments, and payroll-ledger preview values.
- [x] Printable vouchers, invoices, Petty Cash and payroll documents could invoke `window.print()` on the dashboard DOM, producing incorrect or incomplete output. Printing now fetches the authenticated server PDF, validates its media type/size, and prints that PDF; new documents auto-print while edits do not.
- [x] Printed documents had only text scan references and no consistent retrieval path. Stable QR tokens and a permission-aware Document Scanner now cover sale/purchase invoices, vouchers, cash vouchers, debit/credit notes, Petty Cash, payslips and salary payments.
- [x] Petty Cash create/edit changed a selected local date to the previous day in positive UTC-offset time zones. Removed UTC `toISOString()` conversion from the selected date, added local calendar defaults, and verified the June 13 form separately carries the June 12 closing balance.
- [x] Petty-cash A5 printing was clipped after the first income/payment row and did not preserve report colors. Replaced modal-page printing with an isolated A5 landscape document containing all sheet values, totals, reconciliation, company/store identity, audit details, and signatures with exact print colors.
- [x] Petty-cash save returned HTTP 500 after persisting the sheet when a reconciliation log contained nullable fields. Message-log inserts now use explicitly typed database parameters, and alert logging/email failure no longer invalidates a successful sheet save.
- [x] Petty-cash opening balance, transaction pre-calculation, mismatch alerting, A5 printing, and latest cash-in-hand behavior were missing. Replaced generic CRUD with a dedicated daily-cash workflow and validated backend/frontend builds on 2026-06-12.
- [x] Voucher numbering used a browser timestamp and did not follow the required accounting format. New voucher numbers are generated server-side as `StoreCode/yyyyMM/0001`, with one persistent monthly series per store.
- [x] Repeated master-data and page GET requests delayed navigation. Added shared response caching and in-flight request de-duplication with cache invalidation after writes.
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

- [ ] `GET /api/dashboard/home` is mapped in a way that discards its `DashboardHomeDto`; the live endpoint can return HTTP 200 with an empty body.
- [ ] Backend Release build has nullable warnings in purchase receipt mapping and data-consistency number handling.
- [ ] Nuxt production build succeeds but external font metadata providers can fail certificate validation and produce noisy fallback warnings.
- [ ] Authenticated Nuxt pages log `Hydration completed but contains mismatches`; the current register workflows render and operate correctly, but the shared SSR/auth shell needs a dedicated hydration audit.
- [ ] Clean Docker install, fresh database migration, permissions matrix, and backup/restore drill remain pending.

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

## OPEN - Sale/Purchase/Inventory final production hardening

- Status: Open
- Area: Sale / Purchase / Inventory
- Note: Stage 3 UI and Stage 4 numbering/concurrency/stock-operation foundations are implemented. Remaining work is formal Purchase Return documents, exact ITC reversal, supplier refund settlement, formal stock-operation documents, movement-ledger authority, stock valuation, and automated reconciliation tests.
