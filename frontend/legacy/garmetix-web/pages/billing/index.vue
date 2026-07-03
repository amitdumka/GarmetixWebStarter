<script setup lang="ts">
import { h, resolveComponent } from 'vue'
import type { TableColumn } from '@nuxt/ui'

const api = useGarmetixApi()
const router = useRouter()
const route = useRoute()
const auth = useAuth()
const workspace = useWorkspace()
const feedback = useUiFeedback()
const productLookup = useProductLookup()
const documentPrint = useServerDocumentPrint()
const config = useRuntimeConfig()
const isAuthenticated = auth.isAuthenticated
const canDelete = auth.canDelete
const canEditInvoice = computed(() => auth.canSeeAdmin.value || ['PowerUser', 'Accountant', 'RemoteAccountant'].some((role) => String(auth.user.value?.role || auth.user.value?.userType || '').toLowerCase() === role.toLowerCase()))
const canAdminHardDelete = auth.canSeeAdmin

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
const pendingEditInvoice = ref<any | null>(null)
const loading = ref(false)
const saving = ref(false)
const cancelling = ref(false)
const hardDeleting = ref(false)
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
const expandedRemarkInvoiceIds = ref<Record<string, boolean>>({})
const invoiceStatusFilter = ref('all')
const now = new Date()
const saleDatePreset = ref('today')
const invoiceMonth = ref(now.getMonth() + 1)
const invoiceYear = ref(now.getFullYear())
const customFromDate = ref(todayInputDate())
const customToDate = ref(todayInputDate())
const invoicePage = ref(1)
const invoicePageSize = ref(50)
const invoiceTotal = ref(0)
const invoiceServerSummary = reactive({ billAmount: 0, paidAmount: 0, balanceAmount: 0, cancelled: 0 })
const loadError = ref('')
const saleOpen = ref(false)
const editInvoiceOpen = ref(false)
const editingInvoice = ref(false)
const cancelOpen = ref(false)
const invoicePrintFormat = ref<'a4' | 'a5' | 'thermal-2' | 'thermal-3'>('a4')
const invoiceCopyType = ref<'customer' | 'office' | 'duplicate'>('customer')
const invoiceReprint = ref(false)
const invoiceSignatures = ref(true)
const editInvoiceForm = reactive<any>({ invoiceNumber: '', onDate: '', customerName: '', customerMobileNumber: '', customerGstin: '', salesmanId: null, remarks: '' })

const restoringBillingState = ref(false)
const lastOpenedBillingDeepLink = ref('')
const BILLING_STATE_KEY = 'garmetix.billing.invoiceRegisterState.v1'
const fromDayBook = computed(() => route.query.fromDayBook === '1')

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

const saleDatePresetOptions = [
  { value: 'today', label: 'Today' },
  { value: 'yesterday', label: 'Yesterday' },
  { value: 'month', label: 'This month' },
  { value: 'last-month', label: 'Last month' },
  { value: 'year', label: 'This year' },
  { value: 'month-year', label: 'Month-year' },
  { value: 'custom', label: 'Custom' }
]

const monthOptions = [
  { value: 1, label: 'January' },
  { value: 2, label: 'February' },
  { value: 3, label: 'March' },
  { value: 4, label: 'April' },
  { value: 5, label: 'May' },
  { value: 6, label: 'June' },
  { value: 7, label: 'July' },
  { value: 8, label: 'August' },
  { value: 9, label: 'September' },
  { value: 10, label: 'October' },
  { value: 11, label: 'November' },
  { value: 12, label: 'December' }
]

const yearOptions = computed(() => {
  const current = new Date().getFullYear()
  return Array.from({ length: 7 }, (_, index) => current - index).map((value) => ({ value, label: String(value) }))
})

const pageSizeOptions = [
  { value: 25, label: '25 / page' },
  { value: 50, label: '50 / page' },
  { value: 100, label: '100 / page' },
  { value: 200, label: '200 / page' }
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
  { value: '__select__', label: 'Select product' },
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

const invoiceSummary = computed(() => invoiceServerSummary)

const metrics = computed(() => [
  {
    label: 'Invoices',
    value: invoiceTotal.value,
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
  invoiceStatus: invoice.invoiceStatus || 'Pending',
  digitalBillStatus: invoice.digitalBillPublicPath
    ? `${invoice.digitalBillWhatsAppStatus || 'Link Ready'}${invoice.digitalBillIsActive === false ? ' / Disabled' : ''}`
    : 'Not Generated',
  remarks: invoice.remarks || '',
  raw: invoice
})))

