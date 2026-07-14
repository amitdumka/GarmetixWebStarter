# Purchase Import Review Polish

Version: 4.11.77  
Stage: Stage 11D-62 Purchase Import Review Polish

## Added in this stage

- Supplier invoice draft lines now support GST price mode:
  - `Inclusive`: cost entered already includes GST, matching the existing purchase inward engine.
  - `Exclusive`: cost entered is taxable rate before GST; posting converts it to inclusive cost so stock/accounting GST remains correct.
- Import review page now includes product matching against existing product master.
- Existing product match can fill barcode, HSN, unit, MRP, GST, category, subcategory, product type and group.
- Duplicate supplier invoice warnings can be overridden with an audited reason by edit/admin users.
- Current draft can be reparsed from pasted OCR/text without uploading the file again.
- Backup flow now includes supplier invoice proof files through the API backup sidecar archive and production shell backup scripts.
- Production compose now persists API `/app/data`, which stores purchase import proof files.

## Important operating note

Supplier invoice proof files are not stored inside PostgreSQL. They are kept under `/app/data/purchase-imports` inside the API container data volume. Database backups now create a companion proof archive when files exist. Keep the `.dump`, `.sha256`, `.manifest.json`, and `.purchase-import-proofs.*` files together for full restore safety.
