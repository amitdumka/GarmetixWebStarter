<template>
  <section class="space-y-4" :aria-busy="loading || returning">
    <div class="border border-default bg-muted/10 p-4">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="text-sm text-muted">Customer return counter</p>
          <h2 class="mt-1 text-2xl font-semibold">Sales Returns</h2>
          <p class="mt-2 max-w-2xl text-sm text-muted">
            Scan invoice QR or search invoice/customer, select returned items, create credit note, and print the return document.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton color="neutral" variant="soft" icon="i-lucide-refresh-cw" :loading="loading" @click="refresh">Refresh</UButton>
          <UButton color="neutral" variant="ghost" icon="i-lucide-rotate-ccw" :disabled="!selectedInvoice" @click="resetSelection">Clear</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="message" :color="messageTone" variant="subtle" :icon="messageIcon" :description="message" />

    <section class="grid gap-4 xl:grid-cols-[minmax(0,1fr)_360px]">
      <div class="space-y-4">
        <div class="grid gap-3 border border-default bg-muted/10 p-4 lg:grid-cols-[1fr_auto]">
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
          <div class="flex items-end gap-2">
            <UButton icon="i-lucide-search" :loading="loading" @click="selectBestMatch">Find</UButton>
            <UButton color="neutral" variant="soft" icon="i-lucide-scan-line" @click="focusInvoiceSearch">Scan</UButton>
          </div>
        </div>

        <div class="overflow-x-auto border border-default">
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

        <div class="overflow-x-auto border border-default">
          <div class="flex items-center justify-between gap-3 border-b border-default bg-muted/10 p-4">
            <div>
              <h3 class="font-semibold">Return Items</h3>
              <p class="text-sm text-muted">{{ selectedInvoice ? `Invoice ${selectedInvoice.invoiceNumber}` : 'Select an invoice to load sold items.' }}</p>
            </div>
            <UButton color="neutral" variant="soft" size="sm" icon="i-lucide-list-checks" :disabled="!returnLines.length" @click="returnAll">Return all</UButton>
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

      <aside class="space-y-4">
        <div class="border border-default bg-muted/10 p-4">
          <h3 class="text-base font-semibold">Return Summary</h3>
          <dl class="mt-4 space-y-2 text-sm">
            <div class="flex justify-between gap-3"><dt class="text-muted">Invoice</dt><dd class="text-right">{{ selectedInvoice?.invoiceNumber || '-' }}</dd></div>
            <div class="flex justify-between gap-3"><dt class="text-muted">Items</dt><dd>{{ selectedReturnItemCount }}</dd></div>
            <div class="flex justify-between gap-3"><dt class="text-muted">Quantity</dt><dd>{{ selectedReturnQuantity }}</dd></div>
            <div class="flex justify-between gap-3 border-t border-default pt-2 text-lg font-semibold"><dt>Return value</dt><dd>{{ money(returnTotal) }}</dd></div>
            <div class="flex justify-between gap-3"><dt class="text-muted">Refund now</dt><dd>{{ money(Number(returnForm.refundAmount || 0)) }}</dd></div>
            <div class="flex justify-between gap-3"><dt class="text-muted">Customer credit</dt><dd>{{ money(storeCreditAmount) }}</dd></div>
          </dl>
        </div>

        <div class="border border-default bg-muted/10 p-4">
          <h3 class="text-base font-semibold">Refund</h3>
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
      </aside>
    </section>
  </section>
</template>

<script setup lang="ts">
import { createApiUrl, createGarmetixApiClient } from '@garmetix/shared-api'
import { getStoredToken } from '@garmetix/shared-auth'
import { formatIndianMoney } from '@garmetix/shared-utils'

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

interface PrintQueueItem {
  invoiceId: string
  invoiceNumber: string
  customerName: string
  billAmount: number
  savedAt: string
  printedAt?: string
}

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

