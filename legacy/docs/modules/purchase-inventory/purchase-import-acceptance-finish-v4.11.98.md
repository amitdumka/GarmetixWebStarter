# Purchase Import Acceptance Finish - v4.11.98

Stage: Stage 11D-83 Purchase Import Acceptance Finish

This stage returns focus to the Purchase Import module and adds a final QA/acceptance layer before moving to modules outside purchase import.

## Added

- `GET /api/purchase-import/acceptance-summary`
  - Status-wise import health.
  - Posted/ready/review/failed counts.
  - Posted this month count and bill amount.
  - Vendor learning profile count.
  - Protected posted proof storage and unposted storage.
  - Final acceptance checklist.
  - Recent import batches.

- `GET /api/purchase-import/batches/{id}/posting-report`
  - Batch-level posting readiness.
  - Active/ignored/new/matched line counts.
  - Missing barcode, duplicate barcode, missing GST and missing new-product defaults.
  - Gross, line discount, taxable, tax, freight, round-off and grand-total reconciliation.
  - Posting checklist and blocking warnings.

- New page: `/purchase/import-acceptance`
  - Purchase Import Acceptance dashboard.
  - Recent batch diagnostics.
  - Posting report panel.
  - Direct links to Import Invoice and Import Learning.

## Purpose

Use this page before considering Purchase Import complete. It gives a single place to confirm that:

- OCR/import drafts are not stuck.
- Ready drafts can be posted.
- Failed/rejected drafts can be cleaned.
- Posted supplier proofs are protected.
- Vendor learning is active.
- Individual batches have clear blocking reasons before posting.

## Suggested test

1. Upload a supplier invoice.
2. Correct/import lines and save draft.
3. Open Purchase → Import Acceptance.
4. Open the posting report for that batch.
5. Confirm missing barcode/GST/category/discount/total issues are visible.
6. Fix the draft.
7. Reopen report and confirm it becomes Ready to post.
8. Post the draft.
9. Confirm posted proof storage remains protected.
