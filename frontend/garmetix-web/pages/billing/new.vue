<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const workspace = useWorkspace()
const feedback = useUiFeedback()
const productLookup = useProductLookup()
const documentPrint = useServerDocumentPrint()
const isAuthenticated = auth.isAuthenticated

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
  mixPayments: 12,
  creditBalance: 15
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

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const salesmen = ref<any[]>([])
const bankAccounts = ref<any[]>([])
const setupStatus = ref<any | null>(null)
const selectedProduct = ref<any | null>(null)
const productSuggestions = ref<any[]>([])
const customerMatches = ref<any[]>([])
const selectedCustomerProfile = ref<any | null>(null)
const gstinValidation = ref<any | null>(null)
const lastSavedInvoice = ref<any | null>(null)
const loading = ref(false)
const saving = ref(false)
const searchingCustomer = ref(false)
const checkingGstin = ref(false)
const customerSearchComplete = ref(false)
const draftReady = ref(false)
const loadError = ref('')
const cart = ref<any[]>([])
const payments = ref<any[]>([])

const form = reactive(emptyForm())
const draftKey = 'garmetix.billing.new.draft.v1'

function emptyForm() {
  return {
    customerId: null as string | null,
    customerName: 'Walk-in Customer',
    customerMobileNumber: '',
    customerGstin: '',
    salesmanId: null as string | null,
    billDiscountAmount: 0,
    productSearch: '',
    barcodeScan: '',
    quantity: 1,
    lineDiscount: ''
  }
}

function emptyPayment(amount = 0) {
  return {
    paymentMode: paymentModeValue.cash,
    amount,
    bankAccountId: null as string | null,
    referenceNumber: '',
    gatewayReference: '',
    settlementStatus: ''
  }
}

const salesmanOptions = computed(() => salesmen.value.map((item) => ({
  value: item.id,
  label: item.name || 'Salesman'
})))

const customerMatchOptions = computed(() => customerMatches.value.map((item) => ({
  value: item.id,
  label: item.label || `${item.name} | ${item.mobileNumber}`
})))

const bankAccountOptions = computed(() => bankAccounts.value
  .filter((item) => !workspace.companyId.value || item.companyId === workspace.companyId.value)
  .map((item) => ({
    value: item.id,
    label: `${item.accountHolderName || item.bankName || 'Bank'} - ${item.accountNumber || ''}`.trim()
  })))

const creditNoteOptions = computed(() => (selectedCustomerProfile.value?.creditNotes || []).map((item: any) => ({
  value: item.id,
  label: `${item.number} | Available ${money(item.availableAmount)}`
})))

const advanceOptions = computed(() => (selectedCustomerProfile.value?.advanceReceipts || []).map((item: any) => ({
  value: item.id,
  label: `${item.number} | Available ${money(item.availableAmount)}`
})))

const adjustments = reactive({
  storeCreditAmount: 0,
  loyaltyPointsToRedeem: 0,
  creditNoteId: null as string | null,
  creditNoteAmount: 0,
  advanceReceiptId: null as string | null,
  advanceAmount: 0
})

const selectedCreditNote = computed(() => (selectedCustomerProfile.value?.creditNotes || []).find((item: any) => item.id === adjustments.creditNoteId))
const selectedAdvance = computed(() => (selectedCustomerProfile.value?.advanceReceipts || []).find((item: any) => item.id === adjustments.advanceReceiptId))
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
const unroundedPayable = computed(() => Math.max(cartTotal.value - Number(form.billDiscountAmount || 0), 0))
const payableTotal = computed(() => Math.round(unroundedPayable.value))
const roundOff = computed(() => payableTotal.value - unroundedPayable.value)
const manualPaymentTotal = computed(() => payments.value.reduce((sum, item) => sum + Number(item.amount || 0), 0))
const paymentTotal = computed(() => manualPaymentTotal.value + adjustmentTotal.value)
const paymentBalance = computed(() => Math.max(payableTotal.value - paymentTotal.value, 0))
const isNewCustomer = computed(() =>
  customerSearchComplete.value
  && !form.customerId
  && Boolean(String(form.customerMobileNumber || '').trim()))

function paymentRequiresBank(payment: any) {
  return Number(payment.paymentMode) !== paymentModeValue.cash
}

