<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-file-search" class="size-4" /> Accounting statements</p>
          <h2 class="garmetix-dashboard-title">Ledger / Party Statement</h2>
          <p class="garmetix-dashboard-subtitle">Pick a ledger or a party and a period to see its opening balance, every posted entry, and the closing balance.</p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton icon="i-lucide-printer" color="neutral" variant="soft" :disabled="!loaded" @click="printStatement">Print</UButton>
          <UButton icon="i-lucide-download" color="neutral" variant="soft" :disabled="!loaded" @click="exportCsv">Export CSV</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />

    <div class="garmetix-section-card grid gap-3 md:grid-cols-2 xl:grid-cols-[150px_minmax(240px,1.6fr)_170px_150px_150px_auto]">
      <UFormField label="Statement of">
        <USelect v-model="mode" :items="modeItems" class="w-full" @update:model-value="onModeChange" />
      </UFormField>
      <UFormField :label="mode === 'party' ? 'Party' : 'Ledger'">
        <USelectMenu v-model="selectedLedgerId" value-key="value" :items="pickerItems" :placeholder="mode === 'party' ? 'Search party...' : 'Search ledger...'" class="w-full" />
      </UFormField>
      <UFormField label="Period">
        <USelect v-model="preset" :items="presetItems" class="w-full" @update:model-value="applyPreset" />
      </UFormField>
      <UFormField label="From"><UInput v-model="fromDate" type="date" class="w-full" @update:model-value="preset = 'custom'" /></UFormField>
      <UFormField label="To"><UInput v-model="toDate" type="date" class="w-full" @update:model-value="preset = 'custom'" /></UFormField>
      <div class="flex items-end">
        <UButton icon="i-lucide-file-search" color="primary" :loading="loading" :disabled="!selectedLedgerId" @click="loadStatement">View Statement</UButton>
      </div>
    </div>

    <div v-if="!loaded" class="garmetix-section-card p-8 text-center text-muted">
      Select a {{ mode === 'party' ? 'party' : 'ledger' }} and a period, then press View Statement.
    </div>

    <template v-else>
      <section class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
        <div class="garmetix-metric-card">
          <p class="garmetix-metric-label">Opening balance</p>
          <p class="garmetix-metric-value">{{ formatIndianMoney(opening.amount) }} {{ opening.type }}</p>
          <p class="garmetix-metric-caption">As on {{ fromLabel }}</p>
        </div>
        <div class="garmetix-metric-card">
          <p class="garmetix-metric-label">Total debit</p>
          <p class="garmetix-metric-value">{{ formatIndianMoney(totalDebit) }}</p>
          <p class="garmetix-metric-caption">{{ entryRows.length }} entries in the period</p>
        </div>
        <div class="garmetix-metric-card">
          <p class="garmetix-metric-label">Total credit</p>
          <p class="garmetix-metric-value">{{ formatIndianMoney(totalCredit) }}</p>
          <p class="garmetix-metric-caption">{{ periodLabel }}</p>
        </div>
        <div class="garmetix-metric-card">
          <p class="garmetix-metric-label">Closing balance</p>
          <p class="garmetix-metric-value">{{ formatIndianMoney(closing.amount) }} {{ closing.type }}</p>
          <p class="garmetix-metric-caption">As on {{ toLabel }}</p>
        </div>
      </section>

      <section class="garmetix-section-card">
        <div class="mb-3 flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h3 class="garmetix-panel-title">{{ statementTitle }}</h3>
            <p class="garmetix-panel-subtitle">{{ periodLabel }}</p>
          </div>
          <div class="flex items-center gap-2">
            <UInput v-model="search" icon="i-lucide-search" placeholder="Search entry or particulars" class="sm:w-64" />
            <USelect v-model="pageSize" :items="pageSizeItems" class="w-32" />
          </div>
        </div>

        <div class="overflow-x-auto">
          <table class="w-full min-w-[820px] border-collapse text-sm">
            <thead class="bg-muted/30 text-left text-xs uppercase text-muted">
              <tr>
                <th class="border-b border-default p-2">Date</th>
                <th class="border-b border-default p-2">Entry</th>
                <th class="border-b border-default p-2">Source</th>
                <th class="border-b border-default p-2">Particulars</th>
                <th class="border-b border-default p-2 text-right">Debit</th>
                <th class="border-b border-default p-2 text-right">Credit</th>
                <th class="border-b border-default p-2 text-right">Balance</th>
              </tr>
            </thead>
            <tbody>
              <tr v-if="!pagedRows.length"><td colspan="7" class="p-6 text-center text-muted">No entries match.</td></tr>
              <tr v-for="(row, index) in pagedRows" :key="`${row.entry}-${index}`" :class="row.isOpening ? 'bg-muted/20 font-medium' : ''">
                <td class="border-b border-default p-2 whitespace-nowrap">{{ row.date }}</td>
                <td class="border-b border-default p-2">{{ row.entry }}</td>
                <td class="border-b border-default p-2">{{ row.source }}</td>
                <td class="border-b border-default p-2">{{ row.particulars }}</td>
                <td class="border-b border-default p-2 text-right">{{ row.debit }}</td>
                <td class="border-b border-default p-2 text-right">{{ row.credit }}</td>
                <td class="border-b border-default p-2 text-right whitespace-nowrap">{{ row.balance }}</td>
              </tr>
            </tbody>
          </table>
        </div>

        <div class="mt-3 flex flex-wrap items-center justify-between gap-2 text-sm text-muted">
          <span>Page {{ page }} of {{ totalPages }} - {{ filteredRows.length }} rows</span>
          <div class="flex gap-2">
            <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-left" :disabled="page <= 1" @click="page--">Prev</UButton>
            <UButton size="xs" color="neutral" variant="soft" trailing-icon="i-lucide-chevron-right" :disabled="page >= totalPages" @click="page++">Next</UButton>
          </div>
        </div>
      </section>
    </template>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney } from '@garmetix/shared-utils'
