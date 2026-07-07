<template>
  <section class="garmetix-page-stack" :aria-busy="loading || returning">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-rotate-ccw" class="size-4" /> Customer return counter</p>
          <h2 class="garmetix-dashboard-title">Sales Returns</h2>
          <p class="garmetix-dashboard-subtitle">
            Scan invoice QR or search invoice/customer, select returned items, create credit note, and print the return document.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton color="neutral" variant="soft" icon="i-lucide-refresh-cw" :loading="loading" @click="refresh">Refresh</UButton>
          <UButton color="neutral" variant="ghost" icon="i-lucide-rotate-ccw" :disabled="!selectedInvoice" @click="resetSelection">Clear</UButton>
        </div>
      </div>
    </div>

    <PosToast :message="message" :color="messageTone" :icon="messageIcon" />

    <section>
      <div class="space-y-4">
        <div class="garmetix-section-card grid gap-3 lg:grid-cols-[minmax(220px,1fr)_150px_140px_auto]">
          <UFormField label="Invoice number / QR code / customer" name="invoiceSearch">
            <UInput
              ref="invoiceSearchInput"
              v-model="invoiceSearch"
              icon="i-lucide-search"
              placeholder="Scan QR, enter invoice number, mobile, or customer name"
              autofocus
              data-pos-return-search
              @keyup.enter="selectBestMatch"
            />
          </UFormField>
          <UFormField label="Status" name="returnStatusFilter">
            <USelect v-model="statusFilter" :items="statusOptions" />
          </UFormField>
          <UFormField label="Date" name="returnDatePreset">
            <USelect v-model="datePreset" :items="datePresetOptions" />
          </UFormField>
          <div class="flex items-end gap-2">
            <UButton icon="i-lucide-search" :loading="loading" @click="selectBestMatch">Find</UButton>
            <UButton color="neutral" variant="soft" icon="i-lucide-scan-line" @click="focusInvoiceSearch">Scan</UButton>
          </div>
          <template v-if="datePreset === 'custom'">
            <UFormField label="From" name="returnFromDate">
              <UInput v-model="customFromDate" type="date" />
            </UFormField>
            <UFormField label="To" name="returnToDate">
              <UInput v-model="customToDate" type="date" />
            </UFormField>
          </template>
        </div>

        <div class="garmetix-table-panel overflow-x-auto">
          <table class="w-full min-w-[780px] border-collapse text-sm">
            <thead class="bg-muted/30 text-left text-xs uppercase text-muted">
              <tr>
                <th class="border-b border-default p-3">Invoice</th>
                <th class="border-b border-default p-3">Date</th>
                <th class="border-b border-default p-3">Customer</th>
                <th class="border-b border-default p-3 text-right">Amount</th>
                <th class="border-b border-default p-3">Status</th>
                <th class="border-b border-default p-3"></th>
              </tr>
            </thead>
            <tbody>
              <tr v-if="!filteredInvoices.length">
                <td colspan="6" class="p-6 text-center text-muted">No returnable invoice matched the search.</td>
              </tr>
              <tr
                v-for="invoice in filteredInvoices"
                :key="invoice.id"
                :class="selectedInvoice?.id === invoice.id ? 'bg-primary/5' : ''"
              >
                <td class="border-b border-default p-3 font-semibold">{{ invoice.invoiceNumber || '-' }}</td>
                <td class="border-b border-default p-3">{{ formatDate(invoice.onDate) }}</td>
                <td class="border-b border-default p-3">
                  <span>{{ invoice.customerName || 'Walk-in Customer' }}</span>
                  <small class="block text-muted">{{ invoice.customerMobileNumber || '-' }}</small>
                </td>
                <td class="border-b border-default p-3 text-right">{{ money(invoice.billAmount) }}</td>
                <td class="border-b border-default p-3">
                  <UBadge color="success" variant="soft">{{ invoice.invoiceStatus || 'Saved' }}</UBadge>
                </td>
                <td class="border-b border-default p-3 text-right">
                  <UButton size="sm" icon="i-lucide-rotate-ccw" :loading="receiptLoadingId === invoice.id" @click="openReturn(invoice)">
                    Return
                  </UButton>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <div class="garmetix-section-card flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <p class="text-sm text-muted">Showing {{ pageStart }}-{{ pageEnd }} of {{ totalInvoices }} invoice(s)</p>
          <div class="flex flex-wrap items-center gap-2">
            <UButton size="sm" color="neutral" variant="soft" icon="i-lucide-chevrons-left" :disabled="page <= 1 || loading" @click="goToPage(1)">First</UButton>
            <UButton size="sm" color="neutral" variant="soft" icon="i-lucide-chevron-left" :disabled="page <= 1 || loading" @click="goToPage(page - 1)">Prev</UButton>
            <UBadge color="neutral" variant="outline">Page {{ page }} / {{ totalPages }}</UBadge>
            <UButton size="sm" color="neutral" variant="soft" trailing-icon="i-lucide-chevron-right" :disabled="page >= totalPages || loading" @click="goToPage(page + 1)">Next</UButton>
            <UButton size="sm" color="neutral" variant="soft" trailing-icon="i-lucide-chevrons-right" :disabled="page >= totalPages || loading" @click="goToPage(totalPages)">Last</UButton>
          </div>
        </div>

        <div class="garmetix-table-panel overflow-x-auto">
          <div class="flex items-center justify-between gap-3 border-b border-default bg-muted/10 p-4">
            <div>
              <h3 class="garmetix-panel-title">Return Items</h3>
              <p class="garmetix-panel-subtitle">{{ selectedInvoice ? `Invoice ${selectedInvoice.invoiceNumber}` : 'Select an invoice to load sold items.' }}</p>
            </div>
            <div class="flex flex-wrap gap-2">
              <UButton color="neutral" variant="soft" size="sm" icon="i-lucide-list-checks" :disabled="!returnLines.length" @click="returnAll">Return all</UButton>
              <UButton color="primary" size="sm" icon="i-lucide-receipt" :disabled="!selectedInvoice" @click="formOpen = true">Review & Refund</UButton>
            </div>
          </div>
          <table class="w-full min-w-[760px] border-collapse text-sm">
            <thead class="bg-muted/30 text-left text-xs uppercase text-muted">
              <tr>
                <th class="border-b border-default p-3">Item</th>
                <th class="border-b border-default p-3">Barcode</th>
                <th class="border-b border-default p-3 text-right">Sold</th>
                <th class="border-b border-default p-3 text-right">Return</th>
                <th class="border-b border-default p-3 text-right">MRP</th>
                <th class="border-b border-default p-3 text-right">Line value</th>
              </tr>
            </thead>
            <tbody>
              <tr v-if="!returnLines.length">
                <td colspan="6" class="p-6 text-center text-muted">No invoice items loaded.</td>
              </tr>
              <tr v-for="item in returnLines" :key="item.invoiceItemId">
                <td class="border-b border-default p-3">
                  <strong>{{ item.productName }}</strong>
                  <small class="block text-muted">{{ item.unit || 'Unit' }} | Tax {{ item.taxPercentage }}%</small>
                </td>
                <td class="border-b border-default p-3">{{ item.barcode }}</td>
                <td class="border-b border-default p-3 text-right">{{ item.quantity }}</td>
                <td class="border-b border-default p-3 text-right">
                  <UInput v-model="item.returnQuantity" class="ml-auto w-24" inputmode="decimal" @blur="clampReturnQuantity(item)" />
                </td>
                <td class="border-b border-default p-3 text-right">{{ money(item.mrp) }}</td>
                <td class="border-b border-default p-3 text-right font-semibold">{{ money(returnLineValue(item)) }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </section>

    <USlideover v-model:open="formOpen" title="Review & Refund" :description="selectedInvoice?.invoiceNumber || ''">
      <template #body>
        <div class="garmetix-detail-panel">
          <h3 class="garmetix-panel-title">Return Summary</h3>
          <dl class="mt-4 space-y-2 text-sm">
            <div class="flex justify-between gap-3"><dt class="text-muted">Invoice</dt><dd class="text-right">{{ selectedInvoice?.invoiceNumber || '-' }}</dd></div>
            <div class="flex justify-between gap-3"><dt class="text-muted">Items</dt><dd>{{ selectedReturnItemCount }}</dd></div>
            <div class="flex justify-between gap-3"><dt class="text-muted">Quantity</dt><dd>{{ selectedReturnQuantity }}</dd></div>
            <div class="flex justify-between gap-3 border-t border-default pt-2 text-lg font-semibold"><dt>Return value</dt><dd>{{ money(returnTotal) }}</dd></div>
            <div class="flex justify-between gap-3"><dt class="text-muted">Refund now</dt><dd>{{ money(Number(returnForm.refundAmount || 0)) }}</dd></div>
            <div class="flex justify-between gap-3"><dt class="text-muted">Customer credit</dt><dd>{{ money(storeCreditAmount) }}</dd></div>
          </dl>
        </div>

        <div class="garmetix-section-card mt-4">
          <h3 class="garmetix-panel-title">Refund</h3>
          <div class="mt-4 space-y-3">
            <UFormField label="Refund amount now">
              <UInput v-model="returnForm.refundAmount" inputmode="decimal" @input="clampRefund" />
            </UFormField>
            <UFormField label="Refund mode">
              <USelect v-model="returnForm.refundPaymentMode" :items="paymentModeOptions" :disabled="Number(returnForm.refundAmount || 0) <= 0" />
            </UFormField>
            <UFormField v-if="refundRequiresBank" label="Bank account">
              <USelect v-model="returnForm.bankAccountId" :items="bankAccountOptions" placeholder="Select bank" />
            </UFormField>
            <UFormField label="Reason / remarks">
              <UTextarea v-model="returnForm.reason" :rows="3" />
            </UFormField>
            <UButton block icon="i-lucide-printer" :loading="returning" :disabled="!canSubmitReturn" @click="submitReturn">
              Save & Print Return
            </UButton>
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
import { upsertPrintQueueItem, type PosPrintQueueItem } from '../utils/local-pos-storage'
import { normalizePosDocumentSearch, openBillingInvoicePdf, textMatchesDocumentSearch } from '../utils/pos-documents'
import { createSalesReturnRequest } from '../utils/return-contract'

useHead({ title: 'Sales Returns - Garmetix POS' })

interface RecentInvoice {
  id: string
  invoiceNumber: string
  onDate: string
  customerName: string
  customerMobileNumber: string
  billAmount: number
  paidAmount: number
  balanceAmount: number
  invoiceStatus: string
  paymentMode: string
}

interface ReceiptItem {
  id: string
  productName: string
  barcode: string
  quantity: number
  mrp: number
  discountAmount: number
  taxPercentage: number
  unit?: string
}

interface ReturnLine {
  invoiceItemId: string
  productName: string
  barcode: string
  quantity: number
  returnQuantity: number
  mrp: number
  discountAmount: number
  taxPercentage: number
  unit?: string
}

interface SalesReturnResponse {
  returnInvoiceId: string
  creditNoteNumber: string
  originalInvoiceId: string
  originalInvoiceNumber: string
  creditAmount: number
  refundedAmount: number
  storeCreditAmount: number
  reversedQuantity: number
  originalInvoiceStatus: string
}

interface PagedSaleInvoicesResponse {
  items?: RecentInvoice[]
  total?: number
  page?: number
  pageSize?: number
}

const statusOptions = [
  { value: 'all', label: 'All sales' },
  { value: 'Paid', label: 'Paid' },
  { value: 'PartiallyPaid', label: 'Partially paid' },
  { value: 'Pending', label: 'Pending' },
  { value: 'Overdue', label: 'Overdue' }
]

const datePresetOptions = [
  { value: 'today', label: 'Today' },
  { value: 'yesterday', label: 'Yesterday' },
  { value: 'month', label: 'This month' },
  { value: 'last-month', label: 'Last month' },
  { value: 'year', label: 'This year' },
  { value: 'custom', label: 'Custom' }
]

const paymentModeValue = {
  cash: 0,
  card: 1,
  upi: 2,
  wallets: 3,
  imps: 4,
  rtgs: 5,
  neft: 6,
  cheque: 7,
  demandDraft: 8
}

const paymentModeOptions = [
  { value: paymentModeValue.cash, label: 'Cash' },
  { value: paymentModeValue.card, label: 'Card' },
  { value: paymentModeValue.upi, label: 'UPI' },
  { value: paymentModeValue.wallets, label: 'Wallet' },
  { value: paymentModeValue.imps, label: 'IMPS' },
  { value: paymentModeValue.rtgs, label: 'RTGS' },
  { value: paymentModeValue.neft, label: 'NEFT' },
  { value: paymentModeValue.cheque, label: 'Cheque' },
  { value: paymentModeValue.demandDraft, label: 'Demand Draft' }
]

const runtimeConfig = useRuntimeConfig()
const apiBaseUrl = computed(() => String(runtimeConfig.public.apiBaseUrl || ''))
const loading = ref(false)
const returning = ref(false)
const receiptLoadingId = ref('')
const message = ref('')
const messageTone = ref<'success' | 'error' | 'warning' | 'neutral'>('neutral')
const messageIcon = computed(() => messageTone.value === 'success' ? 'i-lucide-circle-check' : messageTone.value === 'error' ? 'i-lucide-circle-alert' : messageTone.value === 'warning' ? 'i-lucide-triangle-alert' : 'i-lucide-info')
const invoiceSearch = ref('')
const invoiceSearchInput = ref<any>(null)
const invoices = ref<RecentInvoice[]>([])
const bankAccounts = ref<any[]>([])
const selectedInvoice = ref<RecentInvoice | null>(null)
const returnLines = ref<ReturnLine[]>([])
const formOpen = ref(false)
const statusFilter = ref('all')
const datePreset = ref('today')
const customFromDate = ref(toInputDate(new Date()))
const customToDate = ref(toInputDate(new Date()))
const page = ref(1)
const pageSize = ref(50)
const total = ref(0)
const returnForm = reactive({
  refundAmount: 0,
  refundPaymentMode: paymentModeValue.cash,
  bankAccountId: null as string | null,
  reason: 'Sales return'
})

const api = computed(() => createGarmetixApiClient({
  baseUrl: apiBaseUrl.value,
  getToken: () => import.meta.client ? getStoredToken(window.localStorage) : null
}))

const filteredInvoices = computed(() => {
  const term = normalizePosDocumentSearch(invoiceSearch.value).toLowerCase()
  return invoices.value
    .filter(invoice => !['cancelled', 'refunded'].includes(String(invoice.invoiceStatus || '').toLowerCase()) && !String(invoice.invoiceNumber || '').toUpperCase().startsWith('SR-'))
    .filter((invoice) => {
      if (!term) return true
      return [invoice.id, invoice.invoiceNumber, invoice.customerName, invoice.customerMobileNumber, invoice.invoiceStatus]
        .some(value => textMatchesDocumentSearch(value, term))
    })
})
const bankAccountOptions = computed(() => bankAccounts.value.map(account => ({
  value: account.id,
  label: `${account.accountHolderName || account.bankName || 'Bank'} ${account.accountNumber || ''}`.trim()
})))
const refundRequiresBank = computed(() => Number(returnForm.refundAmount || 0) > 0 && Number(returnForm.refundPaymentMode) !== paymentModeValue.cash)
const returnTotal = computed(() => returnLines.value.reduce((sum, item) => sum + returnLineValue(item), 0))
const selectedReturnItemCount = computed(() => returnLines.value.filter(item => Number(item.returnQuantity || 0) > 0).length)
const selectedReturnQuantity = computed(() => returnLines.value.reduce((sum, item) => sum + Number(item.returnQuantity || 0), 0))
const storeCreditAmount = computed(() => Math.max(returnTotal.value - Number(returnForm.refundAmount || 0), 0))
const canSubmitReturn = computed(() => Boolean(
  selectedInvoice.value
  && selectedReturnItemCount.value
  && returnTotal.value > 0
  && (!refundRequiresBank.value || returnForm.bankAccountId)
  && !returning.value
))
const totalInvoices = computed(() => total.value || filteredInvoices.value.length)
const totalPages = computed(() => Math.max(1, Math.ceil(totalInvoices.value / Number(pageSize.value || 50))))
const pageStart = computed(() => totalInvoices.value === 0 ? 0 : ((page.value - 1) * Number(pageSize.value || 50)) + 1)
const pageEnd = computed(() => Math.min(totalInvoices.value, page.value * Number(pageSize.value || 50)))

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
  return Number.isNaN(date.getTime()) ? String(value) : new Intl.DateTimeFormat('en-IN').format(date)
}

