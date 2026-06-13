<script setup lang="ts">
import { h, resolveComponent } from 'vue'
import type { TableColumn } from '@nuxt/ui'

const api = useGarmetixApi()
const auth = useAuth()
const workspace = useWorkspace()
const feedback = useUiFeedback()
const documentPrint = useServerDocumentPrint()
const isAuthenticated = auth.isAuthenticated
const canEdit = auth.canEdit
const canDelete = auth.canDelete

const UBadge = resolveComponent('UBadge')
const UButton = resolveComponent('UButton')

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const sheets = ref<any[]>([])
const loading = ref(false)
const loadError = ref('')
const saving = ref(false)
const deleting = ref(false)
const calculating = ref(false)
const search = ref('')
const formOpen = ref(false)
const deleteOpen = ref(false)
const printOpen = ref(false)
const editMode = ref<'create' | 'edit'>('create')
const pendingDelete = ref<any | null>(null)
const selectedPrintSheet = ref<any | null>(null)
const preparation = ref<any | null>(null)
const reconciliationDifferences = ref<any[]>([])

const form = reactive<any>(emptySheet())

const storeOptions = computed(() => stores.value.map((store) => ({
  value: store.id,
  label: store.name || 'Store'
})))

const filteredRows = computed(() => {
  const term = search.value.trim().toLowerCase()
  if (!term) {
    return tableRows.value
  }

  return tableRows.value.filter((row) => JSON.stringify(row).toLowerCase().includes(term))
})

const latestSheet = computed(() => sheets.value
  .filter((sheet) => !workspace.storeId.value || sheet.storeId === workspace.storeId.value)
  .slice()
  .sort((a, b) => String(b.onDate).localeCompare(String(a.onDate)))[0] || null)

const cashSummary = computed(() => {
  return sheets.value.reduce((summary, sheet) => {
    summary.opening += Number(sheet.openingBalance || 0)
    summary.sales += Number(sheet.sales || 0)
    summary.receipts += Number(sheet.receipts || 0) + Number(sheet.dueReceipts || 0)
    summary.expenses += Number(sheet.expenses || 0) + Number(sheet.payments || 0)
    return summary
  }, {
    opening: 0,
    sales: 0,
    receipts: 0,
    expenses: 0,
    cashInHand: latestSheet.value ? sheetCash(latestSheet.value) : 0
  })
})

const metrics = computed(() => [
  {
    label: 'Sheets',
    value: sheets.value.length,
    meta: 'Daily cash records',
    icon: 'i-lucide-circle-dollar-sign',
    color: 'primary'
  },
  {
    label: 'Cash Sales',
    value: money(cashSummary.value.sales),
    meta: 'Recorded cash sales',
    icon: 'i-lucide-shopping-bag',
    color: 'success'
  },
  {
    label: 'Expenses',
    value: money(cashSummary.value.expenses),
    meta: 'Expenses and payments',
    icon: 'i-lucide-arrow-up-right',
    color: 'warning'
  },
  {
    label: 'Cash In Hand',
    value: money(cashSummary.value.cashInHand),
    meta: latestSheet.value ? `Latest: ${formatDate(latestSheet.value.onDate)}` : 'No cash sheet yet',
    icon: 'i-lucide-wallet',
    color: cashSummary.value.cashInHand >= 0 ? 'neutral' : 'error'
  }
])

const totalIn = computed(() => {
  return Number(form.openingBalance || 0) +
    Number(form.sales || 0) +
    Number(form.receipts || 0) +
    Number(form.dueReceipts || 0) +
    Number(form.bankWithdrawal || 0)
})

const totalOut = computed(() => {
  return Number(form.expenses || 0) +
    Number(form.payments || 0) +
    Number(form.customerDue || 0) +
    Number(form.bankDeposit || 0) +
    Number(form.nonCashSale || 0)
})

const calculatedCash = computed(() => totalIn.value - totalOut.value)

const tableRows = computed(() => sheets.value.map((sheet) => ({
  id: sheet.id,
  onDate: formatDate(sheet.onDate),
  store: storeName(sheet.storeId),
  openingBalance: money(Number(sheet.openingBalance || 0)),
  sales: money(Number(sheet.sales || 0)),
  receipts: money(Number(sheet.receipts || 0)),
  expenses: money(Number(sheet.expenses || 0)),
  payments: money(Number(sheet.payments || 0)),
  cashInHand: sheetCash(sheet),
  cashInHandText: money(sheetCash(sheet)),
  raw: sheet
})))

