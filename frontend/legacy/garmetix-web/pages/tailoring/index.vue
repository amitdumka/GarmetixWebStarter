<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const workspace = useWorkspace()
const feedback = useUiFeedback()

const loading = ref(false)
const saving = ref(false)
const loadError = ref('')
const activeTab = ref<'stitching' | 'alteration' | 'deliveries' | 'orders' | 'services' | 'vendors'>('stitching')

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const customers = ref<any[]>([])
const vendors = ref<any[]>([])
const vendorRates = ref<any[]>([])
const serviceItems = ref<any[]>([])
const orders = ref<any[]>([])
const dashboard = ref<any | null>(null)
const deliveries = ref<any | null>(null)
const sourceInvoices = ref<any[]>([])
const sourceInvoiceItems = ref<any[]>([])
const selectedOrder = ref<any | null>(null)
const paymentForm = reactive<any>({ onDate: localDateValue(), amount: 0, paymentMode: 0, bankAccountId: null, referenceNumber: '', remarks: '' })

const serviceCategoryOptions = [
  { label: 'Stitching', value: 0 },
  { label: 'Alteration', value: 1 },
  { label: 'Measurement', value: 2 },
  { label: 'Finishing', value: 3 },
  { label: 'Other', value: 4 }
]
const responsibilityOptions = [
  { label: 'Customer chargeable', value: 0 },
  { label: 'In-house expense / store absorbs', value: 1 },
  { label: 'Complimentary', value: 2 }
]
const paymentModeOptions = [
  { label: 'Cash', value: 0 },
  { label: 'Card', value: 1 },
  { label: 'UPI', value: 2 },
  { label: 'Wallet', value: 3 },
  { label: 'IMPS', value: 4 },
  { label: 'RTGS', value: 5 },
  { label: 'NEFT', value: 6 },
  { label: 'Cheque', value: 7 },
  { label: 'Other', value: 14 }
]
const statusOptions = [
  { label: 'Ordered', value: 1 },
  { label: 'Sent to vendor', value: 2 },
  { label: 'Processing', value: 3 },
  { label: 'Ready', value: 4 },
  { label: 'Delivered', value: 5 },
  { label: 'Completed', value: 9 }
]

const stitching = reactive<any>(emptyOrder(0))
const alteration = reactive<any>(emptyOrder(1))
const serviceForm = reactive<any>(emptyService())
const vendorForm = reactive<any>(emptyVendor())
const vendorRateForm = reactive<any>(emptyVendorRate())
const selectedStatus = reactive<Record<string, number>>({})

const companyOptions = computed(() => companies.value.map((item: any) => ({ label: item.name, value: item.id })))
const storeOptions = computed(() => stores.value.map((item: any) => ({ label: item.name || item.storeName || item.storeCode || 'Store', value: item.id })))
const customerOptions = computed(() => customers.value.map((item: any) => ({ label: `${item.name} · ${item.mobileNumber || '-'}`, value: item.id })))
const vendorOptions = computed(() => [{ label: 'In-house / not assigned', value: '__none' }, ...vendors.value.map((item: any) => ({ label: `${item.name} · ${item.mobileNumber || 'No mobile'}`, value: item.id }))])
const tailoringVendorOptions = computed(() => vendors.value.map((item: any) => ({ label: `${item.name} · ${item.mobileNumber || 'No mobile'}`, value: item.id })))
const stitchingServiceOptions = computed(() => serviceItems.value.filter((item: any) => Number(item.category) !== 1).map((item: any) => ({ label: `${item.name} · ₹${item.defaultCustomerRate}`, value: item.id })))
const alterationServiceOptions = computed(() => serviceItems.value.filter((item: any) => Number(item.category) === 1 || Number(item.category) === 3 || Number(item.category) === 4).map((item: any) => ({ label: `${item.name} · ₹${item.defaultCustomerRate}`, value: item.id })))
const allServiceOptions = computed(() => serviceItems.value.map((item: any) => ({ label: `${item.name} · ${categoryText(item.category)} · ₹${item.defaultCustomerRate}`, value: item.id })))
const sourceInvoiceOptions = computed(() => [{ label: 'Select original sale invoice', value: '__none' }, ...sourceInvoices.value.map((item: any) => ({ label: `${item.invoiceNumber} · ${dateText(item.onDate)} · ${money(item.billAmount)}`, value: item.id }))])
const sourceItemOptions = computed(() => [{ label: 'Select invoice item', value: '__none' }, ...sourceInvoiceItems.value.map((item: any) => ({ label: `${item.productName || item.barcode} · ${item.barcode}`, value: item.id }))])

const stitchingTotals = computed(() => calculateTotals(stitching.lines))
const alterationTotals = computed(() => calculateTotals(alteration.lines))
const deliveryGroups = computed(() => [
  { key: 'today', title: 'Today deliveries', color: 'primary', items: deliveries.value?.today || [] },
  { key: 'tomorrow', title: 'Tomorrow deliveries', color: 'info', items: deliveries.value?.tomorrow || [] },
  { key: 'late', title: 'Late delivery', color: 'error', items: deliveries.value?.late || [] },
  { key: 'ready', title: 'Ready for delivery', color: 'success', items: deliveries.value?.ready || [] }
])

function emptyOrder(orderType: number) {
  return {
    companyId: '',
    storeGroupId: '',
    storeId: '',
    orderType,
    customerMobile: '',
    customerId: '',
    vendorId: '__none',
    sourceInvoiceId: '__none',
    sourceInvoiceItemId: '__none',
    sourceProductId: null,
    sourceProductName: '',
    sourceBarcode: '',
    expectedDeliveryDate: localDateValue(new Date(Date.now() + 3 * 86400000)),
    measurementsJson: '',
    customerInstructions: '',
    internalRemarks: '',
    lines: [emptyLine(orderType)]
  }
}