function buildInvoiceQuery() {
  const query = new URLSearchParams()
  query.set('page', String(page.value))
  query.set('pageSize', String(pageSize.value))
  query.set('datePreset', datePreset.value)
  if (statusFilter.value !== 'all') query.set('status', statusFilter.value)
  const term = normalizePosDocumentSearch(invoiceSearch.value).trim()
  if (term) query.set('q', term)
  if (datePreset.value === 'custom') {
    if (customFromDate.value) query.set('from', customFromDate.value)
    if (customToDate.value) query.set('to', customToDate.value)
  }
  return query
}

function showMessage(tone: typeof messageTone.value, text: string) {
  messageTone.value = tone
  message.value = text
}

function focusInvoiceSearch() {
  if (!import.meta.client) return
  void nextTick(() => {
    const input = invoiceSearchInput.value?.inputRef
      || invoiceSearchInput.value?.$el?.querySelector?.('input')
      || document.querySelector<HTMLInputElement>('[data-pos-return-search]')
    input?.focus?.()
    input?.select?.()
  })
}

async function refresh() {
  if (!import.meta.client) return
  const token = getStoredToken(window.localStorage)
  if (!token) {
    showMessage('warning', 'Login is required before loading sales returns.')
    return
  }

  loading.value = true
  try {
    const [invoiceResponse, bankRows] = await Promise.all([
      api.value.get<PagedSaleInvoicesResponse>(`billing/sales?${buildInvoiceQuery().toString()}`),
      api.value.get<any[]>('bank-accounts')
    ])
    invoices.value = Array.isArray(invoiceResponse?.items) ? invoiceResponse.items : []
    total.value = Number(invoiceResponse?.total ?? invoices.value.length)
    if (invoiceResponse?.page) page.value = Number(invoiceResponse.page)
    if (invoiceResponse?.pageSize) pageSize.value = Number(invoiceResponse.pageSize)
    bankAccounts.value = bankRows
    message.value = ''
  } catch (error) {
    showMessage('error', error instanceof Error ? error.message : 'Could not load returnable invoices.')
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

async function selectBestMatch() {
  invoiceSearch.value = normalizePosDocumentSearch(invoiceSearch.value)
  if (!filteredInvoices.value.length) {
    showMessage('warning', 'No invoice matched the search.')
    return
  }
  await openReturn(filteredInvoices.value[0])
}

async function openReturn(invoice: RecentInvoice) {
  receiptLoadingId.value = invoice.id
  try {
    const receipt = await api.value.get<any>(`billing/sales/${invoice.id}/receipt`)
    selectedInvoice.value = invoice
    returnLines.value = (receipt.items || []).map((item: ReceiptItem) => ({
      invoiceItemId: item.id,
      productName: item.productName,
      barcode: item.barcode,
      quantity: Number(item.quantity || 0),
      returnQuantity: 0,
      mrp: Number(item.mrp || 0),
      discountAmount: Number(item.discountAmount || 0),
      taxPercentage: Number(item.taxPercentage || 0),
      unit: item.unit || 'Unit'
    }))
    returnForm.refundAmount = 0
    returnForm.refundPaymentMode = paymentModeValue.cash
    returnForm.bankAccountId = null
    returnForm.reason = 'Sales return'
    showMessage('neutral', `Loaded ${invoice.invoiceNumber}. Enter returned quantities.`)
  } catch (error) {
    showMessage('error', error instanceof Error ? error.message : 'Could not load invoice items.')
  } finally {
    receiptLoadingId.value = ''
  }
}

function returnLineValue(item: ReturnLine) {
  const quantity = Number(item.returnQuantity || 0)
  return Math.max((Number(item.mrp || 0) - Number(item.discountAmount || 0)) * quantity, 0)
}

function clampReturnQuantity(item: ReturnLine) {
  item.returnQuantity = Math.min(Math.max(Number(item.returnQuantity || 0), 0), Number(item.quantity || 0))
  clampRefund()
}

function returnAll() {
  for (const item of returnLines.value) {
    item.returnQuantity = Number(item.quantity || 0)
  }
  clampRefund()
}

function clampRefund() {
  returnForm.refundAmount = Math.min(Math.max(Number(returnForm.refundAmount || 0), 0), returnTotal.value)
  if (!refundRequiresBank.value) returnForm.bankAccountId = null
}

function validateReturn() {
  if (!selectedInvoice.value) return 'Select an invoice first.'
  const invalidLine = returnLines.value.find(item => Number(item.returnQuantity || 0) > Number(item.quantity || 0))
  if (invalidLine) return `Return quantity for ${invalidLine.barcode} is more than sold quantity.`
  if (!selectedReturnItemCount.value) return 'Enter return quantity for at least one item.'
  if (Number(returnForm.refundAmount || 0) > returnTotal.value) return 'Refund amount cannot be more than return value.'
  if (refundRequiresBank.value && !returnForm.bankAccountId) return 'Select bank account for non-cash refund.'
  return ''
}

async function submitReturn() {
  if (returning.value) return
  const validation = validateReturn()
  if (validation) {
    showMessage('warning', validation)
    return
  }
  if (!selectedInvoice.value) return

  returning.value = true
  try {
    const response = await api.value.post<SalesReturnResponse>(`billing/sales/${selectedInvoice.value.id}/returns`, createSalesReturnRequest({
      refundAmount: Number(returnForm.refundAmount || 0),
      refundPaymentMode: Number(returnForm.refundAmount || 0) > 0 ? Number(returnForm.refundPaymentMode) : null,
      bankAccountId: refundRequiresBank.value ? returnForm.bankAccountId : null,
      reason: returnForm.reason,
      items: returnLines.value
        .filter(item => Number(item.returnQuantity || 0) > 0)
        .map(item => ({
          invoiceItemId: item.invoiceItemId,
          quantity: Number(item.returnQuantity || 0)
        }))
    }))
    addPrintQueueItem({
      invoiceId: response.returnInvoiceId,
      invoiceNumber: response.creditNoteNumber || '',
      customerName: selectedInvoice.value.customerName || 'Walk-in Customer',
      billAmount: Number(response.creditAmount || returnTotal.value),
      savedAt: new Date().toISOString()
    })
    try {
      await printReturn(response.returnInvoiceId)
      showMessage('success', `Return ${response.creditNoteNumber || ''} saved and opened for printing.`.trim())
    } catch (printError) {
      showMessage('warning', printError instanceof Error
        ? `Return ${response.creditNoteNumber || ''} saved. ${printError.message}`.trim()
        : `Return ${response.creditNoteNumber || ''} saved. Use Print Queue to retry printing.`.trim())
    }
    resetSelection()
    await refresh()
  } catch (error) {
    showMessage('error', error instanceof Error ? error.message : 'Could not create sales return.')
  } finally {
    returning.value = false
  }
}

async function printReturn(returnInvoiceId: string) {
  await openBillingInvoicePdf({
    apiBaseUrl: apiBaseUrl.value,
    invoiceId: returnInvoiceId,
    token: getStoredToken(window.localStorage),
    reprint: false
  })
}

function addPrintQueueItem(item: PosPrintQueueItem) {
  if (!import.meta.client || !item.invoiceId) return
  upsertPrintQueueItem(item)
}

function resetSelection() {
  selectedInvoice.value = null
  returnLines.value = []
  returnForm.refundAmount = 0
  returnForm.refundPaymentMode = paymentModeValue.cash
  returnForm.bankAccountId = null
  returnForm.reason = 'Sales return'
  formOpen.value = false
  focusInvoiceSearch()
}

function handleReturnShortcut(event: KeyboardEvent) {
  if (event.defaultPrevented) return
  if (event.key === 'F2') {
    event.preventDefault()
    focusInvoiceSearch()
  }
  if (event.key === 'F4') {
    event.preventDefault()
    void submitReturn()
  }
  if (event.key === 'Escape' && invoiceSearch.value) {
    event.preventDefault()
    invoiceSearch.value = ''
    focusInvoiceSearch()
  }
}

watch(() => [returnForm.refundAmount, returnForm.refundPaymentMode], () => {
  clampRefund()
  if (refundRequiresBank.value && !returnForm.bankAccountId) {
    returnForm.bankAccountId = bankAccountOptions.value[0]?.value || null
  }
})

watch([statusFilter, datePreset, pageSize], () => {
  void applyFilters()
})

watch([customFromDate, customToDate], () => {
  if (datePreset.value === 'custom') void applyFilters()
})

onMounted(() => {
  window.addEventListener('keydown', handleReturnShortcut)
  void refresh()
  focusInvoiceSearch()
})

onBeforeUnmount(() => {
  window.removeEventListener('keydown', handleReturnShortcut)
})
</script>
