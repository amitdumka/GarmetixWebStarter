<script setup lang="ts">
import { h, resolveComponent } from 'vue'
import type { TableColumn } from '@nuxt/ui'

const api = useGarmetixApi()
const auth = useAuth()
const workspace = useWorkspace()
const feedback = useUiFeedback()
const productLookup = useProductLookup()
const config = useRuntimeConfig()
const isAuthenticated = auth.isAuthenticated
const canDelete = auth.canDelete

const UBadge = resolveComponent('UBadge')
const UButton = resolveComponent('UButton')

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const customers = ref<any[]>([])
const salesmen = ref<any[]>([])
const products = ref<any[]>([])
const productSearchOptions = ref<any[]>([])
const invoices = ref<any[]>([])
const bankAccounts = ref<any[]>([])
const selectedReceipt = ref<any | null>(null)
const pendingCancel = ref<any | null>(null)
const loading = ref(false)
const saving = ref(false)
const cancelling = ref(false)
const returnOpen = ref(false)
const returning = ref(false)
const pendingReturnInvoice = ref<any | null>(null)
const returnLines = ref<any[]>([])
const exchangeOpen = ref(false)
const exchanging = ref(false)
const pendingExchangeInvoice = ref<any | null>(null)
const exchangeReturnLines = ref<any[]>([])
const exchangeCart = ref<any[]>([])
const downloadingInvoicePdf = ref(false)
const setupStatus = ref<any | null>(null)
const saleGstinValidation = ref<any | null>(null)
const saleGstinChecking = ref(false)
const selectedCustomerProfile = ref<any | null>(null)
const loadingCustomerProfile = ref(false)
const search = ref('')
const saleOpen = ref(false)
const cancelOpen = ref(false)
const invoicePrintFormat = ref<'a4' | 'a5' | 'thermal-2' | 'thermal-3'>('a4')
const invoiceCopyType = ref<'customer' | 'office' | 'duplicate'>('customer')
const invoiceReprint = ref(false)
const invoiceSignatures = ref(true)

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

const adjustmentPaymentModes = [paymentModeValue.creditBalance, paymentModeValue.creditNote, paymentModeValue.coupons]

const invoicePrintFormatOptions = [
  { value: 'a4', label: 'A4 standard invoice' },
  { value: 'a5', label: 'A5 compact invoice' },
  { value: 'thermal-2', label: 'Thermal 2-inch / 58mm' },
  { value: 'thermal-3', label: 'Thermal 3-inch / 80mm' }
]

const invoiceCopyOptions = [
  { value: 'customer', label: 'Customer copy' },
  { value: 'office', label: 'Office copy' },
  { value: 'duplicate', label: 'Duplicate copy' }
]

const saleForm = reactive<any>(emptySaleForm())
const returnForm = reactive<any>(emptyReturnForm())
const exchangeForm = reactive<any>(emptyExchangeForm())
const saleCart = ref<any[]>([])
const salePayments = ref<any[]>([])

const receiptOpen = computed({
  get: () => Boolean(selectedReceipt.value),
  set: (value: boolean) => {
    if (!value) {
      selectedReceipt.value = null
    }
  }
})

const productOptions = computed(() => [
  { value: '', label: 'Select product' },
  ...products.value.map((product) => ({
    value: product.id,
    label: `${product.name || 'Product'} - ${product.barcode || 'No barcode'}`
  }))
])

const saleProductSuggestions = computed(() => productSearchOptions.value.map((item) => `${item.barcode} | ${item.name} | Qty ${Number(item.availableQty || 0)} | MRP ${Number(item.mrp || 0)}`))

const customerOptions = computed(() => [
  { value: null, label: 'Walk-in / new customer' },
  ...customers.value.map((customer) => ({
    value: customer.id,
    label: customer.label || `${customer.name || 'Customer'} | ${customer.mobileNumber || ''}`
  }))
])

const salesmanOptions = computed(() => [
  { value: null, label: 'No salesman / counter sale' },
  ...salesmen.value.map((salesman) => ({ value: salesman.id, label: salesman.name || 'Salesman' }))
])

const selectedCustomer = computed(() => customers.value.find((item) => item.id === saleForm.customerId))
const selectedProduct = computed(() => products.value.find((item) => item.id === saleForm.selectedProductId))
const requiresBankAccount = computed(() => Number(saleForm.paidAmount || 0) > 0 && Number(saleForm.paymentMode) !== 0)
const returnRefundRequiresBank = computed(() => Number(returnForm.refundAmount || 0) > 0 && Number(returnForm.refundPaymentMode) !== 0)
const exchangeRequiresBank = computed(() => Number(exchangeForm.additionalPaidAmount || 0) > 0 && Number(exchangeForm.additionalPaymentMode) !== 0)

const bankAccountOptions = computed(() => bankAccounts.value.map((account) => ({
  value: account.id,
  label: `${account.accountHolderName || 'Bank'} - ${account.accountNumber || ''}`.trim()
})))

const creditNoteOptions = computed(() => (selectedCustomerProfile.value?.creditNotes || []).map((note: any) => ({
  value: note.id,
  label: `${note.number} | Available ${money(Number(note.availableAmount || 0))}`
})))

const advanceReceiptOptions = computed(() => (selectedCustomerProfile.value?.advanceReceipts || []).map((advance: any) => ({
  value: advance.id,
  label: `${advance.number} | Available ${money(Number(advance.availableAmount || 0))}`
})))

const selectedCreditNote = computed(() => (selectedCustomerProfile.value?.creditNotes || []).find((note: any) => note.id === saleForm.creditNoteId))
const selectedAdvanceReceipt = computed(() => (selectedCustomerProfile.value?.advanceReceipts || []).find((advance: any) => advance.id === saleForm.advanceReceiptId))

const manualPaymentTotal = computed(() => salePayments.value.reduce((sum, item) => sum + Number(item.amount || 0), 0))
const storeCreditPaymentAmount = computed(() => Math.min(Number(saleForm.storeCreditAmount || 0), Number(selectedCustomerProfile.value?.customer?.creditBalance || selectedCustomer.value?.creditBalance || 0)))
const creditNotePaymentAmount = computed(() => Math.min(Number(saleForm.creditNoteAmount || 0), Number(selectedCreditNote.value?.availableAmount || 0)))
const advancePaymentAmount = computed(() => Math.min(Number(saleForm.advanceAmount || 0), Number(selectedAdvanceReceipt.value?.availableAmount || 0)))
const loyaltyRedeemValue = computed(() => {
  const program = selectedCustomerProfile.value?.loyaltyProgram
  const points = Math.min(Number(saleForm.loyaltyPointsToRedeem || 0), Number(selectedCustomerProfile.value?.customer?.loyaltyPoints || selectedCustomer.value?.loyaltyPoints || 0))
  return Math.max(points * Number(program?.redeemValuePerPoint || 0), 0)
})
const adjustmentPaymentTotal = computed(() => storeCreditPaymentAmount.value + creditNotePaymentAmount.value + advancePaymentAmount.value + loyaltyRedeemValue.value)
const paymentTotal = computed(() => manualPaymentTotal.value + adjustmentPaymentTotal.value)
const paymentBalance = computed(() => Math.max(payableTotal.value - paymentTotal.value, 0))

const cartTotal = computed(() => {
  return saleCart.value.reduce((sum, item) => sum + lineTotal(item), 0)
})

