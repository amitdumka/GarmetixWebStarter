<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-receipt-text" class="size-4" /> Back Office read model</p>
          <h2 class="garmetix-dashboard-title">Sale Invoices</h2>
          <p class="garmetix-dashboard-subtitle">
            Back Office sale invoice list and review route. Fast counter sale entry, cancel and hard-delete remain owned by the POS app.
          </p>
        </div>
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="applyFilters">Refresh</UButton>
      </div>
    </div>

    <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="error" />

    <section class="grid gap-3 md:grid-cols-4">
      <div v-for="card in summaryCards" :key="card.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label">{{ card.label }}</p>
        <p class="garmetix-metric-value">{{ card.value }}</p>
        <p class="garmetix-metric-caption">{{ card.detail }}</p>
      </div>
    </section>

    <div class="garmetix-section-card grid gap-3 lg:grid-cols-[minmax(220px,1fr)_160px_150px_150px_auto]">
      <UFormField label="Search invoice, customer or mobile">
        <UInput v-model="search" icon="i-lucide-search" placeholder="Invoice, customer, mobile" @keyup.enter="applyFilters" />
      </UFormField>
      <UFormField label="Status">
        <USelect v-model="statusFilter" :items="statusOptions" />
      </UFormField>
      <UFormField label="Date">
        <USelect v-model="datePreset" :items="datePresetOptions" />
      </UFormField>
      <UFormField label="Rows">
        <USelect v-model="pageSize" :items="pageSizeOptions" />
      </UFormField>
      <div class="flex items-end gap-2">
        <UButton color="neutral" variant="soft" icon="i-lucide-list-filter" :loading="loading" @click="applyFilters">Find</UButton>
        <UButton color="neutral" variant="ghost" icon="i-lucide-x" @click="clearSearch">Clear</UButton>
      </div>
      <template v-if="datePreset === 'custom'">
        <UFormField label="From">
          <UInput v-model="customFromDate" type="date" />
        </UFormField>
        <UFormField label="To">
          <UInput v-model="customToDate" type="date" />
        </UFormField>
      </template>
    </div>

    <div class="garmetix-table-panel overflow-x-auto">
      <table class="w-full min-w-[1000px] border-collapse text-sm">
        <thead class="bg-muted/30 text-left text-xs uppercase text-muted">
          <tr>
            <th class="border-b border-default p-3">Invoice</th>
            <th class="border-b border-default p-3">Date</th>
            <th class="border-b border-default p-3">Customer</th>
            <th class="border-b border-default p-3 text-right">Bill</th>
            <th class="border-b border-default p-3 text-right">Paid</th>
            <th class="border-b border-default p-3 text-right">Balance</th>
            <th class="border-b border-default p-3 text-right">Action</th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="!invoices.length">
            <td colspan="7" class="p-8 text-center text-muted">No sale invoices matched the current filters.</td>
          </tr>
          <tr v-for="invoice in invoices" :key="readText(invoice, ['id'])" class="align-top">
            <td class="border-b border-default p-3">
              <p class="font-semibold text-highlighted">{{ readText(invoice, ['invoiceNumber']) }}</p>
              <UBadge size="xs" :color="statusColor(invoice)" variant="soft">{{ readText(invoice, ['invoiceStatus'], 'Saved') }}</UBadge>
            </td>
            <td class="border-b border-default p-3">{{ formatDate(readText(invoice, ['onDate', 'invoiceDate'], '')) }}</td>
            <td class="border-b border-default p-3">
              <p>{{ readText(invoice, ['customerName'], 'Walk-in Customer') }}</p>
              <p class="text-xs text-muted">{{ readText(invoice, ['customerMobileNumber', 'customerMobile'], '') }}</p>
            </td>
            <td class="border-b border-default p-3 text-right font-semibold">{{ money(readNumber(invoice, ['billAmount', 'netAmount', 'totalAmount'])) }}</td>
            <td class="border-b border-default p-3 text-right">{{ money(readNumber(invoice, ['paidAmount'])) }}</td>
            <td class="border-b border-default p-3 text-right" :class="readNumber(invoice, ['balanceAmount']) > 0 ? 'text-warning' : ''">
              {{ money(readNumber(invoice, ['balanceAmount'])) }}
            </td>
            <td class="border-b border-default p-3 text-right">
              <UButton size="xs" color="neutral" variant="ghost" icon="i-lucide-eye" @click="selectInvoice(invoice)">View</UButton>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <div class="garmetix-section-card flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
      <p class="text-sm text-muted">
        Showing {{ pageStart }}-{{ pageEnd }} of {{ total }} invoice(s)
        <span v-if="serverFromDate && serverToDate">for {{ formatDate(serverFromDate) }} to {{ formatDate(serverToDate) }}</span>
      </p>
      <div class="flex flex-wrap items-center gap-2">
        <UButton size="sm" color="neutral" variant="soft" icon="i-lucide-chevron-left" :disabled="page <= 1 || loading" @click="goToPage(page - 1)">Prev</UButton>
        <UBadge color="neutral" variant="outline">Page {{ page }} / {{ totalPages }}</UBadge>
        <UButton size="sm" color="neutral" variant="soft" trailing-icon="i-lucide-chevron-right" :disabled="page >= totalPages || loading" @click="goToPage(page + 1)">Next</UButton>
      </div>
    </div>

    <USlideover v-model:open="detailOpen" title="Invoice Detail" :description="readText(selectedInvoice, ['invoiceNumber'])">
      <template #body>
        <div v-if="selectedInvoice" class="space-y-5">
          <div class="grid grid-cols-2 gap-3 text-sm">
            <div>
              <p class="text-xs text-muted">Customer</p>
              <p class="font-medium">{{ readText(selectedInvoice, ['customerName'], 'Walk-in Customer') }}</p>
            </div>
            <div>
              <p class="text-xs text-muted">Mobile</p>
              <p class="font-medium">{{ readText(selectedInvoice, ['customerMobileNumber', 'customerMobile'], '-') }}</p>
            </div>
            <div>
              <p class="text-xs text-muted">Bill</p>
              <p class="font-medium">{{ money(readNumber(selectedInvoice, ['billAmount', 'netAmount', 'totalAmount'])) }}</p>
            </div>
            <div>
              <p class="text-xs text-muted">Balance</p>
              <p class="font-medium">{{ money(readNumber(selectedInvoice, ['balanceAmount'])) }}</p>
            </div>
          </div>

          <div>
            <div class="mb-2 flex items-center justify-between">
              <h4 class="text-sm font-semibold">Items</h4>
              <UButton size="xs" color="neutral" variant="ghost" icon="i-lucide-refresh-cw" :loading="receiptLoading" @click="loadReceipt(selectedInvoice)">Reload</UButton>
            </div>
            <div class="max-h-72 space-y-2 overflow-auto pr-1">
              <div v-if="!receiptItems.length" class="rounded-md border border-dashed border-default p-4 text-center text-sm text-muted">
                No receipt items loaded.
              </div>
              <div v-for="(item, index) in receiptItems" :key="readText(item, ['id'], String(index))" class="rounded-md border border-default p-3 text-sm">
                <div class="flex justify-between gap-3">
                  <div class="min-w-0">
                    <p class="truncate font-medium">{{ readText(item, ['productName', 'name'], 'Item') }}</p>
                    <p class="text-xs text-muted">{{ readText(item, ['barcode'], '-') }} | Qty {{ readNumber(item, ['quantity']) }}</p>
                  </div>
                  <p class="font-semibold">{{ money(readNumber(item, ['total', 'lineTotal', 'amount'])) }}</p>
                </div>
              </div>
            </div>
          </div>
        </div>
      </template>
    </USlideover>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney } from '@garmetix/shared-utils'
