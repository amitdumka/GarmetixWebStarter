# GST Returns - Books Link Notes

The GST module remains editable/manual, but now includes a controlled **Load From Books** action.

## Endpoints

- `GET /api/gst-returns/from-books/gstr1?returnPeriod=MMYYYY&companyId=<id>`
- `GET /api/gst-returns/from-books/gstr3b?returnPeriod=MMYYYY&companyId=<id>`

## Behavior

- GSTR-1 is prepared from sales invoices and invoice items.
- GSTR-3B is prepared from sales output tax and purchase input tax summaries.
- Manual editing, preview, draft save, JSON export, Excel export, and audit trail remain available.

## Important

Review loaded GST values against GST portal/offline utility rules before filing. This link prepares data; it does not file returns automatically.
