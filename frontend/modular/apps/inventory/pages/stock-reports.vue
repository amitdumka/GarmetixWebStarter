<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-clipboard-list" class="size-4" /> Inventory intelligence</p>
          <h2 class="garmetix-dashboard-title">Stock Reports</h2>
          <p class="garmetix-dashboard-subtitle">Ledger vs projected quantity, receipt age, risk and reconciliation, with per-item movement history.</p>
        </div>
        <div class="flex flex-wrap items-center gap-2">
          <UBadge v-if="summary" color="neutral" variant="subtle">As of {{ formatShortDate(summary.asOf) }}</UBadge>
          <NuxtLink to="/stock"><UButton icon="i-lucide-arrow-right-left" color="neutral" variant="soft">Stock Operations</UButton></NuxtLink>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="load">Refresh</UButton>
          <UButton icon="i-lucide-download" color="primary" variant="soft" :disabled="!filteredRows.length" @click="exportCsv">Export CSV</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />

    <section v-if="summary" class="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
      <UCard :ui="{ body: 'p-4' }">
        <p class="text-xs font-medium uppercase text-muted">Inventory Value</p>
        <p class="mt-1 text-xl font-semibold">{{ formatMoney(summary.totalInventoryValue) }}</p>
        <p class="mt-1 text-xs text-muted">{{ summary.stockRows }} stock row(s)</p>
      </UCard>
      <UCard :ui="{ body: 'p-4' }">
        <p class="text-xs font-medium uppercase text-muted">Ledger Quantity</p>
        <p class="mt-1 text-xl font-semibold">{{ readNumber(summary, ['totalQuantity']) }}</p>
      </UCard>
      <UCard :ui="{ body: 'p-4' }">
        <p class="text-xs font-medium uppercase text-muted">Low Stock</p>
        <p class="mt-1 text-xl font-semibold" :class="summary.lowStockRows > 0 ? 'text-warning' : ''">{{ summary.lowStockRows }}</p>
        <p class="mt-1 text-xs text-muted">Threshold {{ summary.lowStockThreshold }}</p>
      </UCard>
      <UCard :ui="{ body: 'p-4' }">
        <p class="text-xs font-medium uppercase text-muted">Reconciliation</p>
        <p class="mt-1 text-xl font-semibold" :class="summary.reconciliationMismatchRows > 0 ? 'text-error' : ''">{{ summary.reconciliationMismatchRows }} mismatch</p>
        <p class="mt-1 text-xs text-muted">{{ summary.pendingAccountingDocuments }} pending accounting doc(s)</p>
      </UCard>
    </section>

    <section class="garmetix-section-card">
      <h3 class="garmetix-panel-title mb-3">Report Controls</h3>
      <div class="flex flex-wrap items-end gap-3">
        <label class="space-y-1 text-sm">
          <span class="text-muted">Low Stock Threshold</span>
          <UInput v-model.number="threshold" type="number" min="1" step="1" class="w-32" />
        </label>
        <UButton icon="i-lucide-filter" color="primary" variant="soft" :loading="loading" @click="load">Apply</UButton>
        <label class="space-y-1 text-sm">
          <span class="text-muted">Risk</span>
          <USelect v-model="riskFilter" :items="riskFilterItems" class="w-40" />
        </label>
        <label class="space-y-1 text-sm">
          <span class="text-muted">Receipt Age</span>
          <USelect v-model="ageFilter" :items="ageFilterItems" class="w-44" />
        </label>
        <label class="space-y-1 text-sm">
          <span class="text-muted">Reconciliation</span>
          <USelect v-model="reconFilter" :items="reconFilterItems" class="w-40" />
        </label>
        <UButton icon="i-lucide-x" color="neutral" variant="ghost" @click="clearFilters">Clear Filters</UButton>
      </div>
    </section>

    <section v-if="summary" class="grid gap-3 lg:grid-cols-2">
      <div class="garmetix-section-card">
        <h3 class="garmetix-panel-title mb-3">Receipt Age</h3>
        <button
          v-for="bucket in ageBucketRows"
          :key="bucket.label"
          type="button"
          class="mb-1 flex w-full items-center justify-between rounded-md border border-transparent px-2 py-1.5 text-left text-sm hover:border-default"
          :class="ageFilter === bucket.label ? 'border-primary bg-primary/10' : ''"
          @click="toggleAge(bucket.label)"
        >
          <span>{{ bucket.label }}</span>
          <span class="text-xs text-muted">{{ bucket.rows }} row(s) &middot; {{ bucket.quantity }} qty &middot; {{ formatMoney(bucket.inventoryValue) }}</span>
        </button>
      </div>
      <div class="garmetix-section-card">
        <h3 class="garmetix-panel-title mb-3">Stock Risk</h3>
        <button
          v-for="bucket in riskBucketRows"
          :key="bucket.label"
          type="button"
          class="mb-1 flex w-full items-center justify-between rounded-md border border-transparent px-2 py-1.5 text-left text-sm hover:border-default"
          :class="riskFilter === bucket.label ? 'border-primary bg-primary/10' : ''"
          @click="toggleRisk(bucket.label)"
        >
          <span class="flex items-center gap-2"><UBadge :color="riskColor(bucket.label)" variant="soft" size="xs">{{ bucket.label }}</UBadge></span>
          <span class="text-xs text-muted">{{ bucket.rows }} row(s) &middot; {{ bucket.quantity }} qty &middot; {{ formatMoney(bucket.inventoryValue) }}</span>
        </button>
      </div>
    </section>

    <section class="garmetix-section-card">
      <div class="mb-3 flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h3 class="garmetix-panel-title">Stock Intelligence Register</h3>
          <p class="garmetix-panel-subtitle">{{ filteredRows.length }} of {{ allRows.length }} row(s)</p>
        </div>
        <UInput v-model="search" icon="i-lucide-search" placeholder="Search product, barcode, store..." class="sm:w-72" />
      </div>
      <AdminMasterTable :columns="registerColumns" :rows="pagedRegisterRows" empty-text="No stock rows match these filters.">
        <template #actions="{ row }">
          <UButton icon="i-lucide-history" size="xs" color="neutral" variant="ghost" :loading="historyLoading === row.stockId" @click="loadHistory(row.stockId)">History</UButton>
        </template>
      </AdminMasterTable>
      <div v-if="filteredRows.length" class="mt-3 flex flex-col gap-2 text-sm text-muted sm:flex-row sm:items-center sm:justify-between">
        <p>Showing {{ pagedRegisterRows.length }} of {{ filteredRows.length }} row(s)</p>
        <div class="flex items-center gap-2">
          <USelect v-model="registerPageSize" :items="pageSizeOptions" class="w-28" />
          <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-left" :disabled="registerPage <= 1" @click="registerPage--">Prev</UButton>
          <span>{{ registerPage }} / {{ registerTotalPages }}</span>
          <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-right" :disabled="registerPage >= registerTotalPages" @click="registerPage++">Next</UButton>
        </div>
      </div>
    </section>

    <section v-if="history" class="garmetix-section-card">
      <div class="mb-3 flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h3 class="garmetix-panel-title">Movement History - {{ history.productName }} ({{ history.barcode }})</h3>
          <p class="garmetix-panel-subtitle">{{ history.storeName }}</p>
        </div>
        <div class="flex gap-2">
          <UButton icon="i-lucide-download" size="xs" color="neutral" variant="soft" :disabled="!historyRows.length" @click="exportHistoryCsv">Export History</UButton>
          <UButton icon="i-lucide-x" size="xs" color="neutral" variant="ghost" @click="history = null">Close</UButton>
        </div>
      </div>

      <div class="mb-4 grid grid-cols-2 gap-3 sm:grid-cols-5">
        <UCard :ui="{ body: 'p-3' }"><p class="text-xs text-muted">Current Qty</p><p class="text-lg font-semibold">{{ readNumber(history, ['currentQuantity']) }}</p></UCard>
        <UCard :ui="{ body: 'p-3' }"><p class="text-xs text-muted">Current Avg Cost</p><p class="text-lg font-semibold">{{ formatMoney(readNumber(history, ['currentAverageCost'])) }}</p></UCard>
        <UCard :ui="{ body: 'p-3' }"><p class="text-xs text-muted">Current Stock Value</p><p class="text-lg font-semibold">{{ formatMoney(readNumber(history, ['currentStockValue'])) }}</p></UCard>
        <UCard :ui="{ body: 'p-3' }"><p class="text-xs text-muted">Purchased</p><p class="text-lg font-semibold">{{ readNumber(history, ['totalPurchasedQuantity']) }}</p></UCard>
        <UCard :ui="{ body: 'p-3' }"><p class="text-xs text-muted">Sold</p><p class="text-lg font-semibold">{{ readNumber(history, ['totalSoldQuantity']) }}</p></UCard>
        <UCard :ui="{ body: 'p-3' }"><p class="text-xs text-muted">Sales Return</p><p class="text-lg font-semibold">{{ readNumber(history, ['totalSalesReturnQuantity']) }}</p></UCard>
        <UCard :ui="{ body: 'p-3' }"><p class="text-xs text-muted">Purchase Return</p><p class="text-lg font-semibold">{{ readNumber(history, ['totalPurchaseReturnQuantity']) }}</p></UCard>
        <UCard :ui="{ body: 'p-3' }"><p class="text-xs text-muted">Sales Amount</p><p class="text-lg font-semibold">{{ formatMoney(readNumber(history, ['totalSalesAmount'])) }}</p></UCard>
        <UCard :ui="{ body: 'p-3' }"><p class="text-xs text-muted">COGS</p><p class="text-lg font-semibold">{{ formatMoney(readNumber(history, ['totalCostOfGoodsSold'])) }}</p></UCard>
        <UCard :ui="{ body: 'p-3' }">
          <p class="text-xs text-muted">Profit / Loss</p>
          <p class="text-lg font-semibold" :class="readNumber(history, ['grossProfitOrLoss']) >= 0 ? 'text-success' : 'text-error'">{{ formatMoney(readNumber(history, ['grossProfitOrLoss'])) }}</p>
        </UCard>
      </div>

      <AdminMasterTable :columns="historyColumns" :rows="pagedHistoryRows" empty-text="No movement rows found." />
      <div v-if="historyRows.length" class="mt-3 flex flex-col gap-2 text-sm text-muted sm:flex-row sm:items-center sm:justify-between">
        <p>Showing {{ pagedHistoryRows.length }} of {{ historyRows.length }} movement(s)</p>
        <div class="flex items-center gap-2">
          <USelect v-model="historyPageSize" :items="pageSizeOptions" class="w-28" />
          <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-left" :disabled="historyPage <= 1" @click="historyPage--">Prev</UButton>
          <span>{{ historyPage }} / {{ historyTotalPages }}</span>
          <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-right" :disabled="historyPage >= historyTotalPages" @click="historyPage++">Next</UButton>
        </div>
      </div>
    </section>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney } from '@garmetix/shared-utils'