function paymentReferenceLabel(payment: any) {
  const mode = Number(payment.paymentMode)
  if (mode === paymentModeValue.upi) return 'UPI transaction / UTR'
  if ([paymentModeValue.imps, paymentModeValue.rtgs, paymentModeValue.neft].includes(mode)) return 'UTR / bank reference'
  if (mode === paymentModeValue.cheque) return 'Cheque number'
  if (mode === paymentModeValue.demandDraft) return 'Demand draft number'
  if (mode === paymentModeValue.card) return 'Card authorization / reference'
  if (mode === paymentModeValue.wallets) return 'Wallet transaction reference'
  return 'Reference'
}

function showGateway(payment: any) {
  return [paymentModeValue.card, paymentModeValue.upi, paymentModeValue.wallets].includes(Number(payment.paymentMode))
}

function showSettlement(payment: any) {
  return Number(payment.paymentMode) !== paymentModeValue.cash
}

async function refresh() {
  if (!auth.isAuthenticated.value) return
  loading.value = true
  loadError.value = ''
  try {
    setupStatus.value = await api.get<any>('setup/status')
    const companyId = workspace.companyId.value || setupStatus.value?.companyId
    const storeId = workspace.storeId.value || setupStatus.value?.storeId
    const query = new URLSearchParams({ take: '1' })
    if (companyId) query.set('companyId', companyId)
    if (storeId) query.set('storeId', storeId)

    const [companyRows, storeRows, accountRows, options] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores'),
      api.list<any>('bank-accounts'),
      api.get<any>(`billing/options?${query.toString()}`)
    ])
    companies.value = companyRows
    stores.value = storeRows
    bankAccounts.value = accountRows
    salesmen.value = options?.salesmen || []
    setDefaultSalesman()
    restoreDraft()
  } catch (error) {
    loadError.value = feedback.errorMessage(error, 'Could not load the sales invoice workspace.', 'Sales invoice load failed')
  } finally {
    loading.value = false
    draftReady.value = true
  }
}

function setDefaultSalesman() {
  if (form.salesmanId) return
  const manager = salesmen.value.find((item) => String(item.name || '').trim().toLowerCase() === 'manager')
  form.salesmanId = manager?.id || salesmen.value[0]?.id || null
}

async function searchCustomer() {
  const term = String(form.customerMobileNumber || '').trim()
  customerMatches.value = []
  selectedCustomerProfile.value = null
  form.customerId = null
  customerSearchComplete.value = false
  if (!term) {
    form.customerName = 'Walk-in Customer'
    return
  }

  searchingCustomer.value = true
  try {
    const query = new URLSearchParams({ q: term, take: '10' })
    const companyId = workspace.companyId.value || setupStatus.value?.companyId
    if (companyId) query.set('companyId', companyId)
    customerMatches.value = await api.get<any[]>(`billing/customers/search?${query.toString()}`)
    customerSearchComplete.value = true
    const exact = customerMatches.value.filter((item) => String(item.mobileNumber || '').trim() === term)
    if (exact.length === 1) {
      await selectCustomer(exact[0].id)
    } else if (exact.length === 0) {
      if (!form.customerName || form.customerName === 'Walk-in Customer') form.customerName = 'New Customer'
      feedback.notify('New customer', 'Enter the customer name and optional GSTIN. The customer will be saved with the invoice.', 'info')
    }
  } catch (error) {
    feedback.failed('Could not search customer', error)
  } finally {
    searchingCustomer.value = false
  }
}

async function selectCustomer(customerId: string | null) {
  const customer = customerMatches.value.find((item) => item.id === customerId)
  if (!customer) return
  form.customerId = customer.id
  form.customerName = customer.name || 'Customer'
  form.customerMobileNumber = customer.mobileNumber || ''
  form.customerGstin = customer.gstin || ''
  gstinValidation.value = null
  await loadCustomerProfile(customer.id)
}

async function loadCustomerProfile(customerId: string) {
  try {
    const query = new URLSearchParams()
    const storeId = workspace.storeId.value || setupStatus.value?.storeId
    if (storeId) query.set('storeId', storeId)
    selectedCustomerProfile.value = await api.get<any>(`billing/customers/${customerId}/profile?${query.toString()}`)
  } catch (error) {
    selectedCustomerProfile.value = null
    feedback.failed('Could not load customer balance', error)
  }
}

async function validateGstin() {
  gstinValidation.value = null
  if (!String(form.customerGstin || '').trim()) {
    feedback.notify('Enter customer GSTIN', undefined, 'warning')
    return
  }
  checkingGstin.value = true
  try {
    gstinValidation.value = await api.create<any>('gstin/validate-party', {
      partyType: 'Customer',
      gstin: form.customerGstin,
      name: form.customerName,
      address: ''
    })
    const alerts = gstinValidation.value?.alerts || []
    feedback.notify(
      alerts.length ? 'Customer GSTIN alert' : 'Customer GSTIN checked',
      alerts.length ? alerts.join(' ') : 'GSTIN format and available registration details were checked.',
      alerts.length ? 'warning' : 'success'
    )
  } catch (error) {
    feedback.failed('Could not check customer GSTIN', error)
  } finally {
    checkingGstin.value = false
  }
}

