# Stage 8G Package 1 - Web Icon Runtime Hotfix 2

This hotfix addresses a production-only Nuxt/Iconify runtime issue observed on the Mac mini deployment.

## Problem

The application and API health checks passed, but dashboard pages triggered web log errors for:

- `/api/_nuxt_icon/lucide.json`
- `ERR_IMPORT_ATTRIBUTE_MISSING`
- `@iconify-json/lucide/icons.json needs an import attribute of type: json`

## Fix

- Added `@iconify-json/lucide` as a normal frontend dependency so Docker `npm ci` installs it during build.
- Added a custom Nuxt Nitro route at `server/api/_nuxt_icon/lucide.json.get.ts` that reads the Lucide collection via `fs` + `createRequire` instead of dynamic JSON import.
- Updated the frontend Dockerfile to copy `node_modules/@iconify-json` from the build layer into the production image, avoiding a network `npm install` in the final image.

## Expected result

- `/api/health` remains healthy.
- `/api/_nuxt_icon/lucide.json?...` returns local icon JSON instead of throwing a 500.
- Dashboard pages should not log Nuxt Icon JSON import errors.