function emptyLine(orderType = 0) {
  return {
    serviceItemId: null,
    serviceName: '',
    category: orderType === 1 ? 1 : 0,
    garmentName: '',
    barcode: '',
    quantity: 1,
    customerRate: 0,
    vendorRate: 0,
    discountAmount: 0,
    costResponsibility: 0,
    expectedDeliveryDate: localDateValue(new Date(Date.now() + 3 * 86400000)),
    measurementsJson: '',
    instructions: '',
    vendorRemarks: ''
  }
}

function emptyVendor() {
  return {
    companyId: '',
    name: '',
    mobileNumber: '',
    address: '',
    city: '',
    email: '',
    gstin: '',
    active: true
  }
}

function emptyVendorRate() {
  return {
    companyId: '',
    storeGroupId: '',
    storeId: '',
    vendorId: '',
    serviceItemId: '',
    customerRate: 0,
    vendorRate: 0,
    effectiveFrom: localDateValue(),
    active: true,
    remarks: ''
  }
}

function emptyService() {
  return {
    companyId: '',
    storeGroupId: '',
    storeId: '',
    serviceCode: '',
    name: '',
    category: 0,
    defaultCustomerRate: 0,
    defaultVendorRate: 0,
    taxRate: 5,
    hsnCode: '9988',
    productId: null,
    active: true,
    remarks: ''
  }
}

async function loadAll() {
  if (!auth.isAuthenticated.value) return
  loading.value = true
  loadError.value = ''
  try {
    const [companyList, storeList, customerList, vendorList, vendorRateList, serviceList, orderList, dashboardData, deliveryData] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores'),
      api.list<any>('customers'),
      api.get<any[]>('tailoring/vendors'),
      api.get<any[]>('tailoring/vendor-rates'),
      api.get<any[]>('tailoring/service-items'),
      api.get<any[]>('tailoring/orders'),
      api.get<any>('tailoring/dashboard'),
      api.get<any>('tailoring/deliveries/overview')
    ])
    companies.value = companyList
    stores.value = storeList
    customers.value = customerList
    vendors.value = vendorList
    vendorRates.value = vendorRateList
    serviceItems.value = serviceList
    orders.value = orderList
    dashboard.value = dashboardData
    deliveries.value = deliveryData
    hydrateDefaults(stitching)
    hydrateDefaults(alteration)
    hydrateServiceDefaults()
    hydrateVendorDefaults()
  } catch (error: any) {
    loadError.value = error?.data?.message || error?.message || 'Could not load tailoring and alteration workspace.'
  } finally {
    loading.value = false
  }
}

function hydrateDefaults(form: any) {
  if (!form.companyId && workspace.companyId.value) form.companyId = workspace.companyId.value
  if (!form.storeId && workspace.storeId.value) form.storeId = workspace.storeId.value
  if (!form.storeGroupId && workspace.storeGroupId.value) form.storeGroupId = workspace.storeGroupId.value
  if (!form.companyId && companies.value[0]) form.companyId = companies.value[0].id
  if (!form.storeId && stores.value[0]) {
    form.storeId = stores.value[0].id
    form.storeGroupId = stores.value[0].storeGroupId || form.storeGroupId
    form.companyId = stores.value[0].companyId || form.companyId
  }
}
function hydrateServiceDefaults() {
  serviceForm.companyId = serviceForm.companyId || stitching.companyId || alteration.companyId
  serviceForm.storeGroupId = serviceForm.storeGroupId || stitching.storeGroupId || alteration.storeGroupId
  serviceForm.storeId = serviceForm.storeId || stitching.storeId || alteration.storeId
}
function hydrateVendorDefaults() {
  vendorForm.companyId = vendorForm.companyId || stitching.companyId || alteration.companyId
  vendorRateForm.companyId = vendorRateForm.companyId || stitching.companyId || alteration.companyId
  vendorRateForm.storeGroupId = vendorRateForm.storeGroupId || stitching.storeGroupId || alteration.storeGroupId
  vendorRateForm.storeId = vendorRateForm.storeId || stitching.storeId || alteration.storeId
  if (!vendorRateForm.vendorId && vendors.value[0]) vendorRateForm.vendorId = vendors.value[0].id
  if (!vendorRateForm.serviceItemId && serviceItems.value[0]) {
    vendorRateForm.serviceItemId = serviceItems.value[0].id
    const item = serviceItems.value[0]
    vendorRateForm.customerRate = Number(item.defaultCustomerRate || 0)
    vendorRateForm.vendorRate = Number(item.defaultVendorRate || 0)
  }
}

function calculateTotals(lines: any[]) {
  return lines.reduce((total: any, line: any) => {
    const qty = Number(line.quantity || 0)
    const grossBeforeDiscount = qty * Number(line.customerRate || 0)
    const discount = Number(line.discountAmount || 0)
    const charge = Math.max(0, grossBeforeDiscount - discount)
    const cost = Math.max(0, qty * Number(line.vendorRate || 0))
    total.gross += grossBeforeDiscount
    total.discount += discount
    total.charge += charge
    total.taxable += charge / 1.05
    total.tax += charge - charge / 1.05
    total.cost += cost
    total.inHouse += Number(line.costResponsibility) === 1 ? cost : 0
    total.profit += charge - cost
    return total
  }, { gross: 0, discount: 0, charge: 0, taxable: 0, tax: 0, cost: 0, inHouse: 0, profit: 0 })
}
function normalizeVendorId(form: any) {
  return form.vendorId && form.vendorId !== '__none' ? form.vendorId : ''
}

function vendorSpecificRate(serviceItemId: string, vendorId: string) {
  if (!serviceItemId || !vendorId) return null
  return vendorRates.value.find((rate: any) => rate.serviceItemId === serviceItemId && rate.vendorId === vendorId && rate.active !== false) || null
}

