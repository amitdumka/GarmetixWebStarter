<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const feedback = useUiFeedback()
const isAuthenticated = auth.isAuthenticated

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const invoices = ref<any[]>([])
const loading = ref(false)
const saving = ref(false)
const returnLoading = ref(false)
const search = ref('')
const returnOpen = ref(false)
const pendingInvoice = ref<any | null>(null)
const returnInvoice = ref<any | null>(null)
const reason = ref('Partial purchase return')
const returnDate = ref(new Date().toISOString().slice(0, 10))
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

const selectedReturnRows = computed(() => returnRows.value
  .map((row) => ({ ...row, returnQuantity: decimal(row.returnQuantity) }))
  .filter((row) => row.returnQuantity > 0))

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
  try {
    const [companyRows, storeRows, invoiceRows] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores'),
      api.get<any[]>('purchase/invoices/recent')
    ])
    companies.value = companyRows
    stores.value = storeRows
    invoices.value = invoiceRows
  } catch (error) {
    feedback.failed('Could not load purchase returns', error)
  } finally {
    loading.value = false
  }
}

async function openReturn(invoice: any) {
  pendingInvoice.value = invoice
  reason.value = 'Partial purchase return'
  returnDate.value = new Date().toISOString().slice(0, 10)
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
      returnDate: returnDate.value ? new Date(returnDate.value).toISOString() : null
    })
    feedback.notify('Purchase return saved', `Debit note ${response.debitNoteNumber || ''} created for ${money(response.returnAmount || 0)}.`, 'success')
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

onMounted(refresh)
</script>

<template>
  <AuthScreen v-if="!isAuthenticated" @authenticated="refresh" />
  <AppShell v-else title="Purchase Return" :companies="companies" :stores="stores" @refresh="refresh">
    <section class="planner-dashboard">
      <UiModulePageHeader
        title="Purchase Return / Debit Note"
        description="Create item-wise partial purchase returns, reverse selected stock, and issue vendor debit notes."
        icon="i-lucide-undo-2"
      >
        <template #actions>
          <UBadge color="neutral" variant="subtle">{{ filteredInvoices.length }} returnable purchases</UBadge>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="subtle" :loading="loading" label="Refresh" @click="refresh" />
        </template>
      </UiModulePageHeader>

      <UCard class="planner-card">
        <template #header>
          <div class="planner-card-header">
            <div>
              <h2>Returnable Purchase Invoices</h2>
              <p>Select an inward invoice and return only the selected items/quantities.</p>
            </div>
            <UInput v-model="search" icon="i-lucide-search" placeholder="Search invoice, inward, or supplier" />
          </div>
        </template>

        <div v-if="filteredInvoices.length" class="simple-table-wrap">
          <table class="simple-table">
            <thead>
              <tr>
                <th>Invoice</th>
                <th>Inward</th>
                <th>Date</th>
                <th>Vendor</th>
                <th>Amount</th>
                <th>Balance</th>
                <th>Status</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="invoice in filteredInvoices" :key="invoice.id">
                <td>{{ invoice.invoiceNumber || '-' }}</td>
                <td>{{ invoice.inwardNumber || '-' }}</td>
                <td>{{ formatDate(invoice.onDate || invoice.inwardDate) }}</td>
                <td>{{ invoice.vendorName || 'Vendor' }}</td>
                <td>{{ money(invoice.billAmount || invoice.totalAmount || invoice.netAmount) }}</td>
                <td>{{ money(invoice.balanceAmount || 0) }}</td>
                <td><UBadge color="success" variant="subtle">{{ invoice.invoiceStatus || 'Saved' }}</UBadge></td>
                <td class="text-right">
                  <UButton icon="i-lucide-undo-2" color="warning" variant="subtle" label="Return Items" @click="openReturn(invoice)" />
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <UiCrudEmptyState
          v-else
          title="No returnable purchase invoices"
          description="Purchase inward invoices will appear here for item-wise return processing."
          icon="i-lucide-undo-2"
          action-label="Refresh"
          @action="refresh"
        />
      </UCard>

      <UiFormSlideover
        v-model:open="returnOpen"
        title="Create Partial Purchase Return"
        :description="`Return selected items from ${pendingInvoice?.invoiceNumber || 'purchase invoice'} and create a vendor debit note.`"
        submit-label="Save Purchase Return"
        layout="modal"
        content-class="w-[calc(100vw-2rem)] sm:max-w-6xl"
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

          <div class="grid gap-3 md:grid-cols-4">
            <UCard variant="subtle">
              <p class="text-xs text-muted">Supplier</p>
              <p class="font-semibold">{{ returnInvoice?.vendorName || pendingInvoice?.vendorName || '-' }}</p>
            </UCard>
            <UCard variant="subtle">
              <p class="text-xs text-muted">Invoice</p>
              <p class="font-semibold">{{ returnInvoice?.invoiceNumber || pendingInvoice?.invoiceNumber || '-' }}</p>
            </UCard>
            <UCard variant="subtle">
              <p class="text-xs text-muted">Original Amount</p>
              <p class="font-semibold">{{ money(returnInvoice?.billAmount || pendingInvoice?.billAmount || 0) }}</p>
            </UCard>
            <UCard variant="subtle">
              <p class="text-xs text-muted">Return Amount</p>
              <p class="font-semibold">{{ money(returnSummary.amount) }}</p>
            </UCard>
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
              Selected Qty: <strong>{{ returnSummary.quantity.toFixed(2) }}</strong> · Taxable: <strong>{{ money(returnSummary.taxable) }}</strong> · Tax: <strong>{{ money(returnSummary.tax) }}</strong>
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
                    <div class="text-xs text-muted">Unit: {{ row.unit || '-' }} · Tax {{ row.taxPercentage || 0 }}%</div>
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
    </section>
  </AppShell>
</template>