const columns: TableColumn<any>[] = [
  { accessorKey: 'onDate', header: 'Date' },
  { accessorKey: 'store', header: 'Store' },
  { accessorKey: 'openingBalance', header: 'Opening' },
  { accessorKey: 'sales', header: 'Sales' },
  { accessorKey: 'receipts', header: 'Receipts' },
  { accessorKey: 'expenses', header: 'Expenses' },
  { accessorKey: 'payments', header: 'Payments' },
  {
    accessorKey: 'cashInHandText',
    header: 'Cash In Hand',
    cell: ({ row }) => h(UBadge, {
      color: row.original.cashInHand >= 0 ? 'success' : 'error',
      variant: 'subtle'
    }, () => row.original.cashInHandText)
  },
  {
    id: 'actions',
    header: '',
    cell: ({ row }) => h('div', { class: 'table-action-buttons' }, [
      h(UButton, {
        color: 'neutral',
        variant: 'ghost',
        icon: 'i-lucide-printer',
        label: 'Print',
        onClick: () => openPrint(row.original.raw)
      }),
      canEdit.value ? h(UButton, {
        color: 'neutral',
        variant: 'ghost',
        icon: 'i-lucide-pencil',
        label: 'Edit',
        onClick: () => startEdit(row.original.raw)
      }) : null,
      canDelete.value ? h(UButton, {
        color: 'error',
        variant: 'ghost',
        icon: 'i-lucide-trash-2',
        label: 'Delete',
        onClick: () => askDelete(row.original.raw)
      }) : null
    ].filter(Boolean))
  }
]

function emptySheet() {
  return {
    id: '',
    storeId: '',
    onDate: localDateInput(),
    openingBalance: 0,
    sales: 0,
    receipts: 0,
    dueReceipts: 0,
    bankWithdrawal: 0,
    expenses: 0,
    payments: 0,
    customerDue: 0,
    bankDeposit: 0,
    nonCashSale: 0,
    cashInHand: 0,
    createdBy: 'AutoAdmin'
  }
}

async function refresh() {
  if (!auth.isAuthenticated.value) {
    return
  }

  loading.value = true
  loadError.value = ''
  try {
    const [companyRows, storeRows, sheetRows] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores'),
      api.get<any[]>(workspace.storeId.value
        ? `petty-cash-sheets?storeId=${encodeURIComponent(workspace.storeId.value)}`
        : 'petty-cash-sheets')
    ])

    companies.value = companyRows
    stores.value = storeRows
    sheets.value = sheetRows.sort((a, b) => String(b.onDate).localeCompare(String(a.onDate)))
  } catch (error) {
    loadError.value = feedback.cleanMessage(error instanceof Error ? error.message : 'Please check the service and try again.')
    feedback.failed('Petty cash refresh failed', error)
  } finally {
    loading.value = false
  }
}

async function startCreate() {
  editMode.value = 'create'
  Object.assign(form, emptySheet())
  form.storeId = workspace.storeId.value || stores.value[0]?.id || ''
  preparation.value = null
  reconciliationDifferences.value = []
  formOpen.value = true
  await prepareSheet()
}

function startEdit(sheet: any) {
  editMode.value = 'edit'
  Object.assign(form, {
    ...sheet,
    onDate: String(sheet.onDate || new Date().toISOString()).slice(0, 10)
  })
  preparation.value = null
  reconciliationDifferences.value = []
  formOpen.value = true
}

async function prepareSheet() {
  if (!form.storeId || !form.onDate || editMode.value !== 'create') {
    return
  }

  calculating.value = true
  try {
    const result = await api.get<any>(
      `petty-cash-sheets/prepare?storeId=${encodeURIComponent(form.storeId)}&onDate=${encodeURIComponent(form.onDate)}`
    )
    preparation.value = result
    Object.assign(form, {
      openingBalance: Number(result.openingBalance || 0),
      sales: Number(result.sales || 0),
      receipts: Number(result.receipts || 0),
      dueReceipts: Number(result.dueReceipts || 0),
      bankWithdrawal: Number(result.bankWithdrawal || 0),
      expenses: Number(result.expenses || 0),
      payments: Number(result.payments || 0),
      customerDue: Number(result.customerDue || 0),
      bankDeposit: Number(result.bankDeposit || 0),
      nonCashSale: Number(result.nonCashSale || 0),
      cashInHand: Number(result.cashInHand || 0)
    })
  } catch (error) {
    feedback.failed('Could not calculate the petty cash sheet', error)
  } finally {
    calculating.value = false
  }
}

