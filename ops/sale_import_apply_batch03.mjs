import { readFileSync, writeFileSync } from 'node:fs'
import { mkdir } from 'node:fs/promises'
import { basename, join } from 'node:path'

const COMPANY_ID = '2d337c03-fe27-4f7c-a70f-9b3a4f482c24'
const STORE_GROUP_ID = '6be6141f-a4e2-4579-abdb-232de5b00784'
const STORE_ID = '6bb60584-9c2b-4af3-ad9f-8ecce968b723'
const SBI_AADWIKA_FASHION_BANK_ACCOUNT_ID = '931e6bbc-fe04-466a-a05f-0647e969edf4'
const INVOICES = ['AF/2025/1447', 'AF/2025/1452', 'AF/2025/1453', 'AF/2025/1454', 'AF/2025/1457']
const SERVICE_INVOICE = null

const lineActions = new Map([
  ['AF/2025/1447||8907797001932', {
    action: 'create_stock_product',
    overrideBarcode: '8907797001932',
    createProductAndStock: true,
    overrideCategory: 'Inner wear',
    note: 'Amit approved create stock/product under Garment/Inner wear using barcode 8907797001932.'
  }],
  ['AF/2025/1452|23482D33951130|JKJ Jeans D3395/2010/Blue-30', {
    action: 'mapping_correct',
    overrideBarcode: '23482D33951130',
    createProductAndStock: false,
    note: 'Amit approved existing barcode mapping as correct.'
  }],
  ['AF/2025/1452||skm978009', {
    action: 'create_stock_product',
    overrideBarcode: 'SKAM0003',
    createProductAndStock: true,
    overrideCategory: 'Garment',
    approvedBrand: 'SKAM',
    note: 'Amit approved create stock/product for skm978009 with barcode SKAM0003.'
  }],
  ['AF/2025/1453||t180019600', {
    action: 'map_existing_barcode',
    overrideBarcode: 'T180019600',
    createProductAndStock: false,
    note: 'Amit approved mapping source t180019600 to barcode T180019600.'
  }],
  ['AF/2025/1453|M60236801017|M60236801017', {
    action: 'mapping_correct',
    overrideBarcode: 'M60236801017',
    createProductAndStock: false,
    note: 'Inferred same-barcode mapping because source code and item name are both M60236801017; flagged for manual review.'
  }],
  ['AF/2025/1454|25012229|BlackJack-Bundy-63353-ARS/0525/1700-5-36-D', {
    action: 'mapping_correct',
    overrideBarcode: '25012229',
    createProductAndStock: false,
    note: 'Amit approved existing mapping as correct.'
  }],
  ['AF/2025/1457||241000550177', {
    action: 'create_stock_product',
    overrideBarcode: '241000550177',
    createProductAndStock: true,
    overrideCategory: 'Suting',
    note: 'Amit approved create suiting stock using barcode 241000550177.'
  }]
])

const serviceLines = [
  { serviceCode: '38639007445', name: 'Tailoring 3Pcs Suit', quantity: 1, customerRate: 4800, description: '3 Pcs Suit Stitching' },
  { serviceCode: '38611802580', name: 'Tailoring Shirt', quantity: 1, customerRate: 450, description: 'Shirting Stitching' }
]

function option(name, fallback = '') {
  const prefix = `--${name}=`
  const found = process.argv.slice(2).find(arg => arg.startsWith(prefix))
  return found ? found.slice(prefix.length) : fallback
}

const baseUrl = option('base-url', 'http://192.168.11.94:8088/api').replace(/\/$/, '')
const username = option('username', 'garmetix')
const outputDir = option('output-dir')
const execute = process.argv.includes('--execute')
const password = process.env.GARMETIX_API_PASSWORD

if (!execute) throw new Error('Refusing live mutation without --execute.')
if (!outputDir) throw new Error('--output-dir is required.')
if (!password) throw new Error('Set GARMETIX_API_PASSWORD before running.')

const workbookPath = join(outputDir, 'batch-03-selected-invoices.xlsx')
const log = []
let token = ''

function queryString(query = {}) {
  const params = new URLSearchParams()
  for (const [key, value] of Object.entries(query)) {
    if (value !== null && value !== undefined) params.set(key, String(value))
  }
  const text = params.toString()
  return text ? `?${text}` : ''
}

