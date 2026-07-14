<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 xl:flex-row xl:items-start xl:justify-between">
        <div>
          <p class="garmetix-dashboard-kicker">
            <UIcon name="i-lucide-scroll-text" class="size-4" />
            GST & Taxes
          </p>
          <h2 class="garmetix-dashboard-title">ITC Register</h2>
          <p class="garmetix-dashboard-subtitle">
            An auto-computed Input Tax Credit register built from posted Purchase invoices, using the same ITC eligibility check as
            Purchase GST Review. The existing GSTR-3B builder's "4 ITC" section is manual entry - use this register as a source to
            cross-check those numbers against, not a replacement for it.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton icon="i-lucide-download" color="neutral" variant="soft" :loading="downloading === 'csv'" @click="downloadCsv">Export CSV</UButton>
          <UButton icon="i-lucide-file-spreadsheet" color="primary" variant="soft" :loading="downloading === 'excel'" @click="downloadExcel">CA Excel Export</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />

    <section class="garmetix-section-card">
      <h3 class="garmetix-panel-title mb-3">Filters</h3>
      <div class="grid gap-3 lg:grid-cols-[160px_160px_220px_220px_1fr_140px_auto]">
        <UFormField label="From"><UInput v-model="filters.fromDate" type="date" /></UFormField>
        <UFormField label="To"><UInput v-model="filters.toDate" type="date" /></UFormField>
        <UFormField label="Store">
          <USelect v-model="filters.storeId" :items="storeOptions" />
        </UFormField>
        <UFormField label="Vendor">
          <USelect v-model="filters.vendorId" :items="vendorOptions" />
        </UFormField>
        <UFormField label="Search"><UInput v-model="filters.search" placeholder="Invoice, vendor" @keyup.enter="refresh(true)" /></UFormField>
        <UFormField label="Rows"><USelect v-model="filters.pageSize" :items="pageSizeItems" /></UFormField>
        <div class="flex items-end gap-2">
          <UButton icon="i-lucide-search" :loading="loading" label="Apply" @click="refresh(true)" />
        </div>
      </div>
    </section>

    <section class="garmetix-section-card">
      <div class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
        <div v-for="card in summaryCards" :key="card.label" class="garmetix-metric-card">
          <p class="garmetix-metric-label">{{ card.label }}</p>
          <p class="garmetix-metric-value">{{ card.value }}</p>
          <p class="garmetix-metric-caption">{{ card.detail }}</p>
        </div>
      </div>
      <div class="mt-3 grid gap-3 md:grid-cols-4">
        <div v-for="card in statusCards" :key="card.label" class="garmetix-metric-card">
          <p class="garmetix-metric-label">{{ card.label }}</p>
          <p class="garmetix-metric-value">{{ card.value }}</p>
          <p class="garmetix-metric-caption">{{ card.detail }}</p>
        </div>
      </div>

      <div v-if="report" class="mt-4 overflow-x-auto rounded-lg border border-default">
        <table class="w-full min-w-[1080px] text-left text-sm">
          <thead class="bg-muted/30 text-xs uppercase text-muted">
            <tr>
              <th class="whitespace-nowrap px-3 py-2 font-medium">Invoice</th>
              <th class="whitespace-nowrap px-3 py-2 font-medium">Date</th>
              <th class="whitespace-nowrap px-3 py-2 font-medium">Vendor</th>
              <th class="whitespace-nowrap px-3 py-2 text-right font-medium">Taxable</th>
              <th class="whitespace-nowrap px-3 py-2 text-right font-medium">CGST</th>
              <th class="whitespace-nowrap px-3 py-2 text-right font-medium">SGST</th>
              <th class="whitespace-nowrap px-3 py-2 text-right font-medium">IGST</th>
              <th class="whitespace-nowrap px-3 py-2 text-right font-medium">Tax</th>
              <th class="whitespace-nowrap px-3 py-2 font-medium">ITC Status</th>
              <th class="whitespace-nowrap px-3 py-2 text-right font-medium">Eligible ITC</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-default">
            <tr v-for="row in report.rows" :key="row.purchaseInvoiceId">
              <td class="whitespace-nowrap px-3 py-2 font-medium text-primary">
                {{ row.invoiceNumber }}
                <div class="text-xs text-muted">{{ row.inwardNumber }}</div>
              </td>
              <td class="whitespace-nowrap px-3 py-2">{{ formatDate(row.onDate) }}</td>
              <td class="px-3 py-2">
                <div>{{ row.vendorName }}</div>
                <div class="text-xs text-muted">{{ row.vendorGSTIN || 'No GSTIN' }}</div>
              </td>
              <td class="whitespace-nowrap px-3 py-2 text-right">{{ money(row.taxableValue) }}</td>
              <td class="whitespace-nowrap px-3 py-2 text-right">{{ money(row.cgstAmount) }}</td>
              <td class="whitespace-nowrap px-3 py-2 text-right">{{ money(row.sgstAmount) }}</td>
              <td class="whitespace-nowrap px-3 py-2 text-right">{{ money(row.igstAmount) }}</td>
              <td class="whitespace-nowrap px-3 py-2 text-right">{{ money(row.taxAmount) }}</td>
              <td class="whitespace-nowrap px-3 py-2">
                <UBadge :color="itcStatusColor(row.itcStatus)" variant="subtle">{{ row.itcStatus }}</UBadge>
                <div class="mt-1 max-w-[220px] text-xs text-muted">{{ row.itcNote }}</div>
              </td>
              <td class="whitespace-nowrap px-3 py-2 text-right font-semibold" :class="row.eligibleItcAmount > 0 ? 'text-success' : 'text-muted'">{{ money(row.eligibleItcAmount) }}</td>
            </tr>
            <tr v-if="!report.rows.length">
              <td colspan="10" class="px-3 py-6 text-center text-sm text-muted">No purchase invoices matched the selected filters.</td>
            </tr>
          </tbody>
        </table>
      </div>
      <div v-if="report" class="mt-3 flex items-center justify-between">
        <UButton icon="i-lucide-chevron-left" size="sm" variant="subtle" label="Previous" :disabled="filters.page <= 1" @click="changePage(filters.page - 1)" />
        <span class="text-sm text-muted">Page {{ filters.page }} / {{ totalPages }} - {{ report.totalRows }} invoice(s)</span>
        <UButton trailing-icon="i-lucide-chevron-right" size="sm" variant="subtle" label="Next" :disabled="filters.page >= totalPages" @click="changePage(filters.page + 1)" />
      </div>
    </section>
  </section>
