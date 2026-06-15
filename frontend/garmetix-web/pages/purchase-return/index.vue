<script setup lang="ts">
import { h, resolveComponent } from 'vue'
import type { TableColumn } from '@nuxt/ui'

const api = useGarmetixApi()
const auth = useAuth()
const feedback = useUiFeedback()
const isAuthenticated = auth.isAuthenticated

const UBadge = resolveComponent('UBadge')
const UButton = resolveComponent('UButton')

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const invoices = ref<any[]>([])
const purchaseReturns = ref<any[]>([])
const loading = ref(false)
const loadError = ref('')
const saving = ref(false)
const returnLoading = ref(false)
const search = ref('')
const returnSearch = ref('')
const returnOpen = ref(false)
const detailOpen = ref(false)
const detailLoading = ref(false)
const pendingInvoice = ref<any | null>(null)
const returnInvoice = ref<any | null>(null)
const selectedReturn = ref<any | null>(null)
const reason = ref('Partial purchase return')
const returnDate = ref(localDateInput())
const returnRows = ref<any[]>([])

const filteredInvoices = computed(() => {
  const term = search.value.trim().toLowerCase()
  return invoices.value
    .filter((invoice) => !['Cancelled', 'Refunded'].includes(String(invoice.invoiceStatus || '')))
    .filter((invoice) => {
      if (!term) return true
      return [invoice.invoiceNumber, invoice.inwardNumber, invoice.vendorName, invoice.invoiceStatus]
        .some((value) => String(value || '').toLowerCase().includes(term))
    })
})

const filteredReturns = computed(() => {
  const term = returnSearch.value.trim().toLowerCase()
  if (!term) return purchaseReturns.value
  return purchaseReturns.value.filter((item) => [
    item.returnNumber,
    item.originalInvoiceNumber,
    item.vendorName,
    item.debitNoteNumber,
    item.returnKind,
    item.status
  ].some((value) => String(value || '').toLowerCase().includes(term)))
})

const selectedReturnRows = computed(() => returnRows.value
  .map((row) => ({ ...row, returnQuantity: decimal(row.returnQuantity) }))
  .filter((row) => row.returnQuantity > 0))

const tableRows = computed(() => filteredInvoices.value.map((invoice) => ({
  id: invoice.id,
  invoiceNumber: invoice.invoiceNumber || '-',
  inwardNumber: invoice.inwardNumber || '-',
  onDate: formatDate(invoice.onDate || invoice.inwardDate),
  vendorName: invoice.vendorName || 'Vendor',
  billAmount: money(invoice.billAmount || invoice.totalAmount || invoice.netAmount),
  balanceAmount: money(invoice.balanceAmount || 0),
  invoiceStatus: invoice.invoiceStatus || 'Saved',
  raw: invoice
})))

const returnHistoryRows = computed(() => filteredReturns.value.map((item) => ({
  id: item.id,
  returnNumber: item.returnNumber || '-',
  onDate: formatDate(item.onDate),
  originalInvoiceNumber: item.originalInvoiceNumber || '-',
  vendorName: item.vendorName || 'Vendor',
  returnKind: item.returnKind || 'Partial',
  quantity: decimal(item.quantity).toFixed(2),
  returnAmount: money(item.returnAmount),
  debitNoteNumber: item.debitNoteNumber || '-',
  status: item.status || 'Posted',
  raw: item
})))

const columns: TableColumn<any>[] = [
  { accessorKey: 'invoiceNumber', header: 'Invoice' },
  { accessorKey: 'inwardNumber', header: 'Inward' },
  { accessorKey: 'onDate', header: 'Date' },
  { accessorKey: 'vendorName', header: 'Vendor' },
  { accessorKey: 'billAmount', header: 'Amount' },
  { accessorKey: 'balanceAmount', header: 'Balance' },
  {
    accessorKey: 'invoiceStatus',
    header: 'Status',
    cell: ({ row }) => h(UBadge, { color: 'success', variant: 'subtle' }, () => row.original.invoiceStatus)
  },
  {
    id: 'actions',
    header: '',
    cell: ({ row }) => h(UButton, {
      icon: 'i-lucide-undo-2',
      color: 'warning',
      variant: 'subtle',
      label: 'Return Items',
      onClick: () => openReturn(row.original.raw)
    })
  }
]

