import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { getSmokeVersion, modularRoot } from './smoke-routes.mjs'

const { version, stage } = getSmokeVersion()
const failures = []

console.log('Garmetix Books Version6 parity baseline')
console.log(`Version: ${version}`)
console.log(`Stage: ${stage}`)
console.log('Mutation check: disabled')
console.log('Ledger/voucher posting check: disabled')

if (!version.startsWith('6.')) failures.push(`Expected Version6, found ${version}.`)

checkArtifacts()
checkPackageScripts()
checkRouteOwnership()
checkBooksPages()
checkTodoHandoff()

if (failures.length > 0) {
  console.error('\nBooks Version6 parity baseline failed:')
  for (const failure of failures) console.error(`- ${failure}`)
  process.exit(1)
}

console.log('\nBooks Version6 parity baseline passed.')

function checkArtifacts() {
  const required = [
    'docs/stage-13d1-books-accounting-readiness.md',
    'docs/stage-13d2-books-accounting-contracts.md',
    'docs/stage-13d3-books-browser-acceptance.md',
    'docs/stage-13d4-books-ledger-sync-readiness.md',
    'docs/stage-13d5-books-posting-preflight.md',
    'docs/stage-13d-final-books-closure.md',
    'docs/stage-14c1-books-parity-baseline.md',
    'scripts/books-accounting-readiness.mjs',
    'scripts/books-accounting-contract-check.mjs',
    'scripts/books-browser-acceptance.mjs',
    'scripts/books-ledger-sync-readiness.mjs',
    'scripts/books-posting-preflight.mjs',
    'scripts/books-stage13d-closure.mjs',
    'scripts/books-parity-baseline.mjs'
  ]

  for (const relativePath of required) {
    if (!existsSync(join(modularRoot, relativePath))) failures.push(`Missing required Books baseline artifact: ${relativePath}`)
  }
  console.log(`CHECK Books baseline artifacts -> ${required.length} paths`)
}

function checkPackageScripts() {
  const rootPackage = readFileSync(join(modularRoot, '../../package.json'), 'utf8')
  const modularPackage = readFileSync(join(modularRoot, 'package.json'), 'utf8')
  const required = [
    'modular:books:accounting-readiness',
    'modular:books:accounting-contract',
    'modular:books:browser-acceptance',
    'modular:books:ledger-sync-readiness',
    'modular:books:posting-preflight',
    'modular:books:stage13d-closure',
    'modular:books:parity-baseline',
    'books:parity-baseline'
  ]

  for (const marker of required) {
    const source = marker.startsWith('modular:') ? rootPackage : modularPackage
    if (!source.includes(marker)) failures.push(`Missing package script: ${marker}`)
  }
  console.log('CHECK package scripts -> Books baseline wired')
}

function checkRouteOwnership() {
  const routes = readFileSync(join(modularRoot, 'config/routes.ts'), 'utf8')
  const requiredRoutes = [
    "targetApp: 'books'",
    "path: '/accounting'",
    "path: '/financial-year-locks'",
    "path: '/petty-cash'",
    "path: '/cash-details'",
    "path: '/vouchers'",
    "path: '/vendor-payments'",
    "path: '/vendor-settlements'",
    "path: '/parties'",
    "path: '/gst-returns'",
    "path: '/gst-reports'",
    "path: '/gst-production'",
    "path: '/audit'",
    "path: '/message-logs'"
  ]

  for (const marker of requiredRoutes) {
    if (!routes.includes(marker)) failures.push(`Books route registry missing marker: ${marker}`)
  }
  console.log(`CHECK Books route ownership -> ${requiredRoutes.length} markers`)
}

function checkBooksPages() {
  const checks = [
    {
      file: 'apps/books/pages/accounting.vue',
      markers: ['accounting/trial-balance']
    },
    {
      file: 'apps/books/pages/ledgers.vue',
      markers: ['ledger-groups', 'ledgers', 'accounting/ledger-sync/status']
    },
    {
      // Stage 14Q.6 moved every banking tab (bank accounts, transactions,
      // reconciliation, cheque log, vendor banks, account details) out of
      // accounting.vue into its own page.
      file: 'apps/books/pages/banking.vue',
      markers: ['bank-accounts', 'accounting/bank-transactions', 'cheque-logs', 'vendor-bank-accounts']
    },
    {
      file: 'apps/books/pages/vouchers.vue',
      markers: ['vouchers', 'ledgerId', 'paymentMode', 'downloadVoucher']
    },
    {
      file: 'apps/books/pages/petty-cash.vue',
      markers: ['petty-cash-sheets', 'downloadSelectedSheet', 'Cash in Hand']
    },
    {
      file: 'apps/books/pages/cash-details.vue',
      markers: ['accounting/bank-transactions', 'cheque-logs', 'accounting/bank-reconciliation']
    },
    {
      file: 'apps/books/pages/vendor-payments.vue',
      markers: ['vendor payment', 'downloadVoucher', 'downloadInvoice']
    },
    {
      file: 'apps/books/pages/vendor-settlements.vue',
      markers: ['vendor settlement', 'downloadVoucher']
    },
    {
      file: 'apps/books/pages/parties.vue',
      // Stage 14K rebuilt this page from the internal ledger-linked Party master
      // into legacy's actual design - a customer/vendor GSTIN register - so it
      // legitimately no longer references "ledger" at all (that concept still
      // lives in accounting.vue's Parties tab, unchanged).
      markers: ['parties', 'gstin']
    },
    {
      file: 'apps/books/pages/gst-returns.vue',
      markers: ['gst-returns/drafts', 'gst-returns/accounting-summary', 'downloadDraft']
    },
    {
      file: 'apps/books/pages/gst-reports.vue',
      markers: ['gst-returns/reports/hsn-summary', 'downloadCsv']
    },
    {
      file: 'apps/books/pages/audit.vue',
      markers: ['accounting/audit/recent', 'traceIdentifier']
    },
    {
      file: 'apps/books/pages/message-logs.vue',
      markers: ['accounting/message-logs', 'message']
    }
  ]

  for (const check of checks) {
    const path = join(modularRoot, check.file)
    if (!existsSync(path)) {
      failures.push(`Missing Books page: ${check.file}`)
      continue
    }
    const source = readFileSync(path, 'utf8')
    for (const marker of check.markers) {
      if (!source.toLowerCase().includes(marker.toLowerCase())) failures.push(`${check.file} missing marker: ${marker}`)
    }
  }
  console.log(`CHECK Books page coverage -> ${checks.length} pages`)
}

function checkTodoHandoff() {
  const todo = readFileSync(join(modularRoot, 'docs/MODULAR_TODO.md'), 'utf8')
  const markers = ['## Stage 14C', '14C.1 complete', '14C.2 complete']
  for (const marker of markers) {
    if (!todo.includes(marker)) failures.push(`MODULAR_TODO missing marker: ${marker}`)
  }
  console.log('CHECK Books TODO handoff -> documented')
}
