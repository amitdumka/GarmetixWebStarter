import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import {
  getApiBaseUrl,
  getSmokeHosts,
  getSmokeVersion,
  repoRoot,
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
const returnPeriod = option('--return-period', defaultReturnPeriod())
const apiBaseUrl = getApiBaseUrl(hosts.api)

const accountingDtoPath = join(repoRoot, 'legacy/backend/Garmetix.Api/Accounting/AccountingDtos.cs')
const dtoSource = readFileSync(accountingDtoPath, 'utf8')
const preflightContracts = [
  { record: 'VoucherSaveRequest', required: ['voucherNumber', 'onDate', 'voucherType', 'amount', 'paymentMode', 'isParty', 'partyId', 'ledgerId', 'accountNumber', 'companyId', 'storeGroupId', 'storeId'] },
  { record: 'BankTransactionSaveRequest', required: ['companyId', 'storeGroupId', 'storeId', 'bankAccountId', 'ledgerId', 'partyId', 'onDate', 'transactionType', 'transactionMode', 'amount'] },
  { record: 'GstAccountingPostRequest', required: ['companyId', 'storeGroupId', 'storeId', 'returnPeriod', 'onDate', 'outputTax', 'inputTax', 'interestLateFee', 'draftId'] },
  { record: 'FinancialYearLockSaveRequest', required: ['companyId', 'financialYear', 'periodStart', 'periodEnd', 'lockAccounting', 'lockSales', 'lockPurchase', 'lockInventory', 'lockGst'] }
]

console.log('Garmetix Books posting preflight')
console.log(`Version: ${version}`)
console.log(`Stage: ${stage}`)
console.log(`Mode: ${mode}`)
console.log(`API base URL: ${apiBaseUrl}`)
console.log(`Live network check: ${live ? 'enabled' : 'disabled'}`)
console.log(`Token env: ${tokenEnv}${token ? ' (set)' : ' (not set)'}`)
console.log(`Strict permissions: ${strictPermissions ? 'enabled' : 'disabled'}`)
console.log(`Return period: ${returnPeriod}`)
console.log('Mutation check: disabled')

for (const contract of preflightContracts) {
  const keys = parseRecordParameters(dtoSource, contract.record)
  const missing = contract.required.filter((key) => !keys.includes(key))
  if (missing.length > 0) {
    console.error(`${contract.record} is missing required posting field(s): ${missing.join(', ')}`)
    process.exit(1)
  }
  console.log(`PASS ${contract.record}: required posting fields are present.`)
}

if (!live) {
  console.log(`DRY GET ${apiBaseUrl}/vouchers - regular voucher prerequisites`)
  console.log(`DRY GET ${apiBaseUrl}/ledgers - posting ledger prerequisites`)
  console.log(`DRY GET ${apiBaseUrl}/parties - party-ledger prerequisites`)
  console.log(`DRY GET ${apiBaseUrl}/bank-accounts - bank posting prerequisites`)
  console.log(`DRY GET ${apiBaseUrl}/accounting/journal/validation - journal balance guard`)
  console.log(`DRY GET ${apiBaseUrl}/gst-returns/accounting-summary?returnPeriod=${returnPeriod} - GST posting summary guard`)
  console.log('DRY POST vouchers - intentionally not executed')
  console.log('DRY POST accounting/bank-transactions - intentionally not executed')
  console.log('DRY POST gst-returns/accounting-post - intentionally not executed')
  console.log('DRY POST accounting/financial-year-locks - intentionally not executed')
  console.log('\nDry Books posting preflight passed. Add --live when API and accountant token are available.')
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
    warnings.push(`${tokenEnv} is not set. Live posting prerequisite checks were skipped.`)
  } else {
    const ledgers = await requestJson('ledgers', 'ledgers')
    const parties = await requestJson('parties', 'parties')
    const banks = await requestJson('bank-accounts', 'bank accounts')
    await requestJson('vouchers', 'vouchers')
    const journal = await requestJson('accounting/journal/validation', 'journal validation')
    await requestJson(`gst-returns/accounting-summary?${new URLSearchParams({ returnPeriod }).toString()}`, 'GST accounting summary')

    if (toRows(ledgers).length === 0) warnings.push('No ledgers returned; voucher posting needs ledger prerequisites.')
    if (toRows(parties).length === 0) warnings.push('No parties returned; party voucher posting needs party prerequisites.')
    if (toRows(banks).length === 0) warnings.push('No bank accounts returned; non-cash posting needs bank prerequisites.')
    if (journal && readNumber(journal, ['difference', 'Difference']) !== 0) {
      warnings.push('Journal validation reports a non-zero difference. Review before live posting.')
    }
  }
} catch (error) {
  failures.push(error instanceof Error ? error.message : 'Books posting preflight failed.')
}

for (const warning of warnings) console.log(`WARN ${warning}`)

if (failures.length > 0) {
  console.error('\nBooks posting preflight failed:')
  for (const failure of failures) console.error(`- ${failure}`)
  process.exit(1)
}

console.log('\nBooks posting preflight passed.')

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

function parseRecordParameters(source, recordName) {
  const match = source.match(new RegExp(`record\\s+${recordName}\\s*\\(([\\s\\S]*?)\\);`))
  if (!match) throw new Error(`Could not find backend DTO record ${recordName}.`)

  return match[1]
    .split('\n')
    .map(line => line.trim().replace(/,$/, ''))
    .filter(Boolean)
    .map(line => {
      const withoutDefault = line.split('=')[0]?.trim() ?? ''
      return withoutDefault.split(/\s+/).at(-1)?.replace(/\?$/, '').trim()
    })
    .filter(Boolean)
    .map(pascalToCamel)
}

function pascalToCamel(value) {
  return value ? value[0].toLowerCase() + value.slice(1) : value
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

function defaultReturnPeriod() {
  const now = new Date()
  return `${now.getFullYear()}${String(now.getMonth() + 1).padStart(2, '0')}`
}