const returnHistoryColumns: TableColumn<any>[] = [
  { accessorKey: 'returnNumber', header: 'Return No.' },
  { accessorKey: 'onDate', header: 'Date' },
  { accessorKey: 'originalInvoiceNumber', header: 'Purchase Invoice' },
  { accessorKey: 'vendorName', header: 'Vendor' },
  { accessorKey: 'returnKind', header: 'Type' },
  { accessorKey: 'quantity', header: 'Qty' },
  { accessorKey: 'returnAmount', header: 'Amount' },
  { accessorKey: 'debitNoteNumber', header: 'Debit Note' },
  {
    accessorKey: 'status',
    header: 'Status',
    cell: ({ row }) => h(UBadge, { color: 'success', variant: 'subtle' }, () => row.original.status)
  },
  {
    id: 'actions',
    header: '',
    cell: ({ row }) => h(UButton, {
      icon: 'i-lucide-eye',
      color: 'neutral',
      variant: 'ghost',
      'aria-label': `View ${row.original.returnNumber}`,
      onClick: () => openReturnDetail(row.original.raw)
    })
  }
]

const returnSummary = computed(() => {
  return selectedReturnRows.value.reduce((summary, row) => {
    const quantity = Math.min(row.returnQuantity, decimal(row.returnableQuantity))
    summary.quantity += quantity
    summary.taxable += quantity * decimal(row.unitTaxableAmount)
    summary.tax += quantity * decimal(row.unitTaxAmount)
    summary.amount += quantity * decimal(row.unitAmount)
    return summary
  }, { quantity: 0, taxable: 0, tax: 0, amount: 0 })
})

async function refresh() {
  if (!auth.isAuthenticated.value) return
  loading.value = true
  loadError.value = ''
  try {
    const [companyRows, storeRows, invoiceRows, returnRows] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores'),
      api.get<any[]>('purchase/invoices/recent'),
      api.get<any[]>('purchase/returns/recent')
    ])
    companies.value = companyRows
    stores.value = storeRows
    invoices.value = invoiceRows
    purchaseReturns.value = returnRows
  } catch (error) {
    loadError.value = feedback.cleanMessage(error instanceof Error ? error.message : 'Please check the service and try again.')
    feedback.failed('Could not load purchase returns', error)
  } finally {
    loading.value = false
  }
}

async function openReturnDetail(item: any) {
  selectedReturn.value = item
  detailOpen.value = true
  detailLoading.value = true
  try {
    selectedReturn.value = await api.get<any>(`purchase/returns/${item.id}`)
  } catch (error) {
    feedback.failed('Could not load purchase return', error)
    detailOpen.value = false
  } finally {
    detailLoading.value = false
  }
}

async function openReturn(invoice: any) {
  pendingInvoice.value = invoice
  reason.value = 'Partial purchase return'
  returnDate.value = localDateInput()
  returnRows.value = []
  returnInvoice.value = null
  returnOpen.value = true
  returnLoading.value = true
  try {
    const detail = await api.get<any>(`purchase/invoices/${invoice.id}/returnable`)
    returnInvoice.value = detail
    returnRows.value = (detail.items || []).map((item: any) => ({
      ...item,
      returnQuantity: 0
    }))
  } catch (error) {
    feedback.failed('Could not load returnable purchase items', error)
    returnOpen.value = false
  } finally {
    returnLoading.value = false
  }
}

function selectAllReturnable() {
  returnRows.value = returnRows.value.map((row) => ({
    ...row,
    returnQuantity: decimal(row.returnableQuantity)
  }))
}

