<script setup lang="ts">
import { h, resolveComponent } from 'vue'
import type { TableColumn } from '@nuxt/ui'

const api = useGarmetixApi()
const auth = useAuth()
const workspace = useWorkspace()
const feedback = useUiFeedback()
const productLookup = useProductLookup()
const documentPrint = useServerDocumentPrint()
const config = useRuntimeConfig()
const isAuthenticated = auth.isAuthenticated
const canDelete = auth.canDelete

const UBadge = resolveComponent('UBadge')
const UButton = resolveComponent('UButton')

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const products = ref<any[]>([])
const purchaseProductSearchOptions = ref<any[]>([])
const purchaseInvoices = ref<any[]>([])
const purchaseLookup = ref<any>({ categories: [], subCategories: [], taxes: [] })
const bankAccounts = ref<any[]>([])
const loading = ref(false)
const saving = ref(false)
const setupStatus = ref<any | null>(null)
const vendorGstinValidation = ref<any | null>(null)
const vendorGstinChecking = ref(false)
const selectedReceipt = ref<any | null>(null)
const pendingCancel = ref<any | null>(null)
const search = ref('')
const invoiceStatusFilter = ref('all')
const loadError = ref('')
const formOpen = ref(false)
const cancelOpen = ref(false)
const cancelling = ref(false)
const paymentOpen = ref(false)
const payingVendor = ref(false)
const pendingPaymentInvoice = ref<any | null>(null)
const downloadingPurchasePdf = ref(false)
const purchasePrintFormat = ref<'a4' | 'a5' | 'thermal-2' | 'thermal-3'>('a4')
const purchaseCopyType = ref<'store' | 'supplier' | 'office' | 'duplicate'>('store')
const purchaseReprint = ref(false)
const purchaseSignatures = ref(true)

const paymentModeOptions = [
  { value: 0, label: 'Cash' },
  { value: 1, label: 'Card' },
  { value: 2, label: 'UPI' },
  { value: 4, label: 'IMPS' },
  { value: 5, label: 'RTGS' },
  { value: 6, label: 'NEFT' },
  { value: 7, label: 'Cheque' },
  { value: 8, label: 'Demand Draft' }
]

const purchasePrintFormatOptions = [
  { value: 'a4', label: 'A4 purchase invoice' },
  { value: 'a5', label: 'A5 compact invoice' },
  { value: 'thermal-2', label: 'Thermal 2-inch / 58mm' },
  { value: 'thermal-3', label: 'Thermal 3-inch / 80mm' }
]

const purchaseCopyOptions = [
  { value: 'store', label: 'Store copy' },
  { value: 'supplier', label: 'Supplier copy' },
  { value: 'office', label: 'Office copy' },
  { value: 'duplicate', label: 'Duplicate copy' }
]

const purchaseForm = reactive<any>(emptyPurchaseForm())
const paymentVoucherForm = reactive<any>(emptyPaymentVoucherForm())
const purchaseCart = ref<any[]>([])

const productOptions = computed(() => [
  { value: '', label: 'New product' },
  ...products.value.map((product) => ({
    value: product.id,
    label: `${product.name || 'Product'} - ${product.barcode || 'No barcode'}`
  }))
])

const purchaseProductSuggestions = computed(() => purchaseProductSearchOptions.value.map((item) => `${item.barcode} | ${item.name} | Qty ${Number(item.availableQty || 0)} | MRP ${Number(item.mrp || 0)}`))

const selectedPurchaseProduct = computed(() => products.value.find((item) => item.id === purchaseForm.selectedProductId))
const categoryOptions = computed(() => purchaseLookup.value.categories?.map((item: any) => ({ value: item.id, label: item.name })) || [])
const subCategoryOptions = computed(() => purchaseLookup.value.subCategories?.filter((item: any) => !purchaseForm.productCategoryId || item.categoryId === purchaseForm.productCategoryId)?.map((item: any) => ({ value: item.id, label: item.name })) || [])
const taxOptions = computed(() => purchaseLookup.value.taxes?.map((item: any) => ({ value: item.id, label: `${item.name || 'GST'} - ${Number(item.rate || 0).toFixed(2)}%` })) || [])
const vendorOptions = computed(() => [
  { value: '', label: 'New supplier / manual entry' },
  ...(purchaseLookup.value.vendors?.map((vendor: any) => ({ value: vendor.id, label: `${vendor.name || 'Supplier'}${vendor.gstin ? ` | ${vendor.gstin}` : ''}${Number(vendor.balanceAmount || 0) > 0 ? ` | Due ${money(Number(vendor.balanceAmount || 0))}` : ''}` })) || [])
])
const unitOptions = computed(() => purchaseLookup.value.units?.map((item: any) => ({ value: item.value, label: item.label })) || [])
const productTypeOptions = computed(() => purchaseLookup.value.productTypes?.map((item: any) => ({ value: item.value, label: item.label })) || [])
const productGroupOptions = computed(() => purchaseLookup.value.productGroups?.map((item: any) => ({ value: item.value, label: item.label })) || [])
const selectedVendor = computed(() => purchaseLookup.value.vendors?.find((vendor: any) => vendor.id === purchaseForm.vendorId) || null)
const requiresBankAccount = computed(() => Number(purchaseForm.paidAmount || 0) > 0 && Number(purchaseForm.paymentMode) !== 0)
const paymentVoucherRequiresBank = computed(() => Number(paymentVoucherForm.amount || 0) > 0 && Number(paymentVoucherForm.paymentMode) !== 0)

const bankAccountOptions = computed(() => bankAccounts.value.map((account) => ({
  value: account.id,
  label: `${account.accountHolderName || 'Bank'} - ${account.accountNumber || ''}`.trim()
})))

