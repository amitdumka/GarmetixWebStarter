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
const mutateReturn = hasFlag('--mutate-return')
const mutateExchange = hasFlag('--mutate-exchange')
const confirmReturn = option('--confirm-live-return')
const confirmExchange = option('--confirm-live-exchange')
const tokenEnv = option('--token-env', 'GARMETIX_SMOKE_AUTH_TOKEN')
const token = process.env[tokenEnv]
const backupFile = option('--backup-file', process.env.GARMETIX_SRP_LAST_BACKUP_FILE || '')
const publicBaseUrl = option('--base-url', process.env.GARMETIX_SRP_PUBLIC_BASE_URL || 'https://srp.aadwikafashion.in').replace(/\/$/, '')
const apiBaseUrl = option('--api-base-url', `${publicBaseUrl}/api`).replace(/\/$/, '')
const invoiceIdOption = option('--invoice-id')
const storeIdOption = option('--store-id')
const replacementBarcode = option('--replacement-barcode')
const replacementQuery = option('--replacement-query', option('--query', 'shirt'))
const timeoutMs = Number(option('--timeout-ms', process.env.GARMETIX_POS_ACCEPTANCE_TIMEOUT_MS || '15000'))

const failures = []
const warnings = []

console.log('Garmetix POS live return/exchange acceptance')
console.log(`Version: ${version}`)
console.log(`Stage: ${stage}`)
console.log(`Mode: ${live ? 'live' : 'dry-run'}`)
console.log(`Base URL: ${publicBaseUrl}`)
console.log(`API base URL: ${apiBaseUrl}`)
console.log(`Token env: ${tokenEnv}${token ? ' (set)' : ' (not set)'}`)
console.log(`Return mutation: ${mutateReturn ? 'enabled' : 'disabled'}`)
console.log(`Exchange mutation: ${mutateExchange ? 'enabled' : 'disabled'}`)
console.log(`Backup file: ${backupFile || '(not supplied)'}`)

if (!version.startsWith('6.')) failures.push(`Expected Version6, found ${version}.`)
if (!stage.includes('Stage 14A')) failures.push(`Expected Stage 14A POS lane, found ${stage}.`)
if (mutateReturn && mutateExchange) failures.push('Run return and exchange mutations separately to avoid double-changing the same invoice.')

if (!live) {
  console.log(`DRY GET ${publicBaseUrl}/pos/returns - return page should return HTTP 200 and load POS assets`)
  console.log(`DRY GET ${publicBaseUrl}/pos/exchange - exchange page should return HTTP 200 and load POS assets`)
  console.log(`DRY GET ${apiBaseUrl}/health - API health should return HTTP 200`)
  console.log(`DRY GET ${apiBaseUrl}/auth/me - token should identify POS-capable user when ${tokenEnv} is set`)
  console.log(`DRY GET ${apiBaseUrl}/billing/sales/recent?take=100 - find returnable/exchangeable invoice`)
  console.log(`DRY GET ${apiBaseUrl}/billing/sales/{invoiceId}/receipt - verify returnable item rows`)
  console.log(`DRY POST ${apiBaseUrl}/billing/sales/{invoiceId}/returns - only with --live --mutate-return --confirm-live-return=YES --backup-file=<remote dump>`)
  console.log(`DRY POST ${apiBaseUrl}/billing/sales/{invoiceId}/exchange - only with --live --mutate-exchange --confirm-live-exchange=YES --backup-file=<remote dump>`)
  finish()
}

if (mutateReturn) {
  if (!token) failures.push(`Live return mutation requires ${tokenEnv}.`)
  if (confirmReturn !== 'YES') failures.push('Live return mutation requires --confirm-live-return=YES.')
  if (!validBackupFile()) failures.push('Live return mutation requires --backup-file=<remote .dump backup file>.')
}

