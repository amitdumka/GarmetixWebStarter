# Stage 8C Package 2 Validation

Version: 4.2.1  
Date: 2026-06-15

## Automated

- [x] Backend Release build succeeds with zero warnings and errors.
- [x] Nuxt production build succeeds; known external font-certificate and sourcemap warnings remain tracked.
- [x] All 13 API tests pass, including purchase-return PDF and QR identity coverage.
- [x] EF migration list includes `AddPurchaseReturnPrintAudit`.

## Docker And Runtime

- [x] Docker services rebuild and start on v4.2.1.
- [x] Print audit columns exist in PostgreSQL and migration history records `20260615043000_AddPurchaseReturnPrintAudit`.
- [x] App Info reports Stage 8C v4.2.1 and build code `GARMETIX-8C-20260615-4210`.
- [x] Purchase Return page loads without visible page errors.
- [x] Purchase Return register and print-state filter render correctly at 1280 px and 390 px with no horizontal document overflow.
- [x] Focused tests validate colored multi-page A4/A5 PDF output containing all return lines.
- [x] Focused tests validate the stable purchase-return QR token; the scanner resolver maps that token to `/purchase-return?returnId=<id>`.

## Existing Runtime Note

- The Nuxt shell continues to emit its pre-existing generic hydration-mismatch console message on authenticated reload. The v4.2.1 page has no visible failure, and this package does not add a new hydration path.
