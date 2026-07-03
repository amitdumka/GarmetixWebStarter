<script setup lang="ts">
const api = useGarmetixApi()
const config = useRuntimeConfig()
const auth = useAuth()
const feedback = useUiFeedback()
const isAuthenticated = auth.isAuthenticated

const loading = ref(false)
const exportLoading = ref(false)
const printLoading = ref(false)
const detailLoading = ref(false)
const detailOpen = ref(false)
const result = ref<any>({ rows: [], summary: {}, page: 1, pageSize: 50, total: 0 })
const selectedDetail = ref<any | null>(null)
const datePreset = ref('date')
const typeFilter = ref('all')
const includeJournalEntries = ref(false)
const search = ref('')
const page = ref(1)
const pageSize = ref(50)
const month = ref(new Date().getMonth() + 1)
const year = ref(new Date().getFullYear())
const selectedDate = ref(toInputDate(new Date()))
const fromDate = ref(toInputDate(new Date()))
const toDate = ref(toInputDate(new Date()))
const restoringState = ref(false)

const DAY_BOOK_STATE_KEY = 'garmetix.dayBook.listState.v1'

const datePresetOptions = [
  { value: 'date', label: 'Single date' },
  { value: 'today', label: 'Today' },
  { value: 'yesterday', label: 'Yesterday' },
  { value: 'month', label: 'This month' },
  { value: 'last-month', label: 'Last month' },
  { value: 'year', label: 'This year' },
  { value: 'month-year', label: 'Month-year' },
  { value: 'custom', label: 'Custom' }
]
const typeOptions = [
  { value: 'all', label: 'All visible transactions' },
  { value: 'sales', label: 'Sales + receipts' },
  { value: 'purchase', label: 'Purchase + vendor payments' },
  { value: 'vouchers', label: 'Vouchers' },
  { value: 'payments', label: 'Payments only' },
  { value: 'journal', label: 'Journal entries' }
]
const pageSizeOptions = [25, 50, 100, 200].map((value) => ({ value, label: `${value} rows` }))
const monthOptions = [
  'January', 'February', 'March', 'April', 'May', 'June',
  'July', 'August', 'September', 'October', 'November', 'December'
].map((label, index) => ({ value: index + 1, label }))
const yearOptions = Array.from({ length: 8 }, (_, index) => new Date().getFullYear() - index).map((value) => ({ value, label: String(value) }))

const quickAddItems = [
  { label: 'New Sale Invoice', icon: 'i-lucide-receipt-text', onSelect: () => { persistDayBookState(); navigateTo(sourceWithDayBookReturnHint('/billing/new')) } },
  { label: 'New Purchase Inward', icon: 'i-lucide-package-plus', onSelect: () => { persistDayBookState(); navigateTo(sourceWithDayBookReturnHint('/purchase/new')) } },
  { label: 'Add Vendor Payment', icon: 'i-lucide-hand-coins', onSelect: () => { persistDayBookState(); navigateTo(sourceWithDayBookReturnHint('/vendor-payments?new=invoice')) } },
  { label: 'Add Vendor Advance', icon: 'i-lucide-wallet-cards', onSelect: () => { persistDayBookState(); navigateTo(sourceWithDayBookReturnHint('/vendor-payments?new=advance')) } },
  { label: 'New Book Voucher', icon: 'i-lucide-banknote', onSelect: () => { persistDayBookState(); navigateTo(sourceWithDayBookReturnHint('/vouchers?new=1')) } },
  { label: 'New Cash Voucher', icon: 'i-lucide-wallet', onSelect: () => { persistDayBookState(); navigateTo(sourceWithDayBookReturnHint('/cash-vouchers?new=1')) } }
]

const rows = computed(() => result.value?.rows || [])
const summary = computed(() => result.value?.summary || {})
const totalPages = computed(() => Math.max(1, Math.ceil(Number(result.value?.total || 0) / Number(result.value?.pageSize || pageSize.value || 50))))
const dateCaption = computed(() => {
  const from = result.value?.from ? formatDate(result.value.from) : '-'
  const to = result.value?.to ? formatDate(result.value.to) : '-'
  return from === to ? from : `${from} to ${to}`
})


function isValidOption(value: unknown, options: { value: string | number }[]) {
  return options.some((option) => option.value === value)
}
function isInputDate(value: unknown): value is string {
  return typeof value === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(value)
}
function safePositiveInt(value: unknown, fallback: number) {
  const parsed = Number(value)
  return Number.isFinite(parsed) && parsed > 0 ? Math.floor(parsed) : fallback
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
  sessionStorage.setItem(DAY_BOOK_STATE_KEY, JSON.stringify(currentDayBookState()))
}

