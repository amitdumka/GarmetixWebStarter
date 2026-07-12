<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-book-open-check" class="size-4" /> Accountant evidence</p>
          <h2 class="garmetix-dashboard-title">Day Book</h2>
          <p class="garmetix-dashboard-subtitle">
            Date-wise transaction book for sales, purchases, vouchers, receipts, payments and optional journal evidence.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UDropdownMenu :items="quickAddItems">
            <UButton icon="i-lucide-plus" trailing-icon="i-lucide-chevron-down">New</UButton>
          </UDropdownMenu>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
          <UBadge color="primary" variant="subtle">Legacy parity</UBadge>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />
    <UAlert v-if="message" color="success" variant="subtle" icon="i-lucide-circle-check" :description="message" />

    <section class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
      <div v-for="card in cards" :key="card.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label">{{ card.label }}</p>
        <p class="garmetix-metric-value">{{ card.value }}</p>
        <p class="garmetix-metric-caption">{{ card.detail }}</p>
      </div>
    </section>

    <section class="garmetix-section-card">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
        <div>
          <h3 class="garmetix-panel-title">Evidence Export</h3>
          <p class="garmetix-panel-subtitle">CSV exports up to 5,000 rows. Print opens the backend evidence view for PDF saving.</p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton icon="i-lucide-file-spreadsheet" color="neutral" variant="soft" :loading="exportLoading" @click="downloadCsv">Export CSV</UButton>
          <UButton icon="i-lucide-printer" color="neutral" variant="soft" :loading="printLoading" @click="openPrint">Print / PDF</UButton>
        </div>
      </div>
    </section>

    <section class="garmetix-section-card">
      <div class="grid gap-3 md:grid-cols-2 xl:grid-cols-8">
        <label class="space-y-1 text-sm">
          <span class="text-muted">Book Date</span>
          <UInput v-model="selectedDate" type="date" class="w-full" @focus="datePreset = 'date'" />
        </label>
        <label class="space-y-1 text-sm">
          <span class="text-muted">Period</span>
          <USelect v-model="datePreset" :items="datePresetOptions" class="w-full" />
        </label>
        <label class="space-y-1 text-sm">
          <span class="text-muted">Type</span>
          <USelect v-model="typeFilter" :items="typeOptions" class="w-full" />
        </label>
        <label class="space-y-1 text-sm">
          <span class="text-muted">Rows</span>
          <USelect v-model="pageSize" :items="pageSizeOptions" class="w-full" />
        </label>
        <label v-if="datePreset === 'month-year'" class="space-y-1 text-sm">
          <span class="text-muted">Month</span>
          <USelect v-model="month" :items="monthOptions" class="w-full" />
        </label>
        <label v-if="datePreset === 'month-year'" class="space-y-1 text-sm">
          <span class="text-muted">Year</span>
          <USelect v-model="year" :items="yearOptions" class="w-full" />
        </label>
        <label v-if="datePreset === 'custom'" class="space-y-1 text-sm">
          <span class="text-muted">From</span>
          <UInput v-model="fromDate" type="date" class="w-full" />
        </label>
        <label v-if="datePreset === 'custom'" class="space-y-1 text-sm">
          <span class="text-muted">To</span>
          <UInput v-model="toDate" type="date" class="w-full" />
        </label>
        <label class="space-y-1 text-sm md:col-span-2 xl:col-span-2">
          <span class="text-muted">Search</span>
          <UInput v-model="search" icon="i-lucide-search" placeholder="Voucher, party, invoice, particulars" class="w-full" @keyup.enter="resetPageAndRefresh" />
        </label>
      </div>
      <div class="mt-3 flex flex-wrap items-center gap-2">
        <UButton icon="i-lucide-search" :loading="loading" @click="resetPageAndRefresh">Apply</UButton>
        <UButton icon="i-lucide-chevron-left" color="neutral" variant="soft" @click="moveDay(-1)">Previous Day</UButton>
        <UButton icon="i-lucide-calendar-days" color="neutral" variant="soft" @click="goToday">Today</UButton>
        <UButton trailing-icon="i-lucide-chevron-right" color="neutral" variant="soft" @click="moveDay(1)">Next Day</UButton>
        <UCheckbox v-model="includeJournalEntries" label="Show journal entries" />
      </div>
    </section>

    <section class="garmetix-table-panel">
      <div class="mb-3 flex flex-col gap-2 lg:flex-row lg:items-center lg:justify-between">
        <div>
          <h3 class="garmetix-panel-title">Transaction Register</h3>
          <p class="garmetix-panel-subtitle">{{ totalRows }} row(s) - page {{ page }} of {{ totalPages }} - {{ dateCaption }}</p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton icon="i-lucide-file-spreadsheet" size="sm" color="neutral" variant="ghost" :loading="exportLoading" @click="downloadCsv">CSV</UButton>
          <UButton icon="i-lucide-printer" size="sm" color="neutral" variant="ghost" :loading="printLoading" @click="openPrint">Print</UButton>
          <UButton icon="i-lucide-chevron-left" size="sm" color="neutral" variant="soft" :disabled="page <= 1" @click="previousPage">Previous</UButton>
          <UButton trailing-icon="i-lucide-chevron-right" size="sm" color="neutral" variant="soft" :disabled="page >= totalPages" @click="nextPage">Next</UButton>
        </div>
      </div>

      <div class="overflow-hidden rounded-lg border border-default">
        <div class="overflow-x-auto">
          <table class="w-full min-w-[1120px] text-left text-sm">
            <thead class="bg-muted/30 text-xs uppercase text-muted">
              <tr>
                <th class="whitespace-nowrap px-3 py-2 font-medium">Date</th>
                <th class="whitespace-nowrap px-3 py-2 font-medium">Type</th>
                <th class="whitespace-nowrap px-3 py-2 font-medium">No.</th>
                <th class="whitespace-nowrap px-3 py-2 font-medium">Party</th>
                <th class="whitespace-nowrap px-3 py-2 font-medium">Particulars</th>
                <th class="whitespace-nowrap px-3 py-2 text-right font-medium">Debit</th>
                <th class="whitespace-nowrap px-3 py-2 text-right font-medium">Credit</th>
                <th class="whitespace-nowrap px-3 py-2 font-medium">Mode</th>
                <th class="whitespace-nowrap px-3 py-2 font-medium">Status</th>
                <th class="whitespace-nowrap px-3 py-2 font-medium">Action</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-default">
              <tr v-if="rows.length === 0">
                <td colspan="10" class="px-3 py-8 text-center text-muted">
                  {{ loading ? 'Loading day book...' : 'No day-book rows found for the selected filters.' }}
                </td>
              </tr>
              <tr v-for="row in rows" :key="rowKey(row)" class="bg-default/40">
                <td class="whitespace-nowrap px-3 py-2">{{ formatDate(readText(row, ['onDate'], '')) }}</td>
                <td class="whitespace-nowrap px-3 py-2">
                  <UBadge :color="rowTone(row)" variant="subtle">{{ readText(row, ['documentSubType', 'documentType']) }}</UBadge>
                </td>
                <td class="max-w-44 truncate px-3 py-2 font-medium">{{ readText(row, ['documentNumber']) }}</td>
                <td class="max-w-48 truncate px-3 py-2">{{ readText(row, ['partyName']) }}</td>
                <td class="max-w-md truncate px-3 py-2">{{ readText(row, ['particulars']) }}</td>
                <td class="whitespace-nowrap px-3 py-2 text-right">{{ moneyOrDash(row, 'debitAmount') }}</td>
                <td class="whitespace-nowrap px-3 py-2 text-right">{{ moneyOrDash(row, 'creditAmount') }}</td>
                <td class="whitespace-nowrap px-3 py-2">{{ readText(row, ['paymentMode']) }}</td>
                <td class="whitespace-nowrap px-3 py-2">{{ readText(row, ['status']) }}</td>
                <td class="px-3 py-2">
                  <div class="flex flex-wrap gap-1">
                    <UButton icon="i-lucide-eye" size="xs" color="neutral" variant="soft" :loading="detailLoading === readText(row, ['detailApiPath'], '')" @click="openDetail(row)">
                      View
                    </UButton>
                    <UButton icon="i-lucide-external-link" size="xs" color="primary" variant="ghost" @click="openSource(row)">
                      Source
                    </UButton>
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <div v-if="rows.length === 0 && !loading" class="mt-3 flex flex-wrap gap-2">
        <UDropdownMenu :items="quickAddItems">
          <UButton icon="i-lucide-plus" color="neutral" variant="soft">Create Transaction</UButton>
        </UDropdownMenu>
      </div>
    </section>

    <USlideover v-model:open="detailOpen" :ui="{ content: 'w-full sm:max-w-4xl lg:max-w-6xl xl:max-w-7xl' }">
      <template #content>
        <div class="space-y-4 p-4">
          <div class="flex items-start justify-between gap-3">
            <div>
              <p class="text-xs uppercase text-muted">Day Book Detail</p>
              <h2 class="text-lg font-semibold">{{ readText(selectedRow, ['documentNumber'], 'Transaction') }}</h2>
              <p class="text-sm text-muted">{{ readText(selectedRow, ['documentSubType']) }} - {{ readText(selectedRow, ['partyName']) }}</p>
              <p v-if="readText(selectedRow, ['id'], '')" class="text-xs text-muted">ID: <span class="font-mono">{{ readText(selectedRow, ['id']) }}</span></p>
            </div>
            <UButton icon="i-lucide-x" color="neutral" variant="ghost" @click="detailOpen = false" />
          </div>

          <UAlert v-if="detailLoading" color="neutral" variant="subtle" icon="i-lucide-loader" title="Loading transaction detail" />
          <template v-else-if="selectedDetail">
            <section class="grid gap-3 md:grid-cols-3">
              <div v-for="item in selectedRowCards" :key="item.label" class="garmetix-metric-card">
                <p class="garmetix-metric-label">{{ item.label }}</p>
                <p class="garmetix-metric-value text-base">{{ item.value }}</p>
                <p class="garmetix-metric-caption">{{ item.detail }}</p>
              </div>
            </section>

            <div class="garmetix-section-card">
              <p class="text-xs text-muted">Particulars</p>
              <p class="mt-1 text-sm">{{ readText(selectedRow, ['particulars']) }}</p>
            </div>

            <div v-if="detailRows.length" class="garmetix-section-card">
              <h3 class="garmetix-panel-title">Relevant Fields</h3>
              <dl class="mt-3 grid gap-3 md:grid-cols-2 xl:grid-cols-3">
                <div v-for="item in detailRows" :key="item.key" class="border-b border-default pb-2 text-sm">
                  <dt class="text-xs text-muted">{{ item.label }}</dt>
                  <dd class="mt-1 break-words font-medium">{{ item.value }}</dd>
                </div>
              </dl>
            </div>

            <div v-if="relatedLines.length" class="garmetix-section-card">
              <h3 class="garmetix-panel-title">Related Lines</h3>
              <div class="mt-3 max-h-80 overflow-auto rounded-lg border border-default">
                <table class="w-full min-w-[900px] text-left text-xs">
                  <thead class="bg-muted/30 text-muted">
                    <tr>
                      <th v-for="column in relatedLineColumns" :key="column.key" class="px-2 py-2 font-medium">{{ column.label }}</th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-default">
                    <tr v-for="(line, index) in relatedLines" :key="index">
                      <td v-for="column in relatedLineColumns" :key="column.key" class="px-2 py-2">{{ tableValue(line, column.key) }}</td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </div>

            <div class="flex flex-wrap gap-2">
              <UButton icon="i-lucide-external-link" color="primary" variant="soft" @click="openSource()">Open Source</UButton>
              <UButton icon="i-lucide-x" color="neutral" variant="ghost" @click="detailOpen = false">Close</UButton>
            </div>
          </template>
        </div>
      </template>
    </USlideover>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney } from '@garmetix/shared-utils'
