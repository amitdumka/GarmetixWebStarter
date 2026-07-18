<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-arrow-right-left" class="size-4" /> Stock operations</p>
          <h2 class="garmetix-dashboard-title">Stock Operations</h2>
          <p class="garmetix-dashboard-subtitle">Adjustments, transfers and stock movement history.</p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton icon="i-lucide-arrow-right-left" color="neutral" variant="soft" @click="startTransfer">Transfer</UButton>
          <UButton icon="i-lucide-plus" color="primary" variant="solid" @click="startAdjustment">New Adjustment</UButton>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />
    <UAlert v-if="message" color="success" variant="subtle" icon="i-lucide-circle-check" :description="message" />

    <section class="garmetix-section-card">
      <div class="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <h3 class="garmetix-panel-title">Email Low Stock Digest</h3>
          <p class="garmetix-panel-subtitle">Sends the current Critical/Low risk items to a recipient via Communication & Mail.</p>
        </div>
        <div class="flex flex-wrap items-center gap-2">
          <UInput v-model="digestEmail" type="email" placeholder="recipient@example.com" class="w-64" />
          <UButton icon="i-lucide-mail" color="primary" variant="soft" :loading="sendingDigest" @click="sendLowStockDigest">Send Digest</UButton>
        </div>
      </div>
    </section>

    <section class="garmetix-section-card">
      <div class="mb-3 flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h3 class="garmetix-panel-title">Stock Operation Documents</h3>
          <p class="garmetix-panel-subtitle">{{ filteredRows.length }} of {{ documents.length }} documents</p>
        </div>
        <UInput v-model="search" icon="i-lucide-search" placeholder="Search document number" class="sm:w-72" />
      </div>

      <AdminMasterTable :columns="columns" :rows="filteredRows" empty-text="No stock operations found.">
        <template #actions="{ row }">
          <UButton icon="i-lucide-eye" size="xs" color="neutral" variant="ghost" :loading="detailLoading === row.id" @click="viewDocument(row.id)">View</UButton>
        </template>
      </AdminMasterTable>
    </section>

    <UModal v-model:open="adjustmentOpen" title="New Stock Adjustment">
      <template #body>
        <form class="grid gap-3" @submit.prevent="saveAdjustment">
          <label class="space-y-1 text-sm">
            <span class="text-muted">Product / stock</span>
            <USelectMenu v-model="adjustmentForm.stockId" value-key="value" :items="productSelectItems" placeholder="Search product..." class="w-full" />
          </label>
          <div class="grid grid-cols-2 gap-3">
            <label class="space-y-1 text-sm">
              <span class="text-muted">Quantity</span>
              <UInput v-model.number="adjustmentForm.quantity" type="number" min="0" step="1" class="w-full" />
            </label>
            <label class="space-y-1 text-sm">
              <span class="text-muted">Direction</span>
              <USelect v-model="adjustmentForm.direction" :items="directionItems" class="w-full" />
            </label>
          </div>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Reason</span>
            <UInput v-model="adjustmentForm.reason" placeholder="e.g. Damaged goods, inventory count..." class="w-full" />
          </label>

          <div class="flex justify-end gap-2">
            <UButton type="submit" icon="i-lucide-save" color="primary" :loading="saving">Save Adjustment</UButton>
          </div>
        </form>
      </template>
    </UModal>

    <UModal v-model:open="transferOpen" title="Stock Transfer">
      <template #body>
        <form class="grid gap-3" @submit.prevent="saveTransfer">
          <label class="space-y-1 text-sm">
            <span class="text-muted">Product / stock (from)</span>
            <USelectMenu v-model="transferForm.fromStockId" value-key="value" :items="productSelectItems" placeholder="Search product..." class="w-full" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Destination store</span>
            <USelect v-model="transferForm.toStoreId" :items="storeSelectItems" placeholder="Select store" class="w-full" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Quantity</span>
            <UInput v-model.number="transferForm.quantity" type="number" min="0" step="1" class="w-full" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Reason</span>
            <UInput v-model="transferForm.reason" placeholder="Optional reason" class="w-full" />
          </label>

          <div class="flex justify-end gap-2">
            <UButton type="submit" icon="i-lucide-save" color="primary" :loading="saving">Save Transfer</UButton>
          </div>
        </form>
      </template>
    </UModal>

    <UModal v-model:open="detailOpen" title="Stock Operation Detail" :ui="{ content: 'w-[calc(100vw-2rem)] sm:max-w-2xl' }">
      <template #body>
        <div v-if="detail" class="space-y-3">
          <dl class="grid gap-3 sm:grid-cols-2 text-sm">
            <div><dt class="text-xs text-muted">Document</dt><dd class="font-medium">{{ detail.documentNumber }}</dd></div>
            <div><dt class="text-xs text-muted">Date</dt><dd class="font-medium">{{ formatDate(detail.onDate) }}</dd></div>
            <div><dt class="text-xs text-muted">Type</dt><dd class="font-medium">{{ detail.operationType }}</dd></div>
            <div><dt class="text-xs text-muted">Status</dt><dd class="font-medium">{{ detail.status }}</dd></div>
            <div><dt class="text-xs text-muted">From Store</dt><dd class="font-medium">{{ detail.fromStoreName || '-' }}</dd></div>
            <div><dt class="text-xs text-muted">To Store</dt><dd class="font-medium">{{ detail.toStoreName || '-' }}</dd></div>
            <div class="sm:col-span-2"><dt class="text-xs text-muted">Reason</dt><dd class="font-medium">{{ detail.reason || '-' }}</dd></div>
          </dl>

          <AdminMasterTable :columns="itemColumns" :rows="itemRows" empty-text="No items on this document." />
        </div>
      </template>
    </UModal>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney } from '@garmetix/shared-utils'
