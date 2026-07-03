# Purchase Import Revision Backup QA - v4.12.38

## Stage

Stage 11D-123 Purchase Import Revision Backup QA

## Purpose

This stage closes remaining safety gaps around Purchase Invoice Import before moving outside the module.

## Added

### Correction / revision plan

A new correction plan endpoint and UI action explain the safe path when an imported purchase is wrong.

- Unposted imports: correct draft, rerun posting report, then post.
- Posted imports: keep supplier proof protected and use controlled purchase inward revision/return/reversal. Direct delete/undo is intentionally not allowed.

Endpoint:

```text
GET /api/purchase-import/batches/{id}/correction-plan
```

### Backup / restore proof checklist

Purchase import proof files live under `/app/data/purchase-imports` in the Docker app-data volume. Database backup alone is not enough.

Endpoint:

```text
GET /api/purchase-import/backup-restore-checklist
```

The Import Acceptance page now shows storage root, proof storage, backup commands, and restore verification steps.

### Parser QA summary

The Import Acceptance page now shows parser template coverage and vendor parser summary so Tally/Garment/Generic parsers can be accepted using real invoices.

Endpoint:

```text
GET /api/purchase-import/parser-qa-summary
```

### Parser template addition

Added a QA-focused template option:

```text
tally-prime-mrp
```

Use this for Tally invoices with MRP columns, CGST/SGST split, discount, freight, or packing rows.

## Test checklist

1. Build and deploy.
2. Open Purchase → Import Acceptance.
3. Confirm Parser QA cards load.
4. Confirm Backup / restore proof checklist loads.
5. Open a posted import and click Plan.
6. Confirm posted imports show direct undo blocked and controlled correction guidance.
7. Run backup script and confirm `.purchase-import-proofs.tar.gz` is created when proof files exist.
8. Restore on test system and confirm supplier proof opens from posted purchase inward.

## Notes

This stage does not perform automatic reversal of posted purchases. That must remain a controlled purchase module operation because stock ledger, vendor balance, GST and accounting must all be reversed together.
