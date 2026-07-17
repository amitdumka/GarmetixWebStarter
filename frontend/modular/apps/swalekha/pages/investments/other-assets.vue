<template>
  <section class="space-y-5">
    <div class="flex flex-wrap items-start justify-between gap-3">
      <div>
        <p class="garmetix-kicker"><UIcon name="i-lucide-gem" class="size-4" /> Investments</p>
        <h1 class="garmetix-dashboard-title">Other Assets</h1>
        <p class="text-sm text-muted">PPF, EPF, NPS, Gold and similar holdings - simple value snapshots, updated as statements arrive.</p>
      </div>
      <div class="flex flex-wrap items-center gap-2">
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
        <UButton icon="i-lucide-plus" @click="openCreate">New Asset</UButton>
      </div>
    </div>

    <UAlert v-if="error" icon="i-lucide-circle-alert" color="error" variant="subtle" title="Could not load assets" :description="error" />

    <UCard :ui="{ body: 'p-4' }" class="max-w-xs">
      <p class="text-xs font-medium uppercase text-muted">Total Value</p>
      <p class="truncate text-lg font-semibold text-highlighted">{{ formatCurrency(totalValue) }}</p>
    </UCard>

    <UCard :ui="{ body: 'p-0' }">
      <UTable :data="assets" :columns="assetColumns" :loading="loading" class="w-full">
        <template #assetType-cell="{ row }">
          <UBadge color="primary" variant="subtle">{{ row.original.assetType }}</UBadge>
        </template>
        <template #currentValue-cell="{ row }"><span class="font-mono text-sm">{{ formatCurrency(row.original.currentValue) }}</span></template>
        <template #asOfDate-cell="{ row }">{{ formatDate(row.original.asOfDate) }}</template>
        <template #isActive-cell="{ row }">
          <UBadge :color="row.original.isActive ? 'success' : 'neutral'" variant="subtle">{{ row.original.isActive ? 'Active' : 'Inactive' }}</UBadge>
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
          <UFormField label="Asset Type" name="assetType">
            <USelect v-model="form.assetType" :items="['PPF', 'EPF', 'NPS', 'Gold', 'Other']" class="w-full" />
          </UFormField>

          <UFormField label="Name" name="name">
            <UInput v-model="form.name" required placeholder="PPF - SBI, EPF - Employer, Gold coins..." class="w-full" />
          </UFormField>

          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Current Value" name="currentValue">
              <UInput v-model.number="form.currentValue" type="number" step="0.01" icon="i-lucide-indian-rupee" class="w-full" />
            </UFormField>
            <UFormField label="As Of Date" name="asOfDate">
              <UInput v-model="form.asOfDate" type="date" class="w-full" />
            </UFormField>
          </div>

          <USwitch v-model="form.isActive" label="Active" />

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
import { useSwalekhaApiClient, type SwalekhaOtherAsset, type SwalekhaOtherAssetPayload } from '../../utils/swalekha-api'

useHead({ title: 'Other Assets - Swalekha' })

const api = useSwalekhaApiClient()
const loading = ref(false)
const error = ref('')
const assets = ref<SwalekhaOtherAsset[]>([])

const assetColumns = [
  { accessorKey: 'assetType', header: 'Type' },
  { accessorKey: 'name', header: 'Name' },
  { accessorKey: 'currentValue', header: 'Value' },
  { accessorKey: 'asOfDate', header: 'As Of' },
  { accessorKey: 'isActive', header: 'Status' },
  { accessorKey: 'actions', header: '', enableSorting: false }
]

const totalValue = computed(() => assets.value.filter(a => a.isActive).reduce((sum, a) => sum + a.currentValue, 0))

function formatCurrency(value: number) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(value || 0)
}

function formatDate(value: string) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  return new Intl.DateTimeFormat('en-IN', { dateStyle: 'medium' }).format(date)
}

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    assets.value = await api.get<SwalekhaOtherAsset[]>('other-assets?includeInactive=true')
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
const form = reactive<SwalekhaOtherAssetPayload>(defaultForm())

function defaultForm(): SwalekhaOtherAssetPayload {
  return { assetType: 'PPF', name: '', currentValue: 0, asOfDate: new Date().toISOString().substring(0, 10), isActive: true, notes: '' }
}

function openCreate() {
  editingId.value = null
  formError.value = ''
  Object.assign(form, defaultForm())
  formOpen.value = true
}

function openEdit(asset: SwalekhaOtherAsset) {
  editingId.value = asset.id
  formError.value = ''
  Object.assign(form, {
    assetType: asset.assetType,
    name: asset.name,
    currentValue: asset.currentValue,
    asOfDate: asset.asOfDate.substring(0, 10),
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
      await api.put(`other-assets/${editingId.value}`, form)
    } else {
      await api.post('other-assets', form)
    }
    formOpen.value = false
    await refresh()
  } catch (err) {
    formError.value = err instanceof Error ? err.message : 'Could not save the asset.'
  } finally {
    saving.value = false
  }
}

async function deleteAsset(asset: SwalekhaOtherAsset) {
  if (!confirm(`Delete "${asset.name}"?`)) return
  try {
    await api.del(`other-assets/${asset.id}`)
    await refresh()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not delete the asset.'
  }
}
</script>