function applyVendorRateToLine(line: any, form: any) {
  const vendorId = normalizeVendorId(form)
  const rate = vendorSpecificRate(line.serviceItemId, vendorId)
  if (!rate) return
  if (Number(rate.customerRate || 0) > 0) line.customerRate = Number(rate.customerRate || 0)
  line.vendorRate = Number(rate.vendorRate || 0)
}

function applyVendorRates(form: any) {
  form.lines.forEach((line: any) => applyVendorRateToLine(line, form))
}

function addLine(form: any) { form.lines.push(emptyLine(form.orderType)) }
function removeLine(form: any, index: number) { if (form.lines.length > 1) form.lines.splice(index, 1) }
function applyService(line: any, form?: any) {
  const item = serviceItems.value.find((service: any) => service.id === line.serviceItemId)
  if (!item) return
  line.serviceName = item.name
  line.category = item.category
  line.customerRate = item.defaultCustomerRate
  line.vendorRate = item.defaultVendorRate
  if (form) applyVendorRateToLine(line, form)
}
async function lookupCustomer(form: any) {
  if (!form.customerMobile || form.customerMobile.trim().length < 4) {
    feedback.showWarning('Enter mobile number first.')
    return
  }
  try {
    const customer = await api.get<any>(`tailoring/customers/by-mobile?mobile=${encodeURIComponent(form.customerMobile.trim())}`)
    form.customerId = customer.id
    form.customerMobile = customer.mobileNumber
    feedback.showSuccess(`Customer found: ${customer.name}`)
    if (form.orderType === 1) await loadSourceInvoices(form)
  } catch (error: any) {
    feedback.showError(error?.data?.message || error?.message || 'Customer not found. Add customer first from Customer master.')
  }
}
async function loadSourceInvoices(form: any) {
  if (!form.customerId && !form.customerMobile) return
  const query = form.customerId ? `customerId=${form.customerId}` : `mobile=${encodeURIComponent(form.customerMobile)}`
  sourceInvoices.value = await api.get<any[]>(`tailoring/alteration/source-invoices?${query}`)
}
async function loadSourceInvoiceItems(form: any) {
  sourceInvoiceItems.value = []
  if (!form.sourceInvoiceId || form.sourceInvoiceId === '__none') return
  sourceInvoiceItems.value = await api.get<any[]>(`tailoring/alteration/source-invoices/${form.sourceInvoiceId}/items`)
}
function applySourceInvoiceItem(form: any) {
  const item = sourceInvoiceItems.value.find((source: any) => source.id === form.sourceInvoiceItemId)
  if (!item) return
  form.sourceInvoiceItemId = item.id
  form.sourceProductId = item.productId
  form.sourceProductName = item.productName
  form.sourceBarcode = item.barcode
  if (form.lines[0]) {
    form.lines[0].garmentName = item.productName
    form.lines[0].barcode = item.barcode
  }
}
function buildPayload(form: any) {
  return {
    ...form,
    vendorId: form.vendorId === '__none' ? null : form.vendorId,
    sourceInvoiceId: form.sourceInvoiceId === '__none' ? null : form.sourceInvoiceId,
    sourceInvoiceItemId: form.sourceInvoiceItemId === '__none' ? null : form.sourceInvoiceItemId,
    lines: form.lines.map((line: any) => ({
      ...line,
      quantity: Number(line.quantity || 0),
      customerRate: Number(line.customerRate || 0),
      vendorRate: Number(line.vendorRate || 0),
      discountAmount: Number(line.discountAmount || 0)
    }))
  }
}
async function saveOrder(form: any, successMessage: string) {
  saving.value = true
  try {
    await api.create<any>('tailoring/orders', buildPayload(form))
    Object.assign(form, emptyOrder(form.orderType))
    hydrateDefaults(form)
    await loadAll()
    activeTab.value = 'orders'
    feedback.showSuccess(successMessage)
  } catch (error: any) {
    feedback.showError(error?.data?.message || error?.message || 'Could not save order.')
  } finally {
    saving.value = false
  }
}
async function saveService() {
  saving.value = true
  try {
    await api.create<any>('tailoring/service-items', {
      ...serviceForm,
      defaultCustomerRate: Number(serviceForm.defaultCustomerRate || 0),
      defaultVendorRate: Number(serviceForm.defaultVendorRate || 0),
      taxRate: Number(serviceForm.taxRate || 5)
    })
    Object.assign(serviceForm, emptyService())
    hydrateServiceDefaults()
    await loadAll()
    feedback.showSuccess('Service item saved.')
  } catch (error: any) {
    feedback.showError(error?.data?.message || error?.message || 'Could not save service item.')
  } finally {
    saving.value = false
  }
}
async function saveTailoringVendor() {
  if (!vendorForm.companyId) vendorForm.companyId = stitching.companyId || alteration.companyId
  if (!vendorForm.name || !vendorForm.mobileNumber) {
    feedback.showWarning('Vendor name and mobile number are required.')
    return
  }
  saving.value = true
  try {
    await api.create<any>('tailoring/vendors', { ...vendorForm })
    Object.assign(vendorForm, emptyVendor(), { companyId: stitching.companyId || alteration.companyId })
    await loadAll()
    feedback.showSuccess('Tailoring / alteration vendor added.')
  } catch (error: any) {
    feedback.showError(error?.data?.message || error?.message || 'Could not save tailoring vendor.')
  } finally {
    saving.value = false
  }
}

