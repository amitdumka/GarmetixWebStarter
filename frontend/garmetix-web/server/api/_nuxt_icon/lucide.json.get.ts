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
  const collection = loadLucideCollection()
  const query = getQuery(event)
  const requestedIcons = String(query.icons || '')
    .split(',')
    .map((item) => item.trim())
    .filter(Boolean)

  if (!requestedIcons.length) {
    return collection
  }

  const icons: Record<string, unknown> = {}
  for (const iconName of requestedIcons) {
    if (collection.icons?.[iconName]) {
      icons[iconName] = collection.icons[iconName]
    }
  }

  return {
    ...collection,
    icons
  }
})