const payableTotal = computed(() => Math.max(cartTotal.value - Number(saleForm.billDiscountAmount || 0), 0))
const returnTotal = computed(() => returnLines.value.reduce((sum, item) => sum + lineTotal({ mrp: item.mrp, discountAmount: item.discountAmount, quantity: item.returnQuantity }), 0))
const exchangeReturnTotal = computed(() => exchangeReturnLines.value.reduce((sum, item) => sum + lineTotal({ mrp: item.mrp, discountAmount: item.discountAmount, quantity: item.returnQuantity }), 0))
const exchangeNewTotal = computed(() => exchangeCart.value.reduce((sum, item) => sum + lineTotal(item), 0))
const exchangeNetDue = computed(() => Math.max(exchangeNewTotal.value - exchangeReturnTotal.value, 0))

const invoiceSummary = computed(() => {
  return invoices.value.reduce((summary, invoice) => {
    const billAmount = Number(invoice.billAmount || 0)
    const paidAmount = Number(invoice.paidAmount || 0)
    summary.billAmount += billAmount
    summary.paidAmount += paidAmount
    summary.balanceAmount += Number(invoice.balanceAmount || (billAmount - paidAmount))
    if (invoice.invoiceStatus === 'Cancelled') {
      summary.cancelled += 1
    }
    return summary
  }, {
    billAmount: 0,
    paidAmount: 0,
    balanceAmount: 0,
    cancelled: 0
  })
})

const metrics = computed(() => [
  {
    label: 'Invoices',
    value: invoices.value.length,
    meta: `${invoiceSummary.value.cancelled} cancelled`,
    icon: 'i-lucide-receipt-indian-rupee',
    color: 'primary'
  },
  {
    label: 'Sales',
    value: money(invoiceSummary.value.billAmount),
    meta: 'Bill amount',
    icon: 'i-lucide-indian-rupee',
    color: 'success'
  },
  {
    label: 'Paid',
    value: money(invoiceSummary.value.paidAmount),
    meta: 'Collected amount',
    icon: 'i-lucide-credit-card',
    color: 'neutral'
  },
  {
    label: 'Balance',
    value: money(invoiceSummary.value.balanceAmount),
    meta: 'Outstanding',
    icon: 'i-lucide-wallet',
    color: invoiceSummary.value.balanceAmount > 0 ? 'warning' : 'success'
  }
])

const tableRows = computed(() => invoices.value.map((invoice) => ({
  id: invoice.id,
  invoiceNumber: invoice.invoiceNumber || '-',
  onDate: formatDate(invoice.onDate),
  customerName: invoice.customerName || 'Walk-in Customer',
  billAmount: money(Number(invoice.billAmount || 0)),
  paidAmount: money(Number(invoice.paidAmount || 0)),
  balanceAmount: money(Number(invoice.balanceAmount || (Number(invoice.billAmount || 0) - Number(invoice.paidAmount || 0)))),
  invoiceStatus: invoice.invoiceStatus || 'Saved',
  raw: invoice
})))

const filteredRows = computed(() => {
  const term = search.value.trim().toLowerCase()
  if (!term) {
    return tableRows.value
  }

  return tableRows.value.filter((row) => JSON.stringify(row).toLowerCase().includes(term))
})

const columns: TableColumn<any>[] = [
  { accessorKey: 'invoiceNumber', header: 'Invoice' },
  { accessorKey: 'onDate', header: 'Date' },
  { accessorKey: 'customerName', header: 'Customer' },
  { accessorKey: 'billAmount', header: 'Amount' },
  { accessorKey: 'paidAmount', header: 'Paid' },
  { accessorKey: 'balanceAmount', header: 'Balance' },
  {
    accessorKey: 'invoiceStatus',
    header: 'Status',
    cell: ({ row }) => h(UBadge, {
      color: row.original.invoiceStatus === 'Cancelled' ? 'error' : 'success',
      variant: 'subtle'
    }, () => row.original.invoiceStatus)
  },
  {
    id: 'actions',
    header: '',
    cell: ({ row }) => {
      const invoice = row.original.raw
      const actions = [
        h(UButton, {
          color: 'neutral',
          variant: 'ghost',
          icon: 'i-lucide-file-text',
          label: 'Receipt',
          onClick: () => viewReceipt(invoice.id)
        })
      ]

      if (invoice.invoiceStatus !== 'Cancelled' && invoice.invoiceStatus !== 'Refunded' && !String(invoice.invoiceNumber || '').startsWith('SR-')) {
        actions.push(h(UButton, {
          color: 'warning',
          variant: 'ghost',
          icon: 'i-lucide-rotate-ccw',
          label: 'Return',
          onClick: () => askSalesReturn(invoice)
        }))
        actions.push(h(UButton, {
          color: 'primary',
          variant: 'ghost',
          icon: 'i-lucide-repeat-2',
          label: 'Exchange',
          onClick: () => askExchange(invoice)
        }))
      }

      if (canDelete.value && invoice.invoiceStatus !== 'Cancelled') {
        actions.push(h(UButton, {
          color: 'error',
          variant: 'ghost',
          icon: 'i-lucide-ban',
          label: 'Cancel',
          onClick: () => askCancel(invoice)
        }))
      }

      return h('div', { class: 'table-action-buttons' }, actions)
    }
  }
]

function emptyReturnForm() {
  return {
    refundAmount: 0,
    refundPaymentMode: 0,
    bankAccountId: null,
    reason: ''
  }
}

function emptyExchangeForm() {
  return {
    selectedProductId: '',
    productSearch: '',
    barcodeScan: '',
    quantity: 1,
    lineDiscount: 0,
    additionalPaidAmount: 0,
    additionalPaymentMode: 0,
    bankAccountId: null,
    reason: ''
  }
}

function emptySaleForm() {
  return {
    customerId: null,
    customerName: 'Walk-in Customer',
    customerMobileNumber: '',
    customerGstin: '',
    salesmanId: null,
    paymentMode: paymentModeValue.cash,
    paidAmount: 0,
    billDiscountAmount: 0,
    selectedProductId: '',
    productSearch: '',
    barcodeScan: '',
    quantity: 1,
    lineDiscount: 0,
    bankAccountId: null,
    storeCreditAmount: 0,
    creditNoteId: null,
    creditNoteAmount: 0,
    advanceReceiptId: null,
    advanceAmount: 0,
    loyaltyPointsToRedeem: 0
  }
}

function emptyPaymentRow(amount = 0) {
  return {
    paymentMode: paymentModeValue.cash,
    amount,
    bankAccountId: null,
    referenceNumber: '',
    gatewayReference: '',
    settlementStatus: ''
  }
}

async function refresh() {
  if (!auth.isAuthenticated.value) {
    return
  }

  loading.value = true
  try {
    setupStatus.value = await api.get<any>('setup/status')
    const selectedCompanyId = workspace.companyId.value || setupStatus.value?.companyId
    const selectedStoreId = workspace.storeId.value || setupStatus.value?.storeId
    const billingOptionQuery = new URLSearchParams()
    if (selectedCompanyId) billingOptionQuery.set('companyId', selectedCompanyId)
    if (selectedStoreId) billingOptionQuery.set('storeId', selectedStoreId)

    const [companyRows, storeRows, productRows, invoiceRows, bankAccountRows, billingOptions] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores'),
      api.list<any>('products'),
      api.get<any[]>('billing/sales/recent?take=100'),
      api.list<any>('bank-accounts'),
      api.get<any>(`billing/options?${billingOptionQuery.toString()}`)
    ])

    companies.value = companyRows
    stores.value = storeRows
    products.value = productRows
    productLookup.saveCache(productRows.map((product: any) => ({ productId: product.id, name: product.name, barcode: product.barcode, availableQty: product.currentStock || 0, mrp: product.mrp || 0, taxRate: product.taxRate || 0, taxType: String(product.taxType || 'GST'), unit: String(product.unit || 'Pcs'), category: product.productCategoryName || '', subCategory: product.productSubCategoryName || '' })))
    invoices.value = invoiceRows
    bankAccounts.value = bankAccountRows
    customers.value = billingOptions?.customers || []
    salesmen.value = billingOptions?.salesmen || []
  } catch (error) {
    feedback.failed('Billing refresh failed', error)
  } finally {
    loading.value = false
  }
}

