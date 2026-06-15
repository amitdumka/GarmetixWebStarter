<script setup lang="ts">
import { h, resolveComponent } from 'vue'
import type { TableColumn } from '@nuxt/ui'

const api = useGarmetixApi()
const auth = useAuth()
const feedback = useUiFeedback()
const config = useRuntimeConfig()
const route = useRoute()
const router = useRouter()
const isAuthenticated = auth.isAuthenticated
const canEdit = auth.canEdit

const UBadge = resolveComponent('UBadge')
const UButton = resolveComponent('UButton')

const loading = ref(false)
const loadError = ref('')
const movementSearch = ref('')
const documentSearch = ref('')
const valuationSearch = ref('')
const valuationStatusFilter = ref('all')
const operationTypeFilter = ref('all')
const posting = ref(false)
const options = ref<any>({ products: [], stores: [], recentMovements: [] })
const documents = ref<any[]>([])
const valuation = ref<any>({
  valuationMethod: 'WeightedAverage',
  stockRows: 0,
  totalQuantity: 0,
  totalInventoryValue: 0,
  projectionMismatchCount: 0,
  rows: []
})
const companies = ref<any[]>([])
const stores = ref<any[]>([])
const activeTab = ref('adjustment')
const detailOpen = ref(false)
const detailLoading = ref(false)
const qrBusy = ref(false)
const selectedDocument = ref<any | null>(null)
const openedRouteDocumentId = ref('')

const adjustmentForm = reactive({ stockId: '', direction: 'increase', quantity: 0, reason: '' })
const transferForm = reactive({ fromStockId: '', toStoreId: '', quantity: 0, reason: '' })
const countForm = reactive({ stockId: '', countedQuantity: 0, reason: '' })

const stockOptions = computed(() => (options.value.products || []).map((item: any) => ({
  value: item.stockId,
  label: item.label || `${item.productName} | ${item.barcode}`
})))

const storeOptions = computed(() => (options.value.stores || []).map((item: any) => ({ value: item.id, label: item.name })))

const selectedAdjustmentStock = computed(() => findStock(adjustmentForm.stockId))
const selectedTransferStock = computed(() => findStock(transferForm.fromStockId))
const selectedCountStock = computed(() => findStock(countForm.stockId))
const destinationStoreOptions = computed(() => storeOptions.value.filter((item: any) => item.value !== selectedTransferStock.value?.storeId))
const selectedHasMovement = computed(() => (selectedDocument.value?.items || [])
  .some((item: any) => item.inMovementId || item.outMovementId))

const metrics = computed(() => {
  const movements = options.value.recentMovements?.length || 0
  return [
    { label: 'Ledger quantity', value: Number(valuation.value.totalQuantity || 0).toFixed(2), meta: `${valuation.value.stockRows || 0} stock rows`, icon: 'i-lucide-warehouse', color: 'primary' },
    { label: 'Inventory value', value: money(Number(valuation.value.totalInventoryValue || 0)), meta: 'Weighted-average cost', icon: 'i-lucide-indian-rupee', color: 'success' },
    { label: 'Projection checks', value: valuation.value.projectionMismatchCount || 0, meta: 'Rows requiring rebuild', icon: 'i-lucide-scale', color: valuation.value.projectionMismatchCount ? 'warning' : 'success' },
    { label: 'Recent movements', value: movements, meta: `${documents.value.length} formal documents`, icon: 'i-lucide-history', color: 'neutral' }
  ]
})

const valuationRows = computed(() => (valuation.value.rows || [])
  .filter((item: any) => valuationStatusFilter.value === 'all' || item.projectionStatus === valuationStatusFilter.value)
  .filter((item: any) => {
    const term = valuationSearch.value.trim().toLowerCase()
    return !term || [item.productName, item.barcode, item.storeName, item.projectionStatus]
      .some(value => String(value || '').toLowerCase().includes(term))
  })
  .map((item: any) => ({
    ...item,
    ledgerQuantityText: Number(item.ledgerQuantity || 0).toFixed(2),
    projectedQuantityText: Number(item.projectedQuantity || 0).toFixed(2),
    averageCostText: money(Number(item.averageCost || 0)),
    inventoryValueText: money(Number(item.inventoryValue || 0)),
    lastMovementText: item.lastMovementAt ? formatDateTime(item.lastMovementAt) : '-'
  })))