import { formatDate, pageSizeOptions, paginateRows, partyTypeOptions, readNumber, readText, toRows, type ApiRecord, useBooksApiClient } from '../utils/books-api'

useHead({ title: 'Ledger / Party Statement - Garmetix' })

type Mode = 'ledger' | 'party'
type Preset = 'thisMonth' | 'lastMonth' | 'thisFy' | 'lastFy' | 'all' | 'custom'

const { get } = useBooksApiClient()
const route = useRoute()

const mode = ref<Mode>('ledger')
const selectedLedgerId = ref('')
const preset = ref<Preset>('thisFy')
const fromDate = ref('')
const toDate = ref('')
const loading = ref(false)
const loaded = ref(false)
const error = ref('')
const search = ref('')
const page = ref(1)
const pageSize = ref<number>(50)

const ledgers = ref<ApiRecord[]>([])
const parties = ref<ApiRecord[]>([])
const rows = ref<ApiRecord[]>([])
const shownName = ref('')
const shownFrom = ref('')
const shownTo = ref('')

const modeItems = [{ label: 'Ledger', value: 'ledger' }, { label: 'Party', value: 'party' }]
const presetItems = [
  { label: 'This month', value: 'thisMonth' },
  { label: 'Last month', value: 'lastMonth' },
  { label: 'This financial year', value: 'thisFy' },
  { label: 'Last financial year', value: 'lastFy' },
  { label: 'All time', value: 'all' },
  { label: 'Custom dates', value: 'custom' }
]
const pageSizeItems = pageSizeOptions.map(item => ({ label: item.label, value: item.value }))

const pickerItems = computed(() => {
  if (mode.value === 'party') {
    const categoryLabel = (value: unknown) => partyTypeOptions.find(option => option.value === Number(value))?.label || ''
    return parties.value
      .filter(item => readText(item, ['ledgerId'], '') && readText(item, ['ledgerId'], '') !== '00000000-0000-0000-0000-000000000000')
      .map(item => ({ label: `${readText(item, ['name'], 'Party')}${categoryLabel(item.category) ? ` (${categoryLabel(item.category)})` : ''}`, value: readText(item, ['ledgerId'], '') }))
      .sort((a, b) => a.label.localeCompare(b.label))
  }
  return ledgers.value
    .map(item => ({ label: readText(item, ['name'], 'Ledger'), value: readText(item, ['id'], '') }))
    .filter(item => item.value)
    .sort((a, b) => a.label.localeCompare(b.label))
})

