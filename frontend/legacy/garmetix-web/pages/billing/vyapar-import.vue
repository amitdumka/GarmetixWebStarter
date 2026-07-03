<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const workspace = useWorkspace()
const feedback = useUiFeedback()
const config = useRuntimeConfig()

const file = ref<File | null>(null)
const preview = ref<any | null>(null)
const result = ref<any | null>(null)
const loading = ref(false)
const confirming = ref(false)
const bankAccounts = ref<any[]>([])
const billingOptions = ref<any | null>(null)
const defaultBankAccountId = ref('')
const defaultSalesmanId = ref('')
const useVyaparInvoiceNumbers = ref(true)
const createMissingProductsAndStock = ref(false)
const allowStockBridgeForInsufficientStock = ref(false)
const lineFilter = ref('all')
const invoiceFilter = ref('ready')
const searchText = ref('')
const paymentBankMappings = ref<Record<string, string>>({})
const mappingFile = ref<File | null>(null)
const mappingResult = ref<any | null>(null)
const pendingImportMode = ref<'allReady' | 'fullyMatched' | null>(null)
const showFinalApproval = ref(false)

const selectedStoreId = computed(() => workspace.storeId.value || '')
const selectedCompanyId = computed(() => workspace.companyId.value || '')
const selectedStoreGroupId = computed(() => workspace.storeGroupId.value || '')

const salesmen = computed(() => billingOptions.value?.salesmen || [])
const filteredLines = computed(() => {
  const lines = flattenLines()
  const q = searchText.value.trim().toLowerCase()
  return lines.filter((row: any) => {
    const statusOk = lineFilter.value === 'all' || row.matchStatus === lineFilter.value || (lineFilter.value === 'review' && row.reviewRequired)
    const queryOk = !q || [row.sourceInvoiceNumber, row.itemName, row.vyaparItemCode, row.garmetixBarcode, row.overrideBarcode, row.category]
      .some((value) => String(value || '').toLowerCase().includes(q))
    return statusOk && queryOk
  })
})
const visibleInvoices = computed(() => {
  const invoices = preview.value?.invoices || []
  return invoices.filter((invoice: any) => {
    if (invoiceFilter.value === 'all') return true
    if (invoiceFilter.value === 'ready') return invoice.readyForImport && !invoice.duplicateInvoice
    if (invoiceFilter.value === 'fullyMatched') return invoice.fullyMatched && !invoice.duplicateInvoice
    if (invoiceFilter.value === 'review') return !invoice.readyForImport || invoice.lines?.some((line: any) => line.reviewRequired)
    if (invoiceFilter.value === 'duplicate') return invoice.duplicateInvoice
    return true
  })
})
const readyInvoices = computed(() => preview.value?.invoices?.filter((invoice: any) => invoice.readyForImport && !invoice.duplicateInvoice) || [])
const fullyMatchedInvoices = computed(() => preview.value?.invoices?.filter((invoice: any) => invoice.fullyMatched && !invoice.duplicateInvoice) || [])
const missingCount = computed(() => preview.value?.missingLineCount || 0)
const canConfirm = computed(() => !!preview.value && readyInvoices.value.length > 0 && !confirming.value)
const nonCashPaymentSourceCount = computed(() => (preview.value?.paymentSources || []).filter((source: any) => source.paymentMode !== 'Cash' && source.paymentMode !== 0).length)
const mappedNonCashPaymentSourceCount = computed(() => nonCashPaymentSourceCount.value - unmappedNonCashSources().length)

onMounted(async () => {
  auth.restore()
  await loadOptions()
})

async function loadOptions() {
  try {
    bankAccounts.value = await api.list('bank-accounts')
    if (selectedCompanyId.value && selectedStoreId.value) {
      billingOptions.value = await api.get(`billing/options?companyId=${selectedCompanyId.value}&storeId=${selectedStoreId.value}&take=100`)
      if (!defaultSalesmanId.value && salesmen.value.length === 1) defaultSalesmanId.value = salesmen.value[0].id
    }
  } catch (error) {
    console.warn('Could not load import options', error)
  }
}

