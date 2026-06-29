# Stage 13E.2 Main Back Office Contract Checks

Version: 5.13.32
Branch: Version5

## Purpose

Stage 13E.2 adds static contract parity checks for the modular Main Back Office app. The checker compares backend DTO records with expected frontend field usage so Main read-only pages do not silently drift away from the ASP.NET API.

## Covered Contracts

- Dashboard: business dashboard, today's dashboard, store manager dashboard, metrics, activities, trends, quick actions and health signals.
- Sales review: recent sale invoice rows, customer preview rows and billing options.
- Purchase review: recent purchase invoice rows and purchase lookup options.
- Inventory review: stock report summary, stock risk rows and stock operation review fields.
- Shared lookups: product lookup and workspace scope options.

## Safety Rules

- The contract check is static and does not call the API.
- No sale, purchase, stock or customer mutation is executed.
- POS fast sale entry, HR/payroll, Books/accounting and Admin/SaaS contracts stay owned by their dedicated module checks.

## Validation

```powershell
npm.cmd run modular:main:backoffice-contract
npm.cmd run modular:validate -- --skip-builds
```

## Remaining Risks

- Writable Main workflows still need payload construction checks before mutation is enabled.
- Some Main routes remain placeholder review pages until endpoint contracts are promoted.
- Live evidence still depends on a back-office-capable test token.

## Next Step

Stage 13E.3 should add Main browser acceptance checks for 14 inch laptop usability, read-only table scrolling, safe error messages and app boundary clarity.
