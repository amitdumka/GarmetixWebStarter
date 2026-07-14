<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 xl:flex-row xl:items-start xl:justify-between">
        <div>
          <p class="garmetix-dashboard-kicker">
            <UIcon name="i-lucide-percent" class="size-4" />
            GST & Taxes
          </p>
          <h2 class="garmetix-dashboard-title">GST Rate Master</h2>
          <p class="garmetix-dashboard-subtitle">
            Date-effective GST rate rules by HSN code, product category, and price threshold - including the garment
            slab rule. Rules apply using the invoice date, never "today", so a rate change never retroactively
            changes an already-posted invoice.
          </p>
        </div>
        <UButton icon="i-lucide-plus" color="primary" @click="openCreateModal">New Rate Rule</UButton>
      </div>
    </div>

    <UAlert v-if="error" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />
    <UAlert v-if="message" color="success" variant="subtle" icon="i-lucide-circle-check" :description="message" />

    <section class="garmetix-section-card">
      <div class="mb-3 grid gap-2 sm:grid-cols-3">
        <UInput v-model="filters.hsnCode" icon="i-lucide-hash" placeholder="HSN code" @keyup.enter="refresh" />
        <UInput v-model="filters.productCategory" icon="i-lucide-search" placeholder="Product category" @keyup.enter="refresh" />
        <USelect v-model="filters.isActive" :items="activeFilterItems" />
      </div>
      <UButton size="sm" color="neutral" variant="soft" icon="i-lucide-search" :loading="loading" @click="refresh">Search</UButton>

      <div class="mt-3 overflow-x-auto rounded-lg border border-default">
        <table class="w-full min-w-[1080px] text-left text-sm">
          <thead class="bg-muted/30 text-xs uppercase text-muted">
            <tr>
              <th class="whitespace-nowrap px-3 py-2 font-medium">Rule</th>
              <th class="whitespace-nowrap px-3 py-2 font-medium">HSN</th>
              <th class="whitespace-nowrap px-3 py-2 font-medium">Category</th>
              <th class="whitespace-nowrap px-3 py-2 font-medium">Rate %</th>
              <th class="whitespace-nowrap px-3 py-2 font-medium">Threshold</th>
              <th class="whitespace-nowrap px-3 py-2 font-medium">Effective</th>
              <th class="whitespace-nowrap px-3 py-2 font-medium">Priority</th>
              <th class="whitespace-nowrap px-3 py-2 font-medium">Active</th>
              <th class="whitespace-nowrap px-3 py-2 font-medium">Actions</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-default">
            <tr v-if="rows.length === 0">
              <td colspan="9" class="px-3 py-8 text-center text-muted">No rate rules found.</td>
            </tr>
            <tr v-for="row in rows" :key="row.id">
              <td class="max-w-56 truncate px-3 py-2 font-medium">{{ row.ruleName }}</td>
              <td class="whitespace-nowrap px-3 py-2">{{ row.hsnCode || '-' }}</td>
              <td class="whitespace-nowrap px-3 py-2">{{ row.productCategory || '-' }}</td>
              <td class="whitespace-nowrap px-3 py-2">{{ row.taxRate }}</td>
              <td class="whitespace-nowrap px-3 py-2">{{ thresholdLabel(row) }}</td>
              <td class="whitespace-nowrap px-3 py-2">{{ formatDate(row.effectiveFrom) }} - {{ row.effectiveTo ? formatDate(row.effectiveTo) : 'Open' }}</td>
              <td class="whitespace-nowrap px-3 py-2">{{ row.priority }}</td>
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
    </section>

    <section class="garmetix-section-card">
      <h3 class="garmetix-panel-title mb-1">Resolve A Rate</h3>
      <p class="text-xs text-muted mb-3">Try the resolver exactly the way Sale/Purchase entry will use it.</p>
      <div class="grid gap-2 sm:grid-cols-5">
        <UInput v-model="resolveForm.hsnCode" placeholder="HSN code" />
        <UInput v-model="resolveForm.productCategory" placeholder="Product category" />
        <UInput v-model.number="resolveForm.basicRateAfterDiscount" type="number" placeholder="Basic rate after discount" />
        <UInput v-model="resolveForm.invoiceDate" type="date" />
        <label class="flex items-center gap-2 text-sm">
          <USwitch v-model="resolveForm.isIntraState" />
          <span>Intra-state</span>
        </label>
      </div>
      <UButton class="mt-2" size="sm" color="primary" variant="soft" icon="i-lucide-calculator" :loading="resolving" @click="resolveRate">Resolve</UButton>
      <div v-if="resolveResult" class="mt-3 garmetix-metric-card">
        <p class="garmetix-metric-label">{{ resolveResult.success ? `${resolveResult.taxRate}% (${resolveResult.ruleName})` : 'No rate found' }}</p>
        <p class="garmetix-metric-caption">CGST {{ resolveResult.cgstRate }}% - SGST {{ resolveResult.sgstRate }}% - IGST {{ resolveResult.igstRate }}% - Cess {{ resolveResult.cessRate }}%</p>
        <p v-for="warning in resolveResult.warnings" :key="warning" class="text-xs text-warning">{{ warning }}</p>
      </div>
    </section>

    <UModal v-model:open="modalOpen" :title="editingId ? 'Update Rate Rule' : 'New Rate Rule'" :ui="{ content: 'sm:max-w-3xl' }">
      <template #body>
        <form class="grid gap-3 sm:grid-cols-2" @submit.prevent="save">
          <label class="space-y-1 text-sm sm:col-span-2">
            <span class="text-muted">Rule Name</span>
            <UInput v-model="form.ruleName" required />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">HSN Code (optional if category given)</span>
            <UInput v-model="form.hsnCode" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Product Category (optional if HSN given)</span>
            <UInput v-model="form.productCategory" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Goods Or Service</span>
            <USelect v-model="form.goodsOrService" :items="['Goods', 'Service']" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Tax Rate (%)</span>
            <UInput v-model.number="form.taxRate" type="number" step="0.01" required />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Threshold Basis</span>
            <USelect v-model="form.thresholdBasis" :items="thresholdBasisItems" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Priority (lower wins on conflict)</span>
            <UInput v-model.number="form.priority" type="number" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Price Threshold From</span>
            <UInput v-model.number="form.priceThresholdFrom" type="number" step="0.01" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Price Threshold To</span>
            <UInput v-model.number="form.priceThresholdTo" type="number" step="0.01" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Effective From</span>
            <UInput v-model="form.effectiveFrom" type="date" required />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Effective To (optional)</span>
            <UInput v-model="form.effectiveTo" type="date" />
          </label>
          <label class="garmetix-row-card flex items-center justify-between gap-2 sm:col-span-2">
            <span>Active</span>
            <USwitch v-model="form.isActive" />
          </label>
          <label class="space-y-1 text-sm sm:col-span-2">
            <span class="text-muted">Notes</span>
            <UTextarea v-model="form.notes" :rows="2" />
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
import { formatDate, useBooksApiClient } from '../utils/books-api'

