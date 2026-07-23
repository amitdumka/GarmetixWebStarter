<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-scan-line" class="size-4" /> Physical stock counting</p>
          <h2 class="garmetix-dashboard-title">Stock Audit</h2>
          <p class="garmetix-dashboard-subtitle">Create an audit period, scan barcodes to count stock day by day, then review reports from Stock Audit Reports.</p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton icon="i-lucide-plus" color="primary" variant="solid" @click="startCreatePeriod">New Audit Period</UButton>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />
    <UAlert v-if="message" color="success" variant="subtle" icon="i-lucide-circle-check" :description="message" />

    <section class="garmetix-section-card">
      <div class="mb-3 flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h3 class="garmetix-panel-title">Audit Periods</h3>
          <p class="garmetix-panel-subtitle">{{ periods.length }} period(s)</p>
        </div>
      </div>

      <AdminMasterTable :columns="periodColumns" :rows="pagedPeriodRows" empty-text="No audit periods yet. Create one to start counting stock.">
        <template #actions="{ row }">
          <div class="flex flex-wrap justify-end gap-1">
            <UButton icon="i-lucide-scan-line" size="xs" color="primary" variant="soft" :disabled="row.statusRaw !== 'Open'" @click="selectPeriod(row.id)">Scan</UButton>
            <UButton v-if="row.statusRaw === 'Open'" icon="i-lucide-lock" size="xs" color="neutral" variant="ghost" @click="closePeriod(row.id)">Close</UButton>
            <UButton v-else icon="i-lucide-lock-open" size="xs" color="neutral" variant="ghost" @click="reopenPeriod(row.id)">Reopen</UButton>
            <UButton icon="i-lucide-pencil" size="xs" color="neutral" variant="ghost" @click="startEditPeriod(row.id)">Edit</UButton>
            <UButton icon="i-lucide-trash-2" size="xs" color="error" variant="ghost" @click="startDeletePeriod(row.id)">Delete</UButton>
          </div>
        </template>
      </AdminMasterTable>
      <div v-if="periodRows.length" class="mt-3 flex flex-col gap-2 text-sm text-muted sm:flex-row sm:items-center sm:justify-between">
        <p>Showing {{ pagedPeriodRows.length }} of {{ periodRows.length }} period(s)</p>
        <div class="flex items-center gap-2">
          <USelect v-model="periodsPageSize" :items="pageSizeOptions" class="w-28" />
          <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-left" :disabled="periodsPage <= 1" @click="periodsPage--">Prev</UButton>
          <span>{{ periodsPage }} / {{ periodsTotalPages }}</span>
          <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-right" :disabled="periodsPage >= periodsTotalPages" @click="periodsPage++">Next</UButton>
        </div>
      </div>
    </section>

    <section v-if="activePeriod" class="garmetix-section-card">
      <div class="mb-3 flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h3 class="garmetix-panel-title">Scan Entry - {{ activePeriod.name }}</h3>
          <p class="garmetix-panel-subtitle">{{ activePeriod.storeName }} &middot; {{ activePeriod.startDate }} to {{ activePeriod.endDate }} &middot; Counting for <strong>{{ todayLabel }}</strong></p>
        </div>
        <UBadge v-if="activePeriod.statusRaw !== 'Open'" color="warning" variant="soft">Closed</UBadge>
      </div>

      <form class="mb-4 flex flex-wrap items-end gap-3" @submit.prevent="submitScan">
        <label class="space-y-1 text-sm">
          <span class="text-muted">Barcode (scan or type)</span>
          <UInput ref="barcodeInputRef" v-model="scanForm.barcode" placeholder="Scan or enter barcode" autofocus class="w-56" :disabled="activePeriod.statusRaw !== 'Open'" />
        </label>
        <label class="space-y-1 text-sm">
          <span class="text-muted">Quantity</span>
          <UInput v-model.number="scanForm.quantity" type="number" min="1" step="1" class="w-24" :disabled="activePeriod.statusRaw !== 'Open'" />
        </label>
        <UButton type="submit" icon="i-lucide-scan-line" color="primary" :loading="scanning" :disabled="activePeriod.statusRaw !== 'Open'">Add Scan</UButton>
      </form>

      <UAlert v-if="lastScanNote" color="primary" variant="subtle" icon="i-lucide-check-circle" :description="lastScanNote" class="mb-3" />

      <div class="mb-2 flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
        <h4 class="text-sm font-medium">Today's Scans ({{ todayLabel }})</h4>
        <UButton icon="i-lucide-refresh-cw" size="xs" color="neutral" variant="ghost" :loading="scanLogLoading" @click="loadTodayScans">Refresh List</UButton>
      </div>
      <AdminMasterTable :columns="scanColumns" :rows="pagedTodayScanRows" empty-text="No scans recorded for today yet.">
        <template #actions="{ row }">
          <UButton icon="i-lucide-trash-2" size="xs" color="error" variant="ghost" @click="deleteScan(row.id)">Remove</UButton>
        </template>
      </AdminMasterTable>
      <div v-if="todayScanRows.length" class="mt-3 flex flex-col gap-2 text-sm text-muted sm:flex-row sm:items-center sm:justify-between">
        <p>Showing {{ pagedTodayScanRows.length }} of {{ todayScanRows.length }} scan(s)</p>
        <div class="flex items-center gap-2">
          <USelect v-model="scansPageSize" :items="pageSizeOptions" class="w-28" />
          <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-left" :disabled="scansPage <= 1" @click="scansPage--">Prev</UButton>
          <span>{{ scansPage }} / {{ scansTotalPages }}</span>
          <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-right" :disabled="scansPage >= scansTotalPages" @click="scansPage++">Next</UButton>
        </div>
      </div>
    </section>

    <UModal v-model:open="periodModalOpen" :title="periodForm.id ? 'Edit Audit Period' : 'New Audit Period'">
      <template #body>
        <form class="grid gap-3" @submit.prevent="savePeriod">
          <label class="space-y-1 text-sm">
            <span class="text-muted">Name</span>
            <UInput v-model="periodForm.name" placeholder="e.g. July 2026 Stock Audit" class="w-full" />
          </label>
          <label v-if="!periodForm.id" class="space-y-1 text-sm">
            <span class="text-muted">Store</span>
            <USelect v-model="periodForm.storeId" :items="storeSelectItems" placeholder="Select store" class="w-full" />
          </label>
          <div class="grid grid-cols-2 gap-3">
            <label class="space-y-1 text-sm">
              <span class="text-muted">Start Date</span>
              <UInput v-model="periodForm.startDate" type="date" class="w-full" />
            </label>
            <label class="space-y-1 text-sm">
              <span class="text-muted">End Date</span>
              <UInput v-model="periodForm.endDate" type="date" class="w-full" />
            </label>
          </div>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Notes</span>
            <UTextarea v-model="periodForm.notes" placeholder="Optional notes" class="w-full" />
          </label>

          <div class="flex justify-end gap-2">
            <UButton type="submit" icon="i-lucide-save" color="primary" :loading="savingPeriod">Save</UButton>
          </div>
        </form>
      </template>
    </UModal>

    <UModal v-model:open="deleteModalOpen" title="Delete Audit Period" description="Type DELETE AUDIT PERIOD to confirm. This removes the period and every scan recorded in it.">
      <template #body>
        <div class="grid gap-3">
          <UInput v-model="deleteConfirmText" placeholder="DELETE AUDIT PERIOD" class="w-full" />
          <UAlert v-if="deleteError" color="error" variant="subtle" :description="deleteError" />
          <div class="flex justify-end gap-2">
            <UButton color="error" icon="i-lucide-trash-2" :loading="deleting" @click="confirmDeletePeriod">Delete Permanently</UButton>
          </div>
        </div>
      </template>
    </UModal>
  </section>