import { pageSizeOptions, paginateRows, readNumber, readText, toRows, type ApiRecord, useAdminApiClient } from '../utils/admin-api'

useHead({ title: 'Stock Reports - Garmetix Inventory' })

const { get } = useAdminApiClient()

const loading = ref(true)
const error = ref('')
const summary = ref<ApiRecord | null>(null)
const threshold = ref(3)
const search = ref('')
const riskFilter = ref('all')
const ageFilter = ref('all')
const reconFilter = ref('all')

const historyLoading = ref('')
const history = ref<ApiRecord | null>(null)

const riskFilterItems = [
  { label: 'All Risk', value: 'all' },
  { label: 'Critical', value: 'Critical' },
  { label: 'Low', value: 'Low' },
  { label: 'Watch', value: 'Watch' },
  { label: 'Healthy', value: 'Healthy' }
]
const ageFilterItems = [
  { label: 'All Ages', value: 'all' },
  { label: '0-30 Days', value: '0-30 Days' },
  { label: '31-60 Days', value: '31-60 Days' },
  { label: '61-90 Days', value: '61-90 Days' },
  { label: '91-180 Days', value: '91-180 Days' },
  { label: '180+ Days', value: '180+ Days' },
  { label: 'No Receipt History', value: 'No Receipt History' },
  { label: 'Out of Stock', value: 'Out of Stock' }
]
const reconFilterItems = computed(() => {
  const values = Array.from(new Set(allRows.value.map(row => row.reconciliationStatus).filter(Boolean)))
  return [{ label: 'All Reconciliation', value: 'all' }, ...values.map(value => ({ label: value, value }))]
})

