# Stage 13B.3 - POS Print Recovery and Document Search

Version: 5.13.8

## Scope

This stage keeps the API and database unchanged. It hardens the modular POS frontend around saved document recovery when PDF printing fails and makes return lookup more tolerant of scanned references.

## Added

- Shared POS document helper for opening billing invoice PDFs.
- Browser popup-block detection for invoice/return PDF printing.
- Save-success messaging that does not turn a saved invoice into a false save error when only printing fails.
- Return search normalization for plain invoice numbers, URLs, JSON QR payloads and common reference prefixes.

## Updated Workflows

- New Sale: invoice is queued and cleared from draft after save; print failure is shown as a warning with Print Queue recovery.
- Sales Returns: return document is queued after save; print failure no longer reports the return save as failed.
- Print Queue: uses the same invoice PDF helper as sale and return workflows.
- Return Search: scanned references are normalized before matching recent invoices.

## Validation

Run:

```powershell
npm.cmd --prefix modular run build:pos
npm.cmd run modular:check
npm.cmd run modular:validate -- --skip-builds
```

## Remaining Follow-Up

Server-side held bill persistence and multi-terminal print audit can be added later if the store workflow requires recovery across different browsers or counters.
