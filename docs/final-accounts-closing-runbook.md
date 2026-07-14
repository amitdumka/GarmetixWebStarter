# Final Accounts Closing Runbook

Period close locks the selected fiscal period after required checks pass. Year close additionally creates retained earnings and opening balance evidence.

## Steps

1. Confirm all source posting jobs are complete.
2. Run mapping validation.
3. Run reconciliation summary.
4. Confirm Trial Balance is balanced.
5. Review open CA adjustments.
6. Run period close preview and save checklist output.
7. Commit close with an approval note.
8. Export close evidence, Trial Balance, Balance Sheet, Cash Flow, and schedules.

## Reopen

Reopen only with documented approval. Reopen must create an audit event and must not delete historical close snapshots.