function startCreate() {
  Object.assign(saleForm, emptySaleForm())
  saleCart.value = []
  salePayments.value = [emptyPaymentRow(0)]
  selectedCustomerProfile.value = null
  saleGstinValidation.value = null
  saleOpen.value = true
}

async function applySelectedCustomer() {
  const customer = selectedCustomer.value
  if (!customer) {
    selectedCustomerProfile.value = null
    saleForm.customerName = 'Walk-in Customer'
    saleForm.customerMobileNumber = ''
    saleForm.customerGstin = ''
    resetCustomerAdjustments()
    return
  }

  saleForm.customerName = customer.name || 'Walk-in Customer'
  saleForm.customerMobileNumber = customer.mobileNumber || ''
  saleForm.customerGstin = customer.gstin || ''
  await loadCustomerProfile(customer.id)
}

async function loadCustomerProfile(customerId: string) {
  if (!customerId) {
    return
  }

  loadingCustomerProfile.value = true
  try {
    const query = new URLSearchParams()
    const storeId = workspace.storeId.value || setupStatus.value?.storeId
    if (storeId) query.set('storeId', storeId)
    selectedCustomerProfile.value = await api.get<any>(`billing/customers/${customerId}/profile?${query.toString()}`)
  } catch (error) {
    selectedCustomerProfile.value = null
    feedback.failed('Could not load customer billing profile', error)
  } finally {
    loadingCustomerProfile.value = false
  }
}

function resetCustomerAdjustments() {
  saleForm.storeCreditAmount = 0
  saleForm.creditNoteId = null
  saleForm.creditNoteAmount = 0
  saleForm.advanceReceiptId = null
  saleForm.advanceAmount = 0
  saleForm.loyaltyPointsToRedeem = 0
}

function addPaymentRow() {
  salePayments.value.push(emptyPaymentRow(paymentBalance.value))
}

function removePaymentRow(index: number) {
  salePayments.value.splice(index, 1)
  if (salePayments.value.length === 0) {
    salePayments.value.push(emptyPaymentRow(paymentBalance.value))
  }
}

function paymentRequiresBank(payment: any) {
  return !adjustmentPaymentModes.includes(Number(payment.paymentMode)) && Number(payment.paymentMode) !== paymentModeValue.cash
}

function syncSingleCashPayment() {
  if (salePayments.value.length === 1 && Number(salePayments.value[0].paymentMode) === paymentModeValue.cash) {
    salePayments.value[0].amount = Math.max(payableTotal.value - adjustmentPaymentTotal.value, 0)
  }
}

function buildSalePayments() {
  const payments: any[] = salePayments.value
    .filter((payment) => Number(payment.amount || 0) > 0)
    .map((payment) => ({
      paymentMode: Number(payment.paymentMode),
      amount: Number(payment.amount || 0),
      bankAccountId: paymentRequiresBank(payment) ? payment.bankAccountId : null,
      referenceNumber: payment.referenceNumber || null,
      gatewayReference: payment.gatewayReference || null,
      settlementStatus: payment.settlementStatus || null,
      adjustmentSourceType: null,
      adjustmentSourceId: null
    }))

  if (storeCreditPaymentAmount.value > 0) {
    payments.push({
      paymentMode: paymentModeValue.creditBalance,
      amount: storeCreditPaymentAmount.value,
      bankAccountId: null,
      referenceNumber: 'Customer credit balance',
      gatewayReference: null,
      settlementStatus: null,
      adjustmentSourceType: 'CustomerCreditBalance',
      adjustmentSourceId: null
    })
  }

  if (creditNotePaymentAmount.value > 0 && saleForm.creditNoteId) {
    payments.push({
      paymentMode: paymentModeValue.creditNote,
      amount: creditNotePaymentAmount.value,
      bankAccountId: null,
      referenceNumber: selectedCreditNote.value?.number || 'Credit note',
      gatewayReference: null,
      settlementStatus: null,
      adjustmentSourceType: 'CreditNote',
      adjustmentSourceId: saleForm.creditNoteId
    })
  }

  if (advancePaymentAmount.value > 0 && saleForm.advanceReceiptId) {
    payments.push({
      paymentMode: paymentModeValue.creditBalance,
      amount: advancePaymentAmount.value,
      bankAccountId: null,
      referenceNumber: selectedAdvanceReceipt.value?.number || 'Customer advance',
      gatewayReference: null,
      settlementStatus: null,
      adjustmentSourceType: 'CustomerAdvanceReceipt',
      adjustmentSourceId: saleForm.advanceReceiptId
    })
  }

  if (loyaltyRedeemValue.value > 0) {
    payments.push({
      paymentMode: paymentModeValue.coupons,
      amount: loyaltyRedeemValue.value,
      bankAccountId: null,
      referenceNumber: `${Number(saleForm.loyaltyPointsToRedeem || 0)} loyalty points`,
      gatewayReference: null,
      settlementStatus: null,
      adjustmentSourceType: 'LoyaltyRedemption',
      adjustmentSourceId: null
    })
  }

  return payments
}

async function lookupSaleProduct() {
  const query = String(saleForm.barcodeScan || saleForm.productSearch || '').trim()
  if (!query) {
    feedback.notify('Barcode required', 'Scan barcode or enter product name/barcode first.', 'warning')
    return
  }

  const item = query.includes('|')
    ? productSearchOptions.value.find((row) => `${row.barcode} | ${row.name} | Qty ${Number(row.availableQty || 0)} | MRP ${Number(row.mrp || 0)}` === query)
    : await productLookup.byBarcode(query, workspace.storeId.value || undefined) || (await productLookup.searchProducts(query, workspace.storeId.value || undefined))[0]

  if (!item) {
    feedback.notify('Product not found', 'No cached or server product matched this barcode/search.', 'warning')
    return
  }

  applyLookupProductToSale(item)
}

async function refreshSaleSuggestions(value?: string) {
  const query = String(value || saleForm.productSearch || saleForm.barcodeScan || '').trim()
  productSearchOptions.value = await productLookup.searchProducts(query, workspace.storeId.value || undefined)
}

function applyLookupProductToSale(item: any) {
  if (!products.value.some((product) => product.id === item.productId)) {
    products.value.push({
      id: item.productId,
      name: item.name,
      barcode: item.barcode,
      mrp: item.mrp,
      taxRate: item.taxRate,
      unit: item.unit,
      productCategoryName: item.category,
      productSubCategoryName: item.subCategory
    })
  }

  saleForm.selectedProductId = item.productId
  saleForm.productSearch = `${item.barcode} | ${item.name}`
  saleForm.barcodeScan = item.barcode
  if (!Number(saleForm.lineDiscount || 0)) {
    saleForm.lineDiscount = 0
  }
  feedback.notify('Product loaded', `${item.name} | Available ${Number(item.availableQty || 0)} | MRP ${money(Number(item.mrp || 0))} | GST ${Number(item.taxRate || 0)}%`, 'success')
}

