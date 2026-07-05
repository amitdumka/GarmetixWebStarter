<template>
  <section class="garmetix-page-stack" :aria-busy="loading">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-scale" class="size-4" /> Reports</p>
          <h2 class="garmetix-dashboard-title">Trial Balance</h2>
          <p class="garmetix-dashboard-subtitle">
            Company-wide summary of all ledger balances as of {{ new Date().toLocaleDateString() }}.
          </p>
        </div>
        <div class="flex gap-2">
          <UButton color="neutral" variant="ghost" icon="i-lucide-refresh-cw" @click="fetchTrialBalance">Refresh</UButton>
          <UButton icon="i-lucide-file-down" color="gray">Export CSV</UButton>
        </div>
      </div>
    </div>

    <UCard :ui="{ body: { padding: '' } }">
      <UTable
        :rows="items"
        :columns="columns"
        :loading="loading"
        :empty-state="{ icon: 'i-lucide-scale', label: 'No trial balance data available' }"
      >
        <template #ledgerName-data="{ row }">
          <div class="font-medium text-highlighted">{{ row.ledgerName }}</div>
          <div class="text-xs text-muted">{{ row.groupName }}</div>
        </template>
        <template #debit-data="{ row }">
          <span v-if="row.closingBalance > 0" class="font-medium text-red-600 dark:text-red-400">
            {{ money(row.closingBalance) }}
          </span>
          <span v-else class="text-muted">-</span>
        </template>
        <template #credit-data="{ row }">
          <span v-if="row.closingBalance < 0" class="font-medium text-green-600 dark:text-green-400">
            {{ money(Math.abs(row.closingBalance)) }}
          </span>
          <span v-else class="text-muted">-</span>
        </template>
      </UTable>

      <div class="p-4 border-t border-gray-200 dark:border-gray-800 flex justify-end gap-12 font-semibold">
        <div>Total Debit: <span class="text-red-600 dark:text-red-400">{{ money(totalDebit) }}</span></div>
        <div>Total Credit: <span class="text-green-600 dark:text-green-400">{{ money(totalCredit) }}</span></div>
      </div>
    </UCard>
  </section>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useToast, useRuntimeConfig } from '#imports'

const toast = useToast()
const config = useRuntimeConfig()

const loading = ref(false)
const items = ref<any[]>([])

const columns = [
  { key: 'ledgerName', label: 'Account Name' },
  { key: 'debit', label: 'Debit (Dr)' },
  { key: 'credit', label: 'Credit (Cr)' }
]

const totalDebit = computed(() => {
  return items.value.filter(i => i.closingBalance > 0).reduce((sum, i) => sum + i.closingBalance, 0)
})

const totalCredit = computed(() => {
  return items.value.filter(i => i.closingBalance < 0).reduce((sum, i) => sum + Math.abs(i.closingBalance), 0)
})

function getHeaders() {
  const token = localStorage.getItem('garmetix.token')
  return { 'Authorization': `Bearer ${token}` }
}

function money(amount: any) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR' }).format(Number(amount || 0))
}

async function fetchTrialBalance() {
  loading.value = true
  try {
    const res = await $fetch<any>(`${config.public.apiBaseUrl}/accounting/trial-balance`, {
      headers: getHeaders()
    })
    
    items.value = res || []
  } catch (err: any) {
    toast.add({ title: 'Error', description: err.message || 'Failed to fetch trial balance', color: 'red' })
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  fetchTrialBalance()
})
</script>
