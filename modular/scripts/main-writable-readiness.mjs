import { readFileSync } from 'node:fs'
import { join } from 'node:path'
import {
  getApiBaseUrl,
  getSmokeHosts,
  getSmokeVersion,
  parseSmokeOptions,
  repoRoot
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
const take = Number(option('--take', '25'))
const customerQuery = option('--customer-query', '')
const productQuery = option('--product-query', 'shirt')
const apiBaseUrl = getApiBaseUrl(hosts.api)

const billingDtoSource = readFileSync(join(repoRoot, 'legacy/backend/Garmetix.Api/Billing/BillingDtos.cs'), 'utf8')
const billingEndpointSource = readFileSync(join(repoRoot, 'legacy/backend/Garmetix.Api/Billing/BillingEndpoints.cs'), 'utf8')
const purchaseDtoSource = readFileSync(join(repoRoot, 'legacy/backend/Garmetix.Api/Purchase/PurchaseDtos.cs'), 'utf8')
const purchaseEndpointSource = readFileSync(join(repoRoot, 'legacy/backend/Garmetix.Api/Purchase/PurchaseEndpoints.cs'), 'utf8')
const stockOperationDtoSource = readFileSync(join(repoRoot, 'legacy/backend/Garmetix.Api/Inventory/StockOperationDtos.cs'), 'utf8')
const stockOperationEndpointSource = readFileSync(join(repoRoot, 'legacy/backend/Garmetix.Api/Inventory/StockOperationEndpoints.cs'), 'utf8')
const stockReportEndpointSource = readFileSync(join(repoRoot, 'legacy/backend/Garmetix.Api/Inventory/StockReportEndpoints.cs'), 'utf8')

const failures = []
const warnings = []

const contractChecks = [
  {
    label: 'PosSaleRequest',
    source: billingDtoSource,
    record: 'PosSaleRequest',
    required: ['companyId', 'storeGroupId', 'storeId', 'customerName', 'customerMobileNumber', 'customerGstin', 'paymentMode', 'bankAccountId', 'paidAmount', 'billDiscountAmount', 'items', 'customerId', 'salesmanId', 'payments']
  },
  {
    label: 'InvoicePaymentDetailRequest',
    source: billingDtoSource,
    record: 'InvoicePaymentDetailRequest',
    required: ['paymentMode', 'amount', 'bankAccountId', 'referenceNumber', 'gatewayReference', 'settlementStatus', 'adjustmentSourceType', 'adjustmentSourceId']
  },
  {
    label: 'BillingCustomerProfileDto',
    source: billingDtoSource,
    record: 'BillingCustomerProfileDto',
    required: ['customer', 'creditNotes', 'advanceReceipts', 'loyaltyProgram']
  },
  {
    label: 'PurchaseInwardRequest',
    source: purchaseDtoSource,
    record: 'PurchaseInwardRequest',
    required: ['companyId', 'storeGroupId', 'storeId', 'vendorName', 'vendorMobileNumber', 'vendorGstin', 'invoiceNumber', 'inwardNumber', 'paidAmount', 'paymentMode', 'bankAccountId', 'frightAmount', 'items', 'supplierInvoiceDate', 'dueDate', 'vendorId']
  },
  {
    label: 'PurchaseInwardItemRequest',
    source: purchaseDtoSource,
    record: 'PurchaseInwardItemRequest',
    required: ['productId', 'productName', 'barcode', 'quantity', 'costPrice', 'mrp', 'discountAmount', 'taxId', 'productCategoryId', 'productSubCategoryId', 'hsnCode', 'productUnit', 'productType', 'productGroup']
  },
  {
    label: 'StockOperationOptionsDto',
    source: stockOperationDtoSource,
    record: 'StockOperationOptionsDto',
    required: ['products', 'stores', 'recentMovements']
  },
  {
    label: 'StockAdjustmentRequest',
    source: stockOperationDtoSource,
    record: 'StockAdjustmentRequest',
    required: ['stockId', 'quantity', 'direction', 'reason']
  },
  {
    label: 'StockTransferRequest',
    source: stockOperationDtoSource,
    record: 'StockTransferRequest',
    required: ['fromStockId', 'toStoreId', 'quantity', 'reason']
  },
  {
    label: 'PhysicalStockCountRequest',
    source: stockOperationDtoSource,
    record: 'PhysicalStockCountRequest',
    required: ['stockId', 'countedQuantity', 'reason']
  },
  {
    label: 'StockWriteOffRequest',
    source: stockOperationDtoSource,
    record: 'StockWriteOffRequest',
    required: ['stockId', 'quantity', 'reason']
  }
]

const endpointChecks = [
  { label: 'sale review list', source: billingEndpointSource, pattern: 'group.MapGet("/sales/recent", GetRecentSalesAsync)' },
  { label: 'sale receipt review', source: billingEndpointSource, pattern: 'group.MapGet("/sales/{id:guid}/receipt", GetReceiptAsync)' },
  { label: 'sale PDF review', source: billingEndpointSource, pattern: 'group.MapGet("/sales/{id:guid}/pdf", DownloadInvoicePdfAsync)' },
  { label: 'sale cancel remains delete protected', source: billingEndpointSource, pattern: 'group.MapPost("/sales/{id:guid}/cancel", CancelSaleAsync).RequireAuthorization(GarmetixPolicies.Delete)' },
  { label: 'customer search', source: billingEndpointSource, pattern: 'group.MapGet("/customers/search", SearchCustomersAsync)' },
  { label: 'customer profile handoff', source: billingEndpointSource, pattern: 'group.MapGet("/customers/{customerId:guid}/profile", GetCustomerBillingProfileAsync)' },
  { label: 'purchase lookup options', source: purchaseEndpointSource, pattern: 'group.MapGet("/lookup-options", GetLookupOptionsAsync)' },
  { label: 'purchase recent review', source: purchaseEndpointSource, pattern: 'group.MapGet("/invoices/recent", GetRecentPurchaseInvoicesAsync)' },
  { label: 'purchase receipt review', source: purchaseEndpointSource, pattern: 'group.MapGet("/invoices/{id:guid}/receipt", GetReceiptAsync)' },
  { label: 'purchase inward write endpoint', source: purchaseEndpointSource, pattern: 'group.MapPost("/inward", CreateInwardAsync)' },
  { label: 'inventory stock summary', source: stockReportEndpointSource, pattern: 'group.MapGet("/summary", SummaryAsync)' },
  { label: 'inventory movement history', source: stockReportEndpointSource, pattern: 'group.MapGet("/movement-history", MovementHistoryAsync)' },
  { label: 'stock operation options', source: stockOperationEndpointSource, pattern: 'group.MapGet("/options", OptionsAsync)' },
  { label: 'stock operation movements', source: stockOperationEndpointSource, pattern: 'group.MapGet("/movements", MovementsAsync)' },
  { label: 'stock operation documents', source: stockOperationEndpointSource, pattern: 'group.MapGet("/documents", DocumentsAsync)' },
  { label: 'stock operation valuation', source: stockOperationEndpointSource, pattern: 'group.MapGet("/valuation", ValuationAsync)' },
  { label: 'stock adjustment edit protected', source: stockOperationEndpointSource, pattern: 'group.MapPost("/adjustment", CreateAdjustmentAsync).RequireAuthorization(GarmetixPolicies.Edit)' },
  { label: 'stock transfer edit protected', source: stockOperationEndpointSource, pattern: 'group.MapPost("/transfer", CreateTransferAsync).RequireAuthorization(GarmetixPolicies.Edit)' },
  { label: 'stock physical count edit protected', source: stockOperationEndpointSource, pattern: 'group.MapPost("/physical-count", CreatePhysicalCountAsync).RequireAuthorization(GarmetixPolicies.Edit)' },
  { label: 'stock write-off edit protected', source: stockOperationEndpointSource, pattern: 'group.MapPost("/write-off", CreateWriteOffAsync).RequireAuthorization(GarmetixPolicies.Edit)' }
]

const readChecks = [
  { id: 'recent-sales', path: `billing/sales/recent?${new URLSearchParams({ take: String(take) }).toString()}`, label: 'sale review actions need recent invoice ids before receipt/PDF/cancel handoff' },
  { id: 'billing-options', path: 'billing/options', label: 'sale-review and customer handoff need customer/salesman/billing options' },
  { id: 'customer-search', path: `billing/customers/search?${new URLSearchParams({ q: customerQuery, take: String(take) }).toString()}`, label: 'customer profile handoff needs searchable customer rows' },
  { id: 'recent-purchases', path: `purchase/invoices/recent?${new URLSearchParams({ take: String(take) }).toString()}`, label: 'purchase review needs recent invoice ids before receipt/PDF/payment handoff' },
  { id: 'purchase-lookup-options', path: 'purchase/lookup-options', label: 'purchase intake needs vendors, taxes, product categories and enum options' },
  { id: 'product-lookup', path: `product-lookup?${new URLSearchParams({ query: productQuery, take: String(take) }).toString()}`, label: 'purchase intake and inventory handoff need product lookup rows' },
  { id: 'stock-summary', path: 'inventory/stock-reports/summary', label: 'inventory operation preflight needs stock snapshot summary' },
  { id: 'stock-movements', path: `inventory/stock-operations/movements?${new URLSearchParams({ take: String(take) }).toString()}`, label: 'inventory operation preflight needs recent stock movements' },
  { id: 'stock-options', path: 'inventory/stock-operations/options', label: 'inventory adjustment/transfer preflight needs stock and store options' },
  { id: 'stock-documents', path: `inventory/stock-operations/documents?${new URLSearchParams({ take: String(take) }).toString()}`, label: 'inventory operation review needs posted document rows' },
  { id: 'stock-valuation', path: `inventory/stock-operations/valuation?${new URLSearchParams({ take: String(take) }).toString()}`, label: 'inventory operation preflight needs valuation guard rows' },
  { id: 'workspace-options', path: 'workspace/options', label: 'all Main writable handoffs need company/store scope' }
]

console.log('Garmetix Main Back Office writable readiness')
console.log(`Version: ${version}`)
console.log(`Stage: ${stage}`)
console.log(`Mode: ${mode}`)
console.log(`API base URL: ${apiBaseUrl}`)
console.log(`Live network check: ${live ? 'enabled' : 'disabled'}`)
console.log(`Token env: ${tokenEnv}${token ? ' (set)' : ' (not set)'}`)
console.log(`Strict permissions: ${strictPermissions ? 'enabled' : 'disabled'}`)
console.log(`Take: ${take}`)
console.log('Mutation check: disabled')

for (const check of contractChecks) {
  assertRecordFields(check)
}

for (const check of endpointChecks) {
  assertIncludes(check.label, check.source, check.pattern)
}

assertPreflightPayloadGuards()

if (!live) {
  for (const check of readChecks) {
    console.log(`DRY GET ${apiBaseUrl}/${check.path} - ${check.label}`)
  }
  console.log(`DRY GET ${apiBaseUrl}/billing/customers/{customerId}/profile - only after a customer id is selected from search`)
  console.log(`DRY GET ${apiBaseUrl}/billing/sales/{invoiceId}/receipt - only after a recent invoice id is selected`)
  console.log(`DRY GET ${apiBaseUrl}/purchase/invoices/{invoiceId}/receipt - only after a recent purchase id is selected`)
  console.log('DRY POST billing/sales - intentionally not executed; POS owns fast counter billing')
  console.log('DRY POST purchase/inward - intentionally not executed in readiness stage')
  console.log('DRY POST inventory/stock-operations/adjustment - intentionally not executed in readiness stage')
  console.log('DRY POST inventory/stock-operations/transfer - intentionally not executed in readiness stage')
  console.log('DRY POST inventory/stock-operations/physical-count - intentionally not executed in readiness stage')
  console.log('DRY POST inventory/stock-operations/write-off - intentionally not executed in readiness stage')
  finish()
  process.exit(0)
}

if (requireToken && !token) {
  console.error(`Missing required token. Set ${tokenEnv} before running with --require-token.`)
  process.exit(1)
}

try {
  const health = await request('health', { auth: false })
  expectStatus('API health', health.status, [200])

  if (!token) {
    warnings.push(`${tokenEnv} is not set. Live writable-readiness endpoint checks were skipped after API health.`)
  } else {
    const me = await request('auth/me')
    expectStatus('Token auth', me.status, [200])

    const liveResults = {}
    for (const check of readChecks) {
      liveResults[check.id] = await requestJson(check.path, check.label)
    }

    const sales = toRows(liveResults['recent-sales'])
    const customers = toRows(liveResults['customer-search'])
    const purchases = toRows(liveResults['recent-purchases'])
    const stockOptions = liveResults['stock-options']
    const products = toRows(liveResults['product-lookup'])

    if (sales.length === 0) warnings.push('No recent sales returned. Sale review page can load, but receipt/PDF/cancel handoff needs an invoice row.')
    if (customers.length === 0) warnings.push('No customers returned. Customer profile handoff needs at least one customer row.')
    if (purchases.length === 0) warnings.push('No recent purchases returned. Purchase receipt/PDF/payment handoff needs an invoice row.')
    if (products.length === 0) warnings.push(`No product lookup rows returned for query ${productQuery}. Purchase intake needs products or new-product defaults.`)
    if (toRows(stockOptions?.products).length === 0) warnings.push('Stock operation options returned no product rows. Inventory adjustments need stock prerequisites.')
    if (toRows(stockOptions?.stores).length === 0) warnings.push('Stock operation options returned no stores. Inventory transfer needs destination store prerequisites.')

    const firstCustomerId = firstId(customers)
    if (firstCustomerId) {
      await requestJson(`billing/customers/${firstCustomerId}/profile`, 'customer profile handoff')
    }

    const firstSaleId = firstId(sales)
    if (firstSaleId) {
      await requestJson(`billing/sales/${firstSaleId}/receipt`, 'sale receipt handoff')
    }

    const firstPurchaseId = firstId(purchases)
    if (firstPurchaseId) {
      await requestJson(`purchase/invoices/${firstPurchaseId}/receipt`, 'purchase receipt handoff')
    }
  }
} catch (error) {
  failures.push(error instanceof Error ? error.message : 'Main Back Office writable readiness failed.')
}

finish()

function finish() {
  for (const warning of warnings) console.log(`WARN ${warning}`)

  if (failures.length > 0) {
    console.error('\nMain Back Office writable readiness failed:')
    for (const failure of failures) console.error(`- ${failure}`)
    process.exit(1)
  }

  console.log('\nMain Back Office writable readiness passed.')
}

function assertRecordFields(check) {
  const keys = parseRecordParameters(check.source, check.record)
  const missing = check.required.filter((key) => !keys.includes(key))
  if (missing.length > 0) {
    failures.push(`${check.label} is missing required field(s): ${missing.join(', ')}`)
    return
  }

  console.log(`PASS ${check.label}: required writable-readiness fields are present.`)
}

function assertIncludes(label, source, pattern) {
  if (!source.includes(pattern)) {
    failures.push(`${label}: missing backend pattern ${pattern}`)
    return
  }

  console.log(`PASS ${label}`)
}

function assertPreflightPayloadGuards() {
  const salePayload = buildSalePreflightPayload()
  const purchasePayload = buildPurchaseInwardPreflightPayload()
  const adjustmentPayload = buildStockAdjustmentPreflightPayload()
  const transferPayload = buildStockTransferPreflightPayload()

  validateSalePreflightPayload(salePayload)
  validatePurchaseInwardPreflightPayload(purchasePayload)
  validateStockAdjustmentPreflightPayload(adjustmentPayload)
  validateStockTransferPreflightPayload(transferPayload)

  console.log('PASS sale-review handoff payload guard blocks empty items, negative money and unsafe bankless non-cash payments.')
  console.log('PASS purchase-intake payload guard blocks missing vendor, empty items, invalid quantities and unsafe bankless non-cash payments.')
  console.log('PASS inventory operation payload guard blocks invalid stock ids, quantities and transfer destination.')
}

function buildSalePreflightPayload() {
  const companyId = '00000000-0000-4000-8000-000000000001'
  const storeGroupId = '00000000-0000-4000-8000-000000000002'
  const storeId = '00000000-0000-4000-8000-000000000003'
  const productId = '00000000-0000-4000-8000-000000000010'
  return {
    companyId,
    storeGroupId,
    storeId,
    customerId: null,
    salesmanId: null,
    customerName: 'Main Review Customer',
    customerMobileNumber: '',
    customerGstin: '',
    paymentMode: 0,
    bankAccountId: null,
    paidAmount: 999,
    billDiscountAmount: 0,
    items: [{ productId, barcode: 'MAIN-SALE-PREFLIGHT', quantity: 1, mrp: 999, discountAmount: 0 }],
    payments: [{ paymentMode: 0, amount: 999, bankAccountId: null }]
  }
}

function buildPurchaseInwardPreflightPayload() {
  const companyId = '00000000-0000-4000-8000-000000000001'
  const storeGroupId = '00000000-0000-4000-8000-000000000002'
  const storeId = '00000000-0000-4000-8000-000000000003'
  return {
    companyId,
    storeGroupId,
    storeId,
    vendorName: 'Main Purchase Vendor',
    vendorMobileNumber: '',
    vendorGstin: '',
    invoiceNumber: 'PI-PREFLIGHT',
    inwardNumber: null,
    paidAmount: 0,
    paymentMode: 0,
    bankAccountId: null,
    frightAmount: 0,
    items: [{
      productId: null,
      productName: 'Main Preflight Product',
      barcode: 'MAIN-PURCHASE-PREFLIGHT',
      quantity: 1,
      costPrice: 500,
      mrp: 999,
      discountAmount: 0,
      taxId: '00000000-0000-4000-8000-000000000020',
      productCategoryId: '00000000-0000-4000-8000-000000000021',
      productSubCategoryId: '00000000-0000-4000-8000-000000000022'
    }]
  }
}

function buildStockAdjustmentPreflightPayload() {
  return {
    stockId: '00000000-0000-4000-8000-000000000030',
    quantity: 1,
    direction: 'Increase',
    reason: 'Readiness preflight'
  }
}

function buildStockTransferPreflightPayload() {
  return {
    fromStockId: '00000000-0000-4000-8000-000000000030',
    toStoreId: '00000000-0000-4000-8000-000000000003',
    quantity: 1,
    reason: 'Readiness preflight'
  }
}

function validateSalePreflightPayload(payload) {
  for (const field of ['companyId', 'storeGroupId', 'storeId']) {
    if (!isGuid(payload[field])) failures.push(`Sale preflight payload has invalid ${field}.`)
  }
  if (!payload.items.length) failures.push('Sale preflight payload has no item rows.')
  for (const item of payload.items) {
    if (!isGuid(item.productId)) failures.push('Sale preflight item has invalid productId.')
    if (!item.barcode) failures.push('Sale preflight item is missing barcode.')
    if (item.quantity <= 0) failures.push('Sale preflight item quantity must be greater than zero.')
    if (item.mrp < 0 || item.discountAmount < 0) failures.push('Sale preflight item money values cannot be negative.')
  }
  validatePaymentRows('Sale preflight', payload.payments)
}

function validatePurchaseInwardPreflightPayload(payload) {
  for (const field of ['companyId', 'storeGroupId', 'storeId']) {
    if (!isGuid(payload[field])) failures.push(`Purchase preflight payload has invalid ${field}.`)
  }
  if (!String(payload.vendorName || '').trim()) failures.push('Purchase preflight payload must include a vendor name.')
  if (!payload.items.length) failures.push('Purchase preflight payload has no item rows.')
  if (payload.paidAmount < 0 || payload.frightAmount < 0) failures.push('Purchase preflight money values cannot be negative.')
  if (requiresBank(payload.paymentMode) && payload.paidAmount > 0 && !payload.bankAccountId) {
    failures.push('Purchase preflight non-cash paid amount is missing a bank account.')
  }
  for (const item of payload.items) {
    if (!item.productId && !String(item.productName || '').trim()) failures.push('Purchase preflight item must include a product id or product name.')
    if (!item.barcode) failures.push('Purchase preflight item is missing barcode.')
    if (item.quantity <= 0) failures.push('Purchase preflight item quantity must be greater than zero.')
    if (item.costPrice < 0 || item.mrp < 0 || item.discountAmount < 0) failures.push('Purchase preflight item money values cannot be negative.')
  }
}

function validateStockAdjustmentPreflightPayload(payload) {
  if (!isGuid(payload.stockId)) failures.push('Stock adjustment preflight payload has invalid stockId.')
  if (payload.quantity <= 0) failures.push('Stock adjustment preflight quantity must be greater than zero.')
  if (!['Increase', 'Decrease'].includes(payload.direction)) failures.push('Stock adjustment preflight direction must be Increase or Decrease.')
}

function validateStockTransferPreflightPayload(payload) {
  if (!isGuid(payload.fromStockId)) failures.push('Stock transfer preflight payload has invalid fromStockId.')
  if (!isGuid(payload.toStoreId)) failures.push('Stock transfer preflight payload has invalid toStoreId.')
  if (payload.quantity <= 0) failures.push('Stock transfer preflight quantity must be greater than zero.')
}

function validatePaymentRows(label, payments) {
  if (!Array.isArray(payments) || payments.length === 0) {
    failures.push(`${label} has no payment rows.`)
    return
  }

  for (const payment of payments) {
    if (payment.amount < 0) failures.push(`${label} payment amount cannot be negative.`)
    if (payment.amount > 0 && requiresBank(payment.paymentMode) && !payment.bankAccountId) {
      failures.push(`${label} non-cash payment is missing a bank account.`)
    }
  }
}

async function requestJson(path, label) {
  const response = await request(path)
  if (response.status === 200) return await response.json()

  const body = await response.text().catch(() => '')
  const suffix = body ? ` (${body.replace(/\s+/g, ' ').slice(0, 180)})` : ''
  if ([401, 403].includes(response.status) && !strictPermissions) {
    warnings.push(`${label} returned HTTP ${response.status}${suffix}. Use a Main Back Office token or --strict-permissions.`)
    return null
  }

  failures.push(`${label} expected HTTP 200 but returned HTTP ${response.status}${suffix}.`)
  return null
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

function expectStatus(label, status, expected) {
  if (!expected.includes(status)) failures.push(`${label} expected HTTP ${expected.join('/')} but returned HTTP ${status}.`)
}

function parseRecordParameters(source, recordName) {
  const match = source.match(new RegExp(`record\\s+${recordName}\\s*\\(([\\s\\S]*?)\\);`))
  if (!match) throw new Error(`Could not find backend DTO record ${recordName}.`)

  return splitTopLevelParameters(match[1])
    .map((parameter) => parameter.trim())
    .filter(Boolean)
    .map((parameter) => {
      const withoutDefault = parameter.split('=')[0]?.trim() ?? ''
      return withoutDefault.split(/\s+/).at(-1)?.replace(/\?$/, '').trim()
    })
    .filter(Boolean)
    .map(pascalToCamel)
}

function splitTopLevelParameters(value) {
  const result = []
  let current = ''
  let depth = 0
  for (const char of value) {
    if (char === '<' || char === '(') depth += 1
    if (char === '>' || char === ')') depth = Math.max(0, depth - 1)
    if (char === ',' && depth === 0) {
      result.push(current)
      current = ''
      continue
    }
    current += char
  }
  if (current.trim()) result.push(current)
  return result
}

function pascalToCamel(value) {
  return value ? value[0].toLowerCase() + value.slice(1) : value
}

function requiresBank(paymentMode) {
  return Number(paymentMode) !== 0
}

function isGuid(value) {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(String(value || ''))
}

function toRows(value) {
  if (Array.isArray(value)) return value
  if (value && typeof value === 'object') {
    for (const key of ['items', 'rows', 'data', 'results', 'products', 'stores', 'Items', 'Rows', 'Data', 'Results', 'Products', 'Stores']) {
      if (Array.isArray(value[key])) return value[key]
    }
  }
  return []
}

function firstId(rows) {
  const row = Array.isArray(rows) ? rows.find(Boolean) : null
  const id = row?.id || row?.Id || row?.customerId || row?.CustomerId || row?.invoiceId || row?.InvoiceId
  return isGuid(id) ? id : null
}