async function refreshProductSuggestions() {
  const term = String(form.productSearch || '').trim()
  productSuggestions.value = await productLookup.searchProducts(term, workspace.storeId.value || undefined)
}

async function lookupProduct() {
  const query = String(form.barcodeScan || form.productSearch || '').trim()
  if (!query) {
    feedback.notify('Enter a product', 'Scan a barcode or type a product name.', 'warning')
    return
  }
  const selected = query.includes('|')
    ? productSuggestions.value.find((item) => suggestionLabel(item) === query)
    : await productLookup.byBarcode(query, workspace.storeId.value || undefined)
      || (await productLookup.searchProducts(query, workspace.storeId.value || undefined))[0]
  if (!selected) {
    feedback.notify('Product not found', 'No available product matched the barcode or search.', 'warning')
    return
  }
  selectedProduct.value = selected
  form.productSearch = `${selected.barcode} | ${selected.name}`
  form.barcodeScan = selected.barcode
}

function suggestionLabel(item: any) {
  return `${item.barcode} | ${item.name} | Qty ${Number(item.availableQty || 0)} | MRP ${Number(item.mrp || 0)}`
}

function discountPerUnit() {
  if (!selectedProduct.value) return 0
  const input = String(form.lineDiscount || '').trim()
  const mrp = Number(selectedProduct.value.mrp || 0)
  if (input.endsWith('%')) {
    const percent = Math.min(Math.max(Number(input.slice(0, -1)) || 0, 0), 100)
    return Math.round(mrp * percent) / 100
  }
  return Math.min(Math.max(Number(input) || 0, 0), mrp)
}

function addItem() {
  const item = selectedProduct.value
  const quantity = Number(form.quantity || 0)
  if (!item || quantity <= 0) {
    feedback.notify('Select item and quantity', undefined, 'warning')
    return
  }
  if (Number(item.availableQty || 0) < quantity) {
    feedback.notify('Insufficient stock', `Available quantity is ${Number(item.availableQty || 0)}.`, 'warning')
    return
  }
  const existing = cart.value.find((row) => row.productId === item.productId && row.barcode === item.barcode && row.discountAmount === discountPerUnit())
  if (existing) {
    existing.quantity += quantity
  } else {
    const taxRate = Number(item.taxRate || 0)
    const mrp = Number(item.mrp || 0)
    cart.value.push({
      productId: item.productId,
      name: item.name,
      barcode: item.barcode,
      quantity,
      mrp,
      basicRate: taxRate > 0 ? mrp / (1 + taxRate / 100) : mrp,
      discountAmount: discountPerUnit(),
      taxRate
    })
  }
  selectedProduct.value = null
  form.productSearch = ''
  form.barcodeScan = ''
  form.quantity = 1
  form.lineDiscount = ''
  syncCashPayment()
}

function removeItem(index: number) {
  cart.value.splice(index, 1)
  syncCashPayment()
}

function lineTotal(item: any) {
  return Math.max((Number(item.mrp || 0) - Number(item.discountAmount || 0)) * Number(item.quantity || 0), 0)
}

function addPayment() {
  payments.value.push(emptyPayment(paymentBalance.value))
}

function removePayment(index: number) {
  payments.value.splice(index, 1)
  if (!payments.value.length) payments.value.push(emptyPayment(paymentBalance.value))
}

function syncCashPayment() {
  if (payments.value.length === 1 && Number(payments.value[0]?.paymentMode) === paymentModeValue.cash) {
    payments.value[0].amount = Math.max(payableTotal.value - adjustmentTotal.value, 0)
  }
}

