<script setup lang="ts">
import { h, resolveComponent } from 'vue'
import type { TableColumn } from '@nuxt/ui'

const api = useGarmetixApi()
const auth = useAuth()
const workspace = useWorkspace()
const feedback = useUiFeedback()
const documentPrint = useServerDocumentPrint()
const route = useRoute()
const router = useRouter()

const isAuthenticated = auth.isAuthenticated
const canEdit = auth.canEdit
const canDelete = auth.canDelete
const UBadge = resolveComponent('UBadge')
const UButton = resolveComponent('UButton')

const loading = ref(false)
const saving = ref(false)
const deleting = ref(false)
const downloading = ref(false)
const loadError = ref('')
const reportError = ref('')
const companies = ref<any[]>([])
const stores = ref<any[]>([])
const options = ref<any>({ stocks: [], vendors: [], customers: [], recentDocuments: [] })
const report = ref<any | null>(null)
const search = ref('')
const typeFilter = ref('All')
const paymentFilter = ref('All')
const activeView = ref('register')
const formOpen = ref(false)
const deleteOpen = ref(false)
const printOpen = ref(false)
const pendingDelete = ref<any | null>(null)
const selectedPrint = ref<any | null>(null)
const editMode = ref<'create' | 'edit'>('create')
const printFormat = ref<'a4' | 'a5'>('a4')
const isReprint = ref(false)

const reportForm = reactive({
  from: localDateValue(new Date(Date.now() - 30 * 86400000)),
  to: localDateValue()
})
const form = reactive<any>(emptyForm('Sale'))
const lines = ref<any[]>([emptySaleLine()])

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
const typeFilterOptions = ['All', 'Sale', 'Purchase']
const paymentFilterOptions = ['All', 'Paid', 'Partially Paid', 'Due']
const viewItems = [
  { label: 'Document Register', value: 'register', icon: 'i-lucide-list' },
  { label: 'Off Book Stock', value: 'stock', icon: 'i-lucide-package-open' }
]

const storeOptions = computed(() => stores.value.map(item => ({ label: item.name, value: item.id })))
const vendorOptions = computed(() => (options.value.vendors || []).map((item: any) => ({ label: item.name, value: item.id })))
const customerOptions = computed(() => (options.value.customers || []).map((item: any) => ({ label: item.name, value: item.id })))
const stockOptions = computed(() => (options.value.stocks || [])
  .filter((item: any) => !form.storeId || item.storeId === form.storeId)
  .map((item: any) => ({ label: item.label, value: item.stockId })))
const stockMap = computed(() => new Map((options.value.stocks || []).map((item: any) => [item.stockId, item])))

const totals = computed(() => {
  const quantity = lines.value.reduce((sum, line) => sum + Number(line.quantity || 0), 0)
  const gross = lines.value.reduce((sum, line) => sum + Number(line.quantity || 0) * Number(line.rate || 0), 0)
  const discount = lines.value.reduce((sum, line) => sum + Number(line.discountAmount || 0), 0)
  return { quantity, gross, discount, net: Math.max(0, gross - discount) }
})

const registerRows = computed(() => (report.value?.rows || []).map(formatDocumentRow))
const filteredRows = computed(() => {
  const term = search.value.trim().toLowerCase()
  return registerRows.value.filter((row: any) => {
    const matchesSearch = !term || [
      row.documentNumber,
      row.partyName,
      row.remarks,
      row.documentType,
      row.paymentStatus
    ].some(value => String(value || '').toLowerCase().includes(term))
    return matchesSearch
      && (typeFilter.value === 'All' || row.documentType === typeFilter.value)
      && (paymentFilter.value === 'All' || row.paymentStatus === paymentFilter.value)
  })
})
const stockRows = computed(() => (report.value?.currentStockRows || []).map((item: any) => ({
  ...item,
  purchaseText: number(item.purchaseQty),
  soldText: number(item.soldQty),
  currentText: number(item.currentStock),
  costText: money(item.costPrice),
  mrpText: money(item.mrp),
  valueText: money(item.stockValue)
})))
const metrics = computed(() => [
  {
    label: 'Off Book Sales',
    value: money(report.value?.saleAmount),
    meta: `${report.value?.saleCount || 0} sale memo(s)`,
    icon: 'i-lucide-shopping-bag',
    color: 'success'
  },
  {
    label: 'Off Book Purchases',
    value: money(report.value?.purchaseAmount),
    meta: `${report.value?.purchaseCount || 0} purchase memo(s)`,
    icon: 'i-lucide-package-plus',
    color: 'warning'
  },
  {
    label: 'Off Book Result',
    value: money(report.value?.grossProfit),
    meta: 'Sale value less item cost',
    icon: 'i-lucide-chart-no-axes-combined',
    color: 'primary'
  },
  {
    label: 'Off Book Stock',
    value: money(report.value?.currentStockValue),
    meta: `${number(report.value?.currentStockQty)} quantity`,
    icon: 'i-lucide-warehouse',
    color: 'neutral'
  }
])