function iso(date: Date) {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`
}

function applyPreset() {
  const today = new Date()
  const fyStartYear = today.getMonth() >= 3 ? today.getFullYear() : today.getFullYear() - 1
  if (preset.value === 'thisMonth') {
    fromDate.value = iso(new Date(today.getFullYear(), today.getMonth(), 1))
    toDate.value = iso(new Date(today.getFullYear(), today.getMonth() + 1, 0))
  } else if (preset.value === 'lastMonth') {
    fromDate.value = iso(new Date(today.getFullYear(), today.getMonth() - 1, 1))
    toDate.value = iso(new Date(today.getFullYear(), today.getMonth(), 0))
  } else if (preset.value === 'thisFy') {
    fromDate.value = iso(new Date(fyStartYear, 3, 1))
    toDate.value = iso(new Date(fyStartYear + 1, 2, 31))
  } else if (preset.value === 'lastFy') {
    fromDate.value = iso(new Date(fyStartYear - 1, 3, 1))
    toDate.value = iso(new Date(fyStartYear, 2, 31))
  } else if (preset.value === 'all') {
    fromDate.value = ''
    toDate.value = ''
  }
}

function onModeChange() {
  selectedLedgerId.value = ''
  loaded.value = false
  error.value = ''
}

function signed(row: ApiRecord) {
  const amount = readNumber(row, ['balance'])
  return readText(row, ['balanceType'], 'Dr') === 'Cr' ? -amount : amount
}

const isOpeningRow = (row: ApiRecord) => readText(row, ['entryNumber'], '').toUpperCase() === 'OPENING' || readText(row, ['sourceType'], '').toUpperCase() === 'OPENING'
const openingRow = computed(() => rows.value.find(isOpeningRow) || null)
const entryRows = computed(() => rows.value.filter(row => !isOpeningRow(row)))
const opening = computed(() => ({
  amount: openingRow.value ? readNumber(openingRow.value, ['balance']) : 0,
  type: openingRow.value ? readText(openingRow.value, ['balanceType'], 'Dr') : 'Dr'
}))
const totalDebit = computed(() => entryRows.value.reduce((sum, row) => sum + readNumber(row, ['debit']), 0))
const totalCredit = computed(() => entryRows.value.reduce((sum, row) => sum + readNumber(row, ['credit']), 0))
const closing = computed(() => {
  const last = rows.value[rows.value.length - 1]
  if (!last) return { amount: 0, type: 'Dr' }
  const value = signed(last)
  return { amount: Math.abs(value), type: value >= 0 ? 'Dr' : 'Cr' }
})

const fromLabel = computed(() => shownFrom.value ? formatDate(shownFrom.value) : 'the beginning')
const toLabel = computed(() => shownTo.value ? formatDate(shownTo.value) : 'today')
const periodLabel = computed(() => (shownFrom.value || shownTo.value) ? `${fromLabel.value} to ${toLabel.value}` : 'All entries, from the beginning')
const statementTitle = computed(() => `Statement of ${shownName.value}`)

const tableRows = computed(() => rows.value.map((row) => {
  const opening = isOpeningRow(row)
  return {
    isOpening: opening,
    date: formatDate(row.onDate),
    entry: opening ? '' : readText(row, ['entryNumber', 'referenceNumber'], ''),
    source: opening ? '' : readText(row, ['sourceType'], ''),
    particulars: readText(row, ['particulars'], ''),
    debit: readNumber(row, ['debit']) ? formatIndianMoney(readNumber(row, ['debit'])) : '',
    credit: readNumber(row, ['credit']) ? formatIndianMoney(readNumber(row, ['credit'])) : '',
    balance: `${formatIndianMoney(readNumber(row, ['balance']))} ${readText(row, ['balanceType'], '')}`.trim()
  }
}))
const filteredRows = computed(() => {
  const needle = search.value.trim().toLowerCase()
  if (!needle) return tableRows.value
  return tableRows.value.filter(row => row.isOpening || `${row.entry} ${row.source} ${row.particulars}`.toLowerCase().includes(needle))
})
const totalPages = computed(() => Math.max(1, Math.ceil(filteredRows.value.length / Number(pageSize.value || 50))))
const pagedRows = computed(() => paginateRows(filteredRows.value, page.value, pageSize.value))
watch([search, pageSize], () => { page.value = 1 })
watch(totalPages, (value) => { if (page.value > value) page.value = value })

async function loadLists() {
  const [ledgerData, partyData] = await Promise.allSettled([get<unknown>('ledgers'), get<unknown>('parties')])
  if (ledgerData.status === 'fulfilled') ledgers.value = toRows(ledgerData.value)
  if (partyData.status === 'fulfilled') parties.value = toRows(partyData.value)
  if (ledgerData.status === 'rejected' && partyData.status === 'rejected') error.value = 'Unable to load ledgers and parties.'
}

async function loadStatement() {
  if (!selectedLedgerId.value) {
    error.value = `Select a ${mode.value} before loading the statement.`
    return
  }
  if (fromDate.value && toDate.value && fromDate.value > toDate.value) {
    error.value = 'From date must not be after To date.'
    return
  }
  loading.value = true
  error.value = ''
  try {
    rows.value = toRows(await get<unknown>(`accounting/ledger-statement/${selectedLedgerId.value}`, {
      from: fromDate.value || undefined,
      to: toDate.value ? `${toDate.value}T23:59:59` : undefined
    }))
    shownName.value = pickerItems.value.find(item => item.value === selectedLedgerId.value)?.label || 'selected account'
    shownFrom.value = fromDate.value
    shownTo.value = toDate.value
    page.value = 1
    search.value = ''
    loaded.value = true
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load the statement.'
  } finally {
    loading.value = false
  }
}

function csvCell(value: unknown) {
  const text = String(value ?? '')
  return /[",\n]/.test(text) ? `"${text.replace(/"/g, '""')}"` : text
}

