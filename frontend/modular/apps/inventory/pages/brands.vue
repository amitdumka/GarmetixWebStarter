<template>
  <div class="p-6">
    <div class="mb-6 flex flex-col md:flex-row justify-between items-start md:items-center gap-4">
      <div>
        <h1 class="text-2xl font-bold text-highlighted">Brands Master</h1>
        <p class="text-muted text-sm mt-1">Manage product brands and labels.</p>
      </div>
      <UButton color="primary" icon="i-lucide-plus" @click="openCreate">New Brand</UButton>
    </div>

    <!-- Filters -->
    <UCard class="mb-6">
      <div class="flex flex-wrap gap-4">
        <UInput v-model="search" icon="i-lucide-search" placeholder="Search by name..." class="w-full md:w-64" @keyup.enter="fetchBrands" />
        <UButton color="gray" variant="ghost" icon="i-lucide-refresh-cw" @click="fetchBrands">Refresh</UButton>
      </div>
    </UCard>

    <!-- Data Table -->
    <UCard :ui="{ body: { padding: '' } }">
      <UTable
        :rows="filteredBrands"
        :columns="columns"
        :loading="loading"
        :empty-state="{ icon: 'i-lucide-tag', label: 'No brands found' }"
      >
        <template #isActive-data="{ row }">
          <UBadge :color="row.isActive ? 'green' : 'gray'" variant="subtle">{{ row.isActive ? 'Active' : 'Inactive' }}</UBadge>
        </template>
        <template #actions-data="{ row }">
          <div class="flex items-center justify-end gap-2">
            <UButton color="gray" variant="ghost" icon="i-lucide-pencil" size="sm" @click="openEdit(row)" />
            <UButton color="red" variant="ghost" icon="i-lucide-trash-2" size="sm" @click="deleteBrand(row)" />
          </div>
        </template>
      </UTable>
    </UCard>

    <!-- Create/Edit Form Modal -->
    <USlideover v-model="isModalOpen" :title="isEditing ? 'Edit Brand' : 'New Brand'">
      <div class="p-4 flex-1 overflow-y-auto">
        <form @submit.prevent="saveBrand" class="space-y-4">
          <UFormGroup label="Brand Name" required>
            <UInput v-model="form.name" placeholder="e.g. Raymond" required />
          </UFormGroup>
          
          <UFormGroup label="Description">
            <UInput v-model="form.description" placeholder="Optional description..." />
          </UFormGroup>

          <UFormGroup>
            <UCheckbox v-model="form.isActive" label="Is Active" />
          </UFormGroup>

          <div class="pt-4 flex justify-end gap-3 border-t border-gray-200 dark:border-gray-800 mt-6">
            <UButton color="gray" variant="ghost" @click="isModalOpen = false">Cancel</UButton>
            <UButton type="submit" color="primary" :loading="saving">Save Brand</UButton>
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
const brands = ref<any[]>([])
const loading = ref(false)

const isModalOpen = ref(false)
const isEditing = ref(false)
const saving = ref(false)
const form = ref<any>({})

const columns = [
  { key: 'name', label: 'Brand Name' },
  { key: 'description', label: 'Description' },
  { key: 'isActive', label: 'Status' },
  { key: 'actions', label: '' }
]

const filteredBrands = computed(() => {
  if (!search.value) return brands.value
  const q = search.value.toLowerCase()
  return brands.value.filter(b => b.name.toLowerCase().includes(q) || (b.description || '').toLowerCase().includes(q))
})

function getHeaders() {
  const token = localStorage.getItem('garmetix.token')
  return { 'Authorization': `Bearer ${token}` }
}

async function fetchBrands() {
  loading.value = true
  try {
    const res = await $fetch<any[]>(`${config.public.apiBaseUrl}/masters/brands`, {
      headers: getHeaders()
    })
    brands.value = res || []
  } catch (err: any) {
    toast.add({ title: 'Error', description: err.message || 'Failed to load brands', color: 'red' })
  } finally {
    loading.value = false
  }
}

function openCreate() {
  const companyId = JSON.parse(localStorage.getItem('garmetix.user') || '{}')?.workspace?.companyId
  form.value = {
    name: '',
    description: '',
    isActive: true,
    companyId: companyId
  }
  isEditing.value = false
  isModalOpen.value = true
}

function openEdit(row: any) {
  form.value = { ...row }
  isEditing.value = true
  isModalOpen.value = true
}

async function saveBrand() {
  saving.value = true
  try {
    if (isEditing.value) {
      await $fetch(`${config.public.apiBaseUrl}/masters/brands/${form.value.id}`, {
        method: 'PUT',
        headers: getHeaders(),
        body: form.value
      })
      toast.add({ title: 'Success', description: 'Brand updated successfully', color: 'green' })
    } else {
      await $fetch(`${config.public.apiBaseUrl}/masters/brands`, {
        method: 'POST',
        headers: getHeaders(),
        body: form.value
      })
      toast.add({ title: 'Success', description: 'Brand created successfully', color: 'green' })
    }
    isModalOpen.value = false
    await fetchBrands()
  } catch (err: any) {
    toast.add({ title: 'Error', description: err.data?.detail || err.message || 'Failed to save brand', color: 'red' })
  } finally {
    saving.value = false
  }
}

async function deleteBrand(row: any) {
  if (!confirm(`Delete or inactivate brand ${row.name}?`)) return
  try {
    await $fetch(`${config.public.apiBaseUrl}/masters/brands/${row.id}`, {
      method: 'DELETE',
      headers: getHeaders()
    })
    toast.add({ title: 'Success', description: 'Brand deleted successfully', color: 'green' })
    await fetchBrands()
  } catch (err: any) {
    toast.add({ title: 'Error', description: err.data?.detail || err.message || 'Failed to delete brand', color: 'red' })
  }
}

onMounted(() => {
  fetchBrands()
})
</script>
