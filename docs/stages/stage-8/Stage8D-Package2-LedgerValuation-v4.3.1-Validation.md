# Stage 8D Package 2 Validation

Version: 4.3.1
Date: 2026-06-15

## Automated

- [x] Backend Debug build passes with zero warnings and zero errors.
- [x] Backend Release build passes with zero warnings and zero errors.
- [x] All 33 backend tests pass.
- [x] Weighted-average tests cover incoming valuation, outgoing consumption, subsequent purchases, negative-stock prevention, invalid dual-direction movements, and replay ordering.
- [x] Nuxt production build passes.
- [x] EF valuation and legacy-projection migrations apply to Docker PostgreSQL.
- [x] All seven regular nonzero stock rows have authoritative movement history after backfill.
- [x] Git whitespace validation passes.

## Runtime

- [x] Docker services report healthy on v4.3.1.
- [x] App Info reports build code `GARMETIX-8D-20260615-4310`.
- [x] Stock Operations shows valuation and reconciliation on desktop and mobile.
- [x] Stock Operations renders without horizontal page overflow at 1280 px and 390 px widths.

## Residual

- FIFO evaluation remains deferred until the weighted-average baseline is stable.
- The existing application-wide Vue hydration-mismatch warning remains visible in browser diagnostics and is not introduced by this package.
