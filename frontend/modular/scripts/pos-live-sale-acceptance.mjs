import { getSmokeVersion } from './smoke-routes.mjs'

const args = process.argv.slice(2)
const hasFlag = (name) => args.includes(name)
const option = (name, fallback = '') => {
  const prefix = `${name}=`
  const match = args.find((arg) => arg.startsWith(prefix))
  return match ? match.slice(prefix.length) : fallback
}

const { version, stage } = getSmokeVersion()
const live = hasFlag('--live')
const mutateSale = hasFlag('--mutate-sale')
const confirmLiveSale = option('--confirm-live-sale')
const tokenEnv = option('--token-env', 'GARMETIX_SMOKE_AUTH_TOKEN')
const token = process.env[tokenEnv]
const backupFile = option('--backup-file', process.env.GARMETIX_SRP_LAST_BACKUP_FILE || '')
const publicBaseUrl = option('--base-url', process.env.GARMETIX_SRP_PUBLIC_BASE_URL || 'https://srp.aadwikafashion.in').replace(/\/$/, '')
const apiBaseUrl = option('--api-base-url', `${publicBaseUrl}/api`).replace(/\/$/, '')
const query = option('--query', 'shirt')
const barcode = option('--barcode')
const storeIdOption = option('--store-id')
const companyIdOption = option('--company-id')
const requireToken = hasFlag('--require-token')
const timeoutMs = Number(option('--timeout-ms', process.env.GARMETIX_POS_ACCEPTANCE_TIMEOUT_MS || '15000'))

const failures = []
const warnings = []

console.log('Garmetix POS live sale acceptance')
console.log(`Version: ${version}`)
console.log(`Stage: ${stage}`)
console.log(`Mode: ${live ? 'live' : 'dry-run'}`)
console.log(`Base URL: ${publicBaseUrl}`)
console.log(`API base URL: ${apiBaseUrl}`)
console.log(`Token env: ${tokenEnv}${token ? ' (set)' : ' (not set)'}`)
console.log(`Mutation check: ${mutateSale ? 'enabled' : 'disabled'}`)
console.log(`Backup file: ${backupFile || '(not supplied)'}`)

if (!version.startsWith('6.')) failures.push(`Expected Version6, found ${version}.`)
if (!stage.includes('Stage 14A')) failures.push(`Expected Stage 14A POS lane, found ${stage}.`)

if (!live) {
  console.log(`DRY GET ${publicBaseUrl}/pos/ - POS app shell should return HTTP 200 and load Nuxt assets`)
  console.log(`DRY GET ${apiBaseUrl}/health - API health should return HTTP 200`)
  console.log(`DRY GET ${apiBaseUrl}/auth/me - token should identify POS-capable user when ${tokenEnv} is set`)
  console.log(`DRY GET ${apiBaseUrl}/stores - select permitted store`)
  console.log(`DRY GET ${apiBaseUrl}/billing/options?companyId=<id>&storeId=<id> - verify Manager/default salesman readiness`)
  console.log(`DRY GET ${apiBaseUrl}/product-lookup?query=${encodeURIComponent(query)}&storeId=<id>&take=25 - verify sellable stock`)
  console.log(`DRY POST ${apiBaseUrl}/billing/sales - only with --live --mutate-sale --confirm-live-sale=YES --backup-file=<remote dump>`)
  finish()
}

if (requireToken && !token) failures.push(`Missing required token. Set ${tokenEnv}.`)
if (mutateSale) {
  if (!token) failures.push(`Live sale mutation requires ${tokenEnv}.`)
  if (confirmLiveSale !== 'YES') failures.push('Live sale mutation requires --confirm-live-sale=YES.')
  if (!backupFile || !backupFile.endsWith('.dump')) failures.push('Live sale mutation requires --backup-file=<remote .dump backup file>.')
}

