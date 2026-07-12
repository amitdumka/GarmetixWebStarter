<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon :name="iconName" class="size-4" /> {{ kicker }}</p>
          <h2 class="garmetix-dashboard-title">{{ title }}</h2>
          <p class="garmetix-dashboard-subtitle">{{ description }}</p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton icon="i-lucide-plus" color="primary" variant="solid" @click="openNew">{{ newLabel }}</UButton>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />
    <UAlert v-if="message" color="success" variant="subtle" icon="i-lucide-circle-check" :description="message" />

    <section class="grid gap-3 md:grid-cols-3">
      <div class="garmetix-metric-card">
        <p class="garmetix-metric-label">{{ title }}</p>
        <p class="garmetix-metric-value">{{ notes.length }}</p>
        <p class="garmetix-metric-caption">{{ formatIndianMoney(totalAmount) }} total</p>
      </div>
      <div class="garmetix-metric-card">
        <p class="garmetix-metric-label">Adjusted</p>
        <p class="garmetix-metric-value">{{ adjustedCount }}</p>
        <p class="garmetix-metric-caption">Notes settled against a ledger</p>
      </div>
      <div class="garmetix-metric-card">
        <p class="garmetix-metric-label">Open</p>
        <p class="garmetix-metric-value">{{ openCount }}</p>
        <p class="garmetix-metric-caption">Pending adjustment or review</p>
      </div>
    </section>

    <section class="garmetix-section-card">
      <div class="mb-3 flex flex-col gap-2 lg:flex-row lg:items-center lg:justify-between">
        <div>
          <h3 class="garmetix-panel-title">{{ title }} Register</h3>
          <p class="garmetix-panel-subtitle">{{ filteredRows.length }} of {{ notes.length }} note(s)</p>
        </div>
        <UInput v-model="search" icon="i-lucide-search" :placeholder="`Search ${title.toLowerCase()}`" class="lg:w-80" />
      </div>

      <BooksMasterTable :columns="columns" :rows="filteredRows" :empty-text="`No ${title.toLowerCase()} yet.`">
        <template #actions="{ row }">
          <div class="flex flex-wrap gap-1">
            <UButton icon="i-lucide-eye" size="xs" color="neutral" variant="soft" @click="openDetail(row)">View</UButton>
            <UButton icon="i-lucide-pencil" size="xs" color="neutral" variant="ghost" @click="openEdit(row)">Edit</UButton>
            <UButton icon="i-lucide-file-down" size="xs" color="primary" variant="ghost" :loading="downloading === `${row.id}-a4`" @click="downloadNote(row, false)">A4</UButton>
            <UButton icon="i-lucide-receipt-text" size="xs" color="neutral" variant="ghost" :loading="downloading === `${row.id}-a5`" @click="downloadNote(row, true)">A5</UButton>
          </div>
        </template>
      </BooksMasterTable>
    </section>

    <USlideover v-model:open="formOpen" :title="formTitle" :description="formDescription" :ui="{ content: 'w-full sm:max-w-3xl lg:max-w-4xl' }">
      <template #body>
        <CommercialNoteEntryForm
          v-if="formOpen"
          :key="formKey"
          :note-type="props.noteType"
          :note-id="editingId"
          embedded
          @cancel="formOpen = false"
          @saved="afterSave"
        />
      </template>
    </USlideover>

    <USlideover v-model:open="detailOpen" :title="detailTitle" :description="readText(detail, ['noteType'], title)" :ui="{ content: 'w-full sm:max-w-3xl lg:max-w-4xl' }">
      <template #body>
        <div v-if="detail" class="space-y-4">
          <section class="grid gap-3 md:grid-cols-3">
            <div v-for="card in detailCards" :key="card.label" class="garmetix-metric-card">
              <p class="garmetix-metric-label">{{ card.label }}</p>
              <p class="garmetix-metric-value text-base">{{ card.value }}</p>
              <p class="garmetix-metric-caption">{{ card.detail }}</p>
            </div>
          </section>

          <section class="garmetix-section-card">
            <dl class="grid gap-3 md:grid-cols-2">
              <div v-for="item in detailRows" :key="item.label" class="border-b border-default pb-2 text-sm">
                <dt class="text-xs text-muted">{{ item.label }}</dt>
                <dd class="mt-1 break-words font-medium">{{ item.value }}</dd>
              </div>
            </dl>
          </section>

          <div class="flex flex-wrap justify-end gap-2">
            <UButton icon="i-lucide-pencil" color="neutral" variant="soft" @click="openEditFromDetail">Edit</UButton>
            <UButton icon="i-lucide-file-down" color="primary" variant="soft" :loading="downloading === `${readText(detail, ['id'], '')}-a4`" @click="downloadNote(detail, false)">A4 PDF</UButton>
            <UButton icon="i-lucide-receipt-text" color="neutral" variant="ghost" :loading="downloading === `${readText(detail, ['id'], '')}-a5`" @click="downloadNote(detail, true)">A5 Slip</UButton>
          </div>
        </div>
      </template>
    </USlideover>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney } from '@garmetix/shared-utils'
import { formatDate, readNumber, readText, toRows, type ApiRecord, useBooksApiClient } from '../utils/books-api'

const props = defineProps<{
  noteType: number
}>()