function formatMoney(value: number) {
  return formatIndianMoney(value)
}

function formatShortDate(value: unknown) {
  if (!value) return '-'
  const date = new Date(String(value))
  if (Number.isNaN(date.getTime())) return String(value)
  return date.toLocaleDateString('en-IN', { day: '2-digit', month: 'short', year: 'numeric' })
}

function riskColor(risk: string) {
  const normalized = risk.toLowerCase()
  if (normalized === 'critical') return 'error'
  if (normalized === 'low') return 'warning'
  if (normalized === 'watch') return 'warning'
  if (normalized === 'healthy') return 'success'
  return 'neutral'
}

function reconciliationColor(status: string) {
  const normalized = status.toLowerCase()
  if (normalized.includes('mismatch')) return 'error'
  if (normalized.includes('match')) return 'success'
  return 'neutral'
}

const ageBucketRows = computed(() => toRows(summary.value?.ageBuckets).map(item => ({
  label: readText(item, ['label']),
  rows: readNumber(item, ['rows']),
  quantity: readNumber(item, ['quantity']),
  inventoryValue: readNumber(item, ['inventoryValue'])
})))
const riskBucketRows = computed(() => toRows(summary.value?.riskBuckets).map(item => ({
  label: readText(item, ['label']),
  rows: readNumber(item, ['rows']),
  quantity: readNumber(item, ['quantity']),
  inventoryValue: readNumber(item, ['inventoryValue'])
})))

