<template>
  <div class="p-6">
    <div class="mb-6 flex flex-col md:flex-row justify-between items-start md:items-center gap-4">
      <div>
        <h1 class="text-2xl font-bold text-highlighted">Product Categories</h1>
        <p class="text-muted text-sm mt-1">Manage categories and product groups.</p>
      </div>
      <UButton color="primary" icon="i-lucide-plus" @click="openCreate">New Category</UButton>
    </div>

    <!-- Filters -->
    <UCard class="mb-6">
      <div class="flex flex-wrap gap-4">
        <UInput v-model="search" icon="i-lucide-search" placeholder="Search by name..." class="w-full md:w-64" @keyup.enter="fetchCategories" />
        <UButton color="gray" variant="ghost" icon="i-lucide-refresh-cw" @click="fetchCategories">Refresh</UButton>
      </div>
    </UCard>

    <!-- Data Table -->
    <UCard :ui="{ body: { padding: '' } }">
      <UTable
        :rows="filteredCategories"
        :columns="columns"
        :loading="loading"
        :empty-state="{ icon: 'i-lucide-folder-tree', label: 'No categories found' }"
      >
        <template #isActive-data="{ row }">
          <UBadge :color="row.isActive ? 'green' : 'gray'" variant="subtle">{{ row.isActive ? 'Active' : 'Inactive' }}</UBadge>
        </template>
        <template #actions-data="{ row }">
          <div class="flex items-center justify-end gap-2">
            <UButton color="gray" variant="ghost" icon="i-lucide-pencil" size="sm" @click="openEdit(row)" />
            <UButton color="red" variant="ghost" icon="i-lucide-trash-2" size="sm" @click="deleteCategory(row)" />
          </div>
        </template>
      </UTable>
    </UCard>

    <!-- Create/Edit Form Modal -->
    <USlideover v-model="isModalOpen" :title="isEditing ? 'Edit Category' : 'New Category'">
      <div class="p-4 flex-1 overflow-y-auto">
        <form @submit.prevent="saveCategory" class="space-y-4">
          <UFormGroup label="Category Name" required>
            <UInput v-model="form.name" placeholder="e.g. Shirts" required />
          </UFormGroup>
          
          <UFormGroup label="Product Group">
            <USelect v-model.number="form.productGroup" :options="productGroups" />
          </UFormGroup>

          <UFormGroup>
            <UCheckbox v-model="form.isActive" label="Is Active" />
          </UFormGroup>

          <div class="pt-4 flex justify-end gap-3 border-t border-gray-200 dark:border-gray-800 mt-6">
            <UButton color="gray" variant="ghost" @click="isModalOpen = false">Cancel</UButton>
            <UButton type="submit" color="primary" :loading="saving">Save Category</UButton>
          </div>
        </form>
      </div>
    </USlideover>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useToast } from '#imports'

const toast = useToast()
const config = useRuntimeConfig()

const search = ref('')
const categories = ref<any[]>([])
const loading = ref(false)

const isModalOpen = ref(false)
const isEditing = ref(false)
const saving = ref(false)
const form = ref<any>({})

const productGroups = [
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
]

const columns = [
  { key: 'name', label: 'Category Name' },
  { key: 'productGroupName', label: 'Product Group' },
  { key: 'isActive', label: 'Status' },
  { key: 'actions', label: '' }
]

const filteredCategories = computed(() => {
  if (!search.value) return categories.value
  const q = search.value.toLowerCase()
  return categories.value.filter(c => c.name.toLowerCase().includes(q) || (c.productGroupName || '').toLowerCase().includes(q))
})

function getHeaders() {
  const token = localStorage.getItem('garmetix.token')
  return { 'Authorization': `Bearer ${token}` }
}

async function fetchCategories() {
  loading.value = true
  try {
    const res = await $fetch<any[]>(`${config.public.apiBaseUrl}/masters/product-categories`, {
      headers: getHeaders()
    })
    categories.value = res || []
  } catch (err: any) {
    toast.add({ title: 'Error', description: err.message || 'Failed to load categories', color: 'red' })
  } finally {
    loading.value = false
  }
}

function openCreate() {
  const companyId = JSON.parse(localStorage.getItem('garmetix.user') || '{}')?.workspace?.companyId
  form.value = {
    name: '',
    productGroup: 2,
    isActive: true,
    companyId: companyId
  }
  isEditing.value = false
  isModalOpen.value = true
}

function openEdit(row: any) {
  form.value = { ...row, productGroup: Number(row.productGroup ?? 2) }
  isEditing.value = true
  isModalOpen.value = true
}

async function saveCategory() {
  saving.value = true
  try {
    if (isEditing.value) {
      await $fetch(`${config.public.apiBaseUrl}/masters/product-categories/${form.value.id}`, {
        method: 'PUT',
        headers: getHeaders(),
        body: form.value
      })
      toast.add({ title: 'Success', description: 'Category updated successfully', color: 'green' })
    } else {
      await $fetch(`${config.public.apiBaseUrl}/masters/product-categories`, {
        method: 'POST',
        headers: getHeaders(),
        body: form.value
      })
      toast.add({ title: 'Success', description: 'Category created successfully', color: 'green' })
    }
    isModalOpen.value = false
    await fetchCategories()
  } catch (err: any) {
    toast.add({ title: 'Error', description: err.data?.detail || err.message || 'Failed to save category', color: 'red' })
  } finally {
    saving.value = false
  }
}

async function deleteCategory(row: any) {
  if (!confirm(`Delete or inactivate category ${row.name}?`)) return
  try {
    await $fetch(`${config.public.apiBaseUrl}/masters/product-categories/${row.id}`, {
      method: 'DELETE',
      headers: getHeaders()
    })
    toast.add({ title: 'Success', description: 'Category deleted successfully', color: 'green' })
    await fetchCategories()
  } catch (err: any) {
    toast.add({ title: 'Error', description: err.data?.detail || err.message || 'Failed to delete category', color: 'red' })
  }
}

onMounted(() => {
  fetchCategories()
})
</script>
