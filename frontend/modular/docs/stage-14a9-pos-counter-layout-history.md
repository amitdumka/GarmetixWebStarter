# Stage 14A.9 - POS Counter Layout And History

Version: 6.0.25

## Summary

This stage ports the newer fullscreen POS counter layout into the Version6 modular POS app and adds a POS-owned sales history page for recent invoice lookup, reprint and Digital Bill CRM visibility.

## Added

- POS app now supports Nuxt layouts:
  - default layout keeps `ModularAppShell`
  - fullscreen layout gives `/sale` the counter-first screen
- `/sale` now uses the fullscreen counter layout while keeping:
  - barcode/product datalist normalization
  - shared API/auth client usage
  - customer adjustments
  - split payment bank validation
  - print queue handoff
- `/history` lists recent sales from `billing/sales/recent?take=100`.
- `/history` can view receipt items, reprint invoices and show Digital Bill CRM link/status/counters.
- POS route registry, sidebar and smoke route list now include `/history`.
- `.codex/Priority6_POS_CRM_TODO.md` records the CRM/digital CRM port plan and backup locations.

## Validation

- POS build should be run with `npm --prefix frontend/modular --workspace @garmetix/pos-web run build`.
- POS parity should be run with `npm run modular:pos:parity-baseline`.
- POS cashier workflow should be run with `npm run modular:pos:cashier-workflow`.
- Route smoke should be run with `npm run modular:smoke:routes`.

## Next

- Review `/pos/sale` live on a 14-inch laptop viewport and adjust density if needed.
- Confirm live Digital Bill CRM fields on `/pos/history`.
- Start CRM module parity from customer register, loyalty and customer dues, then Digital Bill CRM.
