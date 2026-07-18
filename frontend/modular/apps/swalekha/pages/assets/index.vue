<template>
  <section class="space-y-5">
    <div class="flex flex-wrap items-start justify-between gap-3">
      <div>
        <p class="garmetix-kicker"><UIcon name="i-lucide-home" class="size-4" /> Net Worth</p>
        <h1 class="garmetix-dashboard-title">Assets</h1>
        <p class="text-sm text-muted">Property and valuables you own - Immovable (house, flat, land) and Movable (gold, vehicle, watches, electronics, and other valuables).</p>
      </div>
      <div class="flex flex-wrap items-center gap-2">
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
        <UButton icon="i-lucide-plus" @click="openCreate('Immovable')">Add Immovable</UButton>
        <UButton icon="i-lucide-plus" color="neutral" @click="openCreate('Movable')">Add Movable</UButton>
      </div>
    </div>

    <UAlert v-if="error" icon="i-lucide-circle-alert" color="error" variant="subtle" title="Could not load assets" :description="error" />

    <div class="grid gap-3 sm:grid-cols-3">
      <UCard :ui="{ body: 'p-4' }">
        <p class="text-xs font-medium uppercase text-muted">Immovable ({{ summary?.immovableCount ?? 0 }})</p>
        <p class="truncate text-lg font-semibold text-highlighted">{{ formatCurrency(summary?.immovableTotal ?? 0) }}</p>
      </UCard>
      <UCard :ui="{ body: 'p-4' }">
        <p class="text-xs font-medium uppercase text-muted">Movable ({{ summary?.movableCount ?? 0 }})</p>
        <p class="truncate text-lg font-semibold text-highlighted">{{ formatCurrency(summary?.movableTotal ?? 0) }}</p>
      </UCard>
      <UCard :ui="{ body: 'p-4' }">
        <p class="text-xs font-medium uppercase text-muted">Total Assets</p>
        <p class="truncate text-lg font-semibold text-primary">{{ formatCurrency(summary?.grandTotal ?? 0) }}</p>
      </UCard>
    </div>

    <UCard :ui="{ body: 'p-0' }">
      <template #header>
        <div class="flex items-center gap-2">
          <UIcon name="i-lucide-building-2" class="size-4" />
          <span class="font-semibold">Immovable - House, Flat, Land</span>
        </div>
      </template>
      <UTable :data="immovableAssets" :columns="assetColumns" :loading="loading" class="w-full">
        <template #assetSubType-cell="{ row }"><UBadge color="primary" variant="subtle">{{ row.original.assetSubType }}</UBadge></template>
        <template #currentValue-cell="{ row }"><span class="font-mono text-sm">{{ formatCurrency(row.original.currentValue) }}</span></template>
        <template #location-cell="{ row }">{{ row.original.location || '-' }}</template>
        <template #isActive-cell="{ row }">
          <UBadge :color="row.original.isActive ? 'success' : 'neutral'" variant="subtle">{{ row.original.isActive ? 'Held' : 'Disposed' }}</UBadge>
        </template>
        <template #actions-cell="{ row }">
          <div class="flex justify-end gap-1">
            <UButton icon="i-lucide-pencil" color="neutral" variant="ghost" size="sm" title="Edit" @click="openEdit(row.original)" />
            <UButton icon="i-lucide-trash-2" color="error" variant="ghost" size="sm" title="Delete" @click="deleteAsset(row.original)" />
          </div>
        </template>
      </UTable>
    </UCard>

    <UCard :ui="{ body: 'p-0' }">
      <template #header>
        <div class="flex items-center gap-2">
          <UIcon name="i-lucide-gem" class="size-4" />
          <span class="font-semibold">Movable - Gold, Vehicle, Luxury &amp; Other Valuables</span>
        </div>
      </template>
      <UTable :data="movableAssets" :columns="assetColumns" :loading="loading" class="w-full">
        <template #assetSubType-cell="{ row }"><UBadge color="warning" variant="subtle">{{ row.original.assetSubType }}</UBadge></template>
        <template #currentValue-cell="{ row }"><span class="font-mono text-sm">{{ formatCurrency(row.original.currentValue) }}</span></template>
        <template #location-cell="{ row }">{{ row.original.location || '-' }}</template>
        <template #isActive-cell="{ row }">
          <UBadge :color="row.original.isActive ? 'success' : 'neutral'" variant="subtle">{{ row.original.isActive ? 'Held' : 'Disposed' }}</UBadge>
        </template>
        <template #actions-cell="{ row }">
          <div class="flex justify-end gap-1">
            <UButton icon="i-lucide-pencil" color="neutral" variant="ghost" size="sm" title="Edit" @click="openEdit(row.original)" />
            <UButton icon="i-lucide-trash-2" color="error" variant="ghost" size="sm" title="Delete" @click="deleteAsset(row.original)" />
          </div>
        </template>
      </UTable>
    </UCard>

    <USlideover v-model:open="formOpen" :title="editingId ? 'Edit Asset' : 'New Asset'" :ui="{ content: 'sm:max-w-md' }">
      <template #body>
        <form class="space-y-4" @submit.prevent="submitAsset">
          <UFormField label="Category" name="category">
            <USelect v-model="form.category" :items="['Immovable', 'Movable']" class="w-full" />
          </UFormField>

          <UFormField label="Type" name="assetSubType">
            <UInput v-model="form.assetSubType" list="asset-subtype-suggestions" required :placeholder="form.category === 'Immovable' ? 'House, Flat, Land, Commercial Property...' : 'Gold, Car, Bike, Watch, Mobile, Laptop, Jewellery...'" class="w-full" />
            <datalist id="asset-subtype-suggestions">
              <option v-for="option in subTypeSuggestions" :key="option" :value="option" />
            </datalist>
          </UFormField>

          <UFormField label="Name / Description" name="name">
            <UInput v-model="form.name" required placeholder="3BHK Flat - Ranchi, Rolex Submariner, Honda City..." class="w-full" />
          </UFormField>

          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Purchase Value (optional)" name="purchaseValue">
              <UInput v-model.number="form.purchaseValue" type="number" step="0.01" icon="i-lucide-indian-rupee" class="w-full" />
            </UFormField>
            <UFormField label="Purchase Date (optional)" name="purchaseDate">
              <UInput v-model="form.purchaseDate" type="date" class="w-full" />
            </UFormField>
          </div>

          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Current Value" name="currentValue">
              <UInput v-model.number="form.currentValue" type="number" step="0.01" icon="i-lucide-indian-rupee" class="w-full" />
            </UFormField>
            <UFormField label="As Of Date" name="asOfDate">
              <UInput v-model="form.asOfDate" type="date" class="w-full" />
            </UFormField>
          </div>

          <UFormField label="Location (optional)" name="location">
            <UInput v-model="form.location" placeholder="Address, or where it's kept/stored" class="w-full" />
          </UFormField>

          <USwitch v-model="form.isActive" label="Still held (turn off if sold/disposed)" />

          <UFormField label="Notes" name="notes">
            <UTextarea v-model="form.notes" :rows="3" class="w-full" />
          </UFormField>

          <UAlert v-if="formError" icon="i-lucide-circle-alert" color="error" variant="subtle" :description="formError" />

          <UButton type="submit" icon="i-lucide-save" :loading="saving" block>{{ editingId ? 'Save' : 'Create Asset' }}</UButton>
        </form>
      </template>
    </USlideover>
  </section>