if (mutateExchange) {
  if (!token) failures.push(`Live exchange mutation requires ${tokenEnv}.`)
  if (confirmExchange !== 'YES') failures.push('Live exchange mutation requires --confirm-live-exchange=YES.')
  if (!validBackupFile()) failures.push('Live exchange mutation requires --backup-file=<remote .dump backup file>.')
}

if (failures.length === 0) {
  try {
    await checkPosPage('/pos/returns', 'Returns page')
    await checkPosPage('/pos/exchange', 'Exchange page')
    await expectStatus('API health', `${apiBaseUrl}/health`, [200], { auth: false })
    await expectStatus('Auth gate', `${apiBaseUrl}/auth/me`, [401, 403], { auth: false })

    if (token) {
      await expectStatus('Token auth', `${apiBaseUrl}/auth/me`, [200])
      const invoice = await selectInvoice()
      if (invoice) {
        const receipt = await requestJson(`billing/sales/${invoice.id || invoice.Id}/receipt`)
        const returnLine = firstReturnableLine(receipt)
        if (!returnLine) {
          failures.push(`Invoice ${invoice.invoiceNumber || invoice.InvoiceNumber || invoice.id} has no returnable receipt item rows.`)
        } else {
          const returnPayload = buildReturnPayload(returnLine)
          validateReturnPayload(returnPayload)
          console.log(`CHECK return payload ready for invoice ${invoice.invoiceNumber || invoice.InvoiceNumber || invoice.id}`)

          if (mutateReturn && failures.length === 0) {
            const response = await postJson(`billing/sales/${invoice.id || invoice.Id}/returns`, returnPayload, 201)
            const returnInvoiceId = response?.returnInvoiceId || response?.ReturnInvoiceId
            if (returnInvoiceId) await verifyReceiptAndPdf(returnInvoiceId, 'Return invoice')
            else failures.push('Return response did not include returnInvoiceId.')
          }

          const store = await selectStore()
          const replacement = await findReplacementProduct(store?.id || store?.Id || storeIdOption)
          if (!replacement) {
            warnings.push(`No replacement product found for ${replacementBarcode ? `barcode ${replacementBarcode}` : `query ${replacementQuery}`}; exchange mutation cannot run yet.`)
          } else {
            const exchangePayload = buildExchangePayload(returnLine, replacement)
            validateExchangePayload(exchangePayload)
            console.log(`CHECK exchange payload ready for invoice ${invoice.invoiceNumber || invoice.InvoiceNumber || invoice.id}`)

            if (mutateExchange && failures.length === 0) {
              const response = await postJson(`billing/sales/${invoice.id || invoice.Id}/exchange`, exchangePayload, 201)
              const exchangeInvoiceId = response?.exchangeInvoiceId || response?.ExchangeInvoiceId
              if (exchangeInvoiceId) await verifyReceiptAndPdf(exchangeInvoiceId, 'Exchange invoice')
              else failures.push('Exchange response did not include exchangeInvoiceId.')
            }
          }
        }
      }
    } else {
      warnings.push(`${tokenEnv} is not set. Authenticated return/exchange prerequisites were skipped.`)
    }
  } catch (error) {
    failures.push(error instanceof Error ? error.message : 'POS return/exchange acceptance failed.')
  }
}

finish()

async function checkPosPage(path, label) {
  const response = await requestUrl(`${publicBaseUrl}${path}`, { auth: false })
  console.log(`CHECK ${label} -> HTTP ${response.status}`)
  if (response.status !== 200) {
    failures.push(`${label} expected HTTP 200 but returned HTTP ${response.status}.`)
    return
  }

  const html = await response.text()
  const scriptSrc = html.match(/<script[^>]+src="([^"]+)"/)?.[1]
  const cssHref = html.match(/<link[^>]+rel="stylesheet"[^>]+href="([^"]+)"/)?.[1]
  if (!scriptSrc || !cssHref) {
    failures.push(`${label} did not include Nuxt script and stylesheet asset links.`)
    return
  }
  if (!scriptSrc.startsWith('/pos/_nuxt/') || !cssHref.startsWith('/pos/_nuxt/')) {
    failures.push(`${label} assets must use /pos/_nuxt/. Found ${scriptSrc} and ${cssHref}.`)
    return
  }
}

