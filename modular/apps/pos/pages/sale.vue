<template>
  <section class="space-y-4" :aria-busy="loading">
    <div class="border border-default bg-muted/10 p-4">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="text-sm text-muted">Counter billing</p>
          <h2 class="mt-1 text-2xl font-semibold">New Sale</h2>
          <p class="mt-2 max-w-2xl text-sm text-muted">
            Draft, scan, total, save and print using the existing Garmetix billing API.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton color="neutral" variant="soft" icon="i-lucide-refresh-cw" :loading="loading" @click="refresh">Refresh</UButton>
          <UButton color="neutral" variant="soft" icon="i-lucide-pause-circle" :loading="holding" :disabled="!cart.length || holding" @click="holdCurrentBill">Hold Bill</UButton>
          <UButton icon="i-lucide-printer" :loading="saving" :disabled="!canSave" @click="saveAndPrint">Save & Print</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="message" :color="messageTone" variant="subtle" :icon="messageIcon" :description="message" />

    <section class="grid gap-4 xl:grid-cols-[minmax(0,1fr)_320px]">
      <div class="space-y-4">
        <div class="grid gap-3 border border-default bg-muted/10 p-4 md:grid-cols-2 xl:grid-cols-4">
          <UFormField label="Store" name="storeId">
            <USelect v-model="form.storeId" :items="storeOptions" placeholder="Select store" @change="onStoreChanged" />
          </UFormField>
          <UFormField label="Salesman" name="salesmanId">
            <USelect v-model="form.salesmanId" :items="salesmanOptions" placeholder="Manager" />
          </UFormField>
          <UFormField label="Customer mobile" name="customerMobileNumber">
            <UInput v-model="form.customerMobileNumber" icon="i-lucide-phone" placeholder="Optional" @keyup.enter="searchCustomer" />
          </UFormField>
          <UFormField label="Customer name" name="customerName">
            <UInput v-model="form.customerName" icon="i-lucide-user-round" />
          </UFormField>
        </div>

        <div class="grid gap-3 border border-default bg-muted/10 p-4 lg:grid-cols-[1fr_1fr_auto]">
          <UFormField label="Customer search" name="customerSearch">
            <UInput v-model="customerSearch" icon="i-lucide-search" placeholder="Mobile, name or GSTIN" @keyup.enter="searchCustomer" />
          </UFormField>
          <UFormField label="Matched customer" name="customerMatch">
            <USelect v-model="selectedCustomerId" :items="customerMatchOptions" placeholder="Walk-in / new customer" @change="selectCustomer" />
          </UFormField>
          <div class="flex items-end gap-2">
            <UButton icon="i-lucide-search" :loading="searchingCustomer" @click="searchCustomer">Search</UButton>
            <UButton color="neutral" variant="soft" icon="i-lucide-user-x" @click="clearCustomer">Clear</UButton>
          </div>
          <UFormField label="Customer GSTIN" name="customerGstin">
            <UInput v-model="form.customerGstin" icon="i-lucide-badge-indian-rupee" placeholder="Optional B2B GSTIN" />
          </UFormField>
          <div v-if="selectedCustomerProfile" class="grid gap-2 text-sm lg:col-span-2 lg:grid-cols-4">
            <div class="border border-default p-3"><p class="text-muted">Credit</p><strong>{{ money(selectedCustomerProfile.customer?.creditBalance || 0) }}</strong></div>
            <div class="border border-default p-3"><p class="text-muted">Loyalty points</p><strong>{{ Number(selectedCustomerProfile.customer?.loyaltyPoints || 0) }}</strong></div>
            <div class="border border-default p-3"><p class="text-muted">Credit notes</p><strong>{{ selectedCustomerProfile.creditNotes?.length || 0 }}</strong></div>
            <div class="border border-default p-3"><p class="text-muted">Advances</p><strong>{{ selectedCustomerProfile.advanceReceipts?.length || 0 }}</strong></div>
          </div>
        </div>

        <div class="grid gap-3 border border-default bg-muted/10 p-4 lg:grid-cols-[1fr_110px_110px_auto]">
          <UFormField label="Barcode / product" name="productSearch">
            <UInput
              ref="productSearchInput"
              v-model="productSearch"
              list="pos-product-suggestions"
              icon="i-lucide-scan-line"
              placeholder="Scan barcode or type product name"
              autofocus
              data-pos-product-search
              @change="selectProductFromInput"
              @keyup.enter="lookupAndAdd"
            />
            <datalist id="pos-product-suggestions">
              <option v-for="item in productSuggestions" :key="`${item.productId}:${item.barcode}`" :value="suggestionLabel(item)" />
            </datalist>
          </UFormField>
          <UFormField label="Qty" name="quantity">
            <UInput v-model="quantity" inputmode="decimal" />
          </UFormField>
          <UFormField label="Disc / unit" name="discount">
            <UInput v-model="lineDiscount" placeholder="0 or 10%" />
          </UFormField>
          <div class="flex items-end">
            <UButton class="w-full" icon="i-lucide-plus" :loading="productLoading" @click="lookupAndAdd">Add</UButton>
          </div>
        </div>

        <div class="overflow-x-auto border border-default">
          <table class="w-full min-w-[760px] border-collapse text-sm">
            <thead class="bg-muted/30 text-left text-xs uppercase text-muted">
              <tr>
                <th class="border-b border-default p-3">Item</th>
                <th class="border-b border-default p-3">Barcode</th>
                <th class="border-b border-default p-3 text-right">Qty</th>
                <th class="border-b border-default p-3 text-right">MRP</th>
                <th class="border-b border-default p-3 text-right">Discount</th>
                <th class="border-b border-default p-3 text-right">Tax</th>
                <th class="border-b border-default p-3 text-right">Total</th>
                <th class="border-b border-default p-3"></th>
              </tr>
            </thead>
            <tbody>
              <tr v-if="!cart.length">
                <td colspan="8" class="p-6 text-center text-muted">Scan or search a product to begin the invoice.</td>
              </tr>
              <tr v-for="(item, index) in cart" :key="`${item.productId}:${item.barcode}:${index}`">
                <td class="border-b border-default p-3">
                  <strong>{{ item.name }}</strong>
                  <small class="block text-muted">{{ item.unit || 'Unit' }} | Available {{ item.availableQty }}</small>
                </td>
                <td class="border-b border-default p-3">{{ item.barcode }}</td>
                <td class="border-b border-default p-3 text-right">{{ item.quantity }}</td>
                <td class="border-b border-default p-3 text-right">{{ money(item.mrp) }}</td>
                <td class="border-b border-default p-3 text-right">{{ money(item.discountAmount) }}</td>
                <td class="border-b border-default p-3 text-right">{{ item.taxRate }}%</td>
                <td class="border-b border-default p-3 text-right font-semibold">{{ money(lineTotal(item)) }}</td>
                <td class="border-b border-default p-3 text-right">
                  <UButton color="error" variant="ghost" size="xs" icon="i-lucide-trash-2" @click="removeItem(index)" />
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <aside class="space-y-4">
        <div class="border border-default bg-muted/10 p-4">
          <h3 class="text-base font-semibold">Payment</h3>
          <div class="mt-4 space-y-3">
            <div v-for="(payment, index) in payments" :key="index" class="space-y-2 border border-default p-3">
              <div class="grid grid-cols-2 gap-2">
                <UFormField label="Mode">
                  <USelect v-model="payment.paymentMode" :items="paymentModeOptions" @change="syncCashPayment" />
                </UFormField>
                <UFormField label="Amount">
                  <UInput v-model="payment.amount" inputmode="decimal" @input="syncCashPayment(false)" />
                </UFormField>
              </div>
              <UFormField v-if="paymentRequiresBank(payment)" label="Bank account">
                <USelect v-model="payment.bankAccountId" :items="bankAccountOptions" placeholder="Select bank" />
              </UFormField>
              <UFormField v-if="paymentRequiresBank(payment)" label="Reference">
                <UInput v-model="payment.referenceNumber" :placeholder="paymentReferenceLabel(payment)" />
              </UFormField>
              <UButton v-if="payments.length > 1" color="error" variant="ghost" size="xs" icon="i-lucide-trash-2" @click="removePayment(index)">
                Remove payment
              </UButton>
            </div>
            <UButton color="neutral" variant="soft" icon="i-lucide-plus" block @click="addPayment">Add payment row</UButton>
          </div>
        </div>

        <div class="border border-default bg-muted/10 p-4">
          <h3 class="text-base font-semibold">Customer adjustments</h3>
          <div class="mt-4 space-y-3">
            <UFormField label="Store credit">
              <UInput v-model="adjustments.storeCreditAmount" inputmode="decimal" :disabled="!selectedCustomerProfile" @input="syncCashPayment" />
            </UFormField>
            <UFormField label="Credit note">
              <USelect v-model="adjustments.creditNoteId" :items="creditNoteOptions" placeholder="Select credit note" :disabled="!creditNoteOptions.length" />
            </UFormField>
            <UFormField label="Credit note amount">
              <UInput v-model="adjustments.creditNoteAmount" inputmode="decimal" :disabled="!adjustments.creditNoteId" @input="syncCashPayment" />
            </UFormField>
            <UFormField label="Advance receipt">
              <USelect v-model="adjustments.advanceReceiptId" :items="advanceOptions" placeholder="Select advance" :disabled="!advanceOptions.length" />
            </UFormField>
            <UFormField label="Advance amount">
              <UInput v-model="adjustments.advanceAmount" inputmode="decimal" :disabled="!adjustments.advanceReceiptId" @input="syncCashPayment" />
            </UFormField>
            <UFormField label="Loyalty points">
              <UInput v-model="adjustments.loyaltyPointsToRedeem" inputmode="decimal" :disabled="!selectedCustomerProfile?.loyaltyProgram?.enabled" @input="syncCashPayment" />
            </UFormField>
          </div>
        </div>

        <div class="border border-default bg-muted/10 p-4">
          <h3 class="text-base font-semibold">Totals</h3>
          <dl class="mt-4 space-y-2 text-sm">
            <div class="flex justify-between gap-3"><dt class="text-muted">Items</dt><dd>{{ cart.length }}</dd></div>
            <div class="flex justify-between gap-3"><dt class="text-muted">Quantity</dt><dd>{{ totalQuantity }}</dd></div>
            <div class="flex justify-between gap-3"><dt class="text-muted">Gross</dt><dd>{{ money(cartTotal) }}</dd></div>
            <div class="flex items-center justify-between gap-3">
              <dt class="text-muted">Bill discount</dt>
              <dd class="w-28"><UInput v-model="form.billDiscountAmount" size="xs" inputmode="decimal" @input="syncCashPayment" /></dd>
            </div>
            <div class="flex justify-between gap-3"><dt class="text-muted">Round off</dt><dd>{{ money(roundOff) }}</dd></div>
            <div class="flex justify-between gap-3 border-t border-default pt-2 text-lg font-semibold"><dt>Payable</dt><dd>{{ money(payableTotal) }}</dd></div>
            <div class="flex justify-between gap-3"><dt class="text-muted">Adjustments</dt><dd>{{ money(adjustmentTotal) }}</dd></div>
            <div class="flex justify-between gap-3"><dt class="text-muted">Paid</dt><dd>{{ money(paymentTotal) }}</dd></div>
            <div class="flex justify-between gap-3"><dt class="text-muted">Balance</dt><dd>{{ money(paymentBalance) }}</dd></div>
          </dl>
          <UButton class="mt-4" block icon="i-lucide-printer" :loading="saving" :disabled="!canSave" @click="saveAndPrint">Save & Print</UButton>
        </div>
      </aside>
    </section>
  </section>