function onFileChange(event: Event) {
  const input = event.target as HTMLInputElement
  file.value = input.files?.[0] || null
  preview.value = null
  result.value = null
}

async function previewFile() {
  if (!file.value) {
    feedback.error('Select Vyapar sale Excel file first.')
    return
  }
  if (!selectedCompanyId.value || !selectedStoreGroupId.value || !selectedStoreId.value) {
    feedback.error('Select company/store workspace before import.')
    return
  }
  loading.value = true
  result.value = null
  try {
    const form = new FormData()
    form.append('file', file.value)
    const query = new URLSearchParams({
      companyId: selectedCompanyId.value,
      storeGroupId: selectedStoreGroupId.value,
      storeId: selectedStoreId.value
    })
    preview.value = await $fetch(`${config.public.apiBase}/sale-import/vyapar/preview?${query.toString()}`, {
      method: 'POST',
      headers: api.authHeaders(),
      body: form
    })
    paymentBankMappings.value = {}
    for (const source of preview.value?.paymentSources || []) {
      if (source.paymentMode === 'Cash' || source.paymentMode === 0) continue
      const suggested = source.bankAccountId || guessBankAccountForSource(source.sourceName)
      if (suggested) paymentBankMappings.value[source.sourceName] = suggested
    }
    feedback.success('Vyapar sale preview created. Review missing barcodes before confirm.')
  } catch (error: any) {
    feedback.error(error?.data?.message || error?.message || 'Vyapar preview failed.')
  } finally {
    loading.value = false
  }
}

async function confirmImport(mode: 'allReady' | 'fullyMatched' = 'allReady') {
  if (!preview.value) return
  const invoicesToImport = mode === 'fullyMatched' ? fullyMatchedInvoices.value : readyInvoices.value
  if (invoicesToImport.length === 0) {
    feedback.error(mode === 'fullyMatched' ? 'No fully matched invoices are available to import.' : 'No ready invoices are available to import.')
    return
  }
  if (hasUnmappedNonCashPayment()) {
    feedback.error('Map each Vyapar bank/POS/UPI column to the correct Garmetix bank account before import.')
    return
  }
  pendingImportMode.value = mode
  showFinalApproval.value = true
}

async function runFinalApprovedImport() {
  if (!preview.value || !pendingImportMode.value) return
  const invoicesToImport = pendingImportMode.value === 'fullyMatched' ? fullyMatchedInvoices.value : readyInvoices.value
  confirming.value = true
  result.value = null
  try {
    result.value = await $fetch(`${config.public.apiBase}/sale-import/vyapar/confirm`, {
      method: 'POST',
      headers: api.authHeaders(),
      body: {
        companyId: preview.value.companyId,
        storeGroupId: preview.value.storeGroupId,
        storeId: preview.value.storeId,
        useVyaparInvoiceNumbers: useVyaparInvoiceNumbers.value,
        createMissingProductsAndStock: createMissingProductsAndStock.value,
        allowStockBridgeForInsufficientStock: allowStockBridgeForInsufficientStock.value,
        defaultBankAccountId: defaultBankAccountId.value || null,
        defaultSalesmanId: defaultSalesmanId.value || null,
        invoices: invoicesToImport,
        paymentBankMappings: buildPaymentBankMappings(),
        finalApprovalConfirmed: true,
        sourceFileName: preview.value.sourceFileName || file.value?.name || null
      }
    })
    showFinalApproval.value = false
    pendingImportMode.value = null
    feedback.success(`Imported ${result.value.importedInvoiceCount || 0} Vyapar sale invoices. Batch: ${result.value.importBatchReference || result.value.importBatchId || '-'}`)
  } catch (error: any) {
    feedback.error(error?.data?.message || error?.message || 'Vyapar confirm failed.')
  } finally {
    confirming.value = false
  }
}

function cancelFinalApproval() {
  showFinalApproval.value = false
  pendingImportMode.value = null
}