function addToCart() {
  if (!selectedProduct.value) {
    feedback.notify('Product missing', 'Select a product before adding to cart.', 'warning')
    return
  }

  saleCart.value.push({
    productId: selectedProduct.value.id,
    name: selectedProduct.value.name,
    barcode: selectedProduct.value.barcode,
    quantity: Number(saleForm.quantity || 0),
    mrp: Number(selectedProduct.value.mrp || 0),
    discountAmount: Number(saleForm.lineDiscount || 0)
  })

  saleForm.selectedProductId = ''
  saleForm.productSearch = ''
  saleForm.barcodeScan = ''
  saleForm.quantity = 1
  saleForm.lineDiscount = 0
  syncSingleCashPayment()
}

function removeCartItem(index: number) {
  saleCart.value.splice(index, 1)
  syncSingleCashPayment()
}

async function validateSaleGstin() {
  saleGstinValidation.value = null
  if (!saleForm.customerGstin) {
    feedback.notify('Enter customer GSTIN first', undefined, 'warning')
    return
  }

  saleGstinChecking.value = true
  try {
    saleGstinValidation.value = await api.create<any>('gstin/validate-party', {
      partyType: 'Customer',
      gstin: saleForm.customerGstin,
      name: saleForm.customerName,
      address: ''
    })

    if (saleGstinValidation.value.alerts?.length) {
      feedback.notify('Customer GSTIN alert', saleGstinValidation.value.alerts.join(' '), 'warning')
    } else {
      feedback.notify('Customer GSTIN checked', saleGstinValidation.value.lookup?.isVerified ? 'GSTIN verified.' : 'GSTIN format checked.', 'success')
    }
  } catch (error) {
    feedback.failed('Could not verify customer GSTIN', error)
  } finally {
    saleGstinChecking.value = false
  }
}

async function submitSale() {
  saving.value = true
  try {
    const selectedStore = stores.value.find((store) => store.id === workspace.storeId.value)
    const companyId = workspace.companyId.value || setupStatus.value?.companyId || selectedStore?.companyId || companies.value[0]?.id
    const storeGroupId = workspace.storeGroupId.value || selectedStore?.storeGroupId || setupStatus.value?.storeGroupId || stores.value[0]?.storeGroupId
    const storeId = workspace.storeId.value || setupStatus.value?.storeId || stores.value[0]?.id

    if (!companyId || !storeGroupId || !storeId) {
      throw new Error('Run quick setup before billing.')
    }

    if (saleCart.value.length === 0) {
      throw new Error('Add at least one item to the bill.')
    }

    const payments = buildSalePayments()
    const missingBankPayment = salePayments.value.find((payment) => Number(payment.amount || 0) > 0 && paymentRequiresBank(payment) && !payment.bankAccountId)
    if (missingBankPayment) {
      throw new Error('Select bank account for every non-cash payment row.')
    }

    const totalPaid = payments.reduce((sum, payment) => sum + Number(payment.amount || 0), 0)
    if (totalPaid > payableTotal.value) {
      throw new Error('Payment total cannot be greater than payable amount.')
    }

    if (saleForm.customerGstin && !saleGstinValidation.value) {
      await validateSaleGstin()
    }

    const response = await api.create<any>('billing/sales', {
      companyId,
      storeGroupId,
      storeId,
      customerId: saleForm.customerId || null,
      salesmanId: saleForm.salesmanId || null,
      customerName: saleForm.customerName,
      customerMobileNumber: saleForm.customerMobileNumber,
      customerGstin: saleForm.customerGstin,
      paymentMode: payments.length > 1 ? paymentModeValue.mixPayments : Number(payments[0]?.paymentMode ?? paymentModeValue.cash),
      bankAccountId: payments.find((payment) => payment.bankAccountId)?.bankAccountId || null,
      paidAmount: totalPaid,
      billDiscountAmount: Number(saleForm.billDiscountAmount || 0),
      payments,
      items: saleCart.value.map((item) => ({
        productId: item.productId,
        barcode: item.barcode,
        quantity: item.quantity,
        mrp: item.mrp,
        discountAmount: item.discountAmount
      }))
    })

    feedback.saved(`Invoice ${response.invoiceNumber || ''}`.trim())
    if (response.gstinAlerts?.length) {
      feedback.notify('Customer GSTIN alert saved', response.gstinAlerts.join(' '), 'warning')
    }
    saleOpen.value = false
    await viewReceipt(response.invoiceId)
    await refresh()
  } catch (error) {
    feedback.failed('Could not save invoice', error)
  } finally {
    saving.value = false
  }
}

async function viewReceipt(invoiceId: string) {
  try {
    selectedReceipt.value = await api.get<any>(`billing/sales/${invoiceId}/receipt`)
    invoicePrintFormat.value = 'a4'
    invoiceCopyType.value = 'customer'
    invoiceReprint.value = false
    invoiceSignatures.value = true
  } catch (error) {
    feedback.failed('Could not open receipt', error)
  }
}


async function askSalesReturn(invoice: any) {
  try {
    const receipt = await api.get<any>(`billing/sales/${invoice.id}/receipt`)
    pendingReturnInvoice.value = invoice
    returnLines.value = (receipt.items || []).map((item: any) => ({
      invoiceItemId: item.id,
      productName: item.productName,
      barcode: item.barcode,
      quantity: Number(item.quantity || 0),
      returnQuantity: 0,
      mrp: Number(item.mrp || 0),
      discountAmount: Number(item.discountAmount || 0)
    }))
    Object.assign(returnForm, emptyReturnForm())
    returnOpen.value = true
  } catch (error) {
    feedback.failed('Could not load invoice items for return', error)
  }
}

async function confirmSalesReturn() {
  if (!pendingReturnInvoice.value) {
    return
  }

  returning.value = true
  try {
    const items = returnLines.value
      .filter((item) => Number(item.returnQuantity || 0) > 0)
      .map((item) => ({ invoiceItemId: item.invoiceItemId, quantity: Number(item.returnQuantity || 0) }))

    if (items.length === 0) {
      throw new Error('Enter return quantity for at least one item.')
    }

    if (returnRefundRequiresBank.value && !returnForm.bankAccountId) {
      throw new Error('Select bank account for non-cash refund.')
    }

    const response = await api.create<any>(`billing/sales/${pendingReturnInvoice.value.id}/returns`, {
      refundAmount: Number(returnForm.refundAmount || 0),
      refundPaymentMode: Number(returnForm.refundAmount || 0) > 0 ? Number(returnForm.refundPaymentMode) : null,
      bankAccountId: returnRefundRequiresBank.value ? returnForm.bankAccountId : null,
      reason: returnForm.reason,
      items
    })

    feedback.saved(`Credit note ${response.creditNoteNumber || ''}`.trim())
    returnOpen.value = false
    pendingReturnInvoice.value = null
    await viewReceipt(response.returnInvoiceId)
    await refresh()
  } catch (error) {
    feedback.failed('Could not create sales return', error)
  } finally {
    returning.value = false
  }
}

async function askExchange(invoice: any) {
  try {
    const receipt = await api.get<any>(`billing/sales/${invoice.id}/receipt`)
    pendingExchangeInvoice.value = invoice
    exchangeReturnLines.value = (receipt.items || []).map((item: any) => ({
      invoiceItemId: item.id,
      productName: item.productName,
      barcode: item.barcode,
      quantity: Number(item.quantity || 0),
      returnQuantity: 0,
      mrp: Number(item.mrp || 0),
      discountAmount: Number(item.discountAmount || 0)
    }))
    exchangeCart.value = []
    Object.assign(exchangeForm, emptyExchangeForm())
    exchangeOpen.value = true
  } catch (error) {
    feedback.failed('Could not load invoice items for exchange', error)
  }
}

