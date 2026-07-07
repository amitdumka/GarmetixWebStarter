import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { getSmokeVersion, modularRoot } from './smoke-routes.mjs'

const args = process.argv.slice(2)
const hasFlag = (name) => args.includes(name)
const option = (name, fallback = '') => {
  const prefix = `${name}=`
  const match = args.find((arg) => arg.startsWith(prefix))
  return match ? match.slice(prefix.length) : fallback
}

const { version, stage } = getSmokeVersion()
const requireManual = hasFlag('--require-manual')
const requireToken = hasFlag('--require-token')
const tokenEnv = option('--token-env', 'GARMETIX_SMOKE_AUTH_TOKEN')
const manualEnv = option('--manual-env', 'GARMETIX_BOOKS_GST_FY_LOCK_MANUAL_ACCEPTANCE')
const token = process.env[tokenEnv]
const manualAccepted = String(process.env[manualEnv] || '').toUpperCase() === 'YES'

const failures = []
const warnings = []

console.log('Garmetix Books Stage 14C.5 closure gate (GST report finalization, financial year lock acceptance)')
console.log(`Version: ${version}`)
console.log(`Stage: ${stage}`)
console.log(`Token env: ${tokenEnv}${token ? ' (set)' : ' (not set)'}`)
console.log(`Manual acceptance env: ${manualEnv}${manualAccepted ? ' (YES)' : ' (not set)'}`)
console.log('Financial year lock create/unlock: guarded write, confirmation phrase required')

if (!version.startsWith('6.0.')) failures.push(`Expected Version6 Books lane version 6.0.x, found ${version}.`)
if (!stage.includes('Stage 14C.5')) failures.push(`Expected Stage 14C.5 lane, found ${stage}.`)

checkRequiredFiles()
checkPackageScripts()
checkGstAndLockWorkflowMarkers()
checkTodoMarkers()
checkEvidence()

finish()

function checkRequiredFiles() {
  const required = [
    'docs/stage-14c5-books-gst-fy-lock-closure.md',
    'scripts/books-accounting-readiness.mjs',
    'scripts/books-accounting-contract-check.mjs',
    'scripts/books-browser-acceptance.mjs',
    'scripts/books-stage14c5-closure.mjs',
    'apps/books/pages/gst-reports.vue',
    'apps/books/pages/gst-returns.vue',
    'apps/books/pages/gst-production.vue',
    'apps/books/pages/financial-year-locks.vue'
  ]

  for (const relativePath of required) {
    if (!existsSync(join(modularRoot, relativePath))) failures.push(`Missing required Books 14C.5 closure artifact: ${relativePath}`)
  }
  console.log(`CHECK Books 14C.5 closure artifacts -> ${required.length} paths`)
}

function checkPackageScripts() {
  const rootPackage = readFileSync(join(modularRoot, '../../package.json'), 'utf8')
  const modularPackage = readFileSync(join(modularRoot, 'package.json'), 'utf8')
  const required = [
    'modular:books:accounting-readiness',
    'modular:books:accounting-contract',
    'modular:books:browser-acceptance',
    'modular:books:stage13d-closure',
    'modular:books:stage14c5-closure',
    'books:stage14c5-closure'
  ]

  for (const marker of required) {
    const source = marker.startsWith('modular:') ? rootPackage : modularPackage
    if (!source.includes(marker)) failures.push(`Missing package script: ${marker}`)
  }
  console.log('CHECK package scripts -> Books 14C.5 closure wired')
}

function checkGstAndLockWorkflowMarkers() {
  const checks = [
    {
      file: 'apps/books/pages/gst-reports.vue',
      markers: ['CSV downloads are available', 'hsn-summary/csv', 'invoice-register/csv', 'tax-summary/csv']
    },
    {
      file: 'apps/books/pages/gst-returns.vue',
      markers: ['stay in controlled flows', 'gst-returns/drafts/${id}/${kind}', '>JSON<', '>Excel<', 'no posting action']
    },
    {
      file: 'apps/books/pages/gst-production.vue',
      markers: ['Admin-only final acceptance remains outside this Books screen', 'schema-review/excel', 'Admin only']
    },
    {
      file: 'apps/books/pages/financial-year-locks.vue',
      markers: ['LOCK FINANCIAL YEAR', 'UNLOCK FINANCIAL YEAR', 'Guarded write']
    }
  ]

  for (const check of checks) {
    const path = join(modularRoot, check.file)
    if (!existsSync(path)) {
      failures.push(`Missing Books GST/FY-lock page: ${check.file}`)
      continue
    }
    const source = readFileSync(path, 'utf8')
    for (const marker of check.markers) {
      if (!source.includes(marker)) failures.push(`${check.file} missing marker: ${marker}`)
    }
  }
  console.log(`CHECK GST/financial-year-lock workflow markers -> ${checks.length} pages`)
}

function checkTodoMarkers() {
  const todo = readFileSync(join(modularRoot, 'docs/MODULAR_TODO.md'), 'utf8')
  if (!todo.includes('14C.5 complete')) failures.push('MODULAR_TODO is missing the 14C.5 complete marker.')
  console.log('CHECK MODULAR_TODO -> 14C.5 complete marker present')
}

function checkEvidence() {
  if (requireToken && !token) failures.push(`Missing ${tokenEnv}; final live-token GST/FY-lock acceptance cannot be required.`)
  if (!token) warnings.push(`${tokenEnv} is not set. Live-token GST export/financial-year-lock acceptance evidence remains pending.`)

  if (requireManual && !manualAccepted) failures.push(`Missing ${manualEnv}=YES; manual GST return filing and financial-year-lock acceptance cannot be required.`)
  if (!manualAccepted) warnings.push(`${manualEnv}=YES is not set. Manual GST filing and lock/unlock evidence remains pending.`)

  if (!token || !manualAccepted) {
    warnings.push('Books GST/FY-lock lane is code-ready, but production filing and financial-year-lock sign-off should wait for live-token and manual evidence.')
  }
  console.log('CHECK Books 14C.5 final evidence -> evaluated')
}

function finish() {
  for (const warning of warnings) console.warn(`WARN ${warning}`)
  if (failures.length > 0) {
    for (const failure of failures) console.error(`FAIL ${failure}`)
    process.exitCode = 1
    return
  }

  const status = token && manualAccepted ? 'GO' : 'CONDITIONAL'
  console.log(`Books Stage 14C.5 closure gate passed. Status: ${status}.`)
}