const documentColumns: TableColumn<any>[] = [
  { accessorKey: 'onDateText', header: 'Date' },
  { accessorKey: 'documentNumber', header: 'Document' },
  {
    accessorKey: 'documentType',
    header: 'Type',
    cell: ({ row }) => h(UBadge, {
      color: row.original.documentType === 'Sale' ? 'success' : 'warning',
      variant: 'subtle'
    }, () => row.original.documentType)
  },
  { accessorKey: 'partyName', header: 'Party' },
  { accessorKey: 'quantityText', header: 'Qty' },
  { accessorKey: 'netText', header: 'Net' },
  { accessorKey: 'paidText', header: 'Paid' },
  { accessorKey: 'balanceText', header: 'Balance' },
  {
    accessorKey: 'paymentStatus',
    header: 'Status',
    cell: ({ row }) => h(UBadge, {
      color: row.original.paymentStatus === 'Paid'
        ? 'success'
        : row.original.paymentStatus === 'Due' ? 'error' : 'warning',
      variant: 'subtle'
    }, () => row.original.paymentStatus)
  },
  {
    id: 'actions',
    header: '',
    cell: ({ row }) => h('div', { class: 'table-action-buttons' }, [
      h(UButton, {
        color: 'neutral',
        variant: 'ghost',
        icon: 'i-lucide-printer',
        label: 'Print',
        onClick: () => openPrint(row.original)
      }),
      canEdit.value ? h(UButton, {
        color: 'neutral',
        variant: 'ghost',
        icon: 'i-lucide-pencil',
        label: 'Edit',
        onClick: () => startEdit(row.original.id)
      }) : null,
      canDelete.value ? h(UButton, {
        color: 'error',
        variant: 'ghost',
        icon: 'i-lucide-trash-2',
        label: 'Delete',
        onClick: () => askDelete(row.original)
      }) : null
    ].filter(Boolean))
  }
]

const stockColumns: TableColumn<any>[] = [
  { accessorKey: 'productName', header: 'Product' },
  { accessorKey: 'barcode', header: 'Barcode' },
  { accessorKey: 'storeName', header: 'Store' },
  { accessorKey: 'purchaseText', header: 'Purchased' },
  { accessorKey: 'soldText', header: 'Sold' },
  { accessorKey: 'currentText', header: 'Current' },
  { accessorKey: 'costText', header: 'Cost' },
  { accessorKey: 'mrpText', header: 'MRP' },
  { accessorKey: 'valueText', header: 'Value' }
]

function emptyForm(type: 'Sale' | 'Purchase') {
  return {
    id: '',
    documentNumber: '',
    documentType: type,
    onDate: localDateValue(),
    storeId: workspace.storeId.value || '',
    partyId: '',
    partyName: '',
    paymentMode: 0,
    referenceNumber: '',
    paidInFull: true,
    paidAmount: 0,
    remarks: ''
  }
}

function emptyPurchaseLine() {
  return {
    stockId: '',
    productName: '',
    barcode: '',
    quantity: 1,
    rate: 0,
    discountAmount: 0,
    costPrice: 0,
    mrp: 0
  }
}

function emptySaleLine() {
  return { stockId: '', quantity: 1, rate: 0, discountAmount: 0 }
}

async function refresh() {
  if (!auth.isAuthenticated.value) return
  loading.value = true
  loadError.value = ''
  try {
    const [companyRows, storeRows, optionRows] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores'),
      api.get<any>('non-gst-goods/options')
    ])
    companies.value = companyRows
    stores.value = storeRows
    options.value = optionRows
    await loadReport()
  } catch (error) {
    loadError.value = 'The Off Book goods register could not be loaded. Check the selected workspace and try again.'
    feedback.failed('Off Book goods refresh failed', error)
  } finally {
    loading.value = false
  }
}

