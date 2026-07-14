<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 xl:flex-row xl:items-start xl:justify-between">
        <div>
          <p class="garmetix-dashboard-kicker">
            <UIcon name="i-lucide-receipt-text" class="size-4" />
            GST & Taxes
          </p>
          <h2 class="garmetix-dashboard-title">Purchase GST Review</h2>
          <p class="garmetix-dashboard-subtitle">
            Resolves each purchase line's GST rate against the configurable Rate Master (Stage GST-5, incl. garment slabs), cross-linked
            to open GST Audit findings (Stage GST-6), plus per-invoice Input Tax Credit (ITC) eligibility warnings based on vendor GSTIN
            presence and verification status. Read-only - no accounting mutation happens here.
          </p>
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
        <UFormField label="Search"><UInput v-model="filters.search" placeholder="Invoice, vendor, barcode, product" @keyup.enter="refresh(true)" /></UFormField>
        <UFormField label="Rows"><USelect v-model="filters.pageSize" :items="pageSizeItems" /></UFormField>
        <div class="flex items-end gap-2">
          <UCheckbox v-model="filters.onlyIssues" label="Only issues" />
          <UButton icon="i-lucide-search" :loading="loading" label="Apply" @click="refresh(true)" />
        </div>
      </div>
    </section>

    <section class="garmetix-section-card">
      <div class="mb-3">
        <h3 class="garmetix-panel-title">Summary</h3>
        <p class="text-xs text-muted">
          ITC eligibility is a heuristic (no vendor GSTIN -> Not Eligible; vendor registration on record as not Active -> At Risk;
          GSTIN present but never confirmed via GSTIN Verification -> Unverified) - always confirm with your accountant before filing.
        </p>
      </div>

      <div class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
        <div v-for="card in summaryCards" :key="card.label" class="garmetix-metric-card">
          <p class="garmetix-metric-label">{{ card.label }}</p>
          <p class="garmetix-metric-value">{{ card.value }}</p>
          <p class="garmetix-metric-caption">{{ card.detail }}</p>
        </div>
      </div>
      <div class="mt-3 grid gap-3 md:grid-cols-3">
        <div v-for="card in itcCards" :key="card.label" class="garmetix-metric-card">
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
              <th class="whitespace-nowrap px-3 py-2 text-right font-medium">Stored tax</th>
              <th class="whitespace-nowrap px-3 py-2 text-right font-medium">Resolved tax</th>
              <th class="whitespace-nowrap px-3 py-2 text-right font-medium">Issues</th>
              <th class="whitespace-nowrap px-3 py-2 font-medium">Open findings</th>
              <th class="whitespace-nowrap px-3 py-2 font-medium">Status</th>
              <th class="whitespace-nowrap px-3 py-2 font-medium">ITC</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-default">
            <tr v-for="invoice in report.invoices" :key="invoice.purchaseInvoiceId">
              <td class="whitespace-nowrap px-3 py-2 font-medium text-primary">
                {{ invoice.invoiceNumber }}
                <div class="text-xs text-muted">{{ invoice.inwardNumber }}</div>
              </td>
              <td class="whitespace-nowrap px-3 py-2">{{ formatDate(invoice.onDate) }}</td>
              <td class="px-3 py-2">
                <div>{{ invoice.vendorName }}</div>
                <div class="text-xs text-muted">{{ invoice.vendorGSTIN || 'No GSTIN' }}</div>
              </td>
              <td class="whitespace-nowrap px-3 py-2 text-right">{{ money(invoice.storedTaxAmount) }}</td>
              <td class="whitespace-nowrap px-3 py-2 text-right">{{ money(invoice.resolvedTaxAmount) }}</td>
              <td class="whitespace-nowrap px-3 py-2 text-right">{{ invoice.issueCount }} / {{ invoice.lineCount }}</td>
              <td class="whitespace-nowrap px-3 py-2">
                <NuxtLink v-if="invoice.openAuditFindingCount" class="text-primary underline" to="/gst-tax-audit">{{ invoice.openAuditFindingCount }} open</NuxtLink>
                <span v-else class="text-xs text-muted">None</span>
              </td>
              <td class="whitespace-nowrap px-3 py-2"><UBadge :color="invoice.issueCount ? 'warning' : 'success'" variant="subtle">{{ invoice.status }}</UBadge></td>
              <td class="whitespace-nowrap px-3 py-2">
                <UBadge :color="itcStatusColor(invoice.itcStatus)" variant="subtle">{{ invoice.itcStatus }}</UBadge>
                <div class="mt-1 max-w-[220px] text-xs text-muted">{{ invoice.itcNote }}</div>
              </td>
            </tr>
            <tr v-if="!report.invoices.length">
              <td colspan="9" class="px-3 py-6 text-center text-sm text-muted">No purchase invoices matched the selected filters.</td>
            </tr>
          </tbody>
        </table>
      </div>

      <div v-if="report" class="mt-4 overflow-x-auto rounded-lg border border-default">
        <table class="w-full min-w-[1040px] text-left text-sm">
          <thead class="bg-muted/30 text-xs uppercase text-muted">
            <tr>
              <th class="whitespace-nowrap px-3 py-2 font-medium">Invoice / Item</th>
              <th class="whitespace-nowrap px-3 py-2 font-medium">HSN / Category</th>
              <th class="whitespace-nowrap px-3 py-2 text-right font-medium">Stored %</th>
              <th class="whitespace-nowrap px-3 py-2 text-right font-medium">Resolved %</th>
              <th class="whitespace-nowrap px-3 py-2 text-right font-medium">Tax diff.</th>
              <th class="whitespace-nowrap px-3 py-2 font-medium">Rule</th>
              <th class="whitespace-nowrap px-3 py-2 font-medium">Status</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-default">
            <tr v-for="line in report.lines" :key="line.purchaseInvoiceItemId">
              <td class="px-3 py-2">
                <div class="font-medium text-primary">{{ line.invoiceNumber }}</div>
                <div class="text-sm">{{ line.productName }}</div>
                <div class="text-xs text-muted">{{ line.barcode }}</div>
              </td>
              <td class="whitespace-nowrap px-3 py-2">
                <div>{{ line.hsnCode || '-' }}</div>
                <div class="text-xs text-muted">
                  {{ line.productCategory || '-' }}
                  <UBadge v-if="line.isGarmentThreshold" color="primary" variant="subtle" size="xs">Garment threshold</UBadge>
                </div>
              </td>
              <td class="whitespace-nowrap px-3 py-2 text-right">{{ num(line.storedTaxPercentage) }}%</td>
              <td class="whitespace-nowrap px-3 py-2 text-right">{{ num(line.resolvedTaxPercentage) }}%</td>
              <td class="whitespace-nowrap px-3 py-2 text-right">{{ money(line.taxDifferenceAmount) }}</td>
              <td class="whitespace-nowrap px-3 py-2 text-xs text-muted">{{ line.ruleName || line.rateSource || '-' }}</td>
              <td class="whitespace-nowrap px-3 py-2"><UBadge :color="rateStatusColor(line.status)" variant="subtle">{{ line.status }}</UBadge></td>
            </tr>
            <tr v-if="!report.lines.length">
              <td colspan="7" class="px-3 py-6 text-center text-sm text-muted">No purchase line rows matched the current filter.</td>
            </tr>
          </tbody>
        </table>
      </div>
      <div v-if="report" class="mt-3 flex items-center justify-between">
        <UButton icon="i-lucide-chevron-left" size="sm" variant="subtle" label="Previous" :disabled="filters.page <= 1" @click="changePage(filters.page - 1)" />
        <span class="text-sm text-muted">Page {{ filters.page }} / {{ totalPages }} - {{ report.totalLines }} line row(s)</span>
        <UButton trailing-icon="i-lucide-chevron-right" size="sm" variant="subtle" label="Next" :disabled="filters.page >= totalPages" @click="changePage(filters.page + 1)" />
      </div>
    </section>
  </section>
