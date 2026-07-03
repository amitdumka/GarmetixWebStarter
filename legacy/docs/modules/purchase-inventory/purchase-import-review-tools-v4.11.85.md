# Garmetix v4.11.85 — Purchase Import Review Tools

Stage 11D-70 improves the supplier invoice import review screen after OCR/parser accuracy work. The goal is faster human verification before posting stock/accounting entries.

## Added

- Draft health summary on the supplier invoice import page:
  - active vs ignored rows
  - new vs matched products
  - missing barcode count
  - missing GST count
  - calculated total vs supplier bill amount difference
  - duplicate barcode group warning
- Bulk review actions:
  - Generate missing barcodes
  - Copy GST/category/unit/type/group defaults from the first reviewed line to new product draft lines
  - Fill missing MRP from cost
  - Ignore low-confidence unmatched OCR rows
  - Restore ignored rows
- Backend duplicate barcode guard before posting purchase inward.

## Why this matters

When invoice OCR creates many product draft lines, most rows often share the same GST/category/type/group. The user can now correct one line and copy defaults to the rest instead of editing each item manually.

Duplicate barcodes inside a draft are now blocked before posting, preventing wrong stock/product creation.

## Test checklist

1. Upload supplier invoice.
2. Review line summary cards.
3. Correct GST/category/type/group on one product line.
4. Click **Copy defaults**.
5. Click **Fill MRP** if OCR did not read MRP.
6. Click **Generate missing barcodes**.
7. Try duplicate barcode manually and confirm posting is blocked.
8. Save and post after warnings are cleared.
