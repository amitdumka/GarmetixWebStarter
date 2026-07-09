<template>
  <div class="p-6">
    <div class="mb-6 flex flex-col md:flex-row justify-between items-start md:items-center gap-4">
      <div>
        <h1 class="text-2xl font-bold text-highlighted">Products Master</h1>
        <p class="text-muted text-sm mt-1">Manage your catalog, stock, and barcodes.</p>
      </div>
      <UButton color="primary" icon="i-lucide-plus" @click="openCreate">New Product</UButton>
    </div>

    <!-- Filters -->
    <UCard class="mb-6">
      <div class="flex flex-wrap gap-4">
        <UInput v-model="search" icon="i-lucide-search" placeholder="Search by name or barcode..." class="w-full md:w-64" @keyup.enter="fetchProducts" />
        <USelect v-model="categoryFilter" :options="categoryOptions" placeholder="All Categories" class="w-full md:w-48" @change="fetchProducts" />
        <USelect v-model="brandFilter" :options="brandOptions" placeholder="All Brands" class="w-full md:w-48" @change="fetchProducts" />
        <UButton color="gray" variant="ghost" icon="i-lucide-refresh-cw" @click="fetchProducts">Refresh</UButton>
      </div>
    </UCard>

    <!-- Data Table -->
    <UCard :ui="{ body: { padding: '' } }">
      <UTable
        :rows="products"
        :columns="columns"
        :loading="loading"
        :empty-state="{ icon: 'i-lucide-box', label: 'No products found' }"
      >
        <template #mrp-data="{ row }">
          ₹{{ row.mrp?.toFixed(2) }}
        </template>
        <template #actions-data="{ row }">
          <div class="flex items-center justify-end gap-2">
            <UButton color="gray" variant="ghost" icon="i-lucide-pencil" size="sm" @click="openEdit(row)" />
          </div>
        </template>
      </UTable>
      
      <!-- Pagination -->
      <div class="flex items-center justify-between px-4 py-3 border-t border-gray-200 dark:border-gray-800" v-if="total > 0">
        <span class="text-sm text-muted">Showing {{ ((page - 1) * pageSize) + 1 }} to {{ Math.min(page * pageSize, total) }} of {{ total }} entries</span>
        <UPagination v-model="page" :page-count="pageSize" :total="total" @update:model-value="fetchProducts" />
      </div>
    </UCard>

    <!-- Create/Edit Form Modal -->
    <USlideover v-model="isModalOpen" :title="isEditing ? 'Edit Product' : 'New Product'">
      <div class="p-4 flex-1 overflow-y-auto">
        <form @submit.prevent="saveProduct" class="space-y-4">
          <UFormGroup label="Product Name" required>
            <UInput v-model="form.name" placeholder="e.g. Cotton Shirt" required />
          </UFormGroup>
          
          <div class="grid grid-cols-2 gap-4">
            <UFormGroup label="Barcode">
              <UInput v-model="form.barcode" placeholder="Auto-generated if empty" />
            </UFormGroup>
            <UFormGroup label="HSN Code">
              <UInput v-model="form.hsnCode" placeholder="e.g. 6205" />
            </UFormGroup>
          </div>

          <div class="grid grid-cols-2 gap-4">
            <UFormGroup label="MRP (₹)" required>
              <UInput v-model.number="form.mrp" type="number" step="0.01" required />
            </UFormGroup>
            <UFormGroup label="Cost Price (₹)">
              <UInput v-model.number="form.costPrice" type="number" step="0.01" />
            </UFormGroup>
          </div>

          <div class="grid grid-cols-2 gap-4">
            <UFormGroup label="Category">
              <USelect v-model="form.productCategoryId" :options="categoryOptions" placeholder="Select Category" />
            </UFormGroup>
            <UFormGroup label="Brand">
              <UInput v-model="form.brand" placeholder="e.g. Raymond" />
            </UFormGroup>
          </div>

          <UFormGroup label="Tax">
            <USelect v-model="form.taxId" :options="taxOptions" placeholder="Select Tax Slab" />
          </UFormGroup>

          <UFormGroup label="Opening Quantity">
            <UInput v-model.number="form.openingQuantity" type="number" :disabled="isEditing" placeholder="0" />
          </UFormGroup>

          <div class="pt-4 flex justify-end gap-3 border-t border-gray-200 dark:border-gray-800 mt-6">
            <UButton color="gray" variant="ghost" @click="isModalOpen = false">Cancel</UButton>
            <UButton type="submit" color="primary" :loading="saving">Save Product</UButton>
          </div>
        </form>
      </div>
    </USlideover>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useToast } from '#imports'

