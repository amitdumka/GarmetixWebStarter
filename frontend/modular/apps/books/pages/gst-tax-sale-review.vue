<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 xl:flex-row xl:items-start xl:justify-between">
        <div>
          <p class="garmetix-dashboard-kicker">
            <UIcon name="i-lucide-receipt-text" class="size-4" />
            GST & Taxes
          </p>
          <h2 class="garmetix-dashboard-title">Sale GST Review</h2>
          <p class="garmetix-dashboard-subtitle">
            Two GST checks over the same sale invoices: the apparel threshold review (5% up to ₹2,499 basic value per item, 18% above)
            with its Option B correction/posting workflow, and a configurable Rate Master line check driven by the GST Rate Master
            (Stage GST-5) and cross-linked to open GST Audit findings (Stage GST-6). Confirm final corrections with your accountant
            before posting adjustments.
          </p>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />
    <UAlert v-if="message" color="success" variant="subtle" icon="i-lucide-circle-check" :description="message" />

    <section class="garmetix-section-card">
      <h3 class="garmetix-panel-title mb-3">Filters</h3>
      <div class="grid gap-3 lg:grid-cols-[160px_160px_220px_1fr_140px_auto]">
        <UFormField label="From"><UInput v-model="filters.fromDate" type="date" /></UFormField>
        <UFormField label="To"><UInput v-model="filters.toDate" type="date" /></UFormField>
        <UFormField label="Store">
          <USelect v-model="filters.storeId" :items="storeOptions" />
        </UFormField>
        <UFormField label="Search"><UInput v-model="filters.search" placeholder="Invoice, customer, barcode, product" @keyup.enter="refresh(true)" /></UFormField>
        <UFormField label="Rows"><USelect v-model="filters.pageSize" :items="pageSizeItems" /></UFormField>
        <div class="flex items-end gap-2">
          <UCheckbox v-model="filters.onlyIssues" label="Only issues" />
          <UButton icon="i-lucide-search" :loading="loading" label="Apply" @click="refresh(true)" />
        </div>
      </div>
    </section>

    <!-- Section A: legacy-parity apparel threshold review -->
    <section class="garmetix-section-card">
      <div class="mb-3 flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h3 class="garmetix-panel-title">Apparel Threshold Review</h3>
          <p class="text-xs text-muted">5% applies up to ₹2,499 basic value per item; 18% applies from ₹2,499.01 and above. Uses the existing Sale Review engine (unchanged).</p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton size="sm" color="neutral" variant="soft" icon="i-lucide-copy" @click="copyIssueSummary">Copy issue summary</UButton>
          <UButton size="sm" color="neutral" variant="soft" icon="i-lucide-download" @click="exportThresholdCsv">Export CSV</UButton>
        </div>
      </div>

      <div class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
        <div v-for="card in thresholdCards" :key="card.label" class="garmetix-metric-card">
          <p class="garmetix-metric-label">{{ card.label }}</p>
          <p class="garmetix-metric-value">{{ card.value }}</p>
          <p class="garmetix-metric-caption">{{ card.detail }}</p>
        </div>
      </div>

      <div v-if="thresholdReport" class="mt-4 overflow-x-auto rounded-lg border border-default">
        <table class="w-full min-w-[960px] text-left text-sm">
          <thead class="bg-muted/30 text-xs uppercase text-muted">
            <tr>
              <th class="whitespace-nowrap px-3 py-2 font-medium">Invoice</th>
              <th class="whitespace-nowrap px-3 py-2 font-medium">Date</th>
              <th class="whitespace-nowrap px-3 py-2 font-medium">Customer</th>
              <th class="whitespace-nowrap px-3 py-2 text-right font-medium">Bill</th>
              <th class="whitespace-nowrap px-3 py-2 text-right font-medium">Tax diff.</th>
              <th class="whitespace-nowrap px-3 py-2 text-right font-medium">Extra</th>
              <th class="whitespace-nowrap px-3 py-2 text-right font-medium">Issues</th>
              <th class="whitespace-nowrap px-3 py-2 font-medium">Status</th>
              <th class="whitespace-nowrap px-3 py-2 font-medium">Action</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-default">
            <tr v-for="invoice in thresholdReport.invoices" :key="invoice.invoiceId">
              <td class="whitespace-nowrap px-3 py-2 font-medium text-primary">{{ invoice.invoiceNumber }}</td>
              <td class="whitespace-nowrap px-3 py-2">{{ formatDate(invoice.onDate) }}</td>
              <td class="px-3 py-2">
                <div>{{ invoice.customerName }}</div>
                <div class="text-xs text-muted">{{ invoice.customerMobileNumber || '-' }}</div>
              </td>
              <td class="whitespace-nowrap px-3 py-2 text-right">{{ money(invoice.billAmount) }}</td>
              <td class="whitespace-nowrap px-3 py-2 text-right">{{ money(invoice.taxDifferenceAmount) }}</td>
              <td class="whitespace-nowrap px-3 py-2 text-right font-semibold text-warning">{{ money(invoice.extraAmountToReview) }}</td>
              <td class="whitespace-nowrap px-3 py-2 text-right">{{ invoice.issueCount }}</td>
              <td class="whitespace-nowrap px-3 py-2"><UBadge :color="invoice.issueCount ? 'warning' : 'success'" variant="subtle">{{ invoice.status }}</UBadge></td>
              <td class="whitespace-nowrap px-3 py-2">
                <UButton
                  v-if="invoice.issueCount && Number(invoice.extraAmountToReview || 0) > 0"
                  size="xs"
                  color="warning"
                  variant="subtle"
                  icon="i-lucide-wrench"
                  :loading="isApplying(invoice.invoiceId)"
                  @click="applyOptionB(invoice)"
                >Apply Option B</UButton>
                <span v-else class="text-xs text-muted">No action</span>
              </td>
            </tr>
            <tr v-if="!thresholdReport.invoices.length">
              <td colspan="9" class="px-3 py-6 text-center text-sm text-muted">No invoices matched the selected filters.</td>
            </tr>
          </tbody>
        </table>
      </div>

      <div v-if="thresholdReport" class="mt-4 overflow-x-auto rounded-lg border border-default">
        <table class="w-full min-w-[1000px] text-left text-sm">
          <thead class="bg-muted/30 text-xs uppercase text-muted">
            <tr>
              <th class="whitespace-nowrap px-3 py-2 font-medium">Invoice / Item</th>
              <th class="whitespace-nowrap px-3 py-2 text-right font-medium">Unit basic</th>
              <th class="whitespace-nowrap px-3 py-2 text-right font-medium">GST</th>
              <th class="whitespace-nowrap px-3 py-2 text-right font-medium">Expected</th>
              <th class="whitespace-nowrap px-3 py-2 text-right font-medium">Extra</th>
              <th class="whitespace-nowrap px-3 py-2 font-medium">Status</th>
              <th class="whitespace-nowrap px-3 py-2 font-medium">Suggested action</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-default">
            <tr v-for="item in thresholdReport.items" :key="item.invoiceItemId">
              <td class="px-3 py-2">
                <div class="font-medium text-primary">{{ item.invoiceNumber }}</div>
                <div class="text-sm">{{ item.productName }}</div>
                <div class="text-xs text-muted">{{ item.barcode }} - HSN {{ item.hsnCode || '-' }}</div>
              </td>
              <td class="whitespace-nowrap px-3 py-2 text-right">{{ money(item.unitBasicPrice) }}</td>
              <td class="whitespace-nowrap px-3 py-2 text-right">{{ num(item.taxPercentage) }}%<div class="text-xs text-muted">{{ money(item.taxAmount) }}</div></td>
              <td class="whitespace-nowrap px-3 py-2 text-right">{{ num(item.expectedTaxPercentage) }}%<div class="text-xs text-muted">{{ money(item.expectedTaxAmount) }}</div></td>
              <td class="whitespace-nowrap px-3 py-2 text-right font-semibold text-warning">{{ money(item.extraAmountToReview) }}</td>
              <td class="whitespace-nowrap px-3 py-2"><UBadge :color="statusColor(item.status)" variant="subtle">{{ item.status }}</UBadge></td>
              <td class="max-w-[360px] px-3 py-2 text-xs text-muted">{{ item.suggestedAction }}</td>
            </tr>
            <tr v-if="!thresholdReport.items.length">
              <td colspan="7" class="px-3 py-6 text-center text-sm text-muted">No sale item rows matched the current filter.</td>
            </tr>
          </tbody>
        </table>
      </div>
      <div v-if="thresholdReport" class="mt-3 flex items-center justify-between">
        <UButton icon="i-lucide-chevron-left" size="sm" variant="subtle" label="Previous" :disabled="filters.page <= 1" @click="changePage(filters.page - 1)" />
        <span class="text-sm text-muted">Page {{ filters.page }} / {{ thresholdTotalPages }} - {{ thresholdReport.totalItems }} item row(s)</span>
        <UButton trailing-icon="i-lucide-chevron-right" size="sm" variant="subtle" label="Next" :disabled="filters.page >= thresholdTotalPages" @click="changePage(filters.page + 1)" />
      </div>
    </section>

    <!-- Section B: configurable Rate Master line check -->
    <section class="garmetix-section-card">
      <div class="mb-3">
        <h3 class="garmetix-panel-title">GST Rate Master Line Check</h3>
        <p class="text-xs text-muted">Resolves each sale line's GST rate against the configurable Rate Master (HSN/category/price-threshold, incl. garment slabs), not a hardcoded number. Read-only - no posting happens here.</p>
      </div>

      <div class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
        <div v-for="card in rateCards" :key="card.label" class="garmetix-metric-card">
          <p class="garmetix-metric-label">{{ card.label }}</p>
          <p class="garmetix-metric-value">{{ card.value }}</p>
          <p class="garmetix-metric-caption">{{ card.detail }}</p>
        </div>
      </div>

      <div v-if="rateReport" class="mt-4 overflow-x-auto rounded-lg border border-default">
        <table class="w-full min-w-[900px] text-left text-sm">
          <thead class="bg-muted/30 text-xs uppercase text-muted">
            <tr>
              <th class="whitespace-nowrap px-3 py-2 font-medium">Invoice</th>
              <th class="whitespace-nowrap px-3 py-2 font-medium">Date</th>
              <th class="whitespace-nowrap px-3 py-2 font-medium">Customer</th>
              <th class="whitespace-nowrap px-3 py-2 text-right font-medium">Stored tax</th>
              <th class="whitespace-nowrap px-3 py-2 text-right font-medium">Resolved tax</th>
              <th class="whitespace-nowrap px-3 py-2 text-right font-medium">Issues</th>
              <th class="whitespace-nowrap px-3 py-2 font-medium">Open findings</th>
              <th class="whitespace-nowrap px-3 py-2 font-medium">Status</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-default">
            <tr v-for="invoice in rateReport.invoices" :key="invoice.invoiceId">
              <td class="whitespace-nowrap px-3 py-2 font-medium text-primary">{{ invoice.invoiceNumber }}</td>
              <td class="whitespace-nowrap px-3 py-2">{{ formatDate(invoice.onDate) }}</td>
              <td class="px-3 py-2">
                <div>{{ invoice.customerName }}</div>
                <div class="text-xs text-muted">{{ invoice.customerMobileNumber || '-' }}</div>
              </td>
              <td class="whitespace-nowrap px-3 py-2 text-right">{{ money(invoice.storedTaxAmount) }}</td>
              <td class="whitespace-nowrap px-3 py-2 text-right">{{ money(invoice.resolvedTaxAmount) }}</td>
              <td class="whitespace-nowrap px-3 py-2 text-right">{{ invoice.issueCount }} / {{ invoice.lineCount }}</td>
              <td class="whitespace-nowrap px-3 py-2">
                <NuxtLink v-if="invoice.openAuditFindingCount" class="text-primary underline" to="/gst-tax-audit">{{ invoice.openAuditFindingCount }} open</NuxtLink>
                <span v-else class="text-xs text-muted">None</span>
              </td>
              <td class="whitespace-nowrap px-3 py-2"><UBadge :color="invoice.issueCount ? 'warning' : 'success'" variant="subtle">{{ invoice.status }}</UBadge></td>
            </tr>
            <tr v-if="!rateReport.invoices.length">
              <td colspan="8" class="px-3 py-6 text-center text-sm text-muted">No invoices matched the selected filters.</td>
            </tr>
          </tbody>
        </table>
      </div>

      <div v-if="rateReport" class="mt-4 overflow-x-auto rounded-lg border border-default">
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
            <tr v-for="line in rateReport.lines" :key="line.invoiceItemId">
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
            <tr v-if="!rateReport.lines.length">
              <td colspan="7" class="px-3 py-6 text-center text-sm text-muted">No sale line rows matched the current filter.</td>
            </tr>
          </tbody>
        </table>
      </div>
      <div v-if="rateReport" class="mt-3 flex items-center justify-between">
        <UButton icon="i-lucide-chevron-left" size="sm" variant="subtle" label="Previous" :disabled="filters.page <= 1" @click="changePage(filters.page - 1)" />
        <span class="text-sm text-muted">Page {{ filters.page }} / {{ rateTotalPages }} - {{ rateReport.totalLines }} line row(s)</span>
        <UButton trailing-icon="i-lucide-chevron-right" size="sm" variant="subtle" label="Next" :disabled="filters.page >= rateTotalPages" @click="changePage(filters.page + 1)" />
      </div>
    </section>
  </section>
