import { readFileSync, writeFileSync } from 'node:fs'
import { mkdir } from 'node:fs/promises'
import { basename, join } from 'node:path'

const COMPANY_ID = '2d337c03-fe27-4f7c-a70f-9b3a4f482c24'
const STORE_GROUP_ID = '6be6141f-a4e2-4579-abdb-232de5b00784'
const STORE_ID = '6bb60584-9c2b-4af3-ad9f-8ecce968b723'
const SBI_AADWIKA_FASHION_BANK_ACCOUNT_ID = '931e6bbc-fe04-466a-a05f-0647e969edf4'
const KOTAK_BANK_ACCOUNT_ID = '5ebe5a0c-e8cf-48c6-819e-a3f5aee66710'
const SERVICE_INVOICE = 'AF/2025/1193'
const INVOICES = [
  'AF/2025/1179',
  'AF/2025/1180',
  'AF/2025/1181',
  'AF/2025/1186',
  'AF/2025/1187',
  'AF/2025/1188',
  SERVICE_INVOICE,
  'AF/2025/1196',
  'AF/2025/1198',
  'AF/2025/1199',
  'AF/2025/1201',
  'AF/2025/1202',
  'AF/2025/1209',
  'AF/2025/1217',
  'AF/2025/1220'
]

const lineActions = new Map([
  ['AF/2025/1181||55402-1/AMI/D595/INDO', ['map_existing_barcode', '26058827', false, 'Amit approved mapping source 55402-1/AMI/D595/INDO to barcode 26058827.']],
  ['AF/2025/1180|26039001|NB Jeans FR', ['mapping_correct', '26039001', false, 'Amit approved existing mapping as correct.']],
  ['AF/2025/1180||26049001 izarO', ['map_existing_barcode', '26049001', false, 'Amit approved mapping source 26049001 izarO to barcode 26049001.']],
  ['AF/2025/1179|26039002|NB Jeans FV', ['approve_stock_bridge', '26039002', false, 'Amit approved insufficient-stock bridge/import for barcode 26039002.']],
  ['AF/2025/1186|26039001|NB Jeans FR', ['mapping_correct', '26039001', false, 'Amit approved existing mapping as correct; repeated occurrence from item 2.']],
  ['AF/2025/1187|Anecdote|777281', ['map_existing_barcode', '777281', false, 'Amit approved mapping source Anecdote/777281 to barcode 777281.']],
  ['AF/2025/1188||26049002', ['map_existing_barcode', '26049002', false, 'Amit approved mapping source 26049002 to barcode 26049002.']],
  ['AF/2025/1196||26049002', ['map_existing_barcode', '26049002', false, 'Amit approved mapping source 26049002 to barcode 26049002; repeated occurrence from item 6.']],
  ['AF/2025/1198|26039002|NB Jeans FV', ['approve_stock_bridge', '26039002', false, 'Amit approved insufficient-stock bridge/import for barcode 26039002; repeated occurrence from item 4.']],
  ['AF/2025/1199|26039002|NB Jeans FV', ['approve_stock_bridge', '26039002', false, 'Amit approved insufficient-stock bridge/import for barcode 26039002.']],
  ['AF/2025/1202||55412-1/38/AMI/D595/SH', ['map_existing_barcode', '26058803', false, 'Amit approved mapping source 55412-1/38/AMI/D595/SH to barcode 26058803.']],
  ['AF/2025/1201||55402-1/38/AMI/D595/INDO', ['map_existing_barcode', '26058828', false, 'Amit approved mapping source 55402-1/38/AMI/D595/INDO to barcode 26058828.']],
  ['AF/2025/1209|26049005|Izaro Shirt Multi Color', ['approve_stock_bridge', '26049005', false, 'Amit approved insufficient-stock bridge/import for barcode 26049005.']],
  ['AF/2025/1209|24355BR A2192134D8|JKJ Jeans BRA219/2013/Grey-34-D5', ['mapping_correct', '24355BR A2192134D8', false, 'Amit approved existing mapping as correct.']],
  ['AF/2025/1209||26049001 izarO', ['map_existing_barcode', '26049001', false, 'Amit approved mapping source 26049001 izarO to barcode 26049001; repeated occurrence from item 3.']],
  ['AF/2025/1217||34245-1/38/ANA/D095/SH', ['map_existing_barcode', '26058810', false, 'Amit approved mapping source 34245-1/38/ANA/D095/SH to barcode 26058810.']],
  ['AF/2025/1220|38676846241|RNT Fancy Diamond', ['approve_stock_bridge', '38676846241', false, 'Amit approved insufficient-stock bridge/import for barcode 38676846241.']]
])

