import { writeFileSync } from 'node:fs'
import { mkdir } from 'node:fs/promises'
import { join } from 'node:path'

const COMPANY_ID = '2d337c03-fe27-4f7c-a70f-9b3a4f482c24'
const STORE_GROUP_ID = '6be6141f-a4e2-4579-abdb-232de5b00784'
const STORE_ID = '6bb60584-9c2b-4af3-ad9f-8ecce968b723'
const SERVICE_SOURCE_INVOICE = 'AF/2025/1193'
const SERVICE_TARGET_INVOICE = 'AF/2025/SI/1193'
const SERVICE_DATE = '2026-05-22'

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
const execute = process.argv.includes('--execute')
const password = process.env.GARMETIX_API_PASSWORD

if (!execute) throw new Error('Refusing live mutation without --execute.')
if (!outputDir) throw new Error('--output-dir is required.')
if (!password) throw new Error('Set GARMETIX_API_PASSWORD before running.')

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

async function findSale(invoiceNumber) {
  const result = await api('GET', 'billing/sales', undefined, {
    companyId: COMPANY_ID,
    storeId: STORE_ID,
    datePreset: 'custom',
    from: '2026-05-01',
    to: '2026-05-31',
    q: invoiceNumber,
    status: 'all',
    pageSize: 50
  })
  return rows(result).find(row => norm(row.invoiceNumber) === norm(invoiceNumber)) || null
}

async function ensureCustomer(name, mobile) {
  const customers = rows(await api('GET', 'customers'))
  const existing = customers.find(row => norm(row.name) === norm(name))
  if (existing) {
    log.push({ action: 'reuse_customer', customer: name, customerId: existing.id, mobileNumber: existing.mobileNumber })
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
    remarks: `Created for Vyapar sale service import ${SERVICE_TARGET_INVOICE}.`
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
    descriptions: `Linked product row for tailoring service invoice conversion. Source ${SERVICE_TARGET_INVOICE}.`,
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

await mkdir(outputDir, { recursive: true })
await login()

const existingTarget = await findSale(SERVICE_TARGET_INVOICE)
if (existingTarget) {
  log.push({ invoice: SERVICE_TARGET_INVOICE, action: 'skip_existing_target_invoice', invoiceId: existingTarget.id, billAmount: existingTarget.billAmount })
  writeJson('batch-10-service1193-action-log.json', log)
  console.log(JSON.stringify({ status: 'already_exists', existingTarget }, null, 2))
  process.exit(0)
}

const salesman = await firstSalesman()
const customer = await ensureCustomer('abhishek kumar', '0000001193')
const serviceItems = []
for (const service of serviceLines) serviceItems.push(await ensureServiceItem(service))
for (let index = 0; index < serviceLines.length; index++) await ensureLinkedServiceProduct(serviceItems[index], serviceLines[index])

let order = await findExistingOpenServiceOrder()
if (order) {
  log.push({ invoice: SERVICE_TARGET_INVOICE, action: 'reuse_tailoring_order', orderId: order.id, orderNumber: order.orderNumber })
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
    expectedDeliveryDate: SERVICE_DATE,
    measurementsJson: null,
    customerInstructions: 'Imported historical Vyapar tailoring service bill.',
    internalRemarks: `VyaparSourceInvoice=${SERVICE_SOURCE_INVOICE}; VyaparTargetInvoice=${SERVICE_TARGET_INVOICE}; created by Codex batch-10 approved mapping.`,
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
      expectedDeliveryDate: SERVICE_DATE,
      measurementsJson: null,
      instructions: service.description,
      vendorRemarks: null
    }))
  })
  log.push({ invoice: SERVICE_TARGET_INVOICE, action: 'create_tailoring_order', orderId: order.id, orderNumber: order.orderNumber })
}

const converted = await api('POST', `tailoring/orders/${order.id}/convert-to-service-invoice`, {
  invoiceDate: SERVICE_DATE,
  salesmanId: salesman.id,
  additionalPaidAmount: 4800,
  additionalPaymentMode: 0,
  bankAccountId: null,
  referenceNumber: `Vyapar service bill ${SERVICE_SOURCE_INVOICE}`,
  remarks: `Converted from Vyapar service bill ${SERVICE_SOURCE_INVOICE}; target invoice ${SERVICE_TARGET_INVOICE}.`
})
log.push({ invoice: SERVICE_TARGET_INVOICE, action: 'convert_to_service_invoice', invoiceId: converted.id, generatedInvoiceNumber: converted.invoiceNumber })

const updated = await api('PUT', `billing/sales/${converted.id}`, {
  invoiceNumber: SERVICE_TARGET_INVOICE,
  onDate: SERVICE_DATE,
  customerName: 'abhishek kumar',
  customerMobileNumber: customer.mobileNumber,
  customerGstin: null,
  salesmanId: salesman.id,
  remarks: `VyaparSaleImport; VyaparSourceInvoice=${SERVICE_SOURCE_INVOICE}; VyaparTargetInvoice=${SERVICE_TARGET_INVOICE}; SourceDate=${SERVICE_DATE}; Service invoice via Tailoring module; CodexBatch=batch-10.`
})
log.push({ invoice: SERVICE_TARGET_INVOICE, action: 'rename_service_invoice_to_target_number', invoiceId: converted.id, invoiceNumber: updated.invoiceNumber })

const verification = await findSale(SERVICE_TARGET_INVOICE)
writeJson('batch-10-service1193-verification.json', verification)
log.push({ action: 'verify_imported', invoice: SERVICE_TARGET_INVOICE, found: Boolean(verification), ...(verification || {}) })
writeJson('batch-10-service1193-action-log.json', log)
writeCsv('batch-10-service1193-action-log.csv', log)

const md = [
  '# Sale Import Batch 10 Service Invoice 1193 Action Log',
  '',
  `Backup taken before mutation: \`${mainBackup || 'see Backupfilehistory.md'}\`.`,
  swalekhaBackup ? `Secondary Swalekha backup: \`${swalekhaBackup}\`.` : '',
  '',
  `Source invoice: \`${SERVICE_SOURCE_INVOICE}\``,
  `Approved target invoice: \`${SERVICE_TARGET_INVOICE}\``,
  '',
  '## Actions',
  ''
].filter(Boolean)
for (const row of log) md.push(`- ${row.action}: \`${JSON.stringify(row)}\``)
md.push('', '## Verification', '', verification ? `- Found \`${verification.invoiceNumber}\`, bill amount ${verification.billAmount}, status ${verification.invoiceStatus}.` : '- NOT FOUND')
writeFileSync(join(outputDir, 'batch-10-service1193-action-log.md'), `${md.join('\n')}\n`, 'utf8')

console.log(JSON.stringify({ verification, logPath: join(outputDir, 'batch-10-service1193-action-log.md') }, null, 2))
