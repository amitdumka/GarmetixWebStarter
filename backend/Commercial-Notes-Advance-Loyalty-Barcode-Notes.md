# Commercial Notes, Customer Advances, Loyalty, and Barcode Lookup

This module adds a separate commercial-document layer without removing the existing Billing/Purchase flows.

## Debit / Credit Notes

Endpoints:

- `GET /api/commercial-notes`
- `GET /api/commercial-notes/{id}`
- `POST /api/commercial-notes`
- `GET /api/commercial-notes/{id}/pdf?a5Slip=false|true&reprint=false|true&signatures=true|false`
- `POST /api/commercial-notes/{id}/mark-printed`

Notes can be issued manually to customer, vendor, supplier, or other parties. Sales returns create a Credit Note. Purchase cancellation/return creates a Debit Note. Notes remain unadjusted by default (`IsAdjusted=false`) so adjustment can be controlled later.

## Customer Advance Receipt

Endpoints:

- `GET /api/customer-advances`
- `POST /api/customer-advances/receipts`

A receipt increases the customer `CreditBalance`. That balance can later be consumed by exchange or future payment flows.

## Loyalty Program

Endpoints:

- `GET /api/loyalty/program?storeId=...`
- `POST /api/loyalty/program`
- `GET /api/loyalty/customers/{customerId}/ledger`

Sales invoices award loyalty points when the program is enabled for the store. Sales returns reverse points proportionally.

## Product Barcode / Autocomplete Lookup

Endpoints:

- `GET /api/product-lookup?query=...&storeId=...`
- `GET /api/product-lookup/barcode/{barcode}?storeId=...`

The lookup returns product id, barcode, name, HSN code, available quantity, MRP, tax rate, tax type, unit, category, and subcategory. Billing and Purchase pages cache the lookup in browser local storage.

## Invoice / Voucher Scan Lookup

Endpoint:

- `GET /api/scan/{code}`

Invoice, purchase invoice, voucher, and cash voucher PDFs now print a scan code. Scanning/entering that code can fetch the matching document.
