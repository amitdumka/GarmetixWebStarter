import {
  getApiBaseUrl,
  getSmokeHosts,
  getSmokeVersion,
  parseSmokeOptions,
  smokeApps
} from './smoke-routes.mjs'

const args = process.argv.slice(2)
const hasFlag = (name) => args.includes(name)
const option = (name, fallback = '') => {
  const prefix = `${name}=`
  const match = args.find((arg) => arg.startsWith(prefix))
  return match ? match.slice(prefix.length) : fallback
}

const { mode, live, timeoutMs } = parseSmokeOptions(args)
const hosts = getSmokeHosts(mode)
const { version, stage } = getSmokeVersion()
const tokenEnv = option('--token-env', 'GARMETIX_SMOKE_AUTH_TOKEN')
const token = process.env[tokenEnv]
const requireToken = hasFlag('--require-token')
const strictPermissions = hasFlag('--strict-permissions')
const returnPeriod = option('--return-period', defaultReturnPeriod())
const bankAccountId = option('--bank-account-id')
const apiBaseUrl = getApiBaseUrl(hosts.api)
const booksApp = smokeApps.find((app) => app.id === 'books')

const routeOwnership = [
  ...(booksApp?.routes ?? []),
  '/cash-details',
  '/financial-year-locks',
  '/commercial-notes',
  '/parties',
  '/vendor-settlements',
  '/gst-reports',
  '/gst-production',
  '/debit-notes',
  '/credit-notes'
]

const checks = [
  { id: 'ledger-groups', path: 'ledger-groups', expectation: 'ledger group master list is reachable' },
  { id: 'ledgers', path: 'ledgers', expectation: 'ledger master list is reachable' },
  { id: 'parties', path: 'parties', expectation: 'party master list is reachable' },
  { id: 'banks', path: 'banks', expectation: 'bank master list is reachable' },
  { id: 'bank-accounts', path: 'bank-accounts', expectation: 'bank account master list is reachable' },
  { id: 'vouchers', path: 'vouchers', expectation: 'regular voucher list is reachable' },
  { id: 'petty-cash-sheets', path: 'petty-cash-sheets', expectation: 'petty cash sheet list is reachable' },
  { id: 'trial-balance', path: 'accounting/trial-balance', expectation: 'trial balance read model is reachable' },
  { id: 'ledger-sync-status', path: 'accounting/ledger-sync/status', expectation: 'party/bank ledger sync status is reachable' },
  { id: 'audit-recent', path: 'accounting/audit/recent?take=25', expectation: 'accounting audit event list is reachable' },
  { id: 'message-logs', path: 'accounting/message-logs?take=25', expectation: 'accounting message log list is reachable' },
  { id: 'bank-transactions', path: 'accounting/bank-transactions', expectation: 'bank transaction list is reachable' },
  { id: 'cheque-logs', path: 'cheque-logs', expectation: 'cheque log list is reachable' },
  { id: 'financial-year-locks', path: 'accounting/financial-year-locks?activeOnly=false', expectation: 'financial year lock list is reachable' },
  { id: 'journal-validation', path: 'accounting/journal/validation', expectation: 'journal validation read model is reachable' },
  { id: 'gst-accounting-summary', path: `gst-returns/accounting-summary?${new URLSearchParams({ returnPeriod }).toString()}`, expectation: 'GST accounting summary is reachable for return period' }
]

const optionalBankAccountChecks = bankAccountId
  ? [
      { id: 'bank-statement', path: `accounting/bank-statement/${bankAccountId}`, expectation: 'bank statement rows are reachable for selected account' },
      { id: 'bank-reconciliation', path: `accounting/bank-reconciliation/${bankAccountId}`, expectation: 'bank reconciliation rows are reachable for selected account' }
    ]
  : []

console.log('Garmetix Books accounting readiness')
console.log(`Version: ${version}`)
console.log(`Stage: ${stage}`)
console.log(`Mode: ${mode}`)
console.log(`API base URL: ${apiBaseUrl}`)
console.log(`Books app base URL: ${hosts.books}`)
console.log(`Live network check: ${live ? 'enabled' : 'disabled'}`)
console.log(`Token env: ${tokenEnv}${token ? ' (set)' : ' (not set)'}`)
console.log(`Strict permissions: ${strictPermissions ? 'enabled' : 'disabled'}`)
console.log(`Return period: ${returnPeriod}`)
console.log(`Bank account id: ${bankAccountId || 'not provided'}`)
console.log('Mutation check: disabled')
console.log('Ledger/voucher posting check: disabled')

