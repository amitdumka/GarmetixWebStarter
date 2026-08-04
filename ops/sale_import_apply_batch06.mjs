import { readFileSync, writeFileSync } from 'node:fs'
import { mkdir } from 'node:fs/promises'
import { basename, join } from 'node:path'

const COMPANY_ID = '2d337c03-fe27-4f7c-a70f-9b3a4f482c24'
const STORE_GROUP_ID = '6be6141f-a4e2-4579-abdb-232de5b00784'
const STORE_ID = '6bb60584-9c2b-4af3-ad9f-8ecce968b723'
const SBI_AADWIKA_FASHION_BANK_ACCOUNT_ID = '931e6bbc-fe04-466a-a05f-0647e969edf4'
const KOTAK_BANK_ACCOUNT_ID = '5ebe5a0c-e8cf-48c6-819e-a3f5aee66710'
const INVOICES = ['AF/2025/1161', 'AF/2025/1167', 'AF/2025/1168', 'AF/2025/1170', 'AF/2025/1172', 'AF/2025/1177']

const lineActions = new Map([
  ['AF/2025/1161|26012033|SJ - Mens Mala - 7761/33', ['mapping_correct', '26012033', false, 'Amit approved existing mapping as correct.']],
  ['AF/2025/1161|26012022|SJ - Pagadi Dupatta - 7750/22', ['mapping_correct', '26012022', false, 'Amit approved existing mapping as correct.']],
  ['AF/2025/1161||55402-1/40/AMI/D595/INDO', ['map_existing_barcode', '26058829', false, 'Amit approved mapping source 55402-1/40/AMI/D595/INDO to barcode 26058829.']],
  ['AF/2025/1167|24069E159100M|Sparky-Shirt-E159/5014-Print-M', ['mapping_correct', '24069E159100M', false, 'Amit approved existing mapping as correct.']],
  ['AF/2025/1167||26049001 izarO', ['map_existing_barcode', '26049001', false, 'Amit approved mapping source 26049001 izarO to barcode 26049001.']],
  ['AF/2025/1168||66302-1/34/ARI/D195/SH', ['map_existing_barcode', '26058857', false, 'Amit approved mapping source 66302-1/34/ARI/D195/SH to barcode 26058857.']],
  ['AF/2025/1168|26012006|SJ - Pagadi - 7734/6', ['mapping_correct', '26012006', false, 'Amit approved existing mapping as correct.']],
  ['AF/2025/1170|26049001|Izaro Shirt', ['mapping_correct', '26049001', false, 'Amit approved existing mapping as correct.']],
  ['AF/2025/1170|26039002|NB Jeans FV', ['mapping_correct', '26039002', false, 'Amit approved existing mapping as correct.']],
  ['AF/2025/1170|26049005|Izaro Shirt Multi Color', ['mapping_correct', '26049005', false, 'Amit approved existing mapping as correct.']],
  ['AF/2025/1172||M50110801010', ['map_existing_barcode', 'M50110801010', false, 'Amit approved mapping source M50110801010 to barcode M50110801010.']],
  ['AF/2025/1177||26049002', ['map_existing_barcode', '26049002', false, 'Amit approved mapping source 26049002 to barcode 26049002.']],
  ['AF/2025/1177|24430D33531132|Sparky Jeans D3353/1065/Blue-32', ['mapping_correct', '24430D33531132', false, 'Amit approved existing mapping as correct.']]
])

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

const workbookPath = join(outputDir, 'batch-06-selected-invoices.xlsx')
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

function applyLineActions(preview) {
  const confirmInvoices = []
  const applied = []
  const unresolved = []
  for (const invoice of preview.invoices || []) {
    const sourceNo = invoice.sourceInvoiceNumber
    if (!INVOICES.includes(sourceNo)) continue
    for (const line of invoice.lines || []) {
      const key = `${sourceNo}|${line.vyaparItemCode || ''}|${line.itemName || ''}`
      const action = lineActions.get(key)
      if (!action && line.reviewRequired) {
        unresolved.push({ invoice: sourceNo, lineNumber: line.lineNumber, itemCode: line.vyaparItemCode || '', itemName: line.itemName || '', reviewMessage: line.reviewMessage || '' })
        continue
      }
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
  if (unresolved.length) {
    writeJson('batch-06-unresolved-lines.json', unresolved)
    throw new Error(`Refusing partial import: ${unresolved.length} unresolved reviewed lines remain.`)
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
writeJson('batch-06-preview-before-actions.json', preview)
log.push({
  action: 'preview',
  invoiceCount: preview.invoiceCount,
  lineCount: preview.lineCount,
  matchedLineCount: preview.matchedLineCount,
  missingLineCount: preview.missingLineCount,
  warnings: (preview.warnings || []).join(' | ')
})

const { confirmInvoices, applied } = applyLineActions(preview)
writeJson('batch-06-confirm-invoices.json', confirmInvoices)

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
writeJson('batch-06-confirm-request-redacted.json', { ...confirmPayload, invoices: `${confirmInvoices.length} invoice objects saved separately` })

const confirmResult = await api('POST', 'sale-import/vyapar/confirm', confirmPayload)
writeJson('batch-06-confirm-result.json', confirmResult)
log.push({ action: 'sale_import_confirm', ...confirmResult })

const verification = []
for (const invoiceNo of INVOICES) {
  const row = await findImported(invoiceNo)
  verification.push({ invoice: invoiceNo, found: Boolean(row), ...(row || {}) })
}
writeJson('batch-06-import-verification.json', verification)
for (const row of verification) log.push({ action: 'verify_imported', ...row })

writeJson('batch-06-action-log.json', log)
writeCsv('batch-06-action-log.csv', log)

const md = [
  '# Sale Import Batch 06 Action Log',
  '',
  `Backup taken before mutation: \`${mainBackup || 'see Backupfilehistory.md'}\`.`,
  swalekhaBackup ? `Secondary Swalekha backup: \`${swalekhaBackup}\`.` : '',
  '',
  '## Approved Actions Applied',
  ''
].filter(Boolean)
for (const row of applied) {
  md.push(`- \`${row.invoice}\` | \`${row.itemCode}\` | ${row.itemName} -> \`${row.overrideBarcode}\` | ${row.action} | qty ${row.quantity} | amount ${row.lineTotal}`)
}
md.push('', '## Confirm Result', '', '```json', JSON.stringify(confirmResult, null, 2), '```', '', '## Verification', '')
for (const row of verification) {
  md.push(`- \`${row.invoice}\`: ${row.found ? 'found' : 'NOT FOUND'} ${row.invoiceNumber || ''} ${row.billAmount ?? ''}`)
}
writeFileSync(join(outputDir, 'batch-06-action-log.md'), `${md.join('\n')}\n`, 'utf8')

console.log(JSON.stringify({ confirmResult, verification, logPath: join(outputDir, 'batch-06-action-log.md') }, null, 2))