</template>

<script setup lang="ts">
import { pageSizeOptions, paginateRows, readNumber, readText, toRows, type ApiRecord, useAdminApiClient } from '../utils/admin-api'

useHead({ title: 'Stock Audit - Garmetix Inventory' })

const { get, post, put, del } = useAdminApiClient()

const loading = ref(true)
const savingPeriod = ref(false)
const scanning = ref(false)
const scanLogLoading = ref(false)
const deleting = ref(false)
const error = ref('')
const message = ref('')
const lastScanNote = ref('')

const periods = ref<ApiRecord[]>([])
const stores = ref<ApiRecord[]>([])
const activePeriodId = ref('')
const todayScans = ref<ApiRecord[]>([])

const periodModalOpen = ref(false)
const deleteModalOpen = ref(false)
const deleteConfirmText = ref('')
const deleteError = ref('')
const deleteTargetId = ref('')
const barcodeInputRef = ref()

const periodForm = reactive({ id: '', name: '', storeId: '', startDate: '', endDate: '', notes: '' })
const scanForm = reactive({ barcode: '', quantity: 1 })

const todayLabel = computed(() => new Date().toLocaleDateString('en-IN', { day: '2-digit', month: 'short', year: 'numeric' }))

const storeSelectItems = computed(() => stores.value
  .map(item => ({ label: readText(item, ['name']), value: readText(item, ['id'], '') }))
  .filter(item => item.value))

