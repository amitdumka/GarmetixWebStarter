<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 xl:flex-row xl:items-start xl:justify-between">
        <div>
          <p class="garmetix-dashboard-kicker">
            <UIcon name="i-lucide-table-properties" class="size-4" />
            GST & Taxes
          </p>
          <h2 class="garmetix-dashboard-title">HSN / SAC Master</h2>
          <p class="garmetix-dashboard-subtitle">
            Local HSN/SAC directory used for daily billing - no live API call is needed to bill a product once its HSN is here.
          </p>
        </div>
        <div class="flex flex-col gap-2 sm:flex-row">
          <UButton icon="i-lucide-download" color="neutral" variant="soft" @click="downloadTemplate">Download Template</UButton>
          <UButton icon="i-lucide-upload" color="neutral" variant="soft" :loading="importing" @click="triggerFileInput">Import CSV</UButton>
          <input ref="fileInput" type="file" accept=".csv" class="hidden" @change="onFileSelected" />
          <UButton icon="i-lucide-plus" color="primary" @click="openCreateModal">New HSN/SAC</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />
    <UAlert v-if="message" color="success" variant="subtle" icon="i-lucide-circle-check" :description="message" />
    <UAlert
      v-if="importResult"
      color="neutral"
      variant="subtle"
      icon="i-lucide-info"
      title="Import summary"
      :description="`Read ${importResult.rowsRead}, created ${importResult.created}, updated ${importResult.updated}, skipped ${importResult.skipped}.${importResult.errors.length ? ' Errors: ' + importResult.errors.join(' | ') : ''}`"
    />

    <section class="garmetix-section-card">
      <div class="mb-3 grid gap-2 sm:grid-cols-4">
        <UInput v-model="filters.hsnCode" icon="i-lucide-hash" placeholder="HSN code" @keyup.enter="refresh" />
        <UInput v-model="filters.description" icon="i-lucide-search" placeholder="Description" @keyup.enter="refresh" />
        <USelect v-model="filters.codeType" :items="codeTypeFilterItems" />
        <USelect v-model="filters.isActive" :items="activeFilterItems" />
      </div>
      <div class="mb-3 flex flex-wrap items-center justify-between gap-2">
        <UButton size="sm" color="neutral" variant="soft" icon="i-lucide-search" :loading="loading" @click="refresh">Search</UButton>
        <div class="flex flex-wrap gap-2">
          <UButton size="sm" color="neutral" variant="soft" icon="i-lucide-eye" :disabled="!selectedIds.length" @click="bulkSetStatus(true)">Activate Selected</UButton>
          <UButton size="sm" color="neutral" variant="soft" icon="i-lucide-eye-off" :disabled="!selectedIds.length" @click="bulkSetStatus(false)">Deactivate Selected</UButton>
        </div>
      </div>

      <div class="overflow-x-auto rounded-lg border border-default">
        <table class="w-full min-w-[960px] text-left text-sm">
          <thead class="bg-muted/30 text-xs uppercase text-muted">
            <tr>
              <th class="whitespace-nowrap px-3 py-2"><UCheckbox :model-value="allSelected" @update:model-value="toggleSelectAll" /></th>
              <th class="whitespace-nowrap px-3 py-2 font-medium">HSN/SAC</th>
              <th class="whitespace-nowrap px-3 py-2 font-medium">Type</th>
              <th class="whitespace-nowrap px-3 py-2 font-medium">Description</th>
              <th class="whitespace-nowrap px-3 py-2 font-medium">GST %</th>
              <th class="whitespace-nowrap px-3 py-2 font-medium">UQC</th>
              <th class="whitespace-nowrap px-3 py-2 font-medium">Products</th>
              <th class="whitespace-nowrap px-3 py-2 font-medium">Active</th>
              <th class="whitespace-nowrap px-3 py-2 font-medium">Actions</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-default">
            <tr v-if="rows.length === 0">
              <td colspan="9" class="px-3 py-8 text-center text-muted">No HSN/SAC entries found.</td>
            </tr>
            <tr v-for="row in rows" :key="row.id">
              <td class="px-3 py-2"><UCheckbox :model-value="selectedIds.includes(row.id)" @update:model-value="toggleSelect(row.id)" /></td>
              <td class="whitespace-nowrap px-3 py-2 font-medium">{{ row.hsnCode }}</td>
              <td class="whitespace-nowrap px-3 py-2">{{ row.codeType }}</td>
              <td class="max-w-72 truncate px-3 py-2">{{ row.description || row.commonTradeDescription || '-' }}</td>
              <td class="whitespace-nowrap px-3 py-2">{{ row.defaultGstRate ?? '-' }}</td>
              <td class="whitespace-nowrap px-3 py-2">{{ row.defaultUqc || '-' }}</td>
              <td class="whitespace-nowrap px-3 py-2">{{ row.productMappingCount }}</td>
              <td class="whitespace-nowrap px-3 py-2">
                <UBadge :color="row.isActive ? 'success' : 'neutral'" variant="subtle">{{ row.isActive ? 'Active' : 'Inactive' }}</UBadge>
              </td>
              <td class="whitespace-nowrap px-3 py-2">
                <div class="flex gap-1">
                  <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-pencil" @click="openEditModal(row)">Edit</UButton>
                  <UButton size="xs" color="error" variant="soft" icon="i-lucide-trash-2" @click="deleteRow(row)">Delete</UButton>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <div class="mt-3 flex items-center justify-between text-xs text-muted">
        <span>{{ total }} total entries</span>
        <div class="flex items-center gap-2">
          <UButton size="xs" color="neutral" variant="soft" :disabled="page <= 1" @click="page--; refresh()">Prev</UButton>
          <span>Page {{ page }}</span>
          <UButton size="xs" color="neutral" variant="soft" :disabled="rows.length < pageSize" @click="page++; refresh()">Next</UButton>
        </div>
      </div>
    </section>

    <UModal v-model:open="modalOpen" :title="editingId ? 'Update HSN/SAC' : 'New HSN/SAC'" :ui="{ content: 'sm:max-w-2xl' }">
      <template #body>
        <form class="grid gap-3 sm:grid-cols-2" @submit.prevent="save">
          <label class="space-y-1 text-sm">
            <span class="text-muted">HSN/SAC Code</span>
            <UInput v-model="form.hsnCode" required />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Type</span>
            <USelect v-model="form.codeType" :items="['Goods', 'Service']" />
          </label>
          <label class="space-y-1 text-sm sm:col-span-2">
            <span class="text-muted">Description</span>
            <UInput v-model="form.description" />
          </label>
          <label class="space-y-1 text-sm sm:col-span-2">
            <span class="text-muted">Common Trade Description</span>
            <UInput v-model="form.commonTradeDescription" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Default UQC</span>
            <UInput v-model="form.defaultUqc" placeholder="PCS" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Default GST Rate (%)</span>
            <UInput v-model.number="form.defaultGstRate" type="number" step="0.01" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">CGST %</span>
            <UInput v-model.number="form.cgstRate" type="number" step="0.01" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">SGST %</span>
            <UInput v-model.number="form.sgstRate" type="number" step="0.01" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">IGST %</span>
            <UInput v-model.number="form.igstRate" type="number" step="0.01" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Cess %</span>
            <UInput v-model.number="form.cessRate" type="number" step="0.01" />
          </label>
          <label class="garmetix-row-card flex items-center justify-between gap-2 sm:col-span-2">
            <span>Active</span>
            <USwitch v-model="form.isActive" />
          </label>
          <div class="flex justify-end gap-2 sm:col-span-2">
            <UButton type="button" color="neutral" variant="soft" @click="modalOpen = false">Cancel</UButton>
            <UButton type="submit" color="primary" icon="i-lucide-save" :loading="saving">{{ editingId ? 'Update' : 'Create' }}</UButton>
          </div>
        </form>
      </template>
    </UModal>
  </section>