const serviceLines = [
  { serviceCode: '38639007445', name: 'Tailoring Suit', quantity: 1, customerRate: 4400, description: 'Historical Vyapar tailoring suit service line.' },
  { serviceCode: '38611802580', name: 'Tailoring Shirt', quantity: 1, customerRate: 400, description: 'Historical Vyapar tailoring shirt service line.' }
]

function option(name, fallback = '') {
  const prefix = `--${name}=`
  const found = process.argv.slice(2).find(arg => arg.startsWith(prefix))
  return found ? found.slice(prefix.length) : fallback
}

const baseUrl = option('base-url', 'http://192.168.11.94:8088/api').replace(/\/$/, '')
const username = option('username', 'garmetix')
const outputDir = option('output-dir')
const mainBackup = option('main-backup')
const swalekhaBackup = option('swalekha-backup')
const skipServiceConflict = process.argv.includes('--skip-service-conflict')
const execute = process.argv.includes('--execute')
const password = process.env.GARMETIX_API_PASSWORD

if (!execute) throw new Error('Refusing live mutation without --execute.')
if (!outputDir) throw new Error('--output-dir is required.')
if (!password) throw new Error('Set GARMETIX_API_PASSWORD before running.')

const workbookPath = join(outputDir, 'batch-07-selected-invoices.xlsx')
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
  const response = await fetch(`${baseUrl}/${path.replace(/^\/+/, '')}${queryString(query)}`, { method, headers, body: requestBody })
  const text = await response.text()
  if (!response.ok) throw new Error(`${method} ${path} failed with ${response.status}: ${text}`)
  return text ? JSON.parse(text) : null
}

async function upload(path, filePath, query = undefined) {
  const form = new FormData()
  form.append('file', new Blob([readFileSync(filePath)]), basename(filePath))
  const headers = { Accept: 'application/json' }
  if (token) headers.Authorization = `Bearer ${token}`
  const response = await fetch(`${baseUrl}/${path.replace(/^\/+/, '')}${queryString(query)}`, { method: 'POST', headers, body: form })
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
  if (!values.length) return writeFileSync(join(outputDir, name), '', 'utf8')
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
    from: '2026-05-01',
    to: '2026-06-30',
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
  const existing = rows(await api('GET', 'product-categories')).find(row => norm(row.name) === norm(name))
  if (existing) return existing
  const created = await api('POST', 'product-categories', { companyId: COMPANY_ID, name, productGroup: 20, isActive: true })
  log.push({ action: 'create_product_category', name, categoryId: created.id })
  return created
}

async function ensureProductSubCategory(category, name = 'Tailoring') {
  const existing = rows(await api('GET', 'product-sub-categories')).find(row => norm(row.name) === norm(name) && row.categoryId === category.id)
  if (existing) return existing
  const created = await api('POST', 'product-sub-categories', { companyId: COMPANY_ID, name, categoryId: category.id })
  log.push({ action: 'create_product_sub_category', name, categoryId: category.id, subCategoryId: created.id })
  return created
}

async function ensureLinkedServiceProduct(serviceItem, service) {
  const byId = rows(await api('GET', 'products')).find(row => row.id === serviceItem.id)
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
    norm(row.customerName) === norm('abhishek kumar') &&
    !row.serviceInvoiceNumber &&
    Number(row.customerChargeAmount || 0) === 4800
  )
  candidates.sort((a, b) => String(b.onDate || '').localeCompare(String(a.onDate || '')))
  return candidates[0] || null
}

