import { readFileSync } from 'node:fs'
import { createRequire } from 'node:module'

const require = createRequire(import.meta.url)

let cachedCollection: any | null = null

function loadLucideCollection() {
  if (cachedCollection) {
    return cachedCollection
  }

  const iconsPath = require.resolve('@iconify-json/lucide/icons.json')
  cachedCollection = JSON.parse(readFileSync(iconsPath, 'utf8'))
  return cachedCollection
}

export default defineEventHandler((event) => {
  setHeader(event, 'cache-control', 'public, max-age=31536000, immutable')
  // Return the full local Lucide collection instead of trying to filter the
  // query string. Nuxt UI internally requests icons such as lucide:x,
  // lucide:chevron-down and lucide:database; returning the full bundled
  // collection prevents false "failed to load icon" warnings when request
  // formats change between Nuxt Icon/Nuxt UI versions.
  return loadLucideCollection()
})