function addExchangeItem() {
  const product = products.value.find((item) => item.id === exchangeForm.selectedProductId)
  if (!product) {
    feedback.notify('Product missing', 'Select replacement product first.', 'warning')
    return
  }

  exchangeCart.value.push({
    productId: product.id,
    name: product.name,
    barcode: product.barcode,
    quantity: Number(exchangeForm.quantity || 0),
    mrp: Number(product.mrp || 0),
    discountAmount: Number(exchangeForm.lineDiscount || 0)
  })
  exchangeForm.selectedProductId = ''
  exchangeForm.quantity = 1
  exchangeForm.lineDiscount = 0
  exchangeForm.additionalPaidAmount = exchangeNetDue.value
}

function removeExchangeItem(index: number) {
  exchangeCart.value.splice(index, 1)
  exchangeForm.additionalPaidAmount = exchangeNetDue.value
}

async function confirmExchange() {
  if (!pendingExchangeInvoice.value) {
    return
  }

  exchanging.value = true
  try {
    const returnItems = exchangeReturnLines.value
      .filter((item) => Number(item.returnQuantity || 0) > 0)
      .map((item) => ({ invoiceItemId: item.invoiceItemId, quantity: Number(item.returnQuantity || 0) }))

    if (returnItems.length === 0) {
      throw new Error('Enter return quantity for at least one original item.')
    }

    if (exchangeCart.value.length === 0) {
      throw new Error('Add at least one replacement item.')
    }

    if (exchangeRequiresBank.value && !exchangeForm.bankAccountId) {
      throw new Error('Select bank account for additional non-cash payment.')
    }

    const response = await api.create<any>(`billing/sales/${pendingExchangeInvoice.value.id}/exchange`, {
      additionalPaidAmount: Number(exchangeForm.additionalPaidAmount || 0),
      additionalPaymentMode: Number(exchangeForm.additionalPaidAmount || 0) > 0 ? Number(exchangeForm.additionalPaymentMode) : null,
      bankAccountId: exchangeRequiresBank.value ? exchangeForm.bankAccountId : null,
      reason: exchangeForm.reason,
      returnItems,
      newItems: exchangeCart.value.map((item) => ({
        productId: item.productId,
        barcode: item.barcode,
        quantity: Number(item.quantity || 0),
        mrp: Number(item.mrp || 0),
        discountAmount: Number(item.discountAmount || 0)
      }))
    })

    feedback.saved(`Exchange ${response.exchangeInvoiceNumber || ''}`.trim())
    exchangeOpen.value = false
    pendingExchangeInvoice.value = null
    await viewReceipt(response.exchangeInvoiceId)
    await refresh()
  } catch (error) {
    feedback.failed('Could not create exchange', error)
  } finally {
    exchanging.value = false
  }
}

function clampReturnQuantity(item: any) {
  const value = Number(item.returnQuantity || 0)
  item.returnQuantity = Math.min(Math.max(value, 0), Number(item.quantity || 0))
}

function askCancel(invoice: any) {
  if (invoice.invoiceStatus === 'Cancelled') {
    return
  }

  pendingCancel.value = invoice
  cancelOpen.value = true
}

async function confirmCancel() {
  if (!pendingCancel.value) {
    return
  }

  cancelling.value = true
  try {
    await api.create<any>(`billing/sales/${pendingCancel.value.id}/cancel`, {
      reason: 'Cancelled from billing page'
    })

    if (selectedReceipt.value?.id === pendingCancel.value.id) {
      selectedReceipt.value = null
    }

    feedback.notify('Invoice cancelled', 'Stock quantities were reversed.', 'warning')
    cancelOpen.value = false
    pendingCancel.value = null
    await refresh()
  } catch (error) {
    feedback.failed('Could not cancel invoice', error)
  } finally {
    cancelling.value = false
  }
}

function printReceipt() {
  const style = document.createElement('style')
  style.id = 'garmetix-invoice-page-size'
  style.textContent = invoicePrintFormat.value === 'a5'
    ? '@page { size: A5 portrait; margin: 7mm; }'
    : invoicePrintFormat.value === 'thermal-2'
      ? '@page { size: 58mm auto; margin: 2mm; }'
      : invoicePrintFormat.value === 'thermal-3'
        ? '@page { size: 80mm auto; margin: 3mm; }'
        : '@page { size: A4 portrait; margin: 8mm; }'
  document.getElementById(style.id)?.remove()
  document.head.appendChild(style)
  window.print()
  window.setTimeout(() => style.remove(), 1000)
}

async function downloadInvoicePdf() {
  if (!selectedReceipt.value?.id) {
    return
  }

  downloadingInvoicePdf.value = true
  try {
    const query = new URLSearchParams({
      format: invoicePrintFormat.value,
      copy: invoiceCopyType.value,
      reprint: String(invoiceReprint.value),
      signatures: String(invoiceSignatures.value)
    })
    const response = await fetch(
      `${config.public.apiBase}/billing/sales/${selectedReceipt.value.id}/pdf?${query.toString()}`,
      { headers: api.authHeaders() }
    )
    if (!response.ok) {
      throw new Error(`Invoice PDF could not be generated (${response.status}).`)
    }

    const blob = await response.blob()
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = `${selectedReceipt.value.invoiceNumber || 'invoice'}-${invoicePrintFormat.value}.pdf`
    document.body.appendChild(link)
    link.click()
    link.remove()
    URL.revokeObjectURL(url)
    feedback.notify('Invoice PDF downloaded')
  } catch (error) {
    feedback.failed('Could not download invoice PDF', error)
  } finally {
    downloadingInvoicePdf.value = false
  }
}

function lineTotal(item: any) {
  return Math.max((Number(item.mrp || 0) - Number(item.discountAmount || 0)) * Number(item.quantity || 0), 0)
}

function formatDate(value: string) {
  return value ? new Date(value).toLocaleDateString() : '-'
}

function money(value: number) {
  return new Intl.NumberFormat('en-IN', {
    style: 'currency',
    currency: 'INR',
    maximumFractionDigits: 2
  }).format(value || 0)
}

onMounted(async () => {
  auth.restore()
  await refresh()
})

watch(() => saleForm.customerId, async () => {
  await applySelectedCustomer()
})

watch(() => saleForm.creditNoteId, () => {
  saleForm.creditNoteAmount = selectedCreditNote.value?.availableAmount || 0
  syncSingleCashPayment()
})

watch(() => saleForm.advanceReceiptId, () => {
  saleForm.advanceAmount = selectedAdvanceReceipt.value?.availableAmount || 0
  syncSingleCashPayment()
})

watch(() => [saleForm.storeCreditAmount, saleForm.creditNoteAmount, saleForm.advanceAmount, saleForm.loyaltyPointsToRedeem, saleForm.billDiscountAmount], () => {
  syncSingleCashPayment()
})

watch(() => saleForm.paymentMode, () => {
  if (requiresBankAccount.value && !saleForm.bankAccountId) {
    saleForm.bankAccountId = bankAccounts.value[0]?.id || null
  }
})

watch(() => saleForm.paidAmount, () => {
  if (requiresBankAccount.value && !saleForm.bankAccountId) {
    saleForm.bankAccountId = bankAccounts.value[0]?.id || null
  }
})

watch(() => returnForm.refundPaymentMode, () => {
  if (returnRefundRequiresBank.value && !returnForm.bankAccountId) {
    returnForm.bankAccountId = bankAccounts.value[0]?.id || null
  }
})

