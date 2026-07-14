# Final Accounts Rollback Runbook

Final Accounts rollback is controlled by feature flag, reversal journals, and scoped disablement. Do not drop tables, rename operational tables, or delete posted journals.

## Feature Rollback

1. Disable `FINAL_ACCOUNTS` for the affected company scope.
2. Confirm `/final-accounts` is hidden in the modular shell.
3. Confirm `/api/final-accounts` returns feature-disabled responses.
4. Keep data for review and future repair.

## Data Rollback

Use reversal journals for posted entries. For dry-run jobs, clean up only dry-run artifacts. For export packages, mark the package superseded and keep checksum evidence.

## Recovery Evidence

Record actor, time, reason, affected company scope, journal IDs, close run IDs, exchange run IDs, and validation results after rollback.
