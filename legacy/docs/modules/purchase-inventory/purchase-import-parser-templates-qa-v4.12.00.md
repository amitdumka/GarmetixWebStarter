# Purchase Import Parser Templates QA - v4.12.00

Stage: Stage 11D-85 Purchase Import Parser Templates QA  
Build: GARMETIX-11D85-20260627-4200

## Purpose

This stage makes the supplier invoice import parser more auditable and repeatable before moving outside Purchase Import.

## Added

- Parser/template catalog endpoint: `GET /api/purchase-import/parser-templates`
- Visible parser/template stored on import batch:
  - `ParserTemplate`
  - `ParserTemplateReason`
  - `ImportQaNotes`
- Vendor profile parser override:
  - Auto detect
  - Tally Prime / common GST invoice
  - Garment article/brand/size column layout
  - Generic conservative parser
- Parser template decision is written into `parser-line-decisions.json`.
- Purchase Import Acceptance recent list shows parser template used.
- Import review page shows parser/template reason and allows QA notes.
- Vendor learning page lets admin/save preferred parser template for repeated vendor layouts.
- Flexible Tally column fallback added for item rows where columns vary but quantity/rate/amount can be reconciled.

## Usage

1. Import a supplier invoice.
2. Check the parser/template shown in the Extraction status box.
3. Open parser-line-decisions.json if line detection is wrong.
4. Go to Purchase → Import Learning.
5. Select the vendor profile and set preferred parser template when the vendor always follows a known layout.
6. Add notes like `Tally Prime, GST exclusive, item name from Art No + Brand + Size`.

## QA checklist before leaving Purchase Import

- Manual purchase inward still saves.
- S.K APPARELS invoice extracts item lines, discount, tax and grand total.
- A Tally Prime invoice extracts invoice number, date, vendor and items.
- Bad OCR rows can be ignored and learned.
- Vendor parser override improves repeated import.
- Audit JSON/CSV include enough information to debug incorrect imports.
- Posted proof opens from purchase list/receipt.
- Price tags can be printed from posted imported inward.