watch(() => returnForm.refundAmount, () => {
  if (returnRefundRequiresBank.value && !returnForm.bankAccountId) {
    returnForm.bankAccountId = bankAccounts.value[0]?.id || null
  }
})

watch(() => exchangeForm.additionalPaymentMode, () => {
  if (exchangeRequiresBank.value && !exchangeForm.bankAccountId) {
    exchangeForm.bankAccountId = bankAccounts.value[0]?.id || null
  }
})

watch(() => exchangeForm.additionalPaidAmount, () => {
  if (exchangeRequiresBank.value && !exchangeForm.bankAccountId) {
    exchangeForm.bankAccountId = bankAccounts.value[0]?.id || null
  }
})
</script>

<template>
  <AuthScreen v-if="!isAuthenticated" @authenticated="refresh" />

  <AppShell
    v-else
    title="Billing"
    :companies="companies"
    :stores="stores"
    @refresh="refresh"
  >
    <section class="planner-dashboard">
      <UiModulePageHeader
        title="Sales Billing"
        description="Create POS invoices, print receipts, and cancel invoices with stock reversal."
        icon="i-lucide-receipt-indian-rupee"
        primary-label="New Invoice"
        primary-icon="i-lucide-plus"
        @primary="startCreate"
      >
        <template #actions>
          <UBadge :color="loading ? 'warning' : 'success'" variant="subtle">
            {{ loading ? 'Loading' : `${invoices.length} invoices` }}
          </UBadge>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="subtle" :loading="loading" label="Refresh" @click="refresh" />
          <UButton icon="i-lucide-plus" label="New Invoice" @click="startCreate" />
        </template>
      </UiModulePageHeader>

      <div class="planner-metric-grid">
        <UCard v-for="metric in metrics" :key="metric.label" class="planner-metric-card">
          <div class="planner-metric-body">
            <UAvatar :icon="metric.icon" :color="metric.color" variant="subtle" />
            <div>
              <p>{{ metric.label }}</p>
              <strong>{{ metric.value }}</strong>
              <span>{{ metric.meta }}</span>
            </div>
          </div>
        </UCard>
      </div>

      <UCard class="planner-card">
        <template #header>
          <div class="planner-card-header">
            <div>
              <h2>Invoice Register</h2>
              <p>Search invoice number, customer, status, or date.</p>
            </div>
            <UBadge color="neutral" variant="subtle">{{ filteredRows.length }} shown</UBadge>
          </div>
        </template>

        <UiCrudToolbar
          v-model:search="search"
          search-placeholder="Search invoice or customer"
          :loading="loading"
          refresh-label="Sync"
          create-label="New Invoice"
          @refresh="refresh"
          @create="startCreate"
        />

        <UTable
          v-if="filteredRows.length"
          :data="filteredRows"
          :columns="columns"
          :loading="loading"
        />

        <UiCrudEmptyState
          v-else
          title="No invoices found"
          description="Create the first sales invoice from the POS billing workflow."
          icon="i-lucide-receipt-indian-rupee"
          action-label="New Invoice"
          @action="startCreate"
        />
      </UCard>

      <UiFormSlideover
        v-model:open="saleOpen"
        title="New Sales Invoice"
        description="Select products, add quantities, collect payment, and save the invoice."
        submit-label="Save Invoice"
        layout="modal"
        content-class="w-[calc(100vw-2rem)] sm:max-w-6xl xl:max-w-7xl"
        :loading="saving"
        @submit="submitSale"
      >
        <USeparator label="Customer & salesman" />
        <div class="form-two-column">
          <UFormField label="Existing customer">
            <USelect v-model="saleForm.customerId" :items="customerOptions" placeholder="Walk-in / search customer" :loading="loadingCustomerProfile" />
          </UFormField>
          <UFormField label="Salesman">
            <USelect v-model="saleForm.salesmanId" :items="salesmanOptions" placeholder="Counter sale" />
          </UFormField>
        </div>
        <div class="form-two-column">
          <UFormField label="Customer name">
            <UInput v-model="saleForm.customerName" />
          </UFormField>
          <UFormField label="Mobile">
            <UInput v-model="saleForm.customerMobileNumber" />
          </UFormField>
        </div>
        <UFormField label="Customer GSTIN">
          <div class="inline-action-row">
            <UInput v-model="saleForm.customerGstin" class="flex-1" placeholder="22AAAAA0000A1Z5" />
            <UButton color="neutral" variant="subtle" icon="i-lucide-search-check" label="Check" type="button" :loading="saleGstinChecking" @click="validateSaleGstin" />
          </div>
        </UFormField>
        <UAlert
          v-if="saleGstinValidation?.alerts?.length"
          color="warning"
          variant="subtle"
          title="Customer GSTIN alert"
          :description="saleGstinValidation.alerts.join(' ')"
        />
        <UAlert
          v-if="selectedCustomerProfile?.customer"
          color="primary"
          variant="subtle"
          title="Customer balance"
          :description="`Credit ${money(Number(selectedCustomerProfile.customer.creditBalance || 0))} | Loyalty ${Number(selectedCustomerProfile.customer.loyaltyPoints || 0)} pts | Bills ${selectedCustomerProfile.customer.billCount || 0}`"
        />

        <USeparator label="Item" />

        <div class="form-two-column">
          <UFormField label="Barcode scan">
            <div class="inline-action-row">
              <UInput v-model="saleForm.barcodeScan" class="flex-1" placeholder="Scan barcode" @keyup.enter="lookupSaleProduct" />
              <UButton color="neutral" variant="subtle" icon="i-lucide-scan-barcode" label="Fetch" type="button" @click="lookupSaleProduct" />
            </div>
          </UFormField>
          <UFormField label="Product autocomplete">
            <UInput v-model="saleForm.productSearch" list="sale-product-cache" placeholder="Type name, barcode, HSN" @input="refreshSaleSuggestions(saleForm.productSearch)" @change="lookupSaleProduct" />
            <datalist id="sale-product-cache">
              <option v-for="option in saleProductSuggestions" :key="option" :value="option" />
            </datalist>
          </UFormField>
        </div>
        <UAlert
          v-if="selectedProduct"
          color="neutral"
          variant="subtle"
          title="Selected product"
          :description="`${selectedProduct.name || selectedProduct.barcode} | Barcode ${selectedProduct.barcode} | MRP ${money(Number(selectedProduct.mrp || 0))} | GST ${Number(selectedProduct.taxRate || 0)}%`"
        />
        <div class="form-two-column">
          <UFormField label="Quantity">
            <UInput v-model="saleForm.quantity" min="1" type="number" />
          </UFormField>
          <UFormField label="Line discount">
            <UInput v-model="saleForm.lineDiscount" min="0" step="0.01" type="number" />
          </UFormField>
        </div>
        <UButton color="neutral" variant="subtle" icon="i-lucide-plus" label="Add Item" type="button" @click="addToCart" />

        <div class="planner-table-wrap">
          <table class="planner-table">
            <thead>
              <tr>
                <th>Item</th>
                <th>Qty</th>
                <th>Total</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(item, index) in saleCart" :key="`${item.productId}-${index}`">
                <td>{{ item.name }}</td>
                <td>{{ item.quantity }}</td>
                <td>{{ money(lineTotal(item)) }}</td>
                <td>
                  <UButton color="error" variant="ghost" icon="i-lucide-x" size="xs" type="button" @click="removeCartItem(index)" />
                </td>
              </tr>
              <tr v-if="saleCart.length === 0">
                <td colspan="4">No items</td>
              </tr>
            </tbody>
          </table>
        </div>

        <USeparator label="Payment" />

        <UFormField label="Bill discount">
          <UInput v-model="saleForm.billDiscountAmount" min="0" step="0.01" type="number" @blur="syncSingleCashPayment" />
        </UFormField>

        <USeparator label="Customer adjustments" />
        <div class="form-two-column">
          <UFormField label="Use store credit balance">
            <UInput v-model="saleForm.storeCreditAmount" min="0" :max="selectedCustomerProfile?.customer?.creditBalance || 0" step="0.01" type="number" @blur="syncSingleCashPayment" />
          </UFormField>
          <UFormField label="Redeem loyalty points">
            <UInput v-model="saleForm.loyaltyPointsToRedeem" min="0" :max="selectedCustomerProfile?.customer?.loyaltyPoints || 0" step="0.01" type="number" @blur="syncSingleCashPayment" />
          </UFormField>
        </div>
        <div class="form-two-column">
          <UFormField label="Credit note">
            <USelect v-model="saleForm.creditNoteId" :items="creditNoteOptions" placeholder="Select credit note" />
          </UFormField>
          <UFormField label="Credit note amount">
            <UInput v-model="saleForm.creditNoteAmount" min="0" :max="selectedCreditNote?.availableAmount || 0" step="0.01" type="number" @blur="syncSingleCashPayment" />
          </UFormField>
        </div>
        <div class="form-two-column">
          <UFormField label="Advance receipt">
            <USelect v-model="saleForm.advanceReceiptId" :items="advanceReceiptOptions" placeholder="Select advance receipt" />
          </UFormField>
          <UFormField label="Advance amount">
            <UInput v-model="saleForm.advanceAmount" min="0" :max="selectedAdvanceReceipt?.availableAmount || 0" step="0.01" type="number" @blur="syncSingleCashPayment" />
          </UFormField>
        </div>

        <USeparator label="Split payment rows" />
        <div class="planner-table-wrap">
          <table class="planner-table">
            <thead>
              <tr>
                <th>Mode</th>
                <th>Amount</th>
                <th>Bank / ref</th>
                <th>Gateway / settlement</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(payment, index) in salePayments" :key="`payment-${index}`">
                <td><USelect v-model="payment.paymentMode" :items="paymentModeOptions" /></td>
                <td><UInput v-model="payment.amount" min="0" step="0.01" type="number" /></td>
                <td>
                  <USelect v-if="paymentRequiresBank(payment)" v-model="payment.bankAccountId" :items="bankAccountOptions" placeholder="Bank account" />
                  <UInput v-model="payment.referenceNumber" placeholder="Ref / UTR / cheque" />
                </td>
                <td>
                  <UInput v-model="payment.gatewayReference" placeholder="Gateway reference" />
                  <UInput v-model="payment.settlementStatus" placeholder="Settlement status" />
                </td>
                <td><UButton color="error" variant="ghost" icon="i-lucide-x" size="xs" type="button" @click="removePaymentRow(index)" /></td>
              </tr>
            </tbody>
          </table>
        </div>
        <UButton color="neutral" variant="subtle" icon="i-lucide-plus" label="Add Payment Row" type="button" @click="addPaymentRow" />

        <div class="payroll-summary">
          <span>Cart total</span><strong>{{ money(cartTotal) }}</strong>
          <span>Discount</span><strong>{{ money(Number(saleForm.billDiscountAmount || 0)) }}</strong>
          <span>Payable</span><strong>{{ money(payableTotal) }}</strong>
          <span>Customer adjustments</span><strong>{{ money(adjustmentPaymentTotal) }}</strong>
          <span>Manual payments</span><strong>{{ money(manualPaymentTotal) }}</strong>
          <span>Balance / credit sale</span><strong>{{ money(paymentBalance) }}</strong>
        </div>
      </UiFormSlideover>

      <UModal v-model:open="receiptOpen" title="Invoice Receipt" :ui="{ content: 'max-w-3xl' }">
        <template #body>
          <div v-if="selectedReceipt" class="invoice-print-toolbar no-print">
            <UFormField label="Format">
              <USelect v-model="invoicePrintFormat" :items="invoicePrintFormatOptions" />
            </UFormField>
            <UFormField label="Copy">
              <USelect v-model="invoiceCopyType" :items="invoiceCopyOptions" />
            </UFormField>
            <UCheckbox v-model="invoiceReprint" label="Reprint" />
            <UCheckbox v-model="invoiceSignatures" label="Signature lines" />
          </div>

          <div
            v-if="selectedReceipt"
            class="receipt-print invoice-print-document"
            :class="[`invoice-print-${invoicePrintFormat}`, `invoice-copy-${invoiceCopyType}`, { 'invoice-reprint': invoiceReprint }]"
          >
            <header class="receipt-header">
              <div class="invoice-copy-chip">{{ invoiceCopyOptions.find(item => item.value === invoiceCopyType)?.label }}</div>
              <div v-if="invoiceReprint" class="invoice-reprint-chip">REPRINT</div>
              <h2>{{ selectedReceipt.companyName }}</h2>
              <p>{{ selectedReceipt.storeName }}</p>
              <p>Tax Invoice {{ selectedReceipt.invoiceNumber }}</p>
              <p>{{ new Date(selectedReceipt.onDate).toLocaleString() }}</p>
            </header>

            <div class="receipt-customer">
              <span>{{ selectedReceipt.customerName }}</span>
              <span>{{ selectedReceipt.customerMobileNumber }}</span>
            </div>

            <table class="receipt-table">
              <thead>
                <tr>
                  <th>Item</th>
                  <th>Qty</th>
                  <th>MRP</th>
                  <th>Tax</th>
                  <th>Amount</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="item in selectedReceipt.items" :key="`${item.barcode}-${item.productName}`">
                  <td>{{ item.productName }}</td>
                  <td>{{ item.quantity }}</td>
                  <td>{{ money(Number(item.mrp || 0)) }}</td>
                  <td>{{ money(Number(item.taxAmount || 0)) }}</td>
                  <td>{{ money(Number(item.amount || 0)) }}</td>
                </tr>
              </tbody>
            </table>

            <div class="receipt-totals">
              <span>MRP</span><strong>{{ money(Number(selectedReceipt.mrp || selectedReceipt.MRP || 0)) }}</strong>
              <span>Discount</span><strong>{{ money(Number(selectedReceipt.discountAmount || 0)) }}</strong>
              <span>Tax</span><strong>{{ money(Number(selectedReceipt.taxAmount || 0)) }}</strong>
              <span>Round off</span><strong>{{ money(Number(selectedReceipt.roundOff || 0)) }}</strong>
              <span>Bill amount</span><strong>{{ money(Number(selectedReceipt.billAmount || 0)) }}</strong>
              <span>Paid</span><strong>{{ money(Number(selectedReceipt.paidAmount || 0)) }}</strong>
              <span>Balance</span><strong>{{ money(Number(selectedReceipt.balanceAmount || 0)) }}</strong>
            </div>

            <div v-if="selectedReceipt.payments?.length" class="planner-table-wrap no-print">
              <table class="planner-table">
                <thead><tr><th>Payment</th><th>Amount</th><th>Reference</th><th>Source</th></tr></thead>
                <tbody>
                  <tr v-for="payment in selectedReceipt.payments" :key="`${payment.paymentMode}-${payment.amount}-${payment.referenceNumber}`">
                    <td>{{ payment.paymentMode }}</td>
                    <td>{{ money(Number(payment.amount || 0)) }}</td>
                    <td>{{ payment.referenceNumber || payment.gatewayReference || '-' }}</td>
                    <td>{{ payment.adjustmentSourceType || payment.settlementStatus || '-' }}</td>
                  </tr>
                </tbody>
              </table>
            </div>

            <div v-if="invoiceSignatures" class="invoice-signatures">
              <div>Prepared by</div>
              <div>Checked by</div>
              <div>Customer</div>
              <div>Authorized</div>
            </div>

            <footer class="receipt-footer">
              Thank you for shopping with us.
            </footer>
          </div>
        </template>

        <template #footer>
          <div class="modal-actions">
            <UButton color="neutral" variant="outline" label="Close" @click="receiptOpen = false" />
            <UButton
              color="neutral"
              variant="soft"
              icon="i-lucide-file-down"
              label="Download PDF"
              :loading="downloadingInvoicePdf"
              @click="downloadInvoicePdf"
            />
            <UButton icon="i-lucide-printer" label="Print" @click="printReceipt" />
          </div>
        </template>
      </UModal>


      <UiFormSlideover
        v-model:open="returnOpen"
        title="Sales Return / Credit Note"
        :description="`Create partial return for ${pendingReturnInvoice?.invoiceNumber || 'invoice'}.`"
        submit-label="Create Credit Note"
        layout="modal"
        content-class="w-[calc(100vw-2rem)] sm:max-w-4xl"
        :loading="returning"
        @submit="confirmSalesReturn"
      >
        <UAlert
          color="warning"
          variant="subtle"
          title="Partial return"
          description="Returned quantity will reverse sold stock. Refund amount is optional; any balance becomes customer store credit."
        />
        <div class="planner-table-wrap">
          <table class="planner-table">
            <thead>
              <tr>
                <th>Item</th>
                <th>Sold</th>
                <th>Return</th>
                <th>Approx credit</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="item in returnLines" :key="item.invoiceItemId">
                <td>{{ item.productName }}<br><small>{{ item.barcode }}</small></td>
                <td>{{ item.quantity }}</td>
                <td>
                  <UInput v-model="item.returnQuantity" min="0" :max="item.quantity" step="1" type="number" @blur="clampReturnQuantity(item)" />
                </td>
                <td>{{ money(lineTotal({ mrp: item.mrp, discountAmount: item.discountAmount, quantity: item.returnQuantity })) }}</td>
              </tr>
            </tbody>
          </table>
        </div>
        <div class="form-two-column">
          <UFormField label="Refund amount">
            <UInput v-model="returnForm.refundAmount" min="0" step="0.01" type="number" />
          </UFormField>
          <UFormField label="Refund mode">
            <USelect v-model="returnForm.refundPaymentMode" :items="paymentModeOptions" />
          </UFormField>
        </div>
        <UFormField v-if="returnRefundRequiresBank" label="Bank account" required>
          <USelect v-model="returnForm.bankAccountId" :items="bankAccountOptions" placeholder="Select bank account" />
        </UFormField>
        <UFormField label="Reason / remarks">
          <UTextarea v-model="returnForm.reason" :rows="3" />
        </UFormField>
        <div class="payroll-summary">
          <span>Return value</span><strong>{{ money(returnTotal) }}</strong>
          <span>Refund now</span><strong>{{ money(Number(returnForm.refundAmount || 0)) }}</strong>
          <span>Store credit</span><strong>{{ money(Math.max(returnTotal - Number(returnForm.refundAmount || 0), 0)) }}</strong>
        </div>
      </UiFormSlideover>

      <UiFormSlideover
        v-model:open="exchangeOpen"
        title="Exchange Item"
        :description="`Return selected items and create replacement bill for ${pendingExchangeInvoice?.invoiceNumber || 'invoice'}.`"
        submit-label="Create Exchange"
        layout="modal"
        content-class="w-[calc(100vw-2rem)] sm:max-w-6xl xl:max-w-7xl"
        :loading="exchanging"
        @submit="confirmExchange"
      >
        <UAlert
          color="primary"
          variant="subtle"
          title="Exchange flow"
          description="Returned items create store credit, replacement items create a new invoice, and any extra payable amount is collected as additional payment."
        />

        <USeparator label="Original items to return" />
        <div class="planner-table-wrap">
          <table class="planner-table">
            <thead>
              <tr>
                <th>Item</th>
                <th>Sold</th>
                <th>Return</th>
                <th>Credit</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="item in exchangeReturnLines" :key="item.invoiceItemId">
                <td>{{ item.productName }}<br><small>{{ item.barcode }}</small></td>
                <td>{{ item.quantity }}</td>
                <td>
                  <UInput v-model="item.returnQuantity" min="0" :max="item.quantity" step="1" type="number" @blur="clampReturnQuantity(item)" />
                </td>
                <td>{{ money(lineTotal({ mrp: item.mrp, discountAmount: item.discountAmount, quantity: item.returnQuantity })) }}</td>
              </tr>
            </tbody>
          </table>
        </div>

        <USeparator label="Replacement items" />
        <UFormField label="Replacement product">
          <USelect v-model="exchangeForm.selectedProductId" :items="productOptions" />
        </UFormField>
        <div class="form-two-column">
          <UFormField label="Quantity">
            <UInput v-model="exchangeForm.quantity" min="1" type="number" />
          </UFormField>
          <UFormField label="Line discount">
            <UInput v-model="exchangeForm.lineDiscount" min="0" step="0.01" type="number" />
          </UFormField>
        </div>
        <UButton color="neutral" variant="subtle" icon="i-lucide-plus" label="Add Replacement" type="button" @click="addExchangeItem" />

        <div class="planner-table-wrap">
          <table class="planner-table">
            <thead>
              <tr>
                <th>Item</th>
                <th>Qty</th>
                <th>Total</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(item, index) in exchangeCart" :key="`${item.productId}-${index}`">
                <td>{{ item.name }}<br><small>{{ item.barcode }}</small></td>
                <td>{{ item.quantity }}</td>
                <td>{{ money(lineTotal(item)) }}</td>
                <td><UButton color="error" variant="ghost" icon="i-lucide-x" size="xs" type="button" @click="removeExchangeItem(index)" /></td>
              </tr>
              <tr v-if="exchangeCart.length === 0"><td colspan="4">No replacement items</td></tr>
            </tbody>
          </table>
        </div>

        <USeparator label="Additional payment" />
        <div class="form-two-column">
          <UFormField label="Additional paid">
            <UInput v-model="exchangeForm.additionalPaidAmount" min="0" step="0.01" type="number" />
          </UFormField>
          <UFormField label="Payment mode">
            <USelect v-model="exchangeForm.additionalPaymentMode" :items="paymentModeOptions" />
          </UFormField>
        </div>
        <UFormField v-if="exchangeRequiresBank" label="Bank account" required>
          <USelect v-model="exchangeForm.bankAccountId" :items="bankAccountOptions" placeholder="Select bank account" />
        </UFormField>
        <UFormField label="Reason / remarks">
          <UTextarea v-model="exchangeForm.reason" :rows="3" />
        </UFormField>
        <div class="payroll-summary">
          <span>Return credit</span><strong>{{ money(exchangeReturnTotal) }}</strong>
          <span>Replacement bill</span><strong>{{ money(exchangeNewTotal) }}</strong>
          <span>Net extra due</span><strong>{{ money(exchangeNetDue) }}</strong>
        </div>
      </UiFormSlideover>

      <UiConfirmDeleteModal
        v-model:open="cancelOpen"
        title="Cancel Invoice"
        :description="`Cancel invoice ${pendingCancel?.invoiceNumber || ''}? Stock will be reversed.`"
        confirm-label="Cancel Invoice"
        :loading="cancelling"
        @confirm="confirmCancel"
      />
    </section>
  </AppShell>
</template>
