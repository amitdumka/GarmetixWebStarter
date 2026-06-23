<template>
  <section class="space-y-4">
    <div class="border border-default bg-muted/10 p-5">
      <div class="flex flex-col gap-3 xl:flex-row xl:items-start xl:justify-between">
        <div>
          <p class="text-sm text-muted">GST filing review</p>
          <h2 class="mt-1 text-2xl font-semibold">GST Returns</h2>
          <p class="mt-2 max-w-3xl text-sm text-muted">
            Review saved GST return drafts, books-derived GSTR-1/GSTR-3B totals, export readiness and accounting settlement visibility. Draft save, filing and posting stay in controlled flows.
          </p>
        </div>
        <div class="flex flex-col gap-2 sm:flex-row">
          <UInput v-model="returnPeriod" placeholder="MMYYYY" class="sm:w-32" />
          <USelect v-model="formFilter" :items="formFilterItems" class="sm:w-40" />
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />

    <section class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
      <div v-for="card in cards" :key="card.label" class="border border-default bg-muted/20 p-4">
        <p class="text-sm text-muted">{{ card.label }}</p>
        <p class="mt-2 text-2xl font-semibold">{{ card.value }}</p>
        <p class="mt-1 text-xs text-muted">{{ card.detail }}</p>
      </div>
    </section>

    <section class="grid gap-4 xl:grid-cols-[minmax(0,1.5fr)_minmax(360px,0.9fr)]">
      <div class="border border-default bg-muted/10 p-4">
        <div class="mb-3 flex flex-col gap-2 lg:flex-row lg:items-center lg:justify-between">
          <div>
            <h3 class="text-base font-semibold">Saved Drafts</h3>
            <p class="text-xs text-muted">{{ filteredDrafts.length }} row(s) shown</p>
          </div>
          <UInput v-model="search" icon="i-lucide-search" placeholder="Search GST drafts" class="lg:w-72" />
        </div>
        <BooksMasterTable :columns="draftColumns" :rows="filteredDraftRows" empty-text="No GST return drafts found." />
      </div>

      <aside class="border border-default bg-muted/10 p-4">
        <div class="flex items-start justify-between gap-3">
          <div>
            <h3 class="text-base font-semibold">Draft Detail</h3>
            <p class="text-xs text-muted">{{ selectedDraftTitle }}</p>
          </div>
          <UBadge :color="selectedDraft ? 'success' : 'neutral'" variant="subtle">{{ selectedDraft ? readText(selectedDraft, ['status']) : 'None' }}</UBadge>
        </div>

        <USelect v-model="selectedDraftId" :items="draftOptions" class="mt-4 w-full" @update:model-value="selectDraftById" />

        <div v-if="selectedDraft" class="mt-4 space-y-4">
          <BooksMasterTable :columns="detailColumns" :rows="detailRows" empty-text="No draft detail rows found." />
          <BooksMasterTable :columns="issueColumns" :rows="previewIssueRows" empty-text="No preview issues found." />
          <div class="flex flex-wrap gap-2">
            <UButton icon="i-lucide-braces" size="sm" color="primary" variant="soft" :loading="downloadLoading === 'json'" @click="downloadDraft('json')">JSON</UButton>
            <UButton icon="i-lucide-file-spreadsheet" size="sm" color="neutral" variant="soft" :loading="downloadLoading === 'excel'" @click="downloadDraft('excel')">Excel</UButton>
          </div>
        </div>

        <div v-else class="mt-8 text-center text-sm text-muted">
          Select a draft from the table to review export details.
        </div>
      </aside>
    </section>

    <section class="grid gap-4 xl:grid-cols-2">
      <div class="border border-default bg-muted/10 p-4">
        <div class="mb-3 flex items-start justify-between gap-3">
          <div>
            <h3 class="text-base font-semibold">From Books Preview</h3>
            <p class="text-xs text-muted">Calculated from sales and purchase books for {{ returnPeriod }}</p>
          </div>
          <UBadge :color="booksLoading ? 'warning' : 'primary'" variant="subtle">{{ booksLoading ? 'Loading' : 'Preview' }}</UBadge>
        </div>
        <BooksMasterTable :columns="booksColumns" :rows="booksRows" empty-text="No books-derived GST preview loaded." />
      </div>

      <div class="border border-default bg-muted/10 p-4">
        <div class="mb-3">
          <h3 class="text-base font-semibold">Accounting Summary</h3>
          <p class="text-xs text-muted">GST settlement journal visibility only, no posting action.</p>
        </div>
        <BooksMasterTable :columns="accountingColumns" :rows="accountingRows" empty-text="No GST accounting summary found." />
      </div>
    </section>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney } from '@garmetix/shared-utils'