import { readArray, readNumber, readText, type ApiRecord, useBooksApiClient } from '../utils/books-api'

useHead({ title: 'Day Book - Garmetix Books' })

const runtimeConfig = useRuntimeConfig()
const { download, get, openBlob } = useBooksApiClient()

const loading = ref(false)
const exportLoading = ref(false)
const printLoading = ref(false)
const detailLoading = ref('')
const detailOpen = ref(false)
const error = ref('')
const message = ref('')
const result = ref<ApiRecord>({ rows: [], summary: {}, page: 1, pageSize: 50, total: 0 })
const selectedDetail = ref<ApiRecord | null>(null)
const datePreset = ref('date')
const typeFilter = ref('all')
const includeJournalEntries = ref(false)
const search = ref('')
const page = ref(1)
const pageSize = ref(50)
const now = new Date()
const month = ref(now.getMonth() + 1)
const year = ref(now.getFullYear())
const selectedDate = ref(toInputDate(now))
const fromDate = ref(toInputDate(now))
const toDate = ref(toInputDate(now))
const restoringState = ref(false)

const DAY_BOOK_STATE_KEY = 'garmetix.modular.books.dayBook.listState.v1'

const datePresetOptions = [
  { value: 'date', label: 'Single Date' },
  { value: 'today', label: 'Today' },
  { value: 'yesterday', label: 'Yesterday' },
  { value: 'month', label: 'This Month' },
  { value: 'last-month', label: 'Last Month' },
  { value: 'year', label: 'This Year' },
  { value: 'month-year', label: 'Month / Year' },
  { value: 'custom', label: 'Custom Range' }
]
const typeOptions = [
  { value: 'all', label: 'All visible transactions' },
  { value: 'sales', label: 'Sales + receipts' },
  { value: 'purchase', label: 'Purchase + vendor payments' },
  { value: 'vouchers', label: 'Vouchers' },
  { value: 'payments', label: 'Payments only' },
  { value: 'journal', label: 'Journal entries' }
]
const pageSizeOptions = [25, 50, 100, 200].map(value => ({ value, label: `${value} rows` }))
const monthOptions = [
  'January', 'February', 'March', 'April', 'May', 'June',
  'July', 'August', 'September', 'October', 'November', 'December'
].map((label, index) => ({ value: index + 1, label }))
const yearOptions = Array.from({ length: 8 }, (_, index) => now.getFullYear() - index).map(value => ({ value, label: String(value) }))

