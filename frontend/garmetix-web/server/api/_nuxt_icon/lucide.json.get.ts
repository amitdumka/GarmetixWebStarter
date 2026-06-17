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

function normalizeIconName(value: string) {
  const trimmed = value.trim()
  const withoutPrefix = trimmed.includes(':') ? trimmed.split(':').pop() || trimmed : trimmed
  return withoutPrefix
    .replace(/^i-lucide[-:]/, '')
    .replace(/^lucide[-:]/, '')
    .replace(/_/g, '-')
}

function addIcon(collection: any, icons: Record<string, unknown>, requestedName: string) {
  const normalized = normalizeIconName(requestedName)
  const candidates = [normalized, normalized.replace(/^circle-/, ''), normalized.replace(/-circle$/, '')]
  for (const candidate of candidates) {
    if (collection.icons?.[candidate]) {
      icons[candidate] = collection.icons[candidate]
      return
    }
    if (collection.aliases?.[candidate]) {
      icons[candidate] = collection.aliases[candidate]
      return
    }
  }

  // Last-resort fallback prevents Nuxt Icon from throwing visible warnings for
  // rarely requested aliases such as lucide:info while keeping the UI usable.
  const fallback = collection.icons?.info || collection.icons?.['circle-help'] || collection.icons?.['circle-alert']
  if (fallback) {
    icons[normalized] = fallback
  }
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
    addIcon(collection, icons, iconName)
  }

  return {
    ...collection,
    icons
  }
})
