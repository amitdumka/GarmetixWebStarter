import {
  getApiBaseUrl,
  getSmokeHosts,
  getSmokeVersion,
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
const requireBank = hasFlag('--require-bank')
const companyIdOption = option('--company-id')
const storeIdOption = option('--store-id')
const barcodeOption = option('--barcode')
const queryOption = option('--query', barcodeOption || 'shirt')
const apiBaseUrl = getApiBaseUrl(hosts.api)

console.log('Garmetix POS live save fixture readiness')
console.log(`Version: ${version}`)
console.log(`Stage: ${stage}`)
console.log(`Mode: ${mode}`)
console.log(`API base URL: ${apiBaseUrl}`)
console.log(`Live network check: ${live ? 'enabled' : 'disabled'}`)
console.log(`Token env: ${tokenEnv}${token ? ' (set)' : ' (not set)'}`)
console.log(`Mutation check: disabled`)

if (!live) {
  console.log(`DRY GET ${apiBaseUrl}/health - API health should return HTTP 200`)
  console.log(`DRY GET ${apiBaseUrl}/auth/me - token should identify a POS-capable user`)
  console.log(`DRY GET ${apiBaseUrl}/stores - at least one permitted store is required`)
  console.log(`DRY GET ${apiBaseUrl}/billing/options?companyId=<id>&storeId=<id> - Manager/default salesman readiness`)
  console.log(`DRY GET ${apiBaseUrl}/product-lookup?query=${encodeURIComponent(queryOption)}&storeId=<id>&take=25 - at least one sellable stock row is required`)
  console.log(`DRY GET ${apiBaseUrl}/bank-accounts - required only for non-cash fixture acceptance`)
  console.log(`DRY POST ${apiBaseUrl}/billing/sales - intentionally not executed in this readiness stage`)
  console.log('\nDry POS live save fixture readiness passed. Add --live when the API and test data are available.')
  process.exit(0)
}

if (requireToken && !token) {
  console.error(`Missing required token. Set ${tokenEnv} before running with --require-token.`)
  process.exit(1)
}

const failures = []
const warnings = []

try {
  const health = await request('health', { auth: false, allowAnonymous: true })
  expectStatus('API health', health.status, [200])

  if (!token) {
    warnings.push(`${tokenEnv} is not set. Live fixture details were skipped after anonymous health.`)
  } else {
    const me = await request('auth/me')
    expectStatus('Token auth', me.status, [200])

    const stores = await requestJson('stores')
    if (!Array.isArray(stores) || stores.length === 0) {
      failures.push('No permitted stores were returned for the smoke user.')
    }

    const store = selectStore(stores)
    if (!store) {
      failures.push('Could not select a store. Pass --store-id=<guid> or check user store permissions.')
    } else {
      const companyId = companyIdOption || store.companyId || store.CompanyId
      const storeGroupId = store.storeGroupId || store.StoreGroupId
      const storeId = store.id || store.Id
      if (!isGuid(companyId)) failures.push('Selected store does not expose a valid company id.')
      if (!isGuid(storeGroupId)) failures.push('Selected store does not expose a valid store group id.')
      if (!isGuid(storeId)) failures.push('Selected store does not expose a valid store id.')

      console.log(`CHECK selected store ${store.name || store.storeName || store.Name || storeId}`)

      const billingOptions = await requestJson(`billing/options?${new URLSearchParams({
        companyId,
        storeId,
        take: '25'
      }).toString()}`)
      const salesmen = Array.isArray(billingOptions?.salesmen) ? billingOptions.salesmen : []
      if (salesmen.length === 0) {
        warnings.push('Billing options returned no salesman rows. Save may still create Manager through backend fallback.')
      } else {
        const manager = salesmen.find((item) => String(item.name || item.Name || '').toLowerCase() === 'manager')
        console.log(`CHECK salesmen ${salesmen.length}${manager ? ' with Manager available' : ''}`)
      }

      const productPath = barcodeOption
        ? `product-lookup/barcode/${encodeURIComponent(barcodeOption)}?${new URLSearchParams({ storeId }).toString()}`
        : `product-lookup?${new URLSearchParams({ query: queryOption, storeId, take: '25' }).toString()}`
      const productResult = await requestJson(productPath)
      const products = Array.isArray(productResult) ? productResult : [productResult].filter(Boolean)
      const sellable = products.find((item) => Number(item.availableQty ?? item.AvailableQty ?? 0) > 0 && Number(item.mrp ?? item.Mrp ?? 0) >= 0)
      if (!sellable) {
        failures.push(`No sellable stock row was found for ${barcodeOption ? `barcode ${barcodeOption}` : `query ${queryOption}`}.`)
      } else {
        const availableQty = Number(sellable.availableQty ?? sellable.AvailableQty ?? 0)
        console.log(`CHECK sellable product ${sellable.name || sellable.Name || sellable.barcode || sellable.Barcode} qty ${availableQty}`)
      }

      const bankAccounts = await requestJson('bank-accounts').catch((error) => {
        warnings.push(`Bank account readiness skipped: ${error.message}`)
        return []
      })
      const matchingBankAccounts = Array.isArray(bankAccounts)
        ? bankAccounts.filter((account) => !companyId || String(account.companyId || account.CompanyId || '').toLowerCase() === String(companyId).toLowerCase())
        : []
      if (requireBank && matchingBankAccounts.length === 0) {
        failures.push('No bank account was available for the selected company, but --require-bank was used.')
      } else if (matchingBankAccounts.length === 0) {
        warnings.push('No matching bank account found. Cash acceptance can run; non-cash acceptance needs a bank account.')
      } else {
        console.log(`CHECK bank accounts ${matchingBankAccounts.length}`)
      }

      if (sellable) {
        const salePayload = buildFixtureSalePayload({ companyId, storeGroupId, storeId, product: sellable, bankAccount: matchingBankAccounts[0] })
        validateFixturePayload(salePayload)
        console.log(`CHECK fixture sale payload ready for barcode ${salePayload.items[0].barcode}`)
      }
    }
  }
} catch (error) {
  failures.push(error instanceof Error ? error.message : 'POS live save fixture readiness failed.')
}

for (const warning of warnings) console.log(`WARN ${warning}`)

if (failures.length > 0) {
  console.error('\nPOS live save fixture readiness failed:')
  for (const failure of failures) console.error(`- ${failure}`)
  process.exit(1)
}

console.log('\nPOS live save fixture readiness passed.')

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

async function requestJson(path) {
  const response = await request(path)
  if (response.status !== 200) {
    const text = await response.text().catch(() => '')
    throw new Error(`${path} expected HTTP 200 but returned HTTP ${response.status}${text ? ` (${text.replace(/\s+/g, ' ').slice(0, 180)})` : ''}`)
  }

  return await response.json()
}

function expectStatus(label, status, expected) {
  if (!expected.includes(status)) failures.push(`${label} expected HTTP ${expected.join('/')} but returned HTTP ${status}.`)
}

function selectStore(stores) {
  if (!Array.isArray(stores)) return null
  if (storeIdOption) {
    return stores.find((store) => String(store.id || store.Id || '').toLowerCase() === storeIdOption.toLowerCase()) || null
  }

  return stores[0] || null
}

function buildFixtureSalePayload({ companyId, storeGroupId, storeId, product, bankAccount }) {
  const productId = product.productId || product.ProductId
  const barcode = product.barcode || product.Barcode
  const mrp = Number(product.mrp ?? product.Mrp ?? 0)
  const bankAccountId = bankAccount?.id || bankAccount?.Id || null
  return {
    companyId,
    storeGroupId,
    storeId,
    customerName: 'POS Fixture Customer',
    customerMobileNumber: '',
    customerGstin: '',
    paymentMode: 0,
    bankAccountId: null,
    paidAmount: Math.round(mrp),
    billDiscountAmount: 0,
    customerId: null,
    salesmanId: null,
    payments: [
      {
        paymentMode: 0,
        amount: Math.round(mrp),
        bankAccountId: null,
        referenceNumber: null,
        gatewayReference: null,
        settlementStatus: null,
        adjustmentSourceType: null,
        adjustmentSourceId: null
      }
    ],
    nonCashFixture: bankAccountId
      ? {
          paymentMode: 2,
          amount: Math.round(mrp),
          bankAccountId,
          referenceNumber: 'UTR-FIXTURE-READINESS'
        }
      : null,
    items: [
      {
        productId,
        barcode,
        quantity: 1,
        mrp,
        discountAmount: 0
      }
    ]
  }
}

function validateFixturePayload(payload) {
  for (const field of ['companyId', 'storeGroupId', 'storeId']) {
    if (!isGuid(payload[field])) failures.push(`Fixture payload has invalid ${field}.`)
  }
  const item = payload.items[0]
  if (!item) failures.push('Fixture payload has no item.')
  if (item && !isGuid(item.productId)) failures.push('Fixture item has invalid productId.')
  if (item && !item.barcode) failures.push('Fixture item has no barcode.')
  if (item && item.quantity <= 0) failures.push('Fixture item quantity must be greater than zero.')
  if (payload.paidAmount < 0) failures.push('Fixture paid amount cannot be negative.')
}

function isGuid(value) {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(String(value || ''))
}