const filteredRows = computed(() => tableRows.value)
const invoicePageFrom = computed(() => invoiceTotal.value === 0 ? 0 : ((invoicePage.value - 1) * invoicePageSize.value) + 1)
const invoicePageTo = computed(() => Math.min(invoicePage.value * invoicePageSize.value, invoiceTotal.value))
const invoiceTotalPages = computed(() => Math.max(1, Math.ceil(invoiceTotal.value / invoicePageSize.value)))
const saleDateRangeLabel = computed(() => {
  if (saleDatePreset.value === 'month-year') {
    const monthName = monthOptions.find((item) => item.value === invoiceMonth.value)?.label || String(invoiceMonth.value)
    return `${monthName} ${invoiceYear.value}`
  }
  if (saleDatePreset.value === 'custom') {
    return `${customFromDate.value || 'From'} to ${customToDate.value || 'To'}`
  }
  return saleDatePresetOptions.find((item) => item.value === saleDatePreset.value)?.label || 'Today'
})

const receiptSummaryRows = computed(() => {
  const receipt = selectedReceipt.value
  if (!receipt) return []
  return [
    { label: 'Invoice ID', value: receipt.id, mono: true },
    { label: 'Invoice No.', value: receipt.invoiceNumber || '-' },
    { label: 'Date', value: formatDateTime(receipt.onDate) },
    { label: 'Customer', value: receipt.customerName || 'Walk-in Customer' },
    { label: 'Mobile', value: receipt.customerMobileNumber || '-' },
    { label: 'Bill amount', value: money(Number(receipt.billAmount || 0)) },
    { label: 'Paid', value: money(Number(receipt.paidAmount || 0)) },
    { label: 'Balance', value: money(Number(receipt.balanceAmount || 0)) },
    { label: 'Remarks', value: receipt.remarks || '-' }
  ]
})

const selectedReceiptPaymentRows = computed(() => (selectedReceipt.value?.payments || []).map((payment: any) => ({
  id: payment.id || '',
  onDate: formatDateTime(payment.onDate),
  paymentMode: paymentModeLabel(payment.paymentMode),
  amount: money(Number(payment.amount || 0)),
  reference: payment.referenceNumber || payment.gatewayReference || '-',
  status: payment.settlementStatus || payment.adjustmentSourceType || '-'
})))

function saleItemBarcode(item: any) {
  const barcode = String(item?.barcode || '').trim()
  return barcode || 'Barcode missing'
}

function saleItemDisplayName(item: any) {
  return String(item?.productName || item?.name || item?.barcode || 'Product').trim() || 'Product'
}

function shortRemark(value: unknown, length = 10) {
  const text = String(value || '').trim()
  if (!text) return '-'
  return text.length <= length ? text : `${text.slice(0, length)}...`
}

function isRemarkExpanded(invoiceId: string) {
  return expandedRemarkInvoiceIds.value[invoiceId] === true
}

function toggleRemark(invoiceId: string) {
  expandedRemarkInvoiceIds.value = {
    ...expandedRemarkInvoiceIds.value,
    [invoiceId]: !expandedRemarkInvoiceIds.value[invoiceId]
  }
}


function isValidOptionValue(value: unknown, options: { value: string | number }[]) {
  return options.some((option) => option.value === value)
}

function isInputDate(value: unknown): value is string {
  return typeof value === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(value)
}

function safePositiveInt(value: unknown, fallback: number) {
  const parsed = Number(value)
  return Number.isFinite(parsed) && parsed > 0 ? Math.floor(parsed) : fallback
}

function currentBillingState() {
  return {
    search: search.value,
    invoiceStatusFilter: invoiceStatusFilter.value,
    saleDatePreset: saleDatePreset.value,
    invoiceMonth: invoiceMonth.value,
    invoiceYear: invoiceYear.value,
    customFromDate: customFromDate.value,
    customToDate: customToDate.value,
    invoicePage: invoicePage.value,
    invoicePageSize: invoicePageSize.value,
    savedAt: new Date().toISOString()
  }
}

function persistBillingState() {
  if (!import.meta.client || restoringBillingState.value) return
  sessionStorage.setItem(BILLING_STATE_KEY, JSON.stringify(currentBillingState()))
}

function restoreBillingState() {
  if (!import.meta.client) return
  const raw = sessionStorage.getItem(BILLING_STATE_KEY)
  if (!raw) return
  try {
    const stored = JSON.parse(raw)
    restoringBillingState.value = true
    search.value = typeof stored.search === 'string' ? stored.search : ''
    invoiceStatusFilter.value = isValidOptionValue(stored.invoiceStatusFilter, [
      { value: 'all' }, { value: 'pending' }, { value: 'paid' }, { value: 'partiallyPaid' },
      { value: 'cancelled' }, { value: 'refunded' }, { value: 'partiallyRefunded' }, { value: 'overdue' }, { value: 'draft' }
    ]) ? stored.invoiceStatusFilter : 'all'
    saleDatePreset.value = isValidOptionValue(stored.saleDatePreset, saleDatePresetOptions) ? stored.saleDatePreset : 'today'
    invoiceMonth.value = isValidOptionValue(stored.invoiceMonth, monthOptions) ? Number(stored.invoiceMonth) : now.getMonth() + 1
    invoiceYear.value = isValidOptionValue(stored.invoiceYear, yearOptions.value) ? Number(stored.invoiceYear) : now.getFullYear()
    if (isInputDate(stored.customFromDate)) customFromDate.value = stored.customFromDate
    if (isInputDate(stored.customToDate)) customToDate.value = stored.customToDate
    invoicePage.value = safePositiveInt(stored.invoicePage, 1)
    invoicePageSize.value = isValidOptionValue(stored.invoicePageSize, pageSizeOptions) ? Number(stored.invoicePageSize) : 50
  } catch {
    sessionStorage.removeItem(BILLING_STATE_KEY)
  } finally {
    nextTick(() => { restoringBillingState.value = false })
  }
}

