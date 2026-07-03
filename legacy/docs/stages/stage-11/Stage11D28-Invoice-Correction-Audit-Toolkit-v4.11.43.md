# Stage 11D-28 / v4.11.43 - Invoice Correction Audit Toolkit

Adds database-level audit tools for purchase/sale invoice corrections.

## Scripts

- `scripts/production/install-invoice-correction-audit.sh`
- `scripts/production/invoice-correction-audit-report.sh`
- `scripts/production/sql/install-invoice-correction-audit.sql`
- `scripts/production/sql/invoice-correction-audit-report.sql`

## Captured events

- SalesInvoices update/delete
- PurchaseInvoices update/delete
- InvoiceItems update/delete

The audit table stores table name, operation, invoice id/number, old values, new values, DB user and timestamp.

## Install

```bash
cd /opt/garmetix/current
chmod +x scripts/production/*.sh
./scripts/production/install-invoice-correction-audit.sh
```

## Report

```bash
./scripts/production/invoice-correction-audit-report.sh
```
