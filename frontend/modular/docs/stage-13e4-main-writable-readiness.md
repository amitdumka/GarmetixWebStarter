# Stage 13E.4 Main Writable Readiness

Version: 5.13.34
Branch: Version5

## Purpose

Stage 13E.4 adds a non-mutating readiness gate for Main Back Office workflows that will later become writable or action-oriented. It keeps the Main app focused on back-office operational review while POS, HR, Books and Admin keep their dedicated writable lanes.

## Covered Areas

- Sale review handoffs: recent invoices, receipt/PDF readiness, customer profile lookup and delete-protected cancel route.
- Purchase intake/review: lookup options, recent purchase invoices, receipt/PDF readiness and inward request contract.
- Customer profile handoff: searchable customer rows and profile endpoint with credit note, advance receipt and loyalty context.
- Inventory operations: stock report summary, stock movement history, stock operation options, documents and valuation.
- Inventory write guards: adjustment, transfer, physical count and write-off contracts are checked, but no POST is executed.

## Validation Commands

Run the dry preflight:

```bash
npm run modular:main:writable-readiness
```

Run the wider modular validation without expensive builds:

```bash
npm run modular:validate -- --skip-builds
```

Optional live check when the API is running and a test token is available:

```bash
npm run modular:main:writable-readiness -- --live
```

Use `--require-token` and `--strict-permissions` only when validating with a real Main Back Office user token.

## Mutation Rule

This stage does not create, edit, cancel, transfer, adjust, write off or post any business document. It only validates backend DTO contracts, route availability and safe payload guards.

## Remaining Risks

- Live receipt/PDF/customer-profile handoffs depend on real invoices and customers being available in the selected workspace.
- Purchase inward and inventory operation writes still need dedicated UI and live acceptance before production use.
- Store-day, tailoring, purchase return and document scan writable behavior remain deferred.

## Next Step

Stage 13E.5 should close the Main hardening lane with a checklist covering dashboards, billing review, purchase review, inventory review, customer profile handoff, reports, deployment readiness and remaining writable risks.