const documentRows = computed(() => documents.value
  .filter((item: any) => operationTypeFilter.value === 'all' || item.operationType === operationTypeFilter.value)
  .filter((item: any) => {
    const term = documentSearch.value.trim().toLowerCase()
    return !term || [
      item.documentNumber,
      item.operationType,
      item.status,
      item.fromStoreName,
      item.toStoreName,
      item.reason
    ].some(value => String(value || '').toLowerCase().includes(term))
  })
  .map((item: any) => ({
    ...item,
    onDateText: formatDateTime(item.onDate),
    storePath: item.toStoreName ? `${item.fromStoreName || 'Store'} to ${item.toStoreName}` : item.fromStoreName || 'Store',
    totalQuantityText: Number(item.totalQuantity || 0).toFixed(2),
    totalCostValueText: money(Number(item.totalCostValue || 0))
  })))

const movementRows = computed(() => (options.value.recentMovements || []).map((item: any) => ({
  ...item,
  onDateText: new Date(item.onDate).toLocaleString(),
  qty: Number(item.quantityIn || 0) > 0 ? `+${item.quantityIn}` : `-${item.quantityOut}`,
  balanceText: Number(item.quantityAfter || 0).toFixed(2),
  averageCostText: money(Number(item.averageCostAfter || 0)),
  inventoryValueText: money(Number(item.inventoryValueAfter || 0))
})))

const filteredMovementRows = computed(() => {
  const term = movementSearch.value.trim().toLowerCase()
  return term
    ? movementRows.value.filter((row: any) => JSON.stringify(row).toLowerCase().includes(term))
    : movementRows.value
})

const columns: TableColumn<any>[] = [
  { accessorKey: 'onDateText', header: 'Date' },
  {
    accessorKey: 'movementType',
    header: 'Type',
    cell: ({ row }) => h(UBadge, { color: movementColor(row.original.movementType), variant: 'subtle' }, () => row.original.movementType)
  },
  { accessorKey: 'productName', header: 'Product' },
  { accessorKey: 'barcode', header: 'Barcode' },
  { accessorKey: 'storeName', header: 'Store' },
  { accessorKey: 'qty', header: 'Qty' },
  { accessorKey: 'balanceText', header: 'Balance' },
  { accessorKey: 'averageCostText', header: 'Avg Cost' },
  { accessorKey: 'inventoryValueText', header: 'Stock Value' },
  { accessorKey: 'sourceNumber', header: 'Reference' },
  { accessorKey: 'remarks', header: 'Remarks' }
]

const valuationColumns: TableColumn<any>[] = [
  { accessorKey: 'productName', header: 'Product' },
  { accessorKey: 'barcode', header: 'Barcode' },
  { accessorKey: 'storeName', header: 'Store' },
  { accessorKey: 'ledgerQuantityText', header: 'Ledger Qty' },
  { accessorKey: 'projectedQuantityText', header: 'Projected Qty' },
  { accessorKey: 'averageCostText', header: 'Average Cost' },
  { accessorKey: 'inventoryValueText', header: 'Inventory Value' },
  { accessorKey: 'lastMovementText', header: 'Last Movement' },
  {
    accessorKey: 'projectionStatus',
    header: 'Projection',
    cell: ({ row }) => h(UBadge, {
      color: row.original.projectionStatus === 'Matched' ? 'success' : 'warning',
      variant: 'subtle'
    }, () => row.original.projectionStatus)
  }
]

