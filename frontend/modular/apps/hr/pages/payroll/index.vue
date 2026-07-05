<template>
  <section class="garmetix-page-stack" :aria-busy="loading">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-file-spreadsheet" class="size-4" /> Operations</p>
          <h2 class="garmetix-dashboard-title">Payroll Adjustments</h2>
          <p class="garmetix-dashboard-subtitle">
            Manage salary advances, deductions, PF, and other payroll elements.
          </p>
        </div>
        <div class="flex gap-2">
          <UButton color="neutral" variant="ghost" icon="i-lucide-refresh-cw" @click="fetchAdjustments">Refresh</UButton>
          <UButton icon="i-lucide-plus" @click="isCreateOpen = true">New Adjustment</UButton>
        </div>
      </div>
    </div>

    <UCard :ui="{ body: { padding: '' } }">
      <div class="p-4 border-b border-gray-200 dark:border-gray-800 flex gap-4">
        <USelect v-model="filterType" :options="['All', 'Advance', 'Deduction', 'Bonus', 'PF', 'Leave']" class="w-40" />
        <UInput v-model="search" icon="i-lucide-search" placeholder="Search employee..." class="w-full md:w-64" @keyup.enter="fetchAdjustments" />
        <UButton color="gray" @click="fetchAdjustments">Go</UButton>
      </div>

      <UTable
        :rows="adjustments"
        :columns="columns"
        :loading="loading"
        :empty-state="{ icon: 'i-lucide-file-spreadsheet', label: 'No payroll adjustments found.' }"
      >
        <template #employee-data="{ row }">
          <div class="font-medium text-highlighted">{{ row.employeeName }}</div>
          <div class="text-xs text-muted">{{ row.employeeCode }}</div>
        </template>
        <template #type-data="{ row }">
          <UBadge :color="getTypeColor(row.adjustmentType)" size="sm">{{ row.adjustmentType }}</UBadge>
        </template>
        <template #onDate-data="{ row }">
          {{ new Date(row.onDate).toLocaleDateString() }}
        </template>
        <template #amount-data="{ row }">
          <span class="font-medium">{{ money(row.amount) }}</span>
          <div v-if="row.recoverFromSalary" class="text-[10px] text-muted">Recoverable</div>
        </template>
      </UTable>

      <div class="p-4 border-t border-gray-200 dark:border-gray-800 flex justify-between items-center">
        <div class="text-sm text-muted">Showing {{ adjustments.length }} adjustments</div>
        <UPagination v-model="page" :total="totalCount" :page-count="pageSize" />
      </div>
    </UCard>

    <USlideover v-model="isCreateOpen" title="New Payroll Adjustment">
      <div class="p-4 flex-1 overflow-y-auto">
        <div class="space-y-4">
          <UFormGroup label="Adjustment Type">
            <USelect v-model="form.adjustmentType" :options="['Advance', 'Deduction', 'Bonus', 'PF', 'Leave']" />
          </UFormGroup>
          <UFormGroup label="Date">
            <UInput v-model="form.onDate" type="date" />
          </UFormGroup>
          <UFormGroup label="Amount">
            <UInput v-model.number="form.amount" type="number" />
          </UFormGroup>
          <UFormGroup label="Recover from Salary (for Advances)">
            <UToggle v-model="form.recoverFromSalary" />
          </UFormGroup>
          <UFormGroup label="Remarks">
            <UTextarea v-model="form.remarks" />
          </UFormGroup>
        </div>
      </div>
      <div class="p-4 border-t border-gray-200 dark:border-gray-800 flex justify-end gap-3">
        <UButton color="gray" @click="isCreateOpen = false">Cancel</UButton>
        <UButton color="primary" :loading="saving" @click="saveAdjustment">Save</UButton>
      </div>
    </USlideover>
  </section>
</template>

<script setup lang="ts">
import { ref, onMounted, watch } from 'vue'
import { useToast, useRuntimeConfig } from '#imports'

const toast = useToast()
const config = useRuntimeConfig()

const filterType = ref('All')
const search = ref('')
const adjustments = ref<any[]>([])
const loading = ref(false)
const page = ref(1)
const pageSize = ref(20)
const totalCount = ref(0)

const isCreateOpen = ref(false)
const saving = ref(false)
const form = ref({
  employeeId: '00000000-0000-0000-0000-000000000000',
  adjustmentType: 'Advance',
  onDate: new Date().toISOString().split('T')[0],
  salaryMonth: new Date().getMonth() + 1,
  amount: 0,
  leaveDays: 0,
  recoverFromSalary: true,
  recoveredAmount: 0,
  pfEmployee: 0,
  pfEmployer: 0,
  gratuityAmount: 0,
  status: 'Draft',
  remarks: '',
  companyId: '00000000-0000-0000-0000-000000000000',
  storeGroupId: '00000000-0000-0000-0000-000000000000',
  storeId: '00000000-0000-0000-0000-000000000000'
})

const columns = [
  { key: 'employee', label: 'Employee' },
  { key: 'type', label: 'Type' },
  { key: 'onDate', label: 'Date' },
  { key: 'amount', label: 'Amount' }
]

function getHeaders() {
  const token = localStorage.getItem('garmetix.token')
  return { 'Authorization': `Bearer ${token}` }
}

function money(amount: any) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR' }).format(Number(amount || 0))
}

function getTypeColor(type: string) {
  const map: Record<string, string> = { 'Advance': 'blue', 'Deduction': 'red', 'Bonus': 'green', 'PF': 'orange', 'Leave': 'gray' }
  return map[type] || 'gray'
}

async function fetchAdjustments() {
  loading.value = true
  try {
    const query = new URLSearchParams()
    if (search.value.trim()) query.set('query', search.value.trim())
    if (filterType.value !== 'All') query.set('type', filterType.value)
    query.set('page', String(page.value))
    query.set('pageSize', String(pageSize.value))

    const res = await $fetch<any>(`${config.public.apiBaseUrl}/hr/payroll/adjustments?${query.toString()}`, {
      headers: getHeaders()
    })
    
    adjustments.value = res.items || res || []
    totalCount.value = res.totalCount || adjustments.value.length
  } catch (err: any) {
    toast.add({ title: 'Error', description: err.message || 'Failed to fetch adjustments', color: 'red' })
  } finally {
    loading.value = false
  }
}

async function saveAdjustment() {
  saving.value = true
  try {
    const payload = { ...form.value }
    await $fetch<any>(`${config.public.apiBaseUrl}/hr/payroll/adjustments`, {
      method: 'POST',
      body: payload,
      headers: getHeaders()
    })
    
    toast.add({ title: 'Success', description: 'Adjustment saved successfully.', color: 'green' })
    isCreateOpen.value = false
    fetchAdjustments()
  } catch (err: any) {
    toast.add({ title: 'Error', description: err.message || 'Failed to save adjustment', color: 'red' })
  } finally {
    saving.value = false
  }
}

watch(page, () => fetchAdjustments())

onMounted(() => {
  fetchAdjustments()
})
</script>
