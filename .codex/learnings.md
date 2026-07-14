# Codex Learnings

## Stage Backups Are Mandatory

As of 2026-07-14, Amit requires a database backup before every implementation stage or deploy that can affect production data.

The backup location is fixed:

```text
/opt/garmetix/backup/database/
```

The history file is fixed:

```text
/opt/garmetix/backup/database/Backupfilehistory.md
```

Use stage names in filenames:

```text
BS16AccountingMasterAudit
BS17IndianCOANormalization
BS18PartyLedgerUnification
```

Do not perform schema repair, data migration, accounting unification, backfill, production deploy or service restart that can affect the database without first running:

```bash
npm --prefix frontend/modular run deploy:srp:backup -- --stage=<StageName>
```

The SRP whole-site deploy script now runs this automatically before upload when called with `--stage=<StageName>`.

This Windows workstation could not reach the deployed SRP host during BS-16 on 2026-07-14 (`192.168.11.127:22` and ping timed out). For that network case, implement and push code locally, then run the backup/deploy/audit from the remote system after pull.

## Accounting Unification Direction

Final Accounts should not stay as a separate accounting universe. Existing Books ledgers, ledger groups, parties and operational source modules must become the canonical accounting master. Mapping should become an exception tool, not the normal setup path.

Use Indian operational accounting practice and TallyPrime/BUSY/Marg-style COA grouping first. Ind AS reporting should be optional/legal-requirement-driven, not forced by default.

BS-17 implements only a read-only COA normalization preview at `/api/final-accounts/audit/coa-normalization`. Do not treat its classifications as approved migration instructions until the deployed-host backup, live preview evidence and Amit/CA review are complete.

BS-18 implements only a read-only party-ledger unification preview at `/api/final-accounts/audit/party-ledger-unification`. Do not relink `Customer.PartyId`, `Vendor.PartyId`, create employee parties, merge parties or merge ledgers until backup, live preview evidence and Amit/CA approval are complete.