async function selectInvoice() {
  if (invoiceIdOption) {
    const receipt = await requestJson(`billing/sales/${invoiceIdOption}/receipt`)
    const invoice = { id: invoiceIdOption, invoiceNumber: receipt.invoiceNumber || receipt.InvoiceNumber }
    console.log(`CHECK selected invoice ${invoice.invoiceNumber || invoice.id}`)
    return invoice
  }

  const rows = await requestJson('billing/sales/recent?take=100')
  const invoices = Array.isArray(rows) ? rows : []
  const invoice = invoices.find((item) => {
    const status = String(item.invoiceStatus || item.InvoiceStatus || '').toLowerCase()
    const number = String(item.invoiceNumber || item.InvoiceNumber || '').toUpperCase()
    return !['cancelled', 'refunded'].includes(status) && !number.startsWith('SR-')
  })
  if (!invoice) {
    failures.push('No recent returnable/exchangeable invoice was found.')
    return null
  }
  console.log(`CHECK selected invoice ${invoice.invoiceNumber || invoice.InvoiceNumber || invoice.id}`)
  return invoice
}

async function selectStore() {
  const stores = await requestJson('stores').catch((error) => {
    warnings.push(`Store selection skipped: ${error.message}`)
    return []
  })
  if (!Array.isArray(stores)) return null
  if (storeIdOption) return stores.find((store) => String(store.id || store.Id || '').toLowerCase() === storeIdOption.toLowerCase()) || null
  return stores[0] || null
}

function firstReturnableLine(receipt) {
  const rows = Array.isArray(receipt?.items || receipt?.Items) ? (receipt.items || receipt.Items) : []
  return rows.find((item) => isGuid(item.id || item.Id) && Number(item.quantity || item.Quantity || 0) > 0) || null
}

function buildReturnPayload(line) {
  return {
    refundAmount: 0,
    refundPaymentMode: null,
    bankAccountId: null,
    reason: `Stage 14A.4 POS return acceptance ${new Date().toISOString()}`,
    items: [
      {
        invoiceItemId: line.id || line.Id,
        quantity: 1
      }
    ]
  }
}

function buildExchangePayload(line, replacement) {
  return {
    additionalPaidAmount: 0,
    additionalPaymentMode: null,
    bankAccountId: null,
    reason: `Stage 14A.4 POS exchange acceptance ${new Date().toISOString()}`,
    returnItems: [
      {
        invoiceItemId: line.id || line.Id,
        quantity: 1
      }
    ],
    newItems: [
      {
        productId: replacement.productId || replacement.ProductId,
        barcode: replacement.barcode || replacement.Barcode,
        quantity: 1,
        mrp: Number(replacement.mrp ?? replacement.Mrp ?? 0),
        discountAmount: 0
      }
    ]
  }
}

async function findReplacementProduct(storeId) {
  const path = replacementBarcode
    ? `product-lookup/barcode/${encodeURIComponent(replacementBarcode)}${storeId ? `?${new URLSearchParams({ storeId }).toString()}` : ''}`
    : `product-lookup?${new URLSearchParams({ query: replacementQuery, ...(storeId ? { storeId } : {}), take: '25' }).toString()}`
  const result = await requestJson(path)
  const rows = Array.isArray(result) ? result : [result].filter(Boolean)
  return rows.find((item) => Number(item.availableQty ?? item.AvailableQty ?? 0) > 0 && isGuid(item.productId || item.ProductId)) || null
}

function validateReturnPayload(payload) {
  if (payload.refundAmount < 0) failures.push('Return refund amount cannot be negative.')
  if (!payload.items.length) failures.push('Return payload has no items.')
  for (const item of payload.items) {
    if (!isGuid(item.invoiceItemId)) failures.push('Return item has invalid invoiceItemId.')
    if (item.quantity <= 0) failures.push('Return item quantity must be greater than zero.')
  }
}