const purchaseCartTotal = computed(() => {
  return purchaseCart.value.reduce((sum, item) => sum + lineTotal(item), 0)
})

const payableTotal = computed(() => purchaseCartTotal.value + Number(purchaseForm.frightAmount || 0))

const receiptOpen = computed({
  get: () => Boolean(selectedReceipt.value),
  set: (value: boolean) => {
    if (!value) {
      selectedReceipt.value = null
    }
  }
})

const invoiceSummary = computed(() => {
  return purchaseInvoices.value.reduce((summary, invoice) => {
    summary.billAmount += Number(invoice.billAmount || invoice.totalAmount || invoice.netAmount || 0)
    summary.paidAmount += Number(invoice.paidAmount || 0)
    summary.freightAmount += Number(invoice.frightAmount || 0)
    if (invoice.invoiceStatus === 'Cancelled') {
      summary.cancelled += 1
    }
    return summary
  }, {
    billAmount: 0,
    paidAmount: 0,
    freightAmount: 0,
    cancelled: 0
  })
})

const metrics = computed(() => [
  {
    label: 'Purchase Invoices',
    value: purchaseInvoices.value.length,
    meta: `${invoiceSummary.value.cancelled} cancelled`,
    icon: 'i-lucide-file-text',
    color: 'primary'
  },
  {
    label: 'Purchase Value',
    value: money(invoiceSummary.value.billAmount),
    meta: 'Invoice total',
    icon: 'i-lucide-indian-rupee',
    color: 'success'
  },
  {
    label: 'Paid',
    value: money(invoiceSummary.value.paidAmount),
    meta: 'Supplier payments',
    icon: 'i-lucide-credit-card',
    color: 'neutral'
  },
  {
    label: 'Freight',
    value: money(invoiceSummary.value.freightAmount),
    meta: 'Purchase freight',
    icon: 'i-lucide-truck',
    color: 'warning'
  }
])

const tableRows = computed(() => purchaseInvoices.value.map((invoice) => ({
  id: invoice.id,
  invoiceNumber: invoice.invoiceNumber || '-',
  inwardNumber: invoice.inwardNumber || '-',
  onDate: formatDate(invoice.onDate || invoice.inwardDate),
  vendorName: invoice.vendorName || invoice.vendor?.name || '-',
  billAmount: money(Number(invoice.billAmount || invoice.totalAmount || invoice.netAmount || 0)),
  paidAmount: money(Number(invoice.paidAmount || 0)),
  status: invoice.invoiceStatus ?? 'Saved',
  balanceAmount: money(Number(invoice.balanceAmount || 0)),
  raw: invoice
})))

const filteredRows = computed(() => {
  const term = search.value.trim().toLowerCase()
  return tableRows.value.filter((row) => {
    const matchesStatus = invoiceStatusFilter.value === 'all'
      || String(row.status).toLowerCase() === invoiceStatusFilter.value
    const matchesSearch = !term || JSON.stringify(row).toLowerCase().includes(term)
    return matchesStatus && matchesSearch
  })
})