function buildPayload() {
  if (!form.storeId) {
    throw new Error('Select store before saving petty cash.')
  }

  const payload: any = {
    storeId: form.storeId,
    onDate: `${form.onDate}T00:00:00`,
    openingBalance: Number(form.openingBalance || 0),
    sales: Number(form.sales || 0),
    receipts: Number(form.receipts || 0),
    dueReceipts: Number(form.dueReceipts || 0),
    bankWithdrawal: Number(form.bankWithdrawal || 0),
    expenses: Number(form.expenses || 0),
    payments: Number(form.payments || 0),
    customerDue: Number(form.customerDue || 0),
    bankDeposit: Number(form.bankDeposit || 0),
    nonCashSale: Number(form.nonCashSale || 0),
    cashInHand: roundMoney(calculatedCash.value),
    createdBy: String(form.createdBy || 'AutoAdmin').trim()
  }

  if (editMode.value === 'edit' && form.id) {
    payload.id = form.id
  }

  return payload
}

async function saveSheet() {
  saving.value = true
  try {
    const creating = editMode.value === 'create'
    const payload = buildPayload()
    let result: any
    if (editMode.value === 'edit' && form.id) {
      result = await api.update<any>('petty-cash-sheets', form.id, payload)
      feedback.updated('Petty cash sheet')
    } else {
      result = await api.create<any>('petty-cash-sheets', payload)
      feedback.saved('Petty cash sheet')
    }

    reconciliationDifferences.value = result?.differences || []
    selectedPrintSheet.value = result?.sheet || payload
    formOpen.value = false
    await refresh()
    printOpen.value = true
    if (creating) {
      await nextTick()
      await printSheet()
    }
  } catch (error) {
    feedback.failed('Could not save petty cash sheet', error)
  } finally {
    saving.value = false
  }
}

function openPrint(sheet: any) {
  selectedPrintSheet.value = sheet
  reconciliationDifferences.value = []
  printOpen.value = true
}

async function printSheet() {
  const sheet = selectedPrintSheet.value
  if (!sheet?.id) return
  try {
    await documentPrint.printPdf(`petty-cash-sheets/${sheet.id}/pdf`)
  } catch (error) {
    feedback.failed('Could not print petty cash PDF', error)
  }
}

async function downloadSheetPdf() {
  const sheet = selectedPrintSheet.value
  if (!sheet?.id) return
  try {
    await documentPrint.downloadPdf(`petty-cash-sheets/${sheet.id}/pdf`, `petty-cash-${String(sheet.onDate || '').slice(0, 10)}.pdf`)
    feedback.notify('Petty cash PDF downloaded')
  } catch (error) {
    feedback.failed('Could not download petty cash PDF', error)
  }
}

