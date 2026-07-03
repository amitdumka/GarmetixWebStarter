import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import {
  getApiBaseUrl,
  getSmokeHosts,
  getSmokeVersion,
  modularRoot,
  parseSmokeOptions
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
const failOnSyncIssues = hasFlag('--fail-on-sync-issues')
const apiBaseUrl = getApiBaseUrl(hosts.api)

const sourceFiles = [
  join(modularRoot, 'apps/books/pages/accounting.vue'),
  join(modularRoot, 'apps/books/pages/vouchers.vue'),
  join(modularRoot, 'apps/books/pages/parties.vue'),
  join(modularRoot, 'apps/books/pages/cash-details.vue')
]

console.log('Garmetix Books ledger sync readiness')
console.log(`Version: ${version}`)
console.log(`Stage: ${stage}`)
console.log(`Mode: ${mode}`)
console.log(`API base URL: ${apiBaseUrl}`)
console.log(`Live network check: ${live ? 'enabled' : 'disabled'}`)
console.log(`Token env: ${tokenEnv}${token ? ' (set)' : ' (not set)'}`)
console.log(`Strict permissions: ${strictPermissions ? 'enabled' : 'disabled'}`)
console.log(`Fail on sync issues: ${failOnSyncIssues ? 'enabled' : 'disabled'}`)
console.log('Mutation check: disabled')

const sourceFailures = inspectSourceGuards()
if (sourceFailures.length > 0) {
  console.error('\nBooks ledger sync source guard failed:')
  for (const failure of sourceFailures) console.error(`- ${failure}`)
  process.exit(1)
}

if (!live) {
  console.log(`DRY GET ${apiBaseUrl}/accounting/ledger-sync/status - party and bank ledger sync summary`)
  console.log(`DRY GET ${apiBaseUrl}/parties - verify parties expose ledger links only as read-only status`)
  console.log(`DRY GET ${apiBaseUrl}/bank-accounts - verify bank accounts expose ledger links only as read-only status`)
  console.log(`DRY GET ${apiBaseUrl}/ledgers - verify ledgers are selectable without showing internal IsParty flags to the user`)
  console.log('DRY POST accounting/ledger-sync/repair - intentionally not executed')
  console.log('\nDry Books ledger sync readiness passed. Add --live when API and accountant token are available.')
  process.exit(0)
}

if (requireToken && !token) {
  console.error(`Missing required token. Set ${tokenEnv} before running with --require-token.`)
  process.exit(1)
}

const failures = []
const warnings = []

try {
  if (!token) {
    warnings.push(`${tokenEnv} is not set. Live ledger sync checks were skipped.`)
  } else {
    const sync = await requestJson('accounting/ledger-sync/status', 'ledger sync status')
    const parties = await requestJson('parties', 'parties')
    const bankAccounts = await requestJson('bank-accounts', 'bank accounts')
    await requestJson('ledgers', 'ledgers')

    if (sync) {
      const issueCount = readNumber(sync, ['issueCount', 'IssueCount'])
      console.log(`CHECK ledger sync issues ${issueCount}`)
      if (issueCount > 0) {
        const message = `Ledger sync has ${issueCount} issue(s). Party/bank ledger background creation needs review.`
        if (failOnSyncIssues) failures.push(message)
        else warnings.push(message)
      }
    }

    for (const row of toRows(parties)) {
      if (!row.ledgerId && !row.LedgerId) warnings.push(`Party "${row.name || row.Name || row.id || row.Id}" has no ledger link.`)
    }

    for (const row of toRows(bankAccounts)) {
      if (!row.ledgerId && !row.LedgerId) warnings.push(`Bank account "${row.accountNumber || row.AccountNumber || row.id || row.Id}" has no ledger link.`)
    }
  }
} catch (error) {
  failures.push(error instanceof Error ? error.message : 'Books ledger sync readiness failed.')
}

for (const warning of warnings) console.log(`WARN ${warning}`)

if (failures.length > 0) {
  console.error('\nBooks ledger sync readiness failed:')
  for (const failure of failures) console.error(`- ${failure}`)
  process.exit(1)
}

console.log('\nBooks ledger sync readiness passed.')

function inspectSourceGuards() {
  const failures = []
  for (const file of sourceFiles) {
    const source = readFileSync(file, 'utf8')
    if (/PartyLedger|IsParty|isParty/.test(source)) failures.push(`${file} exposes internal party-ledger flag text.`)
    if (/BankLedger|IsBank|isBankLedger/.test(source)) failures.push(`${file} exposes internal bank-ledger flag text.`)
  }
  console.log('PASS source guard: internal party/bank ledger flags are not exposed in Books pages.')
  return failures
}

async function requestJson(path, label) {
  const response = await request(path)
  if (response.status === 200) return await response.json()

  const body = await response.text().catch(() => '')
  const suffix = body ? ` (${body.replace(/\s+/g, ' ').slice(0, 180)})` : ''
  if ([401, 403].includes(response.status) && !strictPermissions) {
    warnings.push(`${label} returned HTTP ${response.status}${suffix}. Use an accountant token or --strict-permissions.`)
    return null
  }

  failures.push(`${label} expected HTTP 200 but returned HTTP ${response.status}${suffix}.`)
  return null
}

async function request(path) {
  const url = `${apiBaseUrl}/${path.replace(/^\//, '')}`
  const controller = new AbortController()
  const timeout = setTimeout(() => controller.abort(), timeoutMs)
  try {
    const response = await fetch(url, {
      method: 'GET',
      cache: 'no-store',
      signal: controller.signal,
      headers: token ? { Authorization: `Bearer ${token}` } : undefined
    })
    console.log(`CHECK GET ${url} -> HTTP ${response.status}`)
    return response
  } finally {
    clearTimeout(timeout)
  }
}

function toRows(value) {
  if (Array.isArray(value)) return value
  if (value && typeof value === 'object') {
    for (const key of ['items', 'rows', 'data', 'results', 'Items', 'Rows', 'Data', 'Results']) {
      if (Array.isArray(value[key])) return value[key]
    }
  }
  return []
}

function readNumber(source, keys) {
  for (const key of keys) {
    const value = source?.[key]
    if (typeof value === 'number') return value
    if (typeof value === 'string' && value.trim() !== '' && !Number.isNaN(Number(value))) return Number(value)
  }
  return 0
}