const periodRows = computed(() => periods.value.map(item => ({
  id: readText(item, ['id'], ''),
  name: readText(item, ['name']),
  storeName: readText(item, ['storeName']),
  startDate: formatShortDate(item.startDate),
  endDate: formatShortDate(item.endDate),
  status: readText(item, ['status']),
  statusRaw: readText(item, ['status']),
  scannedBarcodes: readNumber(item, ['scannedBarcodeCount']),
  totalQty: readNumber(item, ['totalScannedQuantity'])
})))
const periodColumns = [
  { key: 'name', label: 'Name' },
  { key: 'storeName', label: 'Store' },
  { key: 'startDate', label: 'Start' },
  { key: 'endDate', label: 'End' },
  { key: 'status', label: 'Status' },
  { key: 'scannedBarcodes', label: 'Barcodes Scanned' },
  { key: 'totalQty', label: 'Total Qty' }
]

const periodsPage = ref(1)
const periodsPageSize = ref<number>(pageSizeOptions[0].value)
const periodsTotalPages = computed(() => Math.max(1, Math.ceil(periodRows.value.length / Number(periodsPageSize.value || 25))))
const pagedPeriodRows = computed(() => paginateRows(periodRows.value, periodsPage.value, periodsPageSize.value))
watch(periodsPageSize, () => { periodsPage.value = 1 })
watch(periodsTotalPages, (value) => { if (periodsPage.value > value) periodsPage.value = value })

const activePeriod = computed(() => periodRows.value.find(row => row.id === activePeriodId.value) || null)

const todayScanRows = computed(() => todayScans.value.map(item => ({
  id: readText(item, ['id'], ''),
  barcode: readText(item, ['barcode']),
  product: readText(item, ['productName']),
  category: readText(item, ['categoryName'], '-'),
  color: readText(item, ['color'], '-'),
  size: readText(item, ['size'], '-'),
  qty: readNumber(item, ['quantity']),
  scanCount: readNumber(item, ['scanCount'])
})))
const scanColumns = [
  { key: 'barcode', label: 'Barcode' },
  { key: 'product', label: 'Product' },
  { key: 'category', label: 'Category' },
  { key: 'color', label: 'Color' },
  { key: 'size', label: 'Size' },
  { key: 'qty', label: 'Qty Today' },
  { key: 'scanCount', label: 'Scans' }
]

const scansPage = ref(1)
const scansPageSize = ref<number>(pageSizeOptions[0].value)
const scansTotalPages = computed(() => Math.max(1, Math.ceil(todayScanRows.value.length / Number(scansPageSize.value || 25))))
const pagedTodayScanRows = computed(() => paginateRows(todayScanRows.value, scansPage.value, scansPageSize.value))
watch(scansPageSize, () => { scansPage.value = 1 })
watch(scansTotalPages, (value) => { if (scansPage.value > value) scansPage.value = value })

function formatShortDate(value: unknown) {
  if (!value) return '-'
  const date = new Date(String(value))
  if (Number.isNaN(date.getTime())) return String(value)
  return date.toLocaleDateString('en-IN', { day: '2-digit', month: 'short', year: 'numeric' })
}

function toDateInputValue(value: unknown) {
  if (!value) return ''
  const date = new Date(String(value))
  if (Number.isNaN(date.getTime())) return ''
  return date.toISOString().slice(0, 10)
}

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const [periodData, optionsData] = await Promise.allSettled([
      get<unknown>('inventory/stock-audit/periods'),
      get<ApiRecord>('inventory/stock-operations/options')
    ])
    if (periodData.status === 'fulfilled') periods.value = toRows(periodData.value)
    if (optionsData.status === 'fulfilled') stores.value = toRows((optionsData.value as ApiRecord)?.stores)
    if (periodData.status === 'rejected') {
      error.value = periodData.reason instanceof Error ? periodData.reason.message : 'Unable to load audit periods.'
    }
    if (activePeriodId.value && !periods.value.some(item => readText(item, ['id']) === activePeriodId.value)) {
      activePeriodId.value = ''
    }
    if (activePeriodId.value) await loadTodayScans()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load audit periods.'
  } finally {
    loading.value = false
  }
}

function selectPeriod(id: string) {
  activePeriodId.value = id
  lastScanNote.value = ''
  scanForm.barcode = ''
  scanForm.quantity = 1
  scansPage.value = 1
  loadTodayScans()
  nextTick(() => barcodeInputRef.value?.inputRef?.focus?.())
}

async function loadTodayScans() {
  if (!activePeriodId.value) return
  scanLogLoading.value = true
  try {
    const todayIso = new Date().toISOString().slice(0, 10)
    const result = await get<ApiRecord>(`inventory/stock-audit/periods/${activePeriodId.value}/scans`, { date: todayIso, pageSize: 200 })
    todayScans.value = toRows((result as ApiRecord)?.rows ?? result)
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load today\'s scans.'
  } finally {
    scanLogLoading.value = false
  }
}

