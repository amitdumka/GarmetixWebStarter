import { readFileSync, writeFileSync } from 'node:fs'
import { mkdir } from 'node:fs/promises'
import { basename, join } from 'node:path'

const COMPANY_ID = '2d337c03-fe27-4f7c-a70f-9b3a4f482c24'
const STORE_GROUP_ID = '6be6141f-a4e2-4579-abdb-232de5b00784'
const STORE_ID = '6bb60584-9c2b-4af3-ad9f-8ecce968b723'
const SBI_AADWIKA_FASHION_BANK_ACCOUNT_ID = '931e6bbc-fe04-466a-a05f-0647e969edf4'
const KOTAK_BANK_ACCOUNT_ID = '5ebe5a0c-e8cf-48c6-819e-a3f5aee66710'
const INVOICES = ['AF/2025/1150', 'AF/2025/1153', 'AF/2025/1156', 'AF/2025/1157']

const lineActions = new Map([
  ['AF/2025/1150||26049002', ['map_existing_barcode', '26049002', false, 'Amit previously approved mapping source 26049002 to barcode 26049002.']],
  ['AF/2025/1153|26039002|NB Jeans FV', ['approve_stock_bridge', '26039002', false, 'Amit previously approved NB Jeans FV / barcode 26039002 mapping and later stock bridge for same barcode.']],
  ['AF/2025/1156||41762-3/42/bab/d395/5pc', ['map_existing_barcode', '26058820', false, 'Amit previously approved mapping source 41762-3/42/bab/d395/5pc to barcode 26058820.']],
  ['AF/2025/1157|26039002|NB Jeans FV', ['approve_stock_bridge', '26039002', false, 'Amit previously approved NB Jeans FV / barcode 26039002 mapping and later stock bridge for same barcode.']]
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

const workbookPath = join(outputDir, 'batch-09-selected-invoices.xlsx')
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
    to: '2026-05-31',
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
    if (!INVOICES.includes(sourceNo) || invoice.hiddenBecauseAlreadyImported) continue
    for (const line of invoice.lines || []) {
      const key = `${sourceNo}|${line.vyaparItemCode || ''}|${line.itemName || ''}`
      const action = lineActions.get(key)
      if (line.reviewRequired && !action) unresolved.push({ invoice: sourceNo, lineNumber: line.lineNumber, itemCode: line.vyaparItemCode || '', itemName: line.itemName || '', reviewMessage: line.reviewMessage || '' })
    }
    if (unresolved.some(row => row.invoice === sourceNo)) continue
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
  if (unresolved.length) {
    writeJson('batch-09-unresolved-lines.json', unresolved)
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
writeJson('batch-09-preview-before-actions.json', preview)
log.push({
  action: 'preview',
  invoiceCount: preview.invoiceCount,
  lineCount: preview.lineCount,
  matchedLineCount: preview.matchedLineCount,
  missingLineCount: preview.missingLineCount,
  insufficientStockLineCount: preview.insufficientStockLineCount,
  warnings: (preview.warnings || []).join(' | ')
})

const { confirmInvoices, applied } = applyLineActions(preview)
writeJson('batch-09-confirm-invoices.json', confirmInvoices)

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
    { sourceName: 'Kotak 811', paymentMode: 2, bankAccountId: KOTAK_BANK_ACCOUNT_ID },
    { sourceName: 'Cheque', paymentMode: 7, bankAccountId: SBI_AADWIKA_FASHION_BANK_ACCOUNT_ID }
  ],
  finalApprovalConfirmed: true,
  importBatchId: crypto.randomUUID(),
  sourceFileName: basename(workbookPath),
  invoices: confirmInvoices
}
writeJson('batch-09-confirm-request-redacted.json', { ...confirmPayload, invoices: `${confirmInvoices.length} invoice objects saved separately` })

const confirmResult = await api('POST', 'sale-import/vyapar/confirm', confirmPayload)
writeJson('batch-09-confirm-result.json', confirmResult)
log.push({ action: 'sale_import_confirm', ...confirmResult })

const verification = []
for (const invoiceNo of INVOICES) {
  const row = await findImported(invoiceNo)
  verification.push({ invoice: invoiceNo, found: Boolean(row), ...(row || {}) })
}
writeJson('batch-09-import-verification.json', verification)
for (const row of verification) log.push({ action: 'verify_imported', ...row })

writeJson('batch-09-action-log.json', log)
writeCsv('batch-09-action-log.csv', log)

const md = [
  '# Sale Import Batch 09 Action Log',
  '',
  `Backup taken before mutation: \`${mainBackup || 'see Backupfilehistory.md'}\`.`,
  swalekhaBackup ? `Secondary Swalekha backup: \`${swalekhaBackup}\`.` : '',
  '',
  '## Approved Actions Applied',
  ''
].filter(Boolean)
for (const row of applied) md.push(`- \`${row.invoice}\` | \`${row.itemCode}\` | ${row.itemName} -> \`${row.overrideBarcode}\` | ${row.action} | qty ${row.quantity} | amount ${row.lineTotal}`)
md.push('', '## Confirm Result', '', '```json', JSON.stringify(confirmResult, null, 2), '```', '', '## Verification', '')
for (const row of verification) md.push(`- \`${row.invoice}\`: ${row.found ? 'found' : 'NOT FOUND'} ${row.invoiceNumber || ''} ${row.billAmount ?? ''}`)
writeFileSync(join(outputDir, 'batch-09-action-log.md'), `${md.join('\n')}\n`, 'utf8')

console.log(JSON.stringify({ confirmResult, verification, logPath: join(outputDir, 'batch-09-action-log.md') }, null, 2))