async function createServiceInvoice(salesman) {
  if (skipServiceConflict) {
    log.push({
      invoice: SERVICE_INVOICE,
      action: 'skip_service_invoice_number_conflict',
      reason: 'AF/2025/1193 already exists as an active November 2025 sale invoice; May 2026 service invoice needs Amit decision before assigning/renaming invoice number.'
    })
    return null
  }

  const existing = await findImported(SERVICE_INVOICE)
  if (existing) {
    log.push({ invoice: SERVICE_INVOICE, action: 'skip_service_invoice_exists', invoiceId: existing.id, invoiceNumber: existing.invoiceNumber })
    return existing
  }

  const customer = await ensureCustomer('abhishek kumar', '0000001193')
  const serviceItems = []
  for (const service of serviceLines) serviceItems.push(await ensureServiceItem(service))
  for (let index = 0; index < serviceLines.length; index++) await ensureLinkedServiceProduct(serviceItems[index], serviceLines[index])

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
      expectedDeliveryDate: '2026-05-22',
      measurementsJson: null,
      customerInstructions: 'Imported historical Vyapar tailoring service bill.',
      internalRemarks: `VyaparSourceInvoice=${SERVICE_INVOICE}; created by Codex batch-07 approved mapping.`,
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
        expectedDeliveryDate: '2026-05-22',
        measurementsJson: null,
        instructions: service.description,
        vendorRemarks: null
      }))
    })
    log.push({ invoice: SERVICE_INVOICE, action: 'create_tailoring_order', orderId: order.id, orderNumber: order.orderNumber })
  }

  const converted = await api('POST', `tailoring/orders/${order.id}/convert-to-service-invoice`, {
    invoiceDate: '2026-05-22',
    salesmanId: salesman.id,
    additionalPaidAmount: 4800,
    additionalPaymentMode: 0,
    bankAccountId: null,
    referenceNumber: `Vyapar service bill ${SERVICE_INVOICE}`,
    remarks: `Converted from Vyapar service bill ${SERVICE_INVOICE}.`
  })
  log.push({ invoice: SERVICE_INVOICE, action: 'convert_to_service_invoice', invoiceId: converted.id, generatedInvoiceNumber: converted.invoiceNumber })

  const updated = await api('PUT', `billing/sales/${converted.id}`, {
    invoiceNumber: SERVICE_INVOICE,
    onDate: '2026-05-22',
    customerName: 'abhishek kumar',
    customerMobileNumber: customer.mobileNumber,
    customerGstin: null,
    salesmanId: salesman.id,
    remarks: `VyaparSaleImport; VyaparSourceInvoice=${SERVICE_INVOICE}; SourceDate=2026-05-22; Service invoice via Tailoring module; CodexBatch=batch-07.`
  })
  log.push({ invoice: SERVICE_INVOICE, action: 'rename_service_invoice_to_vyapar_number', invoiceId: converted.id, invoiceNumber: updated.invoiceNumber })
  return updated
}

function applyLineActions(preview) {
  const confirmInvoices = []
  const applied = []
  const skipped = []
  for (const invoice of preview.invoices || []) {
    const sourceNo = invoice.sourceInvoiceNumber
    if (!INVOICES.includes(sourceNo) || sourceNo === SERVICE_INVOICE || invoice.hiddenBecauseAlreadyImported) continue
    const unresolved = []
    for (const line of invoice.lines || []) {
      const key = `${sourceNo}|${line.vyaparItemCode || ''}|${line.itemName || ''}`
      const action = lineActions.get(key)
      if (line.reviewRequired && !action) unresolved.push({ invoice: sourceNo, lineNumber: line.lineNumber, itemCode: line.vyaparItemCode || '', itemName: line.itemName || '', reviewMessage: line.reviewMessage || '' })
    }
    if (unresolved.length) {
      skipped.push({ invoice: sourceNo, unresolved })
      continue
    }
    for (const line of invoice.lines || []) {
      const key = `${sourceNo}|${line.vyaparItemCode || ''}|${line.itemName || ''}`
      const action = lineActions.get(key)
      if (action) {
        const [actionName, overrideBarcode, createProductAndStock, note] = action
        line.overrideBarcode = overrideBarcode
        line.createProductAndStock = createProductAndStock
        line.importLine = true
        applied.push({
          invoice: sourceNo,
          lineNumber: line.lineNumber,
          itemCode: line.vyaparItemCode || '',
          itemName: line.itemName || '',
          action: actionName,
          overrideBarcode,
          createProductAndStock,
          quantity: line.quantity,
          lineTotal: line.lineTotal,
          note
        })
      } else {
        line.importLine = true
      }
    }
    confirmInvoices.push(invoice)
  }
  for (const row of applied) log.push({ action: 'line_mapping', ...row })
  for (const row of skipped) log.push({ action: 'skip_unresolved_invoice', ...row })
  return { confirmInvoices, applied, skipped }
}

