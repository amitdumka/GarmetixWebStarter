<template>
  <section class="garmetix-page-stack" :aria-busy="loading || returning">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-rotate-ccw" class="size-4" /> Customer return counter</p>
          <h2 class="garmetix-dashboard-title">New Sales Return</h2>
          <p class="garmetix-dashboard-subtitle">
            Select the returned items, decide the refund, then save to create the credit note and print the return document.
          </p>
        </div>
        <div class="flex flex-wrap items-center gap-2">
          <PosPrintFormatPicker compact />
          <UButton color="neutral" variant="soft" icon="i-lucide-arrow-left" to="/returns">Back to returns</UButton>
        </div>
      </div>
    </div>

    <PosToast :message="message" :color="messageTone" :icon="messageIcon" />

    <div v-if="!invoiceId" class="garmetix-section-card text-center text-sm text-muted">
      No invoice selected. <NuxtLink to="/returns" class="font-semibold text-primary">Choose an invoice to return</NuxtLink>.
    </div>

    <div v-else-if="saved" class="garmetix-section-card space-y-3">
      <h3 class="garmetix-panel-title">Return saved</h3>
      <dl class="space-y-2 text-sm">
        <div class="flex justify-between gap-3"><dt class="text-muted">Credit note</dt><dd class="font-semibold">{{ saved.creditNoteNumber || '-' }}</dd></div>
        <div class="flex justify-between gap-3"><dt class="text-muted">Original invoice</dt><dd>{{ saved.originalInvoiceNumber || invoice?.invoiceNumber || '-' }}</dd></div>
        <div class="flex justify-between gap-3"><dt class="text-muted">Credit value</dt><dd>{{ money(saved.creditAmount) }}</dd></div>
        <div class="flex justify-between gap-3"><dt class="text-muted">Refunded now</dt><dd>{{ money(saved.refundedAmount) }}</dd></div>
        <div class="flex justify-between gap-3"><dt class="text-muted">Customer store credit</dt><dd>{{ money(saved.storeCreditAmount) }}</dd></div>
      </dl>
      <div class="flex flex-wrap gap-2">
        <UButton icon="i-lucide-printer" :loading="printing" @click="printSaved">Print again</UButton>
        <UButton color="neutral" variant="soft" icon="i-lucide-list-checks" to="/returns-register">Return register</UButton>
        <UButton color="neutral" variant="soft" icon="i-lucide-undo-2" to="/returns">Another return</UButton>
      </div>
    </div>

    <template v-else>
      <div class="garmetix-section-card">
        <h3 class="garmetix-panel-title">Original invoice</h3>
        <dl v-if="invoice" class="mt-3 grid gap-3 text-sm sm:grid-cols-2 lg:grid-cols-4">
          <div><dt class="text-muted">Invoice</dt><dd class="font-semibold">{{ invoice.invoiceNumber }}</dd></div>
          <div><dt class="text-muted">Date</dt><dd>{{ formatDate(invoice.onDate) }}</dd></div>
          <div><dt class="text-muted">Customer</dt><dd>{{ invoice.customerName || 'Walk-in Customer' }} <span class="text-muted">{{ invoice.customerMobileNumber || '' }}</span></dd></div>
          <div><dt class="text-muted">Bill amount</dt><dd class="font-semibold">{{ money(invoice.billAmount) }}</dd></div>
        </dl>
        <p v-else class="mt-3 text-sm text-muted">{{ loading ? 'Loading invoice...' : 'Invoice could not be loaded.' }}</p>
      </div>

      <div class="garmetix-table-panel overflow-x-auto">
        <div class="flex items-center justify-between gap-3 border-b border-default bg-muted/10 p-4">
          <div>
            <h3 class="garmetix-panel-title">Return items</h3>
            <p class="garmetix-panel-subtitle">Enter the quantity coming back for each item.</p>
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

      <div class="grid gap-4 lg:grid-cols-2">
        <div class="garmetix-detail-panel">
          <h3 class="garmetix-panel-title">Return summary</h3>
          <dl class="mt-4 space-y-2 text-sm">
            <div class="flex justify-between gap-3"><dt class="text-muted">Items</dt><dd>{{ selectedReturnItemCount }}</dd></div>
            <div class="flex justify-between gap-3"><dt class="text-muted">Quantity</dt><dd>{{ selectedReturnQuantity }}</dd></div>
            <div class="flex justify-between gap-3 border-t border-default pt-2 text-lg font-semibold"><dt>Return value</dt><dd>{{ money(returnTotal) }}</dd></div>
            <div class="flex justify-between gap-3"><dt class="text-muted">Refund now</dt><dd>{{ money(Number(returnForm.refundAmount || 0)) }}</dd></div>
            <div class="flex justify-between gap-3"><dt class="text-muted">Customer credit</dt><dd>{{ money(storeCreditAmount) }}</dd></div>
          </dl>
        </div>

        <div class="garmetix-section-card">
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
            <p class="text-xs text-muted">Shortcut: F4 saves and prints.</p>
          </div>
        </div>
      </div>
    </template>
  </section>