</template>

<script setup lang="ts">
import { useSwalekhaApiClient, type SwalekhaAsset, type SwalekhaAssetPayload, type SwalekhaAssetSummary } from '../../utils/swalekha-api'

useHead({ title: 'Assets - Swalekha' })

const api = useSwalekhaApiClient()
const loading = ref(false)
const error = ref('')
const assets = ref<SwalekhaAsset[]>([])
const summary = ref<SwalekhaAssetSummary | null>(null)

const immovableAssets = computed(() => assets.value.filter(a => a.category === 'Immovable'))
const movableAssets = computed(() => assets.value.filter(a => a.category === 'Movable'))

const assetColumns = [
  { accessorKey: 'assetSubType', header: 'Type' },
  { accessorKey: 'name', header: 'Name' },
  { accessorKey: 'currentValue', header: 'Current Value' },
  { accessorKey: 'location', header: 'Location' },
  { accessorKey: 'isActive', header: 'Status' },
  { accessorKey: 'actions', header: '', enableSorting: false }
]

const immovableSuggestions = ['House', 'Flat', 'Apartment', 'Land', 'Plot', 'Commercial Property', 'Farmhouse', 'Shop']
const movableSuggestions = ['Gold', 'Silver', 'Car', 'Bike', 'Watch', 'Mobile', 'Laptop', 'Jewellery', 'Electronics', 'Furniture', 'Art/Antique', 'Other']
const subTypeSuggestions = computed(() => form.category === 'Immovable' ? immovableSuggestions : movableSuggestions)

function formatCurrency(value: number) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(value || 0)
}

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const [assetList, assetSummary] = await Promise.all([
      api.get<SwalekhaAsset[]>('assets?includeInactive=true'),
      api.get<SwalekhaAssetSummary>('assets/summary')
    ])
    assets.value = assetList
    summary.value = assetSummary
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not load assets.'
  } finally {
    loading.value = false
  }
}

onMounted(refresh)

const formOpen = ref(false)
const saving = ref(false)
const formError = ref('')
const editingId = ref<string | null>(null)
const form = reactive<SwalekhaAssetPayload>(defaultForm('Immovable'))

function defaultForm(category: 'Immovable' | 'Movable'): SwalekhaAssetPayload {
  return {
    category,
    assetSubType: '',
    name: '',
    purchaseValue: null,
    purchaseDate: null,
    currentValue: 0,
    asOfDate: new Date().toISOString().substring(0, 10),
    location: '',
    isActive: true,
    notes: ''
  }
}

function openCreate(category: 'Immovable' | 'Movable') {
  editingId.value = null
  formError.value = ''
  Object.assign(form, defaultForm(category))
  formOpen.value = true
}

function openEdit(asset: SwalekhaAsset) {
  editingId.value = asset.id
  formError.value = ''
  Object.assign(form, {
    category: asset.category,
    assetSubType: asset.assetSubType,
    name: asset.name,
    purchaseValue: asset.purchaseValue ?? null,
    purchaseDate: asset.purchaseDate ? asset.purchaseDate.substring(0, 10) : null,
    currentValue: asset.currentValue,
    asOfDate: asset.asOfDate.substring(0, 10),
    location: asset.location || '',
    isActive: asset.isActive,
    notes: asset.notes || ''
  })
  formOpen.value = true
}

async function submitAsset() {
  saving.value = true
  formError.value = ''
  try {
    if (editingId.value) {
      await api.put(`assets/${editingId.value}`, form)
    } else {
      await api.post('assets', form)
    }
    formOpen.value = false
    await refresh()
  } catch (err) {
    formError.value = err instanceof Error ? err.message : 'Could not save the asset.'
  } finally {
    saving.value = false
  }
}

async function deleteAsset(asset: SwalekhaAsset) {
  if (!confirm(`Delete "${asset.name}"?`)) return
  try {
    await api.del(`assets/${asset.id}`)
    await refresh()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not delete the asset.'
  }
}
</script>