function flattenLines() {
  if (!preview.value?.invoices) return []
  return visibleInvoices.value.flatMap((invoice: any) => invoice.lines.map((line: any) => ({ ...line, invoice })))
}

function hasNonCashPayment() {
  return !!preview.value?.invoices?.some((invoice: any) => invoice.payments?.some((payment: any) => Number(payment.amount || 0) > 0 && payment.paymentMode !== 0 && payment.paymentMode !== 'Cash'))
}

function hasUnmappedNonCashPayment() {
  return !!preview.value?.paymentSources?.some((source: any) => source.paymentMode !== 0 && source.paymentMode !== 'Cash' && !paymentBankMappings.value[source.sourceName])
}

function unmappedNonCashSources() {
  return (preview.value?.paymentSources || [])
    .filter((source: any) => source.paymentMode !== 0 && source.paymentMode !== 'Cash' && !paymentBankMappings.value[source.sourceName])
    .map((source: any) => source.sourceName)
}

function guessBankAccountForSource(sourceName: string) {
  const sourceKey = normalizeBankText(sourceName)
  if (!sourceKey) return ''
  const exact = bankAccounts.value.find((account: any) => {
    const labels = [account.accountHolderName, account.bankName, account.accountNumber, account.ifsc, account.upiId]
    return labels.some((value) => normalizeBankText(value) === sourceKey)
  })
  if (exact?.id) return exact.id
  const partial = bankAccounts.value.find((account: any) => {
    const label = normalizeBankText([account.accountHolderName, account.bankName, account.accountNumber, account.ifsc, account.upiId].filter(Boolean).join(' '))
    return label.includes(sourceKey) || sourceKey.includes(label)
  })
  return partial?.id || ''
}

function normalizeBankText(value: any) {
  return String(value || '')
    .toLowerCase()
    .replace(/[^a-z0-9]/g, '')
}

function buildPaymentBankMappings() {
  return (preview.value?.paymentSources || []).map((source: any) => ({
    sourceName: source.sourceName,
    paymentMode: source.paymentMode,
    bankAccountId: paymentBankMappings.value[source.sourceName] || null
  }))
}

function onMappingFileChange(event: Event) {
  const input = event.target as HTMLInputElement
  mappingFile.value = input.files?.[0] || null
  mappingResult.value = null
}

async function previewMappingUpload() {
  if (!mappingFile.value) {
    feedback.error('Select filled barcode mapping Excel/CSV first.')
    return
  }
  try {
    const form = new FormData()
    form.append('file', mappingFile.value)
    mappingResult.value = await $fetch(`${config.public.apiBase}/sale-import/vyapar/barcode-mapping/preview`, {
      method: 'POST',
      headers: api.authHeaders(),
      body: form
    })
    feedback.success(`Loaded ${mappingResult.value.mappingCount || 0} barcode mappings.`)
  } catch (error: any) {
    feedback.error(error?.data?.message || error?.message || 'Barcode mapping preview failed.')
  }
}

function applyUploadedMappings() {
  if (!preview.value || !mappingResult.value?.mappings?.length) return
  let applied = 0
  const mappings = mappingResult.value.mappings
  for (const invoice of preview.value.invoices || []) {
    for (const line of invoice.lines || []) {
      const match = mappings.find((item: any) => {
        const codeOk = item.vyaparItemCode && line.vyaparItemCode && String(item.vyaparItemCode).trim().toLowerCase() === String(line.vyaparItemCode).trim().toLowerCase()
        const nameOk = item.itemName && line.itemName && String(item.itemName).trim().toLowerCase() === String(line.itemName).trim().toLowerCase()
        return codeOk || nameOk
      })
      if (match?.garmetixBarcode) {
        line.overrideBarcode = match.garmetixBarcode
        if (line.matchStatus === 'MissingProductOrStock') {
          line.reviewRequired = false
          line.importLine = true
          line.reviewMessage = 'Mapped from uploaded barcode mapping. Re-preview after applying if you want live stock validation.'
        }
        applied++
      }
    }
    invoice.fullyMatched = (invoice.lines || []).every((line: any) => line.importLine && !line.reviewRequired && (line.matchStatus === 'Matched' || !!line.overrideBarcode))
    invoice.readyForImport = !invoice.duplicateInvoice && (invoice.lines || []).every((line: any) => line.importLine && (!line.reviewRequired || !!line.overrideBarcode || line.matchStatus === 'InsufficientStock'))
  }
  feedback.success(`Applied ${applied} barcode mappings to preview lines.`)
}

