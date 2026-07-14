# Purchase Import Verification Polish v4.11.81

Version: 4.11.81  
Stage: Stage 11D-66 Purchase Import Verification Polish

This stage continues the supplier invoice import module after the v4.11.80 build fix.

## Added

- Duplicate supplier invoice recheck after vendor/invoice details are corrected.
- Header discount distribution into item line discounts so drafts can post through the existing purchase inward calculation model.
- Reparse mode selector:
  - Append newly detected lines.
  - Replace existing draft lines with newly detected lines.
- Stored proof/audit file list on the import review page.
- Download/open access for import files using authenticated API fetch.
- Initial draft JSON, corrected draft JSON, extracted text, OCR diagnostics, original proof, and final posted snapshot tracking.
- Final posted snapshot JSON after a draft is posted to purchase inward.

## Why this matters

Supplier invoices often include header-level discounts and users may correct vendor or invoice numbers after OCR. Before this stage, the import draft could stay blocked or could keep an outdated duplicate warning. Now the review page can refresh duplicate status and convert header discounts into line-level discounts before posting.

## Files changed

- `backend/Garmetix.Api/PurchaseImport/PurchaseInvoiceImportDtos.cs`
- `backend/Garmetix.Api/PurchaseImport/PurchaseInvoiceImportEndpoints.cs`
- `backend/Garmetix.Api/PurchaseImport/PurchaseInvoiceImportService.cs`
- `frontend/garmetix-web/pages/purchase/import.vue`
- `backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs`
- `backend/Garmetix.Api/Garmetix.Api.csproj`
- `frontend/garmetix-web/utils/appVersion.ts`
- `frontend/garmetix-web/package.json`
- `frontend/garmetix-web/package-lock.json`

## Manual test checklist

1. Upload supplier invoice PDF/image/TXT.
2. Open stored proof and audit files from the review page.
3. Edit vendor/invoice number and click duplicate recheck.
4. Enter header discount and distribute it to item lines.
5. Reparse OCR text in append mode.
6. Reparse OCR text in replace-lines mode.
7. Save draft and verify corrected draft JSON appears in stored files.
8. Post inward and verify final posted snapshot appears in stored files.
9. Confirm purchase invoice, stock, vendor balance, accounting posting, and supplier proof link.