const rows = computed(() => readArray(result.value, ['rows']))
const summary = computed(() => (result.value.summary && typeof result.value.summary === 'object' ? result.value.summary as ApiRecord : {}))
const totalRows = computed(() => readNumber(result.value, ['total']))
const totalPages = computed(() => Math.max(1, Math.ceil(totalRows.value / Math.max(1, Number(pageSize.value || 50)))))
const selectedRow = computed(() => (selectedDetail.value?.row && typeof selectedDetail.value.row === 'object' ? selectedDetail.value.row as ApiRecord : null))
const dateCaption = computed(() => {
  const from = readText(result.value, ['from'], '')
  const to = readText(result.value, ['to'], '')
  const fromLabel = from ? formatDate(from) : '-'
  const toLabel = to ? formatDate(to) : '-'
  return fromLabel === toLabel ? fromLabel : `${fromLabel} to ${toLabel}`
})
const cards = computed(() => [
  { label: 'Transactions', value: readNumber(summary.value, ['count']), detail: dateCaption.value },
  { label: 'Credit / Inflow', value: formatIndianMoney(readNumber(summary.value, ['creditAmount'])), detail: 'Receipts and sales' },
  { label: 'Debit / Outflow', value: formatIndianMoney(readNumber(summary.value, ['debitAmount'])), detail: 'Purchases and payments' },
  { label: 'Net', value: formatIndianMoney(readNumber(summary.value, ['netAmount'])), detail: 'Credit minus debit' }
])
const selectedRowCards = computed(() => [
  { label: 'Date', value: formatDateTime(readText(selectedRow.value, ['onDate'], '')), detail: readText(selectedRow.value, ['documentType']) },
  { label: 'Amount', value: formatIndianMoney(Math.abs(readNumber(selectedRow.value, ['netAmount']) || readNumber(selectedRow.value, ['creditAmount']) || readNumber(selectedRow.value, ['debitAmount']))), detail: readText(selectedRow.value, ['paymentMode']) },
  { label: 'Status', value: readText(selectedRow.value, ['status']), detail: readText(selectedRow.value, ['openActionLabel'], 'Source available') }
])
const detailRows = computed(() => toKeyValueRows(selectedDetail.value?.detail))
const relatedLines = computed(() => readArray(selectedDetail.value, ['lines']))
const relatedLineColumns = computed(() => tableColumns(relatedLines.value))
const appUrls = computed(() => (runtimeConfig.public.appUrls ?? {}) as Record<string, string | undefined>)
const quickAddItems = computed(() => [[
  { label: 'New Sale Invoice', icon: 'i-lucide-receipt-text', onSelect: () => openMappedSource('/sale', 'pos') },
  { label: 'New Purchase Inward', icon: 'i-lucide-package-plus', onSelect: () => openMappedSource('/purchase/new', 'main') },
  { label: 'Add Vendor Payment', icon: 'i-lucide-hand-coins', onSelect: () => openMappedSource('/vendor-payments?new=invoice', 'books') },
  { label: 'Add Vendor Advance', icon: 'i-lucide-wallet-cards', onSelect: () => openMappedSource('/vendor-payments?new=advance', 'books') },
  { label: 'New Book Voucher', icon: 'i-lucide-banknote', onSelect: () => openMappedSource('/vouchers?new=1', 'books') },
  { label: 'New Cash Voucher', icon: 'i-lucide-wallet', onSelect: () => openMappedSource('/cash-vouchers?new=1', 'pos') }
]])

