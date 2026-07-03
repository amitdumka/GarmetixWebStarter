<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const workspace = useWorkspace()
const feedback = useUiFeedback()

const loading = ref(false)
const undoing = ref(false)
const rows = ref<any[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(50)
const q = ref('')
const from = ref('')
const to = ref('')
const undoBatch = ref<any | null>(null)
const undoReason = ref('')
const confirmUndo = ref(false)
const showClearHistory = ref(false)
const clearingHistory = ref(false)
const clearCancelledOnly = ref(true)
const allowActiveHistoryClear = ref(false)
const clearReason = ref('Clear cancelled/stale Vyapar import history so invoices can be imported again')
const clearResult = ref<any | null>(null)
const finalSummary = ref<any | null>(null)
const loadingFinalSummary = ref(false)

const selectedCompanyId = computed(() => workspace.companyId.value || '')
const selectedStoreGroupId = computed(() => workspace.storeGroupId.value || '')
const selectedStoreId = computed(() => workspace.storeId.value || '')
const totalPages = computed(() => Math.max(1, Math.ceil(total.value / pageSize.value)))

onMounted(async () => {
  auth.restore()
  setDefaultRange()
  await refresh()
})

function setDefaultRange() {
  const today = new Date()
  const first = new Date(today.getFullYear(), today.getMonth(), 1)
  from.value = first.toISOString().slice(0, 10)
  to.value = today.toISOString().slice(0, 10)
}

async function refresh() {
  if (!selectedCompanyId.value) {
    feedback.error('Select workspace company first.')
    return
  }
  loading.value = true
  try {
    const params = new URLSearchParams({
      companyId: selectedCompanyId.value,
      page: String(page.value),
      pageSize: String(pageSize.value)
    })
    if (selectedStoreId.value) params.set('storeId', selectedStoreId.value)
    if (from.value) params.set('from', from.value)
    if (to.value) params.set('to', to.value)
    if (q.value.trim()) params.set('q', q.value.trim())
    const result: any = await api.get(`sale-import/vyapar/batches?${params.toString()}`)
    rows.value = result.items || []
    total.value = result.total || 0
    await loadFinalSummary()
  } catch (error) {
    feedback.failed('Could not load Vyapar import batches', error)
  } finally {
    loading.value = false
  }
}


async function loadFinalSummary() {
  if (!selectedCompanyId.value) return
  loadingFinalSummary.value = true
  try {
    const params = new URLSearchParams({ companyId: selectedCompanyId.value })
    if (selectedStoreId.value) params.set('storeId', selectedStoreId.value)
    if (from.value) params.set('from', from.value)
    if (to.value) params.set('to', to.value)
    finalSummary.value = await api.get(`sale-import/vyapar/final-summary?${params.toString()}`)
  } catch (error) {
    feedback.failed('Could not load Vyapar final summary', error)
  } finally {
    loadingFinalSummary.value = false
  }
}

function finalSummaryBadgeClass(status: string) {
  return status === 'Complete'
    ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950 dark:text-emerald-200'
    : 'bg-amber-100 text-amber-700 dark:bg-amber-950 dark:text-amber-200'
}

function issueClass(severity: string) {
  if (severity === 'Error') return 'border-red-200 bg-red-50 text-red-800 dark:border-red-900/60 dark:bg-red-950/40 dark:text-red-100'
  if (severity === 'Warning') return 'border-amber-200 bg-amber-50 text-amber-800 dark:border-amber-900/60 dark:bg-amber-950/40 dark:text-amber-100'
  return 'border-slate-200 bg-slate-50 text-slate-700 dark:border-slate-800 dark:bg-slate-900 dark:text-slate-200'
}

function exportFinalSummaryCsv() {
  if (!finalSummary.value) return
  const summary = finalSummary.value
  const rowsData = [
    ['Section', 'Metric', 'Value'],
    ['Header', 'Status', summary.status],
    ['Header', 'Imported invoices', summary.importedInvoiceCount || 0],
    ['Header', 'Active invoices', summary.activeInvoiceCount || 0],
    ['Header', 'Cancelled invoices', summary.cancelledInvoiceCount || 0],
    ['Header', 'Batches', summary.batchCount || 0],
    ['Amount', 'Bill amount', summary.billAmountTotal || 0],
    ['Amount', 'Paid amount', summary.paidAmountTotal || 0],
    ['Amount', 'Balance amount', summary.balanceAmountTotal || 0],
    ['Amount', 'Item amount', summary.itemAmountTotal || 0],
    ['Amount', 'Payment row total', summary.paymentRowTotal || 0],
    ['Tax', 'Taxable amount', summary.taxableAmountTotal || 0],
    ['Tax', 'Tax amount', summary.taxAmountTotal || 0],
    ['Stock', 'Stock out quantity', summary.stockOutQuantity || 0],
    ['Stock', 'Historical bridge movements', summary.historicalBridgeMovementCount || 0],
    ['Stock', 'Historical bridge quantity', summary.historicalBridgeQuantity || 0],
    ...(summary.paymentModes || []).map((item: any) => ['Payment Mode', item.paymentMode, item.totalAmount || 0]),
    ...(summary.gstSummary || []).map((item: any) => ['GST', `${item.taxRate}% / ${item.lineCount} lines`, item.lineAmount || 0]),
    ...(summary.issues || []).map((item: any) => ['Issue', `${item.severity} ${item.code} ${item.reference || ''}`, item.difference ?? item.message])
  ]
  downloadCsv('vyapar-sale-import-final-summary.csv', rowsData)
}

function downloadCsv(name: string, rowsData: any[][]) {
  const csv = rowsData.map((row) => row.map((cell) => `"${String(cell ?? '').replace(/"/g, '""')}"`).join(',')).join('\n')
  const blob = new Blob([csv], { type: 'text/csv;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = name
  link.click()
  URL.revokeObjectURL(url)
}

function openClearHistory() {
  clearCancelledOnly.value = true
  allowActiveHistoryClear.value = false
  clearReason.value = 'Clear cancelled/stale Vyapar import history so invoices can be imported again'
  clearResult.value = null
  showClearHistory.value = true
}

async function runClearHistory() {
  if (!selectedCompanyId.value) {
    feedback.error('Select workspace company first.')
    return
  }
  clearingHistory.value = true
  try {
    const result: any = await api.create('sale-import/vyapar/history/clear', {
      companyId: selectedCompanyId.value,
      storeGroupId: selectedStoreGroupId.value || null,
      storeId: selectedStoreId.value || null,
      from: from.value || null,
      to: to.value || null,
      cancelledOnly: clearCancelledOnly.value,
      allowActiveHistoryClear: allowActiveHistoryClear.value,
      confirmClear: true,
      reason: clearReason.value
    })
    clearResult.value = result
    feedback.success(result.message || `Cleared ${result.clearedInvoiceCount || 0} import history rows.`)
    await refresh()
  } catch (error) {
    feedback.failed('Clear Vyapar import history failed', error)
  } finally {
    clearingHistory.value = false
  }
}

function openUndo(batch: any) {
  undoBatch.value = batch
  undoReason.value = `Admin undo/reversal of Vyapar import batch ${batch.batchReference}`
  confirmUndo.value = false
}

async function runUndo() {
  if (!undoBatch.value || !confirmUndo.value) return
  undoing.value = true
  try {
    const result: any = await api.create(`sale-import/vyapar/batches/${undoBatch.value.batchId}/undo`, {
      companyId: selectedCompanyId.value,
      storeGroupId: selectedStoreGroupId.value || null,
      storeId: selectedStoreId.value || null,
      batchId: undoBatch.value.batchId,
      confirmUndo: true,
      reason: undoReason.value
    })
    feedback.success(`Reversed ${result.cancelledInvoiceCount || 0} invoices from batch.`)
    undoBatch.value = null
    await refresh()
  } catch (error) {
    feedback.failed('Batch undo failed', error)
  } finally {
    undoing.value = false
  }
}

function nextPage() {
  if (page.value >= totalPages.value) return
  page.value++
  refresh()
}
function previousPage() {
  if (page.value <= 1) return
  page.value--
  refresh()
}
function money(value: any) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(Number(value || 0))
}
function formatDate(value: any) {
  if (!value) return '-'
  return new Date(value).toLocaleDateString('en-IN')
}
</script>

<template>
  <AppShell title="Vyapar Import Batches" @refresh="refresh">
    <div class="space-y-6">
      <div class="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm dark:border-slate-800 dark:bg-slate-950">
        <div class="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
          <div>
            <p class="text-sm font-medium text-emerald-600">Sales import</p>
            <h1 class="text-2xl font-bold text-slate-900 dark:text-white">Vyapar Sale Import Batches</h1>
            <p class="mt-1 text-sm text-slate-600 dark:text-slate-300">Review every confirmed Vyapar import batch and safely undo/reverse active invoices if a wrong file was posted.</p>
          </div>
          <div class="flex flex-wrap gap-2">
            <NuxtLink to="/billing/vyapar-import" class="rounded-lg border px-3 py-2 text-sm font-medium hover:bg-slate-50 dark:border-slate-700 dark:hover:bg-slate-900">New Import</NuxtLink>
            <button class="rounded-lg border border-amber-300 px-3 py-2 text-sm font-medium text-amber-700 hover:bg-amber-50 dark:border-amber-900 dark:text-amber-200 dark:hover:bg-amber-950/40" @click="openClearHistory">Clear Cancelled History</button>
            <NuxtLink to="/billing/vyapar-imported" class="rounded-lg border px-3 py-2 text-sm font-medium hover:bg-slate-50 dark:border-slate-700 dark:hover:bg-slate-900">Imported Invoices</NuxtLink>
          </div>
        </div>
      </div>

      <div v-if="finalSummary" class="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm dark:border-slate-800 dark:bg-slate-950">
        <div class="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
          <div>
            <p class="text-sm font-medium text-blue-600">Final closure</p>
            <h2 class="text-xl font-bold text-slate-900 dark:text-white">Vyapar Sale Import final summary + reconciliation</h2>
            <p class="mt-1 text-sm text-slate-600 dark:text-slate-300">Range: {{ from || 'start' }} to {{ to || 'today' }} · store {{ selectedStoreId || 'all accessible stores' }}</p>
          </div>
          <div class="flex flex-wrap items-center gap-2">
            <span class="rounded-full px-3 py-1 text-sm font-semibold" :class="finalSummaryBadgeClass(finalSummary.status)">{{ finalSummary.status }}</span>
            <button class="rounded-lg border px-3 py-2 text-sm font-semibold dark:border-slate-700" :disabled="loadingFinalSummary" @click="loadFinalSummary">Refresh Summary</button>
            <button class="rounded-lg border px-3 py-2 text-sm font-semibold dark:border-slate-700" @click="exportFinalSummaryCsv">Export CSV</button>
          </div>
        </div>

        <div class="mt-4 grid gap-3 md:grid-cols-3 xl:grid-cols-6">
          <div class="rounded-xl border p-3 dark:border-slate-800"><p class="text-xs text-slate-500">Active invoices</p><p class="text-xl font-bold">{{ finalSummary.activeInvoiceCount }}</p><p class="text-xs text-slate-500">{{ finalSummary.cancelledInvoiceCount }} cancelled</p></div>
          <div class="rounded-xl border p-3 dark:border-slate-800"><p class="text-xs text-slate-500">Batches</p><p class="text-xl font-bold">{{ finalSummary.batchCount }}</p><p class="text-xs text-slate-500">{{ finalSummary.sourceInvoiceCount }} source invoices</p></div>
          <div class="rounded-xl border p-3 dark:border-slate-800"><p class="text-xs text-slate-500">Bill total</p><p class="text-xl font-bold">{{ money(finalSummary.billAmountTotal) }}</p><p class="text-xs text-slate-500">Items {{ money(finalSummary.itemAmountTotal) }}</p></div>
          <div class="rounded-xl border p-3 dark:border-slate-800"><p class="text-xs text-slate-500">Paid / balance</p><p class="text-xl font-bold">{{ money(finalSummary.paidAmountTotal) }}</p><p class="text-xs text-amber-600">Bal {{ money(finalSummary.balanceAmountTotal) }}</p></div>
          <div class="rounded-xl border p-3 dark:border-slate-800"><p class="text-xs text-slate-500">GST tax</p><p class="text-xl font-bold">{{ money(finalSummary.taxAmountTotal) }}</p><p class="text-xs text-slate-500">Taxable {{ money(finalSummary.taxableAmountTotal) }}</p></div>
          <div class="rounded-xl border p-3 dark:border-slate-800"><p class="text-xs text-slate-500">Stock out qty</p><p class="text-xl font-bold">{{ finalSummary.stockOutQuantity }}</p><p class="text-xs text-slate-500">Bridge {{ finalSummary.historicalBridgeQuantity }}</p></div>
        </div>

        <div class="mt-4 grid gap-4 xl:grid-cols-2">
          <div class="rounded-xl border p-4 dark:border-slate-800">
            <h3 class="font-semibold">Payment reconciliation</h3>
            <div class="mt-3 overflow-auto">
              <table class="min-w-full text-sm">
                <thead class="text-left text-xs uppercase text-slate-500"><tr><th class="py-1">Mode</th><th class="py-1 text-right">Rows</th><th class="py-1 text-right">Total</th><th class="py-1 text-right">Bank missing</th></tr></thead>
                <tbody>
                  <tr v-for="mode in finalSummary.paymentModes || []" :key="mode.paymentMode" class="border-t dark:border-slate-800">
                    <td class="py-1">{{ mode.paymentMode }}</td>
                    <td class="py-1 text-right">{{ mode.paymentRowCount }}</td>
                    <td class="py-1 text-right">{{ money(mode.totalAmount) }}</td>
                    <td class="py-1 text-right" :class="mode.bankMissingRowCount ? 'text-amber-600' : 'text-slate-500'">{{ mode.bankMissingRowCount }}</td>
                  </tr>
                  <tr v-if="!(finalSummary.paymentModes || []).length"><td colspan="4" class="py-3 text-center text-slate-500">No payment rows.</td></tr>
                </tbody>
              </table>
            </div>
          </div>

          <div class="rounded-xl border p-4 dark:border-slate-800">
            <h3 class="font-semibold">GST reconciliation</h3>
            <div class="mt-3 overflow-auto">
              <table class="min-w-full text-sm">
                <thead class="text-left text-xs uppercase text-slate-500"><tr><th class="py-1">GST</th><th class="py-1 text-right">Lines</th><th class="py-1 text-right">Taxable</th><th class="py-1 text-right">Tax</th><th class="py-1 text-right">Line total</th></tr></thead>
                <tbody>
                  <tr v-for="gst in finalSummary.gstSummary || []" :key="gst.taxRate" class="border-t dark:border-slate-800">
                    <td class="py-1">{{ gst.taxRate }}%</td>
                    <td class="py-1 text-right">{{ gst.lineCount }}</td>
                    <td class="py-1 text-right">{{ money(gst.taxableAmount) }}</td>
                    <td class="py-1 text-right">{{ money(gst.taxAmount) }}</td>
                    <td class="py-1 text-right">{{ money(gst.lineAmount) }}</td>
                  </tr>
                  <tr v-if="!(finalSummary.gstSummary || []).length"><td colspan="5" class="py-3 text-center text-slate-500">No GST rows.</td></tr>
                </tbody>
              </table>
            </div>
          </div>
        </div>

        <div v-if="finalSummary.issues?.length" class="mt-4 space-y-2">
          <h3 class="font-semibold">Reconciliation issues / notices</h3>
          <div v-for="issue in finalSummary.issues" :key="`${issue.code}-${issue.reference || issue.message}`" class="rounded-lg border px-3 py-2 text-sm" :class="issueClass(issue.severity)">
            <b>{{ issue.severity }} · {{ issue.code }}</b> — {{ issue.message }}
            <span v-if="issue.reference" class="ml-1">({{ issue.reference }})</span>
            <span v-if="issue.difference !== null && issue.difference !== undefined" class="ml-1">Diff: {{ money(issue.difference) }}</span>
          </div>
        </div>

        <div class="mt-4 grid gap-4 xl:grid-cols-3">
          <div class="rounded-xl border p-4 dark:border-slate-800">
            <h3 class="font-semibold">Closeout checklist</h3>
            <ul class="mt-2 list-disc space-y-1 pl-5 text-sm text-slate-600 dark:text-slate-300"><li v-for="item in finalSummary.closeoutChecklist || []" :key="item">{{ item }}</li></ul>
          </div>
          <div class="rounded-xl border p-4 dark:border-slate-800">
            <h3 class="font-semibold">Known limitations</h3>
            <ul class="mt-2 list-disc space-y-1 pl-5 text-sm text-slate-600 dark:text-slate-300"><li v-for="item in finalSummary.knownLimitations || []" :key="item">{{ item }}</li></ul>
          </div>
          <div class="rounded-xl border p-4 dark:border-slate-800">
            <h3 class="font-semibold">After Vyapar import</h3>
            <ul class="mt-2 list-disc space-y-1 pl-5 text-sm text-slate-600 dark:text-slate-300"><li v-for="item in finalSummary.nextModuleCandidates || []" :key="item">{{ item }}</li></ul>
          </div>
        </div>
      </div>

      <div class="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm dark:border-slate-800 dark:bg-slate-950">
        <div class="grid gap-3 md:grid-cols-5">
          <label class="text-sm"><span class="font-medium">From</span><input v-model="from" type="date" class="mt-1 w-full rounded-lg border px-3 py-2 dark:border-slate-700 dark:bg-slate-900"></label>
          <label class="text-sm"><span class="font-medium">To</span><input v-model="to" type="date" class="mt-1 w-full rounded-lg border px-3 py-2 dark:border-slate-700 dark:bg-slate-900"></label>
          <label class="text-sm md:col-span-2"><span class="font-medium">Search</span><input v-model="q" class="mt-1 w-full rounded-lg border px-3 py-2 dark:border-slate-700 dark:bg-slate-900" placeholder="Batch ref, invoice, customer"></label>
          <div class="flex items-end gap-2"><button class="rounded-lg bg-blue-600 px-4 py-2 text-sm font-semibold text-white" :disabled="loading" @click="page = 1; refresh()">Apply</button></div>
        </div>

        <div class="mt-4 overflow-auto rounded-xl border dark:border-slate-800">
          <table class="min-w-full divide-y divide-slate-200 text-sm dark:divide-slate-800">
            <thead class="bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-500 dark:bg-slate-900">
              <tr><th class="p-2">Batch</th><th class="p-2">Date range</th><th class="p-2 text-right">Invoices</th><th class="p-2 text-right">Active</th><th class="p-2 text-right">Bill</th><th class="p-2">Sample invoices</th><th class="p-2">Status</th><th class="p-2 text-right">Action</th></tr>
            </thead>
            <tbody class="divide-y divide-slate-100 dark:divide-slate-900">
              <tr v-for="row in rows" :key="row.batchId">
                <td class="p-2 font-medium">{{ row.batchReference }}<div class="text-xs text-slate-500">{{ row.batchId }}</div></td>
                <td class="p-2 whitespace-nowrap">{{ formatDate(row.fromDate) }} - {{ formatDate(row.toDate) }}</td>
                <td class="p-2 text-right">{{ row.invoiceCount }}</td>
                <td class="p-2 text-right">{{ row.activeInvoiceCount }}</td>
                <td class="p-2 text-right">{{ money(row.billAmountTotal) }}</td>
                <td class="p-2 text-xs text-slate-500">{{ (row.sampleInvoices || []).join(', ') }}</td>
                <td class="p-2"><span class="rounded-full px-2 py-1 text-xs" :class="row.status === 'CanUndo' ? 'bg-amber-100 text-amber-700 dark:bg-amber-950 dark:text-amber-200' : 'bg-slate-100 text-slate-600 dark:bg-slate-900 dark:text-slate-300'">{{ row.status }}</span></td>
                <td class="p-2 text-right"><button class="rounded-lg border border-red-300 px-3 py-1.5 text-xs font-semibold text-red-700 disabled:opacity-50 dark:border-red-900 dark:text-red-300" :disabled="row.activeInvoiceCount <= 0" @click="openUndo(row)">Undo</button></td>
              </tr>
              <tr v-if="!rows.length"><td colspan="8" class="p-6 text-center text-slate-500">No Vyapar import batches found.</td></tr>
            </tbody>
          </table>
        </div>

        <div class="mt-4 flex items-center justify-between text-sm">
          <p>{{ total }} batches · page {{ page }} / {{ totalPages }}</p>
          <div class="flex gap-2"><button class="rounded-lg border px-3 py-2 disabled:opacity-50 dark:border-slate-700" :disabled="page <= 1 || loading" @click="previousPage">Previous</button><button class="rounded-lg border px-3 py-2 disabled:opacity-50 dark:border-slate-700" :disabled="page >= totalPages || loading" @click="nextPage">Next</button></div>
        </div>
      </div>

      <div v-if="undoBatch" class="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4">
        <div class="w-full max-w-xl rounded-2xl bg-white p-6 shadow-xl dark:bg-slate-950">
          <h2 class="text-xl font-bold text-red-700 dark:text-red-300">Undo Vyapar import batch</h2>
          <p class="mt-2 text-sm text-slate-600 dark:text-slate-300">This will cancel/reverse all active invoices in batch <b>{{ undoBatch.batchReference }}</b>, return stock, reverse accounting and keep audit history. It does not hard-delete invoices.</p>
          <label class="mt-4 block text-sm"><span class="font-medium">Reason</span><textarea v-model="undoReason" rows="3" class="mt-1 w-full rounded-lg border px-3 py-2 dark:border-slate-700 dark:bg-slate-900"></textarea></label>
          <label class="mt-4 flex items-center gap-2 text-sm"><input v-model="confirmUndo" type="checkbox"> I understand this will reverse the batch.</label>
          <div class="mt-5 flex justify-end gap-2">
            <button class="rounded-lg border px-4 py-2 text-sm font-semibold dark:border-slate-700" :disabled="undoing" @click="undoBatch = null">Cancel</button>
            <button class="rounded-lg bg-red-600 px-4 py-2 text-sm font-semibold text-white disabled:opacity-50" :disabled="!confirmUndo || undoing" @click="runUndo">{{ undoing ? 'Reversing...' : 'Reverse Batch' }}</button>
          </div>
        </div>
      </div>

      <div v-if="showClearHistory" class="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4">
        <div class="w-full max-w-2xl rounded-2xl bg-white p-6 shadow-xl dark:bg-slate-950">
          <h2 class="text-xl font-bold text-amber-700 dark:text-amber-300">Clear Vyapar import history</h2>
          <p class="mt-2 text-sm text-slate-600 dark:text-slate-300">Use this after you have cancelled/undone or hard-deleted wrong imported invoices and want the same Vyapar file to show in preview again. This clears only import markers from invoice remarks; it does not delete sale invoices.</p>
          <div class="mt-4 rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900 dark:border-amber-900/50 dark:bg-amber-950/40 dark:text-amber-100">
            Current filter: {{ from || 'start' }} to {{ to || 'today' }} · store {{ selectedStoreId || 'all accessible stores' }}
          </div>
          <label class="mt-4 flex items-center gap-2 text-sm"><input v-model="clearCancelledOnly" type="checkbox"> Clear only cancelled/undone import history — recommended</label>
          <label class="mt-3 flex items-center gap-2 text-sm"><input v-model="allowActiveHistoryClear" type="checkbox" :disabled="clearCancelledOnly"> I understand and allow clearing active invoice import history</label>
          <label class="mt-4 block text-sm"><span class="font-medium">Reason</span><textarea v-model="clearReason" rows="3" class="mt-1 w-full rounded-lg border px-3 py-2 dark:border-slate-700 dark:bg-slate-900"></textarea></label>
          <div v-if="clearResult" class="mt-4 rounded-lg bg-emerald-50 p-3 text-sm text-emerald-800 dark:bg-emerald-950/40 dark:text-emerald-100">
            {{ clearResult.message }}
            <div v-if="clearResult.skippedActiveInvoiceCount" class="mt-1 text-amber-700 dark:text-amber-200">Skipped active invoices: {{ clearResult.skippedActiveInvoiceCount }}</div>
          </div>
          <div class="mt-5 flex justify-end gap-2">
            <button class="rounded-lg border px-4 py-2 text-sm font-semibold dark:border-slate-700" :disabled="clearingHistory" @click="showClearHistory = false">Close</button>
            <button class="rounded-lg bg-amber-600 px-4 py-2 text-sm font-semibold text-white disabled:opacity-50" :disabled="clearingHistory || (!clearCancelledOnly && !allowActiveHistoryClear)" @click="runClearHistory">{{ clearingHistory ? 'Clearing...' : 'Clear History' }}</button>
          </div>
        </div>
      </div>

    </div>
  </AppShell>
</template>
