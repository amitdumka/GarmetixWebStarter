# Stage 8C Package 4 - Purchase Return ITC Reconciliation

Version: 4.2.3
Build: `GARMETIX-8C-20260615-4230`
Date: 2026-06-15

## Delivered

- Added immutable ITC reversal records for every returned purchase item.
- Preserved HSN, GST rate, quantity, taxable value, and exact CGST, SGST, IGST, and total-tax values.
- Corrected fractional-return rounding so component totals always equal the posted tax total.
- Linked partial returns and full purchase cancellations to their accounting journal.
- Posted item-specific Input GST reversal lines with product and HSN narration.
- Added an end-to-end reconciliation endpoint for header, item, stock, debit note, journal, ITC, and settlement checks.
- Added reconciliation status, detailed checks, and item-level ITC rows to the Purchase Return workspace.
- Added migration backfill for existing formal purchase returns.
- Added Purchase Return and ITC Reversal records to the Audit workspace.
- Simplified the active dashboard shell to one navbar and collapse control, topbar-only account actions, and sidebar-footer notifications.

## Next

Stage 8D begins formal stock adjustment, transfer, and physical-count documents with movement-ledger reconciliation and valuation rules.
