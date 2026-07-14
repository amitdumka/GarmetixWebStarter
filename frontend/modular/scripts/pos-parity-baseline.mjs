import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { getSmokeVersion, modularRoot } from './smoke-routes.mjs'

const { version, stage } = getSmokeVersion()
const repoRoot = join(modularRoot, '..', '..')
const failures = []

const requiredFiles = [
  'apps/pos/pages/index.vue',
  'apps/pos/pages/login.vue',
  'apps/pos/pages/day-open.vue',
  'apps/pos/pages/sale.vue',
  'apps/pos/pages/history.vue',
  'apps/pos/pages/hold-bills.vue',
  'apps/pos/pages/returns.vue',
  'apps/pos/pages/exchange.vue',
  'apps/pos/pages/print.vue',
  'apps/pos/pages/day-close.vue',
  'apps/pos/utils/local-pos-storage.ts',
  'apps/pos/utils/pos-documents.ts',
  'apps/pos/utils/return-contract.ts',
  'apps/pos/utils/sale-contract.ts',
  'docs/stage-14a1-pos-parity-baseline.md'
]

const requiredScripts = [
  'pos-sale-contract-check.mjs',
  'pos-operator-acceptance.mjs',
  'pos-held-bill-smoke.mjs',
  'pos-held-bill-browser-acceptance.mjs',
  'pos-save-after-resume-readiness.mjs',
  'pos-live-save-fixture-readiness.mjs',
  'pos-stage13b-closure.mjs'
]

const requiredRouteMarkers = [
  "id: 'pos-day-open', path: '/day-open'",
  "id: 'billing-new', path: '/sale'",
  "id: 'pos-history', path: '/history'",
  "id: 'pos-held-bills', path: '/hold-bills'",
  "id: 'sales-return', path: '/returns'",
  "id: 'sales-exchange', path: '/exchange'",
  "id: 'pos-print', path: '/print'",
  "id: 'pos-day-close', path: '/day-close'"
]

console.log('Garmetix POS Version6 parity baseline')
console.log(`Version: ${version}`)
console.log(`Stage: ${stage}`)

if (!version.startsWith('6.')) failures.push(`Expected Version6 modular version, found ${version}.`)
if (!stage.includes('Stage 14')) failures.push(`Expected Stage 14 lane, found ${stage}.`)

for (const file of requiredFiles) {
  if (!existsSync(join(modularRoot, file))) failures.push(`Missing POS baseline file: ${file}`)
  else console.log(`PASS file ${file}`)
}

for (const script of requiredScripts) {
  if (!existsSync(join(modularRoot, 'scripts', script))) failures.push(`Missing POS safety script: ${script}`)
  else console.log(`PASS script ${script}`)
}

const routes = readFileSync(join(modularRoot, 'config/routes.ts'), 'utf8')
for (const marker of requiredRouteMarkers) {
  if (!routes.includes(marker)) failures.push(`POS route registry marker missing: ${marker}`)
  else console.log(`PASS route ${marker}`)
}

const todo = readFileSync(join(modularRoot, 'docs/MODULAR_TODO.md'), 'utf8')
for (const marker of ['## Stage 14A', '14A.1 complete', '14B.1 complete']) {
  if (!todo.includes(marker)) failures.push(`MODULAR_TODO missing marker: ${marker}`)
}

const rootPackage = JSON.parse(readFileSync(join(repoRoot, 'package.json'), 'utf8'))
const modularPackage = JSON.parse(readFileSync(join(modularRoot, 'package.json'), 'utf8'))
const posPackage = JSON.parse(readFileSync(join(modularRoot, 'apps/pos/package.json'), 'utf8'))

if (!rootPackage.version.startsWith('6.')) failures.push(`Root package version should be Version6, found ${rootPackage.version}.`)
if (!modularPackage.version.startsWith('6.')) failures.push(`Modular package version should be Version6, found ${modularPackage.version}.`)
if (!posPackage.version.startsWith('6.')) failures.push(`POS package version should be Version6, found ${posPackage.version}.`)
if (!rootPackage.scripts?.['modular:pos:parity-baseline']) failures.push('Root package is missing modular:pos:parity-baseline script.')
if (!modularPackage.scripts?.['pos:parity-baseline']) failures.push('Modular package is missing pos:parity-baseline script.')

if (failures.length > 0) {
  console.error('\nPOS Version6 parity baseline failed:')
  for (const failure of failures) console.error(`- ${failure}`)
  process.exit(1)
}

console.log('\nPOS Version6 parity baseline passed.')
