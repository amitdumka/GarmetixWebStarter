# Stage 13E.3 Main Browser Acceptance

Version: 5.13.33
Branch: Version5

## Purpose

Stage 13E.3 adds repeatable Main Back Office browser acceptance checks. The default mode is dry-run so it is safe on development systems, while live mode can later verify a running Main app with Playwright.

## Covered Routes

- Dashboard
- Today's Dashboard
- Store Manager Dashboard
- Sale Invoices
- Purchase
- Inventory
- Stock Operations
- Customers
- Reports
- Store Day
- Tailoring
- Document Scan

## Acceptance Rules

- Each route heading must render at a 1366x768 viewport.
- The page must not create document-level horizontal overflow on a 14 inch laptop.
- Main tables and dashboard panels must stay inside the Main shell instead of overlapping the sidebar/content layout.
- Visible messages must not expose raw localhost, server or API URLs.
- POS, HR, Books and Admin workflow headings must not appear inside Main-owned routes.

## Validation

```powershell
npm.cmd run modular:main:browser-acceptance
npm.cmd run modular:validate -- --skip-builds
```

Optional live check:

```powershell
Set a valid token in `GARMETIX_SMOKE_AUTH_TOKEN`, start the Main app, then run:
npm.cmd run modular:main:browser-acceptance -- --live
```

## Remaining Risks

- Live evidence still needs the Main app running and a valid token when authenticated API rows are expected.
- Writable Main workflows remain deferred until payload safety checks are added.
- Placeholder routes still need future endpoint-specific acceptance once their contracts are promoted.

## Next Step

Stage 13E.4 should add writable-readiness preflight for Main sale-review actions, purchase intake/review, customer profile handoff and inventory operations without performing mutations by default.
