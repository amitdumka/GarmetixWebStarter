# Final Accounts Posting Rule Guide

Posting rules convert operational source documents into balanced Final Accounts journals. Rules must be deterministic and previewable before posting.

## Principles

Every source posting must identify company scope, source type, source ID, posting date, fiscal period, debit lines, credit lines, and an idempotency key. Debit and credit totals must match after configured rounding. No posting may cross company scope.

## Corrections

Do not edit or delete posted journals. Create a reversal, repair the source or mapping, and post again. Keep the old and new journal IDs in reconciliation evidence.

## Mapping Changes

Account mapping changes require audit evidence with before and after values. Run mapping validation before backfill, period close, Tally export, or CA package export.
