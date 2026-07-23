<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-clipboard-list" class="size-4" /> Stock audit reporting</p>
          <h2 class="garmetix-dashboard-title">Stock Audit Reports</h2>
          <p class="garmetix-dashboard-subtitle">Scan log, audited-vs-current discrepancy, and category/size/color stock summaries.</p>
        </div>
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="periodsLoading" @click="loadPeriods">Refresh</UButton>
      </div>
    </div>

    <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />

    <section class="garmetix-section-card">
      <div class="grid gap-3 sm:grid-cols-2">
        <label class="space-y-1 text-sm">
          <span class="text-muted">Audit Period</span>
          <USelect v-model="selectedPeriodId" :items="periodSelectItems" placeholder="Select an audit period" class="w-full" />
        </label>
        <label class="space-y-1 text-sm">
          <span class="text-muted">Store (for Current Stock Summary only)</span>
          <USelect v-model="selectedStoreId" :items="storeSelectItems" placeholder="All stores" class="w-full" />
        </label>
      </div>
    </section>

    <UTabs v-model="activeTab" :items="tabs" class="w-full max-w-xl" />

    <section v-if="activeTab === 'scan-log'" class="garmetix-section-card">
      <div class="mb-3 flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h3 class="garmetix-panel-title">Scan Log - Date-wise Trail, Clubbed By Period</h3>
          <p class="garmetix-panel-subtitle">Each row is one barcode's period total; expand the date entries to see the day-wise trail.</p>
        </div>
        <UButton icon="i-lucide-download" size="xs" color="neutral" variant="soft" :disabled="!selectedPeriodId" @click="exportScanLog">Export CSV</UButton>
      </div>
      <AdminMasterTable :columns="scanSummaryColumns" :rows="pagedScanSummaryRows" empty-text="Select an audit period with recorded scans.">
        <template #actions="{ row }">
          <UButton icon="i-lucide-calendar-days" size="xs" color="neutral" variant="ghost" @click="toggleDateBreakdown(row.barcode)">
            {{ expandedBarcode === row.barcode ? 'Hide Dates' : 'Show Dates' }}
          </UButton>
        </template>
      </AdminMasterTable>
      <div v-if="scanSummaryRows.length" class="mt-3 flex flex-col gap-2 text-sm text-muted sm:flex-row sm:items-center sm:justify-between">
        <p>Showing {{ pagedScanSummaryRows.length }} of {{ scanSummaryRows.length }} barcode(s)</p>
        <div class="flex items-center gap-2">
          <USelect v-model="scanLogPageSize" :items="pageSizeOptions" class="w-28" />
          <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-left" :disabled="scanLogPage <= 1" @click="scanLogPage--">Prev</UButton>
          <span>{{ scanLogPage }} / {{ scanLogTotalPages }}</span>
          <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-right" :disabled="scanLogPage >= scanLogTotalPages" @click="scanLogPage++">Next</UButton>
        </div>
      </div>
      <div v-if="expandedBarcode" class="mt-3 rounded-lg border border-default p-3">
        <p class="mb-2 text-xs font-medium uppercase text-muted">Date-wise entries for {{ expandedBarcode }}</p>
        <AdminMasterTable :columns="dateColumns" :rows="expandedDateRows" empty-text="No date entries." />
      </div>
    </section>

    <section v-else-if="activeTab === 'discrepancy'" class="garmetix-section-card">
      <div class="mb-3 flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h3 class="garmetix-panel-title">Discrepancy Report - Audited vs Current Stock</h3>
          <p class="garmetix-panel-subtitle">Audited quantity is the sum of every scan across this period's dates. Current quantity is live system stock right now.</p>
        </div>
        <div class="flex gap-2">
          <UButton icon="i-lucide-download" size="xs" color="neutral" variant="soft" :disabled="!selectedPeriodId" @click="exportDiscrepancy('csv')">CSV</UButton>
          <UButton icon="i-lucide-file-spreadsheet" size="xs" color="neutral" variant="soft" :disabled="!selectedPeriodId" @click="exportDiscrepancy('excel')">Excel</UButton>
        </div>
      </div>

      <div v-if="discrepancySummary" class="mb-4 grid grid-cols-2 gap-3 sm:grid-cols-5">
        <UCard :ui="{ body: 'p-3' }"><p class="text-xs text-muted">Matched</p><p class="text-lg font-semibold">{{ discrepancySummary.matchedCount }}</p></UCard>
        <UCard :ui="{ body: 'p-3' }"><p class="text-xs text-muted">Mismatch</p><p class="text-lg font-semibold text-error">{{ discrepancySummary.mismatchCount }}</p></UCard>
        <UCard :ui="{ body: 'p-3' }"><p class="text-xs text-muted">Not Counted</p><p class="text-lg font-semibold">{{ discrepancySummary.notCountedCount }}</p></UCard>
        <UCard :ui="{ body: 'p-3' }"><p class="text-xs text-muted">Missing In System</p><p class="text-lg font-semibold">{{ discrepancySummary.missingInSystemCount }}</p></UCard>
        <UCard :ui="{ body: 'p-3' }"><p class="text-xs text-muted">Variance Value (MRP)</p><p class="text-lg font-semibold">{{ formatMoney(discrepancySummary.totalAuditedMrpValue - discrepancySummary.totalCurrentMrpValue) }}</p></UCard>
      </div>

      <AdminMasterTable :columns="discrepancyColumns" :rows="pagedDiscrepancyRows" empty-text="Select an audit period to see its discrepancy report." />
      <div v-if="discrepancyRows.length" class="mt-3 flex flex-col gap-2 text-sm text-muted sm:flex-row sm:items-center sm:justify-between">
        <p>Showing {{ pagedDiscrepancyRows.length }} of {{ discrepancyRows.length }} row(s)</p>
        <div class="flex items-center gap-2">
          <USelect v-model="discrepancyPageSize" :items="pageSizeOptions" class="w-28" />
          <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-left" :disabled="discrepancyPage <= 1" @click="discrepancyPage--">Prev</UButton>
          <span>{{ discrepancyPage }} / {{ discrepancyTotalPages }}</span>
          <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-right" :disabled="discrepancyPage >= discrepancyTotalPages" @click="discrepancyPage++">Next</UButton>
        </div>
      </div>
    </section>

    <section v-else class="garmetix-section-card">
      <div class="mb-3 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h3 class="garmetix-panel-title">Stock Snapshot</h3>
          <p class="garmetix-panel-subtitle">Product Category, Color, Size, Qty, MRP Value and Cost Value for every stock group. Group By + Group Data filter which rows show; they don't collapse the table to one column.</p>
        </div>
        <div class="flex gap-2">
          <UButton icon="i-lucide-download" size="xs" color="neutral" variant="soft" @click="exportStockSummary('csv')">CSV</UButton>
          <UButton icon="i-lucide-file-spreadsheet" size="xs" color="neutral" variant="soft" @click="exportStockSummary('excel')">Excel</UButton>
        </div>
      </div>

      <div class="mb-4 grid gap-3 sm:grid-cols-3">
        <label class="space-y-1 text-sm">
          <span class="text-muted">Group By</span>
          <USelect v-model="summaryGroupBy" :items="groupByItems" class="w-full" />
        </label>
        <label class="space-y-1 text-sm">
          <span class="text-muted">Group Data</span>
          <USelect v-model="summaryGroupValue" :items="groupValueItems" class="w-full" />
        </label>
        <label class="space-y-1 text-sm">
          <span class="text-muted">Source</span>
          <USelect v-model="summarySource" :items="sourceItems" class="w-full" />
        </label>
      </div>

      <div v-if="stockSummary" class="mb-4 grid grid-cols-2 gap-3 sm:grid-cols-4">
        <UCard :ui="{ body: 'p-3' }"><p class="text-xs text-muted">Products</p><p class="text-lg font-semibold">{{ stockSummary.totalProductCount }}</p></UCard>
        <UCard :ui="{ body: 'p-3' }"><p class="text-xs text-muted">Total Qty</p><p class="text-lg font-semibold">{{ stockSummary.totalQuantity }}</p></UCard>
        <UCard :ui="{ body: 'p-3' }"><p class="text-xs text-muted">MRP Value</p><p class="text-lg font-semibold">{{ formatMoney(stockSummary.totalMrpValue) }}</p></UCard>
        <UCard :ui="{ body: 'p-3' }"><p class="text-xs text-muted">Cost Value</p><p class="text-lg font-semibold">{{ formatMoney(stockSummary.totalCostValue) }}</p></UCard>
      </div>

      <AdminMasterTable :columns="summaryColumns" :rows="pagedSummaryRows" empty-text="No stock found for this grouping." />
      <div v-if="summaryRows.length" class="mt-3 flex flex-col gap-2 text-sm text-muted sm:flex-row sm:items-center sm:justify-between">
        <p>Showing {{ pagedSummaryRows.length }} of {{ summaryRows.length }} row(s)</p>
        <div class="flex items-center gap-2">
          <USelect v-model="summaryPageSize" :items="pageSizeOptions" class="w-28" />
          <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-left" :disabled="summaryPage <= 1" @click="summaryPage--">Prev</UButton>
          <span>{{ summaryPage }} / {{ summaryTotalPages }}</span>
          <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-right" :disabled="summaryPage >= summaryTotalPages" @click="summaryPage++">Next</UButton>
        </div>
      </div>
    </section>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney } from '@garmetix/shared-utils'