function buildPettyCashPrintHtml(sheet: any) {
  const store = stores.value.find((item) => item.id === sheet.storeId)
  const company = companies.value.find((item) => item.id === store?.companyId)
  const companyName = company?.name || 'Garmetix'
  const selectedStoreName = store?.name || 'Store'
  const storeCode = store?.storeCode ? `Code: ${store.storeCode}` : ''
  const storeAddress = [store?.address, store?.city, store?.state, store?.zipCode]
    .filter(Boolean)
    .join(', ')
  const incomeRows = [
    ['Opening balance', sheet.openingBalance],
    ['Cash sales', sheet.sales],
    ['Cash receipts', sheet.receipts],
    ['Due receipts', sheet.dueReceipts],
    ['Bank withdrawal', sheet.bankWithdrawal]
  ]
  const paymentRows = [
    ['Expenses', sheet.expenses],
    ['Cash payments', sheet.payments],
    ['Customer due / credit sales', sheet.customerDue],
    ['Bank deposit', sheet.bankDeposit],
    ['Non-cash sales', sheet.nonCashSale]
  ]
  const totalIncome = totalPrintIncome(sheet)
  const totalPayment = totalPrintPayment(sheet)
  const cashInHand = sheetCash(sheet)
  const printedAt = new Date().toLocaleString('en-IN', {
    dateStyle: 'medium',
    timeStyle: 'short'
  })
  const sheetReference = String(sheet.id || '').slice(0, 8).toUpperCase()

  const rowsHtml = (rows: any[][]) => rows.map(([label, value], index) => `
    <tr>
      <td class="serial">${index + 1}</td>
      <td>${escapePrintHtml(label)}</td>
      <td class="amount">${escapePrintHtml(money(Number(value || 0)))}</td>
    </tr>
  `).join('')

  return `<!doctype html>
  <html lang="en">
  <head>
    <meta charset="utf-8">
    <title>Petty Cash Sheet - ${escapePrintHtml(selectedStoreName)} - ${escapePrintHtml(formatDate(sheet.onDate))}</title>
    <style>
      @page { size: A5 landscape; margin: 6mm; }
      * {
        box-sizing: border-box;
        -webkit-print-color-adjust: exact !important;
        print-color-adjust: exact !important;
      }
      html, body {
        width: 100%;
        margin: 0;
        padding: 0;
        color: #172033;
        background: #ffffff;
        font-family: Arial, Helvetica, sans-serif;
        font-size: 9.5pt;
      }
      .sheet {
        height: 136mm;
        display: flex;
        flex-direction: column;
        border: 1.2px solid #17324d;
        background: #ffffff;
      }
      .top-band {
        height: 5px;
        background: linear-gradient(90deg, #087f5b 0 50%, #e8590c 50% 100%);
      }
      header {
        display: grid;
        grid-template-columns: 1fr auto;
        gap: 16px;
        align-items: center;
        padding: 7px 10px 6px;
        border-bottom: 1px solid #9fb0bf;
      }
      .identity {
        display: grid;
        grid-template-columns: 39px 1fr;
        gap: 8px;
        align-items: center;
      }
      .identity img {
        width: 37px;
        height: 37px;
        object-fit: contain;
      }
      .company {
        margin: 0;
        color: #0b5d4a;
        font-size: 15pt;
        line-height: 1.05;
      }
      .store-name {
        margin-top: 2px;
        color: #172033;
        font-size: 10pt;
        font-weight: 700;
      }
      .store-detail {
        margin-top: 1px;
        color: #526172;
        font-size: 7.5pt;
      }
      .document-title {
        text-align: right;
      }
      .document-title h1 {
        margin: 0;
        color: #17324d;
        font-size: 15pt;
        line-height: 1.05;
      }
      .document-title p {
        margin: 3px 0 0;
        color: #526172;
        font-size: 8pt;
      }
      .summary {
        display: grid;
        grid-template-columns: repeat(4, 1fr);
        gap: 1px;
        background: #b7c4cf;
        border-bottom: 1px solid #9fb0bf;
      }
      .summary-item {
        padding: 5px 8px;
        background: #eef4f7;
      }
      .summary-item.income { background: #dff6ea; }
      .summary-item.payment { background: #fff0e5; }
      .summary-item.balance { background: #dceeff; }
      .summary-item span {
        display: block;
        color: #5c6978;
        font-size: 6.8pt;
        font-weight: 700;
        text-transform: uppercase;
      }
      .summary-item strong {
        display: block;
        margin-top: 2px;
        color: #172033;
        font-size: 10.5pt;
      }
      .columns {
        display: grid;
        grid-template-columns: 1fr 1fr;
        gap: 7px;
        padding: 7px 8px 5px;
      }
      .panel {
        border: 1px solid #9fb0bf;
      }
      .panel h2 {
        margin: 0;
        padding: 5px 7px;
        color: #ffffff;
        font-size: 9.5pt;
        letter-spacing: 0;
      }
      .panel.income h2 { background: #087f5b; }
      .panel.payment h2 { background: #d9480f; }
      table {
        width: 100%;
        border-collapse: collapse;
        table-layout: fixed;
      }
      th, td {
        padding: 3.6px 6px;
        border-bottom: 1px solid #d7e0e7;
        line-height: 1.15;
      }
      th {
        color: #526172;
        background: #f4f7f9;
        font-size: 6.8pt;
        text-transform: uppercase;
      }
      tr:nth-child(even) td { background: #f8fafb; }
      .serial {
        width: 24px;
        color: #718096;
        text-align: center;
      }
      .amount {
        width: 105px;
        text-align: right;
        font-weight: 700;
        white-space: nowrap;
      }
      tfoot td {
        padding: 4.5px 6px;
        border-bottom: 0;
        font-size: 9pt;
        font-weight: 700;
      }
      .income tfoot td { background: #dff6ea; color: #075b42; }
      .payment tfoot td { background: #fff0e5; color: #a8380b; }
      .reconciliation {
        margin: 0 8px;
        display: grid;
        grid-template-columns: 1fr auto;
        align-items: center;
        border: 1px solid #6ea8d6;
        background: #eaf4ff;
      }
      .formula {
        padding: 5px 7px;
        color: #31465c;
        font-size: 7.5pt;
      }
      .closing {
        min-width: 180px;
        padding: 5px 9px;
        color: #ffffff;
        background: #1769aa;
        text-align: right;
      }
      .closing span {
        margin-right: 10px;
        font-size: 7pt;
        font-weight: 700;
        text-transform: uppercase;
      }
      .closing strong { font-size: 11pt; }
      .verification {
        flex: 0 0 auto;
        margin: 6px 8px 0;
        padding: 6px 8px;
        border: 1px solid #b7c4cf;
        background: #f8fafb;
      }
      .verification-title {
        color: #31465c;
        font-size: 7pt;
        font-weight: 700;
        text-transform: uppercase;
      }
      .verification-fields {
        display: grid;
        grid-template-columns: 1fr 1fr 2fr;
        gap: 14px;
        margin-top: 10px;
      }
      .verification-field {
        padding-bottom: 3px;
        border-bottom: 1px solid #617181;
        color: #667485;
        font-size: 7pt;
      }
      footer {
        flex: 1 1 auto;
        display: flex;
        flex-direction: column;
        justify-content: flex-end;
        padding: 6px 8px 5px;
      }
      .audit {
        display: flex;
        justify-content: space-between;
        color: #667485;
        font-size: 6.8pt;
      }
      .signatures {
        display: grid;
        grid-template-columns: repeat(3, 1fr);
        gap: 28px;
        margin-top: 14px;
      }
      .signature {
        padding-top: 3px;
        border-top: 1px solid #617181;
        color: #526172;
        text-align: center;
        font-size: 7pt;
      }
    </style>
  </head>
  <body>
    <main class="sheet">
      <div class="top-band"></div>
      <header>
        <div class="identity">
          <img src="${escapePrintHtml(new URL('/garmetix-logo.png', window.location.origin).href)}" alt="Garmetix">
          <div>
            <h2 class="company">${escapePrintHtml(companyName)}</h2>
            <div class="store-name">${escapePrintHtml(selectedStoreName)} ${escapePrintHtml(storeCode)}</div>
            ${storeAddress ? `<div class="store-detail">${escapePrintHtml(storeAddress)}</div>` : ''}
          </div>
        </div>
        <div class="document-title">
          <h1>Petty Cash Sheet</h1>
          <p>${escapePrintHtml(formatDate(sheet.onDate))}${sheetReference ? ` &nbsp;|&nbsp; Ref: ${escapePrintHtml(sheetReference)}` : ''}</p>
        </div>
      </header>

      <section class="summary">
        <div class="summary-item"><span>Opening balance</span><strong>${escapePrintHtml(money(Number(sheet.openingBalance || 0)))}</strong></div>
        <div class="summary-item income"><span>Total cash in</span><strong>${escapePrintHtml(money(totalIncome))}</strong></div>
        <div class="summary-item payment"><span>Total cash out</span><strong>${escapePrintHtml(money(totalPayment))}</strong></div>
        <div class="summary-item balance"><span>Cash in hand</span><strong>${escapePrintHtml(money(cashInHand))}</strong></div>
      </section>

      <section class="columns">
        <div class="panel income">
          <h2>Income / Cash In</h2>
          <table>
            <thead><tr><th class="serial">No.</th><th>Particulars</th><th class="amount">Amount</th></tr></thead>
            <tbody>${rowsHtml(incomeRows)}</tbody>
            <tfoot><tr><td colspan="2">Total Cash In</td><td class="amount">${escapePrintHtml(money(totalIncome))}</td></tr></tfoot>
          </table>
        </div>
        <div class="panel payment">
          <h2>Payment / Cash Out</h2>
          <table>
            <thead><tr><th class="serial">No.</th><th>Particulars</th><th class="amount">Amount</th></tr></thead>
            <tbody>${rowsHtml(paymentRows)}</tbody>
            <tfoot><tr><td colspan="2">Total Cash Out</td><td class="amount">${escapePrintHtml(money(totalPayment))}</td></tr></tfoot>
          </table>
        </div>
      </section>

      <section class="reconciliation">
        <div class="formula">Cash in hand = Total cash in - Total cash out</div>
        <div class="closing"><span>Closing cash</span><strong>${escapePrintHtml(money(cashInHand))}</strong></div>
      </section>

      <section class="verification">
        <div class="verification-title">Physical Cash Verification</div>
        <div class="verification-fields">
          <div class="verification-field">Cash counted</div>
          <div class="verification-field">Difference</div>
          <div class="verification-field">Remarks</div>
        </div>
      </section>

      <footer>
        <div class="audit">
          <span>Prepared by: ${escapePrintHtml(sheet.createdBy || auth.user.value?.name || 'System')}</span>
          <span>Printed: ${escapePrintHtml(printedAt)}</span>
        </div>
        <div class="signatures">
          <div class="signature">Prepared by</div>
          <div class="signature">Verified by</div>
          <div class="signature">Owner / Authorised Signatory</div>
        </div>
      </footer>
    </main>
  </body>
  </html>`
}

