# Stage 12E.6 - Books Vendor Payment Review

Version: 5.12.20

## Scope

This stage replaces the modular Books vendor payment and vendor settlement placeholders with read-only review workspaces. It focuses on payment register visibility, settlement audit trail, linked vouchers, purchase invoice print handoff, bank references and allocation review.

## Added

- `Vendor Payments` page:
  - Recent purchase payment register.
  - Invoice/advance filter and search.
  - Payment summary cards.
  - Selected payment detail panel.
  - Authenticated voucher PDF download when a voucher is linked.
  - Authenticated purchase invoice PDF download when a purchase invoice is linked.
- `Vendor Settlements` page:
  - Recent vendor debit-note settlement list.
  - Status filter and search.
  - Settlement summary cards.
  - Selected settlement detail panel.
  - Invoice allocation table.
  - Refund voucher PDF download when a voucher is linked.
- Version marker and modular structure validation coverage.

## Connected Endpoints

- `purchase/payments/recent`
- `purchase/vendor-settlements/recent`
- `purchase/vendor-settlements/{id}`
- `vouchers`
- `vouchers/{id}/pdf`
- `purchase/invoices/{id}/pdf`

## Safety

- No POST, PUT, DELETE, payment creation, advance creation, settlement posting, cancellation, repair or ledger mutation endpoints are called.
- Vendor payment and settlement creation remain in the controlled purchase workflows.
- PDF downloads use the stored bearer token instead of opening unauthenticated raw URLs.

## How To Test

```bash
npm --prefix modular run build:books
```

Run locally:

```bash
npm --workspace @garmetix/books-web --prefix modular run dev
```

Open:

- `http://localhost:3104/vendor-payments`
- `http://localhost:3104/vendor-settlements`

after logging in with a purchase/accounting-capable user.

## Next Step

Stage 12E.7 should connect GST return/report review in the Books app: GST return drafts, reports, production checks, accounting posting visibility and export/download handoff without posting actions.
