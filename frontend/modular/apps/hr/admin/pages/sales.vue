<template>
  <div class="p-6">
    <div class="mb-6">
      <h1 class="text-2xl font-bold text-highlighted">Sales History</h1>
      <p class="text-muted text-sm mt-1">View and manage POS sales across all stores.</p>
    </div>

    <!-- Filters -->
    <UCard class="mb-6">
      <div class="flex flex-wrap items-center gap-4">
        <UFormGroup label="Search">
          <UInput v-model="search" icon="i-lucide-search" placeholder="Invoice # or Mobile..." class="w-full md:w-64" @keyup.enter="fetchSales" />
        </UFormGroup>
        
        <UFormGroup label="Date Range">
          <USelect v-model="dateRange" :options="['Today', 'Yesterday', 'This Week', 'This Month', 'Custom']" class="w-40" />
        </UFormGroup>

        <UFormGroup label="Store">
          <USelect v-model="storeId" :options="storeOptions" class="w-48" />
        </UFormGroup>

        <div class="flex-1"></div>

        <div class="mt-6">
          <UButton color="primary" @click="fetchSales" :loading="loading">Filter</UButton>
        </div>
      </div>
    </UCard>

    <!-- Data Table -->
    <UCard :ui="{ body: { padding: '' } }">
      <UTable
        :rows="sales"
        :columns="columns"
        :loading="loading"
        :empty-state="{ icon: 'i-lucide-receipt', label: 'No sales found' }"
      >
        <template #invoiceNumber-data="{ row }">
          <span class="font-medium text-highlighted">{{ row.invoiceNumber }}</span>
          <UBadge v-if="row.status === 'Cancelled'" color="red" size="sm" class="ml-2">Cancelled</UBadge>
        </template>
        <template #customer-data="{ row }">
          <div class="text-sm">
            <div>{{ row.customerName || 'Walk-in Customer' }}</div>
            <div class="text-xs text-muted">{{ row.customerMobile }}</div>
          </div>
        </template>
        <template #date-data="{ row }">
          {{ new Date(row.invoiceDate || row.createdAt).toLocaleString() }}
        </template>
        <template #total-data="{ row }">
          <span class="font-medium">{{ money(row.netAmount || row.totalAmount) }}</span>
        </template>
        <template #actions-data="{ row }">
          <div class="flex items-center justify-end gap-2">
            <UButton color="gray" variant="ghost" icon="i-lucide-eye" size="sm" @click="viewSale(row)" />
            <UButton color="gray" variant="ghost" icon="i-lucide-printer" size="sm" @click="printInvoice(row)" />
          </div>
        </template>
      </UTable>

      <div class="p-4 border-t border-gray-200 dark:border-gray-800 flex justify-between items-center">
        <div class="text-sm text-muted">Showing {{ sales.length }} sales</div>
        <UPagination v-model="page" :total="totalCount" :page-count="pageSize" />
      </div>
    </UCard>

    <USlideover v-model="isDetailOpen" :title="`Sale Details - ${selectedSale?.invoiceNumber}`" v-if="selectedSale">
      <div class="p-4 flex-1 overflow-y-auto">
        <div class="space-y-6">
          <div>
            <h3 class="text-lg font-semibold border-b border-gray-200 dark:border-gray-800 pb-2 mb-3">Summary</h3>
            <div class="grid grid-cols-2 gap-4 text-sm">
              <div><span class="text-muted block text-xs">Customer</span> {{ selectedSale.customerName || 'Walk-in' }}</div>
              <div><span class="text-muted block text-xs">Mobile</span> {{ selectedSale.customerMobile || '-' }}</div>
              <div><span class="text-muted block text-xs">Date</span> {{ new Date(selectedSale.invoiceDate).toLocaleString() }}</div>
              <div><span class="text-muted block text-xs">Total Amount</span> {{ money(selectedSale.netAmount) }}</div>
            </div>
          </div>

          <div>
            <h3 class="text-lg font-semibold border-b border-gray-200 dark:border-gray-800 pb-2 mb-3">Items</h3>
            <div class="space-y-3">
              <div v-for="(item, idx) in selectedSale.items || []" :key="idx" class="flex justify-between text-sm">
                <div>
                  <div class="font-medium">{{ item.productName }}</div>
                  <div class="text-xs text-muted">{{ item.quantity }} x {{ money(item.rate) }}</div>
                </div>
                <div class="font-medium">{{ money(item.total) }}</div>
              </div>
            </div>
          </div>

          <div>
            <h3 class="text-lg font-semibold border-b border-gray-200 dark:border-gray-800 pb-2 mb-3">Payments</h3>
            <div class="space-y-2">
              <div v-for="(payment, idx) in selectedSale.payments || []" :key="idx" class="flex justify-between text-sm">
                <span class="text-muted capitalize">{{ payment.paymentMode || 'Cash' }}</span>
                <span class="font-medium">{{ money(payment.amount) }}</span>
              </div>
            </div>
          </div>
        </div>
      </div>
      <div class="p-4 border-t border-gray-200 dark:border-gray-800 flex justify-end gap-3">
        <UButton color="gray" @click="isDetailOpen = false">Close</UButton>
        <UButton color="primary" icon="i-lucide-printer" @click="printInvoice(selectedSale)">Print Receipt</UButton>
      </div>
    </USlideover>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, watch } from 'vue'