async function loadReport() {
  reportError.value = ''
  try {
    report.value = await api.get<any>(`non-gst-goods/report?from=${reportForm.from}&to=${reportForm.to}`)
  } catch (error) {
    reportError.value = 'The selected Off Book report period could not be loaded.'
    feedback.failed('Off Book report failed', error)
  }
}

function startCreate(type: 'Sale' | 'Purchase') {
  editMode.value = 'create'
  Object.assign(form, emptyForm(type))
  form.storeId = workspace.storeId.value || stores.value[0]?.id || ''
  lines.value = [type === 'Sale' ? emptySaleLine() : emptyPurchaseLine()]
  formOpen.value = true
}

async function startEdit(id: string) {
  try {
    const document = await api.get<any>(`non-gst-goods/documents/${id}`)
    const type = normalizeDocumentType(document.documentType)
    editMode.value = 'edit'
    Object.assign(form, {
      ...emptyForm(type),
      id: document.id,
      documentNumber: document.documentNumber,
      documentType: type,
      onDate: dateInputValue(document.onDate),
      storeId: document.storeId,
      partyId: type === 'Purchase' ? normalizeGuid(document.vendorId) : normalizeGuid(document.customerId),
      partyName: document.partyName || '',
      paymentMode: Number(document.paymentMode || 0),
      referenceNumber: document.referenceNumber || '',
      paidInFull: Number(document.balanceAmount || 0) <= 0,
      paidAmount: Number(document.paidAmount || 0),
      remarks: document.remarks || ''
    })
    lines.value = (document.items || []).map((item: any) => type === 'Sale'
      ? {
          stockId: item.stockId,
          quantity: Number(item.quantity || 0),
          rate: Number(item.rate || 0),
          discountAmount: Number(item.discountAmount || 0)
        }
      : {
          stockId: item.stockId,
          productName: item.productName || '',
          barcode: item.barcode || '',
          quantity: Number(item.quantity || 0),
          rate: Number(item.rate || 0),
          discountAmount: Number(item.discountAmount || 0),
          costPrice: Number(item.costRate || item.rate || 0),
          mrp: Number(item.rate || 0)
        })
    if (!lines.value.length) lines.value = [type === 'Sale' ? emptySaleLine() : emptyPurchaseLine()]
    formOpen.value = true
  } catch (error) {
    feedback.failed('Could not open Off Book document', error)
  }
}

function buildPayload() {
  if (!form.storeId) throw new Error('Select a store.')
  if (!lines.value.length) throw new Error('Add at least one item.')
  if (totals.value.net < 0) throw new Error('Document total cannot be negative.')

  const items = lines.value.map((line) => {
    if (Number(line.quantity || 0) <= 0) throw new Error('Every item quantity must be greater than zero.')
    if (Number(line.rate || 0) < 0) throw new Error('Item rate cannot be negative.')
    if (form.documentType === 'Sale' && !line.stockId) throw new Error('Select an Off Book stock item for every sale line.')
    if (form.documentType === 'Purchase' && !String(line.productName || '').trim()) throw new Error('Enter a product name for every purchase line.')

    return {
      stockId: normalizeGuid(line.stockId) || null,
      productName: form.documentType === 'Purchase' ? String(line.productName || '').trim() : null,
      barcode: form.documentType === 'Purchase' ? nullIfEmpty(line.barcode) : null,
      quantity: Number(line.quantity || 0),
      rate: Number(line.rate || 0),
      discountAmount: Number(line.discountAmount || 0),
      costPrice: form.documentType === 'Purchase' ? Number(line.costPrice || line.rate || 0) : null,
      mrp: form.documentType === 'Purchase' ? Number(line.mrp || line.rate || 0) : null,
      storeId: form.storeId
    }
  })

  return {
    onDate: `${form.onDate}T00:00:00`,
    vendorId: form.documentType === 'Purchase' ? normalizeGuid(form.partyId) || null : null,
    customerId: form.documentType === 'Sale' ? normalizeGuid(form.partyId) || null : null,
    partyName: nullIfEmpty(form.partyName),
    paymentMode: Number(form.paymentMode || 0),
    referenceNumber: nullIfEmpty(form.referenceNumber),
    paidAmount: form.paidInFull ? totals.value.net : Number(form.paidAmount || 0),
    remarks: nullIfEmpty(form.remarks),
    items
  }
}