</template>

<script setup lang="ts">
import { createGarmetixApiClient } from '@garmetix/shared-api'
import { getStoredToken, getStoredUser } from '@garmetix/shared-auth'
import { formatIndianMoney } from '@garmetix/shared-utils'
import {
  clearSaleDraft,
  createLocalPosId,
  readSaleDraft,
  removeHeldBill,
  upsertHeldBill,
  upsertPrintQueueItem,
  writeSaleDraft,
  type PosHeldBill,
  type PosPrintQueueItem
} from '../utils/local-pos-storage'
import { openBillingInvoicePdf } from '../utils/pos-documents'
import { createPosSaleRequest, type PosSalePaymentPayload } from '../utils/sale-contract'

useHead({ title: 'New Sale - Garmetix POS' })

interface ProductLookupItem {
  productId: string
  name: string
  barcode: string
  hsnCode?: string
  availableQty: number
  mrp: number
  taxRate: number
  taxType: string
  unit: string
}

interface CartItem extends ProductLookupItem {
  quantity: number
  discountAmount: number
}

interface PaymentRow {
  paymentMode: number
  amount: string | number
  bankAccountId: string | null
  referenceNumber: string
}

interface CustomerProfile {
  customer?: {
    id: string
    name: string
    mobileNumber: string
    gstin?: string
    creditBalance?: number
    loyaltyPoints?: number
  }
  creditNotes?: AdjustmentOption[]
  advanceReceipts?: AdjustmentOption[]
  loyaltyProgram?: {
    enabled: boolean
    redeemValuePerPoint: number
    earnPointsPerRupee: number
    minimumBillAmount: number
  }
}

