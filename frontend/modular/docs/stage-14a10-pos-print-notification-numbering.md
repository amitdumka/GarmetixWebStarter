# Stage 14A.10 POS Print, Notification And Numbering Fix

Version: `6.0.26`

## Scope

- Port the legacy authenticated PDF print flow into modular POS document printing.
- Make Save & Print, sale history print, and print queue retry use the same hidden-iframe browser print path.
- Move POS operational success/error notifications to the right-bottom of the screen.
- Change new sale invoice numbers to `StoreCode-YYYYMM-INV-NumberSeries`.
- Track remaining physical printer and DotMatrix print-service parity.

## Legacy Reference

Legacy new sale uses `useServerDocumentPrint().printPdf(...)`, which:

- fetches the PDF with the current bearer token,
- validates the server returned an `application/pdf` document,
- loads it into a hidden iframe,
- calls the browser print command from that iframe.

The modular POS previously created a blob URL and opened it in a new browser tab. That could fail silently or look like a PDF-load failure on locked-down browser settings.

## Changes

- `frontend/modular/apps/pos/utils/pos-documents.ts` now mirrors the legacy hidden-iframe print flow.
- POS page messages use `PosToast`, positioned fixed at the bottom-right.
- `DocumentNumberService.NextSaleInvoiceAsync(...)` now returns new sale invoice numbers like `AFSS-202607-INV-0005`.

## Testing Notes

- Build modular POS.
- Build the shared ASP.NET API.
- Create a sale invoice from `/pos/sale` and confirm the print dialog appears.
- Print again from `/pos/history`.
- Retry from `/pos/print`.
- Confirm existing slash-format historical invoice numbers still open correctly.

## Remaining Risks

- Physical Epson LX-310 and DotMatrix service printing still need live device confirmation.
- Browser print behavior can still be affected by per-browser PDF/print settings.
- Existing saved invoices keep their original invoice numbers; only new sale invoice numbers use the hyphen format.