function sourceWithDayBookReturnHint(source: string, row?: any) {
  if (!source) return source
  const [pathAndQuery, hash = ''] = source.split('#')
  const [path, rawQuery = ''] = pathAndQuery.split('?')
  const params = new URLSearchParams(rawQuery)
  params.set('fromDayBook', '1')
  if (row?.id) params.set('dayBookSourceId', String(row.id))
  if (row?.documentType) params.set('dayBookSourceType', String(row.documentType))
  if (row?.onDate) params.set('dayBookDate', String(row.onDate).slice(0, 10))
  if (datePreset.value) params.set('dayBookPreset', datePreset.value)
  if (typeFilter.value) params.set('dayBookType', typeFilter.value)
  const query = params.toString()
  const anchor = hash || (row?.id ? `daybook-${String(row.documentType || 'source').toLowerCase()}-${row.id}` : '')
  return `${path}${query ? `?${query}` : ''}${anchor ? `#${anchor}` : ''}`
}
function restoreDayBookState() {
  if (!import.meta.client) return
  const raw = sessionStorage.getItem(DAY_BOOK_STATE_KEY)
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
    month.value = isValidOption(stored.month, monthOptions) ? Number(stored.month) : new Date().getMonth() + 1
    year.value = isValidOption(stored.year, yearOptions) ? Number(stored.year) : new Date().getFullYear()
    if (isInputDate(stored.selectedDate)) selectedDate.value = stored.selectedDate
    if (isInputDate(stored.fromDate)) fromDate.value = stored.fromDate
    if (isInputDate(stored.toDate)) toDate.value = stored.toDate
  } catch {
    sessionStorage.removeItem(DAY_BOOK_STATE_KEY)
  } finally {
    nextTick(() => { restoringState.value = false })
  }
}

async function refresh() {
  if (!auth.isAuthenticated.value) return
  persistDayBookState()
  loading.value = true
  try {
    const params = buildDayBookQuery(false)
    result.value = await api.get<any>(`day-book?${params.toString()}`)
  } catch (error) {
    feedback.failed('Day Book refresh failed', error)
  } finally {
    loading.value = false
  }
}


function buildDayBookQuery(exportMode = false) {
  const params = new URLSearchParams({
    datePreset: datePreset.value,
    type: typeFilter.value,
    page: exportMode ? '1' : String(page.value),
    pageSize: exportMode ? '5000' : String(pageSize.value)
  })
  if (search.value.trim()) params.set('q', search.value.trim())
  if (includeJournalEntries.value) params.set('includeJournal', 'true')
  if (datePreset.value === 'date') params.set('date', selectedDate.value)
  if (datePreset.value === 'month-year') {
    params.set('month', String(month.value))
    params.set('year', String(year.value))
  }
  if (datePreset.value === 'custom') {
    params.set('from', fromDate.value)
    params.set('to', toDate.value)
  }
  return params
}

async function downloadDayBookCsv() {
  if (!import.meta.client) return
  exportLoading.value = true
  try {
    persistDayBookState()
    const params = buildDayBookQuery(true)
    const blob = await $fetch<Blob>(`${config.public.apiBase}/day-book/export.csv?${params.toString()}`, {
      headers: api.authHeaders() as Record<string, string>,
      responseType: 'blob'
    })
    downloadBlob(blob, `garmetix-day-book-${dateCaption.value.replace(/[^0-9a-z-]+/gi, '-')}.csv`)
    feedback.notify('Day Book CSV downloaded')
  } catch (error) {
    feedback.failed('Day Book CSV export failed', error)
  } finally {
    exportLoading.value = false
  }
}

async function openDayBookPrint() {
  if (!import.meta.client) return
  printLoading.value = true
  try {
    persistDayBookState()
    const params = buildDayBookQuery(true)
    const url = `${config.public.apiBase}/day-book/print?${params.toString()}`
    const response = await fetch(url, { headers: api.authHeaders() as Record<string, string> })
    if (!response.ok) throw new Error(`Print view failed with HTTP ${response.status}`)
    const html = await response.text()
    const blob = new Blob([html], { type: 'text/html' })
    const objectUrl = URL.createObjectURL(blob)
    window.open(objectUrl, '_blank', 'noopener,noreferrer')
    setTimeout(() => URL.revokeObjectURL(objectUrl), 60_000)
  } catch (error) {
    feedback.failed('Day Book print view failed', error)
  } finally {
    printLoading.value = false
  }
}

