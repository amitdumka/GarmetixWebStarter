# Stage 12B.1 POS Foundation

Stage 12B.1 starts the POS frontend extraction under `modular/apps/pos`.

## What Changed

- POS `app.vue` now hosts real Nuxt routes through `<NuxtPage />`.
- POS shell now includes:
  - top app switcher
  - logout action
  - API/auth/stage status cards
  - compact POS route menu
- POS routes created:
  - `/`
  - `/login`
  - `/day-open`
  - `/sale`
  - `/hold-bills`
  - `/returns`
  - `/print`
  - `/day-close`
- Modular auth now uses the same local storage keys as the legacy app:
  - `garmetix.token`
  - `garmetix.user`
  - `garmetix.expiresAtUtc`
- Shared API now includes a login helper for `/api/auth/login`.

## Current Scope

These pages are route-ready shells. They do not yet replace the legacy sale invoice screen. The legacy app remains the source of truth until POS billing parity is completed.

## Next Step

Stage 12B.2 should begin extracting the sale invoice flow into `/sale`, starting with lookup data and invoice draft state, then saving and printing.
