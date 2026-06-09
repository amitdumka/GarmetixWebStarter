<script setup lang="ts">
import { h, resolveComponent } from 'vue'
import type { TableColumn } from '@nuxt/ui'

const api = useGarmetixApi()
const auth = useAuth()
const feedback = useUiFeedback()
const isAuthenticated = auth.isAuthenticated
const canEdit = auth.canEdit

const UBadge = resolveComponent('UBadge')

const loading = ref(false)
const posting = ref(false)
const options = ref<any>({ products: [], stores: [], recentMovements: [] })
const companies = ref<any[]>([])
const stores = ref<any[]>([])
const activeTab = ref('adjustment')

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

const metrics = computed(() => {
  const productCount = options.value.products?.length || 0
  const totalStock = (options.value.products || []).reduce((sum: number, item: any) => sum + Number(item.currentStock || 0), 0)
  const movements = options.value.recentMovements?.length || 0
  return [
    { label: 'Stock rows', value: productCount, meta: 'Store-wise product stock', icon: 'i-lucide-boxes', color: 'primary' },
    { label: 'Current stock', value: totalStock, meta: 'Across scoped stores', icon: 'i-lucide-warehouse', color: totalStock > 0 ? 'success' : 'warning' },
    { label: 'Recent movements', value: movements, meta: 'Latest ledger rows', icon: 'i-lucide-history', color: 'neutral' }
  ]
})

const movementRows = computed(() => (options.value.recentMovements || []).map((item: any) => ({
  ...item,
  onDateText: new Date(item.onDate).toLocaleString(),
  qty: Number(item.quantityIn || 0) > 0 ? `+${item.quantityIn}` : `-${item.quantityOut}`,
  valueText: money(Number(item.mrp || 0) * (Number(item.quantityIn || 0) || Number(item.quantityOut || 0)))
})))

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
  { accessorKey: 'sourceNumber', header: 'Reference' },
  { accessorKey: 'remarks', header: 'Remarks' }
]

async function refresh() {
  if (!auth.isAuthenticated.value) {
    return
  }

  loading.value = true
  try {
    const [companyRows, storeRows, optionRows] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores'),
      api.get<any>('inventory/stock-operations/options')
    ])
    companies.value = companyRows
    stores.value = storeRows
    options.value = optionRows
  } catch (error) {
    feedback.failed('Stock operations refresh failed', error)
  } finally {
    loading.value = false
  }
}

async function postAdjustment() {
  posting.value = true
  try {
    await api.create<any>('inventory/stock-operations/adjustment', {
      stockId: adjustmentForm.stockId,
      direction: adjustmentForm.direction,
      quantity: Number(adjustmentForm.quantity || 0),
      reason: nullIfEmpty(adjustmentForm.reason)
    })
    feedback.saved('Stock adjustment')
    adjustmentForm.quantity = 0
    adjustmentForm.reason = ''
    await refresh()
  } catch (error) {
    feedback.failed('Could not post stock adjustment', error)
  } finally {
    posting.value = false
  }
}

async function postTransfer() {
  posting.value = true
  try {
    await api.create<any>('inventory/stock-operations/transfer', {
      fromStockId: transferForm.fromStockId,
      toStoreId: transferForm.toStoreId,
      quantity: Number(transferForm.quantity || 0),
      reason: nullIfEmpty(transferForm.reason)
    })
    feedback.saved('Stock transfer')
    transferForm.quantity = 0
    transferForm.reason = ''
    await refresh()
  } catch (error) {
    feedback.failed('Could not post stock transfer', error)
  } finally {
    posting.value = false
  }
}

async function postPhysicalCount() {
  posting.value = true
  try {
    await api.create<any>('inventory/stock-operations/physical-count', {
      stockId: countForm.stockId,
      countedQuantity: Number(countForm.countedQuantity || 0),
      reason: nullIfEmpty(countForm.reason)
    })
    feedback.saved('Physical count')
    countForm.reason = ''
    await refresh()
  } catch (error) {
    feedback.failed('Could not post physical count', error)
  } finally {
    posting.value = false
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

function money(value: number) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(value || 0)
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
        description="Stage 4B: adjustment, transfer, and physical count posting through the stock movement ledger."
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
            <UBadge color="primary" variant="subtle">Stage 4B</UBadge>
          </div>
        </template>

        <UTabs v-model="activeTab" :items="[
          { label: 'Adjustment', value: 'adjustment', icon: 'i-lucide-plus-minus' },
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

      <UCard class="planner-card">
        <template #header>
          <div class="planner-card-header">
            <div>
              <h2>Recent Stock Movement Ledger</h2>
              <p>Every adjustment, transfer, physical count, sale, purchase, and return should leave a movement row.</p>
            </div>
            <UBadge color="neutral" variant="subtle">{{ movementRows.length }} rows</UBadge>
          </div>
        </template>

        <UTable v-if="movementRows.length" :data="movementRows" :columns="columns" :loading="loading" />
        <UiCrudEmptyState
          v-else
          title="No stock movement yet"
          description="Post an operation or create product opening stock to start the ledger."
          icon="i-lucide-history"
        />
      </UCard>
    </section>
  </AppShell>
</template>