async function submitScan() {
  if (!activePeriodId.value) return
  const barcode = scanForm.barcode.trim()
  if (!barcode) {
    error.value = 'Scan or enter a barcode.'
    return
  }
  if (!scanForm.quantity || scanForm.quantity <= 0) {
    error.value = 'Enter a quantity greater than zero.'
    return
  }

  scanning.value = true
  error.value = ''
  try {
    const result = await post<ApiRecord>('inventory/stock-audit/scan', {
      auditPeriodId: activePeriodId.value,
      barcode,
      quantity: Number(scanForm.quantity)
    })
    const scan = (result?.scan ?? {}) as ApiRecord
    lastScanNote.value = `${readText(scan, ['productName'])} (${readText(scan, ['barcode'])}): ${readNumber(result, ['todayQuantityForBarcode'])} today, ${readNumber(result, ['periodTotalQuantityForBarcode'])} total across the period.`
    scanForm.barcode = ''
    scanForm.quantity = 1
    await Promise.all([loadTodayScans(), refresh()])
    nextTick(() => barcodeInputRef.value?.inputRef?.focus?.())
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to record scan.'
  } finally {
    scanning.value = false
  }
}

async function deleteScan(id: string) {
  if (!id) return
  try {
    await del<unknown>(`inventory/stock-audit/scans/${id}`)
    await Promise.all([loadTodayScans(), refresh()])
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to remove scan entry.'
  }
}

function startCreatePeriod() {
  Object.assign(periodForm, { id: '', name: '', storeId: storeSelectItems.value[0]?.value || '', startDate: toDateInputValue(new Date()), endDate: toDateInputValue(new Date()), notes: '' })
  message.value = ''
  error.value = ''
  periodModalOpen.value = true
}

function startEditPeriod(id: string) {
  const source = periods.value.find(item => readText(item, ['id']) === id)
  if (!source) return
  Object.assign(periodForm, {
    id,
    name: readText(source, ['name']),
    storeId: readText(source, ['storeId']),
    startDate: toDateInputValue(source.startDate),
    endDate: toDateInputValue(source.endDate),
    notes: readText(source, ['notes'], '')
  })
  message.value = ''
  error.value = ''
  periodModalOpen.value = true
}

async function savePeriod() {
  savingPeriod.value = true
  error.value = ''
  message.value = ''
  try {
    if (!periodForm.name.trim()) throw new Error('Enter a name for this audit period.')
    if (!periodForm.startDate || !periodForm.endDate) throw new Error('Select both start and end dates.')

    if (periodForm.id) {
      await put<unknown>(`inventory/stock-audit/periods/${periodForm.id}`, {
        name: periodForm.name.trim(),
        startDate: periodForm.startDate,
        endDate: periodForm.endDate,
        notes: periodForm.notes.trim() || null
      })
      message.value = 'Audit period updated.'
    } else {
      if (!periodForm.storeId) throw new Error('Select a store.')
      await post<unknown>('inventory/stock-audit/periods', {
        name: periodForm.name.trim(),
        storeId: periodForm.storeId,
        startDate: periodForm.startDate,
        endDate: periodForm.endDate,
        notes: periodForm.notes.trim() || null
      })
      message.value = 'Audit period created.'
    }
    periodModalOpen.value = false
    await refresh()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to save audit period.'
  } finally {
    savingPeriod.value = false
  }
}

async function closePeriod(id: string) {
  try {
    await post<unknown>(`inventory/stock-audit/periods/${id}/close`)
    message.value = 'Audit period closed.'
    await refresh()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to close audit period.'
  }
}

async function reopenPeriod(id: string) {
  try {
    await post<unknown>(`inventory/stock-audit/periods/${id}/reopen`)
    message.value = 'Audit period reopened.'
    await refresh()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to reopen audit period.'
  }
}

function startDeletePeriod(id: string) {
  deleteTargetId.value = id
  deleteConfirmText.value = ''
  deleteError.value = ''
  deleteModalOpen.value = true
}

async function confirmDeletePeriod() {
  if (deleteConfirmText.value !== 'DELETE AUDIT PERIOD') {
    deleteError.value = 'Type DELETE AUDIT PERIOD exactly to confirm.'
    return
  }
  deleting.value = true
  deleteError.value = ''
  try {
    await del<unknown>(`inventory/stock-audit/periods/${deleteTargetId.value}`)
    deleteModalOpen.value = false
    message.value = 'Audit period deleted.'
    if (activePeriodId.value === deleteTargetId.value) activePeriodId.value = ''
    await refresh()
  } catch (caught) {
    deleteError.value = caught instanceof Error ? caught.message : 'Unable to delete audit period.'
  } finally {
    deleting.value = false
  }
}

onMounted(refresh)
</script>
