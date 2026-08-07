<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-undo-2" class="size-4" /> Back Office read model</p>
          <h2 class="garmetix-dashboard-title">Sale Return / Exchange Register</h2>
          <p class="garmetix-dashboard-subtitle">
            Every completed sale return and exchange, with the original invoice and credit note linked. Return/Exchange entry itself stays on the POS app.
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

    <div class="garmetix-section-card grid gap-3 lg:grid-cols-[minmax(220px,1fr)_150px_150px_150px_auto]">
      <UFormField label="Search invoice, customer or mobile">
        <UInput v-model="search" icon="i-lucide-search" placeholder="Invoice, customer, mobile" @keyup.enter="applyFilters" />
      </UFormField>
      <UFormField label="Type">
        <USelect v-model="kindFilter" :items="kindOptions" />
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
      <table class="w-full min-w-[1080px] border-collapse text-sm">
        <thead class="bg-muted/30 text-left text-xs uppercase text-muted">
          <tr>
            <th class="border-b border-default p-3">Invoice</th>
            <th class="border-b border-default p-3">Date</th>
            <th class="border-b border-default p-3">Type</th>
            <th class="border-b border-default p-3">Original Invoice</th>
            <th class="border-b border-default p-3">Customer</th>
            <th class="border-b border-default p-3">Credit Note</th>
            <th class="border-b border-default p-3 text-right">Amount</th>
            <th class="border-b border-default p-3 text-right">Action</th>
          </tr>
        </thead>
        <tbody>
          <tr v-if="!rows.length">
            <td colspan="8" class="p-8 text-center text-muted">No sale returns or exchanges matched the current filters.</td>
          </tr>
          <tr v-for="row in rows" :key="readText(row, ['id'])" class="align-top">
            <td class="border-b border-default p-3">
              <p class="font-semibold text-highlighted">{{ readText(row, ['invoiceNumber']) }}</p>
              <UBadge size="xs" :color="statusColor(row)" variant="soft">{{ readText(row, ['invoiceStatus'], 'Saved') }}</UBadge>
            </td>
            <td class="border-b border-default p-3">{{ formatDate(readText(row, ['onDate'], '')) }}</td>
            <td class="border-b border-default p-3">
              <UBadge size="xs" :color="readText(row, ['kind']) === 'Return' ? 'warning' : 'info'" variant="soft">
                {{ readText(row, ['kind']) }}
              </UBadge>
            </td>
            <td class="border-b border-default p-3">{{ readText(row, ['originalInvoiceNumber'], '-') }}</td>
            <td class="border-b border-default p-3">
              <p>{{ readText(row, ['customerName'], 'Walk-in Customer') }}</p>
              <p class="text-xs text-muted">{{ readText(row, ['customerMobileNumber'], '') }}</p>
            </td>
            <td class="border-b border-default p-3">{{ readText(row, ['creditNoteNumber'], '-') }}</td>
            <td class="border-b border-default p-3 text-right font-semibold">{{ money(readNumber(row, ['billAmount'])) }}</td>
            <td class="border-b border-default p-3 text-right">
              <UButton size="xs" color="neutral" variant="ghost" icon="i-lucide-eye" @click="selectRow(row)">View</UButton>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <div class="garmetix-section-card flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
      <p class="text-sm text-muted">
        Showing {{ pageStart }}-{{ pageEnd }} of {{ total }} record(s)
        <span v-if="serverFromDate && serverToDate">for {{ formatDate(serverFromDate) }} to {{ formatDate(serverToDate) }}</span>
      </p>
      <div class="flex flex-wrap items-center gap-2">
        <UButton size="sm" color="neutral" variant="soft" icon="i-lucide-chevron-left" :disabled="page <= 1 || loading" @click="goToPage(page - 1)">Prev</UButton>
        <UBadge color="neutral" variant="outline">Page {{ page }} / {{ totalPages }}</UBadge>
        <UButton size="sm" color="neutral" variant="soft" trailing-icon="i-lucide-chevron-right" :disabled="page >= totalPages || loading" @click="goToPage(page + 1)">Next</UButton>
      </div>
    </div>

    <USlideover v-model:open="detailOpen" title="Return / Exchange Detail" :description="readText(selectedRow, ['invoiceNumber'])">
      <template #body>
        <div v-if="selectedRow" class="space-y-5">
          <div class="grid grid-cols-2 gap-3 text-sm">
            <div>
              <p class="text-xs text-muted">Type</p>
              <p class="font-medium">{{ readText(selectedRow, ['kind']) }}</p>
            </div>
            <div>
              <p class="text-xs text-muted">Original Invoice</p>
              <p class="font-medium">{{ readText(selectedRow, ['originalInvoiceNumber'], '-') }}</p>
            </div>
            <div>
              <p class="text-xs text-muted">Customer</p>
              <p class="font-medium">{{ readText(selectedRow, ['customerName'], 'Walk-in Customer') }}</p>
            </div>
            <div>
              <p class="text-xs text-muted">Credit Note</p>
              <p class="font-medium">{{ readText(selectedRow, ['creditNoteNumber'], '-') }}</p>
            </div>
            <div>
              <p class="text-xs text-muted">Amount</p>
              <p class="font-medium">{{ money(readNumber(selectedRow, ['billAmount'])) }}</p>
            </div>
            <div>
              <p class="text-xs text-muted">Paid</p>
              <p class="font-medium">{{ money(readNumber(selectedRow, ['paidAmount'])) }}</p>
            </div>
          </div>

          <div>
            <div class="mb-2 flex items-center justify-between">
              <h4 class="text-sm font-semibold">Items</h4>
              <UButton size="xs" color="neutral" variant="ghost" icon="i-lucide-refresh-cw" :loading="receiptLoading" @click="loadReceipt(selectedRow)">Reload</UButton>
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