import {
  formatDate,
  readArray,
  readNumber,
  readText,
  toRows,
  type ApiRecord,
  useBooksApiClient
} from '../utils/books-api'

useHead({ title: 'GST Returns - Garmetix Books' })

const { download, get } = useBooksApiClient()
const loading = ref(true)
const booksLoading = ref(false)
const error = ref('')
const search = ref('')
const formFilter = ref('all')
const returnPeriod = ref(currentReturnPeriod())
const downloadLoading = ref('')
const drafts = ref<ApiRecord[]>([])
const selectedDraftId = ref('')
const selectedDraft = ref<ApiRecord | null>(null)
const gstr1 = ref<ApiRecord | null>(null)
const gstr3b = ref<ApiRecord | null>(null)
const accounting = ref<ApiRecord | null>(null)

const formFilterItems = [
  { label: 'All Forms', value: 'all' },
  { label: 'GSTR-1', value: 'gstr1' },
  { label: 'GSTR-3B', value: 'gstr3b' }
]
const cards = computed(() => [
  { label: 'Drafts', value: drafts.value.length, detail: 'Saved return drafts' },
  { label: 'Draft Taxable', value: money(drafts.value.reduce((sum, item) => sum + readNumber(item, ['taxableValue']), 0)), detail: 'Visible draft taxable value' },
  { label: 'GSTR-1 Rows', value: gstr1RowCount.value, detail: 'Books-derived rows' },
  { label: 'Net GST Payable', value: money(readNumber(accounting.value, ['netPayable'])), detail: 'Accounting bridge summary' }
])
const filteredDrafts = computed(() => {
  const term = search.value.trim().toLowerCase()
  return drafts.value.filter(item => {
    const textMatches = !term || [
      readText(item, ['form']),
      readText(item, ['title']),
      readText(item, ['gstin']),
      readText(item, ['returnPeriod']),
      readText(item, ['status'])
    ].join(' ').toLowerCase().includes(term)
    return textMatches
  })
})
const filteredDraftRows = computed(() => filteredDrafts.value.map(item => ({
  form: displayForm(item.form),
  period: readText(item, ['returnPeriod']),
  gstin: readText(item, ['gstin']),
  title: readText(item, ['title']),
  status: readText(item, ['status']),
  rows: readText(item, ['rowCount']),
  taxable: money(item.taxableValue),
  updated: formatDate(item.updatedAt ?? item.createdAt),
  action: selectedDraftId.value === readText(item, ['id'], '') ? 'Selected' : 'Click row below'
})))
const draftOptions = computed(() => {
  const rows = drafts.value.map(item => ({
    label: `${displayForm(item.form)} ${readText(item, ['returnPeriod'])} - ${readText(item, ['title'])}`,
    value: readText(item, ['id'], '')
  }))
  return rows.length ? rows : [{ label: 'No drafts', value: '' }]
})
const selectedDraftTitle = computed(() => selectedDraft.value ? `${displayForm(selectedDraft.value.form)} - ${readText(selectedDraft.value, ['returnPeriod'])}` : 'Select a draft')
const previewIssueRows = computed(() => parseIssues(readText(selectedDraft.value, ['lastPreviewIssuesJson'], '[]')))
const detailRows = computed(() => {
  const draft = selectedDraft.value
  if (!draft) return []
  return [
    { label: 'Form', value: displayForm(draft.form) },
    { label: 'Title', value: readText(draft, ['title']) },
    { label: 'GSTIN', value: readText(draft, ['gstin']) },
    { label: 'Return Period', value: readText(draft, ['returnPeriod']) },
    { label: 'Status', value: readText(draft, ['status']) },
    { label: 'Rows', value: readText(draft, ['rowCount']) },
    { label: 'Taxable Value', value: money(draft.taxableValue) },
    { label: 'IGST', value: money(draft.integratedTax) },
    { label: 'CGST', value: money(draft.centralTax) },
    { label: 'SGST', value: money(draft.stateTax) },
    { label: 'Created', value: formatDate(draft.createdAt) },
    { label: 'Updated By', value: readText(draft, ['updatedByUserName']) }
  ]
})
const gstr1RowCount = computed(() => readArray(gstr1.value, ['b2BInvoices']).length + readArray(gstr1.value, ['b2CSummaries']).length + readArray(gstr1.value, ['hsnSummaries']).length)
const booksRows = computed(() => [
  { form: 'GSTR-1', section: 'B2B Invoices', rows: readArray(gstr1.value, ['b2BInvoices']).length, taxable: money(sumRows(readArray(gstr1.value, ['b2BInvoices']), ['taxableValue'])), tax: money(sumRows(readArray(gstr1.value, ['b2BInvoices']), ['integratedTax', 'centralTax', 'stateTax'])) },
  { form: 'GSTR-1', section: 'B2C Summary', rows: readArray(gstr1.value, ['b2CSummaries']).length, taxable: money(sumRows(readArray(gstr1.value, ['b2CSummaries']), ['taxableValue'])), tax: money(sumRows(readArray(gstr1.value, ['b2CSummaries']), ['integratedTax', 'centralTax', 'stateTax'])) },
  { form: 'GSTR-1', section: 'HSN Summary', rows: readArray(gstr1.value, ['hsnSummaries']).length, taxable: money(sumRows(readArray(gstr1.value, ['hsnSummaries']), ['taxableValue'])), tax: money(sumRows(readArray(gstr1.value, ['hsnSummaries']), ['integratedTax', 'centralTax', 'stateTax'])) },
  { form: 'GSTR-3B', section: 'Outward Supplies', rows: '-', taxable: money(readNumber(gstr3b.value?.supplies, ['outwardTaxableValue'])), tax: money(readNumber(gstr3b.value?.supplies, ['outwardIntegratedTax']) + readNumber(gstr3b.value?.supplies, ['outwardCentralTax']) + readNumber(gstr3b.value?.supplies, ['outwardStateTax'])) },
  { form: 'GSTR-3B', section: 'Input Tax Credit', rows: '-', taxable: '-', tax: money(readNumber(gstr3b.value?.itc, ['otherIntegratedTax']) + readNumber(gstr3b.value?.itc, ['otherCentralTax']) + readNumber(gstr3b.value?.itc, ['otherStateTax'])) }
])
const accountingRows = computed(() => accounting.value ? [
  { label: 'Output Tax', value: money(readNumber(accounting.value, ['outputTax'])) },
  { label: 'Input Tax', value: money(readNumber(accounting.value, ['inputTax'])) },
  { label: 'Net Payable', value: money(readNumber(accounting.value, ['netPayable'])) },
  { label: 'Credit Carry Forward', value: money(readNumber(accounting.value, ['creditCarryForward'])) },
  { label: 'Already Posted', value: readText(accounting.value, ['alreadyPosted']) },
  { label: 'Journal Entry', value: readText(accounting.value, ['journalEntryNumber']) }
] : [])