</template>

<script setup lang="ts">
import { useBooksApiClient } from '../utils/books-api'

useHead({ title: 'HSN/SAC Master - Garmetix Books' })

interface HsnRow {
  id: string
  hsnCode: string
  codeType: string
  description: string | null
  commonTradeDescription: string | null
  defaultUqc: string | null
  defaultGstRate: number | null
  cgstRate: number | null
  sgstRate: number | null
  igstRate: number | null
  cessRate: number | null
  isActive: boolean
  productMappingCount: number
}

interface ImportResult {
  rowsRead: number
  created: number
  updated: number
  skipped: number
  errors: string[]
}

const { get, post, put, del, postForm, download } = useBooksApiClient()

const loading = ref(true)
const saving = ref(false)
const importing = ref(false)
const error = ref('')
const message = ref('')
const importResult = ref<ImportResult | null>(null)

const rows = ref<HsnRow[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = 50

const filters = reactive({ hsnCode: '', description: '', codeType: '', isActive: '' })
const codeTypeFilterItems = [
  { label: 'All Types', value: '' },
  { label: 'Goods', value: 'Goods' },
  { label: 'Service', value: 'Service' }
]
const activeFilterItems = [
  { label: 'All Status', value: '' },
  { label: 'Active Only', value: 'true' },
  { label: 'Inactive Only', value: 'false' }
]

const selectedIds = ref<string[]>([])
const allSelected = computed(() => rows.value.length > 0 && selectedIds.value.length === rows.value.length)

const modalOpen = ref(false)
const editingId = ref<string | null>(null)
function emptyForm() {
  return {
    hsnCode: '', codeType: 'Goods', description: '', commonTradeDescription: '', defaultUqc: '',
    defaultGstRate: null as number | null, cgstRate: null as number | null, sgstRate: null as number | null,
    igstRate: null as number | null, cessRate: null as number | null, isActive: true
  }
}
const form = ref(emptyForm())

const fileInput = ref<HTMLInputElement | null>(null)

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const query: Record<string, string | number> = { page: page.value, pageSize }
    if (filters.hsnCode) query.hsnCode = filters.hsnCode
    if (filters.description) query.description = filters.description
    if (filters.codeType) query.codeType = filters.codeType
    if (filters.isActive) query.isActive = filters.isActive
    const response = await get<{ total: number, items: HsnRow[] }>('/gst/hsn', query)
    rows.value = response.items ?? []
    total.value = response.total ?? 0
    selectedIds.value = []
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to load the HSN/SAC master.'
  } finally {
    loading.value = false
  }
}