useHead({ title: 'Sale Return / Exchange Register - Garmetix Back Office' })

const { get } = useMainApiClient()

const kindOptions = [
  { value: 'all', label: 'All types' },
  { value: 'Return', label: 'Return only' },
  { value: 'Exchange', label: 'Exchange only' }
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
const kindFilter = ref('all')
const datePreset = ref('month')
const customFromDate = ref(toInputDate(new Date()))
const customToDate = ref(toInputDate(new Date()))
const page = ref(1)
const pageSize = ref(50)
const total = ref(0)
const serverFromDate = ref('')
const serverToDate = ref('')
const serverSummary = reactive({ returnCount: 0, exchangeCount: 0, totalCreditAmount: 0 })
const rows = ref<ApiRecord[]>([])
const selectedRow = ref<ApiRecord | null>(null)
const detailOpen = ref(false)
const receiptItems = ref<ApiRecord[]>([])

const totalPages = computed(() => Math.max(1, Math.ceil(total.value / Number(pageSize.value || 50))))
const pageStart = computed(() => total.value === 0 ? 0 : ((page.value - 1) * Number(pageSize.value || 50)) + 1)
const pageEnd = computed(() => Math.min(total.value, page.value * Number(pageSize.value || 50)))
const summaryCards = computed(() => [
  { label: 'Total records', value: String(total.value), detail: 'Matching register filters' },
  { label: 'Returns', value: String(serverSummary.returnCount), detail: 'In current filter window' },
  { label: 'Exchanges', value: String(serverSummary.exchangeCount), detail: 'In current filter window' },
  { label: 'Credit issued', value: money(serverSummary.totalCreditAmount), detail: 'Sum of return amounts' }
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

function statusColor(row: ApiRecord) {
  const value = readText(row, ['invoiceStatus'], '').toLowerCase()
  if (value.includes('cancel') || value.includes('void')) return 'error' as const
  if (value.includes('due') || value.includes('partial') || value.includes('pending')) return 'warning' as const
  return 'success' as const
}

function buildQuery() {
  const query: Record<string, string | number> = {
    page: page.value,
    pageSize: pageSize.value,
    datePreset: datePreset.value
  }
  if (kindFilter.value !== 'all') query.kind = kindFilter.value
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
    const response = await get<ApiRecord>('billing/sales/returns-register', buildQuery())
    const nextRows = toRows(response, ['items'])
    rows.value = nextRows
    total.value = readNumber(response, ['total']) || nextRows.length
    if (readNumber(response, ['page'])) page.value = readNumber(response, ['page'])
    if (readNumber(response, ['pageSize'])) pageSize.value = readNumber(response, ['pageSize'])
    serverFromDate.value = readText(response, ['fromDate'], '')
    serverToDate.value = readText(response, ['toDate'], '')
    serverSummary.returnCount = readNumber(response, ['returnCount'])
    serverSummary.exchangeCount = readNumber(response, ['exchangeCount'])
    serverSummary.totalCreditAmount = readNumber(response, ['totalCreditAmount'])
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load sale return/exchange register.'
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
  kindFilter.value = 'all'
  datePreset.value = 'month'
  page.value = 1
  await refresh()
}

async function selectRow(row: ApiRecord) {
  selectedRow.value = row
  detailOpen.value = true
  await loadReceipt(row)
}

async function loadReceipt(row: ApiRecord | null) {
  const id = readText(row, ['id'], '')
  if (!id) return
  receiptLoading.value = true
  try {
    const receipt = await get<ApiRecord>(`billing/sales/${id}/receipt`)
    receiptItems.value = readArray(receipt, ['items'])
  } catch (caught) {
    receiptItems.value = []
    error.value = caught instanceof Error ? caught.message : 'Unable to load return/exchange receipt items.'
  } finally {
    receiptLoading.value = false
  }
}

watch([kindFilter, datePreset, pageSize], () => { void applyFilters() })
watch([customFromDate, customToDate], () => { if (datePreset.value === 'custom') void applyFilters() })

onMounted(refresh)
</script>
