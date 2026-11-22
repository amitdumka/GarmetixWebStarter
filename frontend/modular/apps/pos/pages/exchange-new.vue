<template>
  <section class="garmetix-page-stack" :aria-busy="loading || saving">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-repeat-2" class="size-4" /> Customer exchange counter</p>
          <h2 class="garmetix-dashboard-title">New Sales Exchange</h2>
          <p class="garmetix-dashboard-subtitle">
            Select the items coming back, scan the replacement items, collect any extra payment, then save to print the exchange invoice.
          </p>
        </div>
        <div class="flex flex-wrap items-center gap-2">
          <PosPrintFormatPicker compact />
          <UButton color="neutral" variant="soft" icon="i-lucide-arrow-left" to="/exchange">Back to exchanges</UButton>
        </div>
      </div>
    </div>

    <PosToast :message="message" :color="messageTone" :icon="messageIcon" />

    <div v-if="!invoiceId" class="garmetix-section-card text-center text-sm text-muted">
      No invoice selected. <NuxtLink to="/exchange" class="font-semibold text-primary">Choose an invoice to exchange</NuxtLink>.
    </div>

    <div v-else-if="saved" class="garmetix-section-card space-y-3">
      <h3 class="garmetix-panel-title">Exchange saved</h3>
      <dl class="space-y-2 text-sm">
        <div class="flex justify-between gap-3"><dt class="text-muted">Credit note</dt><dd class="font-semibold">{{ saved.creditNoteNumber || '-' }}</dd></div>
        <div class="flex justify-between gap-3"><dt class="text-muted">New invoice</dt><dd class="font-semibold">{{ saved.exchangeInvoiceNumber || '-' }}</dd></div>
        <div class="flex justify-between gap-3"><dt class="text-muted">Return credit</dt><dd>{{ money(saved.creditAmount) }}</dd></div>
        <div class="flex justify-between gap-3"><dt class="text-muted">New invoice amount</dt><dd>{{ money(saved.newInvoiceAmount) }}</dd></div>
        <div class="flex justify-between gap-3"><dt class="text-muted">Extra paid now</dt><dd>{{ money(saved.additionalPaidAmount) }}</dd></div>
        <div class="flex justify-between gap-3"><dt class="text-muted">Remaining store credit</dt><dd>{{ money(saved.remainingStoreCreditAmount) }}</dd></div>
      </dl>
      <div class="flex flex-wrap gap-2">
        <UButton icon="i-lucide-printer" :loading="printing" @click="printSaved">Print again</UButton>
        <UButton color="neutral" variant="soft" icon="i-lucide-list-checks" to="/returns-register">Return / exchange register</UButton>
        <UButton color="neutral" variant="soft" icon="i-lucide-repeat-2" to="/exchange">Another exchange</UButton>
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
            <h3 class="garmetix-panel-title">Returned items</h3>
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
              <th class="border-b border-default p-3 text-right">Credit value</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="!returnLines.length">
              <td colspan="6" class="p-6 text-center text-muted">No original invoice items loaded.</td>
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

      <div class="garmetix-section-card grid gap-3 lg:grid-cols-[1fr_100px_110px_auto]">
        <UFormField label="Replacement barcode / product" name="productSearch">
          <UInput
            ref="productSearchInput"
            v-model="productSearch"
            list="pos-exchange-product-suggestions"
            icon="i-lucide-scan-barcode"
            placeholder="Scan replacement barcode or type product"
            data-pos-exchange-product-search
            :disabled="!invoice"
            @change="selectProductFromInput"
            @keyup.enter="lookupAndAdd"
          />
          <datalist id="pos-exchange-product-suggestions">
            <option v-for="item in productSuggestions" :key="`${item.productId}:${item.barcode}`" :value="suggestionLabel(item)" />
          </datalist>
        </UFormField>
        <UFormField label="Qty" name="quantity">
          <UInput v-model="quantity" inputmode="decimal" :disabled="!invoice" />
        </UFormField>
        <UFormField label="Disc / unit" name="discount">
          <UInput v-model="lineDiscount" placeholder="0 or 10%" :disabled="!invoice" />
        </UFormField>
        <div class="flex items-end">
          <UButton class="w-full" icon="i-lucide-plus" :loading="productLoading" :disabled="!invoice" @click="lookupAndAdd">Add</UButton>
        </div>
      </div>

      <div class="garmetix-table-panel overflow-x-auto">
        <div class="border-b border-default bg-muted/10 p-4">
          <h3 class="garmetix-panel-title">Replacement items</h3>
          <p class="garmetix-panel-subtitle">These items become the new exchange invoice.</p>
        </div>
        <table class="w-full min-w-[760px] border-collapse text-sm">
          <thead class="bg-muted/30 text-left text-xs uppercase text-muted">
            <tr>
              <th class="border-b border-default p-3">Item</th>
              <th class="border-b border-default p-3">Barcode</th>
              <th class="border-b border-default p-3 text-right">Qty</th>
              <th class="border-b border-default p-3 text-right">MRP</th>
              <th class="border-b border-default p-3 text-right">Discount</th>
              <th class="border-b border-default p-3 text-right">Total</th>
              <th class="border-b border-default p-3"></th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="!replacementCart.length">
              <td colspan="7" class="p-6 text-center text-muted">Scan replacement product to complete exchange.</td>
            </tr>
            <tr v-for="(item, index) in replacementCart" :key="`${item.productId}:${item.barcode}:${index}`">
              <td class="border-b border-default p-3">
                <strong>{{ item.name }}</strong>
                <small class="block text-muted">{{ item.unit || 'Unit' }} | Available {{ item.availableQty }}</small>
              </td>
              <td class="border-b border-default p-3">{{ item.barcode }}</td>
              <td class="border-b border-default p-3 text-right">{{ item.quantity }}</td>
              <td class="border-b border-default p-3 text-right">{{ money(item.mrp) }}</td>
              <td class="border-b border-default p-3 text-right">{{ money(item.discountAmount) }}</td>
              <td class="border-b border-default p-3 text-right font-semibold">{{ money(replacementLineValue(item)) }}</td>
              <td class="border-b border-default p-3 text-right">
                <UButton color="error" variant="ghost" size="xs" icon="i-lucide-trash-2" @click="removeReplacement(index)" />
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <div class="grid gap-4 lg:grid-cols-2">
        <div class="garmetix-detail-panel">
          <h3 class="garmetix-panel-title">Exchange summary</h3>
          <dl class="mt-4 space-y-2 text-sm">
            <div class="flex justify-between gap-3"><dt class="text-muted">Return items</dt><dd>{{ selectedReturnItemCount }}</dd></div>
            <div class="flex justify-between gap-3"><dt class="text-muted">Replacement items</dt><dd>{{ replacementCart.length }}</dd></div>
            <div class="flex justify-between gap-3"><dt class="text-muted">Return credit</dt><dd>{{ money(returnTotal) }}</dd></div>
            <div class="flex justify-between gap-3"><dt class="text-muted">New invoice</dt><dd>{{ money(replacementTotal) }}</dd></div>
            <div class="flex justify-between gap-3 border-t border-default pt-2 text-lg font-semibold"><dt>Extra payable</dt><dd>{{ money(additionalDue) }}</dd></div>
            <div class="flex justify-between gap-3"><dt class="text-muted">Remaining credit</dt><dd>{{ money(remainingCreditPreview) }}</dd></div>
          </dl>
        </div>

        <div class="garmetix-section-card">
          <h3 class="garmetix-panel-title">Additional payment</h3>
          <div class="mt-4 space-y-3">
            <UFormField label="Amount paid now">
              <UInput v-model="exchangeForm.additionalPaidAmount" inputmode="decimal" @input="clampAdditionalPayment" />
            </UFormField>
            <UFormField label="Payment mode">
              <USelect v-model="exchangeForm.additionalPaymentMode" :items="paymentModeOptions" :disabled="additionalDue <= 0" />
            </UFormField>
            <UFormField v-if="additionalRequiresBank" label="Bank account">
              <USelect v-model="exchangeForm.bankAccountId" :items="bankAccountOptions" placeholder="Select bank" />
            </UFormField>
            <UFormField label="Reason / remarks">
              <UTextarea v-model="exchangeForm.reason" :rows="3" />
            </UFormField>
            <UButton block icon="i-lucide-printer" :loading="saving" :disabled="!canSubmitExchange" @click="submitExchange">
              Save & Print Exchange
            </UButton>
            <p class="text-xs text-muted">Shortcuts: F2 focuses the replacement scan box, F4 saves and prints.</p>
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
import { createSalesExchangeRequest } from '../utils/return-contract'

