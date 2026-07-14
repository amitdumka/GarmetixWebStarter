# Purchase Supplier Invoice Import Draft

Version: 4.11.77  
Stage: Stage 11D-62 Purchase Import Review Polish

## Purpose

This module adds a safe supplier bill import workflow for purchase inward. The app stores the original supplier invoice PDF/image as proof, creates an editable draft, lets the user verify supplier/product/GST/totals, and only then posts the verified draft into the normal purchase inward, stock, vendor balance, payment, and accounting flow.

## Implemented in this stage

- New Purchase Invoice Import backend module under `Garmetix.Api/PurchaseImport`.
- New import tables:
  - `PurchaseInvoiceImportBatches`
  - `PurchaseInvoiceImportLines`
  - `PurchaseInvoiceImportFiles`
- Schema repair creates the new tables/indexes automatically on startup.
- Upload endpoint accepts PDF, image, WEBP, and TXT files up to 20 MB.
- Original upload is saved as immutable supplier invoice proof under `data/purchase-imports`.
- SHA-256 file hash is stored for duplicate-file checks.
- Built-in text extraction for TXT and simple digital PDF text streams.
- Image/scanned invoice flow is supported as manual-review draft until an OCR provider is configured.
- Basic extraction for GSTIN, invoice number/date, tax components, total amount, vendor guess, and simple item rows.
- Existing vendor matching by GSTIN/name.
- Existing product matching by barcode/name.
- GST/tax matching by nearest composite rate.
- Editable draft update endpoint.
- Draft validation before posting.
- Draft posting creates/updates vendor, product, stock, stock movement, purchase invoice, purchase payment, and accounting entries.
- Posted purchase invoice remains linked to the supplier proof file.
- New frontend page: `Purchase -> Import Supplier Invoice`.
- Purchase receipt modal shows a `Supplier proof` button when the invoice was posted from an import draft.

## API endpoints

- `GET /api/purchase-import/batches`
- `GET /api/purchase-import/batches/{id}`
- `POST /api/purchase-import/uploads`
- `PUT /api/purchase-import/batches/{id}`
- `POST /api/purchase-import/batches/{id}/post`
- `POST /api/purchase-import/batches/{id}/reject`
- `GET /api/purchase-import/batches/{id}/proof`
- `GET /api/purchase-import/purchase-invoices/{purchaseInvoiceId}/proof`

## Frontend workflow

1. Go to `Purchase -> Import Supplier Invoice`.
2. Upload supplier invoice PDF/image/TXT.
3. Optional: paste extracted text for scanned invoices until OCR provider is configured.
4. Review supplier details.
5. Review or add product lines.
6. Generate barcode for new product lines if needed.
7. Select category/sub-category/GST.
8. Save draft.
9. Fix warnings.
10. Post inward.
11. Open the purchase invoice receipt and use `Supplier proof` to view the original uploaded invoice.

## Remaining todo

- Add production OCR provider integration for scanned PDF/image invoices.
- Add better table parser for supplier-specific invoice formats.
- Add product search/mapping dialog inside the draft line editor.
- Add duplicate override permission for owner/admin.
- Add line-level inclusive/exclusive GST selector.
- Include `data/purchase-imports` in deployment backup/restore scripts.
- Add tests for upload, draft validation, duplicate invoice detection, posting, stock ledger, vendor balance, and proof download.


## v4.11.77 follow-up

See `purchase-import-review-polish-v4.11.77.md` for GST inclusive/exclusive review, product matching, duplicate override audit, reparsing, and proof backup updates.

## v4.11.78 follow-up

See `purchase-import-ocr-parser-v4.11.78.md` for optional local OCR helpers, extracted text access, improved parser heuristics, Docker OCR package installation, and scanned PDF/image import readiness.
