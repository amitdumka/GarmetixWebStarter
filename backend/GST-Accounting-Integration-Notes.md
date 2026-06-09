# GST Accounting Integration Notes

This update connects the standalone GST Returns module with the accounting service while keeping manual GST entry/draft/export available.

## New endpoints

- `GET /api/gst-returns/accounting-summary?companyId={id}&returnPeriod=MMYYYY`
- `POST /api/gst-returns/accounting-posting`
- `POST /api/gst-returns/drafts/{id}/accounting-posting`

## Accounting behavior

The GST accounting bridge reads the accounting ledger movement for the selected GST period:

- `Output GST` = credit movement minus debit movement, excluding existing GST settlement journals.
- `Input GST` = debit movement minus credit movement, excluding existing GST settlement journals.

For GSTR-3B posting:

- Output GST is debited to clear liability from sales postings.
- Input GST is credited to set off eligible ITC.
- Net payable is credited to `GST Payable`.
- Excess ITC is debited to `GST Credit Carry Forward`.
- Interest and late fee are debited to `GST Interest & Late Fee` and credited to `GST Payable`.

Posting is idempotent for the same period/draft: the existing `GstReturnAccounting` journal is replaced instead of duplicated.

## Frontend

The GST Returns page now includes a **GST Accounting Bridge** panel with:

- Refresh Accounting
- Post Current GSTR-3B
- Post Saved Draft
- Ledger movement table
- Last posting result

## Database repair

Startup schema repair now runs even when `Database:AutoMigrate=false`. This protects existing Docker volumes that already have migrations marked as applied but are missing newer runtime tables.
