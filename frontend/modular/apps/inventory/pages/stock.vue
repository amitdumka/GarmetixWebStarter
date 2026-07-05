<template>
  <div class="p-6">
    <div class="mb-6 flex flex-col md:flex-row justify-between items-start md:items-center gap-4">
      <div>
        <h1 class="text-2xl font-bold text-highlighted">Stock Operations</h1>
        <p class="text-muted text-sm mt-1">Manage stock adjustments, transfers, and view history.</p>
      </div>
      <div class="flex gap-2">
        <UButton color="white" icon="i-lucide-arrow-right-left" @click="openTransfer">Transfer</UButton>
        <UButton color="primary" icon="i-lucide-plus" @click="openAdjustment">New Adjustment</UButton>
      </div>
    </div>

    <!-- Filters -->
    <UCard class="mb-6">
      <div class="flex flex-wrap gap-4">
        <UInput v-model="search" icon="i-lucide-search" placeholder="Search document number..." class="w-full md:w-64" @keyup.enter="fetchDocuments" />
        <UButton color="gray" variant="ghost" icon="i-lucide-refresh-cw" @click="fetchDocuments">Refresh</UButton>
      </div>
    </UCard>

    <!-- Data Table -->
    <UCard :ui="{ body: { padding: '' } }">
      <UTable
        :rows="documents"
        :columns="columns"
        :loading="loading"
        :empty-state="{ icon: 'i-lucide-history', label: 'No stock operations found' }"
      >
        <template #type-data="{ row }">
          <UBadge color="gray" variant="subtle">{{ row.type }}</UBadge>
        </template>
        <template #createdAt-data="{ row }">
          {{ new Date(row.createdAt).toLocaleString() }}
        </template>
        <template #actions-data="{ row }">
          <div class="flex items-center justify-end gap-2">
            <UButton color="gray" variant="ghost" icon="i-lucide-eye" size="sm" @click="viewDocument(row)" />
          </div>
        </template>
      </UTable>
    </UCard>

    <!-- Adjustment Form Modal -->
    <USlideover v-model="isAdjOpen" title="New Stock Adjustment">
      <div class="p-4 flex-1 overflow-y-auto">
        <form @submit.prevent="saveAdjustment" class="space-y-4">
          <UFormGroup label="Product" required>
            <USelect v-model="adjForm.productId" :options="productOptions" placeholder="Select Product" required />
          </UFormGroup>
          
          <div class="grid grid-cols-2 gap-4">
            <UFormGroup label="Quantity" required>
              <UInput v-model.number="adjForm.quantity" type="number" required />
            </UFormGroup>
            <UFormGroup label="Type" required>
              <USelect v-model="adjForm.operationType" :options="[{label: 'Add (+)', value: 'In'}, {label: 'Remove (-)', value: 'Out'}]" required />
            </UFormGroup>
          </div>

          <UFormGroup label="Reason" required>
            <UInput v-model="adjForm.reason" placeholder="e.g. Damaged goods, inventory count..." required />
          </UFormGroup>

          <div class="pt-4 flex justify-end gap-3 border-t border-gray-200 dark:border-gray-800 mt-6">
            <UButton color="gray" variant="ghost" @click="isAdjOpen = false">Cancel</UButton>
            <UButton type="submit" color="primary" :loading="saving">Save Adjustment</UButton>
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
const documents = ref<any[]>([])
const productOptions = ref<any[]>([])
const loading = ref(false)

const isAdjOpen = ref(false)
const saving = ref(false)
const adjForm = ref<any>({
  operationType: 'In'
})

const columns = [
  { key: 'documentNumber', label: 'Doc #' },
  { key: 'type', label: 'Operation Type' },
  { key: 'reason', label: 'Reason' },
  { key: 'createdAt', label: 'Date' },
  { key: 'actions', label: '' }
]

function getHeaders() {
  const token = localStorage.getItem('garmetix.token')
  return { 'Authorization': `Bearer ${token}` }
}

async function fetchDocuments() {
  loading.value = true
  try {
    const res = await $fetch<any[]>(`${config.public.apiBaseUrl}/inventory/stock-operations/documents?search=${encodeURIComponent(search.value)}`, {
      headers: getHeaders()
    })
    documents.value = res || []
  } catch (err: any) {
    toast.add({ title: 'Error', description: err.message || 'Failed to load documents', color: 'red' })
  } finally {
    loading.value = false
  }
}

async function fetchProducts() {
  try {
    const res = await $fetch<any>(`${config.public.apiBaseUrl}/inventory/product-master/paged?pageSize=100`, {
      headers: getHeaders()
    })
    productOptions.value = (res.items || []).map((p: any) => ({ label: p.name, value: p.id }))
  } catch (err) {
    console.error('Failed to load products for adjustment', err)
  }
}

function openAdjustment() {
  adjForm.value = {
    productId: '',
    quantity: 1,
    operationType: 'In',
    reason: ''
  }
  isAdjOpen.value = true
}

function openTransfer() {
  toast.add({ title: 'Notice', description: 'Store Transfer is not fully implemented in this preview.', color: 'blue' })
}

function viewDocument(row: any) {
  toast.add({ title: 'Document', description: `Viewing document ${row.documentNumber} is coming soon.`, color: 'blue' })
}

async function saveAdjustment() {
  saving.value = true
  try {
    // Determine negative qty if 'Out'
    const qty = adjForm.value.operationType === 'Out' ? -Math.abs(adjForm.value.quantity) : Math.abs(adjForm.value.quantity)
    
    const payload = {
      reason: adjForm.value.reason,
      items: [
        { productId: adjForm.value.productId, quantity: qty }
      ]
    }

    await $fetch(`${config.public.apiBaseUrl}/inventory/stock-operations/adjustment`, {
      method: 'POST',
      headers: getHeaders(),
      body: payload
    })
    
    toast.add({ title: 'Success', description: 'Stock adjustment saved successfully', color: 'green' })
    isAdjOpen.value = false
    await fetchDocuments()
  } catch (err: any) {
    toast.add({ title: 'Error', description: err.data?.detail || err.message || 'Failed to save adjustment', color: 'red' })
  } finally {
    saving.value = false
  }
}

onMounted(() => {
  fetchDocuments()
  fetchProducts()
})
</script>