function exportCsv() {
  const header = ['Date', 'Entry', 'Source', 'Particulars', 'Debit', 'Credit', 'Balance', 'Dr/Cr']
  const lines = [[`Statement of ${shownName.value}`], [periodLabel.value], header]
  for (const row of rows.value) {
    const open = isOpeningRow(row)
    lines.push([
      formatDate(row.onDate), open ? '' : readText(row, ['entryNumber', 'referenceNumber'], ''), open ? '' : readText(row, ['sourceType'], ''),
      readText(row, ['particulars'], ''), readNumber(row, ['debit']) || '', readNumber(row, ['credit']) || '',
      readNumber(row, ['balance']), readText(row, ['balanceType'], '')
    ])
  }
  lines.push(['', '', '', 'Total', totalDebit.value, totalCredit.value, closing.value.amount, closing.value.type])
  const blob = new Blob([lines.map(line => line.map(csvCell).join(',')).join('\r\n')], { type: 'text/csv;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = `statement-${shownName.value.replace(/[^a-z0-9]+/gi, '-').toLowerCase()}.csv`
  anchor.click()
  URL.revokeObjectURL(url)
}

const escapeHtml = (value: string) => value.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')

function printStatement() {
  const body = tableRows.value.map(row => `<tr${row.isOpening ? ' class="open"' : ''}><td>${escapeHtml(row.date)}</td><td>${escapeHtml(row.entry)}</td><td>${escapeHtml(row.source)}</td><td>${escapeHtml(row.particulars)}</td><td class="n">${escapeHtml(row.debit)}</td><td class="n">${escapeHtml(row.credit)}</td><td class="n">${escapeHtml(row.balance)}</td></tr>`).join('')
  const html = `<!doctype html><html><head><meta charset="utf-8"><title>${escapeHtml(statementTitle.value)}</title>
<style>body{font-family:Arial,sans-serif;font-size:12px;margin:20px}h2{margin:0 0 4px}p{margin:0 0 10px;color:#444}table{width:100%;border-collapse:collapse}th,td{border:1px solid #999;padding:4px 6px;text-align:left}th{background:#eee}.n{text-align:right;white-space:nowrap}.open td{background:#f4f4f4;font-weight:bold}tfoot td{font-weight:bold}</style></head><body>
<h2>${escapeHtml(statementTitle.value)}</h2><p>${escapeHtml(periodLabel.value)}</p>
<table><thead><tr><th>Date</th><th>Entry</th><th>Source</th><th>Particulars</th><th>Debit</th><th>Credit</th><th>Balance</th></tr></thead><tbody>${body}</tbody>
<tfoot><tr><td colspan="4">Total / Closing balance</td><td class="n">${escapeHtml(formatIndianMoney(totalDebit.value))}</td><td class="n">${escapeHtml(formatIndianMoney(totalCredit.value))}</td><td class="n">${escapeHtml(formatIndianMoney(closing.value.amount))} ${closing.value.type}</td></tr></tfoot></table></body></html>`
  const win = window.open('', '_blank')
  if (!win) {
    error.value = 'Allow pop-ups to print the statement.'
    return
  }
  win.document.write(html)
  win.document.close()
  win.focus()
  win.print()
}

onMounted(async () => {
  applyPreset()
  await loadLists()
  const queryLedger = String(route.query.ledgerId || '')
  if (queryLedger) {
    selectedLedgerId.value = queryLedger
    await loadStatement()
  }
})
</script>
