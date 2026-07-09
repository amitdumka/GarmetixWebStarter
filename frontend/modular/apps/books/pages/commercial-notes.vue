<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-files" class="size-4" /> Commercial notes</p>
          <h2 class="garmetix-dashboard-title">Commercial Notes Summary</h2>
          <p class="garmetix-dashboard-subtitle">
            Review debit notes, credit notes, and available customer advances from one accounting register.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton icon="i-lucide-file-plus-2" color="primary" variant="solid" to="/credit-notes/new">New Credit Note</UButton>
          <UButton icon="i-lucide-file-minus-2" color="warning" variant="subtle" to="/debit-notes/new">New Debit Note</UButton>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />

    <section class="grid gap-3 md:grid-cols-3">
      <div class="garmetix-metric-card">
        <p class="garmetix-metric-label">Debit Notes</p>
        <p class="garmetix-metric-value">{{ debitNotes.length }}</p>
        <p class="garmetix-metric-caption">{{ formatIndianMoney(debitTotal) }}</p>
      </div>
      <div class="garmetix-metric-card">
        <p class="garmetix-metric-label">Credit Notes</p>
        <p class="garmetix-metric-value">{{ creditNotes.length }}</p>
        <p class="garmetix-metric-caption">{{ formatIndianMoney(creditTotal) }}</p>
      </div>
      <div class="garmetix-metric-card">
        <p class="garmetix-metric-label">Advance Receipts</p>
        <p class="garmetix-metric-value">{{ advances.length }}</p>
        <p class="garmetix-metric-caption">{{ formatIndianMoney(advanceTotal) }} available</p>
      </div>
    </section>

    <section class="garmetix-section-card">
      <div class="mb-3 flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h3 class="garmetix-panel-title">Commercial Note Register</h3>
          <p class="garmetix-panel-subtitle">{{ filteredRows.length }} of {{ notes.length }} notes</p>
        </div>
        <div class="flex flex-col gap-2 sm:flex-row">
          <USelect v-model="noteTypeFilter" :items="noteTypeFilterItems" class="sm:w-40" />
          <UInput v-model="search" icon="i-lucide-search" placeholder="Search notes" class="sm:w-72" />
        </div>
      </div>

      <BooksMasterTable :columns="columns" :rows="filteredRows" empty-text="No commercial notes yet.">
        <template #actions="{ row }">
          <div class="flex flex-wrap gap-1">
            <UButton icon="i-lucide-arrow-up-right" size="xs" color="neutral" variant="ghost" :to="row.noteType === 'DebitNote' ? `/debit-notes/${row.id}` : `/credit-notes/${row.id}`">Open</UButton>
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
import { formatDate, readNumber, readText, toRows, type ApiRecord, useBooksApiClient } from '../utils/books-api'

useHead({ title: 'Commercial Notes - Garmetix Books' })

const { get, download } = useBooksApiClient()
const loading = ref(true)
const error = ref('')
const search = ref('')
const noteTypeFilter = ref('all')
const downloading = ref('')
const notes = ref<ApiRecord[]>([])
const advances = ref<ApiRecord[]>([])

const debitNotes = computed(() => notes.value.filter(item => item.noteType === 'DebitNote'))
const creditNotes = computed(() => notes.value.filter(item => item.noteType === 'CreditNote'))
const debitTotal = computed(() => debitNotes.value.reduce((sum, item) => sum + readNumber(item, ['amount']), 0))
const creditTotal = computed(() => creditNotes.value.reduce((sum, item) => sum + readNumber(item, ['amount']), 0))
const advanceTotal = computed(() => advances.value.reduce((sum, item) => sum + readNumber(item, ['availableAmount']), 0))

const noteTypeFilterItems = [
  { label: 'All notes', value: 'all' },
  { label: 'Debit notes', value: 'DebitNote' },
  { label: 'Credit notes', value: 'CreditNote' }
]

const tableRows = computed(() => notes.value
  .filter(item => noteTypeFilter.value === 'all' || item.noteType === noteTypeFilter.value)
  .map(item => ({
    id: readText(item, ['id'], ''),
    noteType: readText(item, ['noteType']),
    number: readText(item, ['noteNumber']),
    date: formatDate(item.onDate),
    type: item.noteType === 'DebitNote' ? 'Debit note' : 'Credit note',
    party: readText(item, ['partyName']),
    source: readText(item, ['sourceType']),
    amount: formatIndianMoney(readNumber(item, ['amount']))
  })))
const filteredRows = computed(() => {
  const term = search.value.trim().toLowerCase()
  if (!term) return tableRows.value
  return tableRows.value.filter(row => JSON.stringify(row).toLowerCase().includes(term))
})
const columns = [
  { key: 'number', label: 'No' },
  { key: 'date', label: 'Date' },
  { key: 'type', label: 'Type' },
  { key: 'party', label: 'Party' },
  { key: 'source', label: 'Source' },
  { key: 'amount', label: 'Amount' }
]

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const [noteData, advanceData] = await Promise.allSettled([
      get<unknown>('commercial-notes', { take: 150 }),
      get<unknown>('customer-advances', { take: 150 })
    ])
    if (noteData.status === 'fulfilled') notes.value = toRows(noteData.value)
    if (advanceData.status === 'fulfilled') advances.value = toRows(advanceData.value)

    const failedReasons = [noteData, advanceData]
      .filter((item): item is PromiseRejectedResult => item.status === 'rejected')
      .map(item => item.reason instanceof Error ? item.reason.message : String(item.reason))
    if (failedReasons.length) error.value = failedReasons[0]
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load commercial notes.'
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
    await download(`commercial-notes/${id}/pdf`, { a5Slip, signatures: true }, `${readText(row, ['number'], 'note')}.pdf`)
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to download note.'
  } finally {
    downloading.value = ''
  }
}

onMounted(refresh)
</script>