</template>

<script setup lang="ts">
import { formatDate, toRows, useBooksApiClient, type ApiRecord } from '../utils/books-api'

useHead({ title: 'ITC Register - Garmetix Books' })

const ALL_STORES_VALUE = '__ALL_STORES__'
const ALL_VENDORS_VALUE = '__ALL_VENDORS__'

interface RegisterRow {
  purchaseInvoiceId: string
  invoiceNumber: string
  inwardNumber: string
  onDate: string
  vendorName: string
  vendorGSTIN: string | null
  interState: boolean
  taxableValue: number
  cgstAmount: number
  sgstAmount: number
  igstAmount: number
  taxAmount: number
  itcStatus: string
  itcNote: string
  eligibleItcAmount: number
}

interface RegisterReport {
  totalRows: number
  summary: ApiRecord
  rows: RegisterRow[]
}

const { get, download } = useBooksApiClient()

const loading = ref(false)
const downloading = ref<'csv' | 'excel' | null>(null)
const error = ref('')
const stores = ref<ApiRecord[]>([])
const vendors = ref<ApiRecord[]>([])
const report = ref<RegisterReport | null>(null)

const filters = reactive({
  fromDate: new Date(Date.now() - 29 * 24 * 60 * 60 * 1000).toISOString().slice(0, 10),
  toDate: new Date().toISOString().slice(0, 10),
  storeId: ALL_STORES_VALUE,
  vendorId: ALL_VENDORS_VALUE,
  search: '',
  page: 1,
  pageSize: 100
})

