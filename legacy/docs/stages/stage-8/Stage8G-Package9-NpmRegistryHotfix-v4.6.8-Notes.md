# Stage 8G Package 9 - NPM Registry Hotfix 1

This hotfix repairs frontend Docker builds on the Mac mini when `npm ci` tries to download `@iconify-json/lucide` from a non-public/internal registry URL.

## Fixes

- Rewrites `package-lock.json` resolved URL for `@iconify-json/lucide` to the public npm registry.
- Adds frontend `.npmrc` with `https://registry.npmjs.org/`.
- Hardens the frontend Dockerfile with npm retry and registry settings.
- Keeps the runtime Iconify copy from the build layer so the production image does not perform network installs.