const printQueueKey = 'garmetix.pos.print.queue.v1'
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
  const term = invoiceSearch.value.trim().toLowerCase()
  return invoices.value
    .filter(invoice => invoice.invoiceStatus !== 'Cancelled' && invoice.invoiceStatus !== 'Refunded' && !String(invoice.invoiceNumber || '').startsWith('SR-'))
    .filter((invoice) => {
      if (!term) return true
      return [invoice.invoiceNumber, invoice.customerName, invoice.customerMobileNumber, invoice.invoiceStatus]
        .some(value => String(value || '').toLowerCase().includes(term))
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
const canSubmitReturn = computed(() => Boolean(selectedInvoice.value && selectedReturnItemCount.value && returnTotal.value > 0 && !returning.value))

function money(value: number | string | null | undefined) {
  return formatIndianMoney(value)
}

function formatDate(value: string | null | undefined) {
  if (!value) return '-'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? String(value) : new Intl.DateTimeFormat('en-IN').format(date)
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
    const [invoiceRows, bankRows] = await Promise.all([
      api.value.get<RecentInvoice[]>('billing/sales/recent?take=100'),
      api.value.get<any[]>('bank-accounts')
    ])
    invoices.value = invoiceRows
    bankAccounts.value = bankRows
    message.value = ''
  } catch (error) {
    showMessage('error', error instanceof Error ? error.message : 'Could not load returnable invoices.')
  } finally {
    loading.value = false
  }
}

async function selectBestMatch() {
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
  const validation = validateReturn()
  if (validation) {
    showMessage('warning', validation)
    return
  }
  if (!selectedInvoice.value) return

  returning.value = true
  try {
    const response = await api.value.post<SalesReturnResponse>(`billing/sales/${selectedInvoice.value.id}/returns`, {
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
    })
    addPrintQueueItem({
      invoiceId: response.returnInvoiceId,
      invoiceNumber: response.creditNoteNumber || '',
      customerName: selectedInvoice.value.customerName || 'Walk-in Customer',
      billAmount: Number(response.creditAmount || returnTotal.value),
      savedAt: new Date().toISOString()
    })
    showMessage('success', `Return ${response.creditNoteNumber || ''} saved.`.trim())
    await printReturn(response.returnInvoiceId)
    resetSelection()
    await refresh()
  } catch (error) {
    showMessage('error', error instanceof Error ? error.message : 'Could not create sales return.')
  } finally {
    returning.value = false
  }
}

async function printReturn(returnInvoiceId: string) {
  const token = getStoredToken(window.localStorage)
  const url = createApiUrl(apiBaseUrl.value, `billing/sales/${returnInvoiceId}/pdf?format=a4&copy=customer&reprint=false&signatures=true`)
  const response = await fetch(url, {
    headers: token ? { Authorization: `Bearer ${token}` } : undefined
  })
  if (!response.ok) throw new Error('Return saved, but PDF print could not start.')

  const blob = await response.blob()
  const blobUrl = URL.createObjectURL(blob)
  window.open(blobUrl, '_blank', 'noopener,noreferrer')
}

function addPrintQueueItem(item: PrintQueueItem) {
  if (!import.meta.client || !item.invoiceId) return
  const rows = readPrintQueue().filter(row => row.invoiceId !== item.invoiceId)
  rows.unshift(item)
  localStorage.setItem(printQueueKey, JSON.stringify(rows.slice(0, 50)))
}

function readPrintQueue(): PrintQueueItem[] {
  if (!import.meta.client) return []
  try {
    const rows = JSON.parse(localStorage.getItem(printQueueKey) || '[]')
    return Array.isArray(rows) ? rows : []
  } catch {
    return []
  }
}

function resetSelection() {
  selectedInvoice.value = null
  returnLines.value = []
  returnForm.refundAmount = 0
  returnForm.refundPaymentMode = paymentModeValue.cash
  returnForm.bankAccountId = null
  returnForm.reason = 'Sales return'
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

onMounted(() => {
  window.addEventListener('keydown', handleReturnShortcut)
  void refresh()
  focusInvoiceSearch()
})

onBeforeUnmount(() => {
  window.removeEventListener('keydown', handleReturnShortcut)
})
</script>