import { pageSizeOptions, paginateRows, readNumber, readText, toRows, type ApiRecord, useAdminApiClient } from '../utils/admin-api'

useHead({ title: 'Stock Audit Reports - Garmetix Inventory' })

const { get, download } = useAdminApiClient()

const error = ref('')
const periodsLoading = ref(false)
const scanSummaryLoading = ref(false)
const discrepancyLoading = ref(false)
const summaryLoading = ref(false)

const periods = ref<ApiRecord[]>([])
const stores = ref<ApiRecord[]>([])
const selectedPeriodId = ref('')
const selectedStoreId = ref('all')
const activeTab = ref('scan-log')

const scanSummary = ref<ApiRecord[]>([])
const expandedBarcode = ref('')
const discrepancy = ref<ApiRecord | null>(null)
const stockSummary = ref<ApiRecord | null>(null)
const summaryGroupBy = ref('category')
const summaryGroupValue = ref('all')
const summarySource = ref('current')

const tabs = [
  { label: 'Scan Log', value: 'scan-log', icon: 'i-lucide-scan-line' },
  { label: 'Discrepancy Report', value: 'discrepancy', icon: 'i-lucide-git-compare' },
  { label: 'Stock Snapshot', value: 'stock-summary', icon: 'i-lucide-layout-grid' }
]
const groupByItems = [
  { label: 'Category', value: 'category' },
  { label: 'Size', value: 'size' },
  { label: 'Color', value: 'color' }
]
const sourceItems = [
  { label: 'Current Live Stock', value: 'current' },
  { label: 'Audited (Selected Period)', value: 'audited' }
]
const groupByLabel = computed(() => groupByItems.find(item => item.value === summaryGroupBy.value)?.label ?? 'Value')
const groupValueItems = computed(() => {
  const values = Array.isArray(stockSummary.value?.availableGroupValues) ? stockSummary.value!.availableGroupValues as string[] : []
  return [{ label: `All ${groupByLabel.value}`, value: 'all' }, ...values.map(value => ({ label: value, value }))]
})