if (failures.length === 0) {
  try {
    await checkPosShell()
    await expectStatus('API health', `${apiBaseUrl}/health`, [200], { auth: false })
    await expectStatus('Auth gate', `${apiBaseUrl}/auth/me`, [401, 403], { auth: false })

    if (token) {
      await expectStatus('Token auth', `${apiBaseUrl}/auth/me`, [200])
      const stores = await requestJson('stores')
      const store = selectStore(stores)
      if (!store) {
        failures.push('No permitted store was returned for the token.')
      } else {
        const companyId = companyIdOption || store.companyId || store.CompanyId
        const storeGroupId = store.storeGroupId || store.StoreGroupId
        const storeId = store.id || store.Id
        if (!isGuid(companyId)) failures.push('Selected store has no valid company id.')
        if (!isGuid(storeGroupId)) failures.push('Selected store has no valid store group id.')
        if (!isGuid(storeId)) failures.push('Selected store has no valid store id.')

        console.log(`CHECK selected store ${store.name || store.storeName || store.Name || storeId}`)

        const billingOptions = await requestJson(`billing/options?${new URLSearchParams({ companyId, storeId, take: '25' }).toString()}`)
        const salesmen = Array.isArray(billingOptions?.salesmen) ? billingOptions.salesmen : []
        const manager = salesmen.find((item) => String(item.name || item.Name || '').toLowerCase() === 'manager')
        if (salesmen.length === 0) warnings.push('No salesman rows returned; backend Manager fallback must remain available.')
        else console.log(`CHECK salesmen ${salesmen.length}${manager ? ' with Manager available' : ''}`)

        const product = await findSellableProduct(storeId)
        if (!product) {
          failures.push(`No sellable product was found for ${barcode ? `barcode ${barcode}` : `query ${query}`}.`)
        } else {
          console.log(`CHECK sellable product ${product.name || product.Name || product.barcode || product.Barcode}`)
          const payload = buildSalePayload({ companyId, storeGroupId, storeId, product, salesmanId: manager?.id || manager?.Id || null })
          validateSalePayload(payload)
          console.log(`CHECK sale payload ready for ${payload.items[0].barcode}, amount ${payload.paidAmount}`)

          if (mutateSale && failures.length === 0) {
            const sale = await postSale(payload)
            if (sale?.invoiceId) {
              await verifyReceiptAndPdf(sale.invoiceId)
            }
          }
        }
      }
    } else {
      warnings.push(`${tokenEnv} is not set. Authenticated POS sale prerequisites were skipped.`)
    }
  } catch (error) {
    failures.push(error instanceof Error ? error.message : 'POS live sale acceptance failed.')
  }
}

finish()

async function checkPosShell() {
  const html = await requestUrl(`${publicBaseUrl}/pos/`, { auth: false })
  if (html.status !== 200) {
    failures.push(`POS shell expected HTTP 200 but returned HTTP ${html.status}.`)
    return
  }

  const text = await html.text()
  const scriptSrc = text.match(/<script[^>]+src="([^"]+)"/)?.[1]
  const cssHref = text.match(/<link[^>]+rel="stylesheet"[^>]+href="([^"]+)"/)?.[1]
  if (!scriptSrc || !cssHref) {
    failures.push('POS shell did not include Nuxt script and stylesheet asset links.')
    return
  }
  if (!scriptSrc.startsWith('/pos/_nuxt/') || !cssHref.startsWith('/pos/_nuxt/')) {
    failures.push(`POS assets must use /pos/_nuxt/. Found ${scriptSrc} and ${cssHref}.`)
    return
  }

  await expectStatus('POS script asset', new URL(scriptSrc, publicBaseUrl).toString(), [200], { auth: false, absolute: true })
  await expectStatus('POS CSS asset', new URL(cssHref, publicBaseUrl).toString(), [200], { auth: false, absolute: true })
}

async function expectStatus(label, pathOrUrl, statuses, options = {}) {
  const response = await requestUrl(options.absolute ? pathOrUrl : pathOrUrl, { auth: options.auth ?? true })
  const ok = statuses.includes(response.status)
  const detail = ok ? '' : await readBriefBody(response)
  console.log(`CHECK ${label} -> HTTP ${response.status}`)
  if (!ok) failures.push(`${label} expected HTTP ${statuses.join('/')} but returned HTTP ${response.status}${detail ? ` (${detail})` : ''}.`)
}

async function requestJson(path, options = {}) {
  const response = await requestUrl(`${apiBaseUrl}/${path.replace(/^\//, '')}`, options)
  console.log(`CHECK GET ${path} -> HTTP ${response.status}`)
  if (response.status !== 200) {
    const body = await readBriefBody(response)
    throw new Error(`${path} expected HTTP 200 but returned HTTP ${response.status}${body ? ` (${body})` : ''}`)
  }
  return await response.json()
}

async function requestUrl(url, options = {}) {
  const controller = new AbortController()
  const timeout = setTimeout(() => controller.abort(), timeoutMs)
  try {
    return await fetch(url, {
      method: options.method || 'GET',
      cache: 'no-store',
      signal: controller.signal,
      headers: {
        ...(options.auth !== false && token ? { Authorization: `Bearer ${token}` } : {}),
        ...(options.json ? { 'Content-Type': 'application/json' } : {})
      },
      body: options.body
    })
  } finally {
    clearTimeout(timeout)
  }
}

async function readBriefBody(response) {
  const text = await response.text().catch(() => '')
  return text.replace(/\s+/g, ' ').slice(0, 180)
}

function selectStore(stores) {
  if (!Array.isArray(stores)) return null
  if (storeIdOption) {
    return stores.find((store) => String(store.id || store.Id || '').toLowerCase() === storeIdOption.toLowerCase()) || null
  }
  return stores[0] || null
}

