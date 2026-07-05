# Stage 14D.3 CRM Loyalty And Dues Reconciliation Parity

Version: `6.0.33`

This stage promotes the CRM loyalty and customer dues reconciliation routes from read-only placeholders into usable CRM workflows.

## Added

- CRM `/loyalty` now supports:
  - company/store/customer scope selection
  - loyalty program load and save through `/api/loyalty/program`
  - customer loyalty summary through `/api/loyalty/customers/{customerId}`
  - customer loyalty ledger through `/api/loyalty/customers/{customerId}/ledger`
  - guarded manual point adjustment through `/api/loyalty/customers/{customerId}/adjust`
- CRM `/customers/dues-reconciliation` now supports:
  - company/store/date filters
  - reconciliation summary metrics
  - closeout checklist, operator rules and known limitations
  - issue table and customer evidence table
  - credit source summary cards
  - authenticated CSV evidence export through `/api/customers/dues-reconciliation/evidence.csv`
- CRM API helper now supports authenticated blob downloads for CSV/export style endpoints.

## Validation

- Build CRM app.
- Run modular structure validation.
- Open `/crm/loyalty/` and confirm loyalty program plus customer ledger load after login.
- Open `/crm/customers/dues-reconciliation/` and confirm the report loads and CSV export downloads.

## Remaining CRM Work

- Digital Bill register actions: generate, copy/open, send WhatsApp, activity timeline, disable and regenerate token.
- Digital Bill analytics with date/store filters and export handoff.
- Feedback, review settings, WhatsApp settings/logs and guarded retry/test-send actions.
- Campaign audiences, campaigns and invoice ad banner CRUD.
- Public `/i/:token` customer digital bill page.