async function saveVendorRate() {
  hydrateVendorDefaults()
  if (!vendorRateForm.vendorId || !vendorRateForm.serviceItemId) {
    feedback.showWarning('Select vendor and service item first.')
    return
  }
  saving.value = true
  try {
    await api.create<any>('tailoring/vendor-rates', {
      ...vendorRateForm,
      customerRate: Number(vendorRateForm.customerRate || 0),
      vendorRate: Number(vendorRateForm.vendorRate || 0)
    })
    await loadAll()
    feedback.showSuccess('Vendor service rate saved.')
  } catch (error: any) {
    feedback.showError(error?.data?.message || error?.message || 'Could not save vendor rate.')
  } finally {
    saving.value = false
  }
}

function seedVendorRateFromService() {
  const item = serviceItems.value.find((service: any) => service.id === vendorRateForm.serviceItemId)
  if (!item) return
  vendorRateForm.customerRate = Number(item.defaultCustomerRate || 0)
  vendorRateForm.vendorRate = Number(item.defaultVendorRate || 0)
}

async function openOrder(order: any) {
  selectedOrder.value = await api.get<any>(`tailoring/orders/${order.id}`)
}
async function updateStatus(order: any, status: number, remarks = '') {
  await api.create<any>(`tailoring/orders/${order.id}/status`, { status, eventDate: localDateValue(), remarks: remarks || `Marked ${statusText(status)}` })
  await loadAll()
  feedback.showSuccess(`Order marked ${statusText(status)}.`)
}
async function convertToInvoice(order: any) {
  await api.create<any>(`tailoring/orders/${order.id}/convert-to-service-invoice`, { invoiceDate: localDateValue(), additionalPaidAmount: 0, additionalPaymentMode: 0, bankAccountId: null, referenceNumber: '', remarks: 'Converted from tailoring order' })
  await loadAll()
  feedback.showSuccess('Service invoice created with 5% GST.')
}
async function receivePayment(order: any) {
  await api.create<any>(`tailoring/orders/${order.id}/receive-payment`, { ...paymentForm, amount: Number(paymentForm.amount || 0) })
  await loadAll()
  feedback.showSuccess('Customer receipt saved.')
}
async function printDocument(order: any, invoice = false) {
  const data = await api.get<any>(`tailoring/orders/${order.id}/${invoice ? 'print-invoice' : 'print-order'}`)
  const html = buildPrintHtml(data)
  const win = window.open('', '_blank', 'width=900,height=1000')
  if (!win) return
  win.document.write(html)
  win.document.close()
  win.focus()
  setTimeout(() => win.print(), 400)
}
function buildPrintHtml(data: any) {
  const lineRows = (data.lines || []).map((line: any) => `<tr><td>${escapeHtml(line.serviceName)}</td><td>${escapeHtml(line.garmentName || '')}</td><td>${line.quantity}</td><td>${money(line.customerRate)}</td><td>${money(line.discountAmount)}</td><td>${money(line.taxAmount)}</td><td>${money(line.lineTotal)}</td></tr>`).join('')
  const copy = (label: string) => `<section class="copy"><h1>${escapeHtml(data.documentType)} <small>${label}</small></h1><div class="meta"><p><b>Order:</b> ${escapeHtml(data.orderNumber)}</p><p><b>Invoice:</b> ${escapeHtml(data.serviceInvoiceNumber || '-')}</p><p><b>Customer:</b> ${escapeHtml(data.customerName)} / ${escapeHtml(data.customerMobileNumber || '')}</p><p><b>Delivery:</b> ${escapeHtml(dateText(data.expectedDeliveryDate))}</p></div><table><thead><tr><th>Service</th><th>Garment</th><th>Qty</th><th>Rate</th><th>Discount</th><th>GST 5%</th><th>Total</th></tr></thead><tbody>${lineRows}</tbody></table><div class="totals"><p>Taxable: ${money(data.taxableAmount)}</p><p>GST: ${money(data.taxAmount)}</p><p><b>Bill: ${money(data.billAmount)}</b></p><p>Paid: ${money(data.paidAmount)}</p><p>Balance: ${money(data.balanceAmount)}</p></div></section>`
  return `<!doctype html><html><head><title>${escapeHtml(data.documentType)}</title><style>body{font-family:Arial,sans-serif;color:#111}.copy{page-break-after:always;border:1px solid #ddd;padding:18px;margin:12px}h1{margin:0 0 12px}small{font-size:13px;color:#555}.meta{display:grid;grid-template-columns:repeat(2,1fr);gap:4px 18px;font-size:13px}table{width:100%;border-collapse:collapse;margin-top:12px}th,td{border:1px solid #ddd;padding:7px;font-size:12px;text-align:left}.totals{text-align:right;margin-top:12px}</style></head><body>${(data.copies || ['Customer Copy','Store Copy']).map(copy).join('')}</body></html>`
}
function statusText(status: number) { return statusOptions.find(item => item.value === status)?.label || 'updated' }
function localDateValue(date = new Date()) { const local = new Date(date.getTime() - date.getTimezoneOffset() * 60000); return local.toISOString().slice(0, 10) }
function money(value: any) { return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR' }).format(Number(value || 0)) }
function dateText(value: any) { return value ? new Date(value).toLocaleDateString('en-IN') : '-' }
function escapeHtml(value: any) { return String(value ?? '').replace(/[&<>"]/g, (char) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[char] || char)) }
function categoryText(value: any) { return serviceCategoryOptions.find(item => item.value === Number(value))?.label || 'Service' }
function badgeColor(status: string) { return status === 'ReadyForDelivery' ? 'success' : status === 'Delivered' || status === 'Completed' ? 'primary' : status === 'Cancelled' ? 'error' : 'warning' }

watch(() => stitching.storeId, () => { hydrateDefaults(stitching); hydrateVendorDefaults() })
watch(() => alteration.storeId, () => { hydrateDefaults(alteration); hydrateVendorDefaults() })
watch(() => alteration.sourceInvoiceId, () => loadSourceInvoiceItems(alteration))
watch(() => alteration.sourceInvoiceItemId, () => applySourceInvoiceItem(alteration))
onMounted(loadAll)
</script>

<template>
  <AppShell title="Tailoring & Alteration" :companies="companies" :stores="stores" @refresh="loadAll" @workspace-change="loadAll">
    <div class="space-y-6">
      <PageHero
        title="Tailoring & Alteration Pro"
        subtitle="Separate stitching and alteration workflows with customer mobile lookup, multi-line service orders, 5% GST service invoices, two-copy printing, delivery dashboards, vendor costing and in-house alteration cost impact."
        icon="i-lucide-scissors"
      />

      <UAlert v-if="loadError" color="error" variant="soft" :title="loadError" />

      <div class="grid gap-4 md:grid-cols-5">
        <UCard><p class="text-sm text-muted">Pending</p><p class="text-2xl font-bold">{{ dashboard?.pendingOrders || 0 }}</p></UCard>
        <UCard><p class="text-sm text-muted">Today</p><p class="text-2xl font-bold">{{ dashboard?.dueToday || 0 }}</p></UCard>
        <UCard><p class="text-sm text-muted">Late</p><p class="text-2xl font-bold text-error">{{ dashboard?.overdue || 0 }}</p></UCard>
        <UCard><p class="text-sm text-muted">Ready</p><p class="text-2xl font-bold text-success">{{ dashboard?.readyForDelivery || 0 }}</p></UCard>
        <UCard><p class="text-sm text-muted">Customer due</p><p class="text-xl font-bold">{{ money(dashboard?.customerBalance) }}</p></UCard>
      </div>

      <div class="flex flex-wrap gap-2">
        <UButton :variant="activeTab === 'stitching' ? 'solid' : 'soft'" icon="i-lucide-shirt" @click="activeTab = 'stitching'">Tailoring / Stitching</UButton>
        <UButton :variant="activeTab === 'alteration' ? 'solid' : 'soft'" icon="i-lucide-scissors" @click="activeTab = 'alteration'">Alteration</UButton>
        <UButton :variant="activeTab === 'deliveries' ? 'solid' : 'soft'" icon="i-lucide-calendar-days" @click="activeTab = 'deliveries'">Delivery board</UButton>
        <UButton :variant="activeTab === 'orders' ? 'solid' : 'soft'" icon="i-lucide-list-checks" @click="activeTab = 'orders'">All orders</UButton>
        <UButton :variant="activeTab === 'services' ? 'solid' : 'soft'" icon="i-lucide-badge-indian-rupee" @click="activeTab = 'services'">Service master</UButton>
        <UButton :variant="activeTab === 'vendors' ? 'solid' : 'soft'" icon="i-lucide-users-round" @click="activeTab = 'vendors'">Vendors & Rates</UButton>
      </div>

      <section v-if="activeTab === 'stitching'" class="grid gap-6 xl:grid-cols-[0.95fr_1.05fr]">
        <UCard>
          <template #header>
            <div><p class="text-lg font-semibold">New Tailoring / Stitching Service Order</p><p class="text-sm text-muted">Customer mobile lookup, measurements, multiple items and expected delivery date.</p></div>
          </template>
          <div class="grid gap-3 md:grid-cols-2">
            <UFormField label="Store"><USelect v-model="stitching.storeId" :items="storeOptions" /></UFormField>
            <UFormField label="Expected delivery"><UInput v-model="stitching.expectedDeliveryDate" type="date" /></UFormField>
            <UFormField label="Customer mobile"><div class="flex gap-2"><UInput v-model="stitching.customerMobile" placeholder="Mobile number" /><UButton icon="i-lucide-search" @click="lookupCustomer(stitching)">Fetch</UButton></div></UFormField>
            <UFormField label="Customer"><USelect v-model="stitching.customerId" :items="customerOptions" /></UFormField>
            <UFormField label="Tailoring vendor"><USelect v-model="stitching.vendorId" :items="vendorOptions" @update:model-value="applyVendorRates(stitching)" /></UFormField>
            <UFormField label="Order notes"><UInput v-model="stitching.internalRemarks" placeholder="Counter/user remarks" /></UFormField>
            <UFormField class="md:col-span-2" label="Customer instructions"><UTextarea v-model="stitching.customerInstructions" placeholder="Fit, style, fabric, delivery commitment, special instructions" /></UFormField>
          </div>
          <div class="mt-5 space-y-3">
            <div v-for="(line, index) in stitching.lines" :key="index" class="rounded-xl border border-default p-4">
              <div class="mb-2 flex items-center justify-between"><p class="font-semibold">Item {{ index + 1 }}</p><UButton v-if="stitching.lines.length > 1" size="xs" color="error" variant="soft" @click="removeLine(stitching, index)">Remove</UButton></div>
              <div class="grid gap-3 md:grid-cols-3">
                <UFormField label="Service"><USelect v-model="line.serviceItemId" :items="stitchingServiceOptions" @update:model-value="applyService(line, stitching)" /></UFormField>
                <UFormField label="Service name"><UInput v-model="line.serviceName" /></UFormField>
                <UFormField label="Garment / item"><UInput v-model="line.garmentName" placeholder="Shirt, Pant, Suit" /></UFormField>
                <UFormField label="Qty"><UInput v-model="line.quantity" type="number" /></UFormField>
                <UFormField label="Customer rate"><UInput v-model="line.customerRate" type="number" /></UFormField>
                <UFormField label="Discount"><UInput v-model="line.discountAmount" type="number" /></UFormField>
                <UFormField label="Vendor rate"><UInput v-model="line.vendorRate" type="number" /></UFormField>
                <UFormField label="Delivery"><UInput v-model="line.expectedDeliveryDate" type="date" /></UFormField>
                <UFormField label="Measurements"><UInput v-model="line.measurementsJson" placeholder="Chest 40, Waist 34..." /></UFormField>
                <UFormField class="md:col-span-3" label="Line instructions"><UTextarea v-model="line.instructions" /></UFormField>
              </div>
            </div>
            <UButton variant="soft" icon="i-lucide-plus" @click="addLine(stitching)">Add tailoring item</UButton>
          </div>
          <div class="mt-4 grid gap-2 rounded-lg bg-muted p-3 text-sm md:grid-cols-5">
            <span>Gross <b>{{ money(stitchingTotals.gross) }}</b></span><span>Discount <b>{{ money(stitchingTotals.discount) }}</b></span><span>GST 5% <b>{{ money(stitchingTotals.tax) }}</b></span><span>Bill <b>{{ money(stitchingTotals.charge) }}</b></span><span>Vendor cost <b>{{ money(stitchingTotals.cost) }}</b></span>
          </div>
          <UButton class="mt-4" icon="i-lucide-save" :loading="saving" @click="saveOrder(stitching, 'Tailoring service order saved.')">Save tailoring order</UButton>
        </UCard>

        <UCard>
          <template #header>Today / Tomorrow delivery focus</template>
          <div class="space-y-3">
            <div v-for="group in deliveryGroups.slice(0, 2)" :key="group.key" class="rounded-lg border border-default p-3">
              <div class="mb-2 flex items-center justify-between"><p class="font-semibold">{{ group.title }}</p><UBadge :color="group.color">{{ group.items.length }}</UBadge></div>
              <div v-for="order in group.items" :key="order.id" class="flex items-center justify-between border-b border-default py-2 last:border-0">
                <div><p class="font-medium">{{ order.orderNumber }} · {{ order.customerName }}</p><p class="text-xs text-muted">{{ order.orderType }} · {{ order.customerMobileNumber }}</p></div>
                <UButton size="xs" variant="soft" @click="updateStatus(order, 4, 'Ready from delivery board')">Ready</UButton>
              </div>
            </div>
          </div>
        </UCard>
      </section>

      <section v-if="activeTab === 'alteration'" class="grid gap-6 xl:grid-cols-[0.95fr_1.05fr]">
        <UCard>
          <template #header>
            <div><p class="text-lg font-semibold">New Alteration Order</p><p class="text-sm text-muted">Alteration is separate from stitching and links to original sale invoice/product item.</p></div>
          </template>
          <div class="grid gap-3 md:grid-cols-2">
            <UFormField label="Store"><USelect v-model="alteration.storeId" :items="storeOptions" /></UFormField>
            <UFormField label="Expected delivery"><UInput v-model="alteration.expectedDeliveryDate" type="date" /></UFormField>
            <UFormField label="Customer mobile"><div class="flex gap-2"><UInput v-model="alteration.customerMobile" /><UButton icon="i-lucide-search" @click="lookupCustomer(alteration)">Fetch</UButton></div></UFormField>
            <UFormField label="Customer"><USelect v-model="alteration.customerId" :items="customerOptions" @update:model-value="loadSourceInvoices(alteration)" /></UFormField>
            <UFormField label="Sale invoice"><USelect v-model="alteration.sourceInvoiceId" :items="sourceInvoiceOptions" placeholder="Select original invoice" /></UFormField>
            <UFormField label="Invoice item / product"><USelect v-model="alteration.sourceInvoiceItemId" :items="sourceItemOptions" placeholder="Select altered item" /></UFormField>
            <UFormField label="Tailoring / alteration vendor"><USelect v-model="alteration.vendorId" :items="vendorOptions" @update:model-value="applyVendorRates(alteration)" /></UFormField>
            <UFormField label="Source barcode"><UInput v-model="alteration.sourceBarcode" /></UFormField>
            <UFormField class="md:col-span-2" label="Alteration details"><UTextarea v-model="alteration.customerInstructions" placeholder="Length, fitting, waist, sleeve, damage repair, customer promise" /></UFormField>
          </div>
          <div class="mt-5 space-y-3">
            <div v-for="(line, index) in alteration.lines" :key="index" class="rounded-xl border border-default p-4">
              <div class="mb-2 flex items-center justify-between"><p class="font-semibold">Alteration item {{ index + 1 }}</p><UButton v-if="alteration.lines.length > 1" size="xs" color="error" variant="soft" @click="removeLine(alteration, index)">Remove</UButton></div>
              <div class="grid gap-3 md:grid-cols-3">
                <UFormField label="Alteration service"><USelect v-model="line.serviceItemId" :items="alterationServiceOptions" @update:model-value="applyService(line, alteration)" /></UFormField>
                <UFormField label="Service name"><UInput v-model="line.serviceName" /></UFormField>
                <UFormField label="Garment"><UInput v-model="line.garmentName" /></UFormField>
                <UFormField label="Qty"><UInput v-model="line.quantity" type="number" /></UFormField>
                <UFormField label="Customer charge"><UInput v-model="line.customerRate" type="number" /></UFormField>
                <UFormField label="Discount"><UInput v-model="line.discountAmount" type="number" /></UFormField>
                <UFormField label="Vendor / in-house cost"><UInput v-model="line.vendorRate" type="number" /></UFormField>
                <UFormField label="Cost responsibility"><USelect v-model="line.costResponsibility" :items="responsibilityOptions" /></UFormField>
                <UFormField label="Delivery"><UInput v-model="line.expectedDeliveryDate" type="date" /></UFormField>
                <UFormField class="md:col-span-3" label="Work instructions"><UTextarea v-model="line.instructions" /></UFormField>
              </div>
            </div>
            <UButton variant="soft" icon="i-lucide-plus" @click="addLine(alteration)">Add alteration item</UButton>
          </div>
          <UAlert class="mt-4" color="warning" variant="soft" title="In-house alteration cost impact" description="When cost responsibility is In-house expense, delivery applies alteration cost impact to the source stock item once and reduces actual margin." />
          <div class="mt-4 grid gap-2 rounded-lg bg-muted p-3 text-sm md:grid-cols-5">
            <span>Customer bill <b>{{ money(alterationTotals.charge) }}</b></span><span>Discount <b>{{ money(alterationTotals.discount) }}</b></span><span>GST 5% <b>{{ money(alterationTotals.tax) }}</b></span><span>Vendor/in-house cost <b>{{ money(alterationTotals.cost) }}</b></span><span>Margin impact <b>{{ money(alterationTotals.profit) }}</b></span>
          </div>
          <UButton class="mt-4" icon="i-lucide-save" :loading="saving" @click="saveOrder(alteration, 'Alteration order saved.')">Save alteration order</UButton>
        </UCard>

        <UCard>
          <template #header>Alteration rules</template>
          <div class="space-y-3 text-sm text-muted">
            <p>1. Customer is fetched by mobile number before creating alteration.</p>
            <p>2. Original sale invoice and item are linked for audit/profit analysis.</p>
            <p>3. Customer can be charged more than vendor cost; margin is shown separately.</p>
            <p>4. In-house expense is applied to source item cost at delivery so margin is reduced.</p>
            <p>5. Service invoice uses 5% GST and can be printed in two copies.</p>
          </div>
        </UCard>
      </section>

      <section v-if="activeTab === 'deliveries'" class="space-y-4">
        <div class="grid gap-4 lg:grid-cols-2">
          <UCard v-for="group in deliveryGroups" :key="group.key">
            <template #header><div class="flex items-center justify-between"><span>{{ group.title }}</span><UBadge :color="group.color">{{ group.items.length }}</UBadge></div></template>
            <div v-for="order in group.items" :key="order.id" class="rounded-lg border border-default p-3 mb-2">
              <div class="flex items-start justify-between gap-3"><div><p class="font-semibold">{{ order.orderNumber }} · {{ order.customerName }}</p><p class="text-xs text-muted">{{ dateText(order.expectedDeliveryDate) }} · {{ order.customerMobileNumber }} · {{ order.vendorName || 'In-house' }}</p></div><UBadge :color="badgeColor(order.status)" variant="subtle">{{ order.status }}</UBadge></div>
              <div class="mt-3 flex flex-wrap gap-2"><UButton size="xs" variant="soft" @click="updateStatus(order, 3, 'Processing from delivery board')">Processing</UButton><UButton size="xs" variant="soft" @click="updateStatus(order, 4, 'Ready from delivery board')">Ready</UButton><UButton size="xs" color="success" variant="soft" @click="updateStatus(order, 5, 'Delivered from delivery board')">Delivered</UButton><UButton size="xs" variant="soft" @click="printDocument(order, false)">Print order</UButton><UButton size="xs" variant="soft" @click="convertToInvoice(order)">Convert to service invoice</UButton></div>
            </div>
          </UCard>
        </div>
      </section>

      <section v-if="activeTab === 'orders'" class="space-y-3">
        <UCard>
          <template #header>Order history and status</template>
          <div v-for="order in orders" :key="order.id" class="rounded-lg border border-default p-3 mb-3">
            <div class="flex flex-wrap items-center justify-between gap-2"><div><p class="font-semibold">{{ order.orderNumber }} · {{ order.customerName }}</p><p class="text-xs text-muted">{{ order.orderType }} · Delivery {{ dateText(order.expectedDeliveryDate) }} · Vendor {{ order.vendorName || 'In-house / not assigned' }}</p></div><UBadge :color="badgeColor(order.status)" variant="subtle">{{ order.status }}</UBadge></div>
            <div class="mt-2 grid gap-2 text-sm md:grid-cols-5"><span>Bill {{ money(order.customerChargeAmount) }}</span><span>Received {{ money(order.customerReceivedAmount) }}</span><span>Due {{ money(order.customerBalanceAmount) }}</span><span>Vendor due {{ money(order.vendorBalanceAmount) }}</span><span>Profit {{ money(order.profitImpactAmount) }}</span></div>
            <div class="mt-3 flex flex-wrap gap-2"><UButton size="xs" variant="soft" @click="openOrder(order)">History</UButton><UButton size="xs" variant="soft" @click="updateStatus(order, 3)">Processing</UButton><UButton size="xs" variant="soft" @click="updateStatus(order, 4)">Ready</UButton><UButton size="xs" variant="soft" @click="updateStatus(order, 5)">Delivered</UButton><UButton size="xs" variant="soft" @click="printDocument(order, false)">Print order</UButton><UButton size="xs" variant="soft" @click="convertToInvoice(order)">Convert to service invoice</UButton><UButton v-if="order.serviceInvoiceNumber" size="xs" variant="soft" @click="printDocument(order, true)">Print invoice</UButton><UButton size="xs" variant="soft" @click="receivePayment(order)">Receive payment</UButton></div>
          </div>
        </UCard>
      </section>

      <section v-if="activeTab === 'services'" class="grid gap-6 lg:grid-cols-[0.8fr_1.2fr]">
        <UCard>
          <template #header>Service master</template>
          <div class="grid gap-3">
            <UFormField label="Store"><USelect v-model="serviceForm.storeId" :items="storeOptions" /></UFormField>
            <UFormField label="Code"><UInput v-model="serviceForm.serviceCode" placeholder="STITCH-SHIRT" /></UFormField>
            <UFormField label="Name"><UInput v-model="serviceForm.name" placeholder="Shirt stitching / Pant alteration" /></UFormField>
            <UFormField label="Category"><USelect v-model="serviceForm.category" :items="serviceCategoryOptions" /></UFormField>
            <UFormField label="Customer rate"><UInput v-model="serviceForm.defaultCustomerRate" type="number" /></UFormField>
            <UFormField label="Vendor rate"><UInput v-model="serviceForm.defaultVendorRate" type="number" /></UFormField>
            <UFormField label="GST rate"><UInput v-model="serviceForm.taxRate" type="number" /></UFormField>
            <UButton icon="i-lucide-save" :loading="saving" @click="saveService">Save service item</UButton>
          </div>
        </UCard>
        <UCard>
          <template #header>Services</template>
          <div class="space-y-2">
            <div v-for="item in serviceItems" :key="item.id" class="flex items-center justify-between border-b border-default py-2 last:border-0"><div><p class="font-medium">{{ item.name }}</p><p class="text-xs text-muted">{{ item.serviceCode }} · GST {{ item.taxRate || 5 }}% · Vendor {{ money(item.defaultVendorRate) }}</p></div><p class="font-semibold">{{ money(item.defaultCustomerRate) }}</p></div>
          </div>
        </UCard>
      </section>


      <section v-if="activeTab === 'vendors'" class="grid gap-6 xl:grid-cols-[0.9fr_1.1fr]">
        <div class="space-y-6">
          <UCard>
            <template #header>
              <div><p class="text-lg font-semibold">Add Tailoring / Alteration Vendor</p><p class="text-sm text-muted">Use this for stitching job workers, alteration vendors, embroidery/finishing partners and in-house vendor tracking.</p></div>
            </template>
            <div class="grid gap-3 md:grid-cols-2">
              <UFormField label="Vendor name"><UInput v-model="vendorForm.name" placeholder="Vendor / Karigar name" /></UFormField>
              <UFormField label="Mobile"><UInput v-model="vendorForm.mobileNumber" placeholder="Mobile number" /></UFormField>
              <UFormField label="City"><UInput v-model="vendorForm.city" placeholder="City" /></UFormField>
              <UFormField label="Email"><UInput v-model="vendorForm.email" placeholder="Optional" /></UFormField>
              <UFormField class="md:col-span-2" label="Address"><UTextarea v-model="vendorForm.address" placeholder="Workshop / home address" /></UFormField>
              <UFormField label="GSTIN"><UInput v-model="vendorForm.gstin" placeholder="Optional" /></UFormField>
              <UCheckbox v-model="vendorForm.active" label="Active vendor" />
            </div>
            <UButton class="mt-4" icon="i-lucide-user-plus" :loading="saving" @click="saveTailoringVendor">Add vendor</UButton>
          </UCard>

          <UCard>
            <template #header>Vendor-specific service rate</template>
            <div class="grid gap-3 md:grid-cols-2">
              <UFormField label="Store"><USelect v-model="vendorRateForm.storeId" :items="storeOptions" /></UFormField>
              <UFormField label="Vendor"><USelect v-model="vendorRateForm.vendorId" :items="tailoringVendorOptions" placeholder="Select vendor" /></UFormField>
              <UFormField label="Service item"><USelect v-model="vendorRateForm.serviceItemId" :items="allServiceOptions" placeholder="Select service" @update:model-value="seedVendorRateFromService" /></UFormField>
              <UFormField label="Effective from"><UInput v-model="vendorRateForm.effectiveFrom" type="date" /></UFormField>
              <UFormField label="Customer charge"><UInput v-model="vendorRateForm.customerRate" type="number" /></UFormField>
              <UFormField label="Vendor work rate"><UInput v-model="vendorRateForm.vendorRate" type="number" /></UFormField>
              <UFormField class="md:col-span-2" label="Remarks"><UTextarea v-model="vendorRateForm.remarks" placeholder="Special rate condition, fabric type, urgent job rate etc." /></UFormField>
              <UCheckbox v-model="vendorRateForm.active" label="Active rate" />
            </div>
            <UButton class="mt-4" icon="i-lucide-save" :loading="saving" @click="saveVendorRate">Save vendor rate</UButton>
          </UCard>
        </div>

        <UCard>
          <template #header>Vendor rate matrix</template>
          <div class="space-y-2">
            <div v-for="rate in vendorRates" :key="rate.id" class="rounded-lg border border-default p-3">
              <div class="flex items-start justify-between gap-3">
                <div>
                  <p class="font-semibold">{{ rate.vendorName }} · {{ rate.serviceName }}</p>
                  <p class="text-xs text-muted">{{ categoryText(rate.category) }} · Effective {{ dateText(rate.effectiveFrom) }} · {{ rate.active ? 'Active' : 'Inactive' }}</p>
                </div>
                <UBadge color="primary" variant="subtle">Vendor {{ money(rate.vendorRate) }}</UBadge>
              </div>
              <div class="mt-2 grid gap-2 text-sm md:grid-cols-3">
                <span>Customer charge <b>{{ money(rate.customerRate) }}</b></span>
                <span>Vendor rate <b>{{ money(rate.vendorRate) }}</b></span>
                <span>Margin <b>{{ money(Number(rate.customerRate || 0) - Number(rate.vendorRate || 0)) }}</b></span>
              </div>
              <p v-if="rate.remarks" class="mt-2 text-xs text-muted">{{ rate.remarks }}</p>
            </div>
          </div>
        </UCard>
      </section>

      <USlideover :open="!!selectedOrder" @update:open="(value) => { if (!value) selectedOrder = null }">
        <template #content>
          <div class="space-y-4 p-4">
            <h3 class="text-lg font-semibold">{{ selectedOrder?.order?.orderNumber }} history</h3>
            <div v-for="entry in selectedOrder?.history || []" :key="entry.id" class="rounded border border-default p-3 text-sm"><p class="font-medium">{{ entry.action }}</p><p class="text-muted">{{ dateText(entry.eventDate) }} · {{ entry.actor || 'System' }}</p><p>{{ entry.remarks }}</p></div>
          </div>
        </template>
      </USlideover>
    </div>
  </AppShell>
</template>