</template>

<script setup lang="ts">
import { createGarmetixApiClient } from '@garmetix/shared-api'
import { getStoredToken } from '@garmetix/shared-auth'
import { formatIndianMoney } from '@garmetix/shared-utils'
import { upsertPrintQueueItem, type PosPrintQueueItem } from '../utils/local-pos-storage'
import { openBillingInvoicePdf } from '../utils/pos-documents'
import { createSalesReturnRequest } from '../utils/return-contract'

useHead({ title: 'New Sales Return - Garmetix POS' })

interface InvoiceHeader {
  id: string
  invoiceNumber: string
  onDate: string
  customerName: string
  customerMobileNumber: string
  billAmount: number
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

const paymentModeValue = { cash: 0, card: 1, upi: 2, wallets: 3, imps: 4, rtgs: 5, neft: 6, cheque: 7, demandDraft: 8 }
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

const route = useRoute()
const runtimeConfig = useRuntimeConfig()
const apiBaseUrl = computed(() => String(runtimeConfig.public.apiBaseUrl || ''))
const invoiceId = computed(() => String(route.query.invoiceId || '').trim())
const loading = ref(false)
const returning = ref(false)
const printing = ref(false)
const message = ref('')
const messageTone = ref<'success' | 'error' | 'warning' | 'neutral'>('neutral')
const messageIcon = computed(() => messageTone.value === 'success' ? 'i-lucide-circle-check' : messageTone.value === 'error' ? 'i-lucide-circle-alert' : messageTone.value === 'warning' ? 'i-lucide-triangle-alert' : 'i-lucide-info')
const invoice = ref<InvoiceHeader | null>(null)
const bankAccounts = ref<any[]>([])
const returnLines = ref<ReturnLine[]>([])
const saved = ref<SalesReturnResponse | null>(null)
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
  invoice.value
  && selectedReturnItemCount.value
  && returnTotal.value > 0
  && (!refundRequiresBank.value || returnForm.bankAccountId)
  && !returning.value
))

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