</template>

<script setup lang="ts">
import { formatDate, toRows, useBooksApiClient, type ApiRecord } from '../utils/books-api'

useHead({ title: 'Sale GST Review - Garmetix Books' })

const ALL_STORES_VALUE = '__ALL_STORES__'

interface ThresholdInvoice {
  invoiceId: string
  invoiceNumber: string
  onDate: string
  customerName: string
  customerMobileNumber: string
  billAmount: number
  taxDifferenceAmount: number
  extraAmountToReview: number
  issueCount: number
  status: string
}

interface ThresholdItem {
  invoiceId: string
  invoiceItemId: string
  invoiceNumber: string
  productName: string
  barcode: string
  hsnCode: string | null
  unitBasicPrice: number
  taxPercentage: number
  expectedTaxPercentage: number
  taxAmount: number
  expectedTaxAmount: number
  extraAmountToReview: number
  status: string
  suggestedAction: string
}

interface ThresholdReport {
  totalItems: number
  summary: ApiRecord
  invoices: ThresholdInvoice[]
  items: ThresholdItem[]
}

interface RateInvoice {
  invoiceId: string
  invoiceNumber: string
  onDate: string
  customerName: string
  customerMobileNumber: string
  lineCount: number
  issueCount: number
  openAuditFindingCount: number
  storedTaxAmount: number
  resolvedTaxAmount: number
  status: string
}

