# Stage 8H Package 3 - Nuxt Compile Hotfix

This hotfix repairs the frontend Docker build failure caused by `pages/post-go-live-acceptance/index.vue` importing a missing `~/composables/useApi` module.

The page now uses the existing `useGarmetixApi()` composable so Nuxt/Vite can resolve it during `npm run build`.