async function findSellableProduct(storeId) {
  const path = barcode
    ? `product-lookup/barcode/${encodeURIComponent(barcode)}?${new URLSearchParams({ storeId }).toString()}`
    : `product-lookup?${new URLSearchParams({ query, storeId, take: '25' }).toString()}`
  const result = await requestJson(path)
  const rows = Array.isArray(result) ? result : [result].filter(Boolean)
  return rows.find((item) => Number(item.availableQty ?? item.AvailableQty ?? 0) > 0 && Number(item.mrp ?? item.Mrp ?? 0) >= 0) || null
}

function buildSalePayload({ companyId, storeGroupId, storeId, product, salesmanId }) {
  const mrp = Number(product.mrp ?? product.Mrp ?? 0)
  const amount = Math.max(Math.round(mrp), 0)
  return {
    companyId,
    storeGroupId,
    storeId,
    customerName: 'POS Acceptance Customer',
    customerMobileNumber: '',
    customerGstin: '',
    paymentMode: 0,
    bankAccountId: null,
    paidAmount: amount,
    billDiscountAmount: 0,
    originalInvoiceId: null,
    replacementApprovalRequested: false,
    replacementReason: null,
    remarks: `Stage 14A.3 POS live acceptance ${new Date().toISOString()}`,
    customerId: null,
    salesmanId,
    payments: [
      {
        paymentMode: 0,
        amount,
        bankAccountId: null,
        referenceNumber: null,
        gatewayReference: null,
        settlementStatus: null,
        adjustmentSourceType: null,
        adjustmentSourceId: null,
        cardLastFour: null,
        cardAuthorizationCode: null,
        cardNetwork: null,
        upiVpa: null,
        walletProvider: null,
        bankReferenceNumber: null,
        chequeNumber: null,
        chequeDate: null,
        drawerBankName: null,
        accountReference: null
      }
    ],
    items: [
      {
        productId: product.productId || product.ProductId,
        barcode: product.barcode || product.Barcode,
        quantity: 1,
        mrp,
        discountAmount: 0
      }
    ]
  }
}

function validateSalePayload(payload) {
  for (const field of ['companyId', 'storeGroupId', 'storeId']) {
    if (!isGuid(payload[field])) failures.push(`Sale payload has invalid ${field}.`)
  }
  const item = payload.items[0]
  if (!item) failures.push('Sale payload has no item.')
  if (item && !isGuid(item.productId)) failures.push('Sale item has invalid productId.')
  if (item && !item.barcode) failures.push('Sale item has no barcode.')
  if (item && item.quantity <= 0) failures.push('Sale item quantity must be greater than zero.')
  if (payload.paidAmount < 0) failures.push('Sale paid amount cannot be negative.')
}

async function postSale(payload) {
  const response = await requestUrl(`${apiBaseUrl}/billing/sales`, {
    method: 'POST',
    auth: true,
    json: true,
    body: JSON.stringify(payload)
  })
  const body = response.headers.get('content-type')?.includes('application/json')
    ? await response.json().catch(() => null)
    : null
  console.log(`CHECK POST billing/sales -> HTTP ${response.status}`)
  if (response.status !== 201 || !body?.invoiceId) {
    failures.push(`Sale create expected HTTP 201 with invoiceId but returned HTTP ${response.status}.`)
    return null
  }
  console.log(`CHECK sale created ${body.invoiceNumber || body.invoiceId}`)
  return body
}

async function verifyReceiptAndPdf(invoiceId) {
  await expectStatus('Sale receipt', `${apiBaseUrl}/billing/sales/${invoiceId}/receipt`, [200])
  const pdf = await requestUrl(`${apiBaseUrl}/billing/sales/${invoiceId}/pdf?pageSize=A4&download=true`)
  const contentType = pdf.headers.get('content-type') || ''
  console.log(`CHECK sale PDF -> HTTP ${pdf.status} ${contentType}`)
  if (pdf.status !== 200 || !contentType.toLowerCase().includes('pdf')) {
    failures.push(`Sale PDF expected HTTP 200 application/pdf but returned HTTP ${pdf.status} ${contentType}.`)
  }
}

function isGuid(value) {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(String(value || ''))
}

function finish() {
  for (const warning of warnings) console.log(`WARN ${warning}`)
  if (failures.length > 0) {
    console.error('\nPOS live sale acceptance failed:')
    for (const failure of failures) console.error(`- ${failure}`)
    process.exit(1)
  }
  console.log('\nPOS live sale acceptance passed.')
  if (!mutateSale) {
    console.log('Sale creation was not executed. Add --mutate-sale --confirm-live-sale=YES --backup-file=<remote dump> after backup and token are ready.')
  }
  process.exit(0)
}