console.log('\nBooks route ownership:')
for (const route of [...new Set(routeOwnership)]) {
  console.log(`- ${hosts.books}${route === '/' ? '' : route}`)
}

if (!live) {
  console.log(`\nDRY GET ${apiBaseUrl}/health - API health should return HTTP 200`)
  console.log(`DRY GET ${apiBaseUrl}/auth/me - token should identify an accountant/books-capable user`)
  for (const check of [...checks, ...optionalBankAccountChecks]) {
    console.log(`DRY GET ${apiBaseUrl}/${check.path} - ${check.expectation}`)
  }
  if (!bankAccountId) {
    console.log('DRY SKIP accounting/bank-statement/{bankAccountId} - pass --bank-account-id=<guid> to include this optional check')
    console.log('DRY SKIP accounting/bank-reconciliation/{bankAccountId} - pass --bank-account-id=<guid> to include this optional check')
  }
  console.log('DRY POST vouchers - intentionally not executed')
  console.log('DRY POST accounting/bank-transactions - intentionally not executed')
  console.log('DRY POST accounting/financial-year-locks - intentionally not executed')
  console.log('\nDry Books accounting readiness passed. Add --live when API and test credentials are available.')
  process.exit(0)
}

if (requireToken && !token) {
  console.error(`Missing required token. Set ${tokenEnv} before running with --require-token.`)
  process.exit(1)
}

const failures = []
const warnings = []

try {
  const health = await request('health', { auth: false })
  if (health.status !== 200) failures.push(`API health expected HTTP 200 but returned HTTP ${health.status}.`)

  if (!token) {
    warnings.push(`${tokenEnv} is not set. Authenticated Books endpoint checks were skipped after API health.`)
  } else {
    const me = await request('auth/me')
    if (me.status !== 200) failures.push(`/auth/me expected HTTP 200 with token but returned HTTP ${me.status}.`)

    for (const check of [...checks, ...optionalBankAccountChecks]) {
      await checkReadEndpoint(check)
    }
  }
} catch (error) {
  failures.push(error instanceof Error ? error.message : 'Books accounting readiness failed.')
}

for (const warning of warnings) console.log(`WARN ${warning}`)

if (failures.length > 0) {
  console.error('\nBooks accounting readiness failed:')
  for (const failure of failures) console.error(`- ${failure}`)
  process.exit(1)
}

console.log('\nBooks accounting readiness passed.')

async function checkReadEndpoint(check) {
  const response = await request(check.path)
  if (response.status === 200) return

  const body = await readBriefBody(response)
  const suffix = body ? ` (${body})` : ''
  if ([401, 403].includes(response.status) && !strictPermissions) {
    warnings.push(`${check.id} returned HTTP ${response.status}${suffix}. Run with an accountant/admin token or add --strict-permissions to fail this.`)
    return
  }

  if (response.status === 404) {
    failures.push(`${check.id} route is missing: ${check.path} returned HTTP 404${suffix}.`)
    return
  }

  failures.push(`${check.id} expected HTTP 200 but returned HTTP ${response.status}${suffix}.`)
}

async function request(path, options = {}) {
  const auth = options.auth ?? true
  const url = `${apiBaseUrl}/${path.replace(/^\//, '')}`
  const controller = new AbortController()
  const timeout = setTimeout(() => controller.abort(), timeoutMs)
  try {
    const response = await fetch(url, {
      method: 'GET',
      cache: 'no-store',
      signal: controller.signal,
      headers: auth && token
        ? { Authorization: `Bearer ${token}` }
        : undefined
    })
    console.log(`CHECK GET ${url} -> HTTP ${response.status}`)
    return response
  } finally {
    clearTimeout(timeout)
  }
}

async function readBriefBody(response) {
  const text = await response.text().catch(() => '')
  return text.replace(/\s+/g, ' ').slice(0, 180)
}

function defaultReturnPeriod() {
  const now = new Date()
  return `${now.getFullYear()}${String(now.getMonth() + 1).padStart(2, '0')}`
}