function validateExchangePayload(payload) {
  if (payload.additionalPaidAmount < 0) failures.push('Exchange additional paid amount cannot be negative.')
  if (!payload.returnItems.length) failures.push('Exchange payload has no return items.')
  if (!payload.newItems.length) failures.push('Exchange payload has no replacement items.')
  for (const item of payload.returnItems) {
    if (!isGuid(item.invoiceItemId)) failures.push('Exchange return item has invalid invoiceItemId.')
    if (item.quantity <= 0) failures.push('Exchange return quantity must be greater than zero.')
  }
  for (const item of payload.newItems) {
    if (!isGuid(item.productId)) failures.push('Exchange replacement item has invalid productId.')
    if (!item.barcode) failures.push('Exchange replacement item has no barcode.')
    if (item.quantity <= 0) failures.push('Exchange replacement quantity must be greater than zero.')
  }
}

async function expectStatus(label, url, statuses, options = {}) {
  const response = await requestUrl(url, { auth: options.auth ?? true })
  console.log(`CHECK ${label} -> HTTP ${response.status}`)
  if (!statuses.includes(response.status)) {
    const detail = await readBriefBody(response)
    failures.push(`${label} expected HTTP ${statuses.join('/')} but returned HTTP ${response.status}${detail ? ` (${detail})` : ''}.`)
  }
}

async function requestJson(path) {
  const response = await requestUrl(`${apiBaseUrl}/${path.replace(/^\//, '')}`)
  console.log(`CHECK GET ${path} -> HTTP ${response.status}`)
  if (response.status !== 200) {
    const body = await readBriefBody(response)
    throw new Error(`${path} expected HTTP 200 but returned HTTP ${response.status}${body ? ` (${body})` : ''}`)
  }
  return await response.json()
}

async function postJson(path, payload, expectedStatus) {
  const response = await requestUrl(`${apiBaseUrl}/${path.replace(/^\//, '')}`, {
    method: 'POST',
    json: true,
    body: JSON.stringify(payload)
  })
  console.log(`CHECK POST ${path} -> HTTP ${response.status}`)
  const body = response.headers.get('content-type')?.includes('application/json')
    ? await response.json().catch(() => null)
    : null
  if (response.status !== expectedStatus) {
    failures.push(`${path} expected HTTP ${expectedStatus} but returned HTTP ${response.status}.`)
  }
  return body
}

async function verifyReceiptAndPdf(invoiceId, label) {
  await expectStatus(`${label} receipt`, `${apiBaseUrl}/billing/sales/${invoiceId}/receipt`, [200])
  const pdf = await requestUrl(`${apiBaseUrl}/billing/sales/${invoiceId}/pdf?pageSize=A4&download=true`)
  const contentType = pdf.headers.get('content-type') || ''
  console.log(`CHECK ${label} PDF -> HTTP ${pdf.status} ${contentType}`)
  if (pdf.status !== 200 || !contentType.toLowerCase().includes('pdf')) {
    failures.push(`${label} PDF expected HTTP 200 application/pdf but returned HTTP ${pdf.status} ${contentType}.`)
  }
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

function validBackupFile() {
  return Boolean(backupFile && backupFile.endsWith('.dump'))
}

function isGuid(value) {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(String(value || ''))
}

function finish() {
  for (const warning of warnings) console.log(`WARN ${warning}`)
  if (failures.length > 0) {
    console.error('\nPOS live return/exchange acceptance failed:')
    for (const failure of failures) console.error(`- ${failure}`)
    process.exit(1)
  }
  console.log('\nPOS live return/exchange acceptance passed.')
  if (!mutateReturn && !mutateExchange) {
    console.log('Return/exchange creation was not executed. Add guarded mutation flags after backup and token are ready.')
  }
  process.exit(0)
}