const periodSelectItems = computed(() => periods.value.map(item => ({ label: readText(item, ['name']), value: readText(item, ['id'], '') })))
const storeSelectItems = computed(() => [{ label: 'All Stores', value: 'all' }, ...stores.value.map(item => ({ label: readText(item, ['name']), value: readText(item, ['id'], '') }))])

function formatMoney(value: number) {
  return formatIndianMoney(value)
}

function nonAllValue(value: string) {
  return value === 'all' ? undefined : value
}

const scanSummaryColumns = [
  { key: 'barcode', label: 'Barcode' },
  { key: 'product', label: 'Product' },
  { key: 'category', label: 'Category' },
  { key: 'color', label: 'Color' },
  { key: 'size', label: 'Size' },
  { key: 'totalQty', label: 'Period Total Qty' }
]
const scanSummaryRows = computed(() => scanSummary.value.map(item => ({
  barcode: readText(item, ['barcode']),
  product: readText(item, ['productName']),
  category: readText(item, ['categoryName'], '-'),
  color: readText(item, ['color'], '-'),
  size: readText(item, ['size'], '-'),
  totalQty: readNumber(item, ['totalQuantity'])
})))
const scanLogPage = ref(1)
const scanLogPageSize = ref<number>(pageSizeOptions[0].value)
const scanLogTotalPages = computed(() => Math.max(1, Math.ceil(scanSummaryRows.value.length / Number(scanLogPageSize.value || 25))))
const pagedScanSummaryRows = computed(() => paginateRows(scanSummaryRows.value, scanLogPage.value, scanLogPageSize.value))
watch(scanLogPageSize, () => { scanLogPage.value = 1 })
watch(scanLogTotalPages, (value) => { if (scanLogPage.value > value) scanLogPage.value = value })