function escapePrintHtml(value: unknown) {
  return String(value ?? '')
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#039;')
}

function askDelete(sheet: any) {
  pendingDelete.value = sheet
  deleteOpen.value = true
}

async function confirmDelete() {
  if (!pendingDelete.value) {
    return
  }

  deleting.value = true
  try {
    await api.remove('petty-cash-sheets', pendingDelete.value.id)
    feedback.deleted('Petty cash sheet')
    deleteOpen.value = false
    pendingDelete.value = null
    await refresh()
  } catch (error) {
    feedback.failed('Could not delete petty cash sheet', error)
  } finally {
    deleting.value = false
  }
}

function storeName(storeId: string) {
  return stores.value.find((item) => item.id === storeId)?.name || 'Store'
}

function sheetCash(sheet: any) {
  return roundMoney(
    Number(sheet.openingBalance || 0) +
    Number(sheet.sales || 0) +
    Number(sheet.receipts || 0) +
    Number(sheet.dueReceipts || 0) +
    Number(sheet.bankWithdrawal || 0) -
    Number(sheet.expenses || 0) -
    Number(sheet.payments || 0) -
    Number(sheet.customerDue || 0) -
    Number(sheet.bankDeposit || 0) -
    Number(sheet.nonCashSale || 0)
  )
}