const draftColumns = [
  { key: 'form', label: 'Form' },
  { key: 'period', label: 'Period' },
  { key: 'gstin', label: 'GSTIN' },
  { key: 'title', label: 'Title' },
  { key: 'status', label: 'Status' },
  { key: 'rows', label: 'Rows' },
  { key: 'taxable', label: 'Taxable' },
  { key: 'updated', label: 'Updated' }
]
const detailColumns = [
  { key: 'label', label: 'Field' },
  { key: 'value', label: 'Value' }
]
const issueColumns = [
  { key: 'field', label: 'Field' },
  { key: 'message', label: 'Issue' }
]
const booksColumns = [
  { key: 'form', label: 'Form' },
  { key: 'section', label: 'Section' },
  { key: 'rows', label: 'Rows' },
  { key: 'taxable', label: 'Taxable' },
  { key: 'tax', label: 'Tax' }
]
const accountingColumns = [
  { key: 'label', label: 'Metric' },
  { key: 'value', label: 'Value' }
]

function currentReturnPeriod() {
  const date = new Date()
  return `${String(date.getMonth() + 1).padStart(2, '0')}${date.getFullYear()}`
}

function displayForm(value: unknown) {
  const form = String(value ?? '').toLowerCase()
  if (form === 'gstr1') return 'GSTR-1'
  if (form === 'gstr3b') return 'GSTR-3B'
  return readText({ value }, ['value'])
}