async function load() {
  if (!import.meta.client || !invoiceId.value) return
  if (!getStoredToken(window.localStorage)) {
    showMessage('warning', 'Login is required before creating a sales return.')
    return
  }

  loading.value = true
  try {
    const [receipt, bankRows] = await Promise.all([
      api.value.get<any>(`billing/sales/${invoiceId.value}/receipt`),
      api.value.get<any[]>('bank-accounts')
    ])
    bankAccounts.value = bankRows
    invoice.value = {
      id: receipt.id || invoiceId.value,
      invoiceNumber: receipt.invoiceNumber,
      onDate: receipt.onDate,
      customerName: receipt.customerName,
      customerMobileNumber: receipt.customerMobileNumber,
      billAmount: Number(receipt.billAmount || 0)
    }
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
    showMessage('neutral', `Loaded ${receipt.invoiceNumber}. Enter returned quantities.`)
  } catch (error) {
    showMessage('error', error instanceof Error ? error.message : 'Could not load invoice items.')
  } finally {
    loading.value = false
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
  for (const item of returnLines.value) item.returnQuantity = Number(item.quantity || 0)
  clampRefund()
}

function clampRefund() {
  returnForm.refundAmount = Math.min(Math.max(Number(returnForm.refundAmount || 0), 0), returnTotal.value)
  if (!refundRequiresBank.value) returnForm.bankAccountId = null
}

function validateReturn() {
  if (!invoice.value) return 'Select an invoice first.'
  const invalidLine = returnLines.value.find(item => Number(item.returnQuantity || 0) > Number(item.quantity || 0))
  if (invalidLine) return `Return quantity for ${invalidLine.barcode} is more than sold quantity.`
  if (!selectedReturnItemCount.value) return 'Enter return quantity for at least one item.'
  if (Number(returnForm.refundAmount || 0) > returnTotal.value) return 'Refund amount cannot be more than return value.'
  if (refundRequiresBank.value && !returnForm.bankAccountId) return 'Select bank account for non-cash refund.'
  return ''
}

async function submitReturn() {
  if (returning.value || saved.value) return
  const validation = validateReturn()
  if (validation) {
    showMessage('warning', validation)
    return
  }
  if (!invoice.value) return

  returning.value = true
  try {
    const response = await api.value.post<SalesReturnResponse>(`billing/sales/${invoice.value.id}/returns`, createSalesReturnRequest({
      refundAmount: Number(returnForm.refundAmount || 0),
      refundPaymentMode: Number(returnForm.refundAmount || 0) > 0 ? Number(returnForm.refundPaymentMode) : null,
      bankAccountId: refundRequiresBank.value ? returnForm.bankAccountId : null,
      reason: returnForm.reason,
      items: returnLines.value
        .filter(item => Number(item.returnQuantity || 0) > 0)
        .map(item => ({ invoiceItemId: item.invoiceItemId, quantity: Number(item.returnQuantity || 0) }))
    }))
    saved.value = response
    addPrintQueueItem({
      invoiceId: response.returnInvoiceId,
      invoiceNumber: response.creditNoteNumber || '',
      customerName: invoice.value.customerName || 'Walk-in Customer',
      billAmount: Number(response.creditAmount || returnTotal.value),
      savedAt: new Date().toISOString()
    })
    try {
      await printReturn(response.returnInvoiceId)
      showMessage('success', `Return ${response.creditNoteNumber || ''} saved and opened for printing.`.trim())
    } catch (printError) {
      showMessage('warning', printError instanceof Error
        ? `Return ${response.creditNoteNumber || ''} saved. ${printError.message}`.trim()
        : `Return ${response.creditNoteNumber || ''} saved. Use Print again or the Print Queue.`.trim())
    }
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

async function printSaved() {
  if (!saved.value || printing.value) return
  printing.value = true
  try {
    await openBillingInvoicePdf({
      apiBaseUrl: apiBaseUrl.value,
      invoiceId: saved.value.returnInvoiceId,
      token: getStoredToken(window.localStorage),
      reprint: true
    })
  } catch (error) {
    showMessage('error', error instanceof Error ? error.message : 'Could not print return.')
  } finally {
    printing.value = false
  }
}

function addPrintQueueItem(item: PosPrintQueueItem) {
  if (!import.meta.client || !item.invoiceId) return
  upsertPrintQueueItem(item)
}

function handleReturnShortcut(event: KeyboardEvent) {
  if (event.defaultPrevented) return
  if (event.key === 'F4') {
    event.preventDefault()
    void submitReturn()
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
  void load()
})

onBeforeUnmount(() => {
  window.removeEventListener('keydown', handleReturnShortcut)
})
</script>