</template>

<script setup lang="ts">
import { formatDate, toRows, useBooksApiClient, type ApiRecord } from '../utils/books-api'

useHead({ title: 'Purchase GST Review - Garmetix Books' })

const ALL_STORES_VALUE = '__ALL_STORES__'
const ALL_VENDORS_VALUE = '__ALL_VENDORS__'

interface ReviewInvoice {
  purchaseInvoiceId: string
  invoiceNumber: string
  inwardNumber: string
  onDate: string
  storeName: string
  vendorId: string
  vendorName: string
  vendorGSTIN: string | null
  interState: boolean
  lineCount: number
  issueCount: number
  openAuditFindingCount: number
  storedTaxAmount: number
  resolvedTaxAmount: number
  taxDifferenceAmount: number
  status: string
  itcStatus: string
  itcNote: string
}

interface ReviewLine {
  purchaseInvoiceId: string
  purchaseInvoiceItemId: string
  invoiceNumber: string
  productName: string
  barcode: string
  hsnCode: string | null
  productCategory: string | null
  storedTaxPercentage: number
  resolvedTaxPercentage: number
  taxDifferenceAmount: number
  rateSource: string | null
  ruleName: string | null
  isGarmentThreshold: boolean
  status: string
}

interface ReviewReport {
  totalLines: number
  summary: ApiRecord
  invoices: ReviewInvoice[]
  lines: ReviewLine[]
}