function downloadBlob(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  document.body.appendChild(link)
  link.click()
  link.remove()
  URL.revokeObjectURL(url)
}

async function openDetail(row: any) {
  selectedDetail.value = null
  detailOpen.value = true
  detailLoading.value = true
  try {
    selectedDetail.value = await api.get<any>(row.detailApiPath)
  } catch (error) {
    feedback.failed('Could not open transaction detail', error)
  } finally {
    detailLoading.value = false
  }
}

function openSource(row?: any) {
  const source = row?.sourcePath || selectedDetail.value?.sourcePath
  const sourceRow = row || selectedDetail.value?.row
  persistDayBookState()
  if (source) navigateTo(sourceWithDayBookReturnHint(source, sourceRow))
}

function resetPageAndRefresh() {
  if (restoringState.value) return
  page.value = 1
  persistDayBookState()
  refresh()
}
function nextPage() { if (page.value < totalPages.value) { page.value += 1; persistDayBookState(); refresh() } }
function previousPage() { if (page.value > 1) { page.value -= 1; persistDayBookState(); refresh() } }
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
function formatDate(value: string) { return value ? new Date(value).toLocaleDateString('en-IN') : '-' }
function formatDateTime(value: string) { return value ? new Date(value).toLocaleString('en-IN') : '-' }
function money(value: number) { return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(Number(value || 0)) }
function toInputDate(value: Date) {
  const copy = new Date(value)
  copy.setMinutes(copy.getMinutes() - copy.getTimezoneOffset())
  return copy.toISOString().slice(0, 10)
}
function rowTone(row: any) {
  if (Number(row.netAmount || 0) > 0) return 'success'
  if (Number(row.netAmount || 0) < 0) return 'warning'
  return 'neutral'
}
const detailRows = computed(() => toKeyValueRows(selectedDetail.value?.detail))
const relatedLines = computed(() => Array.isArray(selectedDetail.value?.lines) ? selectedDetail.value.lines : [])
const relatedLineColumns = computed(() => tableColumns(relatedLines.value))

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
    .replace(/^./, (match) => match.toUpperCase())
}
function isIsoDateText(value: string) {
  return /^\d{4}-\d{2}-\d{2}(T|$)/.test(value) && !Number.isNaN(Date.parse(value))
}
function formatCell(value: any): string {
  if (value === null || value === undefined || value === '') return '-'
  if (typeof value === 'number') return Number.isFinite(value) ? String(value) : '-'
  if (typeof value === 'boolean') return value ? 'Yes' : 'No'
  if (value instanceof Date) return formatDateTime(value.toISOString())
  if (typeof value === 'string') return isIsoDateText(value) ? formatDateTime(value) : value
  if (Array.isArray(value)) {
    if (!value.length) return '-'
    if (value.every((item) => typeof item !== 'object' || item === null)) return value.map(formatCell).join(', ')
    return `${value.length} item${value.length === 1 ? '' : 's'}`
  }
  if (typeof value === 'object') {
    const entries = Object.entries(value).filter(([, itemValue]) => itemValue !== null && itemValue !== undefined && itemValue !== '')
    if (!entries.length) return '-'
    return entries.slice(0, 6).map(([key, itemValue]) => `${humanizeKey(key)}: ${formatCell(itemValue)}`).join(' · ')
  }
  return String(value)
}
function isHiddenDetailKey(key: string) {
  const normalized = String(key || '').toLowerCase()
  return hiddenDetailKeys.has(normalized) || normalized.endsWith('navigation') || normalized.endsWith('json')
}
function isRelevantScalar(value: any) {
  return value === null || value === undefined || ['string', 'number', 'boolean'].includes(typeof value) || value instanceof Date
}
function isRelevantDetailField(key: string, value: any) {
  if (isHiddenDetailKey(key)) return false
  if (!isRelevantScalar(value)) return false
  return key.toLowerCase() === 'id' || relevantKeyPattern.test(key)
}
function detailLabel(key: string) {
  return key.toLowerCase() === 'id' ? 'ID' : humanizeKey(key)
}
function toKeyValueRows(value: any) {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return []
  return Object.entries(value)
    .filter(([key, itemValue]) => isRelevantDetailField(key, itemValue))
    .map(([key, itemValue]) => ({ key, label: detailLabel(key), value: formatCell(itemValue) }))
}
function tableColumns(lines: any[]) {
  const keys = new Set<string>()
  for (const line of lines) {
    if (line && typeof line === 'object' && !Array.isArray(line)) {
      Object.keys(line).filter((key) => isRelevantDetailField(key, line[key])).forEach((key) => keys.add(key))
    } else keys.add('value')
  }
  return Array.from(keys).map((key) => ({ key, label: detailLabel(key) }))
}
function tableValue(line: any, key: string) {
  if (!line || typeof line !== 'object' || Array.isArray(line)) return key === 'value' ? formatCell(line) : '-'
  return formatCell(line[key])
}

