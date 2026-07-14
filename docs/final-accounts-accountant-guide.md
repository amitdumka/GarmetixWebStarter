# Final Accounts Accountant Guide

Use Final Accounts only after the Owner or Admin enables the `FINAL_ACCOUNTS` feature flag for the selected company scope. The module is isolated from POS, HR, Books, and operational screens.

## Daily Workflow

1. Open `/final-accounts`.
2. Review dashboard warnings for unmapped accounts, unposted sources, period lock status, and trial balance differences.
3. Use Chart of Accounts to maintain account groups and accounts only in the selected company scope.
4. Use Posting Rules to preview source postings before creating journals.
5. Use General Ledger and Trial Balance reports to reconcile source totals against posted journals.
6. Export reports only after reconciliation differences are cleared or documented.

## Controls

Posted journals are immutable. Use reversal actions instead of editing posted entries. If a source posting is wrong, reverse the journal, correct the source or mapping, and repost with a new idempotency key.

## Evidence To Keep

Keep reconciliation exports, trial balance snapshots, adjustment approvals, close-run previews, and final export checksums with the monthly close file.
