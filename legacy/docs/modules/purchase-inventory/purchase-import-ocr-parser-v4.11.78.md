# Purchase Import OCR Parser v4.11.78

Version: 4.11.78  
Stage: Stage 11D-63 Purchase Import OCR Parser

## Purpose

This package improves the supplier invoice import module after the draft/review foundation. The import remains safe: OCR/text extraction creates a draft only, and stock/accounting updates happen only after the operator verifies and posts the draft.

## Added in this stage

- Optional local OCR helpers for supplier invoice PDFs and images.
- Built-in PDF text extraction remains the first attempt for digital PDFs.
- Optional `pdftotext` fallback for cleaner PDF text extraction when Poppler is installed.
- Optional `pdftoppm` + `tesseract` fallback for scanned PDFs.
- Optional `tesseract` fallback for JPG/PNG/WEBP invoice images.
- OCR diagnostics JSON is stored beside the original proof file.
- Extracted text is stored as a proof artifact and can be opened from the review page.
- Review page can copy extracted text into the reparse box for another parser attempt.
- Parser now detects more supplier invoice line formats using HSN, quantity, rate, GST %, and line amount heuristics.
- Parser now estimates GST-inclusive versus GST-exclusive line mode from qty/rate/amount.
- Parser extracts basic mobile/address hints, freight, discount, and round-off where visible.

## Server setup for OCR

The feature works without these packages, but image/scanned-PDF OCR needs them installed on the Ubuntu server:

```bash
sudo apt update
sudo apt install -y tesseract-ocr poppler-utils
```

Optional environment/config keys:

```bash
PurchaseImport__TesseractPath=tesseract
PurchaseImport__PdfToTextPath=pdftotext
PurchaseImport__PdfToPpmPath=pdftoppm
PurchaseImport__OcrLanguage=eng
PurchaseImport__StorageRoot=/app/data/purchase-imports
```

## Operator flow

1. Upload supplier invoice PDF/image.
2. System stores original proof permanently.
3. System extracts text using built-in PDF extraction and optional OCR helpers.
4. System creates draft vendor/header/product lines.
5. User loads extracted text if needed.
6. User can paste extracted text into reparse box after editing OCR mistakes.
7. User verifies product, barcode, category, GST mode, tax, totals, payment, and duplicate warnings.
8. User posts verified draft to Purchase Inward.

## Safety notes

- OCR helpers are optional and fail safely.
- Missing OCR tools do not break upload; the draft remains manually reviewable.
- Original invoice proof remains immutable.
- Extracted text and diagnostics are proof artifacts, not final accounting data.
