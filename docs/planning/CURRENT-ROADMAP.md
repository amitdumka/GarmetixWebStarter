# Garmetix Current Roadmap

Updated: 2026-06-13

## Current Baseline

- Version: 4.0.2
- Stage: Stage 8A
- Release: Loyalty and Petty Cash UI Standardization
- Build code: `GARMETIX-8A-20260613-4002`
- Branch: `Version3.0`
- Pre-Stage 8 baseline commit: `470ba2e`

Stage 7M menu names, route compatibility, permission-aware navigation, Off Book separation, and the legacy-shell rollback option are part of the baseline and must be preserved.

## Completed Before Stage 8 - Operations Priority Patch

- [x] Petty cash previous-day carry-forward, transaction pre-calculation, editable verification, mismatch logging/email, latest cash widget, and automatic A5 two-section printing.
- [x] Server-owned voucher numbering using store code, year-month, and one monthly numeric sequence.
- [x] Revised compact voucher print defaults and document labels.
- [x] Shared frontend GET cache and in-flight request de-duplication to reduce repeated page-load work.

## Priority 0 - Baseline Stabilization

- [x] Fix `GET /api/dashboard/home` returning an empty response body.
- [x] Resolve the nullable warnings in purchase receipt and data-consistency code.
- [x] Keep frontend, backend, package, release, and build-code versions synchronized.
- [ ] Remove build-time dependence on external font metadata or configure a reliable local/system-CA solution.
- [x] Add one current aggregate validation command instead of relying on version-specific historical scripts.
- [ ] Verify login, smart-dashboard routing, workspace selection, role navigation, and API health in Docker.

## Stage 8A / v4.0 - Full UI Audit and Standardization

- [x] Package 1: make `/ui-audit` persistent/actionable and standardize the Credit Note and Debit Note registers with shared loading, error, empty, search, action, and responsive behavior.
- [x] Package 2: standardize Commercial Notes and Customers, restore hidden primary actions, and synchronize all runtime version sources to v4.0.0.
- [x] Package 3: standardize Parties and Vouchers, add retained sanitized errors, expose both party create actions, add voucher-type filtering, and migrate audit progress to v4.0.1.
- [x] Package 4: standardize Loyalty and Petty Cash, widen daily-cash entry, preserve transaction calculation/A5 printing, and migrate audit progress to v4.0.2.
- [ ] Complete every page in the `/ui-audit` queue.
- [ ] Standardize page headers, filters, tables, actions, loading, error, and empty states.
- [ ] Move large invoice, voucher, employee, purchase, payroll, and other master-detail forms to full pages or wide workspaces.
- [ ] Verify mobile sidebar drawer, collapsed icon mode, active-menu state, command palette, footer menus, and topbar actions.
- [ ] Decide and implement a useful notification/action dropdown without exposing technical noise.
- [ ] Check button wrapping, table overflow, modal spacing, and sidebar/topbar/footer overlap at mobile, tablet, and desktop sizes.
- [ ] Replace repeated custom markup with shared Nuxt UI components where it reduces real duplication.
- [ ] Remove stale visible implementation or migration text from business-facing pages.

## Stage 8B / v4.1 - Access and User Administration Hardening

- [ ] Audit the existing user create/edit, role assignment, password reset, and active/inactive workflows.
- [ ] Test the complete server and frontend permission matrix.
- [ ] Confirm StoreManager cannot access Admin and cannot edit/delete restricted records.
- [ ] Confirm edit access is limited to approved roles and delete access to Owner/Admin.
- [ ] Add explicit audit events for permission, role, activation, and password-administration changes.
- [ ] Add automated access tests for Owner, Admin, PowerUser, Accountant, StoreManager, Salesman, HR, and Payroll roles.

## Stage 8C - Purchase Return and Vendor Settlement

- [ ] Add formal Purchase Return and Purchase Return Item records.
- [ ] Preserve product, HSN, GST, quantity, rate, reason, vendor, and original-purchase snapshots.
- [ ] Generate debit notes and printable/PDF return documents.
- [ ] Add vendor refund, adjustment, and outstanding settlement workflows.
- [ ] Implement exact item-level input-tax-credit reversal.
- [ ] Preserve links among purchase, return, stock movement, ledger, bank/cash payment, and audit history.

## Stage 8D - Inventory Documents and Valuation

- [ ] Add formal Stock Adjustment, Transfer, and Physical Count header/detail documents.
- [ ] Make the stock movement ledger the authoritative stock source.
- [ ] Introduce and document weighted-average valuation; evaluate FIFO only after that baseline is stable.
- [ ] Post shortage, excess, write-off, and transfer accounting entries.
- [ ] Add stock ageing, low-stock risk, valuation, and stock-reconciliation reports.
- [ ] Add sequence and stock-concurrency tests, including negative-stock policy.

## Stage 8E - Accounting and Payment Hardening

- [ ] Post separate ledger lines for each split payment row.
- [ ] Persist complete card, UPI, bank, cheque, account, and reference details.
- [ ] Prevent duplicate application of advances, credit notes, loyalty value, and store credit.
- [ ] Complete bank reconciliation and cheque issue/deposit/clear/bounce lifecycle auditing.
- [ ] Add financial-year locking and stronger journal-balancing checks.
- [ ] Harden automatic Party and Bank Account ledger synchronization.
- [ ] Add customer/vendor due dashboards, cash/payment summaries, and store-group comparison views.

## Stage 8F - Audit and Automated Tests

- [ ] Add before/after JSON audit history for important entities.
- [ ] Add xUnit unit and API integration tests.
- [ ] Add PostgreSQL transaction, sequence, and concurrency tests.
- [ ] Add accounting, GST, payroll, purchase-return, and stock reconciliation tests.
- [ ] Add Playwright coverage for critical Nuxt workflows.
- [ ] Test backup and restore using a disposable database.

## Stage 8G - External Integration and Go-Live

- [ ] Validate real SMTP delivery.
- [ ] Validate Google Drive backup and restore using production credentials.
- [ ] Configure and validate the selected licensed GSTIN provider.
- [ ] Test Oracle Cloud wallet/TNS, external-app event flow, review/apply, and final auto-apply policy.
- [ ] Run clean Docker installation and fresh-database migration drills.
- [ ] Complete HTTPS, secrets, scheduled backups, retention, and log rotation.
- [ ] Run the complete role acceptance matrix and production release checklist.

## Dashboard Analytics Carried Forward from the Historical Extra List

The following were explicitly requested in the older extra list and remain acceptance items where not already complete:

- stock ageing and low-stock risk,
- customer and vendor due views,
- cash and payment summaries,
- store-group comparison charts,
- mobile navigation and collapsed-sidebar verification,
- notification/action UX,
- loading, retry, and empty-state consistency,
- documented shell revert and version history.