const pageSizeItems = [
  { label: '50', value: 50 },
  { label: '100', value: 100 },
  { label: '250', value: 250 },
  { label: '500', value: 500 }
]

const storeOptions = computed(() => [
  { label: 'All stores', value: ALL_STORES_VALUE },
  ...stores.value.map((item) => ({ label: `${item.name || item.storeCode || 'Store'}`, value: String(item.id) }))
])

const vendorOptions = computed(() => [
  { label: 'All vendors', value: ALL_VENDORS_VALUE },
  ...vendors.value.map((item) => ({ label: `${item.name || 'Vendor'}`, value: String(item.id) }))
])

const summaryCards = computed(() => {
  const row = (report.value?.summary || {}) as ApiRecord
  return [
    { label: 'Invoices', value: String(row.invoiceCount ?? 0), detail: 'In the selected range' },
    { label: 'Total tax', value: money(row.totalTaxAmount), detail: `On ${money(row.totalTaxableValue)} taxable` },
    { label: 'Eligible ITC', value: money(row.totalEligibleItcAmount), detail: 'Claimable per this heuristic' },
    { label: 'Ineligible ITC', value: money(row.totalIneligibleItcAmount), detail: 'At risk or not eligible' }
  ]
})

const statusCards = computed(() => {
  const row = (report.value?.summary || {}) as ApiRecord
  return [
    { label: 'Eligible', value: String(row.eligibleCount ?? 0), detail: 'GSTIN present and verified' },
    { label: 'Unverified', value: String(row.unverifiedCount ?? 0), detail: 'GSTIN present, never verified' },
    { label: 'At risk', value: String(row.atRiskCount ?? 0), detail: 'Vendor GST registration not Active' },
    { label: 'Not eligible', value: String(row.notEligibleCount ?? 0), detail: 'No vendor GSTIN on the invoice' }
  ]
})

const totalPages = computed(() => Math.max(1, Math.ceil((report.value?.totalRows || 0) / filters.pageSize)))

function money(value: unknown) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(Number(value || 0))
}

function itcStatusColor(status: string) {
  if (status === 'Eligible') return 'success'
  if (status === 'Unverified') return 'warning'
  return 'error'
}

function buildQuery() {
  return {
    fromDate: filters.fromDate,
    toDate: filters.toDate,
    storeId: filters.storeId !== ALL_STORES_VALUE ? filters.storeId : undefined,
    vendorId: filters.vendorId !== ALL_VENDORS_VALUE ? filters.vendorId : undefined,
    search: filters.search.trim() || undefined
  }
}

async function loadStores() {
  try {
    stores.value = toRows(await get<unknown>('stores'))
  } catch {
    stores.value = []
  }
}

async function loadVendors() {
  try {
    vendors.value = toRows(await get<unknown>('vendors'))
  } catch {
    vendors.value = []
  }
}

async function refresh(resetPage = false) {
  if (resetPage) filters.page = 1
  loading.value = true
  error.value = ''
  try {
    const query = { ...buildQuery(), page: filters.page, pageSize: filters.pageSize }
    report.value = await get<RegisterReport>('/gst/itc-register', query as Record<string, string | number | boolean | null | undefined>)
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to load the ITC Register.'
  } finally {
    loading.value = false
  }
}

function changePage(page: number) {
  filters.page = Math.max(1, page)
  refresh()
}

async function downloadCsv() {
  downloading.value = 'csv'
  error.value = ''
  try {
    await download('/gst/itc-register/csv', buildQuery() as Record<string, string | number | boolean | null | undefined>, `itc-register-${filters.fromDate}-to-${filters.toDate}.csv`)
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'CSV export failed.'
  } finally {
    downloading.value = null
  }
}

async function downloadExcel() {
  downloading.value = 'excel'
  error.value = ''
  try {
    await download('/gst/itc-register/excel', buildQuery() as Record<string, string | number | boolean | null | undefined>, `itc-register-${filters.fromDate}-to-${filters.toDate}.xlsx`)
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Excel export failed.'
  } finally {
    downloading.value = null
  }
}

onMounted(async () => {
  await Promise.all([loadStores(), loadVendors()])
  await refresh(true)
})
</script>
