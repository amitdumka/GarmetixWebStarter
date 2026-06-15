<script setup lang="ts">
import { h, resolveComponent } from 'vue'
import type { TableColumn } from '@nuxt/ui'

type StockReportBucket = {
  label: string
  rows: number
  quantity: number
  inventoryValue: number
}

type StockReportRow = {
  stockId: string
  productId: string
  productName: string
  barcode: string
  storeName: string
  ledgerQuantity: number
  projectedQuantity: number
  averageCost: number
  projectedAverageCost: number
  inventoryValue: number
  lastInwardAt?: string | null
  lastMovementAt?: string | null
  ageDays?: number | null
  ageBucket: string
  risk: string
  reconciliationStatus: string
  movementCount: number
}

type StockReportSummary = {
  asOf: string
  lowStockThreshold: number
  stockRows: number
  totalQuantity: number
  totalInventoryValue: number
  lowStockRows: number
  agedOver90DaysRows: number
  reconciliationMismatchRows: number
  pendingAccountingDocuments: number
  ageBuckets: StockReportBucket[]
  riskBuckets: StockReportBucket[]
  rows: StockReportRow[]
}

const api = useGarmetixApi()
const auth = useAuth()
const feedback = useUiFeedback()
const isAuthenticated = auth.isAuthenticated
const UBadge = resolveComponent('UBadge')

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const loading = ref(false)
const loadError = ref('')
const search = ref('')
const riskFilter = ref('all')
const ageFilter = ref('all')
const reconciliationFilter = ref('all')
const lowStockThreshold = ref(3)
const summary = ref<StockReportSummary>(emptySummary())

const riskOptions = [
  { label: 'All risk levels', value: 'all' },
  { label: 'Critical', value: 'Critical' },
  { label: 'Low', value: 'Low' },
  { label: 'Watch', value: 'Watch' },
  { label: 'Healthy', value: 'Healthy' }
]

const ageOptions = [
  { label: 'All receipt ages', value: 'all' },
  { label: '0-30 days', value: '0-30 Days' },
  { label: '31-60 days', value: '31-60 Days' },
  { label: '61-90 days', value: '61-90 Days' },
  { label: '91-180 days', value: '91-180 Days' },
  { label: '180+ days', value: '180+ Days' },
  { label: 'No receipt history', value: 'No Receipt History' },
  { label: 'Out of stock', value: 'Out of Stock' }
]

const reconciliationOptions = [
  { label: 'All reconciliation states', value: 'all' },
  { label: 'Matched', value: 'Matched' },
  { label: 'Mismatch', value: 'Mismatch' }
]

const metrics = computed(() => [
  {
    label: 'Inventory Value',
    value: money(summary.value.totalInventoryValue),
    meta: 'Weighted-average ledger value',
    icon: 'i-lucide-indian-rupee',
    color: 'success'
  },
  {
    label: 'Ledger Quantity',
    value: quantity(summary.value.totalQuantity),
    meta: `${summary.value.stockRows} stock rows`,
    icon: 'i-lucide-warehouse',
    color: 'primary'
  },
  {
    label: 'Low Stock',
    value: summary.value.lowStockRows,
    meta: `At or below ${summary.value.lowStockThreshold}`,
    icon: 'i-lucide-triangle-alert',
    color: summary.value.lowStockRows ? 'warning' : 'success'
  },
  {
    label: 'Reconciliation',
    value: summary.value.reconciliationMismatchRows,
    meta: `${summary.value.pendingAccountingDocuments} accounting pending`,
    icon: 'i-lucide-scale',
    color: summary.value.reconciliationMismatchRows ? 'error' : 'success'
  }
])

const ageBuckets = computed(() => summary.value.ageBuckets || [])
const riskBuckets = computed(() => summary.value.riskBuckets || [])

const rows = computed(() => (summary.value.rows || [])
  .filter(row => riskFilter.value === 'all' || row.risk === riskFilter.value)
  .filter(row => ageFilter.value === 'all' || row.ageBucket === ageFilter.value)
  .filter(row => reconciliationFilter.value === 'all' || row.reconciliationStatus === reconciliationFilter.value)
  .filter((row) => {
    const term = search.value.trim().toLowerCase()
    return !term || [row.productName, row.barcode, row.storeName, row.risk, row.ageBucket, row.reconciliationStatus]
      .some(value => String(value || '').toLowerCase().includes(term))
  })
  .map(row => ({
    ...row,
    ledgerQuantityText: quantity(row.ledgerQuantity),
    projectedQuantityText: quantity(row.projectedQuantity),
    averageCostText: money(row.averageCost),
    inventoryValueText: money(row.inventoryValue),
    lastInwardText: row.lastInwardAt ? formatDate(row.lastInwardAt) : '-',
    ageText: row.ageDays == null ? row.ageBucket : `${row.ageDays} ${row.ageDays === 1 ? 'day' : 'days'}`
  })))

