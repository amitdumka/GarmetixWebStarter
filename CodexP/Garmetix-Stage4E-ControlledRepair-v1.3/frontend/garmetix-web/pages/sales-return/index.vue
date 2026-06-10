<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const feedback = useUiFeedback()
const isAuthenticated = auth.isAuthenticated

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const invoices = ref<any[]>([])
const loading = ref(false)
const returning = ref(false)
const search = ref('')
const returnOpen = ref(false)
const pendingInvoice = ref<any | null>(null)
const returnLines = ref<any[]>([])
const returnForm = reactive({
  refundAmount: 0,
  refundPaymentMode: 0,
  reason: 'Sales return'
})

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

const returnTotal = computed(() => returnLines.value.reduce((sum, item) => {
  const quantity = Number(item.returnQuantity || 0)
  const mrp = Number(item.mrp || 0)
  const discount = Number(item.discountAmount || 0)
  return sum + Math.max(mrp - discount, 0) * quantity
}, 0))

async function refresh() {
  if (!auth.isAuthenticated.value) return
  loading.value = true
  try {
    const [companyRows, storeRows, invoiceRows] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores'),
      api.get<any[]>('billing/sales/recent')
    ])
    companies.value = companyRows
    stores.value = storeRows
    invoices.value = invoiceRows
  } catch (error) {
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

  returning.value = true
  try {
    const response = await api.create<any>(`billing/sales/${pendingInvoice.value.id}/returns`, {
      refundAmount: Number(returnForm.refundAmount || 0),
      refundPaymentMode: Number(returnForm.refundAmount || 0) > 0 ? Number(returnForm.refundPaymentMode) : null,
      bankAccountId: null,
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
</script>

<template>
  <AuthScreen v-if="!isAuthenticated" @authenticated="refresh" />
  <AppShell v-else title="Sales Return" :companies="companies" :stores="stores" @refresh="refresh">
    <section class="planner-dashboard">
      <UiModulePageHeader
        title="Sales Return / Credit Note"
        description="Create item-wise sales returns from existing invoices. Returned value becomes a credit note or optional cash refund."
        icon="i-lucide-rotate-ccw"
      >
        <template #actions>
          <UBadge color="neutral" variant="subtle">{{ filteredInvoices.length }} returnable invoices</UBadge>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="subtle" :loading="loading" label="Refresh" @click="refresh" />
        </template>
      </UiModulePageHeader>

      <UCard class="planner-card">
        <template #header>
          <div class="planner-card-header">
            <div>
              <h2>Returnable Sales Invoices</h2>
              <p>Select an invoice and create a credit note.</p>
            </div>
            <UInput v-model="search" icon="i-lucide-search" placeholder="Search invoice or customer" />
          </div>
        </template>

        <div v-if="filteredInvoices.length" class="simple-table-wrap">
          <table class="simple-table">
            <thead>
              <tr>
                <th>Invoice</th>
                <th>Date</th>
                <th>Customer</th>
                <th>Amount</th>
                <th>Status</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="invoice in filteredInvoices" :key="invoice.id">
                <td>{{ invoice.invoiceNumber || '-' }}</td>
                <td>{{ formatDate(invoice.onDate) }}</td>
                <td>{{ invoice.customerName || 'Walk-in Customer' }}</td>
                <td>{{ money(invoice.billAmount) }}</td>
                <td><UBadge color="success" variant="subtle">{{ invoice.invoiceStatus || 'Saved' }}</UBadge></td>
                <td class="text-right">
                  <UButton icon="i-lucide-rotate-ccw" color="warning" variant="subtle" label="Return" @click="openReturn(invoice)" />
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <UiCrudEmptyState
          v-else
          title="No returnable invoices"
          description="Completed sales invoices will appear here for return processing."
          icon="i-lucide-rotate-ccw"
          action-label="Refresh"
          @action="refresh"
        />
      </UCard>

      <UiFormSlideover
        v-model:open="returnOpen"
        title="Create Sales Return"
        :description="`Return selected items from ${pendingInvoice?.invoiceNumber || 'invoice'}.`"
        submit-label="Save Return / Credit Note"
        layout="modal"
        content-class="w-[calc(100vw-2rem)] sm:max-w-5xl"
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
          <UFormField label="Reason">
            <UInput v-model="returnForm.reason" />
          </UFormField>
        </div>
        <div class="summary-strip mt-4">
          <span>Return value</span><strong>{{ money(returnTotal) }}</strong>
        </div>
      </UiFormSlideover>
    </section>
  </AppShell>
</template>
