# Final Accounts Security And Audit

BS-13 keeps Final Accounts isolated behind `/final-accounts` and `/api/final-accounts`, with the feature flag disabled by default. Default access is limited to Owner and Admin. Accountant, RemoteAccountant, StoreManager, POS, biller, cashier, HR, and payroll roles require a future explicit grant before they can open the module.

## Permission Matrix

| Role | Default access | Configure | Post | Close | Export | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| Owner | Yes | Yes | Yes | Yes | Yes | Business owner may enable and run the module. |
| Admin | Yes | Yes | Yes | Yes | Yes | Administration role may run setup, postings, close, and exports. |
| Accountant | No | No | No | No | No | Requires a future scoped reviewer or accountant grant. |
| RemoteAccountant | No | No | No | No | No | Review access is not enabled by default. |
| StoreManager | No | No | No | No | No | Store operations stay separate from final accounts. |
| Biller/Cashier/POS/Sales | No | No | No | No | No | Billing and POS users must never receive default access. |
| HR/Payroll | No | No | No | No | No | HR and payroll users stay in their operational modules. |

## Tenant Isolation

Every list, lookup, posting, close, projection, and export must resolve a `CompanyId`, optional `StoreGroupId`, and optional `StoreId` before touching data. Cross-company access must return a scoped not-found message instead of confirming whether the requested ID exists elsewhere.

## ID Enumeration

Not-found responses must use scoped wording such as `Journal entry was not found for the selected scope.` They must not echo requested GUIDs, company IDs, or store IDs. This applies to account mappings, journals, CA adjustments, close runs, projection scenarios, and exchange runs.

## Audit Coverage

| Workflow | Required event | Required evidence |
| --- | --- | --- |
| Account mapping | `FinalAccounts.CatalogChanged` | Scope, actor, before value, after value. |
| Journal/reversal | `FinalAccounts.JournalChanged` | Scope, actor, journal ID, action, idempotency key. |
| CA adjustment | `FinalAccounts.CaAdjustmentChanged` | Scope, actor, batch ID, status, comments or attachment metadata. |
| Close/reopen | `FinalAccounts.CloseRunChanged` | Scope, actor, close run ID, period, status. |
| Export/package | `FinalAccounts.ExchangeRunChanged` | Scope, actor, run ID, package type, checksum. |

## Attachment Validation

CA evidence attachments are limited to PDF, PNG, JPEG, CSV, XLSX, and DOCX, with a 25 MB limit. Executable, script, HTML, archive, and unknown binary uploads are blocked. Store file metadata and checksums in audit evidence; do not rely on file names alone.
