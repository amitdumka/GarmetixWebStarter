# Final Accounts BS-15 Merge Readiness Package

Date: 2026-07-14  
Branch: `balancesheet`  
Target branch: `version6`  
Workspace: `C:\AIarea\Codex\bsheet\GarmetixWebStarter`  
Base `version6` commit: `4d3c2b5c13b99fed0237203087d3702b13fd2608`  
BS-15 start commit: `058e23e01d38fc92916fd83617d0a3cb1a52fa0e`  
BS-15 package commit: created by this stage commit; exact hash is reported in session output because a commit cannot contain its own final hash.

## Decision

This package is ready for human review. It does not merge the branch, does not deploy, and does not run production migrations.

Required human decision before merge:

- Review final accounts scope, migrations, security rules and rollout proposal.
- Approve a staging migration/backfill rehearsal.
- Approve or reject merge from `balancesheet` into `version6`.
- Approve any production rollout separately after staging evidence is attached.

## Branch Update

- Fetched `origin/version6` and `origin/balancesheet`.
- Confirmed merge base of `balancesheet` and `origin/version6` is `4d3c2b5c13b99fed0237203087d3702b13fd2608`.
- Confirmed `origin/version6` points to the same commit, so the feature branch is based on the latest fetched `version6`.
- No rebase or merge commit was required.
- No conflicts were present.

## Commit List

Commits included in `origin/version6..balancesheet` at BS-15 start:

```text
8b1b199 docs(final-accounts): add roadmap and isolated implementation plan
ad478ca feat(final-accounts): add disabled module skeleton
7342445 feat(final-accounts): add catalog and fiscal setup
42004c1 feat(final-accounts): add general ledger journals
a96281f feat(final-accounts): add posting rules and mapping validation
6897ce1 feat(final-accounts): add cash bank posting adapters
f4bbc36 feat(final-accounts): add sales posting adapters
4cb5328 feat(final-accounts): add purchase posting adapters
4d902c3 feat(final-accounts): add inventory cogs adapters
76b95ef feat(final-accounts): add payroll tax and tailoring adapters
58e6170 feat(final-accounts): add sync backfill reconciliation foundation
1dfd6e1 feat(final-accounts): add ledger and trial balance reports
3433224 feat(final-accounts): add profit loss statement engine
5891097 feat(final-accounts): add balance sheet cash flow schedules
aa934ef feat(final-accounts): add ca adjustment workspace
c05c84b feat(final-accounts): add period closeout workflow
638f3b5 feat(final-accounts): add projection engine
5c4863a feat(final-accounts): add tally exchange ca package
f0ecb47 docs(final-accounts): add security audit package
058e23e test(final-accounts): add bs14 qa hardening
```

This BS-15 package is appended as a documentation/readiness commit after the list above.

## File And Change Summary

Backend API:

- New `backend/Garmetix.Api/FinalAccounts` module namespace.
- Feature-flagged API root at `/api/final-accounts`.
- Disabled guard, setup/status endpoints, catalog, journals, posting rules, adapters, sync/backfill, reports, CA workspace, closeout, projections, Tally exchange and CA package endpoints.
- Permission policy registered as `GarmetixPolicies.FinalAccounts`.

Backend domain and data:

- New Final Accounts domain models under `backend/Garmetix.Domain/Generated/Models/FinalAccounts`.
- Shared `GarmetixDbContext` mappings and `DbSet` entries for Final Accounts tables.
- Additive migrations under `backend/Garmetix.Infrastructure/Data/Migrations`.

Backend tests:

- Final Accounts rule tests for catalog, journals, posting, adapters, sync, reports, CA workspace, closeout, projections, exchange, security and QA.
- Access permission matrix tests confirm no default access for existing non-admin operational roles.

Frontend:

- New isolated Nuxt app under `frontend/modular/apps/final-accounts`.
- Registered route root `/final-accounts`.
- Pages for setup, chart of accounts, fiscal periods, journals/general ledger, posting rules, reports, CA workspace, period closeout, projections and exchange.
- Shared app registry, route registry and shell navigation updated.

Docs and operations:

- Roadmap/TODO source of truth.
- Accountant, CA review, developer architecture, posting rule, backfill, reconciliation, closing, rollback, security audit and QA hardening docs.
- BS-15 merge-readiness package.

## Migration Summary

Final Accounts migrations are additive and scoped to the `final_accounts` schema:

```text
20260713153000_AddFinalAccountsModuleSettings
20260713162000_AddFinalAccountsCatalogAndFiscalPeriods
20260713170000_AddFinalAccountsGeneralLedger
20260713173000_AddFinalAccountsPostingRules
20260713190000_AddFinalAccountsSyncJobs
20260714120000_AddFinalAccountsCaWorkspace
20260714130000_AddFinalAccountsPeriodClose
20260714140000_AddFinalAccountsProjectionEngine
20260714150000_AddFinalAccountsExchangePackage
```

Review result:

- No operational POS, HR, Books, Sales, Purchase, Inventory or Payroll table is dropped, renamed or rewritten.
- New foreign keys reference Final Accounts tables where possible and operational/source rows only through stored source metadata.
- Posted journals are immutable; corrections use reversal journals.
- Production migrations were not executed from this workspace.

