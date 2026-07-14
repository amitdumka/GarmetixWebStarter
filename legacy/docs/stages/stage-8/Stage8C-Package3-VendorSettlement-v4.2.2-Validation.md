# Stage 8C Package 3 Validation

Version: 4.2.2
Date: 2026-06-15

## Automated

- [x] Backend Release build passes with zero warnings and zero errors.
- [x] Nuxt production build passes.
- [x] All 16 backend tests pass.
- [x] Settlement tests cover mixed settlement, over-allocation, and invoice-outstanding validation.
- [x] EF discovers `20260615061000_AddVendorSettlements`.
- [x] Git whitespace validation passes.

## Runtime

- [x] Docker images rebuild and services start on v4.2.2.
- [x] PostgreSQL records the vendor-settlement migration.
- [x] Vendor settlement tables and new return/payment columns exist.
- [x] App Info reports v4.2.2 and build code `GARMETIX-8C-20260615-4220`.
- [x] Vendor Settlements renders correctly at 1280 px and 390 px without horizontal overflow.
- [x] The empty open-return register and settlement history load without a visible page error.
- [ ] A populated live register was not exercised because the current database contains zero purchase returns; calculation and validation paths are covered by automated tests.

## Known Build Warnings

- Nuxt may warn when external font metadata providers cannot be reached through the local certificate chain.
- Tailwind and VueUse dependencies emit existing sourcemap and pure-annotation warnings.
- The existing persisted authentication/dashboard-shell hydration path emits a generic Nuxt hydration warning on authenticated reloads. The clock is now client-initialized, the warning is non-blocking, and no settlement-page mismatch or overflow is visible.
