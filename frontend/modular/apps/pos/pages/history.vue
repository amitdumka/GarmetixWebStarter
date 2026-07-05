<template>
  <section class="garmetix-page-stack" :aria-busy="loading || receiptLoadingId">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-history" class="size-4" /> Store transactions</p>
          <h2 class="garmetix-dashboard-title">Sales History</h2>
          <p class="garmetix-dashboard-subtitle">
            Review recent invoices, reprint bills, and track Digital Bill CRM activity from the POS counter.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton to="/sale" icon="i-lucide-scan-barcode">New Sale</UButton>
          <UButton color="neutral" variant="soft" icon="i-lucide-refresh-cw" :loading="loading" @click="refresh">Refresh</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="message" :color="messageTone" variant="subtle" :icon="messageIcon" :description="message" />

    <section class="grid gap-4 xl:grid-cols-[minmax(0,1fr)_380px]">
      <div class="space-y-4">
        <div class="grid gap-3 md:grid-cols-4">
          <div v-for="item in summaryItems" :key="item.label" class="garmetix-metric-card">
            <p class="garmetix-metric-label">{{ item.label }}</p>
            <p class="garmetix-metric-value">{{ item.value }}</p>
            <p class="garmetix-metric-caption">{{ item.detail }}</p>
          </div>
        </div>

        <div class="garmetix-section-card grid gap-3 md:grid-cols-[1fr_auto_auto]">
          <UFormField label="Search invoice, customer, mobile or QR" name="saleHistorySearch">
            <UInput
              v-model="search"
              icon="i-lucide-search"
              placeholder="Scan QR or search invoice/customer"
              data-pos-history-search
              @keyup.enter="selectFirstMatch"
            />
          </UFormField>
          <UFormField label="Status" name="saleHistoryStatus">
            <USelect v-model="statusFilter" :items="statusOptions" class="min-w-44" />
          </UFormField>
          <div class="flex items-end gap-2">
            <UButton color="neutral" variant="soft" icon="i-lucide-list-filter" @click="selectFirstMatch">Find</UButton>
            <UButton color="neutral" variant="ghost" icon="i-lucide-x" @click="clearSearch">Clear</UButton>
          </div>
        </div>

        <div class="garmetix-table-panel overflow-x-auto">
          <table class="w-full min-w-[920px] border-collapse text-sm">
            <thead class="bg-muted/30 text-left text-xs uppercase text-muted">
              <tr>
                <th class="border-b border-default p-3">Invoice</th>
                <th class="border-b border-default p-3">Date</th>
                <th class="border-b border-default p-3">Customer</th>
                <th class="border-b border-default p-3 text-right">Bill</th>
                <th class="border-b border-default p-3 text-right">Paid</th>
                <th class="border-b border-default p-3 text-right">Balance</th>
                <th class="border-b border-default p-3">Digital CRM</th>
                <th class="border-b border-default p-3"></th>
              </tr>
            </thead>
            <tbody>
              <tr v-if="!visibleInvoices.length">
                <td colspan="8" class="p-8 text-center text-muted">No sales matched the current filters.</td>
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
                  <div class="flex justify-end gap-2">
                    <UButton size="xs" color="neutral" variant="ghost" icon="i-lucide-eye" :loading="receiptLoadingId === invoice.id" @click="selectInvoice(invoice)">View</UButton>
                    <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-printer" :loading="printingId === invoice.id" @click="printInvoice(invoice)">Print</UButton>
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <aside class="garmetix-section-card h-fit p-0">
        <div class="border-b border-default p-4">
          <h3 class="garmetix-panel-title">Invoice Detail</h3>
          <p class="garmetix-panel-subtitle">{{ selectedInvoice ? selectedInvoice.invoiceNumber : 'Select a sale from the list.' }}</p>
        </div>

        <div v-if="!selectedInvoice" class="p-6 text-center text-sm text-muted">
          Select an invoice to review items, payments, print status and digital CRM actions.
        </div>

        <div v-else class="space-y-5 p-4">
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
          </div>
        </div>
      </aside>
    </section>
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
}

