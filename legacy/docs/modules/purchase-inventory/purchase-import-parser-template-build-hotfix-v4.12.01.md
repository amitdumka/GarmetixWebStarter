# Purchase Import Parser Template Build Hotfix - v4.12.01

Stage: Stage 11D-86 Purchase Import Parser Template Build Hotfix  
Build: GARMETIX-11D86-20260627-4201

## Fix

`PurchaseInvoiceImportService.ParseInvoiceText` now initializes `parserSelection` before passing the parser template key into line guessing and before returning parser-template metadata.

This fixes the deploy/build error:

```text
CS0103: The name 'parserSelection' does not exist in the current context
```

## Kept intact

- Parser template tracking
- Vendor preferred parser override
- Tally Prime / garment-column / generic-conservative catalog
- Import QA notes
- Parser decision audit output