const documentColumns: TableColumn<any>[] = [
  { accessorKey: 'documentNumber', header: 'Document No.' },
  { accessorKey: 'onDateText', header: 'Date' },
  {
    accessorKey: 'operationType',
    header: 'Operation',
    cell: ({ row }) => h(UBadge, { color: operationColor(row.original.operationType), variant: 'subtle' }, () => operationLabel(row.original.operationType))
  },
  { accessorKey: 'storePath', header: 'Store / Transfer' },
  { accessorKey: 'totalQuantityText', header: 'Qty' },
  { accessorKey: 'totalCostValueText', header: 'Cost Value' },
  {
    accessorKey: 'status',
    header: 'Status',
    cell: ({ row }) => h(UBadge, { color: row.original.status === 'Verified' ? 'info' : 'success', variant: 'subtle' }, () => row.original.status)
  },
  {
    id: 'actions',
    header: '',
    cell: ({ row }) => h(UButton, {
      icon: 'i-lucide-eye',
      color: 'neutral',
      variant: 'ghost',
      'aria-label': `View ${row.original.documentNumber}`,
      onClick: () => openDocument(row.original.id)
    })
  }
]

async function refresh() {
  if (!auth.isAuthenticated.value) {
    return
  }

  loading.value = true
  loadError.value = ''
  try {
    const [companyRows, storeRows, optionRows, documentRows, valuationRows] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores'),
      api.get<any>('inventory/stock-operations/options'),
      api.get<any[]>('inventory/stock-operations/documents?take=150'),
      api.get<any>('inventory/stock-operations/valuation?take=500')
    ])
    companies.value = companyRows
    stores.value = storeRows
    options.value = optionRows
    documents.value = documentRows
    valuation.value = valuationRows
    await openRouteDocument()
  } catch (error) {
    loadError.value = 'Stock-operation options and movement history could not be loaded. Try again.'
    feedback.failed('Stock operations refresh failed', error)
  } finally {
    loading.value = false
  }
}

async function postAdjustment() {
  posting.value = true
  try {
    const result = await api.create<any>('inventory/stock-operations/adjustment', {
      stockId: adjustmentForm.stockId,
      direction: adjustmentForm.direction,
      quantity: Number(adjustmentForm.quantity || 0),
      reason: nullIfEmpty(adjustmentForm.reason)
    })
    feedback.saved('Stock adjustment')
    adjustmentForm.quantity = 0
    adjustmentForm.reason = ''
    await refresh()
    await openDocument(result.documentId)
  } catch (error) {
    feedback.failed('Could not post stock adjustment', error)
  } finally {
    posting.value = false
  }
}

async function postTransfer() {
  posting.value = true
  try {
    const result = await api.create<any>('inventory/stock-operations/transfer', {
      fromStockId: transferForm.fromStockId,
      toStoreId: transferForm.toStoreId,
      quantity: Number(transferForm.quantity || 0),
      reason: nullIfEmpty(transferForm.reason)
    })
    feedback.saved('Stock transfer')
    transferForm.quantity = 0
    transferForm.reason = ''
    await refresh()
    await openDocument(result.documentId)
  } catch (error) {
    feedback.failed('Could not post stock transfer', error)
  } finally {
    posting.value = false
  }
}

async function postPhysicalCount() {
  posting.value = true
  try {
    const result = await api.create<any>('inventory/stock-operations/physical-count', {
      stockId: countForm.stockId,
      countedQuantity: Number(countForm.countedQuantity || 0),
      reason: nullIfEmpty(countForm.reason)
    })
    feedback.saved('Physical count')
    countForm.reason = ''
    await refresh()
    await openDocument(result.documentId)
  } catch (error) {
    feedback.failed('Could not post physical count', error)
  } finally {
    posting.value = false
  }
}

async function openDocument(id: string) {
  if (!id) return
  detailLoading.value = true
  detailOpen.value = true
  try {
    selectedDocument.value = await api.get<any>(`inventory/stock-operations/documents/${id}`)
    openedRouteDocumentId.value = id
    if (route.query.documentId !== id) {
      await router.replace({ query: { ...route.query, documentId: id } })
    }
  } catch (error) {
    detailOpen.value = false
    feedback.failed('Could not load stock operation document', error)
  } finally {
    detailLoading.value = false
  }
}

