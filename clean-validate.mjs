import fs from 'fs'

const path = 'frontend/modular/scripts/validate-structure.mjs'
let content = fs.readFileSync(path, 'utf8')

const lines = content.split('\n')
const filteredLines = lines.filter(line => {
  if (line.includes("'apps/pos/")) return false
  if (line.includes("'apps/hr/")) return false
  if (line.includes("'apps/books/")) return false
  if (line.includes("'NUXT_PUBLIC_GARMETIX_POS_URL'")) return false
  if (line.includes("'NUXT_PUBLIC_GARMETIX_HR_URL'")) return false
  if (line.includes("'NUXT_PUBLIC_GARMETIX_BOOKS_URL'")) return false
  if (line.includes("'NUXT_PUBLIC_GARMETIX_POS_BASE_PATH'")) return false
  if (line.includes("'NUXT_PUBLIC_GARMETIX_HR_BASE_PATH'")) return false
  if (line.includes("'NUXT_PUBLIC_GARMETIX_BOOKS_BASE_PATH'")) return false
  return true
})

fs.writeFileSync(path, filteredLines.join('\n'), 'utf8')
console.log('Cleaned validate-structure.mjs')