useHead({ title: 'GST Rate Master - Garmetix Books' })

interface RateRow {
  id: string
  ruleName: string
  hsnCode: string | null
  productCategory: string | null
  goodsOrService: string
  taxRate: number
  priceThresholdFrom: number | null
  priceThresholdTo: number | null
  thresholdBasis: string | null
  effectiveFrom: string
  effectiveTo: string | null
  priority: number
  isActive: boolean
  notes: string | null
}

interface ResolveResult {
  success: boolean
  taxRate: number
  cgstRate: number
  sgstRate: number
  igstRate: number
  cessRate: number
  ruleName: string | null
  warnings: string[]
}

const { get, post, put, del } = useBooksApiClient()

const loading = ref(true)
const saving = ref(false)
const resolving = ref(false)
const error = ref('')
const message = ref('')
const rows = ref<RateRow[]>([])

const filters = reactive({ hsnCode: '', productCategory: '', isActive: '' })
const activeFilterItems = [
  { label: 'All Status', value: '' },
  { label: 'Active Only', value: 'true' },
  { label: 'Inactive Only', value: 'false' }
]
const thresholdBasisItems = ['BasicRateAfterDiscount', 'MRP', 'SaleRate', 'TaxableValue']

const modalOpen = ref(false)
const editingId = ref<string | null>(null)
function emptyForm() {
  return {
    ruleName: '', hsnCode: '', productCategory: '', goodsOrService: 'Goods', taxRate: 5,
    priceThresholdFrom: null as number | null, priceThresholdTo: null as number | null,
    thresholdBasis: 'BasicRateAfterDiscount', priority: 100, isActive: true, notes: '',
    effectiveFrom: new Date().toISOString().slice(0, 10), effectiveTo: ''
  }
}
const form = ref(emptyForm())