const columns: TableColumn<any>[] = [
  { accessorKey: 'invoiceNumber', header: 'Invoice' },
  { accessorKey: 'inwardNumber', header: 'Inward' },
  { accessorKey: 'onDate', header: 'Date' },
  { accessorKey: 'vendorName', header: 'Vendor' },
  { accessorKey: 'billAmount', header: 'Amount' },
  { accessorKey: 'paidAmount', header: 'Paid' },
  { accessorKey: 'balanceAmount', header: 'Balance' },
  {
    accessorKey: 'status',
    header: 'Status',
    cell: ({ row }) => h(UBadge, {
      color: row.original.status === 'Cancelled' ? 'error' : 'success',
      variant: 'subtle'
    }, () => String(row.original.status))
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
          label: 'View',
          onClick: () => viewReceipt(invoice.id)
        })
      ]

      if (invoice.invoiceStatus !== 'Cancelled' && Number(invoice.balanceAmount || 0) > 0) {
        actions.push(h(UButton, {
          color: 'success',
          variant: 'ghost',
          icon: 'i-lucide-wallet-cards',
          label: 'Pay',
          onClick: () => askVendorPayment(invoice)
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

function emptyPaymentVoucherForm() {
  return {
    amount: 0,
    paymentMode: 0,
    bankAccountId: null,
    paymentDetails: '',
    slipNumber: '',
    remarks: ''
  }
}

function emptyPurchaseForm() {
  return {
    vendorId: '',
    vendorName: 'Default Supplier',
    vendorMobileNumber: '',
    vendorGstin: '',
    invoiceNumber: '',
    inwardNumber: '',
    supplierInvoiceDate: todayInputDate(),
    dueDate: todayInputDate(45),
    paymentMode: 0,
    paidAmount: 0,
    frightAmount: 0,
    selectedProductId: '',
    productSearch: '',
    productName: '',
    barcode: '',
    hsnCode: '',
    productUnit: 2,
    productType: 0,
    productGroup: 0,
    quantity: 1,
    costPrice: 0,
    mrp: 0,
    discountAmount: 0,
    taxId: '',
    productCategoryId: '',
    productSubCategoryId: '',
    bankAccountId: null
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

async function refresh() {
  if (!auth.isAuthenticated.value) {
    return
  }

  loading.value = true
  loadError.value = ''
  try {
    setupStatus.value = await api.get<any>('setup/status')
    const [companyRows, storeRows, productRows, purchaseRows, bankAccountRows, lookupRows] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores'),
      api.list<any>('products'),
      api.get<any[]>('purchase/invoices/recent'),
      api.list<any>('bank-accounts'),
      api.get<any>('purchase/lookup-options')
    ])

    companies.value = companyRows
    stores.value = storeRows
    products.value = productRows
    productLookup.saveCache(productRows.map((product: any) => ({ productId: product.id, name: product.name, barcode: product.barcode, hsnCode: product.hsnCode || product.HSNCode || '', availableQty: product.currentStock || 0, mrp: product.mrp || 0, taxRate: product.taxRate || 0, taxType: String(product.taxType || 'GST'), unit: String(product.unit || 'Pcs'), category: product.productCategoryName || product.categoryName || '', subCategory: product.productSubCategoryName || product.subCategoryName || '', taxId: product.taxId, productCategoryId: product.productCategoryId, productSubCategoryId: product.productSubCategoryId })))
    purchaseInvoices.value = purchaseRows
    bankAccounts.value = bankAccountRows
    purchaseLookup.value = lookupRows
  } catch (error) {
    loadError.value = feedback.cleanMessage(error instanceof Error ? error.message : 'Please check the service and try again.')
    feedback.failed('Purchase refresh failed', error)
  } finally {
    loading.value = false
  }
}

function startCreate() {
  Object.assign(purchaseForm, emptyPurchaseForm())
  purchaseCart.value = []
  vendorGstinValidation.value = null
  formOpen.value = true
}

function applySelectedVendor() {
  const vendor = selectedVendor.value
  vendorGstinValidation.value = null
  if (!vendor) {
    return
  }

  purchaseForm.vendorName = vendor.name || purchaseForm.vendorName
  purchaseForm.vendorMobileNumber = vendor.mobileNumber || ''
  purchaseForm.vendorGstin = vendor.gstin || ''
}

function unitValue(unit: any, fallback = 2) {
  if (typeof unit === 'number') return unit
  const numeric = Number(unit)
  if (!Number.isNaN(numeric) && String(unit).trim() !== '') return numeric
  const match = unitOptions.value.find((option: any) => String(option.label).toLowerCase() === String(unit || '').toLowerCase())
  return match?.value ?? fallback
}

function unitLabel(value: any) {
  return unitOptions.value.find((option: any) => option.value === value)?.label || value || '-'
}


async function lookupPurchaseProduct() {
  const query = String(purchaseForm.barcode || purchaseForm.productSearch || '').trim()
  if (!query) {
    feedback.notify('Barcode required', 'Scan barcode or enter product name/barcode first.', 'warning')
    return
  }

  const item = query.includes('|')
    ? purchaseProductSearchOptions.value.find((row) => `${row.barcode} | ${row.name} | Qty ${Number(row.availableQty || 0)} | MRP ${Number(row.mrp || 0)}` === query)
    : await productLookup.byBarcode(query, workspace.storeId.value || undefined) || (await productLookup.searchProducts(query, workspace.storeId.value || undefined))[0]

  if (!item) {
    feedback.notify('Product not found', 'No cached or server product matched this barcode/search.', 'warning')
    return
  }

  applyLookupProductToPurchase(item)
}

async function refreshPurchaseSuggestions(value?: string) {
  const query = String(value || purchaseForm.productSearch || purchaseForm.barcode || '').trim()
  purchaseProductSearchOptions.value = await productLookup.searchProducts(query, workspace.storeId.value || undefined)
}

function applyLookupProductToPurchase(item: any) {
  if (!products.value.some((product) => product.id === item.productId)) {
    products.value.push({
      id: item.productId,
      name: item.name,
      barcode: item.barcode,
      mrp: item.mrp,
      taxRate: item.taxRate,
      hsnCode: item.hsnCode,
      taxId: item.taxId,
      unit: item.unit,
      productCategoryId: item.productCategoryId,
      productSubCategoryId: item.productSubCategoryId,
      productCategoryName: item.category,
      productSubCategoryName: item.subCategory
    })
  }

  purchaseForm.selectedProductId = item.productId
  purchaseForm.productSearch = `${item.barcode} | ${item.name}`
  purchaseForm.productName = item.name
  purchaseForm.barcode = item.barcode
  purchaseForm.mrp = Number(item.mrp || 0)
  purchaseForm.hsnCode = item.hsnCode || purchaseForm.hsnCode
  purchaseForm.productUnit = unitValue(item.unit, purchaseForm.productUnit)
  purchaseForm.taxId = item.taxId || purchaseForm.taxId
  purchaseForm.productCategoryId = item.productCategoryId || purchaseForm.productCategoryId
  purchaseForm.productSubCategoryId = item.productSubCategoryId || purchaseForm.productSubCategoryId
  feedback.notify('Product loaded', `${item.name} | Available ${Number(item.availableQty || 0)} | MRP ${money(Number(item.mrp || 0))} | GST ${Number(item.taxRate || 0)}%`, 'success')
}

function addPurchaseItem() {
  const selected = selectedPurchaseProduct.value
  const productName = selected?.name || purchaseForm.productName
  const barcode = selected?.barcode || purchaseForm.barcode

  if (!productName || !barcode) {
    feedback.notify('Item missing', 'Select an existing product or enter product name and barcode.', 'warning')
    return
  }

  purchaseCart.value.push({
    productId: selected?.id || null,
    productName,
    barcode,
    hsnCode: purchaseForm.hsnCode || '',
    productUnit: Number(purchaseForm.productUnit ?? 2),
    productType: Number(purchaseForm.productType ?? 0),
    productGroup: Number(purchaseForm.productGroup ?? 0),
    quantity: Number(purchaseForm.quantity || 0),
    costPrice: Number(purchaseForm.costPrice || 0),
    mrp: Number(purchaseForm.mrp || selected?.mrp || 0),
    discountAmount: Number(purchaseForm.discountAmount || 0),
    taxId: purchaseForm.taxId || null,
    productCategoryId: purchaseForm.productCategoryId || null,
    productSubCategoryId: purchaseForm.productSubCategoryId || null
  })

  purchaseForm.selectedProductId = ''
  purchaseForm.productSearch = ''
  purchaseForm.productName = ''
  purchaseForm.barcode = ''
  purchaseForm.hsnCode = ''
  purchaseForm.productUnit = 2
  purchaseForm.productType = 0
  purchaseForm.productGroup = 0
  purchaseForm.quantity = 1
  purchaseForm.costPrice = 0
  purchaseForm.mrp = 0
  purchaseForm.discountAmount = 0
  purchaseForm.taxId = ''
  purchaseForm.productCategoryId = ''
  purchaseForm.productSubCategoryId = ''
  purchaseForm.paidAmount = payableTotal.value
}

function removePurchaseItem(index: number) {
  purchaseCart.value.splice(index, 1)
  purchaseForm.paidAmount = payableTotal.value
}

async function validateVendorGstin() {
  vendorGstinValidation.value = null
  if (!purchaseForm.vendorGstin) {
    feedback.notify('Enter vendor GSTIN first', undefined, 'warning')
    return
  }

  vendorGstinChecking.value = true
  try {
    vendorGstinValidation.value = await api.create<any>('gstin/validate-party', {
      partyType: 'Vendor',
      gstin: purchaseForm.vendorGstin,
      name: purchaseForm.vendorName,
      address: ''
    })

    if (vendorGstinValidation.value.alerts?.length) {
      feedback.notify('Vendor GSTIN alert', vendorGstinValidation.value.alerts.join(' '), 'warning')
    } else {
      feedback.notify('Vendor GSTIN checked', vendorGstinValidation.value.lookup?.isVerified ? 'GSTIN verified.' : 'GSTIN format checked.', 'success')
    }
  } catch (error) {
    feedback.failed('Could not verify vendor GSTIN', error)
  } finally {
    vendorGstinChecking.value = false
  }
}

async function submitPurchase() {
  saving.value = true
  try {
    const selectedStore = stores.value.find((store) => store.id === workspace.storeId.value)
    const companyId = workspace.companyId.value || setupStatus.value?.companyId || selectedStore?.companyId || companies.value[0]?.id
    const storeGroupId = workspace.storeGroupId.value || selectedStore?.storeGroupId || setupStatus.value?.storeGroupId || stores.value[0]?.storeGroupId
    const storeId = workspace.storeId.value || setupStatus.value?.storeId || stores.value[0]?.id

    if (!companyId || !storeGroupId || !storeId) {
      throw new Error('Run quick setup before purchase inward.')
    }

    if (purchaseCart.value.length === 0) {
      throw new Error('Add at least one item to the inward cart.')
    }

    if (requiresBankAccount.value && !purchaseForm.bankAccountId) {
      throw new Error('Select bank account for non-cash payment.')
    }

    if (purchaseForm.vendorGstin && !vendorGstinValidation.value) {
      await validateVendorGstin()
    }

    const response = await api.create<any>('purchase/inward', {
      companyId,
      storeGroupId,
      storeId,
      vendorId: purchaseForm.vendorId || null,
      vendorName: purchaseForm.vendorName,
      vendorMobileNumber: purchaseForm.vendorMobileNumber,
      vendorGstin: purchaseForm.vendorGstin,
      invoiceNumber: purchaseForm.invoiceNumber,
      inwardNumber: purchaseForm.inwardNumber,
      supplierInvoiceDate: purchaseForm.supplierInvoiceDate || null,
      dueDate: purchaseForm.dueDate || null,
      paymentMode: Number(purchaseForm.paymentMode),
      bankAccountId: requiresBankAccount.value ? purchaseForm.bankAccountId : null,
      paidAmount: Number(purchaseForm.paidAmount || 0),
      frightAmount: Number(purchaseForm.frightAmount || 0),
      items: purchaseCart.value.map((item) => ({
        productId: item.productId,
        productName: item.productName,
        barcode: item.barcode,
        quantity: item.quantity,
        costPrice: item.costPrice,
        mrp: item.mrp,
        discountAmount: item.discountAmount,
        taxId: item.taxId,
        productCategoryId: item.productCategoryId,
        productSubCategoryId: item.productSubCategoryId,
        hsnCode: item.hsnCode,
        productUnit: item.productUnit,
        productType: item.productType,
        productGroup: item.productGroup
      }))
    })

    feedback.saved(`Inward ${response.inwardNumber || ''}`.trim())
    if (response.gstinAlerts?.length) {
      feedback.notify('Vendor GSTIN alert saved', response.gstinAlerts.join(' '), 'warning')
    }
    formOpen.value = false
    await viewReceipt(response.purchaseInvoiceId)
    await printReceipt()
    await refresh()
  } catch (error) {
    feedback.failed('Could not save purchase inward', error)
  } finally {
    saving.value = false
  }
}


async function viewReceipt(invoiceId: string) {
  try {
    selectedReceipt.value = await api.get<any>(`purchase/invoices/${invoiceId}/receipt`)
    purchasePrintFormat.value = 'a4'
    purchaseCopyType.value = 'store'
    purchaseReprint.value = false
    purchaseSignatures.value = true
  } catch (error) {
    feedback.failed('Could not open purchase invoice', error)
  }
}


function askVendorPayment(invoice: any) {
  pendingPaymentInvoice.value = invoice
  Object.assign(paymentVoucherForm, emptyPaymentVoucherForm())
  paymentVoucherForm.amount = Number(invoice.balanceAmount || 0)
  paymentVoucherForm.paymentMode = 0
  paymentVoucherForm.bankAccountId = null
  paymentOpen.value = true
}

async function confirmVendorPayment() {
  if (!pendingPaymentInvoice.value) {
    return
  }

  payingVendor.value = true
  try {
    if (paymentVoucherRequiresBank.value && !paymentVoucherForm.bankAccountId) {
      throw new Error('Select bank account for non-cash vendor payment.')
    }

    const response = await api.create<any>(`purchase/invoices/${pendingPaymentInvoice.value.id}/payment-voucher`, {
      amount: Number(paymentVoucherForm.amount || 0),
      paymentMode: Number(paymentVoucherForm.paymentMode),
      bankAccountId: paymentVoucherRequiresBank.value ? paymentVoucherForm.bankAccountId : null,
      paymentDetails: paymentVoucherForm.paymentDetails,
      slipNumber: paymentVoucherForm.slipNumber,
      remarks: paymentVoucherForm.remarks
    })

    feedback.saved(`Payment voucher ${response.voucherNumber || ''}`.trim())
    paymentOpen.value = false
    pendingPaymentInvoice.value = null
    await refresh()
  } catch (error) {
    feedback.failed('Could not create vendor payment voucher', error)
  } finally {
    payingVendor.value = false
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
    await api.create<any>(`purchase/invoices/${pendingCancel.value.id}/cancel`, {
      reason: 'Cancelled from purchase page'
    })

    if (selectedReceipt.value?.id === pendingCancel.value.id) {
      selectedReceipt.value = null
    }

    feedback.notify('Purchase invoice cancelled', 'Inward stock quantities were reversed.', 'warning')
    cancelOpen.value = false
    pendingCancel.value = null
    await refresh()
  } catch (error) {
    feedback.failed('Could not cancel purchase invoice', error)
  } finally {
    cancelling.value = false
  }
}

async function printReceipt() {
  if (!selectedReceipt.value?.id) return
  const query = new URLSearchParams({
    format: purchasePrintFormat.value,
    copy: purchaseCopyType.value,
    reprint: String(purchaseReprint.value),
    signatures: String(purchaseSignatures.value)
  })
  try {
    await documentPrint.printPdf(`purchase/invoices/${selectedReceipt.value.id}/pdf?${query.toString()}`)
  } catch (error) {
    feedback.failed('Could not print purchase PDF', error)
  }
}

async function downloadPurchasePdf() {
  if (!selectedReceipt.value?.id) {
    return
  }

  downloadingPurchasePdf.value = true
  try {
    const query = new URLSearchParams({
      format: purchasePrintFormat.value,
      copy: purchaseCopyType.value,
      reprint: String(purchaseReprint.value),
      signatures: String(purchaseSignatures.value)
    })
    const response = await fetch(
      `${config.public.apiBase}/purchase/invoices/${selectedReceipt.value.id}/pdf?${query.toString()}`,
      { headers: api.authHeaders() }
    )
    if (!response.ok) {
      throw new Error(`Purchase PDF could not be generated (${response.status}).`)
    }

    const blob = await response.blob()
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = `${selectedReceipt.value.invoiceNumber || 'purchase'}-${purchasePrintFormat.value}.pdf`
    document.body.appendChild(link)
    link.click()
    link.remove()
    URL.revokeObjectURL(url)
    feedback.notify('Purchase PDF downloaded')
  } catch (error) {
    feedback.failed('Could not download purchase PDF', error)
  } finally {
    downloadingPurchasePdf.value = false
  }
}

function lineTotal(item: any) {
  return Math.max((Number(item.costPrice || 0) - Number(item.discountAmount || 0)) * Number(item.quantity || 0), 0)
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

watch(() => purchaseForm.paymentMode, () => {
  if (requiresBankAccount.value && !purchaseForm.bankAccountId) {
    purchaseForm.bankAccountId = bankAccounts.value[0]?.id || null
  }
})

watch(() => purchaseForm.paidAmount, () => {
  if (requiresBankAccount.value && !purchaseForm.bankAccountId) {
    purchaseForm.bankAccountId = bankAccounts.value[0]?.id || null
  }
})

watch(() => paymentVoucherForm.paymentMode, () => {
  if (paymentVoucherRequiresBank.value && !paymentVoucherForm.bankAccountId) {
    paymentVoucherForm.bankAccountId = bankAccounts.value[0]?.id || null
  }
})

watch(() => paymentVoucherForm.amount, () => {
  if (paymentVoucherRequiresBank.value && !paymentVoucherForm.bankAccountId) {
    paymentVoucherForm.bankAccountId = bankAccounts.value[0]?.id || null
  }
})

watch(() => purchaseForm.vendorId, () => applySelectedVendor())

watch(() => purchaseForm.productCategoryId, () => {
  if (purchaseForm.productSubCategoryId && !purchaseLookup.value.subCategories?.some((item: any) => item.id === purchaseForm.productSubCategoryId && (!item.categoryId || item.categoryId === purchaseForm.productCategoryId))) {
    purchaseForm.productSubCategoryId = ''
  }
})
</script>

<template>
  <AuthScreen v-if="!isAuthenticated" @authenticated="refresh" />

  <AppShell
    v-else
    title="Purchase"
    :companies="companies"
    :stores="stores"
    @refresh="refresh"
    @workspace-change="refresh"
  >
    <section class="planner-dashboard">
      <UiModulePageHeader
        title="Purchase Inward"
        description="Record supplier inward invoices, add items, and update stock in one workflow."
        icon="i-lucide-package-plus"
        primary-label="New Inward"
        primary-icon="i-lucide-plus"
        @primary="startCreate"
      >
        <template #actions>
          <UBadge :color="loading ? 'warning' : 'success'" variant="subtle">
            {{ loading ? 'Loading' : `${purchaseInvoices.length} invoices` }}
          </UBadge>
          <UButton icon="i-lucide-plus" label="New Inward" @click="startCreate" />
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

      <UiRegisterPanel
        title="Purchase Register"
        :description="`${filteredRows.length} of ${purchaseInvoices.length} purchase invoices`"
        :loading="loading"
        :error="loadError"
        :empty="filteredRows.length === 0"
        :empty-title="search || invoiceStatusFilter !== 'all' ? 'No matching purchase invoices' : 'No purchase invoices yet'"
        :empty-description="search || invoiceStatusFilter !== 'all' ? 'Change the search or status filter.' : 'Create the first inward invoice to begin stock receiving.'"
        empty-icon="i-lucide-package-plus"
        @retry="refresh"
      >
        <template #actions>
          <UiCrudToolbar
            v-model:search="search"
            search-placeholder="Search invoice, inward, vendor"
            :loading="loading"
            refresh-label="Sync"
            create-label="New Inward"
            @refresh="refresh"
            @create="startCreate"
          >
            <template #filters>
              <USelect
                v-model="invoiceStatusFilter"
                :items="[
                  { label: 'All statuses', value: 'all' },
                  { label: 'Saved', value: 'saved' },
                  { label: 'Cancelled', value: 'cancelled' },
                  { label: 'Refunded', value: 'refunded' }
                ]"
                aria-label="Filter purchase status"
                class="min-w-36"
              />
            </template>
          </UiCrudToolbar>
        </template>

        <div class="planner-table-wrap">
          <UTable :data="filteredRows" :columns="columns" />
        </div>
      </UiRegisterPanel>

      <UiFormSlideover
        v-model:open="formOpen"
        title="New Purchase Inward"
        description="Create or reuse products and receive stock from a supplier."
        submit-label="Save Inward"
        layout="modal"
        content-class="w-[calc(100vw-2rem)] sm:max-w-6xl xl:max-w-7xl"
        :loading="saving"
        @submit="submitPurchase"
      >
        <UFormField label="Existing vendor">
          <USelect v-model="purchaseForm.vendorId" :items="vendorOptions" placeholder="Select vendor or keep manual" />
        </UFormField>
        <div v-if="selectedVendor" class="payroll-summary">
          <span>Vendor bills</span><strong>{{ money(Number(selectedVendor.billAmount || 0)) }}</strong>
          <span>Paid</span><strong>{{ money(Number(selectedVendor.paidAmount || 0)) }}</strong>
          <span>Balance</span><strong>{{ money(Number(selectedVendor.balanceAmount || 0)) }}</strong>
        </div>
        <div class="form-three-column">
          <UFormField label="Vendor" required>
            <UInput v-model="purchaseForm.vendorName" required />
          </UFormField>
          <UFormField label="Vendor mobile">
            <UInput v-model="purchaseForm.vendorMobileNumber" />
          </UFormField>
          <UFormField label="Vendor GSTIN">
            <div class="inline-action-row">
              <UInput v-model="purchaseForm.vendorGstin" class="flex-1" />
              <UButton color="neutral" variant="subtle" icon="i-lucide-search-check" label="Check" type="button" :loading="vendorGstinChecking" @click="validateVendorGstin" />
            </div>
          </UFormField>
        </div>
        <UAlert
          v-if="vendorGstinValidation?.alerts?.length"
          color="warning"
          variant="subtle"
          title="Vendor GSTIN alert"
          :description="vendorGstinValidation.alerts.join(' ')"
        />
        <div class="form-two-column">
          <UFormField label="Supplier invoice">
            <UInput v-model="purchaseForm.invoiceNumber" />
          </UFormField>
          <UFormField label="Inward number">
            <UInput v-model="purchaseForm.inwardNumber" />
          </UFormField>
        </div>
        <div class="form-two-column">
          <UFormField label="Supplier invoice date">
            <UInput v-model="purchaseForm.supplierInvoiceDate" type="date" />
          </UFormField>
          <UFormField label="Due date">
            <UInput v-model="purchaseForm.dueDate" type="date" />
          </UFormField>
        </div>

        <USeparator label="Item" />

        <div class="form-two-column">
          <UFormField label="Barcode scan">
            <div class="inline-action-row">
              <UInput v-model="purchaseForm.barcode" class="flex-1" placeholder="Scan barcode" @keyup.enter="lookupPurchaseProduct" />
              <UButton color="neutral" variant="subtle" icon="i-lucide-scan-barcode" label="Fetch" type="button" @click="lookupPurchaseProduct" />
            </div>
          </UFormField>
          <UFormField label="Product autocomplete">
            <UInput v-model="purchaseForm.productSearch" list="purchase-product-cache" placeholder="Type name, barcode, HSN" @input="refreshPurchaseSuggestions(purchaseForm.productSearch)" @change="lookupPurchaseProduct" />
            <datalist id="purchase-product-cache">
              <option v-for="option in purchaseProductSuggestions" :key="option" :value="option" />
            </datalist>
          </UFormField>
        </div>
        <UAlert
          v-if="selectedPurchaseProduct"
          color="neutral"
          variant="subtle"
          title="Selected product"
          :description="`${selectedPurchaseProduct.name || selectedPurchaseProduct.barcode} | Barcode ${selectedPurchaseProduct.barcode} | MRP ${money(Number(selectedPurchaseProduct.mrp || 0))} | GST ${Number(selectedPurchaseProduct.taxRate || 0)}%`"
        />
        <div class="form-two-column">
          <UFormField label="Product name">
            <UInput v-model="purchaseForm.productName" :disabled="Boolean(purchaseForm.selectedProductId)" />
          </UFormField>
          <UFormField label="Barcode">
            <UInput v-model="purchaseForm.barcode" :disabled="Boolean(purchaseForm.selectedProductId)" />
          </UFormField>
        </div>
        <div class="form-four-column">
          <UFormField label="HSN">
            <UInput v-model="purchaseForm.hsnCode" />
          </UFormField>
          <UFormField label="Unit">
            <USelect v-model="purchaseForm.productUnit" :items="unitOptions" />
          </UFormField>
          <UFormField label="Product type">
            <USelect v-model="purchaseForm.productType" :items="productTypeOptions" />
          </UFormField>
          <UFormField label="Product group">
            <USelect v-model="purchaseForm.productGroup" :items="productGroupOptions" />
          </UFormField>
        </div>
        <div class="form-two-column">
          <UFormField label="Quantity">
            <UInput v-model="purchaseForm.quantity" min="1" type="number" />
          </UFormField>
          <UFormField label="Cost price">
            <UInput v-model="purchaseForm.costPrice" min="0" step="0.01" type="number" />
          </UFormField>
        </div>
        <div class="form-two-column">
          <UFormField label="MRP">
            <UInput v-model="purchaseForm.mrp" min="0" step="0.01" type="number" />
          </UFormField>
          <UFormField label="Unit discount">
            <UInput v-model="purchaseForm.discountAmount" min="0" step="0.01" type="number" />
          </UFormField>
        </div>
        <div class="form-three-column">
          <UFormField label="Tax">
            <USelect v-model="purchaseForm.taxId" :items="taxOptions" placeholder="Default tax" />
          </UFormField>
          <UFormField label="Category">
            <USelect v-model="purchaseForm.productCategoryId" :items="categoryOptions" placeholder="Default category" />
          </UFormField>
          <UFormField label="Sub category">
            <USelect v-model="purchaseForm.productSubCategoryId" :items="subCategoryOptions" placeholder="Default sub category" />
          </UFormField>
        </div>
        <UButton color="neutral" variant="subtle" icon="i-lucide-plus" label="Add Inward Item" type="button" @click="addPurchaseItem" />

        <div class="planner-table-wrap">
          <table class="planner-table">
            <thead>
              <tr>
                <th>Item</th>
                <th>HSN / Unit</th>
                <th>Qty</th>
                <th>Cost</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(item, index) in purchaseCart" :key="`${item.barcode}-${index}`">
                <td>{{ item.productName }}</td>
                <td>{{ item.hsnCode || '-' }} / {{ unitLabel(item.productUnit) }}</td>
                <td>{{ item.quantity }}</td>
                <td>{{ money(lineTotal(item)) }}</td>
                <td>
                  <UButton color="error" variant="ghost" icon="i-lucide-x" size="xs" type="button" @click="removePurchaseItem(index)" />
                </td>
              </tr>
              <tr v-if="purchaseCart.length === 0">
                <td colspan="5">No inward items</td>
              </tr>
            </tbody>
          </table>
        </div>

        <USeparator label="Payment" />

        <UFormField label="Payment mode">
          <USelect v-model="purchaseForm.paymentMode" :items="paymentModeOptions" />
        </UFormField>
        <UFormField v-if="requiresBankAccount" label="Bank account" required>
          <USelect v-model="purchaseForm.bankAccountId" :items="bankAccountOptions" placeholder="Select bank account" />
        </UFormField>
        <div class="form-two-column">
          <UFormField label="Freight">
            <UInput v-model="purchaseForm.frightAmount" min="0" step="0.01" type="number" />
          </UFormField>
          <UFormField label="Paid">
            <UInput v-model="purchaseForm.paidAmount" min="0" step="0.01" type="number" />
          </UFormField>
        </div>
        <div class="payroll-summary">
          <span>Cart total</span><strong>{{ money(purchaseCartTotal) }}</strong>
          <span>Freight</span><strong>{{ money(Number(purchaseForm.frightAmount || 0)) }}</strong>
          <span>Payable</span><strong>{{ money(payableTotal) }}</strong>
        </div>
      </UiFormSlideover>

      <UModal v-model:open="receiptOpen" title="Purchase Invoice" :ui="{ content: 'max-w-3xl' }">
        <template #body>
          <div v-if="selectedReceipt" class="invoice-print-toolbar no-print">
            <USelect v-model="purchasePrintFormat" :items="purchasePrintFormatOptions" />
            <USelect v-model="purchaseCopyType" :items="purchaseCopyOptions" />
            <UCheckbox v-model="purchaseReprint" label="Reprint" />
            <UCheckbox v-model="purchaseSignatures" label="Signature lines" />
          </div>

          <div
            v-if="selectedReceipt"
            class="receipt-print invoice-print-document"
            :class="[`invoice-print-${purchasePrintFormat}`]"
          >
            <header class="receipt-header">
              <span v-if="purchaseReprint" class="invoice-reprint-stamp">REPRINT</span>
              <span class="invoice-copy-stamp">{{ purchaseCopyOptions.find((item) => item.value === purchaseCopyType)?.label }}</span>
              <h2>{{ selectedReceipt.companyName }}</h2>
              <p>{{ selectedReceipt.storeName }}</p>
              <p>Purchase Invoice {{ selectedReceipt.invoiceNumber }} / Inward {{ selectedReceipt.inwardNumber }}</p>
              <p>{{ new Date(selectedReceipt.onDate).toLocaleString() }}</p>
              <p v-if="selectedReceipt.supplierInvoiceDate">Supplier bill date: {{ new Date(selectedReceipt.supplierInvoiceDate).toLocaleDateString() }}</p>
              <p>Due date: {{ new Date(selectedReceipt.dueDate).toLocaleDateString() }}</p>
            </header>

            <div class="receipt-customer">
              <span>Supplier: {{ selectedReceipt.vendorName }}</span>
              <span>GSTIN: {{ selectedReceipt.vendorGstin || '-' }}</span>
            </div>

            <table class="receipt-table">
              <thead>
                <tr>
                  <th>Item</th>
                  <th>HSN</th>
                  <th>Unit</th>
                  <th>Qty</th>
                  <th>MRP</th>
                  <th>GST</th>
                  <th>Amount</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="item in selectedReceipt.items" :key="`${item.barcode}-${item.productName}`">
                  <td>{{ item.productName }}</td>
                  <td>{{ item.hsnCode || '-' }}</td>
                  <td>{{ item.unit || '-' }}</td>
                  <td>{{ item.quantity }}</td>
                  <td>{{ money(Number(item.mrp || 0)) }}</td>
                  <td>{{ money(Number(item.taxAmount || 0)) }}<br><small>CGST {{ money(Number(item.cgstAmount || 0)) }} / SGST {{ money(Number(item.sgstAmount || 0)) }} / IGST {{ money(Number(item.igstAmount || 0)) }}</small></td>
                  <td>{{ money(Number(item.amount || 0)) }}</td>
                </tr>
              </tbody>
            </table>

            <div class="receipt-totals">
              <span>MRP</span><strong>{{ money(Number(selectedReceipt.mrp || selectedReceipt.MRP || 0)) }}</strong>
              <span>Discount</span><strong>{{ money(Number(selectedReceipt.discountAmount || 0)) }}</strong>
              <span>Tax</span><strong>{{ money(Number(selectedReceipt.taxAmount || 0)) }}</strong>
              <span>Freight</span><strong>{{ money(Number(selectedReceipt.freightAmount || 0)) }}</strong>
              <span>Round off</span><strong>{{ money(Number(selectedReceipt.roundOff || 0)) }}</strong>
              <span>Bill amount</span><strong>{{ money(Number(selectedReceipt.billAmount || 0)) }}</strong>
              <span>Paid</span><strong>{{ money(Number(selectedReceipt.paidAmount || 0)) }}</strong>
              <span>Balance</span><strong>{{ money(Number(selectedReceipt.balanceAmount || 0)) }}</strong>
            </div>

            <div v-if="selectedReceipt.payments?.length" class="planner-table-wrap no-print">
              <h3>Purchase payment history</h3>
              <table class="planner-table">
                <thead>
                  <tr>
                    <th>Date</th>
                    <th>Mode</th>
                    <th>Reference</th>
                    <th>Amount</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="payment in selectedReceipt.payments" :key="payment.id">
                    <td>{{ new Date(payment.onDate).toLocaleString() }}</td>
                    <td>{{ payment.paymentMode }}</td>
                    <td>{{ payment.referenceNumber || payment.voucherId || '-' }}</td>
                    <td>{{ money(Number(payment.amount || 0)) }}</td>
                  </tr>
                </tbody>
              </table>
            </div>

            <div v-if="purchaseSignatures" class="invoice-signatures">
              <div>Prepared by</div>
              <div>Checked by</div>
              <div>Supplier</div>
              <div>Authorized</div>
            </div>

            <footer class="receipt-footer">
              Purchase inward recorded in Garmetix.
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
              :loading="downloadingPurchasePdf"
              @click="downloadPurchasePdf"
            />
            <UButton icon="i-lucide-printer" label="Print" @click="printReceipt" />
          </div>
        </template>
      </UModal>


      <UiFormSlideover
        v-model:open="paymentOpen"
        title="Vendor Payment Voucher"
        :description="`Create payment voucher for ${pendingPaymentInvoice?.invoiceNumber || 'purchase invoice'}.`"
        submit-label="Create Voucher"
        layout="modal"
        content-class="sm:max-w-xl"
        :loading="payingVendor"
        @submit="confirmVendorPayment"
      >
        <UAlert
          color="primary"
          variant="subtle"
          title="Outstanding payment"
          :description="`Balance: ${money(Number(pendingPaymentInvoice?.balanceAmount || 0))}`"
        />
        <UFormField label="Amount">
          <UInput v-model="paymentVoucherForm.amount" min="0" step="0.01" type="number" />
        </UFormField>
        <UFormField label="Payment mode">
          <USelect v-model="paymentVoucherForm.paymentMode" :items="paymentModeOptions" />
        </UFormField>
        <UFormField v-if="paymentVoucherRequiresBank" label="Bank account" required>
          <USelect v-model="paymentVoucherForm.bankAccountId" :items="bankAccountOptions" placeholder="Select bank account" />
        </UFormField>
        <div class="form-two-column">
          <UFormField label="Slip / cheque / UTR">
            <UInput v-model="paymentVoucherForm.slipNumber" />
          </UFormField>
          <UFormField label="Payment details">
            <UInput v-model="paymentVoucherForm.paymentDetails" />
          </UFormField>
        </div>
        <UFormField label="Remarks">
          <UTextarea v-model="paymentVoucherForm.remarks" :rows="3" />
        </UFormField>
      </UiFormSlideover>

      <UiConfirmDeleteModal
        v-model:open="cancelOpen"
        title="Cancel Purchase Invoice"
        :description="`Cancel purchase invoice ${pendingCancel?.invoiceNumber || ''}? Inward stock will be reversed.`"
        confirm-label="Cancel Purchase"
        :loading="cancelling"
        @confirm="confirmCancel"
      />
    </section>
  </AppShell>
</template>