await mkdir(outputDir, { recursive: true })
await login()
const salesman = await firstSalesman()

const preview = await upload('sale-import/vyapar/preview', workbookPath, {
  companyId: COMPANY_ID,
  storeGroupId: STORE_GROUP_ID,
  storeId: STORE_ID
})
writeJson('batch-07-preview-before-actions.json', preview)
log.push({
  action: 'preview',
  invoiceCount: preview.invoiceCount,
  lineCount: preview.lineCount,
  matchedLineCount: preview.matchedLineCount,
  missingLineCount: preview.missingLineCount,
  insufficientStockLineCount: preview.insufficientStockLineCount,
  warnings: (preview.warnings || []).join(' | ')
})

await createServiceInvoice(salesman)

const { confirmInvoices, applied, skipped } = applyLineActions(preview)
writeJson('batch-07-confirm-invoices.json', confirmInvoices)
writeJson('batch-07-skipped-unresolved.json', skipped)

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
    { sourceName: 'SBI Aadwika Fashion AMY', paymentMode: 2, bankAccountId: SBI_AADWIKA_FASHION_BANK_ACCOUNT_ID },
    { sourceName: 'Kotak 811', paymentMode: 2, bankAccountId: KOTAK_BANK_ACCOUNT_ID }
  ],
  finalApprovalConfirmed: true,
  importBatchId: crypto.randomUUID(),
  sourceFileName: basename(workbookPath),
  invoices: confirmInvoices
}
writeJson('batch-07-confirm-request-redacted.json', { ...confirmPayload, invoices: `${confirmInvoices.length} invoice objects saved separately` })

const confirmResult = confirmInvoices.length
  ? await api('POST', 'sale-import/vyapar/confirm', confirmPayload)
  : { importedInvoiceCount: 0, importedInvoiceNumbers: [], skippedInvoices: [], warnings: ['No non-service invoices were ready for import.'] }
writeJson('batch-07-confirm-result.json', confirmResult)
log.push({ action: 'sale_import_confirm', ...confirmResult })

const verification = []
for (const invoiceNo of INVOICES) {
  const row = await findImported(invoiceNo)
  verification.push({ invoice: invoiceNo, found: Boolean(row), ...(row || {}) })
}
writeJson('batch-07-import-verification.json', verification)
for (const row of verification) log.push({ action: 'verify_imported', ...row })

writeJson('batch-07-action-log.json', log)
writeCsv('batch-07-action-log.csv', log)

const md = [
  '# Sale Import Batch 07 Action Log',
  '',
  `Backup taken before mutation: \`${mainBackup || 'see Backupfilehistory.md'}\`.`,
  swalekhaBackup ? `Secondary Swalekha backup: \`${swalekhaBackup}\`.` : '',
  '',
  '## Approved Actions Applied',
  ''
].filter(Boolean)
for (const row of applied) md.push(`- \`${row.invoice}\` | \`${row.itemCode}\` | ${row.itemName} -> \`${row.overrideBarcode}\` | ${row.action} | qty ${row.quantity} | amount ${row.lineTotal}`)
md.push('', '## Service Invoice', '')
for (const item of log.filter(entry => entry.invoice === SERVICE_INVOICE)) md.push(`- ${item.action}: \`${JSON.stringify(item)}\``)
if (skipped.length) {
  md.push('', '## Skipped Unresolved', '')
  for (const row of skipped) md.push(`- \`${row.invoice}\`: ${row.unresolved.length} unresolved line(s)`)
}
md.push('', '## Confirm Result', '', '```json', JSON.stringify(confirmResult, null, 2), '```', '', '## Verification', '')
for (const row of verification) md.push(`- \`${row.invoice}\`: ${row.found ? 'found' : 'NOT FOUND'} ${row.invoiceNumber || ''} ${row.billAmount ?? ''}`)
writeFileSync(join(outputDir, 'batch-07-action-log.md'), `${md.join('\n')}\n`, 'utf8')

console.log(JSON.stringify({ confirmResult, verification, skipped, logPath: join(outputDir, 'batch-07-action-log.md') }, null, 2))