function buildDayBookQuery(exportMode = false) {
  const query: Record<string, string | number | boolean> = {
    datePreset: datePreset.value,
    type: typeFilter.value,
    page: exportMode ? 1 : page.value,
    pageSize: exportMode ? 5000 : pageSize.value
  }
  if (search.value.trim()) query.q = search.value.trim()
  if (includeJournalEntries.value) query.includeJournal = true
  if (datePreset.value === 'date') query.date = selectedDate.value
  if (datePreset.value === 'month-year') {
    query.month = month.value
    query.year = year.value
  }
  if (datePreset.value === 'custom') {
    query.from = fromDate.value
    query.to = toDate.value
  }
  return query
}

async function refresh() {
  persistDayBookState()
  loading.value = true
  error.value = ''
  try {
    result.value = await get<ApiRecord>('day-book', buildDayBookQuery(false))
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load Day Book.'
  } finally {
    loading.value = false
  }
}

async function downloadCsv() {
  exportLoading.value = true
  error.value = ''
  message.value = ''
  try {
    persistDayBookState()
    await download('day-book/export.csv', buildDayBookQuery(true), `garmetix-day-book-${dateCaption.value.replace(/[^0-9a-z-]+/gi, '-')}.csv`)
    message.value = 'Day Book CSV downloaded.'
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to export Day Book CSV.'
  } finally {
    exportLoading.value = false
  }
}