import { useToast } from '#imports'

const toast = useToast()
const config = useRuntimeConfig()

const search = ref('')
const dateRange = ref('Today')
const storeId = ref('all')
const storeOptions = ref<any[]>([{ label: 'All Stores', value: 'all' }])
const sales = ref<any[]>([])
const loading = ref(false)
const page = ref(1)
const pageSize = ref(20)
const totalCount = ref(0)

const isDetailOpen = ref(false)
const selectedSale = ref<any>(null)

const columns = [
  { key: 'invoiceNumber', label: 'Invoice #' },
  { key: 'customer', label: 'Customer' },
  { key: 'storeName', label: 'Store' },
  { key: 'date', label: 'Date' },
  { key: 'total', label: 'Total' },
  { key: 'actions', label: '' }
]

function getHeaders() {
  const token = localStorage.getItem('garmetix.token')
  return { 'Authorization': `Bearer ${token}` }
}

function money(amount: any) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR' }).format(Number(amount || 0))
}

async function fetchStores() {
  try {
    const res = await $fetch<any[]>(`${config.public.apiBaseUrl}/stores`, { headers: getHeaders() })
    storeOptions.value = [
      { label: 'All Stores', value: 'all' },
      ...(res || []).map(s => ({ label: s.name, value: s.id }))
    ]
  } catch (err) {
    console.error('Failed to load stores', err)
  }
}

async function fetchSales() {
  loading.value = true
  try {
    const query = new URLSearchParams()
    if (search.value.trim()) query.set('query', search.value.trim())
    if (storeId.value !== 'all') query.set('storeId', storeId.value)
    
    // Convert dateRange to dates (simplified logic for now)
    const now = new Date()
    if (dateRange.value === 'Today') {
      query.set('fromDate', now.toISOString().split('T')[0])
    }
    
    query.set('page', String(page.value))
    query.set('pageSize', String(pageSize.value))

    const res = await $fetch<any>(`${config.public.apiBaseUrl}/billing/sales?${query.toString()}`, {
      headers: getHeaders()
    })
    
    sales.value = res.items || res || []
    totalCount.value = res.totalCount || sales.value.length
  } catch (err: any) {
    toast.add({ title: 'Error', description: err.message || 'Failed to fetch sales', color: 'red' })
  } finally {
    loading.value = false
  }
}

watch(page, () => fetchSales())

function viewSale(row: any) {
  selectedSale.value = row
  isDetailOpen.value = true
}

async function printInvoice(row: any) {
  try {
    const res = await $fetch<any>(`${config.public.apiBaseUrl}/billing/sales/${row.id}/pdf`, {
      headers: getHeaders()
    })
    if (res?.url) {
      window.open(res.url, '_blank')
    } else {
      toast.add({ title: 'Success', description: 'Print signal sent to server.', color: 'green' })
    }
  } catch (err: any) {
    toast.add({ title: 'Error', description: err.message || 'Failed to print invoice', color: 'red' })
  }
}

onMounted(() => {
  fetchStores()
  fetchSales()
})
</script>
