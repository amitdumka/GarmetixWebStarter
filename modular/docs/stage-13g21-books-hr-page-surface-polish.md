# Stage 13G.21 - Books and HR Page Surface Polish

Version: 5.13.61

## Scope

- Extended the Option A hybrid dashboard surface to Books GST reports, audit events, message logs, and shared Books placeholders.
- Extended the same surface to HR attendance landing, today attendance, monthly attendance, regularization, devices, payroll summary, payroll review, salary draft, and salary payment pages.
- Hardened the shared Books master table against missing rows or columns so empty API responses do not crash the page.

## Behavior

- No backend endpoints, database models, auth rules, or business calculations were changed.
- The pass is UI-only and keeps existing API paths and data-shaping helpers.
- The pages now share the same hero, metric card, table panel, detail panel, and row-card classes used by the current Garmetix modular shell.

## Validation

- Run `npm run check` from `modular`.
- Build the affected apps with `npm run build:books` and `npm run build:hr`.
- Deploy with `npm run deploy:srp`.
- Verify the public SRP deployment with `NODE_OPTIONS=--use-system-ca npm run deploy:srp:acceptance -- --live --strict`.

## Remaining UI Surface Work

- Books transactional pages still need the same treatment: vouchers, petty cash, vendor payments, vendor settlements, parties, accounting, GST returns, GST production, and financial year locks.
- Main Back Office legacy-deep pages should continue to be migrated gradually after the module-specific surfaces are consistent.