function toggleSelect(id: string) {
  selectedIds.value = selectedIds.value.includes(id) ? selectedIds.value.filter(x => x !== id) : [...selectedIds.value, id]
}

function toggleSelectAll(value: boolean) {
  selectedIds.value = value ? rows.value.map(r => r.id) : []
}

async function bulkSetStatus(isActive: boolean) {
  if (!selectedIds.value.length) return
  try {
    await post('/gst/hsn/bulk-status', { ids: selectedIds.value, isActive })
    message.value = `${selectedIds.value.length} entries ${isActive ? 'activated' : 'deactivated'}.`
    await refresh()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Bulk update failed.'
  }
}

function openCreateModal() {
  editingId.value = null
  form.value = emptyForm()
  modalOpen.value = true
}

function openEditModal(row: HsnRow) {
  editingId.value = row.id
  form.value = {
    hsnCode: row.hsnCode,
    codeType: row.codeType,
    description: row.description ?? '',
    commonTradeDescription: row.commonTradeDescription ?? '',
    defaultUqc: row.defaultUqc ?? '',
    defaultGstRate: row.defaultGstRate,
    cgstRate: row.cgstRate,
    sgstRate: row.sgstRate,
    igstRate: row.igstRate,
    cessRate: row.cessRate,
    isActive: row.isActive
  }
  modalOpen.value = true
}

async function save() {
  saving.value = true
  error.value = ''
  try {
    const payload = {
      hsnCode: form.value.hsnCode.trim(),
      codeType: form.value.codeType,
      chapterCode: null,
      description: form.value.description || null,
      technicalDescription: null,
      commonTradeDescription: form.value.commonTradeDescription || null,
      defaultUqc: form.value.defaultUqc || null,
      defaultGstRate: form.value.defaultGstRate,
      cgstRate: form.value.cgstRate,
      sgstRate: form.value.sgstRate,
      igstRate: form.value.igstRate,
      cessRate: form.value.cessRate,
      effectiveFrom: null,
      effectiveTo: null,
      source: editingId.value ? undefined : 'Manual Entry',
      isActive: form.value.isActive
    }

    if (editingId.value) {
      await put(`/gst/hsn/${editingId.value}`, payload)
      message.value = 'HSN/SAC entry updated.'
    } else {
      await post('/gst/hsn', payload)
      message.value = 'HSN/SAC entry created.'
    }

    modalOpen.value = false
    await refresh()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to save the HSN/SAC entry.'
  } finally {
    saving.value = false
  }
}

async function deleteRow(row: HsnRow) {
  try {
    await del(`/gst/hsn/${row.id}`)
    message.value = `${row.hsnCode} deleted.`
    await refresh()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to delete the entry.'
  }
}

function triggerFileInput() {
  fileInput.value?.click()
}

async function onFileSelected(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return

  importing.value = true
  error.value = ''
  importResult.value = null
  try {
    const form = new FormData()
    form.append('file', file)
    importResult.value = await postForm<ImportResult>('/gst/hsn/import-csv', form)
    message.value = 'Import complete.'
    await refresh()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Import failed.'
  } finally {
    importing.value = false
    input.value = ''
  }
}

async function downloadTemplate() {
  try {
    await download('/gst/hsn/template', undefined, 'garmetix-hsn-import-template.csv')
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to download the template.'
  }
}

onMounted(refresh)
</script>