const { get, download } = useBooksApiClient()
const loading = ref(true)
const detailLoading = ref(false)
const error = ref('')
const message = ref('')
const search = ref('')
const downloading = ref('')
const formOpen = ref(false)
const detailOpen = ref(false)
const editingId = ref('')
const notes = ref<ApiRecord[]>([])
const detail = ref<ApiRecord | null>(null)

const isCredit = computed(() => props.noteType === 1)
const title = computed(() => isCredit.value ? 'Credit Notes' : 'Debit Notes')
const singularTitle = computed(() => isCredit.value ? 'Credit Note' : 'Debit Note')
const iconName = computed(() => isCredit.value ? 'i-lucide-file-plus-2' : 'i-lucide-file-minus-2')
const kicker = computed(() => isCredit.value ? 'Customer / party credits' : 'Vendor / supplier debits')
const description = computed(() => isCredit.value
  ? 'Review customer and party credits with source documents and settlement status.'
  : 'Review vendor, supplier and party debits with source documents and settlement status.')
const newLabel = computed(() => `New ${singularTitle.value}`)
const formTitle = computed(() => editingId.value ? `Edit ${singularTitle.value}` : `New ${singularTitle.value}`)
const formDescription = computed(() => 'Entry stays in a slide-over so the register remains in context.')
const formKey = computed(() => `${props.noteType}-${editingId.value || 'new'}-${formOpen.value}`)
const totalAmount = computed(() => notes.value.reduce((sum, item) => sum + readNumber(item, ['amount']), 0))
const adjustedCount = computed(() => notes.value.filter(item => Boolean(item.isAdjusted)).length)
const openCount = computed(() => notes.value.length - adjustedCount.value)
const tableRows = computed(() => notes.value.map(item => ({
  raw: item,
  id: readText(item, ['id'], ''),
  number: readText(item, ['noteNumber']),
  date: formatDate(item.onDate),
  party: readText(item, ['partyName']),
  source: readText(item, ['sourceType']),
  sourceNumber: readText(item, ['sourceNumber'], ''),
  amount: formatIndianMoney(readNumber(item, ['amount'])),
  status: item.isAdjusted ? 'Adjusted' : 'Open',
  reason: readText(item, ['reason'], '')
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
  { key: 'sourceNumber', label: 'Source No.' },
  { key: 'amount', label: 'Amount' },
  { key: 'status', label: 'Status' }
]
const detailTitle = computed(() => readText(detail.value, ['noteNumber'], singularTitle.value))
const detailCards = computed(() => [
  { label: 'Amount', value: formatIndianMoney(readNumber(detail.value, ['amount'])), detail: 'Total note value' },
  { label: 'Taxable', value: formatIndianMoney(readNumber(detail.value, ['taxableAmount'])), detail: 'Taxable amount' },
  { label: 'Tax', value: formatIndianMoney(readNumber(detail.value, ['taxAmount'])), detail: 'GST/tax amount' }
])
const detailRows = computed(() => {
  const item = detail.value
  if (!item) return []
  return [
    { label: 'Date', value: formatDate(item.onDate) },
    { label: 'Party Type', value: readText(item, ['partyType']) },
    { label: 'Party Name', value: readText(item, ['partyName']) },
    { label: 'GSTIN', value: readText(item, ['partyGstin']) },
    { label: 'Source Type', value: readText(item, ['sourceType']) },
    { label: 'Source Number', value: readText(item, ['sourceNumber']) },
    { label: 'Reason', value: readText(item, ['reason']) },
    { label: 'Remarks', value: readText(item, ['remarks']) },
    { label: 'Adjusted', value: readText(item, ['isAdjusted']) },
    { label: 'Adjusted Amount', value: formatIndianMoney(readNumber(item, ['adjustedAmount'])) }
  ]
})

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    notes.value = toRows(await get<unknown>('commercial-notes', { take: 150, noteType: props.noteType }))
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : `Unable to load ${title.value.toLowerCase()}.`
  } finally {
    loading.value = false
  }
}

function openNew() {
  editingId.value = ''
  message.value = ''
  error.value = ''
  formOpen.value = true
}

function openEdit(row: ApiRecord) {
  editingId.value = readText(row, ['id'], '')
  message.value = ''
  error.value = ''
  formOpen.value = true
}

async function openDetail(row: ApiRecord) {
  const id = readText(row, ['id'], '')
  if (!id) return
  detailOpen.value = true
  detailLoading.value = true
  error.value = ''
  try {
    detail.value = await get<ApiRecord>(`commercial-notes/${id}`)
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : `Unable to load ${singularTitle.value.toLowerCase()} detail.`
  } finally {
    detailLoading.value = false
  }
}

function openEditFromDetail() {
  const id = readText(detail.value, ['id'], '')
  if (!id) return
  detailOpen.value = false
  openEdit({ id })
}

async function afterSave() {
  formOpen.value = false
  message.value = `${singularTitle.value} saved.`
  await refresh()
}

async function downloadNote(row: ApiRecord, a5Slip: boolean) {
  const id = readText(row, ['id'], '')
  if (!id) return
  downloading.value = `${id}-${a5Slip ? 'a5' : 'a4'}`
  error.value = ''
  try {
    await download(`commercial-notes/${id}/pdf`, { a5Slip, signatures: true }, `${readText(row, ['number', 'noteNumber'], singularTitle.value)}.pdf`)
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : `Unable to download ${singularTitle.value.toLowerCase()}.`
  } finally {
    downloading.value = ''
  }
}

onMounted(refresh)
</script>