import { formatDate, readArray, readNumber, readText, toRows, type ApiRecord, useMainApiClient } from '../../utils/main-api'

useHead({ title: 'Sale Invoices - Garmetix Back Office' })

const { get } = useMainApiClient()

const statusOptions = [
  { value: 'all', label: 'All sales' },
  { value: 'Pending', label: 'Pending' },
  { value: 'Paid', label: 'Paid' },
  { value: 'PartiallyPaid', label: 'Partially paid' },
  { value: 'Cancelled', label: 'Cancelled' },
  { value: 'Refunded', label: 'Refunded' },
  { value: 'PartiallyRefunded', label: 'Partially refunded' },
  { value: 'Overdue', label: 'Overdue' },
  { value: 'Draft', label: 'Draft' }
]
const datePresetOptions = [
  { value: 'today', label: 'Today' },
  { value: 'yesterday', label: 'Yesterday' },
  { value: 'month', label: 'This month' },
  { value: 'last-month', label: 'Last month' },
  { value: 'year', label: 'This year' },
  { value: 'custom', label: 'Custom' }
]
const pageSizeOptions = [
  { value: 25, label: '25' },
  { value: 50, label: '50' },
  { value: 100, label: '100' },
  { value: 200, label: '200' }
]

const loading = ref(false)
const receiptLoading = ref(false)
const error = ref('')
const search = ref('')
const statusFilter = ref('all')
const datePreset = ref('today')
const customFromDate = ref(toInputDate(new Date()))
const customToDate = ref(toInputDate(new Date()))
const page = ref(1)
const pageSize = ref(50)
const total = ref(0)
const serverFromDate = ref('')
const serverToDate = ref('')
const serverSummary = reactive({ billAmount: 0, paidAmount: 0, balanceAmount: 0, cancelledCount: 0 })
const invoices = ref<ApiRecord[]>([])
const selectedInvoice = ref<ApiRecord | null>(null)
const detailOpen = ref(false)
const receiptItems = ref<ApiRecord[]>([])