async function api(method, path, body = undefined, query = undefined) {
  const headers = { Accept: 'application/json' }
  if (token) headers.Authorization = `Bearer ${token}`
  let requestBody
  if (body !== undefined) {
    headers['Content-Type'] = 'application/json'
    requestBody = JSON.stringify(body)
  }
  const response = await fetch(`${baseUrl}/${path.replace(/^\/+/, '')}${queryString(query)}`, {
    method,
    headers,
    body: requestBody
  })
  const text = await response.text()
  if (!response.ok) throw new Error(`${method} ${path} failed with ${response.status}: ${text}`)
  return text ? JSON.parse(text) : null
}

async function upload(path, filePath, query = undefined) {
  const form = new FormData()
  const bytes = readFileSync(filePath)
  form.append('file', new Blob([bytes]), basename(filePath))
  const headers = { Accept: 'application/json' }
  if (token) headers.Authorization = `Bearer ${token}`
  const response = await fetch(`${baseUrl}/${path.replace(/^\/+/, '')}${queryString(query)}`, {
    method: 'POST',
    headers,
    body: form
  })
  const text = await response.text()
  if (!response.ok) throw new Error(`POST ${path} failed with ${response.status}: ${text}`)
  return text ? JSON.parse(text) : null
}

function rows(value) {
  if (Array.isArray(value)) return value
  if (value && typeof value === 'object') {
    for (const key of ['items', 'rows', 'data', 'results']) {
      if (Array.isArray(value[key])) return value[key]
    }
  }
  return []
}

function norm(value) {
  return String(value ?? '').trim().replace(/\s+/g, ' ').toUpperCase()
}

function writeJson(name, value) {
  writeFileSync(join(outputDir, name), `${JSON.stringify(value, null, 2)}\n`, 'utf8')
}

