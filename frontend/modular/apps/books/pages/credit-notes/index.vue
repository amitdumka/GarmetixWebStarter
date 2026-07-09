<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-file-plus-2" class="size-4" /> Customer / party credits</p>
          <h2 class="garmetix-dashboard-title">Credit Notes</h2>
          <p class="garmetix-dashboard-subtitle">
            Review customer and party credits with their source documents and settlement status.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton icon="i-lucide-plus" color="primary" variant="solid" to="/credit-notes/new">New Credit Note</UButton>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />

    <section class="grid gap-3 md:grid-cols-2">
      <div class="garmetix-metric-card">
        <p class="garmetix-metric-label">Credit Notes</p>
        <p class="garmetix-metric-value">{{ notes.length }}</p>
        <p class="garmetix-metric-caption">{{ formatIndianMoney(totalAmount) }} total</p>
      </div>
      <div class="garmetix-metric-card">
        <p class="garmetix-metric-label">Adjusted</p>
        <p class="garmetix-metric-value">{{ adjustedCount }}</p>
        <p class="garmetix-metric-caption">Notes settled against a ledger</p>
      </div>
    </section>

    <section class="garmetix-section-card">
      <div class="mb-3 flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h3 class="garmetix-panel-title">Credit Note Register</h3>
          <p class="garmetix-panel-subtitle">{{ filteredRows.length }} of {{ notes.length }} credit notes</p>
        </div>
        <UInput v-model="search" icon="i-lucide-search" placeholder="Search credit notes" class="sm:w-72" />
      </div>

      <BooksMasterTable :columns="columns" :rows="filteredRows" empty-text="No credit notes yet.">
        <template #actions="{ row }">
          <div class="flex flex-wrap gap-1">
            <UButton icon="i-lucide-pencil" size="xs" color="neutral" variant="ghost" :to="`/credit-notes/${row.id}`">Edit</UButton>
            <UButton icon="i-lucide-file-down" size="xs" color="primary" variant="ghost" :loading="downloading === `${row.id}-a4`" @click="downloadNote(row, false)">A4</UButton>
            <UButton icon="i-lucide-receipt-text" size="xs" color="neutral" variant="ghost" :loading="downloading === `${row.id}-a5`" @click="downloadNote(row, true)">A5</UButton>
          </div>
        </template>
      </BooksMasterTable>
    </section>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney } from '@garmetix/shared-utils'
import { formatDate, readNumber, readText, toRows, type ApiRecord, useBooksApiClient } from '../../utils/books-api'

useHead({ title: 'Credit Notes - Garmetix Books' })

const { get, download } = useBooksApiClient()
const loading = ref(true)
const error = ref('')
const search = ref('')
const downloading = ref('')
const notes = ref<ApiRecord[]>([])

const totalAmount = computed(() => notes.value.reduce((sum, item) => sum + readNumber(item, ['amount']), 0))
const adjustedCount = computed(() => notes.value.filter(item => Boolean(item.isAdjusted)).length)

const tableRows = computed(() => notes.value.map(item => ({
  id: readText(item, ['id'], ''),
  number: readText(item, ['noteNumber']),
  date: formatDate(item.onDate),
  party: readText(item, ['partyName']),
  source: readText(item, ['sourceType']),
  amount: formatIndianMoney(readNumber(item, ['amount'])),
  status: item.isAdjusted ? 'Adjusted' : 'Open'
})))
const filteredRows = computed(() => {
  const term = search.value.trim().toLowerCase()
  if (!term) return tableRows.value
  return tableRows.value.filter(row => JSON.stringify(row).toLowerCase().includes(term))
})
const columns = [
  { key: 'number', label: 'No' },
  { key: 'date', label: 'Date' },
  { key: 'party', label: 'Party' },
  { key: 'source', label: 'Source' },
  { key: 'amount', label: 'Amount' },
  { key: 'status', label: 'Status' }
]

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    notes.value = toRows(await get<unknown>('commercial-notes', { take: 150, noteType: 1 }))
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load credit notes.'
  } finally {
    loading.value = false
  }
}

async function downloadNote(row: ApiRecord, a5Slip: boolean) {
  const id = readText(row, ['id'], '')
  if (!id) return
  downloading.value = `${id}-${a5Slip ? 'a5' : 'a4'}`
  error.value = ''
  try {
    await download(`commercial-notes/${id}/pdf`, { a5Slip, signatures: true }, `${readText(row, ['number'], 'credit-note')}.pdf`)
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to download credit note.'
  } finally {
    downloading.value = ''
  }
}

onMounted(refresh)
</script>