async function openRouteDocument() {
  const id = String(route.query.documentId || '')
  if (!id || id === openedRouteDocumentId.value) return
  await openDocument(id)
}

async function closeDocument() {
  detailOpen.value = false
  selectedDocument.value = null
  openedRouteDocumentId.value = ''
  const query = { ...route.query }
  delete query.documentId
  await router.replace({ query })
}

function handleDetailOpen(value: boolean) {
  if (!value) void closeDocument()
}

async function downloadQr() {
  if (!selectedDocument.value?.id) return
  qrBusy.value = true
  try {
    const response = await fetch(
      `${config.public.apiBase}/scan/qr/STOCKOPERATION/${selectedDocument.value.id}.svg`,
      { headers: api.authHeaders() as HeadersInit }
    )
    if (!response.ok) throw new Error('QR code could not be generated.')
    const blob = await response.blob()
    const href = URL.createObjectURL(blob)
    const anchor = document.createElement('a')
    anchor.href = href
    anchor.download = `${selectedDocument.value.documentNumber.replaceAll('/', '-')}-QR.svg`
    anchor.click()
    URL.revokeObjectURL(href)
  } catch (error) {
    feedback.failed('Could not download stock document QR', error)
  } finally {
    qrBusy.value = false
  }
}

function useSelectedCurrentForCount() {
  countForm.countedQuantity = Number(selectedCountStock.value?.currentStock || 0)
}

function findStock(stockId: string) {
  return (options.value.products || []).find((item: any) => item.stockId === stockId)
}

function nullIfEmpty(value: unknown) {
  const text = String(value || '').trim()
  return text ? text : null
}

function movementColor(type: string) {
  if (String(type).includes('Out') || String(type).includes('Loss')) return 'warning'
  if (String(type).includes('In') || String(type).includes('Gain')) return 'success'
  return 'neutral'
}

function operationColor(type: string) {
  if (type === 'Transfer') return 'info'
  if (type === 'PhysicalCount') return 'warning'
  return 'primary'
}

function operationLabel(type: string) {
  return type === 'PhysicalCount' ? 'Physical Count' : type
}

function money(value: number) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(value || 0)
}

function formatDateTime(value: string) {
  if (!value) return '-'
  return new Date(value).toLocaleString('en-IN')
}

watch(() => transferForm.fromStockId, () => {
  const source = selectedTransferStock.value
  if (source && transferForm.toStoreId === source.storeId) {
    transferForm.toStoreId = ''
  }
})

watch(() => countForm.stockId, () => {
  useSelectedCurrentForCount()
})

watch(() => route.query.documentId, () => {
  void openRouteDocument()
})

onMounted(async () => {
  auth.restore()
  await refresh()
})
</script>

