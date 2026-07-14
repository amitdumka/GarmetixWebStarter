<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-tag" class="size-4" /> Product brands</p>
          <h2 class="garmetix-dashboard-title">Brands</h2>
          <p class="garmetix-dashboard-subtitle">Manage the brand master used for product labels.</p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton icon="i-lucide-plus" color="primary" variant="solid" @click="startCreate">New Brand</UButton>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />
    <UAlert v-if="message" color="success" variant="subtle" icon="i-lucide-circle-check" :description="message" />

    <section class="garmetix-section-card">
      <div class="mb-3 flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h3 class="garmetix-panel-title">Brand Register</h3>
          <p class="garmetix-panel-subtitle">{{ filteredRows.length }} of {{ brands.length }} brands</p>
        </div>
        <UInput v-model="search" icon="i-lucide-search" placeholder="Search brands" class="sm:w-72" />
      </div>

      <AdminMasterTable :columns="columns" :rows="filteredRows" empty-text="No brands found.">
        <template #actions="{ row }">
          <div class="flex flex-wrap gap-1">
            <UButton icon="i-lucide-pencil" size="xs" color="neutral" variant="ghost" @click="startEdit(findRowById(row.id))" />
            <UButton icon="i-lucide-trash-2" size="xs" color="error" variant="ghost" @click="askDelete(row)" />
          </div>
        </template>
      </AdminMasterTable>
    </section>

    <UModal v-model:open="formOpen" :title="editMode === 'edit' ? 'Edit Brand' : 'New Brand'">
      <template #body>
        <form class="grid gap-3 sm:grid-cols-2" @submit.prevent="save">
          <label class="space-y-1 text-sm">
            <span class="text-muted">Brand name</span>
            <UInput v-model="form.name" placeholder="e.g. Raymond" class="w-full" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Brand code</span>
            <UInput v-model="form.brandCode" placeholder="e.g. RAY" class="w-full" />
          </label>

          <div class="flex justify-end gap-2 sm:col-span-2">
            <UButton type="submit" icon="i-lucide-save" color="primary" :loading="saving">
              {{ editMode === 'edit' ? 'Update' : 'Save' }}
            </UButton>
          </div>
        </form>
      </template>
    </UModal>
  </section>
</template>

<script setup lang="ts">
import { readText, toRows, type ApiRecord, useAdminApiClient } from '../utils/admin-api'

useHead({ title: 'Brands - Garmetix Inventory' })

function emptyForm() {
  return { id: '', name: '', brandCode: '' }
}

const { get, post, put, del } = useAdminApiClient()
const loading = ref(true)
const saving = ref(false)
const error = ref('')
const message = ref('')
const search = ref('')
const editMode = ref<'create' | 'edit'>('create')
const formOpen = ref(false)
const brands = ref<ApiRecord[]>([])
const form = reactive(emptyForm())

const tableRows = computed(() => brands.value.map(item => ({
  id: readText(item, ['id'], ''),
  name: readText(item, ['name']),
  brandCode: readText(item, ['brandCode'])
})))
const filteredRows = computed(() => {
  const term = search.value.trim().toLowerCase()
  if (!term) return tableRows.value
  return tableRows.value.filter(row => JSON.stringify(row).toLowerCase().includes(term))
})
const columns = [
  { key: 'name', label: 'Brand Name' },
  { key: 'brandCode', label: 'Brand Code' }
]

function findRowById(id: unknown) {
  return brands.value.find(item => readText(item, ['id'], '') === id) ?? null
}

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    brands.value = toRows(await get<unknown>('brands'))
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load brands.'
  } finally {
    loading.value = false
  }
}

function startCreate() {
  editMode.value = 'create'
  Object.assign(form, emptyForm())
  message.value = ''
  error.value = ''
  formOpen.value = true
}

function startEdit(item: ApiRecord | null) {
  if (!item) return
  editMode.value = 'edit'
  Object.assign(form, {
    id: readText(item, ['id'], ''),
    name: readText(item, ['name'], ''),
    brandCode: readText(item, ['brandCode'], '')
  })
  message.value = ''
  error.value = ''
  formOpen.value = true
}

async function save() {
  saving.value = true
  error.value = ''
  message.value = ''
  try {
    if (!form.name.trim()) throw new Error('Enter brand name.')
    const payload = {
      name: form.name.trim(),
      brandCode: form.brandCode.trim()
    }

    if (editMode.value === 'edit' && form.id) {
      await put<unknown>(`brands/${form.id}`, payload)
      message.value = 'Brand updated.'
    } else {
      await post<unknown>('brands', payload)
      message.value = 'Brand saved.'
    }

    formOpen.value = false
    await refresh()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to save brand.'
  } finally {
    saving.value = false
  }
}

function askDelete(row: { id: string, name: string }) {
  if (!row.id) return
  if (!window.confirm(`Delete brand "${row.name}"?`)) return
  confirmDelete(row.id)
}

async function confirmDelete(id: string) {
  error.value = ''
  message.value = ''
  try {
    await del<unknown>(`brands/${id}`)
    message.value = 'Brand deleted.'
    await refresh()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to delete brand.'
  }
}

onMounted(refresh)
</script>
