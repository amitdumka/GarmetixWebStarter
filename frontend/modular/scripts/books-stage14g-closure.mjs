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
const manualEnv = option('--manual-env', 'GARMETIX_BOOKS_GST_FULL_PARITY_MANUAL_ACCEPTANCE')
const token = process.env[tokenEnv]
const manualAccepted = String(process.env[manualEnv] || '').toUpperCase() === 'YES'

const failures = []
const warnings = []

console.log('Garmetix Books Stage 14G closure gate (full legacy GST menu parity)')
console.log(`Version: ${version}`)
console.log(`Stage: ${stage}`)
console.log(`Token env: ${tokenEnv}${token ? ' (set)' : ' (not set)'}`)
console.log(`Manual acceptance env: ${manualEnv}${manualAccepted ? ' (YES)' : ' (not set)'}`)

if (!version.startsWith('6.0.')) failures.push(`Expected Version6 Books lane version 6.0.x, found ${version}.`)

checkRequiredFiles()
checkPackageScripts()
checkGstMenuWorkflowMarkers()
checkTodoMarkers()
checkEvidence()

finish()

function checkRequiredFiles() {
  const required = [
    'docs/stage-14g-books-gst-full-parity.md',
    'scripts/books-accounting-contract-check.mjs',
    'scripts/books-browser-acceptance.mjs',
    'scripts/admin-browser-acceptance.mjs',
    'scripts/books-stage14g-closure.mjs',
    'apps/books/composables/useGstReviewContact.ts',
    'apps/books/pages/gst-returns.vue',
    'apps/books/pages/gst-reports.vue',
    'apps/books/pages/gst-production.vue',
    'apps/books/pages/accounting-gst-validation.vue',
    'apps/admin/pages/gst-final-acceptance.vue'
  ]

  for (const relativePath of required) {
    if (!existsSync(join(modularRoot, relativePath))) failures.push(`Missing required Books Stage 14G closure artifact: ${relativePath}`)
  }
  console.log(`CHECK Books Stage 14G closure artifacts -> ${required.length} paths`)
}

function checkPackageScripts() {
  const rootPackage = readFileSync(join(modularRoot, '../../package.json'), 'utf8')
  const modularPackage = readFileSync(join(modularRoot, 'package.json'), 'utf8')
  const required = [
    'modular:books:accounting-contract',
    'modular:books:browser-acceptance',
    'modular:admin:browser-acceptance',
    'modular:books:stage14g-closure',
    'books:stage14g-closure'
  ]

  for (const marker of required) {
    const source = marker.startsWith('modular:') ? rootPackage : modularPackage
    if (!source.includes(marker)) failures.push(`Missing package script: ${marker}`)
  }
  console.log('CHECK package scripts -> Books Stage 14G closure wired')
}

function checkGstMenuWorkflowMarkers() {
  const checks = [
    {
      file: 'apps/books/pages/gst-returns.vue',
      markers: ['B2B Invoices', 'GSTR-3B 3.1 Supplies', 'Mark Filed', 'Audit Trail', 'POST GST ACCOUNTING', 'Review &amp; Send to CA']
    },
    {
      file: 'apps/books/pages/gst-reports.vue',
      markers: ['Send to CA', 'gst-returns/reports/send-review']
    },
    {
      file: 'apps/books/pages/accounting-gst-validation.vue',
      markers: ['Closeout Checklist', 'Known Limitations', 'Next Module Candidates', 'post-import-validation/accounting-gst']
    },
    {
      file: 'apps/admin/pages/gst-final-acceptance.vue',
      markers: ['Acceptance Checklist', 'Ready for CA/Filing', 'data-consistency/summary', 'email-diagnostics/status', 'backups/maintenance/status']
    }
  ]

  for (const check of checks) {
    const path = join(modularRoot, check.file)
    if (!existsSync(path)) {
      failures.push(`Missing GST menu page: ${check.file}`)
      continue
    }
    const source = readFileSync(path, 'utf8')
    for (const marker of check.markers) {
      if (!source.includes(marker)) failures.push(`${check.file} missing marker: ${marker}`)
    }
  }
  console.log(`CHECK GST menu workflow markers -> ${checks.length} pages`)
}

function checkTodoMarkers() {
  const todo = readFileSync(join(modularRoot, 'docs/MODULAR_TODO.md'), 'utf8')
  if (!todo.includes('14G complete') && !todo.includes('Stage 14G')) failures.push('MODULAR_TODO is missing the Stage 14G marker.')
  console.log('CHECK MODULAR_TODO -> Stage 14G marker present')
}

function checkEvidence() {
  if (requireToken && !token) failures.push(`Missing ${tokenEnv}; final live-token GST menu acceptance cannot be required.`)
  if (!token) warnings.push(`${tokenEnv} is not set. Live-token GST return filing/accounting-posting/CA-share evidence remains pending.`)

  if (requireManual && !manualAccepted) failures.push(`Missing ${manualEnv}=YES; manual GST menu acceptance cannot be required.`)
  if (!manualAccepted) warnings.push(`${manualEnv}=YES is not set. Manual GSTR-1/3B builder, accounting posting and CA-share evidence remains pending.`)

  if (!token || !manualAccepted) {
    warnings.push('Books GST menu lane is code-ready, but production sign-off should wait for live-token and manual evidence.')
  }
  console.log('CHECK Books Stage 14G final evidence -> evaluated')
}

function finish() {
  for (const warning of warnings) console.warn(`WARN ${warning}`)
  if (failures.length > 0) {
    for (const failure of failures) console.error(`FAIL ${failure}`)
    process.exitCode = 1
    return
  }

  const status = token && manualAccepted ? 'GO' : 'CONDITIONAL'
  console.log(`Books Stage 14G closure gate passed. Status: ${status}.`)
}
