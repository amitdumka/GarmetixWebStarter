<template>
  <div class="p-6">
    <div class="mb-6">
      <h1 class="text-2xl font-bold text-highlighted">Barcode Printing</h1>
      <p class="text-muted text-sm mt-1">Search for products and print barcode labels.</p>
    </div>

    <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
      <div class="lg:col-span-2 space-y-6">
        <UCard>
          <template #header>
            <div class="flex items-center gap-2">
              <UIcon name="i-lucide-search" class="text-primary size-5" />
              <h3 class="font-semibold">Search Products</h3>
            </div>
          </template>
          
          <div class="flex gap-4">
            <UInput v-model="search" placeholder="Search by name or barcode..." class="flex-1" @keyup.enter="searchProducts" />
            <UButton color="primary" @click="searchProducts" :loading="loading">Search</UButton>
          </div>

          <UTable
            v-if="products.length > 0"
            :rows="products"
            :columns="columns"
            class="mt-4"
          >
            <template #actions-data="{ row }">
              <UButton color="gray" variant="ghost" icon="i-lucide-plus" size="sm" @click="addToList(row)">Add</UButton>
            </template>
          </UTable>
        </UCard>
      </div>

      <div>
        <UCard>
          <template #header>
            <div class="flex items-center justify-between">
              <div class="flex items-center gap-2">
                <UIcon name="i-lucide-printer" class="text-primary size-5" />
                <h3 class="font-semibold">Print Queue</h3>
              </div>
              <UBadge color="gray">{{ selectedProducts.length }} items</UBadge>
            </div>
          </template>
          
          <div v-if="selectedProducts.length === 0" class="text-center py-6 text-muted text-sm">
            No products added to the print queue.
          </div>
          
          <div v-else class="space-y-4">
            <div v-for="(item, idx) in selectedProducts" :key="item.id + idx" class="flex items-center justify-between gap-2 p-2 border border-gray-200 dark:border-gray-800 rounded-lg">
              <div class="min-w-0 flex-1">
                <div class="text-sm font-semibold truncate">{{ item.name }}</div>
                <div class="text-xs text-muted truncate">{{ item.barcode }} • ₹{{ item.mrp }}</div>
              </div>
              <div class="flex items-center gap-2 shrink-0">
                <UInput v-model.number="item.copies" type="number" min="1" class="w-16" size="sm" />
                <UButton color="red" variant="ghost" icon="i-lucide-trash" size="sm" @click="removeFromList(idx)" />
              </div>
            </div>

            <div class="pt-4 border-t border-gray-200 dark:border-gray-800">
              <UFormGroup label="Label Size">
                <USelect v-model="labelSize" :options="[{ label: '50x30 mm', value: '50x30' }, { label: '50x25 mm', value: '50x25' }]" />
              </UFormGroup>
              <UFormGroup label="Print Mode" class="mt-4">
                <USelect v-model="printMode" :options="[{ label: 'Browser Print', value: 'browser' }, { label: 'TSPL Download', value: 'tspl' }]" />
              </UFormGroup>
              <UButton color="primary" block class="mt-6" icon="i-lucide-printer" :loading="printing" @click="printLabels">
                Print {{ totalCopies }} Labels
              </UButton>
            </div>
          </div>
        </UCard>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useToast } from '#imports'

const toast = useToast()
const config = useRuntimeConfig()

const search = ref('')
const loading = ref(false)
const printing = ref(false)
const products = ref<any[]>([])
const selectedProducts = ref<any[]>([])

const labelSize = ref('50x30')
const printMode = ref('browser')

const columns = [
  { key: 'name', label: 'Product Name' },
  { key: 'barcode', label: 'Barcode' },
  { key: 'mrp', label: 'MRP' },
  { key: 'actions', label: '' }
]

const totalCopies = computed(() => {
  return selectedProducts.value.reduce((sum, item) => sum + (Number(item.copies) || 1), 0)
})

function getHeaders() {
  const token = localStorage.getItem('garmetix.token')
  return { 'Authorization': `Bearer ${token}` }
}

async function searchProducts() {
  if (!search.value.trim()) return
  loading.value = true
  try {
    const res = await $fetch<any[]>(`${config.public.apiBaseUrl}/price-tags/search?query=${encodeURIComponent(search.value)}`, {
      headers: getHeaders()
    })
    products.value = res || []
  } catch (err: any) {
    toast.add({ title: 'Error', description: err.message || 'Search failed', color: 'red' })
  } finally {
    loading.value = false
  }
}

function addToList(row: any) {
  const exists = selectedProducts.value.find(p => p.id === row.id)
  if (exists) {
    exists.copies++
  } else {
    selectedProducts.value.push({ ...row, copies: 1 })
  }
  toast.add({ title: 'Added', description: `${row.name} added to print queue`, color: 'green' })
}

function removeFromList(idx: number) {
  selectedProducts.value.splice(idx, 1)
}

async function printLabels() {
  if (selectedProducts.value.length === 0) return
  printing.value = true
  try {
    const payload = {
      labelSize: labelSize.value,
      printerLanguage: printMode.value,
      storeName: 'Garmetix',
      tags: selectedProducts.value.map(p => ({
        id: p.id,
        copies: Number(p.copies) || 1,
        mrp: p.mrp
      }))
    }
    
    const res = await $fetch<any>(`${config.public.apiBaseUrl}/price-tags/prepare`, {
      method: 'POST',
      headers: getHeaders(),
      body: payload
    })
    
    toast.add({ title: 'Success', description: 'Print job prepared successfully!', color: 'green' })
    
    if (printMode.value === 'tspl' && res.commands) {
      const blob = new Blob([res.commands], { type: 'text/plain' })
      const url = URL.createObjectURL(blob)
      const a = document.createElement('a')
      a.href = url
      a.download = 'tags.prn'
      a.click()
    }
    
  } catch (err: any) {
    toast.add({ title: 'Error', description: err.message || 'Print failed', color: 'red' })
  } finally {
    printing.value = false
  }
}
</script>