const allRows = computed(() => toRows(summary.value?.rows).map(item => ({
  stockId: readText(item, ['stockId'], ''),
  productName: readText(item, ['productName']),
  barcode: readText(item, ['barcode']),
  storeName: readText(item, ['storeName']),
  ledgerQty: readNumber(item, ['ledgerQuantity']),
  projectedQty: readNumber(item, ['projectedQuantity']),
  avgCost: formatMoney(readNumber(item, ['averageCost'])),
  value: formatMoney(readNumber(item, ['inventoryValue'])),
  lastReceipt: item.lastInwardAt ? formatShortDate(item.lastInwardAt) : '-',
  ageBucketRaw: readText(item, ['ageBucket'], ''),
  riskRaw: readText(item, ['risk'], ''),
  reconciliationStatus: readText(item, ['reconciliationStatus'], ''),
  movementCount: readNumber(item, ['movementCount'])
})))

const registerColumns = [
  { key: 'productName', label: 'Product' },
  { key: 'barcode', label: 'Barcode' },
  { key: 'storeName', label: 'Store' },
  { key: 'ledgerQty', label: 'Ledger Qty' },
  { key: 'projectedQty', label: 'Projected Qty' },
  { key: 'avgCost', label: 'Avg Cost' },
  { key: 'value', label: 'Value' },
  { key: 'lastReceipt', label: 'Last Receipt' },
  { key: 'ageBucket', label: 'Age' },
  { key: 'risk', label: 'Risk' },
  { key: 'reconciliation', label: 'Reconciliation' },
  { key: 'movementCount', label: 'Movements' }
]

const filteredRows = computed(() => {
  const term = search.value.trim().toLowerCase()
  return allRows.value.filter(row => {
    if (term && !`${row.productName} ${row.barcode} ${row.storeName}`.toLowerCase().includes(term)) return false
    if (riskFilter.value !== 'all' && row.riskRaw !== riskFilter.value) return false
    if (ageFilter.value !== 'all' && row.ageBucketRaw !== ageFilter.value) return false
    if (reconFilter.value !== 'all' && row.reconciliationStatus !== reconFilter.value) return false
    return true
  }).map(row => ({
    ...row,
    ageBucket: row.ageBucketRaw,
    risk: row.riskRaw,
    reconciliation: row.reconciliationStatus
  }))
})

const registerPage = ref(1)
const registerPageSize = ref<number>(pageSizeOptions[0].value)
const registerTotalPages = computed(() => Math.max(1, Math.ceil(filteredRows.value.length / Number(registerPageSize.value || 25))))
const pagedRegisterRows = computed(() => paginateRows(filteredRows.value, registerPage.value, registerPageSize.value))
watch(registerPageSize, () => { registerPage.value = 1 })
watch(registerTotalPages, (value) => { if (registerPage.value > value) registerPage.value = value })
watch([search, riskFilter, ageFilter, reconFilter], () => { registerPage.value = 1 })

function toggleRisk(label: string) {
  riskFilter.value = riskFilter.value === label ? 'all' : label
}
function toggleAge(label: string) {
  ageFilter.value = ageFilter.value === label ? 'all' : label
}
function clearFilters() {
  search.value = ''
  riskFilter.value = 'all'
  ageFilter.value = 'all'
  reconFilter.value = 'all'
}