async function saveDocument() {
  saving.value = true
  try {
    const payload = buildPayload()
    const response = editMode.value === 'edit'
      ? await api.update<any>('non-gst-goods/documents', form.id, payload)
      : await api.create<any>(
          form.documentType === 'Purchase' ? 'non-gst-goods/purchase' : 'non-gst-goods/sale',
          payload
        )
    feedback.notify(
      editMode.value === 'edit' ? 'Off Book document updated' : 'Off Book document saved',
      response?.documentNumber,
      'success'
    )
    formOpen.value = false
    await refresh()
    if (editMode.value === 'create' && response?.id) {
      selectedPrint.value = response
      await printDocument()
    }
  } catch (error) {
    feedback.failed('Could not save Off Book document', error)
  } finally {
    saving.value = false
  }
}

function addLine() {
  lines.value.push(form.documentType === 'Sale' ? emptySaleLine() : emptyPurchaseLine())
}

function removeLine(index: number) {
  if (lines.value.length > 1) lines.value.splice(index, 1)
}

function selectSaleStock(line: any) {
  const stock: any = stockMap.value.get(line.stockId)
  if (!stock) return
  form.storeId = stock.storeId
  line.rate = Number(stock.mrp || 0)
}

function askDelete(document: any) {
  pendingDelete.value = document
  deleteOpen.value = true
}

async function confirmDelete() {
  if (!pendingDelete.value?.id) return
  deleting.value = true
  try {
    await api.remove('non-gst-goods/documents', pendingDelete.value.id)
    feedback.deleted('Off Book document')
    deleteOpen.value = false
    pendingDelete.value = null
    await refresh()
  } catch (error) {
    feedback.failed('Could not delete Off Book document', error)
  } finally {
    deleting.value = false
  }
}

function openPrint(document: any) {
  selectedPrint.value = document
  printFormat.value = 'a4'
  isReprint.value = true
  printOpen.value = true
}

async function printDocument() {
  if (!selectedPrint.value?.id) return
  try {
    await documentPrint.printPdf(`non-gst-goods/documents/${selectedPrint.value.id}/pdf?format=${printFormat.value}&reprint=${isReprint.value}`)
  } catch (error) {
    feedback.failed('Could not print Off Book PDF', error)
  }
}

async function downloadDocument() {
  if (!selectedPrint.value?.id) return
  downloading.value = true
  try {
    await documentPrint.downloadPdf(
      `non-gst-goods/documents/${selectedPrint.value.id}/pdf?format=${printFormat.value}&reprint=${isReprint.value}`,
      `${selectedPrint.value.documentNumber || 'off-book-document'}.pdf`
    )
    feedback.notify('Off Book PDF downloaded')
  } catch (error) {
    feedback.failed('Could not download Off Book PDF', error)
  } finally {
    downloading.value = false
  }
}

function formatDocumentRow(item: any) {
  return {
    ...item,
    onDateText: formatDate(item.onDate),
    quantityText: number(item.quantity),
    netText: money(item.netAmount),
    paidText: money(item.paidAmount),
    balanceText: money(item.balanceAmount),
    profitText: money(item.profitAmount)
  }
}

function normalizeDocumentType(value: unknown): 'Sale' | 'Purchase' {
  return value === 'Purchase' || Number(value) === 0 ? 'Purchase' : 'Sale'
}

function normalizeGuid(value: unknown) {
  const text = String(value || '').trim()
  return text && text !== '00000000-0000-0000-0000-000000000000' ? text : ''
}

function nullIfEmpty(value: unknown) {
  const text = String(value || '').trim()
  return text || null
}