interface AdjustmentOption {
  id: string
  number: string
  availableAmount: number
  sourceType: string
  referenceNumber?: string
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
  demandDraft: 8,
  creditNote: 9,
  coupons: 11,
  creditBalance: 15,
  mixPayments: 12
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
const saving = ref(false)
const holding = ref(false)
const productLoading = ref(false)
const message = ref('')
const messageTone = ref<'success' | 'error' | 'warning' | 'neutral'>('neutral')
const messageIcon = computed(() => messageTone.value === 'success' ? 'i-lucide-circle-check' : messageTone.value === 'error' ? 'i-lucide-circle-alert' : messageTone.value === 'warning' ? 'i-lucide-triangle-alert' : 'i-lucide-info')
const setupStatus = ref<any | null>(null)
const stores = ref<any[]>([])
const salesmen = ref<any[]>([])
const bankAccounts = ref<any[]>([])
const productSuggestions = ref<ProductLookupItem[]>([])
const selectedProduct = ref<ProductLookupItem | null>(null)
const customerMatches = ref<any[]>([])
const selectedCustomerProfile = ref<CustomerProfile | null>(null)
const selectedCustomerId = ref<string | null>(null)
const customerSearch = ref('')
const searchingCustomer = ref(false)
const productSearch = ref('')
const productSearchInput = ref<any>(null)
const quantity = ref('1')
const lineDiscount = ref('')
const cart = ref<CartItem[]>([])
const payments = ref<PaymentRow[]>([emptyPayment(0)])
const form = reactive({
  companyId: '',
  storeGroupId: '',
  storeId: '',
  customerId: null as string | null,
  customerName: 'Walk-in Customer',
  customerMobileNumber: '',
  customerGstin: '',
  salesmanId: null as string | null,
  billDiscountAmount: 0
})
const adjustments = reactive({
  storeCreditAmount: 0,
  loyaltyPointsToRedeem: 0,
  creditNoteId: null as string | null,
  creditNoteAmount: 0,
  advanceReceiptId: null as string | null,
  advanceAmount: 0
})

const api = computed(() => createGarmetixApiClient({
  baseUrl: apiBaseUrl.value,
  getToken: () => import.meta.client ? getStoredToken(window.localStorage) : null
}))

const storeOptions = computed(() => stores.value.map(store => ({
  value: store.id,
  label: store.name || store.storeName || 'Store'
})))
const salesmanOptions = computed(() => salesmen.value.map(salesman => ({
  value: salesman.id,
  label: salesman.name || 'Salesman'
})))
const bankAccountOptions = computed(() => bankAccounts.value
  .filter(account => !form.companyId || account.companyId === form.companyId)
  .map(account => ({
    value: account.id,
    label: `${account.accountHolderName || account.bankName || 'Bank'} ${account.accountNumber || ''}`.trim()
  })))
const customerMatchOptions = computed(() => customerMatches.value.map(customer => ({
  value: customer.id,
  label: customer.label || `${customer.name || 'Customer'} ${customer.mobileNumber || ''}`.trim()
})))
const creditNoteOptions = computed(() => (selectedCustomerProfile.value?.creditNotes || []).map(item => ({
  value: item.id,
  label: `${item.number} | Available ${money(item.availableAmount)}`
})))
const advanceOptions = computed(() => (selectedCustomerProfile.value?.advanceReceipts || []).map(item => ({
  value: item.id,
  label: `${item.number} | Available ${money(item.availableAmount)}`
})))
const selectedCreditNote = computed(() => (selectedCustomerProfile.value?.creditNotes || []).find(item => item.id === adjustments.creditNoteId))
const selectedAdvance = computed(() => (selectedCustomerProfile.value?.advanceReceipts || []).find(item => item.id === adjustments.advanceReceiptId))
const storeCreditAmount = computed(() => Math.min(
  Number(adjustments.storeCreditAmount || 0),
  Number(selectedCustomerProfile.value?.customer?.creditBalance || 0)
))
const creditNoteAmount = computed(() => Math.min(Number(adjustments.creditNoteAmount || 0), Number(selectedCreditNote.value?.availableAmount || 0)))
const advanceAmount = computed(() => Math.min(Number(adjustments.advanceAmount || 0), Number(selectedAdvance.value?.availableAmount || 0)))
const loyaltyValue = computed(() => {
  const profile = selectedCustomerProfile.value
  const points = Math.min(Number(adjustments.loyaltyPointsToRedeem || 0), Number(profile?.customer?.loyaltyPoints || 0))
  return Math.max(points * Number(profile?.loyaltyProgram?.redeemValuePerPoint || 0), 0)
})
const adjustmentTotal = computed(() => storeCreditAmount.value + creditNoteAmount.value + advanceAmount.value + loyaltyValue.value)
const cartTotal = computed(() => cart.value.reduce((sum, item) => sum + lineTotal(item), 0))
const totalQuantity = computed(() => cart.value.reduce((sum, item) => sum + Number(item.quantity || 0), 0))
const unroundedPayable = computed(() => Math.max(cartTotal.value - Number(form.billDiscountAmount || 0), 0))
const payableTotal = computed(() => Math.round(unroundedPayable.value))
const roundOff = computed(() => payableTotal.value - unroundedPayable.value)
const manualPaymentTotal = computed(() => payments.value.reduce((sum, item) => sum + Number(item.amount || 0), 0))
const paymentTotal = computed(() => manualPaymentTotal.value + adjustmentTotal.value)
const paymentBalance = computed(() => Math.max(payableTotal.value - paymentTotal.value, 0))
const canSave = computed(() => Boolean(form.companyId && form.storeGroupId && form.storeId && cart.value.length && !saving.value))

function emptyPayment(amount = 0): PaymentRow {
  return {
    paymentMode: paymentModeValue.cash,
    amount,
    bankAccountId: null,
    referenceNumber: ''
  }
}

function money(value: number | string | null | undefined) {
  return formatIndianMoney(value)
}

function showMessage(tone: typeof messageTone.value, text: string) {
  messageTone.value = tone
  message.value = text
}

function normalizeGstin(value: string) {
  return String(value || '').trim().toUpperCase()
}

function isValidGstin(value: string) {
  if (!value) return true
  return /^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][1-9A-Z]Z[0-9A-Z]$/.test(normalizeGstin(value))
}

