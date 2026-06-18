# Stage 5C — Final Release Stabilization Notes

Package version: v1.6

## Scope

Stage 5C prepares the project for controlled live rollout. It does not change core sale/purchase math. It adds release-support tooling so deployment, training, smoke testing, and first-day operation are easier to repeat.

## Backend additions

New namespace:

- `Garmetix.Api.Release`

New endpoints:

- `GET /api/release-stabilization/smoke-checks`
- `POST /api/release-stabilization/demo-data/seed`

Smoke checks cover:

- database connection
- admin user availability
- company/store setup
- tax/product/category/customer/vendor/salesman master readiness
- stock row availability
- negative stock blocker
- product HSN coverage warning
- backup path visibility warning

Demo seed is idempotent. It creates only missing demo master data:

- demo company
- demo store group
- demo store
- demo product category/subcategory
- GST 5/12/18 tax rows
- demo vendor
- demo customer
- demo salesman
- demo brand
- three demo products with opening stock and stock movements

Training invoices are intentionally not auto-created in v1.6. Operators should create invoices/purchases from the UI so normal stock, GST, payment, and accounting paths are exercised.

## Frontend additions

New admin page:

- `/release-stabilization`

Sidebar:

- Admin → Release Stabilization

The page provides:

- smoke check summary
- check-level messages/fix hints
- demo seed button
- links to operator manual and go-live checklist
- CLI smoke-test command examples

## Scripts

Added:

- `scripts/linux/smoke-test.sh`
- `scripts/windows/smoke-test.ps1`
- `scripts/validation/stage5c-static-checks.py`

## Documentation

Added:

- `../../guides/Operator-User-Manual.md`
- `../../operations/release/GoLive-Smoke-Test-Checklist.md`
- public Nuxt copies under `frontend/garmetix-web/public/docs/`

## Recommended live rollout order

1. Extract package.
2. Run backend build.
3. Run frontend build.
4. Start production stack.
5. Create/verify admin user.
6. Open Production Readiness and resolve critical blockers.
7. Open Release Stabilization and run smoke checks.
8. Take backup and verify checksum.
9. Seed demo data only on training/test database, or skip on live database.
10. Run one manual sale, purchase, return, GST report, data consistency scan.
11. Take final pre-go-live backup.