useHead({ title: 'New Sales Exchange - Garmetix POS' })

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

interface ProductLookupItem {
  productId: string
  name: string
  barcode: string
  availableQty: number
  mrp: number
  taxRate: number
  taxType: string
  unit: string
}

interface ReplacementItem extends ProductLookupItem {
  quantity: number
  discountAmount: number
}

interface SalesExchangeResponse {
  returnInvoiceId: string
  creditNoteNumber: string
  exchangeInvoiceId: string
  exchangeInvoiceNumber: string
  creditAmount: number
  appliedCreditAmount: number
  additionalPaidAmount: number
  newInvoiceAmount: number
  remainingStoreCreditAmount: number
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
const saving = ref(false)
const printing = ref(false)
const productLoading = ref(false)
const message = ref('')
const messageTone = ref<'success' | 'error' | 'warning' | 'neutral'>('neutral')
const messageIcon = computed(() => messageTone.value === 'success' ? 'i-lucide-circle-check' : messageTone.value === 'error' ? 'i-lucide-circle-alert' : messageTone.value === 'warning' ? 'i-lucide-triangle-alert' : 'i-lucide-info')
const productSearch = ref('')
const productSearchInput = ref<any>(null)
const quantity = ref('1')
const lineDiscount = ref('')
const invoice = ref<InvoiceHeader | null>(null)
const bankAccounts = ref<any[]>([])
const productSuggestions = ref<ProductLookupItem[]>([])
const returnLines = ref<ReturnLine[]>([])
const replacementCart = ref<ReplacementItem[]>([])
const saved = ref<SalesExchangeResponse | null>(null)
const exchangeForm = reactive({
  additionalPaidAmount: 0,
  additionalPaymentMode: paymentModeValue.cash,
  bankAccountId: null as string | null,
  reason: 'Sales exchange'
})

const api = computed(() => createGarmetixApiClient({
  baseUrl: apiBaseUrl.value,
  getToken: () => import.meta.client ? getStoredToken(window.localStorage) : null
}))

const bankAccountOptions = computed(() => bankAccounts.value.map(account => ({
  value: account.id,
  label: `${account.accountHolderName || account.bankName || 'Bank'} ${account.accountNumber || ''}`.trim()
})))
const returnTotal = computed(() => returnLines.value.reduce((sum, item) => sum + returnLineValue(item), 0))
const replacementTotal = computed(() => replacementCart.value.reduce((sum, item) => sum + replacementLineValue(item), 0))
const additionalDue = computed(() => Math.max(replacementTotal.value - returnTotal.value, 0))
const remainingCreditPreview = computed(() => Math.max(returnTotal.value - replacementTotal.value, 0))
const selectedReturnItemCount = computed(() => returnLines.value.filter(item => Number(item.returnQuantity || 0) > 0).length)
const additionalRequiresBank = computed(() => Number(exchangeForm.additionalPaidAmount || 0) > 0 && Number(exchangeForm.additionalPaymentMode) !== paymentModeValue.cash)
const canSubmitExchange = computed(() => Boolean(
  invoice.value
  && selectedReturnItemCount.value
  && replacementCart.value.length
  && returnTotal.value > 0
  && replacementTotal.value > 0
  && (!additionalRequiresBank.value || exchangeForm.bankAccountId)
  && !saving.value
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

function focusProductSearch() {
  if (!import.meta.client) return
  void nextTick(() => {
    const input = productSearchInput.value?.inputRef
      || productSearchInput.value?.$el?.querySelector?.('input')
      || document.querySelector<HTMLInputElement>('[data-pos-exchange-product-search]')
    input?.focus?.()
    input?.select?.()
  })
}

async function load() {
  if (!import.meta.client || !invoiceId.value) return
  if (!getStoredToken(window.localStorage)) {
    showMessage('warning', 'Login is required before creating a sales exchange.')
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
    showMessage('neutral', `Loaded ${receipt.invoiceNumber}. Select return items and scan replacement.`)
    focusProductSearch()
  } catch (error) {
    showMessage('error', error instanceof Error ? error.message : 'Could not load invoice items.')
  } finally {
    loading.value = false
  }
}

function returnLineValue(item: ReturnLine) {
  return Math.max((Number(item.mrp || 0) - Number(item.discountAmount || 0)) * Number(item.returnQuantity || 0), 0)
}

function replacementLineValue(item: ReplacementItem) {
  return Math.max((Number(item.mrp || 0) - Number(item.discountAmount || 0)) * Number(item.quantity || 0), 0)
}

function clampReturnQuantity(item: ReturnLine) {
  item.returnQuantity = Math.min(Math.max(Number(item.returnQuantity || 0), 0), Number(item.quantity || 0))
  clampAdditionalPayment()
}

function returnAll() {
  for (const item of returnLines.value) item.returnQuantity = Number(item.quantity || 0)
  clampAdditionalPayment()
}

function suggestionLabel(item: ProductLookupItem) {
  return `${item.barcode} | ${item.name} | Qty ${Number(item.availableQty || 0)} | MRP ${Number(item.mrp || 0)}`
}

async function refreshProductSuggestions(query: string) {
  const term = query.trim()
  if (!term) return
  productSuggestions.value = await api.value.get<ProductLookupItem[]>(`product-lookup?query=${encodeURIComponent(term)}&take=25`)
}

async function selectProductFromInput() {
  const value = productSearch.value.trim()
  if (!value) return
  if (!productSuggestions.value.find(item => suggestionLabel(item) === value)) {
    await refreshProductSuggestions(value)
  }
}

async function lookupAndAdd() {
  const value = productSearch.value.trim()
  if (!invoice.value) {
    showMessage('warning', 'Original invoice is not loaded yet.')
    return
  }
  if (!value) {
    showMessage('warning', 'Scan a replacement barcode or type product name.')
    return
  }

  productLoading.value = true
  try {
    let product = productSuggestions.value.find(item => suggestionLabel(item) === value) || null
    if (!product) {
      try {
        product = await api.value.get<ProductLookupItem>(`product-lookup/barcode/${encodeURIComponent(value)}`)
      } catch {
        const rows = await api.value.get<ProductLookupItem[]>(`product-lookup?query=${encodeURIComponent(value)}&take=10`)
        productSuggestions.value = rows
        product = rows[0] || null
      }
    }
    if (!product) {
      showMessage('warning', 'No available replacement product matched the barcode or search.')
      return
    }
    addReplacement(product)
  } catch (error) {
    showMessage('error', error instanceof Error ? error.message : 'Could not load replacement product.')
  } finally {
    productLoading.value = false
  }
}

function discountPerUnit(product: ProductLookupItem) {
  const input = String(lineDiscount.value || '').trim()
  const mrp = Number(product.mrp || 0)
  if (input.endsWith('%')) {
    const percent = Math.min(Math.max(Number(input.slice(0, -1)) || 0, 0), 100)
    return Math.round(mrp * percent) / 100
  }
  return Math.min(Math.max(Number(input) || 0, 0), mrp)
}

function addReplacement(product: ProductLookupItem) {
  const addQuantity = Number(quantity.value || 0)
  if (addQuantity <= 0) {
    showMessage('warning', 'Replacement quantity must be greater than zero.')
    return
  }
  if (Number(product.availableQty || 0) < addQuantity) {
    showMessage('warning', `Available replacement quantity is ${Number(product.availableQty || 0)}.`)
    return
  }

  const discount = discountPerUnit(product)
  const existing = replacementCart.value.find(item => item.productId === product.productId && item.barcode === product.barcode && item.discountAmount === discount)
  if (existing) {
    existing.quantity += addQuantity
  } else {
    replacementCart.value.push({ ...product, quantity: addQuantity, discountAmount: discount })
  }
  productSearch.value = ''
  quantity.value = '1'
  lineDiscount.value = ''
  clampAdditionalPayment()
  focusProductSearch()
}

function removeReplacement(index: number) {
  replacementCart.value.splice(index, 1)
  clampAdditionalPayment()
}

function clampAdditionalPayment() {
  exchangeForm.additionalPaidAmount = Math.min(Math.max(Number(exchangeForm.additionalPaidAmount || 0), 0), additionalDue.value)
  if (!additionalRequiresBank.value) exchangeForm.bankAccountId = null
}

function validateExchange() {
  if (!invoice.value) return 'Original invoice is not loaded.'
  const invalidLine = returnLines.value.find(item => Number(item.returnQuantity || 0) > Number(item.quantity || 0))
  if (invalidLine) return `Return quantity for ${invalidLine.barcode} is more than sold quantity.`
  if (!selectedReturnItemCount.value) return 'Enter return quantity for at least one original item.'
  if (!replacementCart.value.length) return 'Add at least one replacement item.'
  if (Number(exchangeForm.additionalPaidAmount || 0) > additionalDue.value) return 'Additional payment cannot be more than extra payable amount.'
  if (additionalRequiresBank.value && !exchangeForm.bankAccountId) return 'Select bank account for non-cash additional payment.'
  return ''
}

async function submitExchange() {
  if (saving.value || saved.value) return
  const validation = validateExchange()
  if (validation) {
    showMessage('warning', validation)
    return
  }
  if (!invoice.value) return

  saving.value = true
  try {
    const response = await api.value.post<SalesExchangeResponse>(`billing/sales/${invoice.value.id}/exchange`, createSalesExchangeRequest({
      additionalPaidAmount: Number(exchangeForm.additionalPaidAmount || 0),
      additionalPaymentMode: Number(exchangeForm.additionalPaidAmount || 0) > 0 ? Number(exchangeForm.additionalPaymentMode) : null,
      bankAccountId: additionalRequiresBank.value ? exchangeForm.bankAccountId : null,
      reason: exchangeForm.reason,
      returnItems: returnLines.value
        .filter(item => Number(item.returnQuantity || 0) > 0)
        .map(item => ({ invoiceItemId: item.invoiceItemId, quantity: Number(item.returnQuantity || 0) })),
      newItems: replacementCart.value.map(item => ({
        productId: item.productId,
        barcode: item.barcode,
        quantity: Number(item.quantity || 0),
        mrp: Number(item.mrp || 0),
        discountAmount: Number(item.discountAmount || 0)
      }))
    }))

    saved.value = response
    addPrintQueueItem({
      invoiceId: response.exchangeInvoiceId,
      invoiceNumber: response.exchangeInvoiceNumber || '',
      customerName: invoice.value.customerName || 'Walk-in Customer',
      billAmount: Number(response.newInvoiceAmount || replacementTotal.value),
      savedAt: new Date().toISOString()
    })
    try {
      await printExchange(response.exchangeInvoiceId, false)
      showMessage('success', `Exchange ${response.exchangeInvoiceNumber || ''} saved and opened for printing.`.trim())
    } catch (printError) {
      showMessage('warning', printError instanceof Error
        ? `Exchange ${response.exchangeInvoiceNumber || ''} saved. ${printError.message}`.trim()
        : `Exchange ${response.exchangeInvoiceNumber || ''} saved. Use Print again or the Print Queue.`.trim())
    }
  } catch (error) {
    showMessage('error', error instanceof Error ? error.message : 'Could not create sales exchange.')
  } finally {
    saving.value = false
  }
}

async function printExchange(exchangeInvoiceId: string, reprint: boolean) {
  await openBillingInvoicePdf({
    apiBaseUrl: apiBaseUrl.value,
    invoiceId: exchangeInvoiceId,
    token: getStoredToken(window.localStorage),
    reprint
  })
}

async function printSaved() {
  if (!saved.value || printing.value) return
  printing.value = true
  try {
    await printExchange(saved.value.exchangeInvoiceId, true)
  } catch (error) {
    showMessage('error', error instanceof Error ? error.message : 'Could not print exchange.')
  } finally {
    printing.value = false
  }
}

function addPrintQueueItem(item: PosPrintQueueItem) {
  if (!import.meta.client || !item.invoiceId) return
  upsertPrintQueueItem(item)
}

function handleExchangeShortcut(event: KeyboardEvent) {
  if (event.defaultPrevented) return
  if (event.key === 'F2') {
    event.preventDefault()
    focusProductSearch()
  }
  if (event.key === 'F4') {
    event.preventDefault()
    void submitExchange()
  }
}

watch(productSearch, (value) => {
  if (value.trim().length >= 2) {
    void refreshProductSuggestions(value)
  }
})

watch(() => [exchangeForm.additionalPaidAmount, exchangeForm.additionalPaymentMode, returnTotal.value, replacementTotal.value], () => {
  clampAdditionalPayment()
  if (additionalRequiresBank.value && !exchangeForm.bankAccountId) {
    exchangeForm.bankAccountId = bankAccountOptions.value[0]?.value || null
  }
})

onMounted(() => {
  window.addEventListener('keydown', handleExchangeShortcut)
  void load()
})

onBeforeUnmount(() => {
  window.removeEventListener('keydown', handleExchangeShortcut)
})
</script>