const toast = useToast()
const config = useRuntimeConfig()

const search = ref('')
const categoryFilter = ref('')
const brandFilter = ref('')
const page = ref(1)
const pageSize = ref(15)
const total = ref(0)
const products = ref<any[]>([])
const loading = ref(false)

const isModalOpen = ref(false)
const isEditing = ref(false)
const saving = ref(false)
const form = ref<any>({})

const categoryOptions = ref<{label: string, value: string}[]>([])
const taxOptions = ref<{label: string, value: string}[]>([])
const brandOptions = ref<{label: string, value: string}[]>([])

const columns = [
  { key: 'name', label: 'Product Name' },
  { key: 'barcode', label: 'Barcode' },
  { key: 'categoryName', label: 'Category' },
  { key: 'brand', label: 'Brand' },
  { key: 'mrp', label: 'MRP (₹)' },
  { key: 'actions', label: '' }
]

function getHeaders() {
  const token = localStorage.getItem('garmetix.token')
  return { 'Authorization': `Bearer ${token}` }
}

async function fetchOptions() {
  try {
    const res = await $fetch<any>(`${config.public.apiBaseUrl}/inventory/product-master/options`, {
      headers: getHeaders()
    })
    
    categoryOptions.value = (res.categories || []).map((c: any) => ({ label: c.name, value: c.id }))
    taxOptions.value = (res.taxes || []).map((t: any) => ({ label: t.name, value: t.id }))
    brandOptions.value = (res.brands || []).map((b: string) => ({ label: b, value: b }))
  } catch (err: any) {
    console.error('Failed to load options', err)
  }
}

async function fetchProducts() {
  loading.value = true
  try {
    const query = new URLSearchParams({
      page: page.value.toString(),
      pageSize: pageSize.value.toString()
    })
    if (search.value) query.append('search', search.value)
    if (categoryFilter.value) query.append('categoryId', categoryFilter.value)
    if (brandFilter.value) query.append('brand', brandFilter.value)

    const res = await $fetch<any>(`${config.public.apiBaseUrl}/inventory/product-master/paged?${query.toString()}`, {
      headers: getHeaders()
    })
    
    products.value = res.items || []
    total.value = res.total || 0
  } catch (err: any) {
    toast.add({ title: 'Error', description: err.message || 'Failed to load products', color: 'red' })
  } finally {
    loading.value = false
  }
}

function openCreate() {
  form.value = {
    name: '',
    barcode: '',
    hsnCode: '',
    mrp: 0,
    costPrice: 0,
    openingQuantity: 0,
    productCategoryId: '',
    brand: '',
    taxId: ''
  }
  isEditing.value = false
  isModalOpen.value = true
}

function openEdit(row: any) {
  form.value = { ...row }
  isEditing.value = true
  isModalOpen.value = true
}

async function saveProduct() {
  saving.value = true
  try {
    if (isEditing.value) {
      await $fetch(`${config.public.apiBaseUrl}/inventory/product-master/${form.value.id}`, {
        method: 'PUT',
        headers: getHeaders(),
        body: form.value
      })
      toast.add({ title: 'Success', description: 'Product updated successfully', color: 'green' })
    } else {
      await $fetch(`${config.public.apiBaseUrl}/inventory/product-master`, {
        method: 'POST',
        headers: getHeaders(),
        body: form.value
      })
      toast.add({ title: 'Success', description: 'Product created successfully', color: 'green' })
    }
    isModalOpen.value = false
    await fetchProducts()
  } catch (err: any) {
    toast.add({ title: 'Error', description: err.data?.detail || err.message || 'Failed to save product', color: 'red' })
  } finally {
    saving.value = false
  }
}

onMounted(() => {
  fetchOptions()
  fetchProducts()
})
</script>
