<script setup lang="ts">
import { h, resolveComponent } from 'vue'
import type { TableColumn } from '@nuxt/ui'

const api = useGarmetixApi()
const auth = useAuth()
const workspace = useWorkspace()
const feedback = useUiFeedback()
const isAuthenticated = auth.isAuthenticated

const UBadge = resolveComponent('UBadge')
const UButton = resolveComponent('UButton')

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const invoices = ref<any[]>([])
const bankAccounts = ref<any[]>([])
const loading = ref(false)
const returning = ref(false)
const search = ref('')
const loadError = ref('')
const returnOpen = ref(false)
const pendingInvoice = ref<any | null>(null)
const returnLines = ref<any[]>([])
const returnForm = reactive({
  refundAmount: 0,
  refundPaymentMode: 0,
  bankAccountId: null as string | null,
  reason: 'Sales return'
})

const paymentModeOptions = [
  { value: 0, label: 'Cash' },
  { value: 1, label: 'Card' },
  { value: 2, label: 'UPI' },
  { value: 3, label: 'Wallet' },
  { value: 4, label: 'IMPS' },
  { value: 5, label: 'RTGS' },
  { value: 6, label: 'NEFT' },
  { value: 7, label: 'Cheque' },
  { value: 8, label: 'Demand Draft' }
]

const bankAccountOptions = computed(() => bankAccounts.value
  .filter((account) => !workspace.companyId.value || account.companyId === workspace.companyId.value)
  .map((account) => ({
    value: account.id,
    label: `${account.accountHolderName || 'Bank'} - ${account.accountNumber || ''}`.trim()
  })))

const refundRequiresBank = computed(() =>
  Number(returnForm.refundAmount || 0) > 0
  && Number(returnForm.refundPaymentMode) !== 0)

const filteredInvoices = computed(() => {
  const term = search.value.trim().toLowerCase()
  return invoices.value
    .filter((invoice) => invoice.invoiceStatus !== 'Cancelled' && invoice.invoiceStatus !== 'Refunded' && !String(invoice.invoiceNumber || '').startsWith('SR-'))
    .filter((invoice) => {
      if (!term) return true
      return [invoice.invoiceNumber, invoice.customerName, invoice.customerMobileNumber, invoice.invoiceStatus]
        .some((value) => String(value || '').toLowerCase().includes(term))
    })
})

const tableRows = computed(() => filteredInvoices.value.map((invoice) => ({
  id: invoice.id,
  invoiceNumber: invoice.invoiceNumber || '-',
  onDate: formatDate(invoice.onDate),
  customerName: invoice.customerName || 'Walk-in Customer',
  customerMobileNumber: invoice.customerMobileNumber || '-',
  billAmount: money(Number(invoice.billAmount || 0)),
  invoiceStatus: invoice.invoiceStatus || 'Saved',
  raw: invoice
})))

const columns: TableColumn<any>[] = [
  { accessorKey: 'invoiceNumber', header: 'Invoice' },
  { accessorKey: 'onDate', header: 'Date' },
  { accessorKey: 'customerName', header: 'Customer' },
  { accessorKey: 'customerMobileNumber', header: 'Mobile' },
  { accessorKey: 'billAmount', header: 'Amount' },
  {
    accessorKey: 'invoiceStatus',
    header: 'Status',
    cell: ({ row }) => h(UBadge, { color: 'success', variant: 'subtle' }, () => row.original.invoiceStatus)
  },
  {
    id: 'actions',
    header: '',
    cell: ({ row }) => h(UButton, {
      icon: 'i-lucide-rotate-ccw',
      color: 'warning',
      variant: 'subtle',
      label: 'Return',
      onClick: () => openReturn(row.original.raw)
    })
  }
]

const returnTotal = computed(() => returnLines.value.reduce((sum, item) => {
  const quantity = Number(item.returnQuantity || 0)
  const mrp = Number(item.mrp || 0)
  const discount = Number(item.discountAmount || 0)
  return sum + Math.max(mrp - discount, 0) * quantity
}, 0))

async function refresh() {
  if (!auth.isAuthenticated.value) return
  loading.value = true
  loadError.value = ''
  try {
    const [companyRows, storeRows, invoiceRows, bankAccountRows] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores'),
      api.get<any[]>('billing/sales/recent'),
      api.list<any>('bank-accounts')
    ])
    companies.value = companyRows
    stores.value = storeRows
    invoices.value = invoiceRows
    bankAccounts.value = bankAccountRows
  } catch (error) {
    loadError.value = feedback.cleanMessage(error instanceof Error ? error.message : 'Please check the service and try again.')
    feedback.failed('Could not load sales returns', error)
  } finally {
    loading.value = false
  }
}

async function openReturn(invoice: any) {
  try {
    const receipt = await api.get<any>(`billing/sales/${invoice.id}/receipt`)
    pendingInvoice.value = invoice
    returnLines.value = (receipt.items || []).map((item: any) => ({
      invoiceItemId: item.id,
      productName: item.productName,
      barcode: item.barcode,
      quantity: Number(item.quantity || 0),
      returnQuantity: 0,
      mrp: Number(item.mrp || 0),
      discountAmount: Number(item.discountAmount || 0)
    }))
    returnForm.refundAmount = 0
    returnForm.refundPaymentMode = 0
    returnForm.bankAccountId = null
    returnForm.reason = 'Sales return'
    returnOpen.value = true
  } catch (error) {
    feedback.failed('Could not load invoice items', error)
  }
}

function clampReturnQuantity(item: any) {
  const max = Number(item.quantity || 0)
  const value = Number(item.returnQuantity || 0)
  item.returnQuantity = Math.min(Math.max(value, 0), max)
}

