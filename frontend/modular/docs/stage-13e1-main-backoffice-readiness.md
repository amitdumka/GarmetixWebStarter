# Stage 13E.1 Main Back Office Readiness

Version: 5.13.31
Branch: Version5

## Purpose

Stage 13E starts the Main Back Office operations parity lane after POS, HR and Books hardening. This slice adds repeatable non-mutating readiness checks for the routes and API read models that the modular Main app currently owns.

## Scope

- Dashboard read models: business, today's dashboard and store manager dashboard.
- Sales review: recent sale invoices and billing options.
- Purchase review: recent purchase invoices and purchase lookup options.
- Inventory review: stock summary and product lookup.
- Customer review: customer search preview.
- Workspace scope: company/store option visibility for back-office context.

## Route Ownership

Main Back Office keeps operational review and management routes such as dashboard, sale invoice list, purchase list, inventory, stock operations, customers, reports, document scan, tailoring and store-day overview.

Fast POS entry, HR/payroll, Books/accounting and Admin/SaaS controls remain owned by their dedicated modular apps.

## Safety Rules

- The readiness script performs only `GET` checks.
- Sale, purchase, inventory and store-day mutations are intentionally not executed.
- Live mode requires an external token through `GARMETIX_SMOKE_AUTH_TOKEN`.
- Missing permissions can be warnings by default, or failures with `--strict-permissions`.

## Validation

```powershell
npm.cmd run modular:main:backoffice-readiness
npm.cmd run modular:validate -- --skip-builds
```

Optional live check:

```powershell
Set a valid token in `GARMETIX_SMOKE_AUTH_TOKEN`, then run:
npm.cmd run modular:main:backoffice-readiness -- --live --require-token
```

## Remaining Risks

- Writable Main Back Office workflows still need separate contract checks before mutation is enabled.
- Store-day, tailoring, purchase return and document scan pages remain placeholder/review routes until their endpoint contracts are promoted.
- Live evidence still needs a back-office-capable user token.

## Next Step

Stage 13E.2 should add contract parity checks for Main Back Office sale review, purchase review, inventory summary and customer preview DTO fields.