function sourceWithDayBookReturnHint(source: string) {
  if (!fromDayBook.value || !source) return source
  const [pathAndQuery, hash = ''] = source.split('#')
  const [path, rawQuery = ''] = pathAndQuery.split('?')
  const params = new URLSearchParams(rawQuery)
  params.set('fromDayBook', '1')
  const query = params.toString()
  return `${path}${query ? `?${query}` : ''}${hash ? `#${hash}` : ''}`
}

const columns: TableColumn<any>[] = [
  { accessorKey: 'invoiceNumber', header: 'Invoice' },
  { accessorKey: 'onDate', header: 'Date' },
  { accessorKey: 'customerName', header: 'Customer' },
  {
    accessorKey: 'remarks',
    header: 'Remarks',
    cell: ({ row }) => {
      const invoice = row.original.raw
      const text = String(row.original.remarks || '').trim()
      if (!text) {
        return h('span', { class: 'text-muted' }, '-')
      }
      const expanded = isRemarkExpanded(invoice.id)
      const displayText = expanded ? text : shortRemark(text, 10)
      const children = [
        h('span', { class: 'invoice-remark-text' }, displayText)
      ]
      if (text.length > 10) {
        children.push(h(UButton, {
          color: 'neutral',
          variant: 'link',
          size: 'xs',
          class: 'invoice-remark-toggle',
          label: expanded ? 'Hide' : 'Full',
          onClick: (event: Event) => {
            event.stopPropagation()
            toggleRemark(invoice.id)
          }
        }))
      }
      return h('div', { class: ['invoice-remark-cell', expanded ? 'is-expanded' : ''] }, children)
    }
  },
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
    accessorKey: 'digitalBillStatus',
    header: 'Digital Bill',
    cell: ({ row }) => {
      const invoice = row.original.raw
      const color = invoice.digitalBillPublicPath
        ? (invoice.digitalBillIsActive === false ? 'warning' : 'success')
        : 'neutral'
      return h(UBadge, { color, variant: 'subtle' }, () => row.original.digitalBillStatus)
    }
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

      if (invoice.digitalBillPublicPath) {
        actions.push(h(UButton, {
          color: 'success',
          variant: 'ghost',
          icon: 'i-lucide-copy',
          label: 'Copy Bill',
          onClick: () => copyDigitalBillLink(invoice)
        }))
        actions.push(h(UButton, {
          color: 'primary',
          variant: 'ghost',
          icon: 'i-lucide-external-link',
          label: 'Open Bill',
          to: invoice.digitalBillPublicPath,
          target: '_blank'
        }))
        actions.push(h(UButton, {
          color: 'info',
          variant: 'ghost',
          icon: 'i-lucide-activity',
          label: 'Activity',
          onClick: () => openDigitalBillActivity(invoice)
        }))
        actions.push(h(UButton, {
          color: 'primary',
          variant: 'ghost',
          icon: 'i-lucide-send',
          label: 'WhatsApp',
          onClick: () => sendInvoiceDigitalBillWhatsApp(invoice)
        }))
      } else if (invoice.invoiceStatus !== 'Cancelled') {
        actions.push(h(UButton, {
          color: 'success',
          variant: 'ghost',
          icon: 'i-lucide-link',
          label: 'Digital Bill',
          onClick: () => generateInvoiceDigitalBill(invoice)
        }))
      }

      if (canEditInvoice.value && invoice.invoiceStatus !== 'Cancelled') {
        actions.push(h(UButton, {
          color: 'warning',
          variant: 'ghost',
          icon: 'i-lucide-pencil',
          label: 'Edit',
          onClick: () => startEditInvoice(invoice)
        }))
        actions.push(h(UButton, {
          color: 'primary',
          variant: 'ghost',
          icon: 'i-lucide-copy-plus',
          label: 'Revise',
          onClick: () => startRevisedSale(invoice)
        }))
      }

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
          icon: 'i-lucide-trash-2',
          label: 'Delete',
          onClick: () => askCancel(invoice)
        }))
      }

      if (canAdminHardDelete.value && invoice.invoiceStatus === 'Cancelled') {
        actions.push(h(UButton, {
          color: 'error',
          variant: 'soft',
          icon: 'i-lucide-shredder',
          label: 'Hard Delete',
          disabled: hardDeleting.value,
          onClick: () => hardDeleteInvoice(invoice)
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

  persistBillingState()
  loading.value = true
  loadError.value = ''
  try {
    setupStatus.value = await api.get<any>('setup/status')
    const selectedCompanyId = workspace.companyId.value || setupStatus.value?.companyId
    const selectedStoreId = workspace.storeId.value || setupStatus.value?.storeId
    const billingOptionQuery = new URLSearchParams()
    if (selectedCompanyId) billingOptionQuery.set('companyId', selectedCompanyId)
    if (selectedStoreId) billingOptionQuery.set('storeId', selectedStoreId)

    const invoiceQuery = new URLSearchParams({
      datePreset: saleDatePreset.value,
      page: String(invoicePage.value),
      pageSize: String(invoicePageSize.value)
    })
    if (saleDatePreset.value === 'month-year') {
      invoiceQuery.set('month', String(invoiceMonth.value))
      invoiceQuery.set('year', String(invoiceYear.value))
    }
    if (saleDatePreset.value === 'year') {
      invoiceQuery.set('year', String(invoiceYear.value))
    }
    if (saleDatePreset.value === 'custom') {
      if (customFromDate.value) invoiceQuery.set('from', customFromDate.value)
      if (customToDate.value) invoiceQuery.set('to', customToDate.value)
    }
    if (search.value.trim()) invoiceQuery.set('q', search.value.trim())
    if (invoiceStatusFilter.value !== 'all') invoiceQuery.set('status', invoiceStatusFilter.value)

    const [companyRows, storeRows, productRows, invoicePageRows, bankAccountRows, billingOptions] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores'),
      api.list<any>('products'),
      api.get<any>(`billing/sales?${invoiceQuery.toString()}`),
      api.list<any>('bank-accounts'),
      api.get<any>(`billing/options?${billingOptionQuery.toString()}`)
    ])

    companies.value = companyRows
    stores.value = storeRows
    products.value = productRows
    productLookup.saveCache(productRows.map((product: any) => ({ productId: product.id, name: product.name, barcode: product.barcode, availableQty: product.currentStock || 0, mrp: product.mrp || 0, taxRate: product.taxRate || 0, taxType: String(product.taxType || 'GST'), unit: String(product.unit || 'Pcs'), category: product.productCategoryName || '', subCategory: product.productSubCategoryName || '' })))
    invoices.value = invoicePageRows?.items || []
    invoiceTotal.value = Number(invoicePageRows?.total || 0)
    invoiceServerSummary.billAmount = Number(invoicePageRows?.billAmount || 0)
    invoiceServerSummary.paidAmount = Number(invoicePageRows?.paidAmount || 0)
    invoiceServerSummary.balanceAmount = Number(invoicePageRows?.balanceAmount || 0)
    invoiceServerSummary.cancelled = Number(invoicePageRows?.cancelledCount || 0)
    bankAccounts.value = bankAccountRows
    customers.value = billingOptions?.customers || []
    salesmen.value = billingOptions?.salesmen || []
  } catch (error) {
    loadError.value = feedback.cleanMessage(error instanceof Error ? error.message : 'Please check the service and try again.')
    feedback.failed('Billing refresh failed', error)
  } finally {
    loading.value = false
  }
}

function resetInvoicePageAndRefresh() {
  if (restoringBillingState.value) return
  invoicePage.value = 1
  persistBillingState()
  refresh()
}

function startCreate() {
  void router.push(sourceWithDayBookReturnHint('/billing/new'))
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
    await printReceipt()
    await refresh()
  } catch (error) {
    feedback.failed('Could not save invoice', error)
  } finally {
    saving.value = false
  }
}