import { formatDateTime, readNumber, readText, toRows, type ApiRecord, useAdminApiClient } from '../utils/admin-api'

useHead({ title: 'Stock Operations - Garmetix Inventory' })

function formatDate(value: unknown) {
  return formatDateTime(value)
}

const { get, post } = useAdminApiClient()
const loading = ref(true)
const saving = ref(false)
const detailLoading = ref('')
const error = ref('')
const message = ref('')
const search = ref('')
const digestEmail = ref('')
const sendingDigest = ref(false)
const documents = ref<ApiRecord[]>([])
const products = ref<ApiRecord[]>([])
const stores = ref<ApiRecord[]>([])
const detail = ref<ApiRecord | null>(null)

const adjustmentOpen = ref(false)
const transferOpen = ref(false)
const detailOpen = ref(false)

const adjustmentForm = reactive({ stockId: '', quantity: 1, direction: 'increase', reason: '' })
const transferForm = reactive({ fromStockId: '', toStoreId: '', quantity: 1, reason: '' })

const directionItems = [
  { label: 'Increase (+)', value: 'increase' },
  { label: 'Decrease (-)', value: 'decrease' }
]

const productSelectItems = computed(() => products.value
  .map(item => ({ label: readText(item, ['label'], `${readText(item, ['productName'])} - ${readText(item, ['barcode'])}`), value: readText(item, ['stockId'], '') }))
  .filter(item => item.value))
const storeSelectItems = computed(() => stores.value
  .map(item => ({ label: readText(item, ['name']), value: readText(item, ['id'], '') }))
  .filter(item => item.value))

const tableRows = computed(() => documents.value.map(item => ({
  id: readText(item, ['id'], ''),
  documentNumber: readText(item, ['documentNumber']),
  operationType: readText(item, ['operationType']),
  reason: readText(item, ['reason']),
  date: formatDateTime(item.onDate),
  quantity: readNumber(item, ['totalQuantity'])
})))
const filteredRows = computed(() => {
  const term = search.value.trim().toLowerCase()
  if (!term) return tableRows.value
  return tableRows.value.filter(row => JSON.stringify(row).toLowerCase().includes(term))
})
const columns = [
  { key: 'documentNumber', label: 'Doc #' },
  { key: 'operationType', label: 'Type' },
  { key: 'reason', label: 'Reason' },
  { key: 'quantity', label: 'Quantity' },
  { key: 'date', label: 'Date' }
]

const itemRows = computed(() => readArrayFrom(detail.value, 'items').map(item => ({
  product: readText(item, ['productName']),
  barcode: readText(item, ['barcode']),
  qtyIn: readNumber(item, ['quantityIn']),
  qtyOut: readNumber(item, ['quantityOut']),
  value: formatIndianMoney(readNumber(item, ['costValue']))
})))
const itemColumns = [
  { key: 'product', label: 'Product' },
  { key: 'barcode', label: 'Barcode' },
  { key: 'qtyIn', label: 'Qty In' },
  { key: 'qtyOut', label: 'Qty Out' },
  { key: 'value', label: 'Cost Value' }
]