const totalPages = computed(() => Math.max(1, Math.ceil(total.value / Number(pageSize.value || 50))))
const pageStart = computed(() => total.value === 0 ? 0 : ((page.value - 1) * Number(pageSize.value || 50)) + 1)
const pageEnd = computed(() => Math.min(total.value, page.value * Number(pageSize.value || 50)))
const summaryCards = computed(() => [
  { label: 'Invoices', value: String(total.value), detail: 'Matching register filters' },
  { label: 'Sales amount', value: money(serverSummary.billAmount), detail: 'Server register total' },
  { label: 'Due balance', value: money(serverSummary.balanceAmount), detail: `Paid ${money(serverSummary.paidAmount)}` },
  { label: 'Cancelled', value: String(serverSummary.cancelledCount), detail: 'Matching filters' }
])

function money(value: number) {
  return formatIndianMoney(value)
}

function toInputDate(value: Date) {
  const year = value.getFullYear()
  const month = String(value.getMonth() + 1).padStart(2, '0')
  const day = String(value.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

function statusColor(invoice: ApiRecord) {
  const value = readText(invoice, ['invoiceStatus'], '').toLowerCase()
  if (value.includes('cancel') || value.includes('void')) return 'error' as const
  if (value.includes('due') || value.includes('partial') || value.includes('pending')) return 'warning' as const
  if (value.includes('draft')) return 'neutral' as const
  return 'success' as const
}

function buildQuery() {
  const query: Record<string, string | number> = {
    page: page.value,
    pageSize: pageSize.value,
    datePreset: datePreset.value
  }
  if (statusFilter.value !== 'all') query.status = statusFilter.value
  if (search.value.trim()) query.q = search.value.trim()
  if (datePreset.value === 'custom') {
    if (customFromDate.value) query.from = customFromDate.value
    if (customToDate.value) query.to = customToDate.value
  }
  return query
}

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const response = await get<ApiRecord>('billing/sales', buildQuery())
    const rows = toRows(response, ['items'])
    invoices.value = rows
    total.value = readNumber(response, ['total']) || rows.length
    if (readNumber(response, ['page'])) page.value = readNumber(response, ['page'])
    if (readNumber(response, ['pageSize'])) pageSize.value = readNumber(response, ['pageSize'])
    serverFromDate.value = readText(response, ['fromDate'], '')
    serverToDate.value = readText(response, ['toDate'], '')
    serverSummary.billAmount = readNumber(response, ['billAmount'])
    serverSummary.paidAmount = readNumber(response, ['paidAmount'])
    serverSummary.balanceAmount = readNumber(response, ['balanceAmount'])
    serverSummary.cancelledCount = readNumber(response, ['cancelledCount'])
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load sale invoices.'
  } finally {
    loading.value = false
  }
}

async function applyFilters() {
  page.value = 1
  await refresh()
}

async function goToPage(nextPage: number) {
  page.value = Math.min(Math.max(1, nextPage), totalPages.value)
  await refresh()
}

async function clearSearch() {
  search.value = ''
  statusFilter.value = 'all'
  datePreset.value = 'today'
  page.value = 1
  await refresh()
}

async function selectInvoice(invoice: ApiRecord) {
  selectedInvoice.value = invoice
  detailOpen.value = true
  await loadReceipt(invoice)
}

async function loadReceipt(invoice: ApiRecord | null) {
  const id = readText(invoice, ['id'], '')
  if (!id) return
  receiptLoading.value = true
  try {
    const receipt = await get<ApiRecord>(`billing/sales/${id}/receipt`)
    receiptItems.value = readArray(receipt, ['items'])
  } catch (caught) {
    receiptItems.value = []
    error.value = caught instanceof Error ? caught.message : 'Unable to load invoice receipt items.'
  } finally {
    receiptLoading.value = false
  }
}

watch([statusFilter, datePreset, pageSize], () => { void applyFilters() })
watch([customFromDate, customToDate], () => { if (datePreset.value === 'custom') void applyFilters() })

onMounted(refresh)
</script>