const resolveForm = reactive({
  hsnCode: '', productCategory: '', basicRateAfterDiscount: 999,
  invoiceDate: new Date().toISOString().slice(0, 10), isIntraState: true
})
const resolveResult = ref<ResolveResult | null>(null)

function thresholdLabel(row: RateRow) {
  if (row.priceThresholdFrom == null && row.priceThresholdTo == null) return 'Any price'
  return `${row.priceThresholdFrom ?? '0'} - ${row.priceThresholdTo ?? '∞'}`
}

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const query: Record<string, string> = {}
    if (filters.hsnCode) query.hsnCode = filters.hsnCode
    if (filters.productCategory) query.productCategory = filters.productCategory
    if (filters.isActive) query.isActive = filters.isActive
    rows.value = await get<RateRow[]>('/gst/rates', query)
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to load rate rules.'
  } finally {
    loading.value = false
  }
}

function openCreateModal() {
  editingId.value = null
  form.value = emptyForm()
  modalOpen.value = true
}

function openEditModal(row: RateRow) {
  editingId.value = row.id
  form.value = {
    ruleName: row.ruleName,
    hsnCode: row.hsnCode ?? '',
    productCategory: row.productCategory ?? '',
    goodsOrService: row.goodsOrService,
    taxRate: row.taxRate,
    priceThresholdFrom: row.priceThresholdFrom,
    priceThresholdTo: row.priceThresholdTo,
    thresholdBasis: row.thresholdBasis ?? 'BasicRateAfterDiscount',
    priority: row.priority,
    isActive: row.isActive,
    notes: row.notes ?? '',
    effectiveFrom: row.effectiveFrom.slice(0, 10),
    effectiveTo: row.effectiveTo ? row.effectiveTo.slice(0, 10) : ''
  }
  modalOpen.value = true
}

async function save() {
  saving.value = true
  error.value = ''
  try {
    const payload = {
      ruleName: form.value.ruleName.trim(),
      hsnCode: form.value.hsnCode || null,
      productCategory: form.value.productCategory || null,
      goodsOrService: form.value.goodsOrService,
      taxRate: form.value.taxRate,
      cgstRate: null,
      sgstRate: null,
      igstRate: null,
      cessRate: null,
      priceThresholdFrom: form.value.priceThresholdFrom,
      priceThresholdTo: form.value.priceThresholdTo,
      thresholdBasis: form.value.thresholdBasis,
      effectiveFrom: form.value.effectiveFrom,
      effectiveTo: form.value.effectiveTo || null,
      priority: form.value.priority,
      isActive: form.value.isActive,
      notes: form.value.notes || null
    }

    if (editingId.value) {
      await put(`/gst/rates/${editingId.value}`, payload)
      message.value = 'Rate rule updated.'
    } else {
      await post('/gst/rates', payload)
      message.value = 'Rate rule created.'
    }

    modalOpen.value = false
    await refresh()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to save the rate rule.'
  } finally {
    saving.value = false
  }
}

async function deleteRow(row: RateRow) {
  try {
    await del(`/gst/rates/${row.id}`)
    message.value = `"${row.ruleName}" deleted.`
    await refresh()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to delete the rate rule.'
  }
}

async function resolveRate() {
  resolving.value = true
  error.value = ''
  try {
    resolveResult.value = await post<ResolveResult>('/gst/rates/resolve', {
      hsnCode: resolveForm.hsnCode || null,
      productCategory: resolveForm.productCategory || null,
      basicRateAfterDiscount: resolveForm.basicRateAfterDiscount,
      invoiceDate: resolveForm.invoiceDate,
      isIntraState: resolveForm.isIntraState
    })
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Rate resolve failed.'
  } finally {
    resolving.value = false
  }
}

onMounted(refresh)
</script>
