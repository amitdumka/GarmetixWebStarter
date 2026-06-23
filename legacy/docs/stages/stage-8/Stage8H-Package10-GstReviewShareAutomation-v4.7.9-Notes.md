# Stage 8H Package 10 - GST Review & Share Automation (v4.7.9)

This package strengthens GST Returns and GST Reports so the workflow is ready for Accountant/CA review.

## GST source verification

The GST module continues to source data from the main books:

- Billing / Sales invoices for outward supply and GSTR-1/GSTR-3B output tax.
- Purchase invoices for inward supply and GSTR-3B ITC/input tax.
- GST accounting bridge for GSTR-3B payable/ITC settlement reconciliation.
- GST report pages use invoice item snapshots, HSN/product fallback, tax-rate summaries and sales/purchase invoice registers.

## Added automation

- GST Returns now has **Review & CA Sharing**.
- User loads values from books, previews validation, saves draft, reviews, then confirms email sending.
- Confirmed GST draft sends Accountant/CA email through configured SMTP.
- Return package can include JSON, Excel, HSN summary CSV, tax summary CSV and invoice register CSV.
- GST Reports page can send HSN summary, tax summary and invoice register CSV directly after review confirmation.
- WhatsApp share text/link is generated after email delivery.
- Email delivery is blocked unless SMTP is configured.
- GST draft audit records the CA share event.