function buildPayments() {
  const rows: any[] = payments.value
    .filter((item) => Number(item.amount || 0) > 0)
    .map((item) => ({
      paymentMode: Number(item.paymentMode),
      amount: Number(item.amount || 0),
      bankAccountId: paymentRequiresBank(item) ? item.bankAccountId : null,
      referenceNumber: item.referenceNumber || null,
      gatewayReference: item.gatewayReference || null,
      settlementStatus: item.settlementStatus || null,
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

function adjustmentPayment(mode: number, amount: number, reference: string, sourceType: string, sourceId: string | null = null) {
  return {
    paymentMode: mode,
    amount,
    bankAccountId: null,
    referenceNumber: reference,
    gatewayReference: null,
    settlementStatus: null,
    adjustmentSourceType: sourceType,
    adjustmentSourceId: sourceId
  }
}

async function submitSale() {
  const selectedStore = stores.value.find((item) => item.id === workspace.storeId.value)
  const companyId = workspace.companyId.value || setupStatus.value?.companyId || selectedStore?.companyId || companies.value[0]?.id
  const storeGroupId = workspace.storeGroupId.value || setupStatus.value?.storeGroupId || selectedStore?.storeGroupId
  const storeId = workspace.storeId.value || setupStatus.value?.storeId || selectedStore?.id || stores.value[0]?.id
  if (!companyId || !storeGroupId || !storeId) {
    feedback.notify('Complete company setup', 'Select an active company and store before billing.', 'warning')
    return
  }
  if (!cart.value.length) {
    feedback.notify('Add at least one item', undefined, 'warning')
    return
  }
  const missingBank = payments.value.find((item) => Number(item.amount || 0) > 0 && paymentRequiresBank(item) && !item.bankAccountId)
  if (missingBank) {
    feedback.notify('Select bank account', `A bank account is required for ${paymentReferenceLabel(missingBank)}.`, 'warning')
    return
  }
  const salePayments = buildPayments()
  const totalPaid = salePayments.reduce((sum, item) => sum + Number(item.amount || 0), 0)
  if (totalPaid > payableTotal.value) {
    feedback.notify('Payment is above bill amount', `Reduce payment by ${money(totalPaid - payableTotal.value)}.`, 'warning')
    return
  }

  saving.value = true
  try {
    if (form.customerGstin && !gstinValidation.value) await validateGstin()
    const response = await api.create<any>('billing/sales', {
      companyId,
      storeGroupId,
      storeId,
      customerId: form.customerId || null,
      salesmanId: form.salesmanId || null,
      customerName: form.customerName || 'Walk-in Customer',
      customerMobileNumber: form.customerMobileNumber || '',
      customerGstin: form.customerGstin || '',
      paymentMode: salePayments.length > 1 ? paymentModeValue.mixPayments : Number(salePayments[0]?.paymentMode ?? paymentModeValue.cash),
      bankAccountId: salePayments.find((item) => item.bankAccountId)?.bankAccountId || null,
      paidAmount: totalPaid,
      billDiscountAmount: Number(form.billDiscountAmount || 0),
      payments: salePayments,
      items: cart.value.map((item) => ({
        productId: item.productId,
        barcode: item.barcode,
        quantity: Number(item.quantity),
        mrp: Number(item.mrp),
        discountAmount: Number(item.discountAmount)
      }))
    })
    lastSavedInvoice.value = response
    feedback.saved(`Invoice ${response.invoiceNumber || ''}`.trim())
    if (response.gstinAlerts?.length) feedback.notify('GSTIN alert saved', response.gstinAlerts.join(' '), 'warning')
    try {
      await documentPrint.printPdf(`billing/sales/${response.invoiceId}/pdf?format=a4&copy=customer&reprint=false&signatures=true`)
    } catch (printError) {
      feedback.failed('Invoice saved but print could not start', printError)
    }
    resetForNextInvoice()
  } catch (error) {
    feedback.failed('Could not save invoice', error)
  } finally {
    saving.value = false
  }
}

function resetForNextInvoice() {
  Object.assign(form, emptyForm())
  Object.assign(adjustments, {
    storeCreditAmount: 0,
    loyaltyPointsToRedeem: 0,
    creditNoteId: null,
    creditNoteAmount: 0,
    advanceReceiptId: null,
    advanceAmount: 0
  })
  cart.value = []
  payments.value = [emptyPayment(0)]
  selectedProduct.value = null
  selectedCustomerProfile.value = null
  customerMatches.value = []
  customerSearchComplete.value = false
  gstinValidation.value = null
  setDefaultSalesman()
  clearDraft()
}

function persistDraft() {
  if (!import.meta.client || !draftReady.value) return
  localStorage.setItem(draftKey, JSON.stringify({
    form: { ...form },
    adjustments: { ...adjustments },
    cart: cart.value,
    payments: payments.value
  }))
}

function restoreDraft() {
  if (!import.meta.client) return
  try {
    const saved = JSON.parse(localStorage.getItem(draftKey) || 'null')
    if (!saved) {
      if (!payments.value.length) payments.value = [emptyPayment(0)]
      return
    }
    Object.assign(form, emptyForm(), saved.form || {})
    Object.assign(adjustments, saved.adjustments || {})
    cart.value = Array.isArray(saved.cart) ? saved.cart : []
    payments.value = Array.isArray(saved.payments) && saved.payments.length ? saved.payments : [emptyPayment(0)]
    if (form.customerId) void loadCustomerProfile(form.customerId)
  } catch {
    clearDraft()
  }
}

function clearDraft() {
  if (import.meta.client) localStorage.removeItem(draftKey)
}

function money(value: number) {
  return new Intl.NumberFormat('en-IN', {
    style: 'currency',
    currency: 'INR',
    maximumFractionDigits: 2
  }).format(Number(value || 0))
}

onMounted(async () => {
  auth.restore()
  await refresh()
})

watch(() => adjustments.creditNoteId, () => {
  adjustments.creditNoteAmount = selectedCreditNote.value?.availableAmount || 0
  syncCashPayment()
})
watch(() => adjustments.advanceReceiptId, () => {
  adjustments.advanceAmount = selectedAdvance.value?.availableAmount || 0
  syncCashPayment()
})
watch(() => [
  form.billDiscountAmount,
  adjustments.storeCreditAmount,
  adjustments.creditNoteAmount,
  adjustments.advanceAmount,
  adjustments.loyaltyPointsToRedeem
], syncCashPayment)
watch([form, adjustments, cart, payments], persistDraft, { deep: true })
</script>

<template>
  <AuthScreen v-if="!isAuthenticated" @authenticated="refresh" />
  <AppShell
    v-else
    title="New Sales Invoice"
    :companies="companies"
    :stores="stores"
    @refresh="refresh"
    @workspace-change="refresh"
  >
    <section class="invoice-page">
      <UiModulePageHeader
        title="New Sales Invoice"
        description="Create, collect and print a complete invoice without leaving the billing workspace."
        icon="i-lucide-receipt-indian-rupee"
      >
        <template #actions>
          <UButton to="/billing" color="neutral" variant="outline" icon="i-lucide-arrow-left" label="Invoice Register" />
          <UButton icon="i-lucide-save" label="Save & Print" :loading="saving" @click="submitSale" />
        </template>
      </UiModulePageHeader>

      <UAlert v-if="loadError" color="error" variant="subtle" title="Invoice workspace unavailable" :description="loadError" />
      <UAlert
        v-if="lastSavedInvoice"
        color="success"
        variant="subtle"
        icon="i-lucide-circle-check"
        title="Invoice saved and next bill is ready"
        :description="`${lastSavedInvoice.invoiceNumber} was saved for ${money(lastSavedInvoice.billAmount)}.`"
      />

      <div class="invoice-grid" :aria-busy="loading">
        <main class="sale-entry-main">
          <section class="invoice-band">
            <div class="section-heading">
              <div><span class="step">1</span><h2>Customer</h2></div>
              <UBadge v-if="form.customerGstin" color="primary" variant="subtle">B2B</UBadge>
              <UBadge v-else color="neutral" variant="subtle">B2C</UBadge>
            </div>

            <div class="customer-grid">
              <UFormField label="Mobile number">
                <div class="inline-control">
                  <UInput v-model="form.customerMobileNumber" inputmode="numeric" maxlength="15" placeholder="Search by mobile" @keyup.enter="searchCustomer" @blur="searchCustomer" />
                  <UButton color="neutral" variant="subtle" icon="i-lucide-search" :loading="searchingCustomer" aria-label="Search customer" @click="searchCustomer" />
                </div>
              </UFormField>
              <UFormField v-if="customerMatches.length > 1" label="Matching customer">
                <USelect v-model="form.customerId" :items="customerMatchOptions" placeholder="Choose matching customer" @update:model-value="selectCustomer" />
              </UFormField>
              <UFormField label="Customer name">
                <UInput v-model="form.customerName" placeholder="Walk-in Customer" />
              </UFormField>
              <UFormField label="Salesman">
                <USelect v-model="form.salesmanId" :items="salesmanOptions" placeholder="Manager" />
              </UFormField>
              <UFormField label="GSTIN" class="gstin-field">
                <div class="inline-control">
                  <UInput v-model="form.customerGstin" maxlength="15" placeholder="22AAAAA0000A1Z5" />
                  <UButton color="neutral" variant="subtle" icon="i-lucide-search-check" label="Check" :loading="checkingGstin" @click="validateGstin" />
                </div>
              </UFormField>
            </div>

            <UAlert
              v-if="isNewCustomer"
              color="info"
              variant="subtle"
              title="New customer"
              description="This customer will be added automatically when the invoice is saved."
            />
            <UAlert
              v-if="gstinValidation?.alerts?.length"
              color="warning"
              variant="subtle"
              title="GSTIN alert"
              :description="gstinValidation.alerts.join(' ')"
            />
            <div v-if="selectedCustomerProfile?.customer" class="customer-balance">
              <span>Credit <strong>{{ money(selectedCustomerProfile.customer.creditBalance) }}</strong></span>
              <span>Loyalty <strong>{{ Number(selectedCustomerProfile.customer.loyaltyPoints || 0) }} pts</strong></span>
              <span>Bills <strong>{{ selectedCustomerProfile.customer.billCount || 0 }}</strong></span>
            </div>
          </section>

          <section class="invoice-band">
            <div class="section-heading">
              <div><span class="step">2</span><h2>Items</h2></div>
              <UBadge color="neutral" variant="subtle">{{ cart.length }} lines</UBadge>
            </div>

            <div class="item-entry">
              <UFormField label="Barcode">
                <div class="inline-control">
                  <UInput v-model="form.barcodeScan" placeholder="Scan barcode" @keyup.enter="lookupProduct" />
                  <UButton color="neutral" variant="subtle" icon="i-lucide-scan-barcode" aria-label="Fetch barcode" @click="lookupProduct" />
                </div>
              </UFormField>
              <UFormField label="Product">
                <UInput v-model="form.productSearch" list="invoice-product-cache" placeholder="Name, barcode or HSN" @input="refreshProductSuggestions" @change="lookupProduct" />
                <datalist id="invoice-product-cache">
                  <option v-for="item in productSuggestions" :key="`${item.productId}-${item.barcode}`" :value="suggestionLabel(item)" />
                </datalist>
              </UFormField>
              <UFormField label="Qty">
                <UInput v-model="form.quantity" type="number" min="1" />
              </UFormField>
              <UFormField label="Discount">
                <UInput v-model="form.lineDiscount" placeholder="100 or 10%" />
              </UFormField>
              <UButton class="add-item-button" color="primary" variant="solid" icon="i-lucide-plus" label="Add Item" @click="addItem" />
            </div>

            <div v-if="selectedProduct" class="selected-product">
              <strong>{{ selectedProduct.name }}</strong>
              <span>{{ selectedProduct.barcode }}</span>
              <span>Available {{ Number(selectedProduct.availableQty || 0) }}</span>
              <span>MRP {{ money(selectedProduct.mrp) }}</span>
              <span>GST {{ Number(selectedProduct.taxRate || 0) }}%</span>
            </div>

            <div class="invoice-table-wrap">
              <table class="invoice-table">
                <thead>
                  <tr>
                    <th>Item name</th>
                    <th class="basic-col">Basic rate</th>
                    <th>MRP</th>
                    <th>Discount</th>
                    <th>Qty</th>
                    <th class="tax-col">Tax</th>
                    <th>Line total</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="(item, index) in cart" :key="`${item.productId}-${item.barcode}-${index}`">
                    <td><strong>{{ item.name }}</strong><small>{{ item.barcode }}</small></td>
                    <td class="basic-col">{{ money(item.basicRate) }}</td>
                    <td>{{ money(item.mrp) }}</td>
                    <td>{{ money(item.discountAmount * item.quantity) }}</td>
                    <td>{{ item.quantity }}</td>
                    <td class="tax-col">{{ item.taxRate }}%</td>
                    <td><strong>{{ money(lineTotal(item)) }}</strong></td>
                    <td><UButton color="error" variant="ghost" size="xs" icon="i-lucide-x" aria-label="Remove item" @click="removeItem(index)" /></td>
                  </tr>
                  <tr v-if="!cart.length"><td colspan="8" class="empty-row">Scan or search a product to begin the invoice.</td></tr>
                </tbody>
              </table>
            </div>
          </section>

          <section class="invoice-band">
            <div class="section-heading">
              <div><span class="step">3</span><h2>Payment</h2></div>
              <UBadge :color="paymentBalance > 0 ? 'warning' : 'success'" variant="subtle">{{ paymentBalance > 0 ? 'Credit sale' : 'Fully paid' }}</UBadge>
            </div>

            <UFormField label="Bill discount" class="compact-field">
              <UInput v-model="form.billDiscountAmount" type="number" min="0" step="0.01" @blur="syncCashPayment" />
            </UFormField>

            <details class="adjustment-panel">
              <summary><span>Customer credit, notes and loyalty</span><strong>{{ money(adjustmentTotal) }}</strong></summary>
              <div class="adjustment-grid">
                <UFormField label="Store credit">
                  <UInput v-model="adjustments.storeCreditAmount" type="number" min="0" :max="selectedCustomerProfile?.customer?.creditBalance || 0" />
                </UFormField>
                <UFormField label="Loyalty points">
                  <UInput v-model="adjustments.loyaltyPointsToRedeem" type="number" min="0" :max="selectedCustomerProfile?.customer?.loyaltyPoints || 0" />
                </UFormField>
                <UFormField label="Credit note">
                  <USelect v-model="adjustments.creditNoteId" :items="creditNoteOptions" placeholder="Select credit note" />
                </UFormField>
                <UFormField label="Credit note amount">
                  <UInput v-model="adjustments.creditNoteAmount" type="number" min="0" :max="selectedCreditNote?.availableAmount || 0" />
                </UFormField>
                <UFormField label="Advance receipt">
                  <USelect v-model="adjustments.advanceReceiptId" :items="advanceOptions" placeholder="Select advance" />
                </UFormField>
                <UFormField label="Advance amount">
                  <UInput v-model="adjustments.advanceAmount" type="number" min="0" :max="selectedAdvance?.availableAmount || 0" />
                </UFormField>
              </div>
            </details>

            <div class="payment-list">
              <div v-for="(payment, index) in payments" :key="`payment-${index}`" class="payment-row">
                <UFormField label="Mode"><USelect v-model="payment.paymentMode" :items="paymentModeOptions" /></UFormField>
                <UFormField label="Amount"><UInput v-model="payment.amount" type="number" min="0" step="0.01" /></UFormField>
                <UFormField v-if="paymentRequiresBank(payment)" label="Bank account"><USelect v-model="payment.bankAccountId" :items="bankAccountOptions" placeholder="Select account" /></UFormField>
                <UFormField v-if="paymentRequiresBank(payment)" :label="paymentReferenceLabel(payment)"><UInput v-model="payment.referenceNumber" :placeholder="paymentReferenceLabel(payment)" /></UFormField>
                <UFormField v-if="showGateway(payment)" label="Gateway / app"><UInput v-model="payment.gatewayReference" placeholder="Provider or gateway reference" /></UFormField>
                <UFormField v-if="showSettlement(payment)" label="Settlement status"><UInput v-model="payment.settlementStatus" placeholder="Pending / settled" /></UFormField>
                <UButton color="error" variant="ghost" icon="i-lucide-trash-2" aria-label="Remove payment" @click="removePayment(index)" />
              </div>
            </div>
            <UButton color="info" variant="soft" size="sm" icon="i-lucide-plus" label="Add Payment" @click="addPayment" />
          </section>
        </main>

        <aside class="sale-total-panel">
          <div class="summary-heading"><span>Invoice total</span><strong>{{ money(payableTotal) }}</strong></div>
          <dl>
            <div><dt>Items total</dt><dd>{{ money(cartTotal) }}</dd></div>
            <div><dt>Bill discount</dt><dd>- {{ money(Number(form.billDiscountAmount || 0)) }}</dd></div>
            <div><dt>Round off</dt><dd>{{ money(roundOff) }}</dd></div>
            <div><dt>Customer adjustments</dt><dd>{{ money(adjustmentTotal) }}</dd></div>
            <div><dt>Payments</dt><dd>{{ money(manualPaymentTotal) }}</dd></div>
            <div class="balance"><dt>Balance</dt><dd>{{ money(paymentBalance) }}</dd></div>
          </dl>
          <UButton block size="lg" icon="i-lucide-save" label="Save & Print Invoice" :loading="saving" :disabled="loading" @click="submitSale" />
          <p>The saved invoice prints automatically. This page then resets for the next customer.</p>
        </aside>
      </div>
    </section>
  </AppShell>
</template>

<style scoped>
.invoice-page { display: grid; gap: 1rem; min-width: 0; }
.invoice-grid { display: grid; grid-template-columns: minmax(0, 1fr) minmax(17rem, 21rem); gap: 1rem; align-items: start; }
.sale-entry-main { display: grid; gap: 1rem; min-width: 0; }
.invoice-band { padding: 1rem; border: 1px solid var(--ui-border); border-radius: 8px; background: var(--ui-bg); display: grid; gap: 1rem; }
.section-heading, .section-heading > div, .inline-control, .selected-product, .customer-balance { display: flex; align-items: center; gap: .65rem; }
.section-heading { justify-content: space-between; }
.section-heading h2 { margin: 0; font-size: 1rem; font-weight: 700; letter-spacing: 0; }
.step { display: grid; place-items: center; width: 1.65rem; height: 1.65rem; border-radius: 50%; background: var(--ui-primary); color: var(--ui-bg); font-size: .8rem; font-weight: 800; }
.customer-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: .85rem; align-items: end; }
.gstin-field { max-width: 31rem; }
.inline-control > :first-child { flex: 1; min-width: 0; }
.customer-balance { flex-wrap: wrap; padding: .7rem .8rem; border-left: 3px solid var(--ui-primary); background: var(--ui-bg-elevated); font-size: .85rem; }
.customer-balance span { padding-right: 1rem; }
.item-entry { display: grid; grid-template-columns: minmax(11rem, .8fr) minmax(15rem, 1.6fr) 5rem 8rem auto; gap: .7rem; align-items: end; }
.add-item-button { align-self: end; }
.selected-product { flex-wrap: wrap; padding: .65rem .8rem; background: var(--ui-bg-elevated); border-radius: 6px; font-size: .84rem; }
.selected-product span { color: var(--ui-text-muted); }
.invoice-table-wrap { overflow-x: auto; border: 1px solid var(--ui-border); border-radius: 6px; }
.invoice-table { width: 100%; border-collapse: collapse; font-size: .85rem; }
.invoice-table th, .invoice-table td { padding: .7rem .65rem; border-bottom: 1px solid var(--ui-border); text-align: left; white-space: nowrap; }
.invoice-table th { background: var(--ui-bg-elevated); color: var(--ui-text-muted); font-size: .75rem; text-transform: uppercase; }
.invoice-table td:first-child { white-space: normal; min-width: 12rem; }
.invoice-table small { display: block; color: var(--ui-text-muted); margin-top: .2rem; }
.empty-row { height: 5rem; text-align: center !important; color: var(--ui-text-muted); }
.compact-field { max-width: 14rem; }
.adjustment-panel { border: 1px solid var(--ui-border); border-radius: 6px; background: var(--ui-bg-elevated); }
.adjustment-panel summary { display: flex; justify-content: space-between; gap: 1rem; cursor: pointer; padding: .8rem; font-size: .88rem; }
.adjustment-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: .8rem; padding: 0 .8rem .8rem; }
.payment-list { display: grid; gap: .65rem; }
.payment-row { display: grid; grid-template-columns: repeat(3, minmax(9rem, 1fr)) auto; gap: .65rem; align-items: end; padding: .8rem; border: 1px solid var(--ui-border); border-radius: 6px; }
.sale-total-panel { position: sticky; top: 4.5rem; padding: 1rem; border: 1px solid var(--ui-border); border-radius: 8px; background: var(--ui-bg); display: grid; gap: 1rem; }
.summary-heading { display: grid; gap: .25rem; }
.summary-heading span { color: var(--ui-text-muted); font-size: .82rem; }
.summary-heading strong { font-size: 1.75rem; letter-spacing: 0; }
.sale-total-panel dl { margin: 0; display: grid; gap: .65rem; }
.sale-total-panel dl > div { display: flex; justify-content: space-between; gap: 1rem; font-size: .87rem; }
.sale-total-panel dt { color: var(--ui-text-muted); }
.sale-total-panel dd { margin: 0; font-weight: 650; }
.sale-total-panel .balance { padding-top: .7rem; border-top: 1px solid var(--ui-border); font-size: 1rem; }
.sale-total-panel p { margin: 0; color: var(--ui-text-muted); font-size: .78rem; line-height: 1.45; }
@media (max-width: 1400px) {
  .invoice-grid { grid-template-columns: 1fr; }
  .sale-total-panel { position: static; }
  .item-entry { grid-template-columns: repeat(2, minmax(0, 1fr)); }
  .payment-row { grid-template-columns: repeat(2, minmax(0, 1fr)) auto; }
}
@media (max-width: 640px) {
  .invoice-band { padding: .8rem; }
  .customer-grid, .item-entry, .adjustment-grid, .payment-row { grid-template-columns: 1fr; }
  .payment-row > button { justify-self: end; }
  .basic-col, .tax-col { display: none; }
  .invoice-table th, .invoice-table td { padding: .55rem .45rem; }
  .invoice-table td:first-child { min-width: 9rem; }
  .selected-product, .customer-balance { align-items: flex-start; flex-direction: column; gap: .3rem; }
}
</style>