const statusOptions = [
  { value: 'all', label: 'All sales' },
  { value: 'active', label: 'Active only' },
  { value: 'due', label: 'Due balance' },
  { value: 'digital', label: 'Digital bill ready' },
  { value: 'cancelled', label: 'Cancelled' }
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
const search = ref('')
const statusFilter = ref('active')
const invoices = ref<SaleInvoice[]>([])
const selectedInvoice = ref<SaleInvoice | null>(null)
const receiptItems = ref<any[]>([])
const message = ref('')
const messageTone = ref<'success' | 'error' | 'warning' | 'neutral'>('neutral')
const messageIcon = computed(() => messageTone.value === 'success' ? 'i-lucide-circle-check' : messageTone.value === 'error' ? 'i-lucide-circle-alert' : messageTone.value === 'warning' ? 'i-lucide-triangle-alert' : 'i-lucide-info')

const api = computed(() => createGarmetixApiClient({
  baseUrl: apiBaseUrl.value,
  getToken: () => import.meta.client ? getStoredToken(window.localStorage) : null
}))

const filteredInvoices = computed(() => {
  const term = normalizePosDocumentSearch(search.value).toLowerCase()
  return invoices.value
    .filter((invoice) => statusMatches(invoice))
    .filter((invoice) => {
      if (!term) return true
      return [
        invoice.id,
        invoice.invoiceNumber,
        invoice.customerName,
        invoice.customerMobileNumber,
        invoice.customerMobile,
        invoice.invoiceStatus,
        invoice.paymentMode
      ].some(value => textMatchesDocumentSearch(value, term))
    })
})

const visibleInvoices = computed(() => filteredInvoices.value.slice(0, 100))
const saleCount = computed(() => filteredInvoices.value.length)
const saleAmount = computed(() => filteredInvoices.value.reduce((sum, invoice) => sum + Number(invoice.billAmount || invoice.netAmount || invoice.totalAmount || 0), 0))
const dueAmount = computed(() => filteredInvoices.value.reduce((sum, invoice) => sum + Number(invoice.balanceAmount || 0), 0))
const digitalReadyCount = computed(() => filteredInvoices.value.filter(invoice => hasDigitalBill(invoice)).length)
const summaryItems = computed(() => [
  { label: 'Invoices', value: String(saleCount.value), detail: 'Matching current filters' },
  { label: 'Sales amount', value: money(saleAmount.value), detail: 'Recent server invoices' },
  { label: 'Due balance', value: money(dueAmount.value), detail: 'Customer payable balance' },
  { label: 'Digital bills', value: String(digitalReadyCount.value), detail: 'Links ready for customer view' }
])

function statusMatches(invoice: SaleInvoice) {
  const status = String(invoice.invoiceStatus || '').toLowerCase()
  if (statusFilter.value === 'all') return true
  if (statusFilter.value === 'active') return !['cancelled', 'refunded'].includes(status)
  if (statusFilter.value === 'due') return Number(invoice.balanceAmount || 0) > 0
  if (statusFilter.value === 'digital') return hasDigitalBill(invoice)
  if (statusFilter.value === 'cancelled') return status.includes('cancel')
  return true
}

function hasDigitalBill(invoice: SaleInvoice | null) {
  return Boolean(invoice?.digitalBillPublicPath || invoice?.digitalBillPublicToken || invoice?.digitalBillIsActive)
}

function money(value: number | string | null | undefined) {
  return formatIndianMoney(value)
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
  if (value.includes('due') || value.includes('partial')) return 'warning'
  return 'success'
}

function lineValue(item: any) {
  return Number(item.total || item.lineTotal || item.amount || 0)
    || (Number(item.quantity || 0) * Number(item.mrp || item.rate || 0)) - Number(item.discountAmount || 0)
}

function showMessage(tone: typeof messageTone.value, text: string) {
  messageTone.value = tone
  message.value = text
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
    invoices.value = await api.value.get<SaleInvoice[]>('billing/sales/recent?take=100')
    if (!selectedInvoice.value && invoices.value.length) {
      await selectInvoice(invoices.value[0])
    } else if (selectedInvoice.value) {
      const current = invoices.value.find(invoice => invoice.id === selectedInvoice.value?.id)
      if (current) selectedInvoice.value = current
    }
    message.value = ''
  } catch (error) {
    showMessage('error', error instanceof Error ? error.message : 'Could not load sale history.')
  } finally {
    loading.value = false
  }
}

async function selectInvoice(invoice: SaleInvoice) {
  selectedInvoice.value = invoice
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

function selectFirstMatch() {
  const first = filteredInvoices.value[0]
  if (first) void selectInvoice(first)
  else showMessage('neutral', 'No sale matched the current search.')
}

function clearSearch() {
  search.value = ''
  statusFilter.value = 'active'
  message.value = ''
}

watch(statusFilter, () => {
  if (selectedInvoice.value && !filteredInvoices.value.some(invoice => invoice.id === selectedInvoice.value?.id)) {
    receiptItems.value = []
    selectedInvoice.value = filteredInvoices.value[0] || null
    if (selectedInvoice.value) void loadReceipt(selectedInvoice.value)
  }
})

onMounted(() => {
  void refresh()
})
</script>