function exportMissingCsv() {
  if (!preview.value) return
  const rows = [
    ['VyaparItemCode', 'ItemName', 'Category', 'HSN', 'Size', 'Quantity', 'Amount', 'TaxRate', 'SuggestedBarcode', 'GarmetixBarcodeToFill', 'Action'],
    ...(preview.value.missingProducts || []).map((item: any) => [
      item.vyaparItemCode || '',
      item.itemName || '',
      item.category || '',
      item.hsnCode || '',
      item.size || '',
      item.quantity || 0,
      item.amount || 0,
      item.taxRate || 0,
      item.suggestedBarcode || '',
      item.garmetixBarcodeToFill || '',
      'Fill GarmetixBarcodeToFill or enable create product/stock'
    ])
  ]
  downloadCsv('vyapar-sale-missing-barcodes.csv', rows)
}

function exportPreviewCsv() {
  if (!preview.value) return
  const rows = [
    ['InvoiceNo', 'Date', 'Customer', 'VyaparItemCode', 'ItemName', 'Qty', 'Amount', 'MatchStatus', 'GarmetixBarcode', 'OverrideBarcode', 'ReviewMessage'],
    ...flattenLines().map((line: any) => [
      line.sourceInvoiceNumber,
      line.invoiceDate,
      line.partyName,
      line.vyaparItemCode || '',
      line.itemName,
      line.quantity,
      line.lineTotal,
      line.matchStatus,
      line.garmetixBarcode || '',
      line.overrideBarcode || '',
      line.reviewMessage || ''
    ])
  ]
  downloadCsv('vyapar-sale-preview.csv', rows)
}

