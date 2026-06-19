export const APP_VERSION = '4.9.15'
export const APP_STAGE = 'Stage 8I Package 16 Secret Hygiene and Hydration Guard'
export const APP_RELEASE_NAME = 'Secret Hygiene & Hydration Guard'
export const APP_BUILD_DATE = '2026-06-19'
export const APP_BUILD_CODE = 'GARMETIX-8I-20260619-49150'

export const APP_HIGHLIGHTS = [
  'Local deployment env files are excluded from the release archive and blocked by repository ignore rules.',
  'Secret hygiene validation scans the source package for Cloudflare tokens, local env files and obvious private credentials before release.',
  'The Nuxt app now uses a client hydration gate so authenticated pages render after browser session restore, reducing SSR/auth shell mismatch warnings.'
]