function roundMoney(value: number) {
  return Math.round((Number(value || 0) + Number.EPSILON) * 100) / 100
}

function formatDate(value: string) {
  return value ? new Date(value).toLocaleDateString() : '-'
}

function localDateInput(date = new Date()) {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

function money(value: number) {
  return new Intl.NumberFormat('en-IN', {
    style: 'currency',
    currency: 'INR',
    maximumFractionDigits: 2
  }).format(value || 0)
}

function totalPrintIncome(sheet: any) {
  return Number(sheet.openingBalance || 0) + Number(sheet.sales || 0) +
    Number(sheet.receipts || 0) + Number(sheet.dueReceipts || 0) + Number(sheet.bankWithdrawal || 0)
}

function totalPrintPayment(sheet: any) {
  return Number(sheet.expenses || 0) + Number(sheet.payments || 0) +
    Number(sheet.customerDue || 0) + Number(sheet.bankDeposit || 0) + Number(sheet.nonCashSale || 0)
}

const printIncomeRows = computed(() => {
  const sheet = selectedPrintSheet.value
  return sheet ? [
    ['Opening balance', sheet.openingBalance],
    ['Sales', sheet.sales],
    ['Receipts', sheet.receipts],
    ['Due receipts', sheet.dueReceipts],
    ['Bank withdrawal', sheet.bankWithdrawal]
  ] : []
})

const printPaymentRows = computed(() => {
  const sheet = selectedPrintSheet.value
  return sheet ? [
    ['Expenses', sheet.expenses],
    ['Payments', sheet.payments],
    ['Customer due', sheet.customerDue],
    ['Bank deposit', sheet.bankDeposit],
    ['Non-cash sale', sheet.nonCashSale]
  ] : []
})

onMounted(async () => {
  auth.restore()
  await refresh()
})

watch(calculatedCash, (value) => {
  form.cashInHand = roundMoney(value)
}, { immediate: true })

watch(() => [form.storeId, form.onDate], () => {
  if (formOpen.value && editMode.value === 'create') {
    prepareSheet()
  }
})

watch(workspace.storeId, (value, previous) => {
  if (value && value !== previous && auth.isAuthenticated.value) {
    refresh()
  }
})
</script>

<template>
  <AuthScreen v-if="!isAuthenticated" @authenticated="refresh" />

  <AppShell
    v-else
    title="Petty Cash"
    :companies="companies"
    :stores="stores"
    @refresh="refresh"
    @workspace-change="refresh"
  >
    <section class="planner-dashboard">
      <UiModulePageHeader
        title="Daily Cash Sheets"
        description="Track opening cash, sales, receipts, expenses, deposits, and calculated cash in hand."
        icon="i-lucide-circle-dollar-sign"
        primary-label="New Sheet"
        primary-icon="i-lucide-plus"
        @primary="startCreate"
      >
        <template #actions>
          <UBadge :color="loading ? 'warning' : 'success'" variant="subtle">
            {{ loading ? 'Loading' : `${sheets.length} sheets` }}
          </UBadge>
          <UButton icon="i-lucide-plus" label="New Sheet" @click="startCreate" />
        </template>
      </UiModulePageHeader>

      <div class="planner-metric-grid">
        <UCard v-for="metric in metrics" :key="metric.label" class="planner-metric-card">
          <div class="planner-metric-body">
            <UAvatar :icon="metric.icon" :color="metric.color" variant="subtle" />
            <div>
              <p>{{ metric.label }}</p>
              <strong>{{ metric.value }}</strong>
              <span>{{ metric.meta }}</span>
            </div>
          </div>
        </UCard>
      </div>

      <UiRegisterPanel
        title="Cash Register"
        :description="`${filteredRows.length} of ${sheets.length} daily cash sheets`"
        :loading="loading"
        :error="loadError"
        :empty="filteredRows.length === 0"
        :empty-title="search ? 'No matching cash sheets' : 'No petty cash sheets yet'"
        :empty-description="search ? 'Try a different store, date, amount, or cash value.' : 'Create the first daily cash sheet for a store.'"
        empty-icon="i-lucide-wallet"
        @retry="refresh"
      >
        <template #actions>
          <UiCrudToolbar
            v-model:search="search"
            search-placeholder="Search store, date, or amount"
            :loading="loading"
            refresh-label="Sync"
            create-label="New Sheet"
            @refresh="refresh"
            @create="startCreate"
          />
        </template>

        <div class="planner-table-wrap">
          <UTable :data="filteredRows" :columns="columns" />
        </div>
      </UiRegisterPanel>

      <UiFormSlideover
        v-model:open="formOpen"
        layout="modal"
        :title="editMode === 'create' ? 'New Petty Cash Sheet' : 'Edit Petty Cash Sheet'"
        description="Enter daily cash in, cash out, and calculated cash in hand."
        :submit-label="editMode === 'create' ? 'Save Sheet' : 'Update Sheet'"
        content-class="w-[calc(100vw-2rem)] sm:max-w-4xl lg:max-w-5xl"
        :loading="saving"
        @submit="saveSheet"
      >
        <UAlert
          v-if="preparation"
          color="primary"
          variant="subtle"
          icon="i-lucide-calculator"
          title="Pre-calculated from transactions"
          :description="`${preparation.openingBalanceSource}. Verify and adjust any value before saving.`"
        />
        <div class="form-action-row">
          <UButton
            v-if="editMode === 'create'"
            icon="i-lucide-refresh-cw"
            color="neutral"
            variant="outline"
            label="Recalculate"
            :loading="calculating"
            @click="prepareSheet"
          />
        </div>
        <UFormField label="Store" required>
          <USelect v-model="form.storeId" :items="storeOptions" placeholder="Select store" />
        </UFormField>
        <UFormField label="Date" required>
          <UInput v-model="form.onDate" required type="date" />
        </UFormField>

        <div class="form-two-column">
          <UFormField label="Opening balance">
            <UInput v-model="form.openingBalance" step="0.01" type="number" />
          </UFormField>
          <UFormField label="Cash sales">
            <UInput v-model="form.sales" step="0.01" type="number" />
          </UFormField>
        </div>

        <div class="form-two-column">
          <UFormField label="Receipts">
            <UInput v-model="form.receipts" step="0.01" type="number" />
          </UFormField>
          <UFormField label="Due receipts">
            <UInput v-model="form.dueReceipts" step="0.01" type="number" />
          </UFormField>
        </div>

        <UFormField label="Bank withdrawal">
          <UInput v-model="form.bankWithdrawal" step="0.01" type="number" />
        </UFormField>

        <div class="form-two-column">
          <UFormField label="Expenses">
            <UInput v-model="form.expenses" step="0.01" type="number" />
          </UFormField>
          <UFormField label="Payments">
            <UInput v-model="form.payments" step="0.01" type="number" />
          </UFormField>
        </div>

        <div class="form-two-column">
          <UFormField label="Customer due">
            <UInput v-model="form.customerDue" step="0.01" type="number" />
          </UFormField>
          <UFormField label="Bank deposit">
            <UInput v-model="form.bankDeposit" step="0.01" type="number" />
          </UFormField>
        </div>

        <div class="form-two-column">
          <UFormField label="Non-cash sale">
            <UInput v-model="form.nonCashSale" step="0.01" type="number" />
          </UFormField>
          <UFormField label="Cash in hand">
            <UInput v-model="form.cashInHand" readonly step="0.01" type="number" />
          </UFormField>
        </div>

        <div class="payroll-summary">
          <span>Total in</span><strong>{{ money(totalIn) }}</strong>
          <span>Total out</span><strong>{{ money(totalOut) }}</strong>
          <span>Calculated cash</span><strong>{{ money(calculatedCash) }}</strong>
        </div>
      </UiFormSlideover>

      <UModal v-model:open="printOpen" title="Petty Cash Sheet" :ui="{ content: 'max-w-5xl' }">
        <template #body>
          <UAlert
            v-if="reconciliationDifferences.length"
            class="petty-print-screen-only"
            color="warning"
            variant="subtle"
            icon="i-lucide-triangle-alert"
            title="Transaction values do not match"
            description="The sheet was saved and an owner alert was added to Message Logs."
          />

          <div v-if="selectedPrintSheet" class="petty-print-document">
            <header>
              <div>
                <strong>Garmetix</strong>
                <span>{{ storeName(selectedPrintSheet.storeId) }}</span>
              </div>
              <div>
                <h2>Petty Cash Sheet</h2>
                <span>{{ formatDate(selectedPrintSheet.onDate) }}</span>
              </div>
            </header>

            <div class="petty-print-columns">
              <section>
                <h3>Income / Cash In</h3>
                <div v-for="[label, value] in printIncomeRows" :key="String(label)" class="petty-print-row">
                  <span>{{ label }}</span><strong>{{ money(Number(value || 0)) }}</strong>
                </div>
                <div class="petty-print-total">
                  <span>Total Income</span><strong>{{ money(totalPrintIncome(selectedPrintSheet)) }}</strong>
                </div>
              </section>
              <section>
                <h3>Payment / Cash Out</h3>
                <div v-for="[label, value] in printPaymentRows" :key="String(label)" class="petty-print-row">
                  <span>{{ label }}</span><strong>{{ money(Number(value || 0)) }}</strong>
                </div>
                <div class="petty-print-total">
                  <span>Total Payment</span><strong>{{ money(totalPrintPayment(selectedPrintSheet)) }}</strong>
                </div>
              </section>
            </div>

            <footer>
              <div class="petty-cash-balance">
                <span>Cash in hand</span><strong>{{ money(sheetCash(selectedPrintSheet)) }}</strong>
              </div>
              <div class="petty-signatures">
                <span>Prepared by</span><span>Verified by</span><span>Owner</span>
              </div>
            </footer>
          </div>
        </template>
        <template #footer>
          <div class="modal-actions petty-print-screen-only">
            <UButton color="neutral" variant="outline" label="Close" @click="printOpen = false" />
            <UButton color="neutral" variant="subtle" icon="i-lucide-download" label="Download PDF" @click="downloadSheetPdf" />
            <UButton icon="i-lucide-printer" label="Print A5" @click="printSheet" />
          </div>
        </template>
      </UModal>

      <UiConfirmDeleteModal
        v-model:open="deleteOpen"
        title="Delete Petty Cash Sheet"
        :description="`Delete petty cash sheet for ${pendingDelete ? formatDate(pendingDelete.onDate) : ''}?`"
        :loading="deleting"
        @confirm="confirmDelete"
      />
    </section>
  </AppShell>
</template>

<style>
.form-action-row {
  display: flex;
  justify-content: flex-end;
}

.petty-print-document {
  color: #111827;
  background: white;
  border: 1px solid #94a3b8;
  padding: 18px;
  font-size: 12px;
}

.petty-print-document header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  border-bottom: 3px solid #0f766e;
  padding-bottom: 10px;
  margin-bottom: 12px;
}

.petty-print-document header > div {
  display: grid;
  gap: 2px;
}

.petty-print-document header > div:last-child {
  text-align: right;
}

.petty-print-document h2,
.petty-print-document h3 {
  margin: 0;
}

.petty-print-columns {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
}

.petty-print-columns section {
  border: 1px solid #94a3b8;
}

.petty-print-columns h3 {
  padding: 7px 9px;
  color: white;
  background: #087f5b;
}

.petty-print-columns section:last-child h3 {
  background: #d9480f;
}

.petty-print-row,
.petty-print-total {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  padding: 6px 9px;
  border-top: 1px solid #cbd5e1;
}

.petty-print-total {
  background: #ccfbf1;
  font-size: 13px;
}

.petty-print-document footer {
  margin-top: 12px;
}

.petty-cash-balance {
  display: flex;
  justify-content: space-between;
  padding: 8px 10px;
  color: white;
  background: #0f766e;
  font-size: 14px;
}

.petty-signatures {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 24px;
  margin-top: 28px;
}

.petty-signatures span {
  border-top: 1px solid #475569;
  padding-top: 4px;
  text-align: center;
}

@media print {
  .petty-print-screen-only {
    display: none !important;
  }
}
</style>