interface RateLine {
  invoiceId: string
  invoiceItemId: string
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

interface RateReport {
  totalLines: number
  summary: ApiRecord
  invoices: RateInvoice[]
  lines: RateLine[]
}

const { get, post } = useBooksApiClient()

const loading = ref(false)
const error = ref('')
const message = ref('')
const stores = ref<ApiRecord[]>([])
const thresholdReport = ref<ThresholdReport | null>(null)
const rateReport = ref<RateReport | null>(null)
const applyingInvoiceIds = ref<Set<string>>(new Set())

const filters = reactive({
  fromDate: new Date(Date.now() - 29 * 24 * 60 * 60 * 1000).toISOString().slice(0, 10),
  toDate: new Date().toISOString().slice(0, 10),
  storeId: ALL_STORES_VALUE,
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

const thresholdCards = computed(() => {
  const row = (thresholdReport.value?.summary || {}) as ApiRecord
  return [
    { label: 'Invoices', value: String(row.invoiceCount ?? 0), detail: `${row.itemCount ?? 0} item(s) reviewed` },
    { label: 'GST issues', value: String(row.issueCount ?? 0), detail: 'Threshold mismatches' },
    { label: 'Extra amount', value: money(row.extraAmountToReview), detail: 'Review before posting' },
    { label: 'Tax difference', value: money(row.taxDifferenceAmount), detail: `Expected tax ${money(row.expectedTaxAmount)}` }
  ]
})

const rateCards = computed(() => {
  const row = (rateReport.value?.summary || {}) as ApiRecord
  return [
    { label: 'Invoices', value: String(row.invoiceCount ?? 0), detail: `${row.lineCount ?? 0} line(s) reviewed` },
    { label: 'Rate mismatches', value: String(row.mismatchCount ?? 0), detail: 'Stored % differs from resolved %' },
    { label: 'Not configured', value: String(row.notConfiguredCount ?? 0), detail: 'No rate rule/HSN default matched' },
    { label: 'Open audit findings', value: String(row.openAuditFindingCount ?? 0), detail: 'From the GST Audit Engine' }
  ]
})

const thresholdTotalPages = computed(() => Math.max(1, Math.ceil((thresholdReport.value?.totalItems || 0) / filters.pageSize)))
const rateTotalPages = computed(() => Math.max(1, Math.ceil((rateReport.value?.totalLines || 0) / filters.pageSize)))

function money(value: unknown) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(Number(value || 0))
}

function num(value: unknown) {
  return new Intl.NumberFormat('en-IN', { maximumFractionDigits: 2 }).format(Number(value || 0))
}

function statusColor(status: string) {
  if (status === 'OK') return 'success'
  if (status?.includes('18%')) return 'warning'
  if (status?.includes('5%')) return 'error'
  return 'neutral'
}

function rateStatusColor(status: string) {
  if (status === 'OK') return 'success'
  if (status === 'Rate Mismatch') return 'warning'
  return 'neutral'
}

function isApplying(invoiceId: string) {
  return applyingInvoiceIds.value.has(invoiceId)
}

async function loadStores() {
  try {
    stores.value = toRows(await get<unknown>('stores'))
  } catch {
    stores.value = []
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
      search: filters.search.trim() || undefined
    }
    const [thresholdResult, rateResult] = await Promise.all([
      get<ThresholdReport>('/sale-review', query as Record<string, string | number | boolean | null | undefined>),
      get<RateReport>('/gst/sale-review', query as Record<string, string | number | boolean | null | undefined>)
    ])
    thresholdReport.value = thresholdResult
    rateReport.value = rateResult
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to load Sale GST Review.'
  } finally {
    loading.value = false
  }
}

function changePage(page: number) {
  filters.page = Math.max(1, page)
  refresh()
}

function copyIssueSummary() {
  const rows = (thresholdReport.value?.items || []).filter((item) => item.status !== 'OK')
  if (!rows.length) {
    message.value = 'No GST issue rows are visible for the selected filters.'
    return
  }
  const text = rows.map((item) => [
    item.invoiceNumber,
    item.productName,
    item.barcode,
    `GST ${item.taxPercentage}% -> expected ${item.expectedTaxPercentage}%`,
    `Extra ${money(item.extraAmountToReview)}`,
    item.suggestedAction
  ].join(' | ')).join('\n')
  navigator.clipboard?.writeText(text)
  message.value = `${rows.length} issue row(s) copied for accountant review.`
}

function exportThresholdCsv() {
  const rows = thresholdReport.value?.items || []
  const header = ['Invoice', 'Product', 'Barcode', 'HSN', 'Unit Basic', 'Actual GST', 'Expected GST', 'Tax', 'Expected Tax', 'Extra Amount', 'Status']
  const lines = rows.map((item) => [
    item.invoiceNumber, item.productName, item.barcode, item.hsnCode,
    item.unitBasicPrice, item.taxPercentage, item.expectedTaxPercentage,
    item.taxAmount, item.expectedTaxAmount, item.extraAmountToReview, item.status
  ].map((value) => `"${String(value ?? '').replaceAll('"', '""')}"`).join(','))
  const blob = new Blob([[header.join(','), ...lines].join('\n')], { type: 'text/csv;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = `sale-gst-review-threshold-${filters.fromDate}-to-${filters.toDate}.csv`
  anchor.click()
  URL.revokeObjectURL(url)
}

async function applyOptionB(invoice: ThresholdInvoice) {
  const extra = Number(invoice.extraAmountToReview || 0)
  if (!invoice.issueCount || extra <= 0) {
    message.value = 'This invoice has no 18% below-threshold extra amount to post.'
    return
  }
  const ok = window.confirm(`Apply Option B to ${invoice.invoiceNumber}?\n\nThis will correct eligible 18% item(s) to 5%, reduce the invoice close to the corrected total, and create an Extra Amount receipt voucher dated ${formatDate(invoice.onDate)} for approx. ${money(extra)}. This cannot be casually undone.`)
  if (!ok) return

  const next = new Set(applyingInvoiceIds.value)
  next.add(invoice.invoiceId)
  applyingInvoiceIds.value = next
  error.value = ''
  message.value = ''
  try {
    const result = await post<{ message?: string }>('/sale-review/adjustments/apply', {
      invoiceId: invoice.invoiceId,
      invoiceItemIds: null,
      employeeId: null,
      remarks: 'Applied from Sale GST Review page using Option B',
      dryRun: false
    })
    message.value = result?.message || `Invoice ${invoice.invoiceNumber} corrected and Extra Amount voucher created.`
    await refresh()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Adjustment failed. Check employee/accounting setup and try again.'
  } finally {
    const done = new Set(applyingInvoiceIds.value)
    done.delete(invoice.invoiceId)
    applyingInvoiceIds.value = done
  }
}

onMounted(async () => {
  await loadStores()
  await refresh(true)
})
</script>
