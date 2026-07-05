<template>
  <section class="garmetix-page-stack" :aria-busy="loading">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-receipt" class="size-4" /> Transactions</p>
          <h2 class="garmetix-dashboard-title">Voucher Management</h2>
          <p class="garmetix-dashboard-subtitle">
            View, track, and manage all manual accounting entries.
          </p>
        </div>
        <div class="flex gap-2">
          <UButton color="neutral" variant="ghost" icon="i-lucide-refresh-cw" @click="fetchVouchers">Refresh</UButton>
          <UButton icon="i-lucide-plus" @click="isCreateOpen = true">New Voucher</UButton>
        </div>
      </div>
    </div>

    <UCard :ui="{ body: { padding: '' } }">
      <div class="p-4 border-b border-gray-200 dark:border-gray-800 flex gap-4">
        <UInput v-model="search" icon="i-lucide-search" placeholder="Search Vouchers..." class="w-full md:w-64" @keyup.enter="fetchVouchers" />
      </div>

      <UTable
        :rows="vouchers"
        :columns="columns"
        :loading="loading"
        :empty-state="{ icon: 'i-lucide-receipt', label: 'No vouchers found' }"
      >
        <template #voucherNumber-data="{ row }">
          <span class="font-medium text-highlighted">{{ row.voucherNumber }}</span>
        </template>
        <template #onDate-data="{ row }">
          {{ new Date(row.onDate).toLocaleDateString() }}
        </template>
        <template #voucherType-data="{ row }">
          <UBadge color="gray" size="sm">{{ row.voucherType }}</UBadge>
        </template>
        <template #amount-data="{ row }">
          <span class="font-medium">{{ money(row.amount) }}</span>
        </template>
      </UTable>

      <div class="p-4 border-t border-gray-200 dark:border-gray-800 flex justify-between items-center">
        <div class="text-sm text-muted">Showing {{ vouchers.length }} vouchers</div>
        <UPagination v-model="page" :total="totalCount" :page-count="pageSize" />
      </div>
    </UCard>

    <USlideover v-model="isCreateOpen" title="New Voucher">
      <div class="p-4 flex-1 overflow-y-auto">
        <div class="space-y-4">
          <UFormGroup label="Voucher Type">
            <USelect v-model="form.voucherType" :options="['Payment', 'Receipt', 'Journal', 'Contra']" />
          </UFormGroup>
          <UFormGroup label="Date">
            <UInput v-model="form.onDate" type="date" />
          </UFormGroup>
          <UFormGroup label="Amount">
            <UInput v-model.number="form.amount" type="number" />
          </UFormGroup>
          <UFormGroup label="Particulars">
            <UTextarea v-model="form.particulars" />
          </UFormGroup>
        </div>
      </div>
      <div class="p-4 border-t border-gray-200 dark:border-gray-800 flex justify-end gap-3">
        <UButton color="gray" @click="isCreateOpen = false">Cancel</UButton>
        <UButton color="primary" :loading="saving" @click="saveVoucher">Save</UButton>
      </div>
    </USlideover>
  </section>
</template>

<script setup lang="ts">
import { ref, onMounted, watch } from 'vue'
import { useToast, useRuntimeConfig } from '#imports'

const toast = useToast()
const config = useRuntimeConfig()

const search = ref('')
const vouchers = ref<any[]>([])
const loading = ref(false)
const page = ref(1)
const pageSize = ref(20)
const totalCount = ref(0)

const isCreateOpen = ref(false)
const saving = ref(false)
const form = ref({
  voucherType: 'Payment',
  onDate: new Date().toISOString().split('T')[0],
  amount: 0,
  particulars: '',
  partyName: 'N/A',
  paymentMode: 0,
  isParty: false,
  ledgerId: '00000000-0000-0000-0000-000000000000',
  employeeId: '00000000-0000-0000-0000-000000000000',
  companyId: '00000000-0000-0000-0000-000000000000',
  storeGroupId: '00000000-0000-0000-0000-000000000000',
  storeId: '00000000-0000-0000-0000-000000000000'
})

const columns = [
  { key: 'voucherNumber', label: 'Voucher #' },
  { key: 'onDate', label: 'Date' },
  { key: 'voucherType', label: 'Type' },
  { key: 'particulars', label: 'Particulars' },
  { key: 'amount', label: 'Amount' }
]

function getHeaders() {
  const token = localStorage.getItem('garmetix.token')
  return { 'Authorization': `Bearer ${token}` }
}

function money(amount: any) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR' }).format(Number(amount || 0))
}

async function fetchVouchers() {
  loading.value = true
  try {
    const query = new URLSearchParams()
    if (search.value.trim()) query.set('query', search.value.trim())
    query.set('page', String(page.value))
    query.set('pageSize', String(pageSize.value))

    const res = await $fetch<any>(`${config.public.apiBaseUrl}/accounting/vouchers?${query.toString()}`, {
      headers: getHeaders()
    })
    
    vouchers.value = res.items || res || []
    totalCount.value = res.totalCount || vouchers.value.length
  } catch (err: any) {
    toast.add({ title: 'Error', description: err.message || 'Failed to fetch vouchers', color: 'red' })
  } finally {
    loading.value = false
  }
}

async function saveVoucher() {
  saving.value = true
  try {
    const payload = { ...form.value }
    // Hack to match enum map
    if (payload.voucherType === 'Payment') payload.voucherType = 0 as any
    if (payload.voucherType === 'Receipt') payload.voucherType = 1 as any
    if (payload.voucherType === 'Journal') payload.voucherType = 2 as any
    if (payload.voucherType === 'Contra') payload.voucherType = 3 as any
    
    await $fetch<any>(`${config.public.apiBaseUrl}/accounting/vouchers`, {
      method: 'POST',
      body: payload,
      headers: getHeaders()
    })
    
    toast.add({ title: 'Success', description: 'Voucher saved successfully.', color: 'green' })
    isCreateOpen.value = false
    fetchVouchers()
  } catch (err: any) {
    toast.add({ title: 'Error', description: err.message || 'Failed to save voucher', color: 'red' })
  } finally {
    saving.value = false
  }
}

watch(page, () => fetchVouchers())

onMounted(() => {
  fetchVouchers()
})
</script>