const columns: TableColumn<any>[] = [
  {
    accessorKey: 'productName',
    header: 'Product',
    cell: ({ row }) => h('div', { class: 'min-w-48' }, [
      h('strong', row.original.productName),
      h('p', { class: 'text-xs text-muted' }, row.original.barcode || 'No barcode')
    ])
  },
  { accessorKey: 'storeName', header: 'Store' },
  { accessorKey: 'ledgerQuantityText', header: 'Ledger Qty' },
  { accessorKey: 'projectedQuantityText', header: 'Projected Qty' },
  { accessorKey: 'averageCostText', header: 'Avg Cost' },
  { accessorKey: 'inventoryValueText', header: 'Value' },
  { accessorKey: 'lastInwardText', header: 'Last Receipt' },
  {
    accessorKey: 'ageText',
    header: 'Receipt Age',
    cell: ({ row }) => h(UBadge, {
      color: ageColor(row.original.ageBucket),
      variant: 'subtle'
    }, () => row.original.ageText)
  },
  {
    accessorKey: 'risk',
    header: 'Risk',
    cell: ({ row }) => h(UBadge, {
      color: riskColor(row.original.risk),
      variant: 'subtle'
    }, () => row.original.risk)
  },
  {
    accessorKey: 'reconciliationStatus',
    header: 'Reconciliation',
    cell: ({ row }) => h(UBadge, {
      color: row.original.reconciliationStatus === 'Matched' ? 'success' : 'error',
      variant: 'subtle'
    }, () => row.original.reconciliationStatus)
  },
  { accessorKey: 'movementCount', header: 'Movements' }
]

function emptySummary(): StockReportSummary {
  return {
    asOf: '',
    lowStockThreshold: 3,
    stockRows: 0,
    totalQuantity: 0,
    totalInventoryValue: 0,
    lowStockRows: 0,
    agedOver90DaysRows: 0,
    reconciliationMismatchRows: 0,
    pendingAccountingDocuments: 0,
    ageBuckets: [],
    riskBuckets: [],
    rows: []
  }
}

async function refresh() {
  if (!auth.isAuthenticated.value) return

  loading.value = true
  loadError.value = ''
  try {
    const threshold = Math.max(1, Math.round(Number(lowStockThreshold.value) || 3))
    lowStockThreshold.value = threshold
    const [companyRows, storeRows, report] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores'),
      api.get<StockReportSummary>(`inventory/stock-reports/summary?lowStockThreshold=${threshold}`)
    ])
    companies.value = companyRows
    stores.value = storeRows
    summary.value = report
  } catch (error) {
    loadError.value = 'Stock ageing, risk and valuation could not be loaded. Check the selected workspace and try again.'
    feedback.failed('Stock reports refresh failed', error)
  } finally {
    loading.value = false
  }
}

function resetFilters() {
  search.value = ''
  riskFilter.value = 'all'
  ageFilter.value = 'all'
  reconciliationFilter.value = 'all'
}

