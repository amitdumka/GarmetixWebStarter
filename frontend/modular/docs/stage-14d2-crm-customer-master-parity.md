# Stage 14D.2 CRM Customer Master Parity

Version: `6.0.31`

This stage promotes the CRM customer master from read-only route coverage to usable customer operations.

## Added In This Stage

- Customer register with legacy-style metrics:
  - customer count
  - loyalty point total
  - credit balance total
  - GST mismatch alert count
- Searchable customer register with Edit and Loyalty actions.
- Reusable `CustomerForm` component for new and edit pages.
- New customer save using `/api/customers`.
- Existing customer update using `/api/customers/{id}`.
- GSTIN validation using `/api/gstin/validate-party`.
- Inline loyalty ledger preview using `/api/loyalty/customers/{customerId}/ledger`.
- Route registry status changed to live for customer list, new and edit routes.

## Legacy References

- `frontend/legacy/garmetix-web/pages/customers/index.vue`
- `frontend/legacy/garmetix-web/components/CustomerEntryForm.vue`
- `frontend/legacy/garmetix-web/pages/customers/new.vue`
- `frontend/legacy/garmetix-web/pages/customers/[id].vue`

## Validation

- Build CRM app.
- Run modular structure validation.
- Deploy SRP static site when build is clean.
- Check `/crm/customers`, `/crm/customers/new` and `/crm/customers/{id}` after login.

## Next Stage

Stage 14D.3 should complete dues reconciliation and loyalty management parity:

- dues CSV/evidence export
- loyalty summary
- loyalty ledger filters
- guarded manual loyalty adjustment