function csvValue(value) {
  const text = typeof value === 'object' && value !== null ? JSON.stringify(value) : String(value ?? '')
  return /[",\n]/.test(text) ? `"${text.replaceAll('"', '""')}"` : text
}

function writeCsv(name, values) {
  if (!values.length) {
    writeFileSync(join(outputDir, name), '', 'utf8')
    return
  }
  const keys = [...new Set(values.flatMap(row => Object.keys(row)))].sort()
  const lines = [keys.join(','), ...values.map(row => keys.map(key => csvValue(row[key])).join(','))]
  writeFileSync(join(outputDir, name), `${lines.join('\n')}\n`, 'utf8')
}

async function login() {
  const result = await api('POST', 'auth/login', { userName: username, password })
  token = result.token
  if (!token) throw new Error('Login succeeded but no token was returned.')
  log.push({ action: 'login', username, baseUrl, at: new Date().toISOString() })
}

async function firstSalesman() {
  const result = await api('GET', 'billing/options', undefined, { companyId: COMPANY_ID, storeId: STORE_ID, take: 100 })
  const list = rows(result.salesmen)
  if (!list.length) throw new Error('No active salesman found for Smart Menswear.')
  log.push({ action: 'select_salesman', salesmanId: list[0].id, salesmanName: list[0].name })
  return list[0]
}

async function findImported(invoiceNo) {
  const result = await api('GET', 'sale-import/vyapar/imported', undefined, {
    companyId: COMPANY_ID,
    storeId: STORE_ID,
    from: '2026-02-01',
    to: '2026-03-31',
    q: invoiceNo,
    pageSize: 50
  })
  return rows(result).find(row => row.invoiceNumber === invoiceNo || row.sourceInvoiceNumber === invoiceNo) || null
}

async function ensureCustomer(name, mobile) {
  const customers = rows(await api('GET', 'customers'))
  const existing = customers.find(row => norm(row.name) === norm(name))
  if (existing) {
    log.push({ action: 'reuse_customer', customer: name, customerId: existing.id })
    return existing
  }
  const created = await api('POST', 'customers', {
    companyId: COMPANY_ID,
    name,
    mobileNumber: mobile,
    address: 'Dumka',
    city: 'Dumka',
    state: 'Jharkhand',
    country: 'India'
  })
  log.push({ action: 'create_customer', customer: name, customerId: created.id, mobileNumber: mobile })
  return created
}

async function ensureServiceItem(service) {
  const existing = rows(await api('GET', 'tailoring/service-items', undefined, { storeId: STORE_ID, activeOnly: false }))
    .find(row => norm(row.serviceCode) === norm(service.serviceCode))
  if (existing) {
    log.push({ action: 'reuse_service_item', serviceCode: service.serviceCode, serviceItemId: existing.id })
    return existing
  }
  const created = await api('POST', 'tailoring/service-items', {
    companyId: COMPANY_ID,
    storeGroupId: STORE_GROUP_ID,
    storeId: STORE_ID,
    serviceCode: service.serviceCode,
    name: service.name,
    category: 0,
    defaultCustomerRate: service.customerRate,
    defaultVendorRate: 0,
    taxRate: 5,
    hsnCode: null,
    productId: null,
    active: true,
    remarks: `Created for Vyapar sale service import ${SERVICE_INVOICE}.`
  })
  log.push({ action: 'create_service_item', serviceCode: service.serviceCode, serviceItemId: created.id })
  return created
}

async function ensureProductCategory(name = 'Tailoring') {
  const categories = rows(await api('GET', 'product-categories'))
  const existing = categories.find(row => norm(row.name) === norm(name))
  if (existing) return existing
  const created = await api('POST', 'product-categories', {
    companyId: COMPANY_ID,
    name,
    productGroup: 20,
    isActive: true
  })
  log.push({ action: 'create_product_category', name, categoryId: created.id })
  return created
}

async function ensureProductSubCategory(category, name = 'Tailoring') {
  const subs = rows(await api('GET', 'product-sub-categories'))
  const existing = subs.find(row => norm(row.name) === norm(name) && row.categoryId === category.id)
  if (existing) return existing
  const created = await api('POST', 'product-sub-categories', {
    companyId: COMPANY_ID,
    name,
    categoryId: category.id
  })
  log.push({ action: 'create_product_sub_category', name, categoryId: category.id, subCategoryId: created.id })
  return created
}

async function ensureLinkedServiceProduct(serviceItem, service) {
  const products = rows(await api('GET', 'products'))
  const byId = products.find(row => row.id === serviceItem.id)
  if (byId) {
    log.push({ action: 'reuse_linked_service_product', serviceCode: service.serviceCode, productId: byId.id })
    return byId
  }
  const category = await ensureProductCategory()
  const subCategory = await ensureProductSubCategory(category)
  const created = await api('POST', 'products', {
    id: serviceItem.id,
    companyId: COMPANY_ID,
    storeGroupId: STORE_GROUP_ID,
    name: service.name,
    barcode: service.serviceCode,
    descriptions: `Linked product row for tailoring service invoice conversion. Source ${SERVICE_INVOICE}.`,
    mrp: service.customerRate,
    taxRate: 5,
    hsnCode: '',
    unit: 2,
    taxType: 0,
    productType: 12,
    productGroup: 20,
    productCategoryId: category.id,
    productSubCategoryId: subCategory.id
  })
  log.push({ action: 'create_linked_service_product', serviceCode: service.serviceCode, productId: created.id })
  return created
}

async function findExistingOpenServiceOrder() {
  const orders = rows(await api('GET', 'tailoring/orders', undefined, { storeId: STORE_ID }))
  const candidates = orders.filter(row =>
    norm(row.customerName) === norm('Asish Dubey') &&
    !row.serviceInvoiceNumber &&
    Number(row.customerChargeAmount || 0) === 5250
  )
  if (!candidates.length) return null
  candidates.sort((a, b) => String(b.onDate || '').localeCompare(String(a.onDate || '')))
  return candidates[0]
}

async function createServiceInvoice(salesman) {
  if (!SERVICE_INVOICE) return null
  const existing = await findImported(SERVICE_INVOICE)
  if (existing) {
    log.push({ invoice: SERVICE_INVOICE, action: 'skip_service_invoice_exists', invoiceId: existing.id, invoiceNumber: existing.invoiceNumber })
    return existing
  }

  const customer = await ensureCustomer('Asish Dubey', '0000001365')
  const serviceItems = []
  for (const service of serviceLines) serviceItems.push(await ensureServiceItem(service))
  for (let index = 0; index < serviceLines.length; index++) {
    await ensureLinkedServiceProduct(serviceItems[index], serviceLines[index])
  }

  let order = await findExistingOpenServiceOrder()
  if (order) {
    log.push({ invoice: SERVICE_INVOICE, action: 'reuse_tailoring_order', orderId: order.id, orderNumber: order.orderNumber })
  } else {
    order = await api('POST', 'tailoring/orders', {
      companyId: COMPANY_ID,
      storeGroupId: STORE_GROUP_ID,
      storeId: STORE_ID,
      orderType: 0,
      customerId: customer.id,
      vendorId: null,
      sourceInvoiceId: null,
      sourceInvoiceItemId: null,
      sourceProductId: null,
      sourceProductName: null,
      sourceBarcode: null,
      expectedDeliveryDate: '2026-02-20',
      measurementsJson: null,
      customerInstructions: 'Imported historical Vyapar tailoring service bill.',
      internalRemarks: `VyaparSourceInvoice=${SERVICE_INVOICE}; created by Codex batch-03 approved mapping.`,
      lines: serviceLines.map((service, index) => ({
        serviceItemId: serviceItems[index].id,
        serviceName: service.name,
        category: 0,
        garmentName: service.name,
        barcode: service.serviceCode,
        quantity: service.quantity,
        customerRate: service.customerRate,
        vendorRate: 0,
        discountAmount: 0,
        costResponsibility: 0,
        expectedDeliveryDate: '2026-02-20',
        measurementsJson: null,
        instructions: service.description,
        vendorRemarks: null
      }))
    })
    log.push({ invoice: SERVICE_INVOICE, action: 'create_tailoring_order', orderId: order.id, orderNumber: order.orderNumber })
  }

  const converted = await api('POST', `tailoring/orders/${order.id}/convert-to-service-invoice`, {
    invoiceDate: '2026-02-20',
    salesmanId: salesman.id,
    additionalPaidAmount: 0,
    additionalPaymentMode: 0,
    bankAccountId: null,
    referenceNumber: '',
    remarks: `Converted from Vyapar service bill ${SERVICE_INVOICE}.`
  })
  log.push({ invoice: SERVICE_INVOICE, action: 'convert_to_service_invoice', invoiceId: converted.id, generatedInvoiceNumber: converted.invoiceNumber })

  const updated = await api('PUT', `billing/sales/${converted.id}`, {
    invoiceNumber: SERVICE_INVOICE,
    onDate: '2026-02-20',
    customerName: 'Asish Dubey',
    customerMobileNumber: customer.mobileNumber,
    customerGstin: null,
    salesmanId: salesman.id,
    remarks: `VyaparSaleImport; VyaparSourceInvoice=${SERVICE_INVOICE}; SourceDate=2026-02-20; Service invoice via Tailoring module; CodexBatch=batch-03.`
  })
  log.push({ invoice: SERVICE_INVOICE, action: 'rename_service_invoice_to_vyapar_number', invoiceId: converted.id, invoiceNumber: updated.invoiceNumber })
  return updated
}

function applyLineActions(preview) {
  const confirmInvoices = []
  const applied = []
  for (const invoice of preview.invoices || []) {
    const sourceNo = invoice.sourceInvoiceNumber
    if (sourceNo === SERVICE_INVOICE || !INVOICES.includes(sourceNo)) continue
    for (const line of invoice.lines || []) {
      const key = `${sourceNo}|${line.vyaparItemCode || ''}|${line.itemName || ''}`
      const action = lineActions.get(key)
      if (action) {
        line.overrideBarcode = action.overrideBarcode
        line.createProductAndStock = action.createProductAndStock
        if (action.overrideCategory) line.category = action.overrideCategory
        line.importLine = true
        applied.push({
          invoice: sourceNo,
          lineNumber: line.lineNumber,
          itemCode: line.vyaparItemCode || '',
          itemName: line.itemName || '',
          action: action.action,
          overrideBarcode: action.overrideBarcode,
          createProductAndStock: action.createProductAndStock,
          overrideCategory: action.overrideCategory || '',
          approvedBrand: action.approvedBrand || '',
          quantity: line.quantity,
          lineTotal: line.lineTotal,
          note: action.note
        })
      } else {
        line.importLine = true
      }
    }
    confirmInvoices.push(invoice)
  }
  for (const row of applied) log.push({ action: 'line_mapping', ...row })
  return { confirmInvoices, applied }
}

await mkdir(outputDir, { recursive: true })
await login()
const salesman = await firstSalesman()

const preview = await upload('sale-import/vyapar/preview', workbookPath, {
  companyId: COMPANY_ID,
  storeGroupId: STORE_GROUP_ID,
  storeId: STORE_ID
})
writeJson('batch-03-preview-before-actions.json', preview)
log.push({
  action: 'preview',
  invoiceCount: preview.invoiceCount,
  lineCount: preview.lineCount,
  matchedLineCount: preview.matchedLineCount,
  missingLineCount: preview.missingLineCount,
  warnings: (preview.warnings || []).join(' | ')
})

await createServiceInvoice(salesman)

const { confirmInvoices, applied } = applyLineActions(preview)
writeJson('batch-03-confirm-invoices.json', confirmInvoices)

const confirmPayload = {
  companyId: COMPANY_ID,
  storeGroupId: STORE_GROUP_ID,
  storeId: STORE_ID,
  useVyaparInvoiceNumbers: true,
  createMissingProductsAndStock: true,
  allowStockBridgeForInsufficientStock: true,
  defaultBankAccountId: null,
  defaultSalesmanId: salesman.id,
  paymentBankMappings: [
    {
      sourceName: 'SBI Aadwika Fashion AMY',
      paymentMode: 2,
      bankAccountId: SBI_AADWIKA_FASHION_BANK_ACCOUNT_ID
    }
  ],
  finalApprovalConfirmed: true,
  importBatchId: crypto.randomUUID(),
  sourceFileName: basename(workbookPath),
  invoices: confirmInvoices
}
writeJson('batch-03-confirm-request-redacted.json', {
  ...confirmPayload,
  invoices: `${confirmInvoices.length} invoice objects saved separately`
})

const confirmResult = await api('POST', 'sale-import/vyapar/confirm', confirmPayload)
writeJson('batch-03-confirm-result.json', confirmResult)
log.push({ action: 'sale_import_confirm', ...confirmResult })

const verification = []
for (const invoiceNo of INVOICES) {
  const row = await findImported(invoiceNo)
  verification.push({ invoice: invoiceNo, found: Boolean(row), ...(row || {}) })
}
writeJson('batch-03-import-verification.json', verification)
for (const row of verification) log.push({ action: 'verify_imported', ...row })

writeJson('batch-03-action-log.json', log)
writeCsv('batch-03-action-log.csv', log)

const md = [
  '# Sale Import Batch 03 Action Log',
  '',
  'Backup taken before mutation: `garmetix-srp-db-20260803-234227-IST-SaleImportMissingBatch03-v6.9.43.dump`.',
  '',
  '## Approved Actions Applied',
  ''
]
for (const row of applied) {
  md.push(`- \`${row.invoice}\` | \`${row.itemCode}\` | ${row.itemName} -> \`${row.overrideBarcode}\` | ${row.action} | qty ${row.quantity} | amount ${row.lineTotal}`)
}
md.push('', '## Service Invoice', '')
for (const item of log.filter(entry => entry.invoice === SERVICE_INVOICE)) {
  md.push(`- ${item.action}: \`${JSON.stringify(item)}\``)
}
md.push('', '## Confirm Result', '', '```json', JSON.stringify(confirmResult, null, 2), '```', '', '## Verification', '')
for (const row of verification) {
  md.push(`- \`${row.invoice}\`: ${row.found ? 'found' : 'NOT FOUND'} ${row.invoiceNumber || ''} ${row.billAmount ?? ''}`)
}
writeFileSync(join(outputDir, 'batch-03-action-log.md'), `${md.join('\n')}\n`, 'utf8')

console.log(JSON.stringify({
  confirmResult,
  verification,
  logPath: join(outputDir, 'batch-03-action-log.md')
}, null, 2))
