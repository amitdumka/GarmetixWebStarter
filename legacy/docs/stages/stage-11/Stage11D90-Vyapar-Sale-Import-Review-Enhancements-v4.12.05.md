# Stage 11D-90 — Vyapar Sale Import Review Enhancements v4.12.05

Base: v4.12.04 Stage 11D-89 Vyapar Sale Import API Build Fix.

## Added

- Matched-only invoice filter in Vyapar Sale Import preview.
- `Import Fully Matched` button for invoices where every line is matched and import-ready.
- Auto-hide already imported Vyapar invoices when source invoice number and invoice date already exist.
- Per-source bank/POS/UPI mapping for unique Vyapar payment columns.
- Payment references include Vyapar Sale Report `Description` notes for later review.
- Customer extraction from Party Name such as `Cash Sale(Amit Kumar)`.
- Customer lookup/import uses mobile number as the primary matching criteria.
- Imported Vyapar invoice list page: `/billing/vyapar-imported`.
- Nullable `Remarks` field on sale invoice table/model.
- Normal sale invoice create/edit UI can save remarks/notes.

## Notes

- A migration adds nullable `Remarks` to `SalesInvoices`.
- Imported invoice remarks are saved with `VyaparSaleImport | VyaparSourceInvoice=... | VyaparInvoiceDate=...` so future reimports can identify and hide duplicates even when Garmetix invoice numbers are auto-generated.
- Generated Garmetix invoice numbers for imported sales now use the source invoice date when `Preserve Vyapar invoice numbers` is off.