## Feature Flag Activation Steps

The module remains disabled by default.

Staging activation:

1. Deploy the branch to a staging environment only after human approval.
2. Apply migrations to a staging database backup or clone.
3. Sign in as an authorized admin/owner.
4. Open `/final-accounts/setup`.
5. Enable feature key `FINAL_ACCOUNTS` for the intended company/store scope.
6. Keep scheduled posting disabled until reconciliation is reviewed.
7. Configure Chart of Accounts, fiscal periods and mappings.
8. Run dry-run backfill and review mapping validation.
9. Enable manual posting/backfill only after the dry-run result balances.

Production activation requires a separate approval after staging evidence is attached.

## Staging-Only Backfill Plan

1. Take a staging database backup and record the restore point.
2. Apply all Final Accounts migrations to staging.
3. Enable Final Accounts only for one agreed company/store scope.
4. Seed or configure the garment-retail Chart of Accounts.
5. Configure fiscal year and fiscal periods.
6. Complete required mappings for Sales, Purchase, Inventory, Cash/Bank, GST, Payroll, Tailoring and Other Income.
7. Run mapping validation and resolve all required missing mappings.
8. Run backfill dry-run by module and date range.
9. Review candidate counts, source hashes, unbalanced previews and exceptions.
10. Run manual sync/backfill in bounded batches.
11. Re-run reconciliation after each batch.
12. Freeze evidence before production approval.

## Reconciliation Evidence Required

Staging reviewer must attach:

- Source control totals by module and date range.
- Final Accounts journal count and total debits/credits.
- Trial Balance with zero difference.
- Balance Sheet with `Assets = Liabilities + Equity`.
- Profit & Loss net result tied to retained earnings/current-year result.
- Cash Flow cash-equivalent movement tied to opening/closing cash and bank accounts.
- Exception report showing resolved/unresolved items.
- Tally/CA package control totals if exchange is in scope.

Workspace status:

- No live staging database was supplied in this workspace.
- Reconciliation model and report endpoints are implemented and tested at rule level.
- Live dataset evidence must be produced in staging before merge/production approval.

## Test Evidence

BS-15 validation rerun:

- Backend build: passed with 7 existing nullable warnings outside the Final Accounts module and 0 errors.
- Backend tests: passed, 219 passed, 3 skipped, 222 total.
- Final Accounts readiness: passed for BS-15.
- Frontend structure check: passed.
- Workspace links: passed.
- Final Accounts production build: passed.
- Full modular validation: passed.
- Full validator API build: passed with 0 warnings, 0 errors.
- Full validator warnings: existing manual/live-token acceptance warnings for POS, HR, Books and CRM; existing Nuxt/Rollup/unifont/cache-driver warnings.

Additional BS-15 hygiene:

- `git diff --check`: passed.
- Secret scan: passed with key-shaped token patterns.

## Production Rollout Proposal

1. Human review signs off this package.
2. Staging database migration is rehearsed from a fresh backup.
3. Staging feature flag activation and backfill are completed.
4. Reconciliation evidence is attached and approved by business/accounting reviewer.
5. CA/accountant signs off opening balances, mappings, P&L, Balance Sheet and exception report.
6. Release window is scheduled with database backup and rollback owner assigned.
7. Migrations are applied to production during the approved window.
8. Feature flag remains disabled immediately after deployment.
9. Enable Final Accounts for one approved scope only.
10. Run bounded manual backfill and reconcile before broad enablement.

## Rollback Proposal

Operational rollback:

- Disable `FINAL_ACCOUNTS` from `/final-accounts/setup` or equivalent settings path.
- Stop scheduled sync/backfill if enabled.
- Do not delete or edit posted journals.
- Correct accounting data with reversal journals only.
- Preserve export packages and audit logs for review.

Deployment rollback:

- Roll back application version using the normal release process if needed.
- Leave additive Final Accounts tables in place unless a DBA-approved rollback migration is explicitly scheduled.
- Do not drop `final_accounts` tables as an emergency operational rollback.

Database rollback:

- Use database backup/restore only under DBA approval.
- If migrations must be reverted in non-production, use EF Down paths against the `final_accounts` schema only after evidence is exported.

## Known Limitations

- No live staging PostgreSQL dataset was available in this workspace, so staging migration execution and backfill reconciliation remain pending.
- Browser/manual QA is marker and build validated; an interactive staging browser pass is still required after deployment.
- TallyPrime direct validation requires an approved test company and was not executed locally.
- CA package supporting-document embedding is represented by manifest/evidence paths until document storage and redaction rules are approved.
- Full validator may print existing Nuxt/Rollup/unifont provider warnings that do not fail the build.

## Human Review Request

Reviewer required: engineering lead plus accounting/CA reviewer.

Decision options:

- Approve staging rehearsal.
- Request changes.
- Reject merge.

Current decision: pending human review.

## Guardrails

- No auto-merge.
- No auto-deploy.
- No production migration.
- Do not merge into `version6` until human review approves.