function digitalBillFullUrl(row: any) {
  const path = row?.digitalBillPublicPath
  if (!path) return ''
  if (/^https?:\/\//i.test(path)) return path
  if (import.meta.client) return `${window.location.origin}${path}`
  return path
}

function applyDigitalBillToInvoice(invoiceId: string, digitalBill: any) {
  const index = invoices.value.findIndex((item) => item.id === invoiceId)
  if (index >= 0) {
    invoices.value[index] = {
      ...invoices.value[index],
      digitalBillId: digitalBill.id || invoices.value[index].digitalBillId,
      digitalBillPublicPath: digitalBill.publicPath || invoices.value[index].digitalBillPublicPath,
      digitalBillPublicToken: digitalBill.publicToken || invoices.value[index].digitalBillPublicToken,
      digitalBillIsActive: digitalBill.isActive ?? invoices.value[index].digitalBillIsActive,
      digitalBillWhatsAppStatus: digitalBill.whatsAppStatus || invoices.value[index].digitalBillWhatsAppStatus || 'Link Ready'
    }
  }

  if (selectedReceipt.value?.id === invoiceId) {
    selectedReceipt.value = {
      ...selectedReceipt.value,
      digitalBillId: digitalBill.id || selectedReceipt.value.digitalBillId,
      digitalBillPublicPath: digitalBill.publicPath || selectedReceipt.value.digitalBillPublicPath,
      digitalBillPublicToken: digitalBill.publicToken || selectedReceipt.value.digitalBillPublicToken,
      digitalBillIsActive: digitalBill.isActive ?? selectedReceipt.value.digitalBillIsActive,
      digitalBillWhatsAppStatus: digitalBill.whatsAppStatus || selectedReceipt.value.digitalBillWhatsAppStatus || 'Link Ready'
    }
  }
}

async function generateInvoiceDigitalBill(invoice: any) {
  if (!invoice?.id) return
  try {
    const response = await api.create<any>(`billing/sales/${invoice.id}/digital-bill`, {})
    applyDigitalBillToInvoice(invoice.id, response)
    feedback.notify('Digital bill ready', response.publicPath || 'Link generated.', 'success')
  } catch (error) {
    feedback.failed('Could not generate digital bill', error)
  }
}

async function copyDigitalBillLink(row: any) {
  const link = digitalBillFullUrl(row)
  if (!link) {
    feedback.notify('Digital bill missing', 'Generate the digital bill link first.', 'warning')
    return
  }

  try {
    await navigator.clipboard.writeText(link)
    feedback.notify('Digital bill link copied', link, 'success')
  } catch {
    window.prompt('Copy Digital Bill link', link)
  }
}

async function sendInvoiceDigitalBillWhatsApp(invoice: any) {
  if (!invoice?.id) return
  try {
    const response = await api.create<any>(`billing/sales/${invoice.id}/digital-bill/send-whatsapp`, { force: true })
    const index = invoices.value.findIndex((item) => item.id === invoice.id)
    if (index >= 0) {
      invoices.value[index] = {
        ...invoices.value[index],
        digitalBillId: response.digitalInvoiceId || invoices.value[index].digitalBillId,
        digitalBillWhatsAppStatus: response.status || invoices.value[index].digitalBillWhatsAppStatus
      }
    }
    if (selectedReceipt.value?.id === invoice.id) {
      selectedReceipt.value = { ...selectedReceipt.value, digitalBillWhatsAppStatus: response.status || selectedReceipt.value.digitalBillWhatsAppStatus }
    }
    feedback.notify(`WhatsApp status: ${response.status || 'Queued'}`, response.errorMessage || response.messageBody || 'Digital bill WhatsApp action completed.', response.status === 'Sent' ? 'success' : 'warning')
  } catch (error) {
    feedback.failed('Could not send digital bill WhatsApp', error)
  }
}

async function openDigitalBillActivity(invoice: any) {
  if (!invoice?.digitalBillId) {
    feedback.notify('Digital bill missing', 'Generate the digital bill link first, then open activity.', 'warning')
    return
  }
  await navigateTo({ path: '/marketing/digital-bills', query: { activityId: invoice.digitalBillId } })
}


async function openBillingDeepLinkFromRoute() {
  if (!auth.isAuthenticated.value) return
  const invoiceId = String(route.query.invoiceId || '')
  const paymentId = String(route.query.paymentId || '')
  if (!invoiceId && !paymentId) return
  const deepLinkKey = `${invoiceId}:${paymentId}`
  if (lastOpenedBillingDeepLink.value === deepLinkKey) return
  lastOpenedBillingDeepLink.value = deepLinkKey

  try {
    if (invoiceId) {
      const receipt = await viewReceipt(invoiceId)
      await focusInvoiceInRegister(receipt)
      return
    }

    // Customer receipt deep links generated from Day Book include invoiceId. If a legacy link
    // has only paymentId, keep the page stable and show a clear message instead of failing.
    feedback.notify('Open source needs invoice', 'This payment link does not include its sale invoice id. Open it from Day Book detail or use the invoice link.', 'warning')
  } catch (error) {
    feedback.failed('Could not open linked sale invoice', error)
  }
}

async function viewReceipt(invoiceId: string) {
  try {
    selectedReceipt.value = await api.get<any>(`billing/sales/${invoiceId}/receipt`)
    invoicePrintFormat.value = 'a4'
    invoiceCopyType.value = 'customer'
    invoiceReprint.value = false
    invoiceSignatures.value = true
    return selectedReceipt.value
  } catch (error) {
    feedback.failed('Could not open receipt', error)
    return null
  }
}

async function focusInvoiceInRegister(receipt: any) {
  if (!receipt?.onDate) return
  const invoiceDate = dateInput(receipt.onDate)
  if (!invoiceDate) return
  restoringBillingState.value = true
  saleDatePreset.value = 'custom'
  customFromDate.value = invoiceDate
  customToDate.value = invoiceDate
  search.value = receipt.invoiceNumber || ''
  invoiceStatusFilter.value = 'all'
  invoicePage.value = 1
  await nextTick()
  restoringBillingState.value = false
  persistBillingState()
  await refresh()
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

function startRevisedSale(invoice: any) {
  if (!invoice?.id) return
  router.push(sourceWithDayBookReturnHint(`/billing/new?copyFrom=${encodeURIComponent(invoice.id)}`))
}


function dateInput(value: any) {
  if (!value) return ''
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return ''
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

function startEditInvoice(invoice: any) {
  pendingEditInvoice.value = invoice
  Object.assign(editInvoiceForm, {
    invoiceNumber: invoice.invoiceNumber || '',
    onDate: dateInput(invoice.onDate),
    customerName: invoice.customerName || 'Walk-in Customer',
    customerMobileNumber: invoice.customerMobileNumber || '',
    customerGstin: invoice.customerGstin || invoice.customerGSTIN || '',
    salesmanId: invoice.salesmanId || invoice.salemanId || null,
    remarks: invoice.remarks || ''
  })
  editInvoiceOpen.value = true
}

async function saveEditInvoice() {
  if (!pendingEditInvoice.value) return
  editingInvoice.value = true
  try {
    await api.update<any>('billing/sales', pendingEditInvoice.value.id, {
      invoiceNumber: editInvoiceForm.invoiceNumber,
      onDate: editInvoiceForm.onDate || null,
      customerName: editInvoiceForm.customerName,
      customerMobileNumber: editInvoiceForm.customerMobileNumber,
      customerGstin: editInvoiceForm.customerGstin,
      salesmanId: editInvoiceForm.salesmanId || null,
      remarks: editInvoiceForm.remarks || null
    })
    feedback.saved('Sales invoice updated')
    editInvoiceOpen.value = false
    pendingEditInvoice.value = null
    if (selectedReceipt.value?.id) selectedReceipt.value = null
    await refresh()
  } catch (error) {
    feedback.failed('Could not update sales invoice', error)
  } finally {
    editingInvoice.value = false
  }
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

    feedback.notify('Invoice deleted/cancelled', 'Stock quantities were reversed.', 'warning')
    cancelOpen.value = false
    pendingCancel.value = null
    await refresh()
  } catch (error) {
    feedback.failed('Could not cancel invoice', error)
  } finally {
    cancelling.value = false
  }
}

async function hardDeleteInvoice(invoice: any) {
  if (!canAdminHardDelete.value || !invoice?.id) return
  if (invoice.invoiceStatus !== 'Cancelled') {
    feedback.notify('Cancel first', 'Hard delete is allowed only after stock, payment and accounting are reversed by cancellation.', 'warning')
    return
  }

  const typed = window.prompt(`Type invoice number ${invoice.invoiceNumber} to permanently hard delete this cancelled invoice and its linked rows.`)
  if (typed !== invoice.invoiceNumber) {
    feedback.notify('Hard delete cancelled', 'Invoice number confirmation did not match.', 'warning')
    return
  }

  const reason = window.prompt('Reason for admin hard delete?', 'Wrong/revised invoice cleanup after cancellation') || 'Admin hard delete after cancellation'
  hardDeleting.value = true
  try {
    const query = new URLSearchParams({
      confirmInvoiceNumber: invoice.invoiceNumber,
      reason
    })
    const result = await api.remove(`billing/sales/${invoice.id}`, `hard-delete?${query.toString()}`) as any
    if (selectedReceipt.value?.id === invoice.id) selectedReceipt.value = null
    feedback.notify('Invoice hard deleted', `Removed rows: items ${result.removedInvoiceItems || 0}, payments ${result.removedInvoicePayments || 0}, journals ${result.removedJournalEntries || 0}.`, 'warning')
    await refresh()
  } catch (error) {
    feedback.failed('Could not hard delete invoice', error)
  } finally {
    hardDeleting.value = false
  }
}

async function printReceipt() {
  if (!selectedReceipt.value?.id) return
  const query = new URLSearchParams({
    format: invoicePrintFormat.value,
    copy: invoiceCopyType.value,
    reprint: String(invoiceReprint.value),
    signatures: String(invoiceSignatures.value)
  })
  try {
    await documentPrint.printPdf(`billing/sales/${selectedReceipt.value.id}/pdf?${query.toString()}`)
  } catch (error) {
    feedback.failed('Could not print invoice PDF', error)
  }
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

function todayInputDate(offsetDays = 0) {
  const date = new Date()
  date.setDate(date.getDate() + offsetDays)
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

function lineTotal(item: any) {
  return Math.max((Number(item.mrp || 0) - Number(item.discountAmount || 0)) * Number(item.quantity || 0), 0)
}

function formatDate(value: string) {
  return value ? new Date(value).toLocaleDateString() : '-'
}

function formatDateTime(value: string) {
  return value ? new Date(value).toLocaleString('en-IN') : '-'
}

function paymentModeLabel(value: any) {
  const text = String(value || '').trim()
  if (!text) return '-'
  return text
    .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
    .replace(/_/g, ' ')
    .replace(/^./, (match) => match.toUpperCase())
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
  restoreBillingState()
  await refresh()
  await openBillingDeepLinkFromRoute()
})

watch(() => route.fullPath, async () => {
  await openBillingDeepLinkFromRoute()
})

watch([saleDatePreset, invoiceMonth, invoiceYear, customFromDate, customToDate, invoiceStatusFilter, invoicePageSize], () => {
  resetInvoicePageAndRefresh()
})

watch(invoicePage, () => {
  if (restoringBillingState.value) return
  persistBillingState()
  refresh()
})

let saleSearchTimer: ReturnType<typeof setTimeout> | null = null
watch(search, () => {
  if (restoringBillingState.value) return
  if (saleSearchTimer) clearTimeout(saleSearchTimer)
  saleSearchTimer = setTimeout(() => resetInvoicePageAndRefresh(), 350)
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
    @workspace-change="refresh"
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
            {{ loading ? 'Loading' : `${invoiceTotal} invoices` }}
          </UBadge>
          <UButton to="/billing/final-qa" icon="i-lucide-badge-check" label="Final QA" variant="subtle" />
          <UButton icon="i-lucide-plus" label="New Invoice" @click="startCreate" />
        </template>
      </UiModulePageHeader>

      <UiDayBookReturnButton />

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

      <UiRegisterPanel
        title="Invoice Register"
        :description="`Showing ${invoicePageFrom}-${invoicePageTo} of ${invoiceTotal} invoices • ${saleDateRangeLabel}`"
        :loading="loading"
        :error="loadError"
        :empty="filteredRows.length === 0"
        :empty-title="search || invoiceStatusFilter !== 'all' ? 'No matching invoices' : 'No invoices for this period'"
        :empty-description="search || invoiceStatusFilter !== 'all' ? 'Change the search, status, or date filter.' : 'Create the first sales invoice or choose a wider date filter.'"
        empty-icon="i-lucide-receipt-indian-rupee"
        @retry="refresh"
      >
        <template #actions>
          <UiCrudToolbar
            v-model:search="search"
            search-placeholder="Search invoice or customer"
            :loading="loading"
            refresh-label="Sync"
            create-label="New Invoice"
            @refresh="refresh"
            @create="startCreate"
          >
            <template #filters>
              <USelect
                v-model="saleDatePreset"
                :items="saleDatePresetOptions"
                aria-label="Filter sale date range"
                class="min-w-36"
              />
              <USelect
                v-if="saleDatePreset === 'month-year'"
                v-model="invoiceMonth"
                :items="monthOptions"
                aria-label="Filter sale month"
                class="min-w-36"
              />
              <USelect
                v-if="saleDatePreset === 'month-year' || saleDatePreset === 'year'"
                v-model="invoiceYear"
                :items="yearOptions"
                aria-label="Filter sale year"
                class="min-w-28"
              />
              <UInput
                v-if="saleDatePreset === 'custom'"
                v-model="customFromDate"
                type="date"
                aria-label="Custom sale from date"
                class="min-w-36"
              />
              <UInput
                v-if="saleDatePreset === 'custom'"
                v-model="customToDate"
                type="date"
                aria-label="Custom sale to date"
                class="min-w-36"
              />
              <USelect
                v-model="invoiceStatusFilter"
                :items="[
                  { label: 'All statuses', value: 'all' },
                  { label: 'Pending', value: 'pending' },
                  { label: 'Paid', value: 'paid' },
                  { label: 'Partially paid', value: 'partiallyPaid' },
                  { label: 'Cancelled', value: 'cancelled' },
                  { label: 'Refunded', value: 'refunded' },
                  { label: 'Partially refunded', value: 'partiallyRefunded' },
                  { label: 'Overdue', value: 'overdue' },
                  { label: 'Draft', value: 'draft' }
                ]"
                aria-label="Filter invoice status"
                class="min-w-36"
              />
              <USelect
                v-model="invoicePageSize"
                :items="pageSizeOptions"
                aria-label="Sale invoice page size"
                class="min-w-32"
              />
            </template>
          </UiCrudToolbar>
        </template>

        <div class="planner-table-wrap">
          <UTable :data="filteredRows" :columns="columns" />
        </div>
        <div class="flex flex-wrap items-center justify-between gap-3 border-t border-slate-200 px-4 py-3 text-sm text-slate-600 dark:border-slate-800 dark:text-slate-300">
          <span>Showing {{ invoicePageFrom }}-{{ invoicePageTo }} of {{ invoiceTotal }}</span>
          <div class="flex items-center gap-2">
            <UButton size="sm" variant="outline" color="neutral" icon="i-lucide-chevron-left" label="Previous" :disabled="invoicePage <= 1 || loading" @click="invoicePage--" />
            <span>Page {{ invoicePage }} / {{ invoiceTotalPages }}</span>
            <UButton size="sm" variant="outline" color="neutral" icon="i-lucide-chevron-right" trailing label="Next" :disabled="invoicePage >= invoiceTotalPages || loading" @click="invoicePage++" />
          </div>
        </div>
      </UiRegisterPanel>

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

      <UModal v-model:open="receiptOpen" title="Invoice Receipt" :ui="{ content: 'max-w-5xl' }">
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

          <div v-if="selectedReceipt" class="planner-table-wrap no-print">
            <table class="planner-table">
              <tbody>
                <tr v-for="row in receiptSummaryRows" :key="row.label">
                  <th class="w-40 text-left text-muted">{{ row.label }}</th>
                  <td :class="row.mono ? 'break-all font-mono text-xs' : ''">{{ row.value }}</td>
                </tr>
              </tbody>
            </table>
          </div>

          <div v-if="selectedReceipt" class="digital-bill-receipt-actions no-print">
            <div>
              <strong>Digital Bill CRM</strong>
              <p>{{ selectedReceipt.digitalBillPublicPath ? `Link ready • WhatsApp: ${selectedReceipt.digitalBillWhatsAppStatus || 'Not sent'} • Opened ${selectedReceipt.digitalBillOpenCount || 0}x` : 'No digital bill link generated yet for this invoice.' }}</p>
            </div>
            <div class="table-action-buttons">
              <UButton
                v-if="!selectedReceipt.digitalBillPublicPath"
                size="sm"
                color="success"
                variant="subtle"
                icon="i-lucide-link"
                label="Generate Digital Bill"
                @click="generateInvoiceDigitalBill(selectedReceipt)"
              />
              <UButton
                v-if="selectedReceipt.digitalBillPublicPath"
                size="sm"
                color="success"
                variant="subtle"
                icon="i-lucide-copy"
                label="Copy Link"
                @click="copyDigitalBillLink(selectedReceipt)"
              />
              <UButton
                v-if="selectedReceipt.digitalBillPublicPath"
                size="sm"
                color="primary"
                variant="subtle"
                icon="i-lucide-external-link"
                label="Open Web Bill"
                :to="selectedReceipt.digitalBillPublicPath"
                target="_blank"
              />
              <UButton
                v-if="selectedReceipt.digitalBillPublicPath"
                size="sm"
                color="primary"
                variant="subtle"
                icon="i-lucide-send"
                label="Send WhatsApp"
                @click="sendInvoiceDigitalBillWhatsApp(selectedReceipt)"
              />
              <UButton
                v-if="selectedReceipt.digitalBillPublicPath"
                size="sm"
                color="info"
                variant="subtle"
                icon="i-lucide-activity"
                label="Activity"
                @click="openDigitalBillActivity(selectedReceipt)"
              />
            </div>
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
                  <th>Item / Barcode</th>
                  <th>Qty</th>
                  <th>MRP</th>
                  <th>Tax</th>
                  <th>Amount</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="item in selectedReceipt.items" :key="`${item.barcode}-${item.productName}`">
                  <td class="receipt-item-cell">
                    <strong>{{ saleItemDisplayName(item) }}</strong>
                    <small>Barcode: {{ saleItemBarcode(item) }}</small>
                  </td>
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

            <div v-if="selectedReceiptPaymentRows.length" class="planner-table-wrap no-print">
              <table class="planner-table">
                <thead><tr><th>Payment ID</th><th>Date</th><th>Mode</th><th>Amount</th><th>Reference</th><th>Status / source</th></tr></thead>
                <tbody>
                  <tr v-for="payment in selectedReceiptPaymentRows" :key="payment.id || `${payment.paymentMode}-${payment.amount}-${payment.reference}`">
                    <td class="break-all font-mono text-xs">{{ payment.id || '-' }}</td>
                    <td>{{ payment.onDate }}</td>
                    <td>{{ payment.paymentMode }}</td>
                    <td>{{ payment.amount }}</td>
                    <td>{{ payment.reference }}</td>
                    <td>{{ payment.status }}</td>
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


      <UiFormSlideover
        v-model:open="editInvoiceOpen"
        title="Edit Sales Invoice"
        description="Edit header details only. Item lines, stock and accounting values are protected; cancel and recreate if amounts/items are wrong."
        submit-label="Save Invoice"
        layout="modal"
        content-class="sm:max-w-2xl"
        :loading="editingInvoice"
        @submit="saveEditInvoice"
      >
        <div class="form-two-column">
          <UFormField label="Invoice number" required>
            <UInput v-model="editInvoiceForm.invoiceNumber" required />
          </UFormField>
          <UFormField label="Invoice date">
            <UInput v-model="editInvoiceForm.onDate" type="date" />
          </UFormField>
          <UFormField label="Customer name">
            <UInput v-model="editInvoiceForm.customerName" />
          </UFormField>
          <UFormField label="Customer mobile">
            <UInput v-model="editInvoiceForm.customerMobileNumber" />
          </UFormField>
          <UFormField label="Customer GSTIN">
            <UInput v-model="editInvoiceForm.customerGstin" />
          </UFormField>
          <UFormField label="Salesman">
            <USelect v-model="editInvoiceForm.salesmanId" :items="salesmen.map((item) => ({ value: item.id, label: item.name }))" placeholder="Select salesman" />
          </UFormField>
          <UFormField label="Remarks / note" class="md:col-span-2">
            <UTextarea v-model="editInvoiceForm.remarks" :rows="3" placeholder="Optional invoice note" />
          </UFormField>
        </div>
      </UiFormSlideover>

      <UiConfirmDeleteModal
        v-model:open="cancelOpen"
        title="Delete / Cancel Invoice"
        :description="`Cancel invoice ${pendingCancel?.invoiceNumber || ''}? Stock will be reversed.`"
        confirm-label="Delete / Cancel Invoice"
        :loading="cancelling"
        @confirm="confirmCancel"
      />
    </section>
  </AppShell>
</template>