function focusProductSearch() {
  if (!import.meta.client) return
  void nextTick(() => {
    const input = productSearchInput.value?.inputRef
      || productSearchInput.value?.$el?.querySelector?.('input')
      || document.querySelector<HTMLInputElement>('[data-pos-product-search]')
    input?.focus?.()
    input?.select?.()
  })
}

function handleCounterShortcut(event: KeyboardEvent) {
  if (event.defaultPrevented) return
  if (event.key === 'F2') {
    event.preventDefault()
    focusProductSearch()
  }
  if (event.key === 'F4') {
    event.preventDefault()
    void saveAndPrint()
  }
  if (event.key === 'F8') {
    event.preventDefault()
    addPayment()
  }
  if (event.key === 'F9') {
    event.preventDefault()
    void holdCurrentBill()
  }
  if (event.key === 'Escape' && productSearch.value) {
    event.preventDefault()
    productSearch.value = ''
    selectedProduct.value = null
    focusProductSearch()
  }
}

function paymentRequiresBank(payment: PaymentRow) {
  return Number(payment.paymentMode) !== paymentModeValue.cash
}

function paymentReferenceLabel(payment: PaymentRow) {
  const mode = Number(payment.paymentMode)
  if (mode === paymentModeValue.upi) return 'UPI transaction / UTR'
  if ([paymentModeValue.imps, paymentModeValue.rtgs, paymentModeValue.neft].includes(mode)) return 'UTR / bank reference'
  if (mode === paymentModeValue.cheque) return 'Cheque number'
  if (mode === paymentModeValue.demandDraft) return 'Demand draft number'
  if (mode === paymentModeValue.card) return 'Card authorization / reference'
  if (mode === paymentModeValue.wallets) return 'Wallet transaction reference'
  return 'Reference'
}