function localDateValue(date = new Date()) {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

function dateInputValue(value: unknown) {
  const text = String(value || '')
  return /^\d{4}-\d{2}-\d{2}/.test(text) ? text.slice(0, 10) : localDateValue()
}

function formatDate(value: unknown) {
  const [year, month, day] = dateInputValue(value).split('-').map(Number)
  return new Date(year, month - 1, day).toLocaleDateString('en-IN')
}

function number(value: unknown) {
  return Number(value || 0).toLocaleString('en-IN', { maximumFractionDigits: 2 })
}

function money(value: unknown) {
  return new Intl.NumberFormat('en-IN', {
    style: 'currency',
    currency: 'INR',
    maximumFractionDigits: 2
  }).format(Number(value || 0))
}

onMounted(async () => {
  auth.restore()
  await refresh()
  const documentId = String(route.query.documentId || '')
  if (documentId) {
    await startEdit(documentId)
    await router.replace({ query: { ...route.query, documentId: undefined } })
  }
})
</script>

<template>
  <AuthScreen v-if="!isAuthenticated" @authenticated="refresh" />

  <AppShell
    v-else
    title="Non-GST Goods"
    :companies="companies"
    :stores="stores"
    @refresh="refresh"
    @workspace-change="refresh"
  >
    <section class="planner-dashboard">
      <UiModulePageHeader
        title="Off Book Sale and Purchase"
        description="Maintain an independent Non-GST goods register, stock, settlement, print, and QR trail."
        icon="i-lucide-book-lock"
        primary-label="New Sale"
        primary-icon="i-lucide-shopping-bag"
        @primary="startCreate('Sale')"
      >
        <template #actions>
          <UBadge color="warning" variant="subtle" icon="i-lucide-book-lock">Off Book</UBadge>
          <UButton
            icon="i-lucide-package-plus"
            color="neutral"
            variant="subtle"
            label="New Purchase"
            :disabled="!canEdit"
            @click="startCreate('Purchase')"
          />
          <UButton
            icon="i-lucide-shopping-bag"
            label="New Sale"
            :disabled="!canEdit"
            @click="startCreate('Sale')"
          />
        </template>
      </UiModulePageHeader>

      <UAlert
        color="warning"
        variant="subtle"
        icon="i-lucide-shield-alert"
        title="Completely separate from regular books"
        description="These sales, purchases, payments, receipts, stock movements, and results do not create regular invoices, purchase bills, GST rows, ledgers, journals, vouchers, or bank transactions."
      />

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
        <div class="off-book-report-bar">
          <UTabs v-model="activeView" :items="viewItems" />
          <div class="off-book-period">
            <UFormField label="From"><UInput v-model="reportForm.from" type="date" /></UFormField>
            <UFormField label="To"><UInput v-model="reportForm.to" type="date" /></UFormField>
            <UButton icon="i-lucide-search" label="Load" :loading="loading" @click="loadReport" />
          </div>
        </div>
        <UAlert
          v-if="reportError"
          class="mt-4"
          color="error"
          variant="subtle"
          title="Report unavailable"
          :description="reportError"
        />
      </UCard>

      <UiRegisterPanel
        v-if="activeView === 'register'"
        title="Off Book Document Register"
        description="Search and manage independent sale and purchase memos."
        :loading="loading"
        :error="loadError"
        :empty="!filteredRows.length"
        empty-title="No Off Book documents found"
        empty-description="Create a sale or purchase memo, or change the period and filters."
        empty-icon="i-lucide-book-lock"
        @retry="refresh"
      >
        <template #actions>
          <UBadge color="neutral" variant="subtle">{{ filteredRows.length }} shown</UBadge>
        </template>
        <UiCrudToolbar
          v-model:search="search"
          search-placeholder="Search document, party, type, status"
          :loading="loading"
          refresh-label="Sync"
          create-label="New Sale"
          @refresh="refresh"
          @create="startCreate('Sale')"
        >
          <template #filters>
            <USelect v-model="typeFilter" :items="typeFilterOptions" aria-label="Filter document type" class="min-w-36" />
            <USelect v-model="paymentFilter" :items="paymentFilterOptions" aria-label="Filter payment status" class="min-w-44" />
            <UButton icon="i-lucide-package-plus" color="neutral" variant="subtle" label="Purchase" @click="startCreate('Purchase')" />
          </template>
        </UiCrudToolbar>
        <UTable v-if="filteredRows.length" :data="filteredRows" :columns="documentColumns" :loading="loading" />
      </UiRegisterPanel>

      <UiRegisterPanel
        v-else
        title="Independent Off Book Stock"
        description="Only stock created and consumed by this module is shown here."
        :loading="loading"
        :error="loadError"
        :empty="!stockRows.length"
        empty-title="No Off Book stock"
        empty-description="Post an Off Book purchase to create independent stock."
        empty-icon="i-lucide-package-open"
        @retry="refresh"
      >
        <template #actions>
          <UBadge color="warning" variant="subtle">{{ stockRows.length }} stock row(s)</UBadge>
        </template>
        <UTable v-if="stockRows.length" :data="stockRows" :columns="stockColumns" :loading="loading" />
      </UiRegisterPanel>

      <UiFormSlideover
        v-model:open="formOpen"
        :title="`${editMode === 'create' ? 'New' : 'Edit'} Off Book ${form.documentType}`"
        description="This document and its stock remain outside the regular accounting and GST system."
        :submit-label="editMode === 'create' ? `Save ${form.documentType}` : `Update ${form.documentType}`"
        layout="modal"
        content-class="w-[calc(100vw-1rem)] sm:max-w-6xl"
        body-class="overflow-y-auto max-h-[calc(100vh-12rem)]"
        :loading="saving"
        :submit-disabled="!canEdit"
        @submit="saveDocument"
      >
        <div class="form-two-column">
          <UFormField label="Date" required><UInput v-model="form.onDate" type="date" class="w-full" required /></UFormField>
          <UFormField label="Store" required><USelectMenu v-model="form.storeId" :items="storeOptions" value-key="value" class="w-full" /></UFormField>
        </div>
        <div class="form-two-column">
          <UFormField :label="form.documentType === 'Purchase' ? 'Vendor' : 'Customer'">
            <USelectMenu
              v-model="form.partyId"
              :items="form.documentType === 'Purchase' ? vendorOptions : customerOptions"
              value-key="value"
              class="w-full"
            />
          </UFormField>
          <UFormField label="Party name"><UInput v-model="form.partyName" class="w-full" placeholder="Walk-in or alternate party name" /></UFormField>
        </div>
        <div class="form-two-column">
          <UFormField label="Payment mode"><USelect v-model="form.paymentMode" :items="paymentModeOptions" class="w-full" /></UFormField>
          <UFormField label="Reference"><UInput v-model="form.referenceNumber" class="w-full" /></UFormField>
        </div>

        <div class="off-book-lines-header">
          <div>
            <h3>Items</h3>
            <p>{{ form.documentType === 'Purchase' ? 'Purchase creates independent Off Book stock.' : 'Sale consumes only independent Off Book stock.' }}</p>
          </div>
          <UButton icon="i-lucide-plus" color="neutral" variant="subtle" label="Add Item" @click="addLine" />
        </div>

        <div class="planner-table-wrap">
          <table class="off-book-lines">
            <thead>
              <tr v-if="form.documentType === 'Sale'">
                <th>Off Book stock</th><th>Qty</th><th>Rate</th><th>Discount</th><th>Amount</th><th />
              </tr>
              <tr v-else>
                <th>Product</th><th>Barcode</th><th>Qty</th><th>Rate</th><th>Discount</th><th>Cost</th><th>MRP</th><th>Amount</th><th />
              </tr>
            </thead>
            <tbody>
              <tr v-for="(line, index) in lines" :key="index">
                <template v-if="form.documentType === 'Sale'">
                  <td><USelectMenu v-model="line.stockId" :items="stockOptions" value-key="value" class="min-w-80" @update:model-value="selectSaleStock(line)" /></td>
                  <td><UInput v-model.number="line.quantity" type="number" min="0.01" step="0.01" /></td>
                  <td><UInput v-model.number="line.rate" type="number" min="0" step="0.01" /></td>
                  <td><UInput v-model.number="line.discountAmount" type="number" min="0" step="0.01" /></td>
                </template>
                <template v-else>
                  <td><UInput v-model="line.productName" class="min-w-48" /></td>
                  <td><UInput v-model="line.barcode" class="min-w-40" placeholder="Auto if blank" /></td>
                  <td><UInput v-model.number="line.quantity" type="number" min="0.01" step="0.01" /></td>
                  <td><UInput v-model.number="line.rate" type="number" min="0" step="0.01" /></td>
                  <td><UInput v-model.number="line.discountAmount" type="number" min="0" step="0.01" /></td>
                  <td><UInput v-model.number="line.costPrice" type="number" min="0" step="0.01" /></td>
                  <td><UInput v-model.number="line.mrp" type="number" min="0" step="0.01" /></td>
                </template>
                <td class="amount-cell">{{ money(Number(line.quantity || 0) * Number(line.rate || 0) - Number(line.discountAmount || 0)) }}</td>
                <td><UButton icon="i-lucide-trash-2" color="error" variant="ghost" :disabled="lines.length === 1" @click="removeLine(index)" /></td>
              </tr>
            </tbody>
          </table>
        </div>

        <div class="off-book-total-bar">
          <span>Qty <strong>{{ number(totals.quantity) }}</strong></span>
          <span>Gross <strong>{{ money(totals.gross) }}</strong></span>
          <span>Discount <strong>{{ money(totals.discount) }}</strong></span>
          <span>Net <strong>{{ money(totals.net) }}</strong></span>
        </div>

        <div class="form-two-column">
          <UFormField label="Settlement">
            <USwitch v-model="form.paidInFull" label="Paid in full" />
          </UFormField>
          <UFormField v-if="!form.paidInFull" label="Paid amount">
            <UInput v-model.number="form.paidAmount" type="number" min="0" :max="totals.net" step="0.01" />
          </UFormField>
        </div>
        <UFormField label="Remarks"><UTextarea v-model="form.remarks" :rows="3" autoresize /></UFormField>
      </UiFormSlideover>

      <UiConfirmDeleteModal
        v-model:open="deleteOpen"
        title="Delete Off Book Document"
        :description="`Delete ${pendingDelete?.documentNumber || 'this document'} and reverse its independent stock movement?`"
        :loading="deleting"
        @confirm="confirmDelete"
      />

      <UModal v-model:open="printOpen" title="Off Book Document PDF">
        <template #body>
          <div class="space-y-4">
            <UAlert
              color="warning"
              variant="subtle"
              icon="i-lucide-qr-code"
              title="Printable independent document"
              description="The server PDF includes every item, paid and balance amounts, an Off Book notice, and a QR code that opens this record."
            />
            <div class="form-two-column">
              <UFormField label="Paper size">
                <USelect v-model="printFormat" :items="[{ label: 'A4', value: 'a4' }, { label: 'A5', value: 'a5' }]" />
              </UFormField>
              <UFormField label="Copy status"><USwitch v-model="isReprint" label="Mark as reprint" /></UFormField>
            </div>
          </div>
        </template>
        <template #footer>
          <div class="modal-actions">
            <UButton color="neutral" variant="outline" label="Close" @click="printOpen = false" />
            <UButton icon="i-lucide-download" color="neutral" variant="subtle" label="Download" :loading="downloading" @click="downloadDocument" />
            <UButton icon="i-lucide-printer" label="Print" @click="printDocument" />
          </div>
        </template>
      </UModal>
    </section>
  </AppShell>
</template>

<style scoped>
.off-book-report-bar,
.off-book-lines-header,
.off-book-total-bar {
  display: flex;
  align-items: end;
  justify-content: space-between;
  gap: 16px;
}

.off-book-period {
  display: grid;
  grid-template-columns: minmax(150px, 1fr) minmax(150px, 1fr) auto;
  align-items: end;
  gap: 10px;
}

.off-book-lines-header {
  margin-top: 8px;
}

.off-book-lines-header h3 {
  margin: 0;
  font-size: 15px;
}

.off-book-lines-header p {
  margin: 3px 0 0;
  color: var(--muted);
  font-size: 12px;
}

.off-book-lines {
  width: 100%;
  min-width: 880px;
  border-collapse: collapse;
}

.off-book-lines th,
.off-book-lines td {
  padding: 8px;
  border-bottom: 1px solid var(--border);
  text-align: left;
  vertical-align: middle;
}

.off-book-lines th {
  color: var(--muted);
  font-size: 12px;
}

.amount-cell {
  min-width: 120px;
  font-weight: 700;
  white-space: nowrap;
}

.off-book-total-bar {
  justify-content: flex-end;
  flex-wrap: wrap;
  padding: 12px;
  background: color-mix(in srgb, var(--ui-primary) 8%, transparent);
  border: 1px solid color-mix(in srgb, var(--ui-primary) 24%, var(--border));
  border-radius: 6px;
}

.off-book-total-bar span {
  min-width: 125px;
  color: var(--muted);
  font-size: 12px;
}

.off-book-total-bar strong {
  display: block;
  margin-top: 2px;
  color: var(--text);
  font-size: 14px;
}

@media (max-width: 760px) {
  .off-book-report-bar {
    align-items: stretch;
    flex-direction: column;
  }

  .off-book-period {
    grid-template-columns: 1fr 1fr;
  }

  .off-book-period > :last-child {
    grid-column: 1 / -1;
  }
}
</style>