async function openPrint() {
  printLoading.value = true
  error.value = ''
  try {
    persistDayBookState()
    await openBlob('day-book/print', buildDayBookQuery(true))
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to open Day Book print view.'
  } finally {
    printLoading.value = false
  }
}

async function openDetail(row: ApiRecord) {
  const detailApiPath = readText(row, ['detailApiPath'], '')
  if (!detailApiPath) return
  selectedDetail.value = null
  detailOpen.value = true
  detailLoading.value = detailApiPath
  error.value = ''
  try {
    selectedDetail.value = await get<ApiRecord>(detailApiPath)
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Could not open transaction detail.'
  } finally {
    detailLoading.value = ''
  }
}

function openSource(row?: ApiRecord | null) {
  const sourceRow = row ?? selectedRow.value
  const sourcePath = readText(sourceRow, ['sourcePath'], readText(selectedDetail.value, ['sourcePath'], ''))
  if (!sourcePath) return
  persistDayBookState()
  const mapped = mapLegacySourcePath(sourcePath, sourceRow ?? undefined)
  openMappedSource(mapped.path, mapped.app)
}

function openMappedSource(path: string, app: 'main' | 'pos' | 'books') {
  if (!import.meta.client) return
  persistDayBookState()
  const target = withDayBookReturnHint(path)
  if (app === 'books') {
    navigateTo(target)
    return
  }
  window.location.assign(joinAppPath(app, target))
}