const historyRows = ref<ApiRecord[]>([])
const historyColumns = [
  { key: 'date', label: 'Date' },
  { key: 'movement', label: 'Movement' },
  { key: 'source', label: 'Source' },
  { key: 'qtyIn', label: 'In' },
  { key: 'qtyOut', label: 'Out' },
  { key: 'qtyAfter', label: 'Qty After' },
  { key: 'unitCost', label: 'Unit Cost' },
  { key: 'unitSale', label: 'Unit Sale' },
  { key: 'sales', label: 'Sales' },
  { key: 'cost', label: 'Cost' },
  { key: 'profitOrLoss', label: 'P/L' },
  { key: 'stockValue', label: 'Stock Value' }
]
const historyRowsMapped = computed(() => historyRows.value.map(item => ({
  date: formatShortDate(item.onDate),
  movement: `${readText(item, ['movementLabel', 'movementType'])} (${readText(item, ['direction'])})`,
  source: readText(item, ['sourceNumber', 'sourceType'], '-'),
  qtyIn: readNumber(item, ['quantityIn']),
  qtyOut: readNumber(item, ['quantityOut']),
  qtyAfter: readNumber(item, ['quantityAfter']),
  unitCost: formatMoney(readNumber(item, ['unitCost'])),
  unitSale: item.unitSalePrice !== null && item.unitSalePrice !== undefined ? formatMoney(readNumber(item, ['unitSalePrice'])) : '-',
  sales: item.salesAmount !== null && item.salesAmount !== undefined ? formatMoney(readNumber(item, ['salesAmount'])) : '-',
  cost: formatMoney(readNumber(item, ['costAmount'])),
  profitOrLoss: formatMoney(readNumber(item, ['profitOrLoss'])),
  stockValue: formatMoney(readNumber(item, ['inventoryValueAfter']))
})))

const historyPage = ref(1)
const historyPageSize = ref<number>(pageSizeOptions[0].value)
const historyTotalPages = computed(() => Math.max(1, Math.ceil(historyRowsMapped.value.length / Number(historyPageSize.value || 25))))
const pagedHistoryRows = computed(() => paginateRows(historyRowsMapped.value, historyPage.value, historyPageSize.value))
watch(historyPageSize, () => { historyPage.value = 1 })
watch(historyTotalPages, (value) => { if (historyPage.value > value) historyPage.value = value })

async function load() {
  loading.value = true
  error.value = ''
  try {
    summary.value = await get<ApiRecord>('inventory/stock-reports/summary', { lowStockThreshold: threshold.value || undefined })
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load stock report.'
  } finally {
    loading.value = false
  }
}

async function loadHistory(stockId: string) {
  if (!stockId) return
  historyLoading.value = stockId
  error.value = ''
  try {
    history.value = await get<ApiRecord>('inventory/stock-reports/movement-history', { stockId })
    historyRows.value = toRows(history.value?.rows)
    historyPage.value = 1
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load movement history.'
  } finally {
    historyLoading.value = ''
  }
}

function downloadCsv(fileName: string, header: string[], rows: string[][]) {
  const escape = (value: string) => {
    const text = value ?? ''
    return /[",\n]/.test(text) ? `"${text.replace(/"/g, '""')}"` : text
  }
  const lines = [header, ...rows].map(row => row.map(escape).join(',')).join('\n')
  const blob = new Blob([lines], { type: 'text/csv;charset=utf-8;' })
  const objectUrl = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = objectUrl
  anchor.download = fileName
  document.body.appendChild(anchor)
  anchor.click()
  anchor.remove()
  URL.revokeObjectURL(objectUrl)
}

function exportCsv() {
  const header = ['Product', 'Barcode', 'Store', 'Ledger Qty', 'Projected Qty', 'Avg Cost', 'Value', 'Last Receipt', 'Age', 'Risk', 'Reconciliation', 'Movements']
  const rows = filteredRows.value.map(row => [
    row.productName, row.barcode, row.storeName, String(row.ledgerQty), String(row.projectedQty),
    row.avgCost, row.value, row.lastReceipt, row.ageBucket, row.risk, row.reconciliation, String(row.movementCount)
  ])
  const asOf = summary.value ? String(summary.value.asOf).slice(0, 10) : new Date().toISOString().slice(0, 10)
  downloadCsv(`Garmetix-Stock-Report-${asOf}.csv`, header, rows)
}

function exportHistoryCsv() {
  if (!history.value) return
  const header = ['Date', 'Movement', 'Source', 'In', 'Out', 'Qty After', 'Unit Cost', 'Unit Sale', 'Sales', 'Cost', 'P/L', 'Stock Value']
  const rows = historyRowsMapped.value.map(row => [
    row.date, row.movement, row.source, String(row.qtyIn), String(row.qtyOut), String(row.qtyAfter),
    row.unitCost, row.unitSale, row.sales, row.cost, row.profitOrLoss, row.stockValue
  ])
  downloadCsv(`Garmetix-Stock-Movement-${readText(history.value, ['barcode'], 'export')}.csv`, header, rows)
}

onMounted(load)
</script>
