<template>
  <section class="garmetix-page-stack" :aria-busy="loading || receiptLoadingId">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-history" class="size-4" /> Invoice register</p>
          <h2 class="garmetix-dashboard-title">Sales History</h2>
          <p class="garmetix-dashboard-subtitle">
            Search, page, reprint, cancel, hard delete and review Digital Bill CRM activity from the POS counter.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton to="/sale" icon="i-lucide-scan-barcode">New Sale</UButton>
          <UButton color="neutral" variant="soft" icon="i-lucide-refresh-cw" :loading="loading" @click="refresh">Refresh</UButton>
        </div>
      </div>
    </div>

    <PosToast :message="message" :color="messageTone" :icon="messageIcon" />

    <section>
      <div class="space-y-4">
        <div class="grid gap-3 md:grid-cols-4">
          <div v-for="item in summaryItems" :key="item.label" class="garmetix-metric-card">
            <p class="garmetix-metric-label">{{ item.label }}</p>
            <p class="garmetix-metric-value">{{ item.value }}</p>
            <p class="garmetix-metric-caption">{{ item.detail }}</p>
          </div>
        </div>

        <div class="garmetix-section-card grid gap-3 lg:grid-cols-[minmax(220px,1fr)_160px_150px_150px_auto]">
          <UFormField label="Search invoice, customer, mobile or QR" name="saleHistorySearch">
            <UInput
              v-model="search"
              icon="i-lucide-search"
              placeholder="Scan QR or search invoice/customer"
              data-pos-history-search
              @keyup.enter="applyFilters"
            />
          </UFormField>
          <UFormField label="Status" name="saleHistoryStatus">
            <USelect v-model="statusFilter" :items="statusOptions" />
          </UFormField>
          <UFormField label="Date" name="saleHistoryDatePreset">
            <USelect v-model="datePreset" :items="datePresetOptions" />
          </UFormField>
          <UFormField label="Rows" name="saleHistoryPageSize">
            <USelect v-model="pageSize" :items="pageSizeOptions" />
          </UFormField>
          <div class="flex items-end gap-2">
            <UButton color="neutral" variant="soft" icon="i-lucide-list-filter" :loading="loading" @click="applyFilters">Find</UButton>
            <UButton color="neutral" variant="ghost" icon="i-lucide-x" @click="clearSearch">Clear</UButton>
          </div>
          <template v-if="datePreset === 'custom'">
            <UFormField label="From" name="saleHistoryFromDate">
              <UInput v-model="customFromDate" type="date" />
            </UFormField>
            <UFormField label="To" name="saleHistoryToDate">
              <UInput v-model="customToDate" type="date" />
            </UFormField>
          </template>
        </div>

        <div class="garmetix-table-panel overflow-x-auto">
          <table class="w-full min-w-[1080px] border-collapse text-sm">
            <thead class="bg-muted/30 text-left text-xs uppercase text-muted">
              <tr>
                <th class="border-b border-default p-3">Invoice</th>
                <th class="border-b border-default p-3">Date</th>
                <th class="border-b border-default p-3">Customer</th>
                <th class="border-b border-default p-3">Remarks</th>
                <th class="border-b border-default p-3 text-right">Bill</th>
                <th class="border-b border-default p-3 text-right">Paid</th>
                <th class="border-b border-default p-3 text-right">Balance</th>
                <th class="border-b border-default p-3">Digital CRM</th>
                <th class="border-b border-default p-3"></th>
              </tr>
            </thead>
            <tbody>
              <tr v-if="!visibleInvoices.length">
                <td colspan="9" class="p-8 text-center text-muted">No sales matched the current filters.</td>
              </tr>
              <tr
                v-for="invoice in visibleInvoices"
                :key="invoice.id"
                :class="selectedInvoice?.id === invoice.id ? 'bg-primary/5' : ''"
              >
                <td class="border-b border-default p-3">
                  <button class="text-left font-semibold text-highlighted hover:underline" type="button" @click="selectInvoice(invoice)">
                    {{ invoice.invoiceNumber || 'Invoice' }}
                  </button>
                  <div class="mt-1 flex flex-wrap gap-1">
                    <UBadge size="xs" :color="statusColor(invoice.invoiceStatus)" variant="soft">{{ invoice.invoiceStatus || 'Saved' }}</UBadge>
                    <UBadge v-if="invoice.paymentMode" size="xs" color="neutral" variant="outline">{{ invoice.paymentMode }}</UBadge>
                  </div>
                </td>
                <td class="border-b border-default p-3">{{ formatDateTime(invoice.onDate || invoice.invoiceDate || invoice.createdAt) }}</td>
                <td class="border-b border-default p-3">
                  <span>{{ invoice.customerName || 'Walk-in Customer' }}</span>
                  <small class="block text-muted">{{ invoice.customerMobileNumber || invoice.customerMobile || '-' }}</small>
                </td>
                <td class="max-w-52 truncate border-b border-default p-3 text-muted">{{ invoice.remarks || '-' }}</td>
                <td class="border-b border-default p-3 text-right font-semibold">{{ money(invoice.billAmount || invoice.netAmount || invoice.totalAmount) }}</td>
                <td class="border-b border-default p-3 text-right">{{ money(invoice.paidAmount) }}</td>
                <td class="border-b border-default p-3 text-right" :class="Number(invoice.balanceAmount || 0) > 0 ? 'text-warning' : ''">
                  {{ money(invoice.balanceAmount) }}
                </td>
                <td class="border-b border-default p-3">
                  <div class="flex flex-wrap items-center gap-1">
                    <UBadge v-if="hasDigitalBill(invoice)" size="xs" color="success" variant="soft">Link ready</UBadge>
                    <UBadge v-else size="xs" color="neutral" variant="soft">No link</UBadge>
                    <UBadge v-if="invoice.digitalBillWhatsAppStatus" size="xs" color="neutral" variant="outline">{{ invoice.digitalBillWhatsAppStatus }}</UBadge>
                  </div>
                  <small v-if="hasDigitalBill(invoice)" class="mt-1 block text-muted">
                    {{ Number(invoice.digitalBillOpenCount || 0) }} opens | {{ Number(invoice.digitalBillPdfDownloadCount || 0) }} PDF
                  </small>
                </td>
                <td class="border-b border-default p-3 text-right">
                  <div class="flex flex-wrap justify-end gap-2">
                    <UButton size="xs" color="neutral" variant="ghost" icon="i-lucide-eye" :loading="receiptLoadingId === invoice.id" @click="selectInvoice(invoice)">View</UButton>
                    <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-printer" :loading="printingId === invoice.id" @click="printInvoice(invoice)">Print</UButton>
                    <UButton
                      v-if="canCancel(invoice)"
                      size="xs"
                      color="warning"
                      variant="soft"
                      icon="i-lucide-ban"
                      :loading="cancellingId === invoice.id"
                      @click="cancelInvoice(invoice)"
                    >
                      Cancel
                    </UButton>
                    <UButton
                      v-if="canHardDelete(invoice)"
                      size="xs"
                      color="error"
                      variant="soft"
                      icon="i-lucide-shredder"
                      :loading="hardDeletingId === invoice.id"
                      @click="hardDeleteInvoice(invoice)"
                    >
                      Delete
                    </UButton>
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <div class="garmetix-section-card flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <p class="text-sm text-muted">
            Showing {{ pageStart }}-{{ pageEnd }} of {{ totalInvoices }} invoices
            <span v-if="serverDateRange">for {{ serverDateRange }}</span>
          </p>
          <div class="flex flex-wrap items-center gap-2">
            <UButton size="sm" color="neutral" variant="soft" icon="i-lucide-chevrons-left" :disabled="page <= 1 || loading" @click="goToPage(1)">First</UButton>
            <UButton size="sm" color="neutral" variant="soft" icon="i-lucide-chevron-left" :disabled="page <= 1 || loading" @click="goToPage(page - 1)">Prev</UButton>
            <UBadge color="neutral" variant="outline">Page {{ page }} / {{ totalPages }}</UBadge>
            <UButton size="sm" color="neutral" variant="soft" trailing-icon="i-lucide-chevron-right" :disabled="page >= totalPages || loading" @click="goToPage(page + 1)">Next</UButton>
            <UButton size="sm" color="neutral" variant="soft" trailing-icon="i-lucide-chevrons-right" :disabled="page >= totalPages || loading" @click="goToPage(totalPages)">Last</UButton>
          </div>
        </div>
      </div>
    </section>

    <USlideover v-model:open="detailOpen" title="Invoice Detail" :description="selectedInvoice?.invoiceNumber || ''">
      <template #body>
        <div v-if="selectedInvoice" class="space-y-5">
          <div class="grid grid-cols-2 gap-3 text-sm">
            <div>
              <p class="text-xs text-muted">Customer</p>
              <p class="font-medium">{{ selectedInvoice.customerName || 'Walk-in Customer' }}</p>
            </div>
            <div>
              <p class="text-xs text-muted">Mobile</p>
              <p class="font-medium">{{ selectedInvoice.customerMobileNumber || selectedInvoice.customerMobile || '-' }}</p>
            </div>
            <div>
              <p class="text-xs text-muted">Bill</p>
              <p class="font-medium">{{ money(selectedInvoice.billAmount || selectedInvoice.netAmount || selectedInvoice.totalAmount) }}</p>
            </div>
            <div>
              <p class="text-xs text-muted">Balance</p>
              <p class="font-medium">{{ money(selectedInvoice.balanceAmount) }}</p>
            </div>
          </div>

          <div class="rounded-md border border-default p-3">
            <div class="mb-2 flex items-center justify-between gap-2">
              <h4 class="text-sm font-semibold">Digital Bill CRM</h4>
              <UBadge :color="hasDigitalBill(selectedInvoice) ? 'success' : 'neutral'" variant="soft">
                {{ hasDigitalBill(selectedInvoice) ? 'Ready' : 'Not generated' }}
              </UBadge>
            </div>
            <div class="grid grid-cols-3 gap-2 text-center text-xs">
              <div class="rounded border border-default p-2">
                <strong class="block text-sm">{{ Number(selectedInvoice.digitalBillOpenCount || 0) }}</strong>
                Opens
              </div>
              <div class="rounded border border-default p-2">
                <strong class="block text-sm">{{ Number(selectedInvoice.digitalBillPdfDownloadCount || 0) }}</strong>
                PDF
              </div>
              <div class="rounded border border-default p-2">
                <strong class="block text-sm">{{ Number(selectedInvoice.digitalBillReviewClickCount || 0) }}</strong>
                Review
              </div>
            </div>
            <div class="mt-3 flex flex-wrap gap-2">
              <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-copy" :disabled="!digitalBillUrl(selectedInvoice)" @click="copyDigitalBillLink(selectedInvoice)">Copy link</UButton>
              <UButton size="xs" color="neutral" variant="ghost" icon="i-lucide-external-link" :disabled="!digitalBillUrl(selectedInvoice)" @click="openDigitalBill(selectedInvoice)">Open</UButton>
            </div>
          </div>

          <div>
            <div class="mb-2 flex items-center justify-between">
              <h4 class="text-sm font-semibold">Items</h4>
              <UButton size="xs" color="neutral" variant="ghost" icon="i-lucide-refresh-cw" :loading="receiptLoadingId === selectedInvoice.id" @click="loadReceipt(selectedInvoice)">Reload</UButton>
            </div>
            <div class="max-h-72 space-y-2 overflow-auto pr-1">
              <div v-if="!receiptItems.length" class="rounded-md border border-dashed border-default p-4 text-center text-sm text-muted">
                No receipt items loaded.
              </div>
              <div v-for="(item, index) in receiptItems" :key="item.id || index" class="rounded-md border border-default p-3 text-sm">
                <div class="flex justify-between gap-3">
                  <div class="min-w-0">
                    <p class="truncate font-medium">{{ item.productName || item.name || 'Item' }}</p>
                    <p class="text-xs text-muted">{{ item.barcode || '-' }} | Qty {{ Number(item.quantity || 0) }}</p>
                  </div>
                  <p class="font-semibold">{{ money(lineValue(item)) }}</p>
                </div>
              </div>
            </div>
          </div>

          <div class="flex flex-wrap gap-2">
            <UButton icon="i-lucide-printer" :loading="printingId === selectedInvoice.id" @click="printInvoice(selectedInvoice)">Reprint</UButton>
            <UButton color="neutral" variant="soft" icon="i-lucide-undo-2" to="/returns">Return</UButton>
            <UButton color="neutral" variant="soft" icon="i-lucide-repeat-2" to="/exchange">Exchange</UButton>
            <UButton v-if="canCancel(selectedInvoice)" color="warning" variant="soft" icon="i-lucide-ban" :loading="cancellingId === selectedInvoice.id" @click="cancelInvoice(selectedInvoice)">Cancel</UButton>
            <UButton v-if="canHardDelete(selectedInvoice)" color="error" variant="soft" icon="i-lucide-shredder" :loading="hardDeletingId === selectedInvoice.id" @click="hardDeleteInvoice(selectedInvoice)">Delete</UButton>
          </div>
        </div>
      </template>
    </USlideover>
  </section>