function clearReturnQuantities() {
  returnRows.value = returnRows.value.map((row) => ({ ...row, returnQuantity: 0 }))
}

async function submitReturn() {
  if (!pendingInvoice.value) return
  const items = selectedReturnRows.value.map((row) => ({
    itemId: row.itemId,
    quantity: Math.min(row.returnQuantity, decimal(row.returnableQuantity))
  }))

  if (!items.length) {
    feedback.notify('No return quantity selected', 'Enter quantity against at least one returnable item.', 'warning')
    return
  }

  saving.value = true
  try {
    const response = await api.create<any>(`purchase/invoices/${pendingInvoice.value.id}/partial-return`, {
      items,
      reason: reason.value || 'Partial purchase return',
      returnDate: returnDate.value ? `${returnDate.value}T00:00:00` : null
    })
    feedback.notify('Purchase return saved', `${response.returnNumber || 'Return'} posted with debit note ${response.debitNoteNumber || ''} for ${money(response.returnAmount || 0)}.`, 'success')
    pendingInvoice.value = null
    returnInvoice.value = null
    returnRows.value = []
    returnOpen.value = false
    await refresh()
  } catch (error) {
    feedback.failed('Could not create purchase return', error)
  } finally {
    saving.value = false
  }
}

function decimal(value: unknown) {
  const number = Number(value || 0)
  return Number.isFinite(number) ? number : 0
}

function lineAmount(row: any) {
  return Math.min(decimal(row.returnQuantity), decimal(row.returnableQuantity)) * decimal(row.unitAmount)
}

function money(value: number) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(Number(value || 0))
}

function formatDate(value: string) {
  return value ? new Date(value).toLocaleDateString('en-IN') : '-'
}