function exportCsv() {
  if (!rows.value.length) {
    feedback.failed('No stock report rows to export')
    return
  }

  const headings = [
    'Product', 'Barcode', 'Store', 'Ledger Quantity', 'Projected Quantity',
    'Average Cost', 'Projected Average Cost', 'Inventory Value', 'Last Receipt',
    'Receipt Age Days', 'Age Bucket', 'Risk', 'Reconciliation', 'Movement Count'
  ]
  const values = rows.value.map(row => [
    row.productName,
    row.barcode,
    row.storeName,
    row.ledgerQuantity,
    row.projectedQuantity,
    row.averageCost,
    row.projectedAverageCost,
    row.inventoryValue,
    row.lastInwardAt || '',
    row.ageDays ?? '',
    row.ageBucket,
    row.risk,
    row.reconciliationStatus,
    row.movementCount
  ])
  const csv = [headings, ...values].map(line => line.map(csvCell).join(',')).join('\n')
  const blob = new Blob([csv], { type: 'text/csv;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = `Garmetix-Stock-Report-${summary.value.asOf || 'current'}.csv`
  anchor.click()
  URL.revokeObjectURL(url)
  feedback.notify('Stock report exported', `${rows.value.length} rows downloaded.`)
}

function csvCell(value: unknown) {
  return `"${String(value ?? '').replaceAll('"', '""')}"`
}

function money(value: number) {
  return new Intl.NumberFormat('en-IN', {
    style: 'currency',
    currency: 'INR',
    maximumFractionDigits: 2
  }).format(Number(value || 0))
}

function quantity(value: number) {
  return new Intl.NumberFormat('en-IN', { maximumFractionDigits: 2 }).format(Number(value || 0))
}

function formatDate(value: string) {
  return new Date(value).toLocaleDateString('en-IN', {
    day: '2-digit',
    month: 'short',
    year: 'numeric'
  })
}

function riskColor(value: string) {
  if (value === 'Critical') return 'error'
  if (value === 'Low') return 'warning'
  if (value === 'Watch') return 'info'
  return 'success'
}

function ageColor(value: string) {
  if (value === 'Out of Stock') return 'neutral'
  if (value === 'No Receipt History' || value === '180+ Days') return 'error'
  if (value === '91-180 Days') return 'warning'
  if (value === '61-90 Days') return 'info'
  return 'success'
}

onMounted(async () => {
  auth.restore()
  await refresh()
})
</script>

<template>
  <AuthScreen v-if="!isAuthenticated" @authenticated="refresh" />

  <AppShell
    v-else
    title="Stock Reports"
    :companies="companies"
    :stores="stores"
    @refresh="refresh"
    @workspace-change="refresh"
  >
    <section class="planner-dashboard">
      <UiModulePageHeader
        title="Stock Reports"
        description="Review receipt-age indicators, configurable stock risk, weighted-average valuation and ledger reconciliation."
        icon="i-lucide-chart-column-stacked"
        primary-label="Export CSV"
        primary-icon="i-lucide-download"
        @primary="exportCsv"
      >
        <template #actions>
          <UBadge :color="loading ? 'warning' : 'success'" variant="subtle">
            {{ loading ? 'Loading' : `As of ${summary.asOf ? formatDate(summary.asOf) : '-'}` }}
          </UBadge>
          <UButton to="/stock-operations" color="neutral" variant="subtle" icon="i-lucide-arrow-left-right" label="Stock Operations" />
          <UButton color="neutral" variant="subtle" icon="i-lucide-refresh-cw" label="Refresh" :loading="loading" @click="refresh" />
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

      <UCard class="planner-card">
        <template #header>
          <div class="planner-card-header">
            <div>
              <h2>Report Controls</h2>
              <p>The threshold changes Low and Watch classifications without changing stock data.</p>
            </div>
            <UButton color="neutral" variant="ghost" icon="i-lucide-filter-x" label="Clear filters" @click="resetFilters" />
          </div>
        </template>

        <div class="stock-report-filters">
          <UFormField label="Low-stock threshold">
            <div class="flex gap-2">
              <UInput v-model="lowStockThreshold" type="number" min="1" step="1" />
              <UButton icon="i-lucide-check" aria-label="Apply low-stock threshold" :loading="loading" @click="refresh" />
            </div>
          </UFormField>
          <UFormField label="Risk">
            <USelect v-model="riskFilter" :items="riskOptions" />
          </UFormField>
          <UFormField label="Receipt age">
            <USelect v-model="ageFilter" :items="ageOptions" />
          </UFormField>
          <UFormField label="Reconciliation">
            <USelect v-model="reconciliationFilter" :items="reconciliationOptions" />
          </UFormField>
        </div>
      </UCard>

      <div class="stock-report-summary-grid">
        <UCard class="planner-card">
          <template #header>
            <div class="planner-card-header">
              <div>
                <h2>Receipt Age</h2>
                <p>Age is measured from each stock row's latest inward movement.</p>
              </div>
              <UBadge color="neutral" variant="subtle">{{ summary.agedOver90DaysRows }} over 90 days</UBadge>
            </div>
          </template>
          <div class="stock-bucket-list">
            <button
              v-for="bucket in ageBuckets"
              :key="bucket.label"
              type="button"
              class="stock-bucket-row"
              :class="{ active: ageFilter === bucket.label }"
              @click="ageFilter = ageFilter === bucket.label ? 'all' : bucket.label"
            >
              <span><strong>{{ bucket.label }}</strong><small>{{ bucket.rows }} rows / {{ quantity(bucket.quantity) }} qty</small></span>
              <b>{{ money(bucket.inventoryValue) }}</b>
            </button>
          </div>
        </UCard>

        <UCard class="planner-card">
          <template #header>
            <div class="planner-card-header">
              <div>
                <h2>Stock Risk</h2>
                <p>Critical, Low and Watch bands use the selected threshold.</p>
              </div>
              <UBadge :color="summary.lowStockRows ? 'warning' : 'success'" variant="subtle">
                {{ summary.lowStockRows }} attention
              </UBadge>
            </div>
          </template>
          <div class="stock-bucket-list">
            <button
              v-for="bucket in riskBuckets"
              :key="bucket.label"
              type="button"
              class="stock-bucket-row"
              :class="{ active: riskFilter === bucket.label }"
              @click="riskFilter = riskFilter === bucket.label ? 'all' : bucket.label"
            >
              <span>
                <UBadge :color="riskColor(bucket.label)" variant="subtle">{{ bucket.label }}</UBadge>
                <small>{{ bucket.rows }} rows / {{ quantity(bucket.quantity) }} qty</small>
              </span>
              <b>{{ money(bucket.inventoryValue) }}</b>
            </button>
          </div>
        </UCard>
      </div>

      <UiRegisterPanel
        title="Stock Intelligence Register"
        description="Ledger quantities and weighted-average values are compared with the operational stock projection for each product and store."
        :loading="loading"
        :error="loadError"
        :empty="!rows.length"
        empty-title="No stock rows match these filters"
        empty-description="Clear one or more filters or refresh the selected workspace."
        empty-icon="i-lucide-chart-column-stacked"
        @retry="refresh"
      >
        <template #actions>
          <UBadge color="neutral" variant="subtle">{{ rows.length }} rows</UBadge>
          <UBadge :color="summary.reconciliationMismatchRows ? 'error' : 'success'" variant="subtle">
            {{ summary.reconciliationMismatchRows }} mismatches
          </UBadge>
        </template>

        <UiCrudToolbar
          v-model:search="search"
          search-placeholder="Search product, barcode, store, risk or age"
          :loading="loading"
          refresh-label="Refresh"
          create-label="Export CSV"
          @refresh="refresh"
          @create="exportCsv"
        />

        <div class="planner-table-wrap">
          <UTable :data="rows" :columns="columns" :loading="loading" />
        </div>
      </UiRegisterPanel>
    </section>
  </AppShell>
</template>

<style scoped>
.stock-report-filters {
  display: grid;
  grid-template-columns: minmax(12rem, 0.7fr) repeat(3, minmax(12rem, 1fr));
  gap: 12px;
  align-items: end;
}

.planner-metric-card strong {
  font-size: 20px;
  white-space: nowrap;
}

.stock-report-summary-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 16px;
}

.stock-bucket-list {
  display: grid;
  gap: 6px;
}

.stock-bucket-row {
  display: flex;
  min-width: 0;
  width: 100%;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  border: 1px solid transparent;
  border-radius: 6px;
  padding: 9px 10px;
  color: inherit;
  background: color-mix(in srgb, var(--ui-bg-elevated) 76%, transparent);
  text-align: left;
}

.stock-bucket-row:hover,
.stock-bucket-row.active {
  border-color: color-mix(in srgb, var(--ui-primary) 45%, transparent);
  background: color-mix(in srgb, var(--ui-primary) 10%, var(--ui-bg-elevated));
}

.stock-bucket-row span {
  display: grid;
  min-width: 0;
  gap: 3px;
}

.stock-bucket-row small {
  color: var(--ui-text-muted);
}

.stock-bucket-row b {
  flex: 0 0 auto;
  font-size: 13px;
}

@media (max-width: 1024px) {
  .stock-report-filters {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .stock-report-summary-grid {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 640px) {
  .stock-report-filters {
    grid-template-columns: 1fr;
  }

  .stock-bucket-row {
    align-items: flex-start;
    flex-direction: column;
  }
}
</style>