async function submitReturn() {
  if (!pendingInvoice.value) return
  const items = returnLines.value
    .filter((item) => Number(item.returnQuantity || 0) > 0)
    .map((item) => ({ invoiceItemId: item.invoiceItemId, quantity: Number(item.returnQuantity || 0) }))

  if (!items.length) {
    feedback.notify('Enter return quantity', 'At least one item must have return quantity greater than zero.', 'warning')
    return
  }

  if (refundRequiresBank.value && !returnForm.bankAccountId) {
    feedback.notify('Select bank account', 'A bank account is required for non-cash refunds.', 'warning')
    return
  }

  returning.value = true
  try {
    const response = await api.create<any>(`billing/sales/${pendingInvoice.value.id}/returns`, {
      refundAmount: Number(returnForm.refundAmount || 0),
      refundPaymentMode: Number(returnForm.refundAmount || 0) > 0 ? Number(returnForm.refundPaymentMode) : null,
      bankAccountId: refundRequiresBank.value ? returnForm.bankAccountId : null,
      reason: returnForm.reason,
      items
    })
    feedback.saved(`Credit note ${response.creditNoteNumber || ''}`.trim())
    returnOpen.value = false
    pendingInvoice.value = null
    await refresh()
  } catch (error) {
    feedback.failed('Could not create sales return', error)
  } finally {
    returning.value = false
  }
}

function money(value: number) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(Number(value || 0))
}

function formatDate(value: string) {
  return value ? new Date(value).toLocaleDateString('en-IN') : '-'
}

onMounted(refresh)

watch(() => [returnForm.refundAmount, returnForm.refundPaymentMode], () => {
  if (refundRequiresBank.value && !returnForm.bankAccountId) {
    returnForm.bankAccountId = bankAccountOptions.value[0]?.value || null
  }
})
</script>

<template>
  <AuthScreen v-if="!isAuthenticated" @authenticated="refresh" />
  <AppShell
    v-else
    title="Sales Return"
    :companies="companies"
    :stores="stores"
    @refresh="refresh"
    @workspace-change="refresh"
  >
    <section class="planner-dashboard">
      <UiModulePageHeader
        title="Sales Return / Credit Note"
        description="Create item-wise sales returns from existing invoices. Returned value becomes a credit note or optional cash refund."
        icon="i-lucide-rotate-ccw"
      >
        <template #actions>
          <UBadge color="neutral" variant="subtle">{{ filteredInvoices.length }} returnable invoices</UBadge>
        </template>
      </UiModulePageHeader>

      <UiRegisterPanel
        title="Returnable Sales Invoices"
        :description="`${tableRows.length} invoices available for item-wise return`"
        :loading="loading"
        :error="loadError"
        :empty="tableRows.length === 0"
        :empty-title="search ? 'No matching returnable invoices' : 'No returnable invoices'"
        :empty-description="search ? 'Change the invoice or customer search.' : 'Completed sales invoices will appear here for return processing.'"
        empty-icon="i-lucide-rotate-ccw"
        @retry="refresh"
      >
        <template #actions>
          <UiCrudToolbar
            v-model:search="search"
            search-placeholder="Search invoice or customer"
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
        title="Create Sales Return"
        :description="`Return selected items from ${pendingInvoice?.invoiceNumber || 'invoice'}.`"
        submit-label="Save Return / Credit Note"
        layout="modal"
        content-class="w-[calc(100vw-2rem)] sm:max-w-5xl lg:max-w-6xl"
        :loading="returning"
        @submit="submitReturn"
      >
        <UAlert color="warning" variant="subtle" icon="i-lucide-info" title="Credit note workflow" description="Returned quantity reverses sold stock. If refund is not paid now, the balance remains as customer credit." />
        <div class="simple-table-wrap mt-4">
          <table class="simple-table">
            <thead>
              <tr>
                <th>Item</th>
                <th>Sold</th>
                <th>Return</th>
                <th>MRP</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="item in returnLines" :key="item.invoiceItemId">
                <td>{{ item.productName }}<small>{{ item.barcode }}</small></td>
                <td>{{ item.quantity }}</td>
                <td><UInput v-model="item.returnQuantity" type="number" min="0" :max="item.quantity" @blur="clampReturnQuantity(item)" /></td>
                <td>{{ money(item.mrp) }}</td>
              </tr>
            </tbody>
          </table>
        </div>
        <div class="form-two-column mt-4">
          <UFormField label="Refund amount now">
            <UInput v-model="returnForm.refundAmount" type="number" min="0" step="0.01" />
          </UFormField>
          <UFormField label="Refund mode">
            <USelect v-model="returnForm.refundPaymentMode" :items="paymentModeOptions" />
          </UFormField>
        </div>
        <UFormField v-if="refundRequiresBank" label="Bank account" required class="mt-4">
          <USelect v-model="returnForm.bankAccountId" :items="bankAccountOptions" placeholder="Select bank account" />
        </UFormField>
        <UFormField label="Reason / remarks" class="mt-4">
          <UTextarea v-model="returnForm.reason" :rows="3" />
        </UFormField>
        <div class="payroll-summary mt-4">
          <span>Return value</span><strong>{{ money(returnTotal) }}</strong>
          <span>Refund now</span><strong>{{ money(Number(returnForm.refundAmount || 0)) }}</strong>
          <span>Store credit</span><strong>{{ money(Math.max(returnTotal - Number(returnForm.refundAmount || 0), 0)) }}</strong>
        </div>
      </UiFormSlideover>
    </section>
  </AppShell>
</template>