</template>

<script setup lang="ts">
import { createGarmetixApiClient } from '@garmetix/shared-api'
import { getStoredToken } from '@garmetix/shared-auth'
import { formatIndianMoney } from '@garmetix/shared-utils'
import { normalizePosDocumentSearch, openBillingInvoicePdf, textMatchesDocumentSearch } from '../utils/pos-documents'

useHead({ title: 'Sales History - Garmetix POS' })

interface SaleInvoice {
  id: string
  invoiceNumber?: string
  onDate?: string
  invoiceDate?: string
  createdAt?: string
  customerName?: string
  customerMobileNumber?: string
  customerMobile?: string
  billAmount?: number
  netAmount?: number
  totalAmount?: number
  paidAmount?: number
  balanceAmount?: number
  invoiceStatus?: string
  paymentMode?: string
  digitalBillPublicPath?: string
  digitalBillPublicToken?: string
  digitalBillIsActive?: boolean
  digitalBillWhatsAppStatus?: string
  digitalBillOpenCount?: number
  digitalBillPdfDownloadCount?: number
  digitalBillReviewClickCount?: number
  remarks?: string
}

interface PagedSaleInvoicesResponse {
  items?: SaleInvoice[]
  total?: number
  page?: number
  pageSize?: number
  datePreset?: string
  fromDate?: string
  toDate?: string
  billAmount?: number
  paidAmount?: number
  balanceAmount?: number
  cancelledCount?: number
}