function money(value: unknown) {
  return formatIndianMoney(readNumber({ value }, ['value']))
}

function sumRows(rows: ApiRecord[], keys: string[]) {
  return rows.reduce((sum, row) => sum + keys.reduce((inner, key) => inner + readNumber(row, [key]), 0), 0)
}

function parseIssues(value: string): ApiRecord[] {
  try {
    const parsed = JSON.parse(value || '[]')
    return Array.isArray(parsed) ? parsed as ApiRecord[] : []
  } catch {
    return [{ field: 'Preview', message: 'Preview issue JSON could not be parsed.' }]
  }
}

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const [draftData] = await Promise.allSettled([
      get<unknown>('gst-returns/drafts', { form: formFilter.value, returnPeriod: returnPeriod.value, take: 100 })
    ])
    if (draftData.status === 'fulfilled') drafts.value = toRows(draftData.value)
    if (!selectedDraftId.value && drafts.value.length > 0) await selectDraft(drafts.value[0])
    await loadBooksPreview()
    if (draftData.status === 'rejected') error.value = 'GST drafts could not be loaded.'
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load GST returns.'
  } finally {
    loading.value = false
  }
}

async function loadBooksPreview() {
  booksLoading.value = true
  try {
    const [gstr1Data, gstr3bData, accountingData] = await Promise.allSettled([
      get<unknown>('gst-returns/from-books/gstr1', { returnPeriod: returnPeriod.value }),
      get<unknown>('gst-returns/from-books/gstr3b', { returnPeriod: returnPeriod.value }),
      get<unknown>('gst-returns/accounting-summary', { returnPeriod: returnPeriod.value })
    ])
    if (gstr1Data.status === 'fulfilled' && gstr1Data.value && typeof gstr1Data.value === 'object') gstr1.value = gstr1Data.value as ApiRecord
    if (gstr3bData.status === 'fulfilled' && gstr3bData.value && typeof gstr3bData.value === 'object') gstr3b.value = gstr3bData.value as ApiRecord
    if (accountingData.status === 'fulfilled' && accountingData.value && typeof accountingData.value === 'object') accounting.value = accountingData.value as ApiRecord
  } finally {
    booksLoading.value = false
  }
}

async function selectDraft(row: ApiRecord) {
  const id = readText(row, ['id'], '')
  selectedDraftId.value = id
  selectedDraft.value = row
  if (!id) return
  try {
    const detail = await get<unknown>(`gst-returns/drafts/${id}`)
    if (detail && typeof detail === 'object') selectedDraft.value = detail as ApiRecord
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load GST draft detail.'
  }
}

async function selectDraftById(value: string | number | boolean | Record<string, unknown> | undefined) {
  const id = String(value ?? '')
  const draft = drafts.value.find(item => readText(item, ['id'], '') === id)
  if (draft) await selectDraft(draft)
}

async function downloadDraft(kind: 'json' | 'excel') {
  const id = selectedDraftId.value
  if (!id) return
  downloadLoading.value = kind
  error.value = ''
  try {
    await download(`gst-returns/drafts/${id}/${kind}`, undefined, `${selectedDraftTitle.value}.${kind === 'json' ? 'json' : 'xlsx'}`)
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to download GST draft export.'
  } finally {
    downloadLoading.value = ''
  }
}

watch([formFilter, returnPeriod], () => {
  selectedDraftId.value = ''
  selectedDraft.value = null
})

onMounted(refresh)
</script>
