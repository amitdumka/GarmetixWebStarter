# Stage 12E.7 - Books GST Review

Version: 5.12.21

## Scope

This stage replaces the modular Books GST placeholders with read-only review workspaces for GST returns, GST reports and GST production readiness. It exposes export/download handoff where safe and intentionally avoids filing, posting, save, delete and send-review actions.

## Added

- `GST Returns` page:
  - Saved GST return draft list.
  - Draft selector and selected draft detail review.
  - Preview issue visibility.
  - Authenticated JSON and Excel export downloads for saved drafts.
  - Books-derived GSTR-1 and GSTR-3B preview totals.
  - GST accounting bridge summary visibility without posting.
- `GST Reports` page:
  - HSN summary review.
  - Tax rate summary review.
  - Invoice register review.
  - Sales/purchase/both direction filter where supported.
  - Authenticated CSV downloads.
- `GST Production Readiness` page:
  - GST export readiness.
  - E-invoice status.
  - GSTIN provider status.
  - GST schema review mapping.
  - Authenticated schema-review Excel download.
- Version marker and modular structure validation coverage.

## Connected Endpoints

- `gst-returns/drafts`
- `gst-returns/drafts/{id}`
- `gst-returns/drafts/{id}/json`
- `gst-returns/drafts/{id}/excel`
- `gst-returns/from-books/gstr1`
- `gst-returns/from-books/gstr3b`
- `gst-returns/accounting-summary`
- `gst-returns/reports/hsn-summary`
- `gst-returns/reports/hsn-summary/csv`
- `gst-returns/reports/tax-summary`
- `gst-returns/reports/tax-summary/csv`
- `gst-returns/reports/invoice-register`
- `gst-returns/reports/invoice-register/csv`
- `gst-returns/schema-review`
- `gst-returns/schema-review/excel`
- `gst-production/readiness`
- `gst-production/e-invoice/status`
- `gstin/provider/status`

## Safety

- No POST, PUT, DELETE, filing, draft save/update, accounting posting, provider test, email send or WhatsApp send endpoints are called.
- Draft export GET endpoints may add export audit records on the API side; this is treated as document handoff visibility and remains user-triggered.
- Admin-only final acceptance is not called from the Books app.
- Downloads use the stored bearer token instead of opening unauthenticated raw URLs.

## How To Test

```bash
npm --prefix modular run build:books
```

Run locally:

```bash
npm --workspace @garmetix/books-web --prefix modular run dev
```

Open:

- `http://localhost:3104/gst-returns`
- `http://localhost:3104/gst-reports`
- `http://localhost:3104/gst-production`

after logging in with an accounting-capable user.

## Next Step

Stage 12E.8 should connect financial year locks and accounting audit/message-log review inside Books, then decide whether the remaining Books screens need write-enabled slices or should stay as review-only in Version5.