const statusOptions = [
  { value: 'all', label: 'All sales' },
  { value: 'Pending', label: 'Pending' },
  { value: 'Paid', label: 'Paid' },
  { value: 'PartiallyPaid', label: 'Partially paid' },
  { value: 'Cancelled', label: 'Cancelled' },
  { value: 'Refunded', label: 'Refunded' },
  { value: 'PartiallyRefunded', label: 'Partially refunded' },
  { value: 'Overdue', label: 'Overdue' },
  { value: 'Draft', label: 'Draft' }
]

const datePresetOptions = [
  { value: 'today', label: 'Today' },
  { value: 'yesterday', label: 'Yesterday' },
  { value: 'month', label: 'This month' },
  { value: 'last-month', label: 'Last month' },
  { value: 'year', label: 'This year' },
  { value: 'custom', label: 'Custom' }
]

const pageSizeOptions = [
  { value: 25, label: '25' },
  { value: 50, label: '50' },
  { value: 100, label: '100' },
  { value: 200, label: '200' }
]

const runtimeConfig = useRuntimeConfig()
const apiBaseUrl = computed(() => String(runtimeConfig.public.apiBaseUrl || ''))
const publicAppUrls = computed(() => runtimeConfig.public.appUrls as Record<string, string | undefined>)
const mainAppUrl = computed(() => String(
  publicAppUrls.value?.NUXT_PUBLIC_GARMETIX_MAIN_URL
  || publicAppUrls.value?.NUXT_PUBLIC_MAIN_WEB_URL
  || ''
).replace(/\/$/, ''))
const appOrigin = computed(() => {
  if (!import.meta.client) return ''
  return window.location.origin
})
const loading = ref(false)
const receiptLoadingId = ref('')
const printingId = ref('')
const cancellingId = ref('')
const hardDeletingId = ref('')
const search = ref('')
const statusFilter = ref('all')
const datePreset = ref('today')
const customFromDate = ref(toInputDate(new Date()))
const customToDate = ref(toInputDate(new Date()))
const page = ref(1)
const pageSize = ref(50)
const total = ref(0)
const serverFromDate = ref('')
const serverToDate = ref('')
const serverSummary = ref({
  billAmount: 0,
  paidAmount: 0,
  balanceAmount: 0,
  cancelledCount: 0
})
const invoices = ref<SaleInvoice[]>([])
const selectedInvoice = ref<SaleInvoice | null>(null)
const detailOpen = ref(false)
const receiptItems = ref<any[]>([])
const message = ref('')
const messageTone = ref<'success' | 'error' | 'warning' | 'neutral'>('neutral')
const messageIcon = computed(() => messageTone.value === 'success' ? 'i-lucide-circle-check' : messageTone.value === 'error' ? 'i-lucide-circle-alert' : messageTone.value === 'warning' ? 'i-lucide-triangle-alert' : 'i-lucide-info')