<template>
  <AuthScreen v-if="!isAuthenticated" @authenticated="refresh" />

  <AppShell
    v-else
    title="Stock Operations"
    :companies="companies"
    :stores="stores"
    @refresh="refresh"
  >
    <section class="planner-dashboard">
      <UiModulePageHeader
        title="Stock Operations"
        description="Post and review formal stock adjustment, transfer and physical-count documents with linked movement history."
        icon="i-lucide-arrow-left-right"
        primary-label="Refresh"
        primary-icon="i-lucide-refresh-cw"
        @primary="refresh"
      >
        <template #actions>
          <UBadge :color="loading ? 'warning' : 'success'" variant="subtle">
            {{ loading ? 'Loading' : 'Ledger ready' }}
          </UBadge>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="subtle" :loading="loading" label="Refresh" @click="refresh" />
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
              <h2>Post Stock Operation</h2>
              <p>Use stock operations for non-sale/non-purchase changes so audit history stays complete.</p>
            </div>
            <UBadge color="primary" variant="subtle">Stock operations</UBadge>
          </div>
        </template>

        <UTabs v-model="activeTab" :items="[
          { label: 'Adjustment', value: 'adjustment', icon: 'i-lucide-sliders-horizontal' },
          { label: 'Transfer', value: 'transfer', icon: 'i-lucide-arrow-left-right' },
          { label: 'Physical Count', value: 'count', icon: 'i-lucide-clipboard-check' }
        ]" />

        <div v-if="activeTab === 'adjustment'" class="form-grid mt-4">
          <UFormField label="Stock item" required>
            <USelect v-model="adjustmentForm.stockId" :items="stockOptions" placeholder="Select product/barcode/store" />
          </UFormField>
          <UFormField label="Direction" required>
            <USelect v-model="adjustmentForm.direction" :items="[
              { value: 'increase', label: 'Increase stock' },
              { value: 'decrease', label: 'Decrease stock' }
            ]" />
          </UFormField>
          <UFormField label="Quantity" required>
            <UInput v-model="adjustmentForm.quantity" type="number" min="0" step="1" />
          </UFormField>
          <UFormField label="Reason">
            <UTextarea v-model="adjustmentForm.reason" :rows="2" placeholder="Damage, correction, manual adjustment" />
          </UFormField>
          <div class="flex items-center justify-between gap-3">
            <p class="text-sm text-muted">Current: {{ selectedAdjustmentStock?.currentStock ?? '-' }}</p>
            <UButton :disabled="!canEdit" :loading="posting" label="Post Adjustment" icon="i-lucide-save" @click="postAdjustment" />
          </div>
        </div>

        <div v-else-if="activeTab === 'transfer'" class="form-grid mt-4">
          <UFormField label="Source stock" required>
            <USelect v-model="transferForm.fromStockId" :items="stockOptions" placeholder="Select source stock" />
          </UFormField>
          <UFormField label="Destination store" required>
            <USelect v-model="transferForm.toStoreId" :items="destinationStoreOptions" placeholder="Select destination store" />
          </UFormField>
          <UFormField label="Quantity" required>
            <UInput v-model="transferForm.quantity" type="number" min="0" step="1" />
          </UFormField>
          <UFormField label="Reason">
            <UTextarea v-model="transferForm.reason" :rows="2" placeholder="Inter-store transfer" />
          </UFormField>
          <div class="flex items-center justify-between gap-3">
            <p class="text-sm text-muted">Available: {{ selectedTransferStock?.currentStock ?? '-' }}</p>
            <UButton :disabled="!canEdit" :loading="posting" label="Post Transfer" icon="i-lucide-send" @click="postTransfer" />
          </div>
        </div>

        <div v-else class="form-grid mt-4">
          <UFormField label="Stock item" required>
            <USelect v-model="countForm.stockId" :items="stockOptions" placeholder="Select counted stock" />
          </UFormField>
          <UFormField label="Counted quantity" required>
            <UInput v-model="countForm.countedQuantity" type="number" min="0" step="1" />
          </UFormField>
          <UFormField label="Reason">
            <UTextarea v-model="countForm.reason" :rows="2" placeholder="Physical verification / stock take" />
          </UFormField>
          <div class="flex items-center justify-between gap-3">
            <p class="text-sm text-muted">System: {{ selectedCountStock?.currentStock ?? '-' }}</p>
            <div class="flex gap-2">
              <UButton color="neutral" variant="subtle" label="Use System Qty" @click="useSelectedCurrentForCount" />
              <UButton :disabled="!canEdit" :loading="posting" label="Post Count" icon="i-lucide-clipboard-check" @click="postPhysicalCount" />
            </div>
          </div>
        </div>
      </UCard>

      <UiRegisterPanel
        title="Stock Operation Documents"
        description="Formal posted records preserve quantity, value, store, product and movement references for audit."
        :loading="loading"
        :error="loadError"
        :empty="!documentRows.length"
        empty-title="No stock operation document found"
        empty-description="Post an adjustment, transfer or physical count to create the first formal document."
        empty-icon="i-lucide-files"
        @retry="refresh"
      >
        <template #actions>
          <UBadge color="primary" variant="subtle">{{ documentRows.length }} documents</UBadge>
        </template>

        <div class="grid gap-3 md:grid-cols-[minmax(0,1fr)_13rem]">
          <UiCrudToolbar
            v-model:search="documentSearch"
            search-placeholder="Search document, operation, store, status or reason"
            :loading="loading"
            refresh-label="Refresh"
            @refresh="refresh"
          />
          <USelect
            v-model="operationTypeFilter"
            :items="[
              { label: 'All operations', value: 'all' },
              { label: 'Adjustments', value: 'Adjustment' },
              { label: 'Transfers', value: 'Transfer' },
              { label: 'Physical counts', value: 'PhysicalCount' }
            ]"
            aria-label="Filter stock operation type"
          />
        </div>
        <UTable v-if="documentRows.length" :data="documentRows" :columns="documentColumns" :loading="loading" />
      </UiRegisterPanel>

      <UiRegisterPanel
        title="Stock Valuation and Ledger Reconciliation"
        description="Ledger quantity and weighted-average valuation compared with the fast stock projection used by operational screens."
        :loading="loading"
        :error="loadError"
        :empty="!valuationRows.length"
        empty-title="No stock valuation rows found"
        empty-description="Stock valuation appears after an opening, purchase, transfer or adjustment movement."
        empty-icon="i-lucide-scale"
        @retry="refresh"
      >
        <template #actions>
          <UBadge :color="valuation.projectionMismatchCount ? 'warning' : 'success'" variant="subtle">
            {{ valuation.projectionMismatchCount || 0 }} mismatches
          </UBadge>
          <UBadge color="info" variant="subtle">Weighted Average</UBadge>
        </template>

        <div class="grid gap-3 md:grid-cols-[minmax(0,1fr)_13rem]">
          <UiCrudToolbar
            v-model:search="valuationSearch"
            search-placeholder="Search product, barcode, store or projection status"
            :loading="loading"
            refresh-label="Refresh"
            @refresh="refresh"
          />
          <USelect
            v-model="valuationStatusFilter"
            :items="[
              { label: 'All projection rows', value: 'all' },
              { label: 'Matched', value: 'Matched' },
              { label: 'Rebuild required', value: 'Rebuild Required' }
            ]"
            aria-label="Filter projection status"
          />
        </div>
        <UTable v-if="valuationRows.length" :data="valuationRows" :columns="valuationColumns" :loading="loading" />
      </UiRegisterPanel>

      <UiRegisterPanel
        title="Recent Stock Movement Ledger"
        description="Every adjustment, transfer, physical count, sale, purchase, and return should leave a movement row."
        :loading="loading"
        :error="loadError"
        :empty="!filteredMovementRows.length"
        empty-title="No stock movement found"
        empty-description="Post an operation or create product opening stock to start the ledger."
        empty-icon="i-lucide-history"
        @retry="refresh"
      >
        <template #actions>
            <UBadge color="neutral" variant="subtle">{{ movementRows.length }} rows</UBadge>
        </template>

        <UiCrudToolbar
          v-model:search="movementSearch"
          search-placeholder="Search product, barcode, store, reference, or remarks"
          :loading="loading"
          refresh-label="Refresh"
          @refresh="refresh"
        />
        <UTable v-if="filteredMovementRows.length" :data="filteredMovementRows" :columns="columns" :loading="loading" />
      </UiRegisterPanel>

      <UModal
        v-model:open="detailOpen"
        title="Stock Operation Document"
        :ui="{ content: 'sm:max-w-5xl xl:max-w-6xl' }"
        @update:open="handleDetailOpen"
      >
        <template #body>
          <div v-if="detailLoading" class="py-10 text-center text-sm text-muted">
            Loading stock document...
          </div>
          <div v-else-if="selectedDocument" class="space-y-4">
            <UAlert
              color="neutral"
              variant="subtle"
              icon="i-lucide-file-check-2"
              :title="`${selectedDocument.documentNumber} / ${operationLabel(selectedDocument.operationType)}`"
              :description="selectedDocument.toStoreName
                ? `${selectedDocument.fromStoreName || 'Store'} to ${selectedDocument.toStoreName}`
                : selectedDocument.fromStoreName || 'Stock operation'"
            />

            <div class="payroll-summary">
              <span>Operation date</span><strong>{{ formatDateTime(selectedDocument.onDate) }}</strong>
              <span>Posted at</span><strong>{{ formatDateTime(selectedDocument.postedAt) }}</strong>
              <span>Status</span><strong>{{ selectedDocument.status }}</strong>
              <span>Items</span><strong>{{ selectedDocument.itemCount }}</strong>
              <span>Total quantity</span><strong>{{ Number(selectedDocument.totalQuantity || 0).toFixed(2) }}</strong>
              <span>Cost value</span><strong>{{ money(selectedDocument.totalCostValue) }}</strong>
              <span>MRP value</span><strong>{{ money(selectedDocument.totalMrpValue) }}</strong>
              <span>Movement link</span><strong>{{ selectedHasMovement ? 'Linked' : 'No movement required' }}</strong>
            </div>

            <div class="planner-table-wrap">
              <table class="planner-table">
                <thead>
                  <tr>
                    <th>Product Snapshot</th>
                    <th>System</th>
                    <th>Counted</th>
                    <th>In</th>
                    <th>Out</th>
                    <th>Difference</th>
                    <th>Source Before / After</th>
                    <th>Destination Before / After</th>
                    <th>Cost Value</th>
                    <th>Movement Audit</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="item in selectedDocument.items || []" :key="item.id">
                    <td>
                      <div class="font-medium">{{ item.productName }}</div>
                      <div class="text-xs text-muted">{{ item.barcode }} | {{ item.hsnCode || 'No HSN' }} | {{ item.unit }}</div>
                    </td>
                    <td>{{ Number(item.systemQuantity || 0).toFixed(2) }}</td>
                    <td>{{ item.countedQuantity == null ? '-' : Number(item.countedQuantity).toFixed(2) }}</td>
                    <td>{{ Number(item.quantityIn || 0).toFixed(2) }}</td>
                    <td>{{ Number(item.quantityOut || 0).toFixed(2) }}</td>
                    <td>{{ Number(item.quantityDifference || 0).toFixed(2) }}</td>
                    <td>{{ Number(item.fromQuantityBefore || 0).toFixed(2) }} / {{ Number(item.fromQuantityAfter || 0).toFixed(2) }}</td>
                    <td>
                      {{ item.toQuantityBefore == null ? '-' : `${Number(item.toQuantityBefore).toFixed(2)} / ${Number(item.toQuantityAfter || 0).toFixed(2)}` }}
                    </td>
                    <td>{{ money(item.costValue) }}</td>
                    <td>
                      <UBadge :color="item.inMovementId || item.outMovementId ? 'success' : 'neutral'" variant="subtle">
                        {{ item.inMovementId || item.outMovementId ? 'Linked' : 'Not required' }}
                      </UBadge>
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>

            <UAlert
              color="info"
              variant="subtle"
              title="Reason"
              :description="selectedDocument.reason || 'No reason recorded.'"
            />
          </div>
        </template>
        <template #footer>
          <div class="flex w-full flex-wrap items-center justify-between gap-2">
            <UButton color="neutral" variant="outline" label="Close" @click="closeDocument" />
            <UButton
              icon="i-lucide-qr-code"
              color="neutral"
              variant="subtle"
              label="Download QR"
              :loading="qrBusy"
              @click="downloadQr"
            />
          </div>
        </template>
      </UModal>
    </section>
  </AppShell>
</template>
