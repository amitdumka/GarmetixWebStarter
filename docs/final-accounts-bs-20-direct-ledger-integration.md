# BS-20 - Final Accounts Direct Ledger Integration

Stage name: `BS20FinalAccountsLedgerIntegration`

Endpoint:

```text
GET /api/final-accounts/audit/direct-ledger-integration
```

Optional query parameters:

- `companyId`
- `storeGroupId`
- `storeId`
- `from`
- `to`
- `asOf`

Example:

```text
GET /api/final-accounts/audit/direct-ledger-integration?from=2026-04-01&to=2027-03-31&asOf=2027-03-31
```

## Purpose

BS-20 proves whether Final Accounts can safely read the canonical Books ledger directly.

The canonical source is:

- `LedgerGroups`
- `Ledgers`
- `JournalEntries`
- `JournalLines`

The endpoint compares that Books ledger evidence against the existing Final Accounts statement reports. It does not switch report sources yet.

## Safety

`WritesData = false`.

The endpoint must not:

- create or update Final Accounts accounts;
- create or update Final Accounts mappings;
- create sync jobs;
- create posting links;
- post journal entries;
- run schema repair;
- relink ledgers or parties;
- deploy or restart services.

## Backup Requirement

Before SRP deploy or live evidence capture, run this on the deployed host:

```bash
npm --prefix frontend/modular run deploy:srp:backup -- --stage=BS20FinalAccountsLedgerIntegration
```

Confirm `/opt/garmetix/backup/database/Backupfilehistory.md` contains the backup row.

## What The Endpoint Returns

- Direct Books ledger Trial Balance rows.
- Books ledger group classification using the BS-17 Indian/Tally-style COA rules.
- Books source-type posting evidence by `JournalEntry.SourceType`.
- Comparison of Books ledger values against Final Accounts Trial Balance, Profit & Loss and Balance Sheet.
- Issues that block report-source switching.
- Integration plan and rollback plan.

## Approval Gates

Do not switch Final Accounts report source to Books ledger until:

- BS-20 backup exists on SRP.
- Direct ledger endpoint output is captured for the approved financial year.
- Manual or low-confidence ledger group classifications are resolved.
- Source-type journal totals are balanced.
- Trial Balance, Profit & Loss and Balance Sheet differences are explained or fixed.
- Active Final Accounts mapping rows are reviewed and kept only for exception/import gaps.
- Amit/CA approve the evidence.

## Remote Handoff

After this code is pulled on the deployed host:

```bash
git pull origin version6
npm --prefix frontend/modular run deploy:srp -- --stage=BS20FinalAccountsLedgerIntegration --install-remote
```

Then capture:

```text
GET /api/final-accounts/audit/direct-ledger-integration?from=<fy-start>&to=<fy-end>&asOf=<fy-end>
```

Use the captured response as the decision evidence for the later source-switch stage.