const api = computed(() => createGarmetixApiClient({
  baseUrl: apiBaseUrl.value,
  getToken: () => import.meta.client ? getStoredToken(window.localStorage) : null
}))

const visibleInvoices = computed(() => {
  const term = normalizePosDocumentSearch(search.value).toLowerCase()
  if (!term) return invoices.value
  return invoices.value.filter((invoice) => [
    invoice.id,
    invoice.invoiceNumber,
    invoice.customerName,
    invoice.customerMobileNumber,
    invoice.customerMobile,
    invoice.invoiceStatus,
    invoice.paymentMode,
    invoice.remarks
  ].some(value => textMatchesDocumentSearch(value, term)))
})
const totalInvoices = computed(() => total.value || visibleInvoices.value.length)
const totalPages = computed(() => Math.max(1, Math.ceil(totalInvoices.value / Number(pageSize.value || 50))))
const pageStart = computed(() => totalInvoices.value === 0 ? 0 : ((page.value - 1) * Number(pageSize.value || 50)) + 1)
const pageEnd = computed(() => Math.min(totalInvoices.value, page.value * Number(pageSize.value || 50)))
const digitalReadyCount = computed(() => visibleInvoices.value.filter(invoice => hasDigitalBill(invoice)).length)
const serverDateRange = computed(() => {
  if (!serverFromDate.value || !serverToDate.value) return ''
  return `${formatDate(serverFromDate.value)} to ${formatDate(serverToDate.value)}`
})
const summaryItems = computed(() => [
  { label: 'Invoices', value: String(totalInvoices.value), detail: 'Matching register filters' },
  { label: 'Sales amount', value: money(serverSummary.value.billAmount), detail: 'Server register total' },
  { label: 'Due balance', value: money(serverSummary.value.balanceAmount), detail: `Paid ${money(serverSummary.value.paidAmount)}` },
  { label: 'Cancelled', value: String(serverSummary.value.cancelledCount), detail: `${digitalReadyCount.value} digital links on this page` }
])

