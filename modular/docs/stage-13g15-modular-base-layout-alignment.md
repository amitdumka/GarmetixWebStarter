# Stage 13G.15 - Modular Base Layout Alignment

Version: 5.13.55

## Goal

Repair the modular frontend base layout so Back Office, POS, HR, AI Sense, Books and Admin use one consistent Garmetix dashboard shell instead of separate lightweight shells.

## Legacy Reference

The layout, branding and theme were aligned with the legacy Nuxt app:

- `legacy/frontend/garmetix-web/components/AppShell.vue`
- `legacy/frontend/garmetix-web/app.config.ts`
- `legacy/frontend/garmetix-web/assets/css/main.css`
- `legacy/frontend/garmetix-web/public/*`

## Changes

- Added a shared modular shell component in `packages/shared-ui/components/ModularAppShell.vue`.
- Added shared dashboard shell CSS in `packages/shared-ui/assets/modular-shell.css`.
- Replaced each modular app root `app.vue` with the shared shell wrapper.
- Set dark mode as the default in every modular Nuxt config.
- Added the legacy Garmetix favicon, icon, manifest and logo assets to each modular app.
- Added the same Nuxt UI teal/slate color config used by the legacy app.
- Changed app-switch links to external navigation for other apps so POS, HR, Books, AI Sense and Admin do not get swallowed by the current app base path.

## Validation

Run from `modular/`:

```bash
npm run check
npm run build:main
npm run build:pos
npm run build:hr
npm run build:ai-sense
npm run build:books
npm run build:admin
```

## Remaining Risks

- The SRP deployment is still path-based (`/pos/`, `/hr/`, `/books/`, `/admin/`) until Cloudflare hostnames are wired for each modular app.
- Some module pages are still shell or readiness pages and need the full legacy feature migration in later stages.
- Browser acceptance should include visual checks after deploy, because this stage changes the page frame for every modular app.