const { get } = useBooksApiClient()

const loading = ref(false)
const error = ref('')
const stores = ref<ApiRecord[]>([])
const vendors = ref<ApiRecord[]>([])
const report = ref<ReviewReport | null>(null)

const filters = reactive({
  fromDate: new Date(Date.now() - 29 * 24 * 60 * 60 * 1000).toISOString().slice(0, 10),
  toDate: new Date().toISOString().slice(0, 10),
  storeId: ALL_STORES_VALUE,
  vendorId: ALL_VENDORS_VALUE,
  search: '',
  onlyIssues: true,
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
    { label: 'Invoices', value: String(row.invoiceCount ?? 0), detail: `${row.lineCount ?? 0} line(s) reviewed` },
    { label: 'Rate mismatches', value: String(row.mismatchCount ?? 0), detail: 'Stored % differs from resolved %' },
    { label: 'Not configured', value: String(row.notConfiguredCount ?? 0), detail: 'No rate rule/HSN default matched' },
    { label: 'Open audit findings', value: String(row.openAuditFindingCount ?? 0), detail: 'From the GST Audit Engine' }
  ]
})

const itcCards = computed(() => {
  const row = (report.value?.summary || {}) as ApiRecord
  return [
    { label: 'ITC not eligible', value: String(row.itcNotEligibleCount ?? 0), detail: 'No vendor GSTIN on the invoice' },
    { label: 'ITC at risk', value: String(row.itcAtRiskCount ?? 0), detail: 'Vendor GST registration not Active' },
    { label: 'ITC unverified', value: String(row.itcUnverifiedCount ?? 0), detail: 'GSTIN never confirmed via GSTIN Verification' }
  ]
})

const totalPages = computed(() => Math.max(1, Math.ceil((report.value?.totalLines || 0) / filters.pageSize)))

function money(value: unknown) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(Number(value || 0))
}

function num(value: unknown) {
  return new Intl.NumberFormat('en-IN', { maximumFractionDigits: 2 }).format(Number(value || 0))
}

function rateStatusColor(status: string) {
  if (status === 'OK') return 'success'
  if (status === 'Rate Mismatch') return 'warning'
  return 'neutral'
}

function itcStatusColor(status: string) {
  if (status === 'Eligible') return 'success'
  if (status === 'Unverified') return 'warning'
  return 'error'
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
    const query = {
      fromDate: filters.fromDate,
      toDate: filters.toDate,
      onlyIssues: filters.onlyIssues,
      page: filters.page,
      pageSize: filters.pageSize,
      storeId: filters.storeId !== ALL_STORES_VALUE ? filters.storeId : undefined,
      vendorId: filters.vendorId !== ALL_VENDORS_VALUE ? filters.vendorId : undefined,
      search: filters.search.trim() || undefined
    }
    report.value = await get<ReviewReport>('/gst/purchase-review', query as Record<string, string | number | boolean | null | undefined>)
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to load Purchase GST Review.'
  } finally {
    loading.value = false
  }
}

function changePage(page: number) {
  filters.page = Math.max(1, page)
  refresh()
}

onMounted(async () => {
  await Promise.all([loadStores(), loadVendors()])
  await refresh(true)
})
</script>