function localDateInput(date = new Date()) {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

onMounted(refresh)
</script>

<template>
  <AuthScreen v-if="!isAuthenticated" @authenticated="refresh" />
  <AppShell
    v-else
    title="Purchase Return"
    :companies="companies"
    :stores="stores"
    @refresh="refresh"
    @workspace-change="refresh"
  >
    <section class="planner-dashboard">
      <UiModulePageHeader
        title="Purchase Return / Debit Note"
        description="Create item-wise partial purchase returns, reverse selected stock, and issue vendor debit notes."
        icon="i-lucide-undo-2"
      >
        <template #actions>
          <UBadge color="success" variant="subtle">{{ purchaseReturns.length }} posted returns</UBadge>
          <UBadge color="neutral" variant="subtle">{{ filteredInvoices.length }} returnable purchases</UBadge>
        </template>
      </UiModulePageHeader>

      <UiRegisterPanel
        title="Purchase Return Register"
        :description="`${returnHistoryRows.length} of ${purchaseReturns.length} formal return documents`"
        :loading="loading"
        :error="loadError"
        :empty="returnHistoryRows.length === 0"
        :empty-title="returnSearch ? 'No matching purchase returns' : 'No purchase returns posted yet'"
        :empty-description="returnSearch ? 'Change the return number, invoice, vendor, or debit-note search.' : 'New partial returns and purchase cancellations will be recorded here with immutable item and tax snapshots.'"
        empty-icon="i-lucide-files"
        @retry="refresh"
      >
        <template #actions>
          <UiCrudToolbar
            v-model:search="returnSearch"
            search-placeholder="Search return, invoice, vendor, debit note"
            :loading="loading"
            refresh-label="Sync"
            @refresh="refresh"
          />
        </template>

        <div class="planner-table-wrap">
          <UTable :data="returnHistoryRows" :columns="returnHistoryColumns" />
        </div>
      </UiRegisterPanel>

      <UiRegisterPanel
        title="Returnable Purchase Invoices"
        :description="`${tableRows.length} purchases available for item-wise return`"
        :loading="loading"
        :error="loadError"
        :empty="tableRows.length === 0"
        :empty-title="search ? 'No matching returnable purchases' : 'No returnable purchase invoices'"
        :empty-description="search ? 'Change the invoice, inward, or supplier search.' : 'Purchase inward invoices will appear here for item-wise return processing.'"
        empty-icon="i-lucide-undo-2"
        @retry="refresh"
      >
        <template #actions>
          <UiCrudToolbar
            v-model:search="search"
            search-placeholder="Search invoice, inward, or supplier"
            :loading="loading"
            refresh-label="Sync"
            @refresh="refresh"
          />
        </template>

        <div class="planner-table-wrap">
          <UTable :data="tableRows" :columns="columns" />
        </div>
      </UiRegisterPanel>

      <UiFormSlideover
        v-model:open="returnOpen"
        title="Create Partial Purchase Return"
        :description="`Return selected items from ${pendingInvoice?.invoiceNumber || 'purchase invoice'} and create a vendor debit note.`"
        submit-label="Save Purchase Return"
        layout="modal"
        content-class="w-[calc(100vw-2rem)] sm:max-w-6xl xl:max-w-7xl"
        :loading="saving"
        @submit="submitReturn"
      >
        <div v-if="returnLoading" class="py-10 text-center text-sm text-muted">
          Loading returnable items...
        </div>

        <div v-else class="space-y-4">
          <UAlert
            color="info"
            variant="subtle"
            icon="i-lucide-info"
            title="Item-wise purchase return"
            description="Only selected quantities will be reversed from stock. The original purchase invoice values remain for audit history, and a debit note is created for the return value."
          />

          <div class="payroll-summary">
            <span>Supplier</span><strong>{{ returnInvoice?.vendorName || pendingInvoice?.vendorName || '-' }}</strong>
            <span>Invoice</span><strong>{{ returnInvoice?.invoiceNumber || pendingInvoice?.invoiceNumber || '-' }}</strong>
            <span>Original amount</span><strong>{{ money(returnInvoice?.billAmount || pendingInvoice?.billAmount || 0) }}</strong>
            <span>Return amount</span><strong>{{ money(returnSummary.amount) }}</strong>
          </div>

          <div class="grid gap-3 md:grid-cols-[1fr_180px]">
            <UFormField label="Reason">
              <UTextarea v-model="reason" :rows="3" />
            </UFormField>
            <UFormField label="Return Date">
              <UInput v-model="returnDate" type="date" />
            </UFormField>
          </div>

          <div class="flex flex-wrap items-center justify-between gap-2">
            <div class="text-sm text-muted">
              Selected Qty: <strong>{{ returnSummary.quantity.toFixed(2) }}</strong> | Taxable: <strong>{{ money(returnSummary.taxable) }}</strong> | Tax: <strong>{{ money(returnSummary.tax) }}</strong>
            </div>
            <div class="flex gap-2">
              <UButton color="neutral" variant="subtle" icon="i-lucide-check-check" label="Return All Available" @click="selectAllReturnable" />
              <UButton color="neutral" variant="ghost" icon="i-lucide-x" label="Clear" @click="clearReturnQuantities" />
            </div>
          </div>

          <div v-if="returnRows.length" class="simple-table-wrap">
            <table class="simple-table">
              <thead>
                <tr>
                  <th>Item</th>
                  <th>Barcode / HSN</th>
                  <th>Purchased</th>
                  <th>Returned</th>
                  <th>Returnable</th>
                  <th>Return Qty</th>
                  <th>Unit Amt</th>
                  <th>Line Return</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="row in returnRows" :key="row.itemId">
                  <td>
                    <div class="font-medium">{{ row.productName || 'Item' }}</div>
                    <div class="text-xs text-muted">Unit: {{ row.unit || '-' }} | Tax {{ row.taxPercentage || 0 }}%</div>
                  </td>
                  <td>
                    <div>{{ row.barcode || '-' }}</div>
                    <div class="text-xs text-muted">{{ row.hsnCode || 'No HSN' }}</div>
                  </td>
                  <td>{{ decimal(row.purchasedQuantity).toFixed(2) }}</td>
                  <td>{{ decimal(row.alreadyReturnedQuantity).toFixed(2) }}</td>
                  <td>{{ decimal(row.returnableQuantity).toFixed(2) }}</td>
                  <td class="min-w-36">
                    <UInput
                      v-model="row.returnQuantity"
                      type="number"
                      min="0"
                      :max="decimal(row.returnableQuantity)"
                      step="0.01"
                      :disabled="decimal(row.returnableQuantity) <= 0"
                    />
                  </td>
                  <td>{{ money(row.unitAmount || 0) }}</td>
                  <td>{{ money(lineAmount(row)) }}</td>
                </tr>
              </tbody>
            </table>
          </div>

          <UiCrudEmptyState
            v-else
            title="No returnable items"
            description="All quantities from this purchase invoice may already be returned."
            icon="i-lucide-package-x"
          />
        </div>
      </UiFormSlideover>

      <UModal v-model:open="detailOpen" title="Purchase Return Details" :ui="{ content: 'sm:max-w-5xl xl:max-w-6xl' }">
        <template #body>
          <div v-if="detailLoading" class="py-10 text-center text-sm text-muted">
            Loading purchase return...
          </div>
          <div v-else-if="selectedReturn" class="space-y-4">
            <UAlert
              color="neutral"
              variant="subtle"
              icon="i-lucide-file-check-2"
              :title="`${selectedReturn.returnNumber} / ${selectedReturn.returnKind}`"
              :description="`${selectedReturn.vendorName} | Original purchase ${selectedReturn.originalInvoiceNumber} | Debit note ${selectedReturn.debitNoteNumber || 'not linked'}`"
            />

            <div class="payroll-summary">
              <span>Return date</span><strong>{{ formatDate(selectedReturn.onDate) }}</strong>
              <span>Status</span><strong>{{ selectedReturn.status || 'Posted' }}</strong>
              <span>Quantity</span><strong>{{ decimal(selectedReturn.quantity).toFixed(2) }}</strong>
              <span>Taxable</span><strong>{{ money(selectedReturn.taxableAmount) }}</strong>
              <span>GST reversal</span><strong>{{ money(selectedReturn.taxAmount) }}</strong>
              <span>Return amount</span><strong>{{ money(selectedReturn.returnAmount) }}</strong>
            </div>

            <div class="planner-table-wrap">
              <table class="planner-table">
                <thead>
                  <tr>
                    <th>Item Snapshot</th>
                    <th>Barcode / HSN</th>
                    <th>Purchased</th>
                    <th>Previously Returned</th>
                    <th>Returned</th>
                    <th>Rate</th>
                    <th>Tax</th>
                    <th>Amount</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="item in selectedReturn.items || []" :key="item.id">
                    <td>
                      <div class="font-medium">{{ item.productName }}</div>
                      <div class="text-xs text-muted">{{ item.unit || '-' }} | GST {{ decimal(item.taxRate).toFixed(2) }}%</div>
                    </td>
                    <td>
                      <div>{{ item.barcode || '-' }}</div>
                      <div class="text-xs text-muted">{{ item.hsnCode || 'No HSN' }}</div>
                    </td>
                    <td>{{ decimal(item.purchasedQuantity).toFixed(2) }}</td>
                    <td>{{ decimal(item.previouslyReturnedQuantity).toFixed(2) }}</td>
                    <td>{{ decimal(item.returnedQuantity).toFixed(2) }}</td>
                    <td>{{ money(item.unitRate) }}</td>
                    <td>{{ money(item.taxAmount) }}</td>
                    <td>{{ money(item.returnAmount) }}</td>
                  </tr>
                </tbody>
              </table>
            </div>

            <UAlert
              color="info"
              variant="subtle"
              title="Return reason"
              :description="selectedReturn.reason || 'No reason recorded.'"
            />
          </div>
        </template>
        <template #footer>
          <UButton color="neutral" variant="outline" label="Close" @click="detailOpen = false" />
        </template>
      </UModal>
    </section>
  </AppShell>
</template>