function downloadCsv(name: string, rows: any[][]) {
  const csv = rows.map((row) => row.map((cell) => `"${String(cell ?? '').replace(/"/g, '""')}"`).join(',')).join('\n')
  const blob = new Blob([csv], { type: 'text/csv;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = name
  link.click()
  URL.revokeObjectURL(url)
}

function money(value: any) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(Number(value || 0))
}
</script>

<template>
  <AppShell title="Vyapar Sale Import" @refresh="loadOptions">
    <div class="space-y-6">
    <div class="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm dark:border-slate-800 dark:bg-slate-950">
      <div class="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
        <div>
          <p class="text-sm font-medium text-emerald-600">Sales import</p>
          <h1 class="text-2xl font-bold text-slate-900 dark:text-white">Vyapar Sale Import</h1>
          <p class="mt-1 max-w-3xl text-sm text-slate-600 dark:text-slate-300">
            Upload Vyapar Sale Report Excel. The system previews invoices, matches Item Code to Garmetix stock barcode, highlights missing products, and imports only after review.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <NuxtLink to="/billing/vyapar-imported" class="rounded-lg border px-3 py-2 text-sm font-medium hover:bg-slate-50 dark:border-slate-700 dark:hover:bg-slate-900">Imported Invoices</NuxtLink>
          <NuxtLink to="/billing/vyapar-import-batches" class="rounded-lg border px-3 py-2 text-sm font-medium hover:bg-slate-50 dark:border-slate-700 dark:hover:bg-slate-900">Import Batches / Final Summary</NuxtLink>
          <NuxtLink to="/billing" class="rounded-lg border px-3 py-2 text-sm font-medium hover:bg-slate-50 dark:border-slate-700 dark:hover:bg-slate-900">Back to Billing</NuxtLink>
        </div>
      </div>
    </div>

    <div class="grid gap-4 lg:grid-cols-3">
      <div class="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm dark:border-slate-800 dark:bg-slate-950 lg:col-span-2">
        <h2 class="text-lg font-semibold">1. Upload Vyapar Excel</h2>
        <div class="mt-4 grid gap-4 md:grid-cols-2">
          <label class="block">
            <span class="text-sm font-medium">Vyapar Sale Report .xlsx</span>
            <input class="mt-2 w-full rounded-lg border border-slate-300 p-2 text-sm dark:border-slate-700 dark:bg-slate-900" type="file" accept=".xlsx,.csv" @change="onFileChange">
          </label>
          <div class="flex items-end gap-2">
            <button class="rounded-lg bg-emerald-600 px-4 py-2 text-sm font-semibold text-white disabled:opacity-50" :disabled="loading || !file" @click="previewFile">
              {{ loading ? 'Creating preview...' : 'Preview Data' }}
            </button>
            <button v-if="preview" class="rounded-lg border px-4 py-2 text-sm font-semibold dark:border-slate-700" @click="exportPreviewCsv">Export Preview CSV</button>
          </div>
        </div>
        <p class="mt-3 text-xs text-slate-500">Expected sheets: <b>Sale Report</b> and <b>Item Details</b>. Item Code is treated as Vyapar barcode and matched to Garmetix stock barcode. Cancelled/undone previous imports will no longer auto-hide from preview; use Import Batches / Final Summary for stale import markers.</p>
      </div>

      <div class="rounded-2xl border border-amber-200 bg-amber-50 p-5 text-sm text-amber-900 dark:border-amber-900/50 dark:bg-amber-950/40 dark:text-amber-100">
        <h2 class="font-semibold">Import rule</h2>
        <p class="mt-2">Matched barcode lines can import directly. Missing lines must be linked by barcode or imported with product/stock creation enabled.</p>
      </div>
    </div>

    <div v-if="preview" class="grid gap-4 md:grid-cols-4 lg:grid-cols-7">
      <div class="rounded-xl border bg-white p-4 dark:border-slate-800 dark:bg-slate-950">
        <p class="text-xs text-slate-500">Invoices</p>
        <p class="text-2xl font-bold">{{ preview.invoiceCount }}</p>
      </div>
      <div class="rounded-xl border bg-white p-4 dark:border-slate-800 dark:bg-slate-950">
        <p class="text-xs text-slate-500">Lines</p>
        <p class="text-2xl font-bold">{{ preview.lineCount }}</p>
      </div>
      <div class="rounded-xl border bg-white p-4 dark:border-slate-800 dark:bg-slate-950">
        <p class="text-xs text-slate-500">Matched</p>
        <p class="text-2xl font-bold text-emerald-600">{{ preview.matchedLineCount }}</p>
      </div>
      <div class="rounded-xl border bg-white p-4 dark:border-slate-800 dark:bg-slate-950">
        <p class="text-xs text-slate-500">Missing</p>
        <p class="text-2xl font-bold text-amber-600">{{ preview.missingLineCount }}</p>
      </div>
      <div class="rounded-xl border bg-white p-4 dark:border-slate-800 dark:bg-slate-950">
        <p class="text-xs text-slate-500">Total</p>
        <p class="text-xl font-bold">{{ money(preview.invoiceAmountTotal) }}</p>
      </div>
      <div class="rounded-xl border bg-white p-4 dark:border-slate-800 dark:bg-slate-950">
        <p class="text-xs text-slate-500">Fully matched invoices</p>
        <p class="text-2xl font-bold text-emerald-600">{{ preview.fullyMatchedInvoiceCount || 0 }}</p>
      </div>
      <div class="rounded-xl border bg-white p-4 dark:border-slate-800 dark:bg-slate-950">
        <p class="text-xs text-slate-500">Auto-hidden old imports</p>
        <p class="text-2xl font-bold text-slate-700 dark:text-slate-200">{{ preview.autoHiddenExistingInvoiceCount || 0 }}</p>
      </div>
    </div>

    <div v-if="preview?.warnings?.length" class="rounded-2xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-900 dark:border-amber-900/60 dark:bg-amber-950/40 dark:text-amber-100">
      <p v-for="warning in preview.warnings" :key="warning">• {{ warning }}</p>
    </div>

    <div v-if="preview" class="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm dark:border-slate-800 dark:bg-slate-950">
      <div class="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
        <div>
          <h2 class="text-lg font-semibold">2. Review barcode matches</h2>
          <p class="text-sm text-slate-500">Fill Override Barcode where Item Code does not match Garmetix stock.</p>
        </div>
        <div class="flex flex-wrap gap-2">
          <button class="rounded-lg border px-3 py-2 text-sm font-semibold dark:border-slate-700" @click="exportMissingCsv">Export Missing Barcode CSV</button>
          <select v-model="invoiceFilter" class="rounded-lg border px-3 py-2 text-sm dark:border-slate-700 dark:bg-slate-900">
            <option value="ready">Ready invoices</option>
            <option value="fullyMatched">Fully matched invoices</option>
            <option value="review">Needs review invoices</option>
            <option value="duplicate">Duplicate invoices</option>
            <option value="all">All current preview</option>
          </select>
          <select v-model="lineFilter" class="rounded-lg border px-3 py-2 text-sm dark:border-slate-700 dark:bg-slate-900">
            <option value="all">All lines</option>
            <option value="Matched">Matched</option>
            <option value="MissingProductOrStock">Missing</option>
            <option value="InsufficientStock">Insufficient stock</option>
            <option value="review">Needs review</option>
          </select>
          <input v-model="searchText" class="rounded-lg border px-3 py-2 text-sm dark:border-slate-700 dark:bg-slate-900" placeholder="Search item/invoice/barcode">
        </div>
      </div>

      <div class="mt-4 max-h-[560px] overflow-auto rounded-xl border dark:border-slate-800">
        <table class="min-w-full divide-y divide-slate-200 text-sm dark:divide-slate-800">
          <thead class="sticky top-0 bg-slate-50 dark:bg-slate-900">
            <tr class="text-left text-xs uppercase tracking-wide text-slate-500">
              <th class="p-2">Import</th>
              <th class="p-2">Invoice</th>
              <th class="p-2">Item</th>
              <th class="p-2">Vyapar Code</th>
              <th class="p-2">Garmetix Barcode</th>
              <th class="p-2">Override Barcode</th>
              <th class="p-2">Qty</th>
              <th class="p-2">Amount</th>
              <th class="p-2">Status</th>
              <th class="p-2">Create</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-slate-100 dark:divide-slate-900">
            <tr v-for="line in filteredLines" :key="`${line.sourceInvoiceNumber}-${line.lineNumber}`" :class="line.reviewRequired ? 'bg-amber-50/60 dark:bg-amber-950/20' : ''">
              <td class="p-2"><input v-model="line.importLine" type="checkbox"></td>
              <td class="p-2 whitespace-nowrap">{{ line.sourceInvoiceNumber }}</td>
              <td class="p-2 min-w-[260px]">
                <div class="font-medium">{{ line.itemName }}</div>
                <div class="text-xs text-slate-500">{{ line.category || '-' }} · {{ line.size || '-' }} · GST {{ line.taxRate }}%</div>
                <div v-if="line.reviewMessage" class="mt-1 text-xs text-amber-700 dark:text-amber-300">{{ line.reviewMessage }}</div>
              </td>
              <td class="p-2 whitespace-nowrap">{{ line.vyaparItemCode || '-' }}</td>
              <td class="p-2 whitespace-nowrap">{{ line.garmetixBarcode || '-' }}</td>
              <td class="p-2"><input v-model="line.overrideBarcode" class="w-36 rounded border px-2 py-1 dark:border-slate-700 dark:bg-slate-900" placeholder="Barcode"></td>
              <td class="p-2 text-right">{{ line.quantity }}</td>
              <td class="p-2 text-right">{{ money(line.lineTotal) }}</td>
              <td class="p-2 whitespace-nowrap">
                <span class="rounded-full px-2 py-1 text-xs font-semibold" :class="line.matchStatus === 'Matched' ? 'bg-emerald-100 text-emerald-700' : 'bg-amber-100 text-amber-700'">{{ line.matchStatus }}</span>
              </td>
              <td class="p-2 text-center"><input v-model="line.createProductAndStock" type="checkbox"></td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <div v-if="preview?.paymentSources?.length" class="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm dark:border-slate-800 dark:bg-slate-950">
      <div class="flex flex-col gap-1">
        <h2 class="text-lg font-semibold">3. Map Vyapar bank / POS / UPI sources</h2>
        <p class="text-sm text-slate-500">Vyapar keeps every bank/POS/UPI account in its own column. Map each non-cash column to the exact Garmetix bank account so receipts post to the right ledger.</p>
        <p v-if="unmappedNonCashSources().length" class="mt-2 rounded-lg bg-amber-50 px-3 py-2 text-sm text-amber-800 dark:bg-amber-950/40 dark:text-amber-200">Unmapped non-cash sources: {{ unmappedNonCashSources().join(', ') }}</p>
      </div>
      <div class="mt-4 grid gap-3 md:grid-cols-2 xl:grid-cols-3">
        <label v-for="source in preview.paymentSources" :key="source.sourceName" class="rounded-xl border p-3 text-sm dark:border-slate-800">
          <div class="font-semibold">{{ source.sourceName }}</div>
          <div class="text-xs text-slate-500">{{ money(source.totalAmount) }} · {{ source.invoiceCount }} invoices · {{ source.exampleDescription || 'No description' }}</div>
          <select v-if="source.paymentMode !== 'Cash' && source.paymentMode !== 0" v-model="paymentBankMappings[source.sourceName]" class="mt-2 w-full rounded-lg border px-3 py-2 dark:border-slate-700 dark:bg-slate-900">
            <option value="">Select exact bank/POS account</option>
            <option v-for="account in bankAccounts" :key="account.id" :value="account.id">{{ account.accountHolderName || account.bankName || 'Bank' }} {{ account.accountNumber || '' }}</option>
          </select>
          <p v-else class="mt-2 text-xs text-slate-500">Cash source; no bank mapping needed.</p>
        </label>
      </div>
    </div>

    <div v-if="preview" class="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm dark:border-slate-800 dark:bg-slate-950">
      <h2 class="text-lg font-semibold">4. Confirm import</h2>
      <div class="mt-4 grid gap-4 md:grid-cols-2 lg:grid-cols-4">
        <label class="flex items-center gap-2 text-sm"><input v-model="useVyaparInvoiceNumbers" type="checkbox"> Preserve Vyapar invoice numbers</label>
        <label class="flex items-center gap-2 text-sm"><input v-model="createMissingProductsAndStock" type="checkbox"> Create missing product + stock</label>
        <label class="flex items-center gap-2 text-sm"><input v-model="allowStockBridgeForInsufficientStock" type="checkbox"> Bridge insufficient/historical stock</label>
        <label class="block text-sm">
          <span class="font-medium">Default salesman</span>
          <select v-model="defaultSalesmanId" class="mt-1 w-full rounded-lg border px-3 py-2 dark:border-slate-700 dark:bg-slate-900">
            <option value="">Auto select active salesman</option>
            <option v-for="salesman in salesmen" :key="salesman.id" :value="salesman.id">{{ salesman.name }}</option>
          </select>
        </label>
        <label class="block text-sm lg:col-span-2">
          <span class="font-medium">Fallback bank/account for old files without bank columns</span>
          <select v-model="defaultBankAccountId" class="mt-1 w-full rounded-lg border px-3 py-2 dark:border-slate-700 dark:bg-slate-900">
            <option value="">Optional fallback only</option>
            <option v-for="account in bankAccounts" :key="account.id" :value="account.id">{{ account.accountHolderName || account.bankName || 'Bank' }} {{ account.accountNumber || '' }}</option>
          </select>
          <p class="mt-1 text-xs text-slate-500">Named Vyapar columns like SBI POS, Kotak, HDFC, PhonePe/UPI must still be mapped above.</p>
        </label>
      </div>
      <div class="mt-4 flex flex-wrap items-center gap-3">
        <button class="rounded-lg bg-emerald-600 px-4 py-2 text-sm font-semibold text-white disabled:opacity-50" :disabled="!canConfirm || fullyMatchedInvoices.length === 0" @click="confirmImport('fullyMatched')">
          {{ confirming ? 'Importing...' : `Import Fully Matched ${fullyMatchedInvoices.length}` }}
        </button>
        <button class="rounded-lg bg-blue-600 px-4 py-2 text-sm font-semibold text-white disabled:opacity-50" :disabled="!canConfirm" @click="confirmImport('allReady')">
          {{ confirming ? 'Importing...' : `Confirm Import Ready ${readyInvoices.length}` }}
        </button>
        <p class="text-sm text-slate-500">Already imported same-date/source invoices are hidden automatically. Always take DB backup before large import.</p>
      </div>
    </div>

    <div v-if="showFinalApproval" class="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4">
      <div class="w-full max-w-2xl rounded-2xl bg-white p-6 shadow-xl dark:bg-slate-950">
        <h2 class="text-xl font-bold text-slate-900 dark:text-white">Final approval before posting sale import</h2>
        <p class="mt-2 text-sm text-slate-600 dark:text-slate-300">This will create sale invoices, payments, stock movements and accounting entries. A Vyapar import batch id will be saved so the whole batch can be reversed later from Import Batches.</p>
        <div class="mt-4 grid gap-3 text-sm md:grid-cols-2">
          <p>Mode: <b>{{ pendingImportMode === 'fullyMatched' ? 'Fully matched only' : 'All ready invoices' }}</b></p>
          <p>Invoices: <b>{{ pendingImportMode === 'fullyMatched' ? fullyMatchedInvoices.length : readyInvoices.length }}</b></p>
          <p>Create missing product/stock: <b>{{ createMissingProductsAndStock ? 'Yes' : 'No' }}</b></p>
          <p>Allow historical stock bridge: <b>{{ allowStockBridgeForInsufficientStock ? 'Yes' : 'No' }}</b></p>
          <p>Use Vyapar invoice number: <b>{{ useVyaparInvoiceNumbers ? 'Yes' : 'No, use Garmetix auto-number' }}</b></p>
          <p>Non-cash mapping: <b>{{ mappedNonCashPaymentSourceCount }} / {{ nonCashPaymentSourceCount }} mapped</b></p>
        </div>
        <div class="mt-5 flex justify-end gap-2">
          <button class="rounded-lg border px-4 py-2 text-sm font-semibold dark:border-slate-700" :disabled="confirming" @click="cancelFinalApproval">Cancel</button>
          <button class="rounded-lg bg-red-600 px-4 py-2 text-sm font-semibold text-white disabled:opacity-50" :disabled="confirming" @click="runFinalApprovedImport">{{ confirming ? 'Posting...' : 'Approve & Import to Database' }}</button>
        </div>
      </div>
    </div>

    <div v-if="result" class="rounded-2xl border border-emerald-200 bg-emerald-50 p-5 text-sm text-emerald-900 dark:border-emerald-900/60 dark:bg-emerald-950/40 dark:text-emerald-100">
      <h2 class="text-lg font-semibold">Import completed</h2>
      <div class="mt-2 grid gap-2 md:grid-cols-4">
        <p>Invoices: <b>{{ result.importedInvoiceCount }}</b></p>
        <p>Lines: <b>{{ result.importedLineCount }}</b></p>
        <p>Products created: <b>{{ result.createdProductCount }}</b></p>
        <p>Bill total: <b>{{ money(result.importedBillAmount) }}</b></p>
      </div>
      <p v-if="result.skippedInvoices?.length" class="mt-3">Skipped: {{ result.skippedInvoices.join(', ') }}</p>
    </div>
  </div>
  </AppShell>
</template>
