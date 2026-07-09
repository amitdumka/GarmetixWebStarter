export const garmetixModularVersion = {
  version: '6.0.70',
  stage: 'Stage 14P.1 Inventory App Switcher Fix',
  label: 'Version6 Stage 14P.1 Inventory App Switcher Fix',
  summary: 'Fixed the module switcher dropdown (top-left) not offering Inventory, and Inventory\'s own switcher showing every other app as disabled. Root cause: each modular app declares its own hardcoded runtimeConfig.public.appUrls object in nuxt.config.ts, and Nuxt only exposes env vars for keys already present in that object - the deploy script was already passing NUXT_PUBLIC_GARMETIX_INVENTORY_URL to every build, but none of the 7 existing apps\' appUrls objects declared that key, so it was silently dropped and the switcher link rendered disabled (configured: false). Inventory\'s own nuxt.config.ts never declared an appUrls object at all, so from inside Inventory every other app link was disabled too. Added the missing key to all 7 existing apps and added a full appUrls block to Inventory\'s own config, matching the established per-app pattern.'
} as const
