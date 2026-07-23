<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-layers" class="size-4" /> Sale</p>
          <h2 class="garmetix-dashboard-title">Vyapar Import Batches</h2>
          <p class="garmetix-dashboard-subtitle">Reconciliation summary, import batch history, and batch-level undo.</p>
        </div>
        <div class="flex flex-wrap gap-2">
          <NuxtLink to="/billing/vyapar-import"><UButton icon="i-lucide-upload" color="primary" variant="soft">New Import</UButton></NuxtLink>
          <NuxtLink to="/billing/vyapar-imported"><UButton icon="i-lucide-list" color="neutral" variant="soft">Imported Invoices</UButton></NuxtLink>
          <UButton icon="i-lucide-trash-2" color="error" variant="soft" @click="startClearHistory">Clear Cancelled History</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="error" />
    <UAlert v-if="message" color="success" variant="subtle" icon="i-lucide-circle-check" :description="message" />

    <!-- Final summary -->
    <section class="garmetix-section-card space-y-4">
      <div class="flex flex-wrap items-center justify-between gap-2">
        <h3 class="garmetix-panel-title">Final Summary</h3>
        <div class="flex gap-2">
          <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-refresh-cw" :loading="summaryLoading" @click="loadSummary">Refresh Summary</UButton>
          <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-download" :disabled="!summary" @click="exportSummaryCsv">Export CSV</UButton>
        </div>
      </div>

      <template v-if="summary">
        <UBadge :color="statusColor(readText(summary, ['status']))" variant="soft" size="lg">{{ readText(summary, ['status']) }}</UBadge>

        <div class="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-6">
          <UCard :ui="{ body: 'p-3' }"><p class="text-xs text-muted">Active Invoices</p><p class="text-lg font-semibold">{{ readNumber(summary, ['activeInvoiceCount']) }}</p></UCard>
          <UCard :ui="{ body: 'p-3' }"><p class="text-xs text-muted">Cancelled Invoices</p><p class="text-lg font-semibold">{{ readNumber(summary, ['cancelledInvoiceCount']) }}</p></UCard>
          <UCard :ui="{ body: 'p-3' }"><p class="text-xs text-muted">Batches</p><p class="text-lg font-semibold">{{ readNumber(summary, ['batchCount']) }}</p></UCard>
          <UCard :ui="{ body: 'p-3' }"><p class="text-xs text-muted">Bill Total</p><p class="text-lg font-semibold">{{ money(readNumber(summary, ['billAmountTotal'])) }}</p></UCard>
          <UCard :ui="{ body: 'p-3' }"><p class="text-xs text-muted">Paid / Balance</p><p class="text-lg font-semibold">{{ money(readNumber(summary, ['paidAmountTotal'])) }} / {{ money(readNumber(summary, ['balanceAmountTotal'])) }}</p></UCard>
          <UCard :ui="{ body: 'p-3' }"><p class="text-xs text-muted">GST Tax Total</p><p class="text-lg font-semibold">{{ money(readNumber(summary, ['taxAmountTotal'])) }}</p></UCard>
        </div>

        <div class="grid gap-4 lg:grid-cols-2">
          <div>
            <h4 class="mb-2 text-sm font-medium">Payment Mode Reconciliation</h4>
            <div class="garmetix-table-panel overflow-x-auto">
              <table class="w-full min-w-[420px] text-left text-sm">
                <thead class="bg-muted/30 text-xs uppercase text-muted"><tr><th class="px-2 py-2">Mode</th><th class="px-2 py-2 text-right">Rows</th><th class="px-2 py-2 text-right">Total</th><th class="px-2 py-2 text-right">Bank Missing</th></tr></thead>
                <tbody class="divide-y divide-default">
                  <tr v-for="row in paymentModeRows" :key="row.paymentMode">
                    <td class="px-2 py-2">{{ row.paymentMode }}</td>
                    <td class="px-2 py-2 text-right tabular-nums">{{ row.paymentRowCount }}</td>
                    <td class="px-2 py-2 text-right tabular-nums">{{ money(row.totalAmount) }}</td>
                    <td class="px-2 py-2 text-right tabular-nums" :class="row.bankMissingRowCount > 0 ? 'text-error' : ''">{{ row.bankMissingRowCount }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
          <div>
            <h4 class="mb-2 text-sm font-medium">GST Reconciliation</h4>
            <div class="garmetix-table-panel overflow-x-auto">
              <table class="w-full min-w-[420px] text-left text-sm">
                <thead class="bg-muted/30 text-xs uppercase text-muted"><tr><th class="px-2 py-2">Rate</th><th class="px-2 py-2 text-right">Lines</th><th class="px-2 py-2 text-right">Taxable</th><th class="px-2 py-2 text-right">Tax</th></tr></thead>
                <tbody class="divide-y divide-default">
                  <tr v-for="row in gstRows" :key="row.taxRate">
                    <td class="px-2 py-2">{{ row.taxRate }}%</td>
                    <td class="px-2 py-2 text-right tabular-nums">{{ row.lineCount }}</td>
                    <td class="px-2 py-2 text-right tabular-nums">{{ money(row.taxableAmount) }}</td>
                    <td class="px-2 py-2 text-right tabular-nums">{{ money(row.taxAmount) }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </div>

        <div v-if="issueRows.length">
          <h4 class="mb-2 text-sm font-medium">Issues &amp; Notices</h4>
          <div class="garmetix-table-panel overflow-x-auto">
            <table class="w-full min-w-[700px] text-left text-sm">
              <thead class="bg-muted/30 text-xs uppercase text-muted"><tr><th class="px-2 py-2">Severity</th><th class="px-2 py-2">Code</th><th class="px-2 py-2">Message</th><th class="px-2 py-2">Reference</th></tr></thead>
              <tbody class="divide-y divide-default">
                <tr v-for="(issue, index) in issueRows" :key="index">
                  <td class="px-2 py-2"><UBadge size="xs" :color="issueColor(issue.severity)" variant="soft">{{ issue.severity }}</UBadge></td>
                  <td class="px-2 py-2 tabular-nums">{{ issue.code }}</td>
                  <td class="px-2 py-2">{{ issue.message }}</td>
                  <td class="px-2 py-2 text-xs text-muted">{{ issue.reference }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>

        <div class="grid gap-4 lg:grid-cols-3">
          <UCard :ui="{ body: 'p-3' }">
            <h4 class="mb-2 text-sm font-semibold">Closeout Checklist</h4>
            <ul class="list-inside list-disc space-y-1 text-xs text-muted"><li v-for="(item, i) in readArray(summary, ['closeoutChecklist'])" :key="i">{{ item }}</li></ul>
          </UCard>
          <UCard :ui="{ body: 'p-3' }">
            <h4 class="mb-2 text-sm font-semibold">Known Limitations</h4>
            <ul class="list-inside list-disc space-y-1 text-xs text-muted"><li v-for="(item, i) in readArray(summary, ['knownLimitations'])" :key="i">{{ item }}</li></ul>
          </UCard>
          <UCard :ui="{ body: 'p-3' }">
            <h4 class="mb-2 text-sm font-semibold">Next Module Candidates</h4>
            <ul class="list-inside list-disc space-y-1 text-xs text-muted"><li v-for="(item, i) in readArray(summary, ['nextModuleCandidates'])" :key="i">{{ item }}</li></ul>
          </UCard>
        </div>
      </template>
    </section>

    <!-- Batches -->
    <section class="garmetix-section-card">
      <div class="mb-3 flex flex-wrap items-end gap-3">
        <h3 class="garmetix-panel-title mr-auto">Import Batches</h3>
        <UFormField label="From"><UInput v-model="fromDate" type="date" class="w-40" /></UFormField>
        <UFormField label="To"><UInput v-model="toDate" type="date" class="w-40" /></UFormField>
        <UInput v-model="search" icon="i-lucide-search" placeholder="Search batch" class="sm:w-56" />
        <UButton icon="i-lucide-filter" color="primary" variant="soft" :loading="batchesLoading" @click="() => { page = 1; loadBatches(); loadSummary() }">Apply</UButton>
      </div>

      <div class="garmetix-table-panel overflow-x-auto">
        <table class="w-full min-w-[900px] text-left text-sm">
          <thead class="bg-muted/30 text-xs uppercase text-muted">
            <tr>
              <th class="px-3 py-2">Batch</th>
              <th class="px-3 py-2">Date Range</th>
              <th class="px-3 py-2 text-right">Invoices</th>
              <th class="px-3 py-2 text-right">Active</th>
              <th class="px-3 py-2 text-right">Bill Total</th>
              <th class="px-3 py-2">Status</th>
              <th class="px-3 py-2">Actions</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-default">
            <tr v-if="!batchRows.length"><td colspan="7" class="px-3 py-8 text-center text-muted">No import batches found.</td></tr>
            <tr v-for="row in batchRows" :key="row.batchId">
              <td class="px-3 py-2">{{ row.batchReference }}<p class="text-xs text-muted">{{ row.sampleInvoices }}</p></td>
              <td class="px-3 py-2 text-xs">{{ formatDate(row.fromDate) }} - {{ formatDate(row.toDate) }}</td>
              <td class="px-3 py-2 text-right tabular-nums">{{ row.invoiceCount }}</td>
              <td class="px-3 py-2 text-right tabular-nums">{{ row.activeInvoiceCount }}</td>
              <td class="px-3 py-2 text-right tabular-nums">{{ money(row.billAmountTotal) }}</td>
              <td class="px-3 py-2"><UBadge size="xs" :color="row.status === 'CanUndo' ? 'success' : 'neutral'" variant="soft">{{ row.status }}</UBadge></td>
              <td class="px-3 py-2">
                <UButton size="xs" color="error" variant="soft" icon="i-lucide-undo-2" :disabled="row.activeInvoiceCount <= 0" @click="startUndo(row)">Undo</UButton>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
      <div class="mt-3 flex flex-col gap-2 text-sm text-muted sm:flex-row sm:items-center sm:justify-between">
        <p>Showing {{ batchRows.length }} of {{ batchTotal }} batch(es)</p>
        <div class="flex items-center gap-2">
          <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-left" :disabled="page <= 1" :loading="batchesLoading" @click="() => { page--; loadBatches() }">Prev</UButton>
          <span>{{ page }} / {{ batchTotalPages }}</span>
          <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-right" :disabled="page >= batchTotalPages" :loading="batchesLoading" @click="() => { page++; loadBatches() }">Next</UButton>
        </div>
      </div>
    </section>

    <UModal v-model:open="undoOpen" title="Reverse Import Batch" description="This cancels every active invoice in the batch and reverses stock/accounting. This cannot be undone.">
      <template #body>
        <div class="grid gap-3">
          <p class="text-sm">Batch: <strong>{{ undoTarget?.batchReference }}</strong> ({{ undoTarget?.activeInvoiceCount }} active invoice(s))</p>
          <UTextarea v-model="undoReason" :rows="3" placeholder="Reason for reversal" />
          <label class="flex items-center gap-2 text-sm"><input type="checkbox" v-model="undoConfirmed"> I understand this reverses stock and accounting for this batch.</label>
          <UAlert v-if="undoError" color="error" variant="subtle" :description="undoError" />
          <div class="flex justify-end gap-2">
            <UButton color="error" icon="i-lucide-undo-2" :loading="undoing" :disabled="!undoConfirmed" @click="confirmUndo">Reverse Batch</UButton>
          </div>
        </div>
      </template>
    </UModal>

    <UModal v-model:open="clearHistoryOpen" title="Clear Cancelled Import History" description="Clears import-marker remarks so a re-uploaded file isn't auto-hidden as a duplicate.">
      <template #body>
        <div class="grid gap-3">
          <label class="flex items-center gap-2 text-sm"><input type="checkbox" v-model="clearForm.clearCancelledOnly" @change="onClearCancelledOnlyChange"> Cancelled invoices only (recommended)</label>
          <label class="flex items-center gap-2 text-sm"><input type="checkbox" v-model="clearForm.allowActiveHistoryClear" :disabled="clearForm.clearCancelledOnly"> Allow clearing active invoice history too</label>
          <UTextarea v-model="clearForm.reason" :rows="3" placeholder="Reason" />
          <UAlert v-if="clearError" color="error" variant="subtle" :description="clearError" />
          <div v-if="clearResult" class="rounded-md border border-default p-3 text-sm">
            <p>{{ readText(clearResult, ['message']) }}</p>
            <p class="text-xs text-muted">Cleared: {{ readNumber(clearResult, ['clearedInvoiceCount']) }} - Skipped active: {{ readNumber(clearResult, ['skippedActiveInvoiceCount']) }}</p>
          </div>
          <div class="flex justify-end gap-2">
            <UButton color="error" icon="i-lucide-trash-2" :loading="clearing" @click="confirmClearHistory">Clear History</UButton>
          </div>
        </div>
      </template>
    </UModal>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney } from '@garmetix/shared-utils'
import { getStoredUser } from '@garmetix/shared-auth'
import { formatDate, readArray, readNumber, readText, toRows, type ApiRecord, useMainApiClient } from '../../utils/main-api'

useHead({ title: 'Vyapar Import Batches - Garmetix' })

const { get, post } = useMainApiClient()

const error = ref('')
const message = ref('')
const summaryLoading = ref(false)
const batchesLoading = ref(false)

const fromDate = ref(startOfMonth())
const toDate = ref(todayIso())
const search = ref('')
const page = ref(1)
const pageSize = 25

const summary = ref<ApiRecord | null>(null)
const batches = ref<ApiRecord[]>([])
const batchTotal = ref(0)

function todayIso() { return new Date().toISOString().slice(0, 10) }
function startOfMonth() {
  const date = new Date()
  return new Date(date.getFullYear(), date.getMonth(), 1).toISOString().slice(0, 10)
}
function money(value: number) { return formatIndianMoney(value) }
function statusColor(status: string) {
  const normalized = (status || '').toLowerCase()
  if (normalized.includes('not complete') || normalized.includes('incomplete')) return 'warning'
  if (normalized.includes('complete')) return 'success'
  return 'neutral'
}
function issueColor(severity: string) {
  const normalized = (severity || '').toLowerCase()
  if (normalized === 'error') return 'error'
  if (normalized === 'warning') return 'warning'
  return 'neutral'
}

const paymentModeRows = computed(() => toRows(summary.value?.paymentModes).map(item => ({
  paymentMode: readText(item, ['paymentMode']),
  paymentRowCount: readNumber(item, ['paymentRowCount']),
  totalAmount: readNumber(item, ['totalAmount']),
  bankMissingRowCount: readNumber(item, ['bankMissingRowCount'])
})))
const gstRows = computed(() => toRows(summary.value?.gstSummary).map(item => ({
  taxRate: readNumber(item, ['taxRate']),
  lineCount: readNumber(item, ['lineCount']),
  taxableAmount: readNumber(item, ['taxableAmount']),
  taxAmount: readNumber(item, ['taxAmount'])
})))
const issueRows = computed(() => toRows(summary.value?.issues).map(item => ({
  severity: readText(item, ['severity']),
  code: readText(item, ['code']),
  message: readText(item, ['message']),
  reference: readText(item, ['reference'], '')
})))

const batchRows = computed(() => batches.value.map(item => ({
  batchId: readText(item, ['batchId'], ''),
  batchReference: readText(item, ['batchReference']),
  fromDate: item.fromDate,
  toDate: item.toDate,
  invoiceCount: readNumber(item, ['invoiceCount']),
  activeInvoiceCount: readNumber(item, ['activeInvoiceCount']),
  billAmountTotal: readNumber(item, ['billAmountTotal']),
  status: readText(item, ['status']),
  sampleInvoices: readArray(item, ['sampleInvoices']).join(', ')
})))
const batchTotalPages = computed(() => Math.max(1, Math.ceil(batchTotal.value / pageSize)))

function scopeParams() {
  const storedUser = getStoredUser(window.localStorage)
  return { companyId: storedUser?.companyId || undefined, storeId: storedUser?.storeId || undefined }
}

async function loadSummary() {
  summaryLoading.value = true
  error.value = ''
  try {
    summary.value = await get<ApiRecord>('sale-import/vyapar/final-summary', { ...scopeParams(), from: fromDate.value || undefined, to: toDate.value || undefined })
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load final summary.'
  } finally {
    summaryLoading.value = false
  }
}

async function loadBatches() {
  batchesLoading.value = true
  error.value = ''
  try {
    const result = await get<ApiRecord>('sale-import/vyapar/batches', { ...scopeParams(), from: fromDate.value || undefined, to: toDate.value || undefined, q: search.value.trim() || undefined, page: page.value, pageSize })
    batches.value = toRows(result?.items ?? result)
    batchTotal.value = readNumber(result, ['total'])
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load import batches.'
  } finally {
    batchesLoading.value = false
  }
}

function exportSummaryCsv() {
  if (!summary.value) return
  const header = ['Field', 'Value']
  const rows: string[][] = [
    ['Status', readText(summary.value, ['status'])],
    ['Active Invoices', String(readNumber(summary.value, ['activeInvoiceCount']))],
    ['Cancelled Invoices', String(readNumber(summary.value, ['cancelledInvoiceCount']))],
    ['Bill Amount Total', String(readNumber(summary.value, ['billAmountTotal']))],
    ['Paid Amount Total', String(readNumber(summary.value, ['paidAmountTotal']))],
    ['Balance Amount Total', String(readNumber(summary.value, ['balanceAmountTotal']))],
    ['Tax Amount Total', String(readNumber(summary.value, ['taxAmountTotal']))]
  ]
  const escape = (value: string) => (/[",\n]/.test(value) ? `"${value.replace(/"/g, '""')}"` : value)
  const lines = [header, ...rows].map(row => row.map(escape).join(',')).join('\n')
  const blob = new Blob([lines], { type: 'text/csv;charset=utf-8;' })
  const objectUrl = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = objectUrl
  anchor.download = 'vyapar-import-final-summary.csv'
  document.body.appendChild(anchor)
  anchor.click()
  anchor.remove()
  URL.revokeObjectURL(objectUrl)
}

// Undo
const undoOpen = ref(false)
const undoing = ref(false)
const undoError = ref('')
const undoReason = ref('')
const undoConfirmed = ref(false)
const undoTarget = ref<{ batchId: string, batchReference: string, activeInvoiceCount: number } | null>(null)

function startUndo(row: { batchId: string, batchReference: string, activeInvoiceCount: number }) {
  undoTarget.value = row
  undoReason.value = `Reversing batch ${row.batchReference}`
  undoConfirmed.value = false
  undoError.value = ''
  undoOpen.value = true
}

async function confirmUndo() {
  if (!undoTarget.value) return
  undoing.value = true
  undoError.value = ''
  try {
    const scope = scopeParams()
    await post<ApiRecord>(`sale-import/vyapar/batches/${undoTarget.value.batchId}/undo`, {
      companyId: scope.companyId,
      storeGroupId: undefined,
      storeId: scope.storeId,
      batchId: undoTarget.value.batchId,
      confirmUndo: true,
      reason: undoReason.value.trim() || null
    })
    undoOpen.value = false
    message.value = `Batch ${undoTarget.value.batchReference} reversed.`
    await Promise.all([loadBatches(), loadSummary()])
  } catch (caught) {
    undoError.value = caught instanceof Error ? caught.message : 'Unable to reverse batch.'
  } finally {
    undoing.value = false
  }
}

// Clear history
const clearHistoryOpen = ref(false)
const clearing = ref(false)
const clearError = ref('')
const clearResult = ref<ApiRecord | null>(null)
const clearForm = reactive({ clearCancelledOnly: true, allowActiveHistoryClear: false, reason: 'Clearing cancelled import markers so files can be re-uploaded.' })

function onClearCancelledOnlyChange() {
  if (clearForm.clearCancelledOnly) clearForm.allowActiveHistoryClear = false
}

function startClearHistory() {
  clearForm.clearCancelledOnly = true
  clearForm.allowActiveHistoryClear = false
  clearResult.value = null
  clearError.value = ''
  clearHistoryOpen.value = true
}

async function confirmClearHistory() {
  clearing.value = true
  clearError.value = ''
  try {
    const scope = scopeParams()
    clearResult.value = await post<ApiRecord>('sale-import/vyapar/history/clear', {
      companyId: scope.companyId,
      storeGroupId: undefined,
      storeId: scope.storeId,
      from: fromDate.value || undefined,
      to: toDate.value || undefined,
      cancelledOnly: clearForm.clearCancelledOnly,
      allowActiveHistoryClear: clearForm.allowActiveHistoryClear,
      confirmClear: true,
      reason: clearForm.reason.trim() || null
    })
  } catch (caught) {
    clearError.value = caught instanceof Error ? caught.message : 'Unable to clear history.'
  } finally {
    clearing.value = false
  }
}

onMounted(async () => {
  await Promise.all([loadSummary(), loadBatches()])
})
</script>