function readArrayFrom(source: ApiRecord | null, key: string): ApiRecord[] {
  const value = source?.[key]
  return Array.isArray(value) ? value as ApiRecord[] : []
}

async function sendLowStockDigest() {
  if (!digestEmail.value.trim()) {
    error.value = 'Enter a recipient email address first.'
    return
  }
  sendingDigest.value = true
  error.value = ''
  message.value = ''
  try {
    const result = await post<ApiRecord>('inventory/stock-reports/low-stock-alert/send-email', {
      recipientEmail: digestEmail.value.trim(),
      recipientName: null,
      lowStockThreshold: null
    })
    if (result?.enqueued) {
      message.value = `Low stock digest queued (${result.itemCount ?? 0} item(s)).`
    } else {
      error.value = readText(result, ['skipReason'], 'Could not queue the low stock digest.')
    }
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to queue the low stock digest.'
  } finally {
    sendingDigest.value = false
  }
}

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const [documentData, optionsData] = await Promise.allSettled([
      get<unknown>('inventory/stock-operations/documents', { take: 150 }),
      get<ApiRecord>('inventory/stock-operations/options')
    ])
    if (documentData.status === 'fulfilled') documents.value = toRows(documentData.value)
    if (optionsData.status === 'fulfilled') {
      products.value = toRows((optionsData.value as ApiRecord)?.products)
      stores.value = toRows((optionsData.value as ApiRecord)?.stores)
    }

    if (documentData.status === 'rejected') {
      error.value = documentData.reason instanceof Error ? documentData.reason.message : 'Unable to load stock operations.'
    }
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load stock operations.'
  } finally {
    loading.value = false
  }
}

function startAdjustment() {
  Object.assign(adjustmentForm, { stockId: productSelectItems.value[0]?.value || '', quantity: 1, direction: 'increase', reason: '' })
  message.value = ''
  error.value = ''
  adjustmentOpen.value = true
}

function startTransfer() {
  Object.assign(transferForm, { fromStockId: productSelectItems.value[0]?.value || '', toStoreId: storeSelectItems.value[0]?.value || '', quantity: 1, reason: '' })
  message.value = ''
  error.value = ''
  transferOpen.value = true
}

async function saveAdjustment() {
  saving.value = true
  error.value = ''
  message.value = ''
  try {
    if (!adjustmentForm.stockId) throw new Error('Select a product.')
    if (!adjustmentForm.quantity || adjustmentForm.quantity <= 0) throw new Error('Enter a quantity greater than zero.')

    await post<unknown>('inventory/stock-operations/adjustment', {
      stockId: adjustmentForm.stockId,
      quantity: Math.abs(Number(adjustmentForm.quantity)),
      direction: adjustmentForm.direction,
      reason: adjustmentForm.reason.trim() || null
    })
    message.value = 'Stock adjustment saved.'
    adjustmentOpen.value = false
    await refresh()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to save adjustment.'
  } finally {
    saving.value = false
  }
}

async function saveTransfer() {
  saving.value = true
  error.value = ''
  message.value = ''
  try {
    if (!transferForm.fromStockId) throw new Error('Select a product.')
    if (!transferForm.toStoreId) throw new Error('Select a destination store.')
    if (!transferForm.quantity || transferForm.quantity <= 0) throw new Error('Enter a quantity greater than zero.')

    await post<unknown>('inventory/stock-operations/transfer', {
      fromStockId: transferForm.fromStockId,
      toStoreId: transferForm.toStoreId,
      quantity: Math.abs(Number(transferForm.quantity)),
      reason: transferForm.reason.trim() || null
    })
    message.value = 'Stock transfer saved.'
    transferOpen.value = false
    await refresh()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to save transfer.'
  } finally {
    saving.value = false
  }
}

async function viewDocument(id: string) {
  if (!id) return
  detailLoading.value = id
  error.value = ''
  try {
    detail.value = await get<ApiRecord>(`inventory/stock-operations/documents/${id}`)
    detailOpen.value = true
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load document detail.'
  } finally {
    detailLoading.value = ''
  }
}

onMounted(refresh)
</script>
