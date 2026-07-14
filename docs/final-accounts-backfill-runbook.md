# Final Accounts Backfill Runbook

Use backfill only in staging or an approved test company until production rollout is separately reviewed.

## Steps

1. Confirm `FINAL_ACCOUNTS` is enabled only for the intended scope.
2. Export the current source transaction counts.
3. Run posting preview and resolve mapping errors.
4. Run dry-run backfill and save the dry-run job result.
5. Compare source totals to journal totals.
6. Run live backfill only after approval.
7. Save job number, actor, source window, posted count, skipped count, failed count, and reconciliation export.

## Rollback

Backfill rollback is reversal-based. Do not delete posted journals. Reverse the affected backfill journals by job number and rerun reconciliation.