watch([datePreset, typeFilter, includeJournalEntries, pageSize], resetPageAndRefresh)
watch(search, persistDayBookState)
watch(selectedDate, () => { if (datePreset.value === 'date') resetPageAndRefresh() })
watch([month, year, fromDate, toDate], () => { if (datePreset.value === 'month-year' || datePreset.value === 'custom') resetPageAndRefresh() })
onMounted(async () => { auth.restore(); restoreDayBookState(); await refresh() })
</script>

<template>
  <AuthScreen v-if="!isAuthenticated" @authenticated="refresh" />
  <AppShell v-else title="Day Book" @refresh="refresh">
    <section class="planner-dashboard space-y-5">
      <UiModulePageHeader
        title="Day Book"
        description="Tally-style date-wise transaction book. Select any sale, purchase, voucher, receipt, payment or journal row to open its details directly from this page."
        icon="i-lucide-book-open-check"
      >
        <template #actions>
          <UDropdownMenu :items="quickAddItems">
            <UButton icon="i-lucide-plus" label="New" trailing-icon="i-lucide-chevron-down" />
          </UDropdownMenu>
          <UButton icon="i-lucide-refresh-cw" label="Refresh" :loading="loading" variant="subtle" @click="refresh" />
        </template>
      </UiModulePageHeader>

      <div class="planner-metric-grid">
        <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-list" color="primary" variant="subtle"/><div><p>Transactions</p><strong>{{ summary.count || 0 }}</strong><span>{{ dateCaption }}</span></div></div></UCard>
        <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-arrow-down-left" color="success" variant="subtle"/><div><p>Credit / Inflow</p><strong>{{ money(summary.creditAmount || 0) }}</strong><span>Receipts and sales</span></div></div></UCard>
        <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-arrow-up-right" color="warning" variant="subtle"/><div><p>Debit / Outflow</p><strong>{{ money(summary.debitAmount || 0) }}</strong><span>Purchases and payments</span></div></div></UCard>
        <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-scale" color="neutral" variant="subtle"/><div><p>Net</p><strong>{{ money(summary.netAmount || 0) }}</strong><span>Credit - debit</span></div></div></UCard>
      </div>

      <UCard>
        <div class="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
          <div>
            <p class="text-xs uppercase text-muted">Final export/source opening polish</p>
            <h3 class="text-base font-semibold">Day Book evidence is ready for accountant review</h3>
            <p class="text-sm text-muted">Export the current filtered register to CSV/Excel-compatible evidence or open a print view for PDF. Source links preserve your date/type/search/page state when you return.</p>
          </div>
          <div class="flex flex-wrap gap-2">
            <UButton icon="i-lucide-file-spreadsheet" label="Export CSV" variant="subtle" :loading="exportLoading" @click="downloadDayBookCsv" />
            <UButton icon="i-lucide-printer" label="Print / PDF" variant="subtle" :loading="printLoading" @click="openDayBookPrint" />
          </div>
        </div>
        <div class="mt-3 grid gap-2 md:grid-cols-3">
          <UAlert color="success" variant="subtle" icon="i-lucide-check-circle" title="Filters remembered" description="Returning from source pages keeps the selected date, type, search and page." />
          <UAlert color="success" variant="subtle" icon="i-lucide-check-circle" title="Evidence export" description="CSV includes summary totals, IDs and source paths for audit cross-checking." />
          <UAlert color="warning" variant="subtle" icon="i-lucide-alert-triangle" title="Journal rows hidden by default" description="Use the checkbox or Journal type when accountant wants journal evidence." />
        </div>
      </UCard>

      <UCard>
        <div class="grid gap-3 md:grid-cols-8">
          <UFormField label="Book date">
            <div class="flex gap-2">
              <UButton label="− Previous" variant="subtle" title="Previous date" @click="moveDay(-1)" />
              <UInput v-model="selectedDate" type="date" class="min-w-40" @focus="datePreset = 'date'" />
            </div>
          </UFormField>
          <UFormField label="Period"><USelect v-model="datePreset" :items="datePresetOptions" /></UFormField>
          <UFormField label="Type"><USelect v-model="typeFilter" :items="typeOptions" /></UFormField>
          <UFormField label="Journal rows"><UCheckbox v-model="includeJournalEntries" label="Show journal entries" /></UFormField>
          <UFormField v-if="datePreset === 'month-year'" label="Month"><USelect v-model="month" :items="monthOptions" /></UFormField>
          <UFormField v-if="datePreset === 'month-year'" label="Year"><USelect v-model="year" :items="yearOptions" /></UFormField>
          <UFormField v-if="datePreset === 'custom'" label="From"><UInput v-model="fromDate" type="date" /></UFormField>
          <UFormField v-if="datePreset === 'custom'" label="To"><UInput v-model="toDate" type="date" /></UFormField>
          <UFormField label="Search"><UInput v-model="search" placeholder="Voucher, party, invoice" @keyup.enter="resetPageAndRefresh" /></UFormField>
          <UFormField label="Rows"><USelect v-model="pageSize" :items="pageSizeOptions" /></UFormField>
        </div>
        <div class="mt-3 flex flex-wrap items-center gap-2">
          <UButton icon="i-lucide-search" label="Apply" :loading="loading" @click="resetPageAndRefresh" />
          <UButton icon="i-lucide-calendar-days" label="Today" variant="subtle" @click="goToday" />
          <UButton label="+ Next" variant="subtle" title="Next date" @click="moveDay(1)" />
        </div>
      </UCard>

      <UCard>
        <div class="mb-3 flex flex-wrap items-center justify-between gap-2">
          <div>
            <h3 class="font-semibold">Transaction Register</h3>
            <p class="text-sm text-muted">{{ result.total || 0 }} rows · page {{ page }} of {{ totalPages }}</p>
          </div>
          <div class="flex flex-wrap gap-2">
            <UButton icon="i-lucide-file-spreadsheet" label="CSV" variant="subtle" :loading="exportLoading" @click="downloadDayBookCsv" />
            <UButton icon="i-lucide-printer" label="Print" variant="subtle" :loading="printLoading" @click="openDayBookPrint" />
            <UButton label="Previous" icon="i-lucide-chevron-left" variant="subtle" :disabled="page <= 1" @click="previousPage" />
            <UButton label="Next" icon="i-lucide-chevron-right" trailing variant="subtle" :disabled="page >= totalPages" @click="nextPage" />
          </div>
        </div>
        <div class="planner-table-wrap">
          <table class="min-w-full text-sm">
            <thead>
              <tr class="border-b text-left text-muted">
                <th class="p-2">Date</th>
                <th class="p-2">Type</th>
                <th class="p-2">No.</th>
                <th class="p-2">Party</th>
                <th class="p-2">Particulars</th>
                <th class="p-2 text-right">Debit</th>
                <th class="p-2 text-right">Credit</th>
                <th class="p-2">Mode</th>
                <th class="p-2">Status</th>
                <th class="p-2"></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="row in rows" :key="`${row.documentType}-${row.id}`" class="border-b hover:bg-muted/40">
                <td class="p-2 whitespace-nowrap">{{ formatDate(row.onDate) }}</td>
                <td class="p-2"><UBadge :color="rowTone(row)" variant="subtle">{{ row.documentSubType || row.documentType }}</UBadge></td>
                <td class="p-2 font-medium">{{ row.documentNumber }}</td>
                <td class="p-2">{{ row.partyName }}</td>
                <td class="p-2 max-w-md truncate">{{ row.particulars }}</td>
                <td class="p-2 text-right">{{ row.debitAmount ? money(row.debitAmount) : '-' }}</td>
                <td class="p-2 text-right">{{ row.creditAmount ? money(row.creditAmount) : '-' }}</td>
                <td class="p-2">{{ row.paymentMode }}</td>
                <td class="p-2">{{ row.status }}</td>
                <td class="p-2 whitespace-nowrap">
                  <UButton size="xs" icon="i-lucide-eye" label="Open" variant="ghost" @click="openDetail(row)" />
                  <UButton size="xs" icon="i-lucide-external-link" variant="ghost" @click="openSource(row)" />
                </td>
              </tr>
            </tbody>
          </table>
        </div>
        <div v-if="!rows.length && !loading" class="mt-3 space-y-2">
          <UAlert color="neutral" variant="subtle" icon="i-lucide-info" title="No day-book rows" description="Change period, type or search filter, or create the first transaction for this date." />
          <UDropdownMenu :items="quickAddItems">
            <UButton icon="i-lucide-plus" label="Create transaction" variant="subtle" />
          </UDropdownMenu>
        </div>
      </UCard>

      <USlideover v-model:open="detailOpen" :ui="{ content: 'w-full sm:max-w-4xl lg:max-w-6xl xl:max-w-7xl' }">
        <template #content>
          <div class="space-y-4 p-4">
            <div class="flex items-start justify-between gap-3">
              <div>
                <p class="text-xs uppercase text-muted">Day Book Detail</p>
                <h2 class="text-lg font-semibold">{{ selectedDetail?.row?.documentNumber || 'Transaction' }}</h2>
                <p class="text-sm text-muted">{{ selectedDetail?.row?.documentSubType }} · {{ selectedDetail?.row?.partyName }}</p>
                <p v-if="selectedDetail?.row?.id" class="text-xs text-muted">ID: <span class="font-mono">{{ selectedDetail.row.id }}</span></p>
              </div>
              <UButton icon="i-lucide-x" variant="ghost" @click="detailOpen = false" />
            </div>
            <UAlert v-if="detailLoading" color="neutral" title="Loading transaction detail" />
            <template v-else-if="selectedDetail">
              <div class="grid gap-3 md:grid-cols-3">
                <UCard><p class="text-xs text-muted">ID</p><strong class="break-all font-mono text-xs">{{ selectedDetail.row.id }}</strong></UCard>
                <UCard><p class="text-xs text-muted">Date</p><strong>{{ formatDateTime(selectedDetail.row.onDate) }}</strong></UCard>
                <UCard><p class="text-xs text-muted">Amount</p><strong>{{ money(Math.abs(selectedDetail.row.netAmount || selectedDetail.row.creditAmount || selectedDetail.row.debitAmount || 0)) }}</strong></UCard>
                <UCard><p class="text-xs text-muted">Payment mode</p><strong>{{ selectedDetail.row.paymentMode }}</strong></UCard>
                <UCard><p class="text-xs text-muted">Status</p><strong>{{ selectedDetail.row.status }}</strong></UCard>
              </div>
              <UCard>
                <p class="text-xs text-muted">Particulars</p>
                <p>{{ selectedDetail.row.particulars }}</p>
              </UCard>
              <UCard v-if="relatedLines.length">
                <h3 class="font-semibold">Related lines</h3>
                <div class="planner-table-wrap mt-2 max-h-72 overflow-auto">
                  <table class="min-w-full text-xs">
                    <thead>
                      <tr class="border-b text-left text-muted">
                        <th v-for="column in relatedLineColumns" :key="column.key" class="p-2">{{ column.label }}</th>
                      </tr>
                    </thead>
                    <tbody>
                      <tr v-for="(line, index) in relatedLines" :key="index" class="border-b">
                        <td v-for="column in relatedLineColumns" :key="column.key" class="p-2 align-top">{{ tableValue(line, column.key) }}</td>
                      </tr>
                    </tbody>
                  </table>
                </div>
              </UCard>
              <UCard v-if="detailRows.length">
                <h3 class="font-semibold">Transaction details</h3>
                <div class="planner-table-wrap mt-2 max-h-72 overflow-auto">
                  <table class="min-w-full text-sm">
                    <tbody>
                      <tr v-for="item in detailRows" :key="item.key" class="border-b">
                        <th class="w-48 p-2 text-left text-muted align-top">{{ item.label }}</th>
                        <td class="p-2 align-top">{{ item.value }}</td>
                      </tr>
                    </tbody>
                  </table>
                </div>
              </UCard>
              <div class="flex justify-end gap-2">
                <UButton variant="subtle" label="Close" @click="detailOpen = false" />
                <UButton icon="i-lucide-external-link" :label="selectedDetail.openActionLabel || 'Open Source'" @click="openSource(selectedDetail.row)" />
              </div>
            </template>
          </div>
        </template>
      </USlideover>
    </section>
  </AppShell>
</template>