const dateColumns = [
  { key: 'date', label: 'Date' },
  { key: 'qty', label: 'Qty' },
  { key: 'scanCount', label: 'Scans' }
]
const expandedDateRows = computed(() => {
  const source = scanSummary.value.find(item => readText(item, ['barcode']) === expandedBarcode.value)
  const entries = Array.isArray(source?.dateEntries) ? source!.dateEntries as ApiRecord[] : []
  return entries.map(entry => ({
    date: formatShortDate(entry.scanDate),
    qty: readNumber(entry, ['quantity']),
    scanCount: readNumber(entry, ['scanCount'])
  }))
})

const discrepancySummary = computed(() => discrepancy.value?.summary as ApiRecord | undefined)
const discrepancyColumns = [
  { key: 'product', label: 'Product' },
  { key: 'barcode', label: 'Barcode' },
  { key: 'category', label: 'Category' },
  { key: 'color', label: 'Color' },
  { key: 'size', label: 'Size' },
  { key: 'audited', label: 'Audited Qty' },
  { key: 'current', label: 'Current Qty' },
  { key: 'variance', label: 'Variance' },
  { key: 'status', label: 'Status' }
]
const discrepancyRows = computed(() => {
  const rows = Array.isArray(discrepancy.value?.rows) ? discrepancy.value!.rows as ApiRecord[] : []
  return rows.map(item => ({
    product: readText(item, ['productName']),
    barcode: readText(item, ['barcode']),
    category: readText(item, ['categoryName'], '-'),
    color: readText(item, ['color'], '-'),
    size: readText(item, ['size'], '-'),
    audited: readNumber(item, ['auditedQuantity']),
    current: readNumber(item, ['currentQuantity']),
    variance: readNumber(item, ['variance']),
    status: readText(item, ['status'])
  }))
})
const discrepancyPage = ref(1)
const discrepancyPageSize = ref<number>(pageSizeOptions[0].value)
const discrepancyTotalPages = computed(() => Math.max(1, Math.ceil(discrepancyRows.value.length / Number(discrepancyPageSize.value || 25))))
const pagedDiscrepancyRows = computed(() => paginateRows(discrepancyRows.value, discrepancyPage.value, discrepancyPageSize.value))
watch(discrepancyPageSize, () => { discrepancyPage.value = 1 })
watch(discrepancyTotalPages, (value) => { if (discrepancyPage.value > value) discrepancyPage.value = value })

const summaryColumns = [
  { key: 'category', label: 'Product Category' },
  { key: 'color', label: 'Color' },
  { key: 'size', label: 'Size' },
  { key: 'qty', label: 'Qty' },
  { key: 'mrpValue', label: 'MRP Value' },
  { key: 'costValue', label: 'Cost Value' }
]
const summaryRows = computed(() => {
  const rows = Array.isArray(stockSummary.value?.rows) ? stockSummary.value!.rows as ApiRecord[] : []
  return rows.map(item => ({
    category: readText(item, ['categoryName']),
    color: readText(item, ['colorName']),
    size: readText(item, ['sizeName']),
    qty: readNumber(item, ['quantity']),
    mrpValue: formatMoney(readNumber(item, ['mrpValue'])),
    costValue: formatMoney(readNumber(item, ['costValue']))
  }))
})
const summaryPage = ref(1)
const summaryPageSize = ref<number>(pageSizeOptions[0].value)
const summaryTotalPages = computed(() => Math.max(1, Math.ceil(summaryRows.value.length / Number(summaryPageSize.value || 25))))
const pagedSummaryRows = computed(() => paginateRows(summaryRows.value, summaryPage.value, summaryPageSize.value))
watch(summaryPageSize, () => { summaryPage.value = 1 })
watch(summaryTotalPages, (value) => { if (summaryPage.value > value) summaryPage.value = value })

function formatShortDate(value: unknown) {
  if (!value) return '-'
  const date = new Date(String(value))
  if (Number.isNaN(date.getTime())) return String(value)
  return date.toLocaleDateString('en-IN', { day: '2-digit', month: 'short', year: 'numeric' })
}