function mapLegacySourcePath(sourcePath: string, row?: ApiRecord): { app: 'main' | 'pos' | 'books', path: string } {
  const [pathAndQuery, hash = ''] = String(sourcePath || '').split('#')
  const [rawPath, rawQuery = ''] = pathAndQuery.split('?')
  const query = new URLSearchParams(rawQuery)
  let app: 'main' | 'pos' | 'books' = 'books'
  let path = rawPath || '/'

  if (path.startsWith('/billing')) {
    app = 'pos'
    path = '/history'
  } else if (path.startsWith('/purchase')) {
    app = 'main'
    path = path.replace(/^\/purchase(?=\/|$)/, '/purchase') || '/purchase'
  } else if (path.startsWith('/cash-vouchers')) {
    app = 'pos'
  } else if (path.startsWith('/vendor-payments') || path.startsWith('/vouchers') || path.startsWith('/accounting')) {
    app = 'books'
  }

  if (row) {
    const id = readText(row, ['id'], '')
    const type = readText(row, ['documentType'], '')
    if (type === 'SaleInvoice' && id && !query.has('invoiceId')) query.set('invoiceId', id)
    if (type === 'Voucher' && id && !query.has('voucherId')) query.set('voucherId', id)
    if (type === 'VendorPayment' && id && !query.has('paymentId')) query.set('paymentId', id)
  }

  const queryText = query.toString()
  return { app, path: `${path}${queryText ? `?${queryText}` : ''}${hash ? `#${hash}` : ''}` }
}

function withDayBookReturnHint(path: string) {
  const [pathAndQuery, hash = ''] = String(path || '/').split('#')
  const [rawPath, rawQuery = ''] = pathAndQuery.split('?')
  const query = new URLSearchParams(rawQuery)
  query.set('fromDayBook', '1')
  query.set('dayBookPreset', datePreset.value)
  query.set('dayBookType', typeFilter.value)
  const queryText = query.toString()
  return `${rawPath || '/'}${queryText ? `?${queryText}` : ''}${hash ? `#${hash}` : ''}`
}

function joinAppPath(app: 'main' | 'pos' | 'books', path: string) {
  const configured = appUrl(app)
  const base = configured || fallbackAppBase(app)
  const cleanPath = path.startsWith('/') ? path : `/${path}`
  return `${base.replace(/\/+$/, '')}${cleanPath}`
}

function appUrl(app: 'main' | 'pos' | 'books') {
  const keyMap = {
    main: ['NUXT_PUBLIC_GARMETIX_MAIN_URL', 'NUXT_PUBLIC_MAIN_WEB_URL'],
    pos: ['NUXT_PUBLIC_GARMETIX_POS_URL', 'NUXT_PUBLIC_POS_WEB_URL'],
    books: ['NUXT_PUBLIC_GARMETIX_BOOKS_URL', 'NUXT_PUBLIC_ACCOUNTING_WEB_URL']
  }[app]
  for (const key of keyMap) {
    const value = String(appUrls.value[key] || '').trim()
    if (value) return value
  }
  return ''
}

function fallbackAppBase(app: 'main' | 'pos' | 'books') {
  if (app === 'main') return ''
  if (app === 'pos') return '/pos'
  return ''
}

function currentDayBookState() {
  return {
    datePreset: datePreset.value,
    typeFilter: typeFilter.value,
    includeJournalEntries: includeJournalEntries.value,
    search: search.value,
    page: page.value,
    pageSize: pageSize.value,
    month: month.value,
    year: year.value,
    selectedDate: selectedDate.value,
    fromDate: fromDate.value,
    toDate: toDate.value,
    savedAt: new Date().toISOString()
  }
}

function persistDayBookState() {
  if (!import.meta.client || restoringState.value) return
  window.sessionStorage.setItem(DAY_BOOK_STATE_KEY, JSON.stringify(currentDayBookState()))
}

function restoreDayBookState() {
  if (!import.meta.client) return
  const raw = window.sessionStorage.getItem(DAY_BOOK_STATE_KEY)
  if (!raw) return
  try {
    const stored = JSON.parse(raw)
    restoringState.value = true
    if (isValidOption(stored.datePreset, datePresetOptions)) datePreset.value = stored.datePreset
    if (isValidOption(stored.typeFilter, typeOptions)) typeFilter.value = stored.typeFilter
    includeJournalEntries.value = Boolean(stored.includeJournalEntries)
    search.value = typeof stored.search === 'string' ? stored.search : ''
    page.value = safePositiveInt(stored.page, 1)
    pageSize.value = isValidOption(stored.pageSize, pageSizeOptions) ? Number(stored.pageSize) : 50
    month.value = isValidOption(stored.month, monthOptions) ? Number(stored.month) : now.getMonth() + 1
    year.value = isValidOption(stored.year, yearOptions) ? Number(stored.year) : now.getFullYear()
    if (isInputDate(stored.selectedDate)) selectedDate.value = stored.selectedDate
    if (isInputDate(stored.fromDate)) fromDate.value = stored.fromDate
    if (isInputDate(stored.toDate)) toDate.value = stored.toDate
  } catch {
    window.sessionStorage.removeItem(DAY_BOOK_STATE_KEY)
  } finally {
    nextTick(() => { restoringState.value = false })
  }
}

function resetPageAndRefresh() {
  if (restoringState.value) return
  page.value = 1
  refresh()
}

function nextPage() {
  if (page.value >= totalPages.value) return
  page.value += 1
  refresh()
}

function previousPage() {
  if (page.value <= 1) return
  page.value -= 1
  refresh()
}

function moveDay(delta: number) {
  const current = selectedDate.value ? new Date(`${selectedDate.value}T00:00:00`) : new Date()
  current.setDate(current.getDate() + delta)
  selectedDate.value = toInputDate(current)
  datePreset.value = 'date'
  resetPageAndRefresh()
}

function goToday() {
  selectedDate.value = toInputDate(new Date())
  datePreset.value = 'date'
  resetPageAndRefresh()
}

function isValidOption(value: unknown, options: Array<{ value: string | number }>) {
  return options.some(option => option.value === value)
}

function isInputDate(value: unknown): value is string {
  return typeof value === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(value)
}

function safePositiveInt(value: unknown, fallback: number) {
  const parsed = Number(value)
  return Number.isFinite(parsed) && parsed > 0 ? Math.floor(parsed) : fallback
}

function toInputDate(value: Date) {
  const copy = new Date(value)
  copy.setMinutes(copy.getMinutes() - copy.getTimezoneOffset())
  return copy.toISOString().slice(0, 10)
}

function formatDate(value: unknown) {
  if (!value) return '-'
  const date = new Date(String(value))
  if (Number.isNaN(date.getTime())) return String(value)
  return new Intl.DateTimeFormat('en-IN', { dateStyle: 'medium' }).format(date)
}

function formatDateTime(value: unknown) {
  if (!value) return '-'
  const date = new Date(String(value))
  if (Number.isNaN(date.getTime())) return String(value)
  return new Intl.DateTimeFormat('en-IN', { dateStyle: 'medium', timeStyle: 'short' }).format(date)
}

function moneyOrDash(row: ApiRecord, key: string) {
  const value = readNumber(row, [key])
  return value ? formatIndianMoney(value) : '-'
}

function rowTone(row: ApiRecord) {
  const net = readNumber(row, ['netAmount'])
  if (net > 0) return 'success'
  if (net < 0) return 'warning'
  return 'neutral'
}

function rowKey(row: ApiRecord) {
  return `${readText(row, ['documentType'], 'row')}-${readText(row, ['id'], readText(row, ['documentNumber']))}`
}

const hiddenDetailKeys = new Set([
  'companyid', 'storegroupid', 'storeid', 'tenantid', 'workspaceid', 'bankaccountid', 'accountnumber',
  'createdat', 'createdby', 'updatedat', 'updatedby', 'modifiedat', 'modifiedby', 'deletedat', 'deletedby',
  'isdeleted', 'rowversion', 'concurrencytoken', 'passwordhash', 'securitystamp'
])
const relevantKeyPattern = /(id|number|date|name|mobile|phone|gst|amount|total|discount|tax|qty|quantity|mrp|rate|price|mode|status|type|particular|remark|reference|invoice|inward|voucher|barcode|product|hsn|ledger|narration|paid|balance|due|roundoff|count|slip)/i

function humanizeKey(key: string) {
  return String(key || '')
    .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
    .replace(/[_-]+/g, ' ')
    .replace(/\s+/g, ' ')
    .trim()
    .replace(/^./, match => match.toUpperCase())
}

function isIsoDateText(value: string) {
  return /^\d{4}-\d{2}-\d{2}(T|$)/.test(value) && !Number.isNaN(Date.parse(value))
}

function formatCell(value: unknown): string {
  if (value === null || value === undefined || value === '') return '-'
  if (typeof value === 'number') return Number.isFinite(value) ? String(value) : '-'
  if (typeof value === 'boolean') return value ? 'Yes' : 'No'
  if (value instanceof Date) return formatDateTime(value.toISOString())
  if (typeof value === 'string') return isIsoDateText(value) ? formatDateTime(value) : value
  if (Array.isArray(value)) {
    if (!value.length) return '-'
    if (value.every(item => typeof item !== 'object' || item === null)) return value.map(formatCell).join(', ')
    return `${value.length} item${value.length === 1 ? '' : 's'}`
  }
  if (typeof value === 'object') {
    const entries = Object.entries(value).filter(([, itemValue]) => itemValue !== null && itemValue !== undefined && itemValue !== '')
    if (!entries.length) return '-'
    return entries.slice(0, 6).map(([key, itemValue]) => `${humanizeKey(key)}: ${formatCell(itemValue)}`).join(' / ')
  }
  return String(value)
}

function isHiddenDetailKey(key: string) {
  const normalized = String(key || '').toLowerCase()
  return hiddenDetailKeys.has(normalized) || normalized.endsWith('navigation') || normalized.endsWith('json')
}

function isRelevantScalar(value: unknown) {
  return value === null || value === undefined || ['string', 'number', 'boolean'].includes(typeof value) || value instanceof Date
}

function isRelevantDetailField(key: string, value: unknown) {
  if (isHiddenDetailKey(key)) return false
  if (!isRelevantScalar(value)) return false
  return key.toLowerCase() === 'id' || relevantKeyPattern.test(key)
}

function detailLabel(key: string) {
  return key.toLowerCase() === 'id' ? 'ID' : humanizeKey(key)
}

function toKeyValueRows(value: unknown) {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return []
  return Object.entries(value)
    .filter(([key, itemValue]) => isRelevantDetailField(key, itemValue))
    .map(([key, itemValue]) => ({ key, label: detailLabel(key), value: formatCell(itemValue) }))
}

function tableColumns(lines: ApiRecord[]) {
  const keys = new Set<string>()
  for (const line of lines) {
    if (line && typeof line === 'object' && !Array.isArray(line)) {
      Object.keys(line).filter(key => isRelevantDetailField(key, line[key])).forEach(key => keys.add(key))
    } else keys.add('value')
  }
  return Array.from(keys).map(key => ({ key, label: detailLabel(key) }))
}

function tableValue(line: ApiRecord, key: string) {
  if (!line || typeof line !== 'object' || Array.isArray(line)) return key === 'value' ? formatCell(line) : '-'
  return formatCell(line[key])
}

watch([datePreset, typeFilter, includeJournalEntries, pageSize], resetPageAndRefresh)
watch(search, persistDayBookState)
watch(selectedDate, () => { if (datePreset.value === 'date') resetPageAndRefresh() })
watch([month, year, fromDate, toDate], () => {
  if (datePreset.value === 'month-year' || datePreset.value === 'custom') resetPageAndRefresh()
})

onMounted(async () => {
  restoreDayBookState()
  await refresh()
})
</script>