async function refresh() {
  if (!import.meta.client) return
  const token = getStoredToken(window.localStorage)
  if (!token) {
    showMessage('warning', 'Login is required before loading POS sale data.')
    return
  }

  loading.value = true
  message.value = ''
  try {
    const storedUser = getStoredUser(window.localStorage)
    const [status, storeRows, bankRows] = await Promise.all([
      api.value.get<any>('setup/status'),
      api.value.get<any[]>('stores'),
      api.value.get<any[]>('bank-accounts')
    ])
    setupStatus.value = status
    stores.value = storeRows
    bankAccounts.value = bankRows
    form.companyId = storedUser?.companyId || status?.companyId || form.companyId || storeRows[0]?.companyId || ''
    form.storeGroupId = storedUser?.storeGroupId || status?.storeGroupId || form.storeGroupId || storeRows[0]?.storeGroupId || ''
    form.storeId = storedUser?.storeId || status?.storeId || form.storeId || storeRows[0]?.id || ''
    await loadBillingOptions()
    restoreDraft()
    if (form.customerId) await loadCustomerProfile(form.customerId)
    syncCashPayment()
  } catch (error) {
    showMessage('error', error instanceof Error ? error.message : 'Could not load POS sale data.')
  } finally {
    loading.value = false
  }
}

async function loadBillingOptions() {
  const query = new URLSearchParams({ take: '25' })
  if (form.companyId) query.set('companyId', form.companyId)
  if (form.storeId) query.set('storeId', form.storeId)
  const options = await api.value.get<any>(`billing/options?${query.toString()}`)
  salesmen.value = options?.salesmen || []
  setDefaultSalesman()
}

async function onStoreChanged() {
  const store = stores.value.find(item => item.id === form.storeId)
  form.companyId = store?.companyId || form.companyId
  form.storeGroupId = store?.storeGroupId || form.storeGroupId
  await loadBillingOptions()
  saveDraft()
}

function setDefaultSalesman() {
  if (form.salesmanId) return
  const manager = salesmen.value.find(item => String(item.name || '').trim().toLowerCase() === 'manager')
  form.salesmanId = manager?.id || salesmen.value[0]?.id || null
}

async function searchCustomer() {
  const term = String(customerSearch.value || form.customerMobileNumber || form.customerName || '').trim()
  if (!term || term === 'Walk-in Customer') {
    showMessage('warning', 'Enter customer mobile, name, or GSTIN to search.')
    return
  }

  searchingCustomer.value = true
  try {
    const query = new URLSearchParams({ q: term, take: '10' })
    if (form.companyId) query.set('companyId', form.companyId)
    customerMatches.value = await api.value.get<any[]>(`billing/customers/search?${query.toString()}`)
    const exact = customerMatches.value.filter(customer => String(customer.mobileNumber || '').trim() === term)
    if (exact.length === 1) {
      selectedCustomerId.value = exact[0].id
      await selectCustomer()
      return
    }
    if (!customerMatches.value.length) {
      form.customerId = null
      selectedCustomerId.value = null
      selectedCustomerProfile.value = null
      if (!form.customerName || form.customerName === 'Walk-in Customer') form.customerName = 'New Customer'
      showMessage('neutral', 'No existing customer matched. This customer will be saved with the invoice.')
    }
  } catch (error) {
    showMessage('error', error instanceof Error ? error.message : 'Could not search customer.')
  } finally {
    searchingCustomer.value = false
  }
}

