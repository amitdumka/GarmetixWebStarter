# Stage 13G.22 - Books Transactional Surface Polish

Version: 5.13.62

## Scope

- Applied the Option A hybrid Garmetix dashboard surface to the remaining active Books pages.
- Covered vouchers, petty cash, bank operations, accounting masters, parties, vendor payments, vendor settlements, GST returns, GST production readiness, financial year locks, and Books login.
- Kept all existing Books API calls, filters, download actions, and detail-loading behavior unchanged.

## Behavior

- This stage is UI-only.
- Register pages now use shared table/detail/section surfaces instead of raw bordered panels.
- Metrics now use the shared metric card classes so Books pages match the Back Office, HR and AI Sense layout language.

## Validation

- Run `npm run check` from `modular`.
- Build Books with `npm run build:books`.
- Deploy SRP with `npm run deploy:srp`.
- Verify live with `NODE_OPTIONS=--use-system-ca npm run deploy:srp:acceptance -- --live --strict`.

## Remaining Work

- Continue polishing any deep Back Office legacy pages that still use older surfaces.
- Add browser visual acceptance checks for authenticated Books pages once stable test credentials and seeded demo data are available.
