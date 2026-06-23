# Stage 12A.4 Shell Menu And App Links

## Purpose

Stage 12A.4 wires the route ownership registry into the modular shell apps.

No legacy route is moved or deleted in this stage. Each modular app now reads the same route registry and displays:

- app switch links
- owned route count
- API base target
- grouped route ownership list
- migration status badges

## Source Files

- `modular/config/routes.ts`: route ownership and fallback URL helpers.
- `modular/config/apps.ts`: app target definitions and env keys.
- `modular/packages/shared-ui/src/index.ts`: shared shell model and route grouping helpers.
- `modular/apps/*/app.vue`: shell screens consuming the shared model.

## Link Safety

App switch links are driven by environment values exposed in each app's Nuxt runtime config.

When a target URL is configured, the shell can link to that app. When it is missing, the route helper falls back to the legacy route path so a missing env value does not create a broken production-domain link.

## Current Limitation

The modular apps still do not contain real business pages. The route list is intentionally a migration map. Real page extraction begins after shared auth/API helpers are stable, with POS first.

## Next Step

Stage 12A.5 should add a shared layout contract and smoke/status route pattern so the later POS/HR/Books/AI/Admin pages have the same top bar, app links, auth placeholder, and API status behavior.