async function selectCustomer() {
  const customer = customerMatches.value.find(item => item.id === selectedCustomerId.value)
  if (!customer) return
  form.customerId = customer.id
  form.customerName = customer.name || 'Customer'
  form.customerMobileNumber = customer.mobileNumber || ''
  form.customerGstin = customer.gstin || ''
  await loadCustomerProfile(customer.id)
  saveDraft()
}

async function loadCustomerProfile(customerId: string) {
  try {
    const query = new URLSearchParams()
    if (form.storeId) query.set('storeId', form.storeId)
    selectedCustomerProfile.value = await api.value.get<CustomerProfile>(`billing/customers/${customerId}/profile?${query.toString()}`)
  } catch (error) {
    selectedCustomerProfile.value = null
    showMessage('error', error instanceof Error ? error.message : 'Could not load customer balance.')
  }
}

function clearCustomer() {
  form.customerId = null
  form.customerName = 'Walk-in Customer'
  form.customerMobileNumber = ''
  form.customerGstin = ''
  customerSearch.value = ''
  selectedCustomerId.value = null
  customerMatches.value = []
  selectedCustomerProfile.value = null
  resetAdjustments()
  syncCashPayment()
}

function resetAdjustments() {
  adjustments.storeCreditAmount = 0
  adjustments.loyaltyPointsToRedeem = 0
  adjustments.creditNoteId = null
  adjustments.creditNoteAmount = 0
  adjustments.advanceReceiptId = null
  adjustments.advanceAmount = 0
}

function suggestionLabel(item: ProductLookupItem) {
  return `${item.barcode} | ${item.name} | Qty ${Number(item.availableQty || 0)} | MRP ${Number(item.mrp || 0)}`
}

async function refreshProductSuggestions(query: string) {
  const term = query.trim()
  if (!term) return
  productSuggestions.value = await api.value.get<ProductLookupItem[]>(`product-lookup?query=${encodeURIComponent(term)}${form.storeId ? `&storeId=${form.storeId}` : ''}&take=25`)
}

async function selectProductFromInput() {
  const value = productSearch.value.trim()
  if (!value) return
  selectedProduct.value = productSuggestions.value.find(item => suggestionLabel(item) === value) || null
  if (!selectedProduct.value) await refreshProductSuggestions(value)
}