function hasDigitalBill(invoice: SaleInvoice | null) {
  return Boolean(invoice?.digitalBillPublicPath || invoice?.digitalBillPublicToken || invoice?.digitalBillIsActive)
}

function money(value: number | string | null | undefined) {
  return formatIndianMoney(value)
}

function toInputDate(value: Date) {
  const year = value.getFullYear()
  const month = String(value.getMonth() + 1).padStart(2, '0')
  const day = String(value.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

function formatDate(value: string | null | undefined) {
  if (!value) return '-'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return String(value)
  return new Intl.DateTimeFormat('en-IN', { dateStyle: 'medium' }).format(date)
}

function formatDateTime(value: string | null | undefined) {
  if (!value) return '-'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return String(value)
  return new Intl.DateTimeFormat('en-IN', {
    dateStyle: 'medium',
    timeStyle: 'short'
  }).format(date)
}

function statusColor(status: string | null | undefined) {
  const value = String(status || '').toLowerCase()
  if (value.includes('cancel') || value.includes('void')) return 'error'
  if (value.includes('due') || value.includes('partial') || value.includes('pending')) return 'warning'
  if (value.includes('draft')) return 'neutral'
  return 'success'
}

function isCancelled(invoice: SaleInvoice | null) {
  return String(invoice?.invoiceStatus || '').toLowerCase().includes('cancel')
}

function canCancel(invoice: SaleInvoice | null) {
  return Boolean(invoice?.id) && !isCancelled(invoice)
}

function canHardDelete(invoice: SaleInvoice | null) {
  return Boolean(invoice?.id) && isCancelled(invoice)
}

function lineValue(item: any) {
  return Number(item.total || item.lineTotal || item.amount || 0)
    || (Number(item.quantity || 0) * Number(item.mrp || item.rate || 0)) - Number(item.discountAmount || 0)
}

function showMessage(tone: typeof messageTone.value, text: string) {
  messageTone.value = tone
  message.value = text
}

function buildInvoiceQuery() {
  const query = new URLSearchParams()
  query.set('page', String(page.value))
  query.set('pageSize', String(pageSize.value))
  query.set('datePreset', datePreset.value)
  if (statusFilter.value !== 'all') query.set('status', statusFilter.value)
  const term = normalizePosDocumentSearch(search.value).trim()
  if (term) query.set('q', term)
  if (datePreset.value === 'custom') {
    if (customFromDate.value) query.set('from', customFromDate.value)
    if (customToDate.value) query.set('to', customToDate.value)
  }
  return query
}

async function refresh() {
  if (!import.meta.client) return
  const token = getStoredToken(window.localStorage)
  if (!token) {
    showMessage('warning', 'Login is required to load sale history.')
    return
  }

  loading.value = true
  try {
    const response = await api.value.get<PagedSaleInvoicesResponse>(`billing/sales?${buildInvoiceQuery().toString()}`)
    const rows = Array.isArray(response?.items) ? response.items : []
    invoices.value = rows
    total.value = Number(response?.total ?? rows.length)
    if (response?.page) page.value = Number(response.page)
    if (response?.pageSize) pageSize.value = Number(response.pageSize)
    serverFromDate.value = response?.fromDate || ''
    serverToDate.value = response?.toDate || ''
    serverSummary.value = {
      billAmount: Number(response?.billAmount || 0),
      paidAmount: Number(response?.paidAmount || 0),
      balanceAmount: Number(response?.balanceAmount || 0),
      cancelledCount: Number(response?.cancelledCount || 0)
    }

    const current = selectedInvoice.value ? rows.find(invoice => invoice.id === selectedInvoice.value?.id) : null
    if (current) selectedInvoice.value = current
    else selectedInvoice.value = rows[0] || null
    if (selectedInvoice.value) await loadReceipt(selectedInvoice.value)
    else receiptItems.value = []
    message.value = ''
  } catch (error) {
    showMessage('error', error instanceof Error ? error.message : 'Could not load sale history.')
  } finally {
    loading.value = false
  }
}

async function applyFilters() {
  page.value = 1
  await refresh()
}

async function goToPage(nextPage: number) {
  page.value = Math.min(Math.max(1, nextPage), totalPages.value)
  await refresh()
}

async function selectInvoice(invoice: SaleInvoice) {
  selectedInvoice.value = invoice
  detailOpen.value = true
  await loadReceipt(invoice)
}

async function loadReceipt(invoice: SaleInvoice) {
  if (!invoice?.id || receiptLoadingId.value) return
  receiptLoadingId.value = invoice.id
  try {
    const receipt = await api.value.get<any>(`billing/sales/${invoice.id}/receipt`)
    receiptItems.value = Array.isArray(receipt?.items) ? receipt.items : []
  } catch (error) {
    receiptItems.value = []
    showMessage('warning', error instanceof Error ? error.message : 'Could not load invoice receipt items.')
  } finally {
    receiptLoadingId.value = ''
  }
}

async function printInvoice(invoice: SaleInvoice) {
  if (!invoice?.id || printingId.value) return
  const token = getStoredToken(window.localStorage)
  if (!token) {
    showMessage('warning', 'Login is required before printing invoices.')
    return
  }

  printingId.value = invoice.id
  try {
    await openBillingInvoicePdf({
      apiBaseUrl: apiBaseUrl.value,
      invoiceId: invoice.id,
      token,
      reprint: true
    })
    showMessage('success', `Invoice ${invoice.invoiceNumber || ''} opened for printing.`.trim())
  } catch (error) {
    showMessage('error', error instanceof Error ? error.message : 'Could not print invoice.')
  } finally {
    printingId.value = ''
  }
}

async function cancelInvoice(invoice: SaleInvoice | null) {
  if (!invoice?.id || !import.meta.client || cancellingId.value) return
  if (!window.confirm(`Cancel invoice ${invoice.invoiceNumber || ''}? Stock, payment and accounting entries will be reversed.`)) return
  const reason = window.prompt('Reason for cancellation', 'Cancelled from POS sales history') || 'Cancelled from POS sales history'
  cancellingId.value = invoice.id
  try {
    await api.value.post(`billing/sales/${invoice.id}/cancel`, { reason })
    showMessage('warning', `Invoice ${invoice.invoiceNumber || ''} cancelled.`.trim())
    await refresh()
  } catch (error) {
    showMessage('error', error instanceof Error ? error.message : 'Could not cancel invoice.')
  } finally {
    cancellingId.value = ''
  }
}

async function hardDeleteInvoice(invoice: SaleInvoice | null) {
  if (!invoice?.id || !import.meta.client || hardDeletingId.value) return
  const typed = window.prompt(`Type invoice number ${invoice.invoiceNumber || ''} to hard delete permanently.`)
  if (!typed || typed.trim() !== String(invoice.invoiceNumber || '').trim()) {
    showMessage('neutral', 'Hard delete was not confirmed.')
    return
  }
  const reason = window.prompt('Reason for hard delete', 'Hard deleted from POS sales history') || 'Hard deleted from POS sales history'
  const query = new URLSearchParams({
    confirmInvoiceNumber: typed.trim(),
    reason
  })
  hardDeletingId.value = invoice.id
  try {
    const result = await api.value.delete<any>(`billing/sales/${invoice.id}/hard-delete?${query.toString()}`)
    showMessage('warning', `Invoice hard deleted. Removed items ${Number(result?.removedInvoiceItems || 0)}, payments ${Number(result?.removedInvoicePayments || 0)}.`)
    await refresh()
  } catch (error) {
    showMessage('error', error instanceof Error ? error.message : 'Could not hard delete invoice.')
  } finally {
    hardDeletingId.value = ''
  }
}

function digitalBillUrl(invoice: SaleInvoice | null) {
  if (!invoice) return ''
  const path = String(invoice.digitalBillPublicPath || '').trim()
  if (path.startsWith('http://') || path.startsWith('https://')) return path
  const baseUrl = mainAppUrl.value || appOrigin.value
  if (path) return `${baseUrl}${path.startsWith('/') ? path : `/${path}`}`
  const token = String(invoice.digitalBillPublicToken || '').trim()
  return token ? `${baseUrl}/i/${encodeURIComponent(token)}` : ''
}

async function copyDigitalBillLink(invoice: SaleInvoice | null) {
  const url = digitalBillUrl(invoice)
  if (!url || !import.meta.client) return
  await navigator.clipboard.writeText(url)
  showMessage('success', 'Digital bill link copied.')
}

function openDigitalBill(invoice: SaleInvoice | null) {
  const url = digitalBillUrl(invoice)
  if (!url || !import.meta.client) return
  window.open(url, '_blank', 'noopener,noreferrer')
}

async function clearSearch() {
  search.value = ''
  statusFilter.value = 'all'
  datePreset.value = 'today'
  page.value = 1
  message.value = ''
  await refresh()
}

watch([statusFilter, datePreset, pageSize], () => {
  void applyFilters()
})

watch([customFromDate, customToDate], () => {
  if (datePreset.value === 'custom') void applyFilters()
})

onMounted(() => {
  void refresh()
})
</script>
