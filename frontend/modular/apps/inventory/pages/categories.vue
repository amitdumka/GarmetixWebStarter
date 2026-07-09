<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-folder-tree" class="size-4" /> Product categories</p>
          <h2 class="garmetix-dashboard-title">Categories</h2>
          <p class="garmetix-dashboard-subtitle">Manage product categories and their product group.</p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton icon="i-lucide-plus" color="primary" variant="solid" @click="startCreate">New Category</UButton>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />
    <UAlert v-if="message" color="success" variant="subtle" icon="i-lucide-circle-check" :description="message" />

    <section class="garmetix-section-card">
      <div class="mb-3 flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h3 class="garmetix-panel-title">Category Register</h3>
          <p class="garmetix-panel-subtitle">{{ filteredRows.length }} of {{ categories.length }} categories</p>
        </div>
        <UInput v-model="search" icon="i-lucide-search" placeholder="Search categories" class="sm:w-72" />
      </div>

      <AdminMasterTable :columns="columns" :rows="filteredRows" empty-text="No categories found.">
        <template #actions="{ row }">
          <div class="flex flex-wrap gap-1">
            <UButton icon="i-lucide-pencil" size="xs" color="neutral" variant="ghost" @click="startEdit(findRowById(row.id))" />
            <UButton icon="i-lucide-trash-2" size="xs" color="error" variant="ghost" @click="askDelete(row)" />
          </div>
        </template>
      </AdminMasterTable>
    </section>

    <UModal v-model:open="formOpen" :title="editMode === 'edit' ? 'Edit Category' : 'New Category'">
      <template #body>
        <form class="grid gap-3 sm:grid-cols-2" @submit.prevent="save">
          <label class="space-y-1 text-sm sm:col-span-2">
            <span class="text-muted">Category name</span>
            <UInput v-model="form.name" placeholder="e.g. Shirts" class="w-full" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Product group</span>
            <USelect v-model="form.productGroup" :items="productGroupItems" class="w-full" />
          </label>
          <label class="flex items-center gap-2 text-sm">
            <UCheckbox v-model="form.isActive" />
            <span class="text-muted">Active</span>
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

useHead({ title: 'Categories - Garmetix Inventory' })

const productGroupItems = [
  { value: 0, label: 'Shirting' },
  { value: 1, label: 'Suiting' },
  { value: 2, label: 'Readymade' },
  { value: 3, label: 'Sherwani' },
  { value: 4, label: 'Suits' },
  { value: 5, label: 'Blazers' },
  { value: 6, label: 'Kurta' },
  { value: 7, label: 'Kurta Pajama' },
  { value: 8, label: 'Pajama' },
  { value: 9, label: 'Pagadi' },
  { value: 10, label: 'Dupatta' },
  { value: 11, label: 'Pagadi Dupatta Set' },
  { value: 12, label: 'Brochs' },
  { value: 13, label: 'Kalgi' },
  { value: 14, label: 'Jodhpuri' },
  { value: 15, label: 'Winter Wear' },
  { value: 16, label: 'Inner Wear' },
  { value: 17, label: 'Shoes' },
  { value: 18, label: 'Nagra' },
  { value: 19, label: 'Accessories' },
  { value: 20, label: 'Others' }
] as const

function productGroupLabel(value: unknown) {
  const numeric = Number(value)
  return productGroupItems.find(item => item.value === numeric)?.label ?? '-'
}

function emptyForm() {
  return { id: '', name: '', productGroup: 2, isActive: true }
}

const { get, post, put, del } = useAdminApiClient()
const loading = ref(true)
const saving = ref(false)
const error = ref('')
const message = ref('')
const search = ref('')
const editMode = ref<'create' | 'edit'>('create')
const formOpen = ref(false)
const categories = ref<ApiRecord[]>([])
const companies = ref<ApiRecord[]>([])
const form = reactive(emptyForm())

const tableRows = computed(() => categories.value.map(item => ({
  id: readText(item, ['id'], ''),
  name: readText(item, ['name']),
  productGroupName: productGroupLabel(item.productGroup),
  status: item.isActive === false ? 'Inactive' : 'Active'
})))
const filteredRows = computed(() => {
  const term = search.value.trim().toLowerCase()
  if (!term) return tableRows.value
  return tableRows.value.filter(row => JSON.stringify(row).toLowerCase().includes(term))
})
const columns = [
  { key: 'name', label: 'Category Name' },
  { key: 'productGroupName', label: 'Product Group' },
  { key: 'status', label: 'Status' }
]

function findRowById(id: unknown) {
  return categories.value.find(item => readText(item, ['id'], '') === id) ?? null
}

function resolveCompanyId() {
  const companyId = readText(companies.value[0], ['id'], '')
  if (!companyId) throw new Error('Run quick setup before saving categories.')
  return companyId
}

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const [categoryData, companyData] = await Promise.allSettled([
      get<unknown>('product-categories'),
      get<unknown>('companies')
    ])
    if (categoryData.status === 'fulfilled') categories.value = toRows(categoryData.value)
    if (companyData.status === 'fulfilled') companies.value = toRows(companyData.value)

    if (categoryData.status === 'rejected') {
      error.value = categoryData.reason instanceof Error ? categoryData.reason.message : 'Unable to load categories.'
    }
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load categories.'
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
    productGroup: Number(item.productGroup ?? 2),
    isActive: item.isActive !== false
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
    if (!form.name.trim()) throw new Error('Enter category name.')
    const companyId = resolveCompanyId()
    const payload = {
      name: form.name.trim(),
      productGroup: Number(form.productGroup),
      isActive: Boolean(form.isActive),
      companyId
    }

    if (editMode.value === 'edit' && form.id) {
      await put<unknown>(`product-categories/${form.id}`, payload)
      message.value = 'Category updated.'
    } else {
      await post<unknown>('product-categories', payload)
      message.value = 'Category saved.'
    }

    formOpen.value = false
    await refresh()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to save category.'
  } finally {
    saving.value = false
  }
}

function askDelete(row: { id: string, name: string }) {
  if (!row.id) return
  if (!window.confirm(`Delete category "${row.name}"?`)) return
  confirmDelete(row.id)
}

async function confirmDelete(id: string) {
  error.value = ''
  message.value = ''
  try {
    await del<unknown>(`product-categories/${id}`)
    message.value = 'Category deleted.'
    await refresh()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to delete category.'
  }
}

onMounted(refresh)
</script>