async function lookupAndAdd() {
  const value = productSearch.value.trim()
  if (!value) {
    showMessage('warning', 'Scan a barcode or type product name.')
    return
  }

  productLoading.value = true
  try {
    let product = productSuggestions.value.find(item => suggestionLabel(item) === value) || null
    if (!product) {
      try {
        product = await api.value.get<ProductLookupItem>(`product-lookup/barcode/${encodeURIComponent(value)}${form.storeId ? `?storeId=${form.storeId}` : ''}`)
      } catch {
        const rows = await api.value.get<ProductLookupItem[]>(`product-lookup?query=${encodeURIComponent(value)}${form.storeId ? `&storeId=${form.storeId}` : ''}&take=10`)
        productSuggestions.value = rows
        product = rows[0] || null
      }
    }
    if (!product) {
      showMessage('warning', 'No available product matched the barcode or search.')
      return
    }
    addItem(product)
  } catch (error) {
    showMessage('error', error instanceof Error ? error.message : 'Could not load product.')
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

function addItem(product: ProductLookupItem) {
  const addQuantity = Number(quantity.value || 0)
  if (addQuantity <= 0) {
    showMessage('warning', 'Quantity must be greater than zero.')
    return
  }
  if (Number(product.availableQty || 0) < addQuantity) {
    showMessage('warning', `Available quantity is ${Number(product.availableQty || 0)}.`)
    return
  }

  const discount = discountPerUnit(product)
  const existing = cart.value.find(item => item.productId === product.productId && item.barcode === product.barcode && item.discountAmount === discount)
  if (existing) {
    existing.quantity += addQuantity
  } else {
    cart.value.push({ ...product, quantity: addQuantity, discountAmount: discount })
  }
  productSearch.value = ''
  selectedProduct.value = null
  quantity.value = '1'
  lineDiscount.value = ''
  syncCashPayment()
  saveDraft()
}

function removeItem(index: number) {
  cart.value.splice(index, 1)
  syncCashPayment()
  saveDraft()
}

function lineTotal(item: CartItem) {
  return Math.max((Number(item.mrp || 0) - Number(item.discountAmount || 0)) * Number(item.quantity || 0), 0)
}

function addPayment() {
  payments.value.push(emptyPayment(paymentBalance.value))
  saveDraft()
}

function removePayment(index: number) {
  payments.value.splice(index, 1)
  if (!payments.value.length) payments.value.push(emptyPayment(paymentBalance.value))
  saveDraft()
}

function syncCashPayment(autoFill = true) {
  if (autoFill && payments.value.length === 1 && Number(payments.value[0]?.paymentMode) === paymentModeValue.cash) {
    payments.value[0].amount = payableTotal.value
  }
  saveDraft()
}

function buildSaleDraft() {
  return {
    form: { ...form },
    cart: cart.value,
    payments: payments.value,
    adjustments: { ...adjustments }
  }
}

async function holdCurrentBill() {
  if (holding.value) return
  if (!cart.value.length) {
    showMessage('warning', 'Add at least one item before holding the bill.')
    return
  }

  holding.value = true
  const heldBill: PosHeldBill = {
    id: createLocalPosId('held'),
    clientHeldBillId: createLocalPosId('held-client'),
    heldAt: new Date().toISOString(),
    customerName: form.customerName || 'Walk-in Customer',
    customerMobileNumber: form.customerMobileNumber || '',
    itemCount: cart.value.length,
    quantity: totalQuantity.value,
    payableTotal: payableTotal.value,
    note: cart.value.map(item => item.name).slice(0, 3).join(', '),
    companyId: form.companyId,
    storeGroupId: form.storeGroupId,
    storeId: form.storeId,
    draft: buildSaleDraft()
  }
  upsertHeldBill(heldBill)
  try {
    const saved = await api.value.post<PosHeldBill>('pos/held-bills', {
      ...heldBill,
      id: null,
      clientHeldBillId: heldBill.clientHeldBillId || heldBill.id
    })
    removeHeldBill(heldBill.id)
    upsertHeldBill(saved)
    showMessage('success', 'Bill held on server. Open Hold Bills to resume it from any POS browser.')
  } catch {
    showMessage('warning', 'Bill held locally in this POS browser. API sync was not available.')
  } finally {
    clearDraft()
    resetForNext()
    holding.value = false
  }
}

function buildPayments(): PosSalePaymentPayload[] {
  const rows = payments.value
    .filter(item => Number(item.amount || 0) > 0)
    .map(item => ({
      paymentMode: Number(item.paymentMode),
      amount: Number(item.amount || 0),
      bankAccountId: paymentRequiresBank(item) ? item.bankAccountId : null,
      referenceNumber: item.referenceNumber || null,
      gatewayReference: null,
      settlementStatus: null,
      adjustmentSourceType: null,
      adjustmentSourceId: null
    }))
  if (storeCreditAmount.value > 0) rows.push(adjustmentPayment(paymentModeValue.creditBalance, storeCreditAmount.value, 'Customer credit balance', 'CustomerCreditBalance'))
  if (creditNoteAmount.value > 0 && adjustments.creditNoteId) {
    rows.push(adjustmentPayment(paymentModeValue.creditNote, creditNoteAmount.value, selectedCreditNote.value?.number || 'Credit note', 'CreditNote', adjustments.creditNoteId))
  }
  if (advanceAmount.value > 0 && adjustments.advanceReceiptId) {
    rows.push(adjustmentPayment(paymentModeValue.creditBalance, advanceAmount.value, selectedAdvance.value?.number || 'Customer advance', 'CustomerAdvanceReceipt', adjustments.advanceReceiptId))
  }
  if (loyaltyValue.value > 0) {
    rows.push(adjustmentPayment(paymentModeValue.coupons, loyaltyValue.value, `${adjustments.loyaltyPointsToRedeem} loyalty points`, 'LoyaltyRedemption'))
  }
  return rows
}

function adjustmentPayment(paymentMode: number, amount: number, referenceNumber: string, sourceType: string, sourceId: string | null = null) {
  return {
    paymentMode,
    amount,
    bankAccountId: null,
    referenceNumber,
    gatewayReference: null,
    settlementStatus: null,
    adjustmentSourceType: sourceType,
    adjustmentSourceId: sourceId
  }
}

async function saveAndPrint() {
  if (!canSave.value) {
    showMessage('warning', 'Select store and add at least one item.')
    return
  }
  const validation = validateSaleInputs()
  if (validation) {
    showMessage('warning', validation)
    return
  }
  const missingBank = payments.value.find(item => Number(item.amount || 0) > 0 && paymentRequiresBank(item) && !item.bankAccountId)
  if (missingBank) {
    showMessage('warning', `Select bank account for ${paymentReferenceLabel(missingBank)}.`)
    return
  }
  if (paymentTotal.value > payableTotal.value) {
    showMessage('warning', `Payment is above bill amount by ${money(paymentTotal.value - payableTotal.value)}.`)
    return
  }

  saving.value = true
  message.value = ''
  try {
    const salePayments = buildPayments()
    const response = await api.value.post<any>('billing/sales', createPosSaleRequest({
      companyId: form.companyId,
      storeGroupId: form.storeGroupId,
      storeId: form.storeId,
      customerId: form.customerId,
      salesmanId: form.salesmanId,
      customerName: form.customerName,
      customerMobileNumber: form.customerMobileNumber,
      customerGstin: normalizeGstin(form.customerGstin),
      fallbackPaymentMode: paymentModeValue.cash,
      mixedPaymentMode: paymentModeValue.mixPayments,
      billDiscountAmount: Number(form.billDiscountAmount || 0),
      payments: salePayments,
      items: cart.value.map(item => ({
        productId: item.productId,
        barcode: item.barcode,
        quantity: Number(item.quantity),
        mrp: Number(item.mrp),
        discountAmount: Number(item.discountAmount)
      }))
    }))
    addPrintQueueItem({
      invoiceId: response.invoiceId,
      invoiceNumber: response.invoiceNumber || '',
      customerName: form.customerName || 'Walk-in Customer',
      billAmount: Number(response.billAmount || payableTotal.value),
      savedAt: new Date().toISOString()
    })
    clearDraft()
    resetForNext()
    try {
      await printInvoice(response.invoiceId)
      showMessage('success', `Invoice ${response.invoiceNumber || ''} saved and opened for printing.`.trim())
    } catch (printError) {
      showMessage('warning', printError instanceof Error
        ? `Invoice ${response.invoiceNumber || ''} saved. ${printError.message}`.trim()
        : `Invoice ${response.invoiceNumber || ''} saved. Use Print Queue to retry printing.`.trim())
    }
  } catch (error) {
    showMessage('error', error instanceof Error ? error.message : 'Could not save invoice.')
  } finally {
    saving.value = false
  }
}

async function printInvoice(invoiceId: string) {
  await openBillingInvoicePdf({
    apiBaseUrl: apiBaseUrl.value,
    invoiceId,
    token: getStoredToken(window.localStorage),
    reprint: false
  })
}

function validateSaleInputs() {
  if (!isValidGstin(form.customerGstin)) return 'Enter a valid GSTIN or leave it blank.'
  if (Number(form.billDiscountAmount || 0) < 0) return 'Bill discount cannot be negative.'
  if (Number(form.billDiscountAmount || 0) > cartTotal.value) return 'Bill discount cannot be more than bill gross amount.'
  if (Number(adjustments.storeCreditAmount || 0) > Number(selectedCustomerProfile.value?.customer?.creditBalance || 0)) {
    return 'Store credit amount is more than available customer credit.'
  }
  if (adjustments.creditNoteId && Number(adjustments.creditNoteAmount || 0) <= 0) return 'Enter credit note amount.'
  if (Number(adjustments.creditNoteAmount || 0) > Number(selectedCreditNote.value?.availableAmount || 0)) return 'Credit note amount is more than available amount.'
  if (adjustments.advanceReceiptId && Number(adjustments.advanceAmount || 0) <= 0) return 'Enter advance receipt amount.'
  if (Number(adjustments.advanceAmount || 0) > Number(selectedAdvance.value?.availableAmount || 0)) return 'Advance amount is more than available amount.'
  if (Number(adjustments.loyaltyPointsToRedeem || 0) > Number(selectedCustomerProfile.value?.customer?.loyaltyPoints || 0)) {
    return 'Loyalty points are more than available points.'
  }
  if (adjustmentTotal.value > payableTotal.value) return 'Customer adjustments cannot be more than payable amount.'
  return ''
}

function resetForNext() {
  form.customerId = null
  form.customerName = 'Walk-in Customer'
  form.customerMobileNumber = ''
  form.customerGstin = ''
  form.billDiscountAmount = 0
  selectedCustomerId.value = null
  customerMatches.value = []
  selectedCustomerProfile.value = null
  resetAdjustments()
  cart.value = []
  payments.value = [emptyPayment(0)]
  productSearch.value = ''
  quantity.value = '1'
  lineDiscount.value = ''
  setDefaultSalesman()
  focusProductSearch()
}

function addPrintQueueItem(item: PosPrintQueueItem) {
  if (!import.meta.client || !item.invoiceId) return
  upsertPrintQueueItem(item)
}

function saveDraft() {
  if (!import.meta.client) return
  writeSaleDraft(buildSaleDraft())
}

function restoreDraft() {
  if (!import.meta.client) return
  try {
    const draft = readSaleDraft()
    if (!draft) return
    Object.assign(form, draft.form || {})
    Object.assign(adjustments, draft.adjustments || {})
    cart.value = Array.isArray(draft.cart) ? draft.cart : []
    payments.value = Array.isArray(draft.payments) && draft.payments.length ? draft.payments : [emptyPayment(0)]
  } catch {
    clearDraft()
  }
}

function clearDraft() {
  clearSaleDraft()
}

watch(productSearch, (value) => {
  if (value.trim().length >= 2) {
    void refreshProductSuggestions(value)
  }
})

watch(() => [form.customerName, form.customerMobileNumber, form.billDiscountAmount, form.salesmanId], () => saveDraft())
watch(payments, () => saveDraft(), { deep: true })
watch(adjustments, () => {
  syncCashPayment()
  saveDraft()
}, { deep: true })

onMounted(() => {
  window.addEventListener('keydown', handleCounterShortcut)
  void refresh()
  focusProductSearch()
})

onBeforeUnmount(() => {
  window.removeEventListener('keydown', handleCounterShortcut)
})
</script>
