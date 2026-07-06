import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import { getSmokeVersion, modularRoot } from './smoke-routes.mjs'

const { version, stage } = getSmokeVersion()
const repoRoot = join(modularRoot, '../..')
const failures = []

console.log('Garmetix AI Sense API path normalization readiness')
console.log(`Version: ${version}`)
console.log(`Stage: ${stage}`)
console.log('Mutation check: disabled')

if (version !== '6.0.50') failures.push(`Expected modular version 6.0.50, found ${version}.`)
if (!stage.includes('Stage 14F.6B')) failures.push(`Expected Stage 14F.6B, found ${stage}.`)

checkUrl('/api', 'api/dashboard/business', '/api/dashboard/business')
checkUrl('/api', '/api/dashboard/business', '/api/dashboard/business')
checkUrl('/api/', 'api/inventory/stock-reports/summary', '/api/inventory/stock-reports/summary')
checkUrl('https://srp.aadwikafashion.in/api', 'api/dashboard/business', 'https://srp.aadwikafashion.in/api/dashboard/business')
checkUrl('https://srp.aadwikafashion.in', 'api/dashboard/business', 'https://srp.aadwikafashion.in/api/dashboard/business')

checkFile('frontend/modular/packages/shared-api/src/index.ts', [
  'nextPath = nextPath.replace(/^api\\/+',
  "endsWith('/api')"
])

checkFile('frontend/modular/apps/ai-sense/pages/index.vue', [
  "get<ApiRecord>('api/dashboard/business')",
  "get<ApiRecord>('api/inventory/stock-reports/summary'"
])

checkFile('frontend/modular/apps/ai-sense/utils/ai-api.ts', [
  'normalizeAiApiPath',
  'replace(/^api\\/+',
  'normalizeAiApiPath(path)'
])

checkFile('frontend/modular/docs/stage-14f5-ai-sense-api-path-hotfix.md', [
  'Stage 14F.5',
  '6.0.47',
  '/api/api/dashboard/business',
  '/api/api/inventory/stock-reports/summary'
])

checkFile('frontend/modular/docs/stage-14f6-ai-sense-runtime-path-guard.md', [
  'Stage 14F.6',
  '6.0.50',
  'stale copied',
  'workspace-link'
])

checkFile('frontend/modular/docs/MODULAR_TODO.md', [
  '14F.6B complete',
  '6.0.50',
  'AI Sense API path'
])

checkPackageScripts()

if (failures.length > 0) {
  console.error('\nAI Sense API path normalization readiness failed:')
  for (const failure of failures) console.error(`- ${failure}`)
  process.exit(1)
}

console.log('\nAI Sense API path normalization readiness passed.')

function checkUrl(baseUrl, path, expected) {
  const actual = createApiUrl(baseUrl, path)
  if (actual !== expected) {
    failures.push(`Expected createApiUrl(${baseUrl}, ${path}) to be ${expected}, found ${actual}.`)
  }
  console.log(`CHECK ${baseUrl} + ${path} -> ${actual}`)
}

function createApiUrl(baseUrl, path) {
  const base = String(baseUrl || '').trim().replace(/\/+$/, '')
  let nextPath = String(path || '').replace(/^\/+/, '')
  if (base.replace(/\/+$/, '').toLowerCase().endsWith('/api')) {
    nextPath = nextPath.replace(/^api\/+/i, '')
  }
  return nextPath ? `${base}/${nextPath}` : base
}

function checkFile(relativePath, markers) {
  const source = readFileSync(join(repoRoot, relativePath), 'utf8')
  for (const marker of markers) {
    if (!source.includes(marker)) failures.push(`${relativePath} missing marker: ${marker}`)
  }
  console.log(`CHECK ${relativePath} -> ${markers.length} marker(s)`)
}

function checkPackageScripts() {
  const rootPackage = readFileSync(join(repoRoot, 'package.json'), 'utf8')
  const modularPackage = readFileSync(join(modularRoot, 'package.json'), 'utf8')
  const script = 'ai-sense-api-path-readiness.mjs'
  if (!rootPackage.includes('modular:ai-sense:api-path')) failures.push('Root package missing modular:ai-sense:api-path script.')
  if (!modularPackage.includes('ai-sense:api-path')) failures.push('Modular package missing ai-sense:api-path script.')
  if (!rootPackage.includes(script) || !modularPackage.includes(script)) failures.push(`Package scripts missing ${script}.`)
  console.log('CHECK package scripts -> AI Sense API path readiness wired')
}
