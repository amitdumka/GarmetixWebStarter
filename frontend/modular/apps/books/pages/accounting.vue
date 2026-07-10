<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-book-open-check" class="size-4" /> Books master data</p>
          <h2 class="garmetix-dashboard-title">Accounting</h2>
          <p class="garmetix-dashboard-subtitle">
            Read-only trial balance from posted journals. Ledgers moved to <NuxtLink to="/ledgers" class="underline">Ledgers</NuxtLink>, Parties moved to <NuxtLink to="/parties" class="underline">Parties</NuxtLink>, and banking (bank accounts, transactions, reconciliation, cheques, vendor banks, account details) moved to <NuxtLink to="/banking" class="underline">Banking</NuxtLink>.
          </p>
        </div>
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
      </div>
    </div>

    <UAlert v-if="error" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />

    <section class="garmetix-section-card">
      <div class="mb-3 flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h3 class="garmetix-panel-title">Trial Balance</h3>
          <p class="garmetix-panel-subtitle">Read-only trial balance from posted journals.</p>
        </div>
        <UInput v-model="search" icon="i-lucide-search" placeholder="Search trial balance" class="sm:w-72" />
      </div>

      <BooksMasterTable :columns="columns" :rows="pagedRows" empty-text="No trial balance rows found." />

      <div v-if="filteredRows.length" class="mt-3 flex flex-wrap items-center justify-between gap-2 text-sm text-muted">
        <p>Showing {{ pagedRows.length }} of {{ filteredRows.length }} row(s)</p>
        <div class="flex items-center gap-2">
          <USelect v-model="pageSize" :items="pageSizeOptions" class="w-28" />
          <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-left" :disabled="page <= 1" @click="page--">Prev</UButton>
          <span>{{ page }} / {{ totalPages }}</span>
          <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-right" :disabled="page >= totalPages" @click="page++">Next</UButton>
        </div>
      </div>
    </section>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney } from '@garmetix/shared-utils'
import { pageSizeOptions, paginateRows, readNumber, readText, toRows, type ApiRecord, useBooksApiClient } from '../utils/books-api'

useHead({ title: 'Accounting - Garmetix Books' })

const { get } = useBooksApiClient()
const loading = ref(true)
const error = ref('')
const search = ref('')
const trialBalance = ref<ApiRecord[]>([])

const columns = [
  { key: 'ledger', label: 'Ledger' },
  { key: 'group', label: 'Group' },
  { key: 'debit', label: 'Debit' },
  { key: 'credit', label: 'Credit' },
  { key: 'closingDebit', label: 'Closing Debit' },
  { key: 'closingCredit', label: 'Closing Credit' }
]

const rows = computed(() => trialBalance.value.map(item => ({
  ledger: readText(item, ['ledgerName']),
  group: readText(item, ['ledgerGroup']),
  debit: formatIndianMoney(readNumber(item, ['debit'])),
  credit: formatIndianMoney(readNumber(item, ['credit'])),
  closingDebit: formatIndianMoney(readNumber(item, ['closingDebit'])),
  closingCredit: formatIndianMoney(readNumber(item, ['closingCredit']))
})))
const filteredRows = computed(() => {
  const term = search.value.trim().toLowerCase()
  if (!term) return rows.value
  return rows.value.filter(row => JSON.stringify(row).toLowerCase().includes(term))
})

const page = ref(1)
const pageSize = ref<number>(pageSizeOptions[0].value)
const totalPages = computed(() => Math.max(1, Math.ceil(filteredRows.value.length / Number(pageSize.value || 25))))
const pagedRows = computed(() => paginateRows(filteredRows.value, page.value, pageSize.value))
watch([search, pageSize], () => { page.value = 1 })
watch(totalPages, (value) => { if (page.value > value) page.value = value })

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    trialBalance.value = toRows(await get<unknown>('accounting/trial-balance'))
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load trial balance.'
  } finally {
    loading.value = false
  }
}

onMounted(refresh)
</script>