function toggleDateBreakdown(barcode: string) {
  expandedBarcode.value = expandedBarcode.value === barcode ? '' : barcode
}

async function loadPeriods() {
  periodsLoading.value = true
  error.value = ''
  try {
    const [periodData, optionsData] = await Promise.allSettled([
      get<unknown>('inventory/stock-audit/periods'),
      get<ApiRecord>('inventory/stock-operations/options')
    ])
    if (periodData.status === 'fulfilled') periods.value = toRows(periodData.value)
    if (optionsData.status === 'fulfilled') stores.value = toRows((optionsData.value as ApiRecord)?.stores)
    if (!selectedPeriodId.value && periods.value.length) selectedPeriodId.value = readText(periods.value[0], ['id'], '')
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load audit periods.'
  } finally {
    periodsLoading.value = false
  }
}

async function loadScanSummary() {
  if (!selectedPeriodId.value) {
    scanSummary.value = []
    return
  }
  scanSummaryLoading.value = true
  error.value = ''
  try {
    const result = await get<unknown>(`inventory/stock-audit/periods/${selectedPeriodId.value}/scan-summary`)
    scanSummary.value = toRows(result)
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load scan log.'
  } finally {
    scanSummaryLoading.value = false
  }
}

async function loadDiscrepancy() {
  if (!selectedPeriodId.value) {
    discrepancy.value = null
    return
  }
  discrepancyLoading.value = true
  error.value = ''
  try {
    discrepancy.value = await get<ApiRecord>(`inventory/stock-audit/periods/${selectedPeriodId.value}/discrepancy`)
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load discrepancy report.'
  } finally {
    discrepancyLoading.value = false
  }
}

async function loadStockSummary() {
  if (summarySource.value === 'audited' && !selectedPeriodId.value) {
    stockSummary.value = null
    return
  }
  summaryLoading.value = true
  error.value = ''
  try {
    stockSummary.value = await get<ApiRecord>('inventory/stock-audit/stock-summary', {
      groupBy: summaryGroupBy.value,
      groupValue: nonAllValue(summaryGroupValue.value),
      source: summarySource.value,
      auditPeriodId: summarySource.value === 'audited' ? selectedPeriodId.value : undefined,
      storeId: nonAllValue(selectedStoreId.value)
    })
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load stock summary.'
  } finally {
    summaryLoading.value = false
  }
}

async function exportScanLog() {
  if (!selectedPeriodId.value) return
  try {
    await download(`inventory/stock-audit/periods/${selectedPeriodId.value}/scan-summary/csv`, undefined, 'stock-audit-scan-log.csv')
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to export scan log.'
  }
}

async function exportDiscrepancy(format: 'csv' | 'excel') {
  if (!selectedPeriodId.value) return
  try {
    await download(`inventory/stock-audit/periods/${selectedPeriodId.value}/discrepancy/${format}`, undefined, `stock-audit-discrepancy.${format === 'excel' ? 'xlsx' : 'csv'}`)
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to export discrepancy report.'
  }
}

async function exportStockSummary(format: 'csv' | 'excel') {
  try {
    await download('inventory/stock-audit/stock-summary/' + format, {
      groupBy: summaryGroupBy.value,
      groupValue: nonAllValue(summaryGroupValue.value),
      source: summarySource.value,
      auditPeriodId: summarySource.value === 'audited' ? selectedPeriodId.value : undefined,
      storeId: nonAllValue(selectedStoreId.value)
    }, `stock-snapshot.${format === 'excel' ? 'xlsx' : 'csv'}`)
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to export stock summary.'
  }
}

watch(selectedPeriodId, () => {
  expandedBarcode.value = ''
  scanLogPage.value = 1
  discrepancyPage.value = 1
  loadScanSummary()
  loadDiscrepancy()
  if (summarySource.value === 'audited') loadStockSummary()
})
watch(summaryGroupBy, () => {
  summaryGroupValue.value = 'all'
  summaryPage.value = 1
  loadStockSummary()
})
watch([summaryGroupValue, summarySource, selectedStoreId], () => {
  summaryPage.value = 1
  loadStockSummary()
})

onMounted(async () => {
  await loadPeriods()
  await Promise.all([loadScanSummary(), loadDiscrepancy(), loadStockSummary()])
})
</script>
