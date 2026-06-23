<script setup lang="ts">
import { h, resolveComponent } from 'vue'
import type { TableColumn } from '@nuxt/ui'

const api = useGarmetixApi()
const auth = useAuth()
const feedback = useUiFeedback()
const documentPrint = useServerDocumentPrint()
const route = useRoute()
const router = useRouter()
const isAuthenticated = auth.isAuthenticated

const UBadge = resolveComponent('UBadge')
const UButton = resolveComponent('UButton')

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const bankAccounts = ref<any[]>([])
const returns = ref<any[]>([])
const settlements = ref<any[]>([])
const loading = ref(false)
const loadError = ref('')
const search = ref('')
const statusFilter = ref('all')
const formOpen = ref(false)
const formLoading = ref(false)
const saving = ref(false)
const detailOpen = ref(false)
const detailLoading = ref(false)
const selectedReturn = ref<any | null>(null)
const settlementOptions = ref<any | null>(null)
const selectedSettlement = ref<any | null>(null)
const openedRouteReturnId = ref('')

const paymentModes = [
  { value: 0, label: 'Cash' },
  { value: 1, label: 'Card' },
  { value: 2, label: 'UPI' },
  { value: 3, label: 'Wallet' },
  { value: 4, label: 'IMPS' },
  { value: 5, label: 'RTGS' },
  { value: 6, label: 'NEFT' },
  { value: 7, label: 'Cheque' },
  { value: 8, label: 'Demand Draft' },
  { value: 14, label: 'Other' }
]

const form = reactive({
  onDate: localDateInput(),
  refundAmount: 0,
  paymentMode: 0,
  bankAccountId: '',
  referenceNumber: '',
  remarks: '',
  allocations: [] as any[]
})

const openReturns = computed(() => returns.value.filter((item) =>
  item.debitNoteId && decimal(item.availableSettlementAmount) > 0))

const filteredReturns = computed(() => {
  const term = search.value.trim().toLowerCase()
  return openReturns.value.filter((item) => {
    const matchesStatus = statusFilter.value === 'all' || String(item.settlementStatus || 'Open') === statusFilter.value
    const matchesSearch = !term || [
      item.returnNumber,
      item.originalInvoiceNumber,
      item.vendorName,
      item.debitNoteNumber,
      item.settlementStatus
    ].some((value) => String(value || '').toLowerCase().includes(term))
    return matchesStatus && matchesSearch
  })
})

const returnRows = computed(() => filteredReturns.value.map((item) => ({
  id: item.id,
  returnNumber: item.returnNumber || '-',
  onDate: formatDate(item.onDate),
  vendorName: item.vendorName || 'Vendor',
  debitNoteNumber: item.debitNoteNumber || '-',
  returnAmount: money(item.returnAmount),
  settledAmount: money(item.settledAmount),
  availableAmount: money(item.availableSettlementAmount),
  settlementStatus: item.settlementStatus || 'Open',
  raw: item
})))

const settlementRows = computed(() => settlements.value.map((item) => ({
  id: item.id,
  settlementNumber: item.settlementNumber || '-',
  onDate: formatDate(item.onDate),
  vendorName: item.vendorName || 'Vendor',
  returnNumber: item.returnNumber || '-',
  settlementType: item.settlementType || 'Adjustment',
  adjustedAmount: money(item.adjustedAmount),
  refundAmount: money(item.refundAmount),
  totalAmount: money(item.totalAmount),
  status: item.status || 'Posted',
  raw: item
})))

const allocationTotal = computed(() => form.allocations.reduce(
  (total, item) => total + Math.max(decimal(item.amount), 0),
  0))
const settlementTotal = computed(() => allocationTotal.value + Math.max(decimal(form.refundAmount), 0))
const availableAfter = computed(() => Math.max(decimal(settlementOptions.value?.availableAmount) - settlementTotal.value, 0))
const requiresBank = computed(() => decimal(form.refundAmount) > 0 && Number(form.paymentMode) !== 0)
const bankAccountOptions = computed(() => bankAccounts.value
  .filter((account) => !selectedReturn.value?.companyId || account.companyId === selectedReturn.value.companyId)
  .map((account) => ({
    value: account.id,
    label: `${account.accountHolderName || 'Bank'} - ${account.accountNumber || ''}`.trim()
  })))

const returnColumns: TableColumn<any>[] = [
  { accessorKey: 'returnNumber', header: 'Return' },
  { accessorKey: 'onDate', header: 'Date' },
  { accessorKey: 'vendorName', header: 'Vendor' },
  { accessorKey: 'debitNoteNumber', header: 'Debit Note' },
  { accessorKey: 'returnAmount', header: 'Return Value' },
  { accessorKey: 'settledAmount', header: 'Settled' },
  { accessorKey: 'availableAmount', header: 'Available' },
  {
    accessorKey: 'settlementStatus',
    header: 'Status',
    cell: ({ row }) => h(UBadge, {
      color: row.original.settlementStatus === 'Open' ? 'warning' : 'info',
      variant: 'subtle'
    }, () => row.original.settlementStatus)
  },
  {
    id: 'actions',
    header: '',
    cell: ({ row }) => h(UButton, {
      icon: 'i-lucide-hand-coins',
      label: 'Settle',
      color: 'primary',
      variant: 'subtle',
      onClick: () => openSettlement(row.original.raw)
    })
  }
]

const settlementColumns: TableColumn<any>[] = [
  { accessorKey: 'settlementNumber', header: 'Settlement' },
  { accessorKey: 'onDate', header: 'Date' },
  { accessorKey: 'vendorName', header: 'Vendor' },
  { accessorKey: 'returnNumber', header: 'Return' },
  { accessorKey: 'settlementType', header: 'Type' },
  { accessorKey: 'adjustedAmount', header: 'Adjusted' },
  { accessorKey: 'refundAmount', header: 'Refund' },
  { accessorKey: 'totalAmount', header: 'Total' },
  {
    accessorKey: 'status',
    header: 'Status',
    cell: ({ row }) => h(UBadge, { color: 'success', variant: 'subtle' }, () => row.original.status)
  },
  {
    id: 'actions',
    header: '',
    cell: ({ row }) => h('div', { class: 'flex justify-end gap-1' }, [
      row.original.raw.voucherId
        ? h(UButton, {
            icon: 'i-lucide-printer',
            color: 'primary',
            variant: 'ghost',
            'aria-label': `Print ${row.original.settlementNumber}`,
            onClick: () => printRefundVoucher(row.original.raw.voucherId)
          })
        : null,
      h(UButton, {
        icon: 'i-lucide-eye',
        color: 'neutral',
        variant: 'ghost',
        'aria-label': `View ${row.original.settlementNumber}`,
        onClick: () => openDetail(row.original.raw)
      })
    ].filter(Boolean))
  }
]

async function refresh() {
  if (!auth.isAuthenticated.value) return
  loading.value = true
  loadError.value = ''
  try {
    const [companyRows, storeRows, bankRows, returnData, settlementData] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores'),
      api.list<any>('bank-accounts'),
      api.get<any[]>('purchase/returns/recent'),
      api.get<any[]>('purchase/vendor-settlements/recent')
    ])
    companies.value = companyRows
    stores.value = storeRows
    bankAccounts.value = bankRows
    returns.value = returnData
    settlements.value = settlementData
    await openRouteReturn()
  } catch (error) {
    loadError.value = feedback.cleanMessage(error instanceof Error ? error.message : 'Please check the service and try again.')
    feedback.failed('Could not load vendor settlements', error)
  } finally {
    loading.value = false
  }
}

async function openRouteReturn() {
  const returnId = String(route.query.returnId || '')
  if (!returnId || openedRouteReturnId.value === returnId) return
  const match = returns.value.find((item) => item.id === returnId)
  if (!match || decimal(match.availableSettlementAmount) <= 0) return
  openedRouteReturnId.value = returnId
  await openSettlement(match)
}

async function openSettlement(item: any) {
  selectedReturn.value = item
  settlementOptions.value = null
  Object.assign(form, {
    onDate: localDateInput(),
    refundAmount: 0,
    paymentMode: 0,
    bankAccountId: '',
    referenceNumber: '',
    remarks: '',
    allocations: []
  })
  formOpen.value = true
  formLoading.value = true
  try {
    settlementOptions.value = await api.get<any>(`purchase/returns/${item.id}/settlement-options`)
    form.allocations = (settlementOptions.value.outstandingInvoices || []).map((invoice: any) => ({
      ...invoice,
      amount: 0
    }))
  } catch (error) {
    feedback.failed('Could not load settlement options', error)
    formOpen.value = false
  } finally {
    formLoading.value = false
  }
}

function allocateAvailable() {
  let remaining = decimal(settlementOptions.value?.availableAmount) - Math.max(decimal(form.refundAmount), 0)
  form.allocations = form.allocations.map((invoice) => {
    const amount = Math.max(Math.min(decimal(invoice.outstandingAmount), remaining), 0)
    remaining -= amount
    return { ...invoice, amount }
  })
}

function clearAllocations() {
  form.allocations = form.allocations.map((invoice) => ({ ...invoice, amount: 0 }))
}

async function saveSettlement() {
  if (!selectedReturn.value || settlementTotal.value <= 0) {
    feedback.notify('Settlement amount required', 'Enter a refund or allocate the debit note to outstanding purchases.', 'warning')
    return
  }
  if (settlementTotal.value > decimal(settlementOptions.value?.availableAmount)) {
    feedback.notify('Settlement exceeds available amount', 'Reduce the refund or invoice adjustments.', 'warning')
    return
  }
  if (requiresBank.value && !form.bankAccountId) {
    feedback.notify('Bank account required', 'Select the receiving bank account for a non-cash refund.', 'warning')
    return
  }

  saving.value = true
  try {
    const response = await api.create<any>(`purchase/returns/${selectedReturn.value.id}/settle`, {
      onDate: form.onDate ? `${form.onDate}T00:00:00` : null,
      refundAmount: decimal(form.refundAmount),
      paymentMode: decimal(form.refundAmount) > 0 ? Number(form.paymentMode) : null,
      bankAccountId: requiresBank.value ? form.bankAccountId : null,
      referenceNumber: form.referenceNumber || null,
      remarks: form.remarks || null,
      allocations: form.allocations
        .filter((item) => decimal(item.amount) > 0)
        .map((item) => ({
          purchaseInvoiceId: item.purchaseInvoiceId,
          amount: decimal(item.amount)
        }))
    })
    feedback.notify(
      'Vendor settlement posted',
      `${response.settlementNumber} posted for ${money(response.totalAmount)}. Remaining debit note: ${money(response.remainingAmount)}.`,
      'success')
    formOpen.value = false
    await router.replace({ query: {} })
    await refresh()
    if (response.voucherId) {
      try {
        await printRefundVoucher(response.voucherId)
      } catch {
        feedback.notify('Settlement saved; receipt print pending', 'Open the settlement register to print the refund receipt again.', 'warning')
      }
    }
  } catch (error) {
    feedback.failed('Could not post vendor settlement', error)
  } finally {
    saving.value = false
  }
}

async function openDetail(item: any) {
  selectedSettlement.value = item
  detailOpen.value = true
  detailLoading.value = true
  try {
    selectedSettlement.value = await api.get<any>(`purchase/vendor-settlements/${item.id}`)
  } catch (error) {
    feedback.failed('Could not load settlement details', error)
    detailOpen.value = false
  } finally {
    detailLoading.value = false
  }
}

async function printRefundVoucher(voucherId: string) {
  await documentPrint.printPdf(`vouchers/${voucherId}/pdf?format=a5-one&reprint=false&signatures=true`)
}

function decimal(value: unknown) {
  const number = Number(value || 0)
  return Number.isFinite(number) ? number : 0
}

function money(value: unknown) {
  return new Intl.NumberFormat('en-IN', {
    style: 'currency',
    currency: 'INR',
    maximumFractionDigits: 2
  }).format(decimal(value))
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
    title="Vendor Settlements"
    :companies="companies"
    :stores="stores"
    @refresh="refresh"
    @workspace-change="refresh"
  >
    <section class="planner-dashboard">
      <UiModulePageHeader
        title="Vendor Settlements"
        description="Allocate purchase-return debit notes to supplier dues or record actual cash and bank refunds."
        icon="i-lucide-hand-coins"
      >
        <template #actions>
          <UBadge color="warning" variant="subtle">{{ openReturns.length }} open debit notes</UBadge>
          <UBadge color="success" variant="subtle">{{ settlements.length }} posted settlements</UBadge>
        </template>
      </UiModulePageHeader>

      <UiRegisterPanel
        title="Debit Notes Available For Settlement"
        :description="`${returnRows.length} return credits available for allocation or refund`"
        :loading="loading"
        :error="loadError"
        :empty="returnRows.length === 0"
        :empty-title="search || statusFilter !== 'all' ? 'No matching debit notes' : 'No vendor debit notes awaiting settlement'"
        :empty-description="search || statusFilter !== 'all' ? 'Change the search or settlement-status filter.' : 'Purchase returns with an available debit-note balance will appear here.'"
        empty-icon="i-lucide-badge-indian-rupee"
        @retry="refresh"
      >
        <template #actions>
          <div class="flex flex-wrap items-center gap-2">
            <USelect
              v-model="statusFilter"
              :items="[
                { label: 'All settlement states', value: 'all' },
                { label: 'Open', value: 'Open' },
                { label: 'Partially Settled', value: 'Partially Settled' }
              ]"
              class="w-48"
              aria-label="Filter debit notes by settlement status"
            />
            <UiCrudToolbar
              v-model:search="search"
              search-placeholder="Search return, vendor, invoice, debit note"
              :loading="loading"
              refresh-label="Sync"
              @refresh="refresh"
            />
          </div>
        </template>
        <div class="planner-table-wrap">
          <UTable :data="returnRows" :columns="returnColumns" />
        </div>
      </UiRegisterPanel>

      <UiRegisterPanel
        title="Settlement History"
        :description="`${settlementRows.length} posted vendor settlement records`"
        :loading="loading"
        :error="loadError"
        :empty="settlementRows.length === 0"
        empty-title="No vendor settlements posted yet"
        empty-description="Debit-note allocations and vendor refund receipts will be recorded here with accounting links."
        empty-icon="i-lucide-history"
        @retry="refresh"
      >
        <div class="planner-table-wrap">
          <UTable :data="settlementRows" :columns="settlementColumns" />
        </div>
      </UiRegisterPanel>

      <UiFormSlideover
        v-model:open="formOpen"
        title="Post Vendor Settlement"
        :description="`Settle ${selectedReturn?.debitNoteNumber || 'vendor debit note'} from ${selectedReturn?.vendorName || 'supplier'}.`"
        submit-label="Post Settlement"
        layout="modal"
        content-class="w-[calc(100vw-2rem)] sm:max-w-6xl xl:max-w-7xl"
        :loading="saving"
        @submit="saveSettlement"
      >
        <div v-if="formLoading" class="py-10 text-center text-sm text-muted">
          Loading outstanding purchases...
        </div>
        <div v-else-if="settlementOptions" class="space-y-4">
          <UAlert
            color="info"
            variant="subtle"
            icon="i-lucide-scale"
            title="One debit note, two settlement methods"
            description="Invoice allocation updates purchase outstanding without duplicating the debit-note ledger posting. A real refund creates a receipt voucher and cash or bank accounting entry."
          />

          <div class="payroll-summary">
            <span>Vendor</span><strong>{{ settlementOptions.vendorName }}</strong>
            <span>Purchase return</span><strong>{{ settlementOptions.returnNumber }}</strong>
            <span>Debit note</span><strong>{{ settlementOptions.debitNoteNumber }}</strong>
            <span>Available credit</span><strong>{{ money(settlementOptions.availableAmount) }}</strong>
            <span>Invoice adjustment</span><strong>{{ money(allocationTotal) }}</strong>
            <span>Refund receipt</span><strong>{{ money(form.refundAmount) }}</strong>
            <span>Settlement total</span><strong>{{ money(settlementTotal) }}</strong>
            <span>Balance after</span><strong>{{ money(availableAfter) }}</strong>
          </div>

          <div class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
            <UFormField label="Settlement Date">
              <UInput v-model="form.onDate" type="date" />
            </UFormField>
            <UFormField label="Vendor Refund Amount">
              <UInput v-model="form.refundAmount" type="number" min="0" step="0.01" />
            </UFormField>
            <UFormField v-if="decimal(form.refundAmount) > 0" label="Refund Mode">
              <USelect v-model="form.paymentMode" :items="paymentModes" />
            </UFormField>
            <UFormField v-if="requiresBank" label="Receiving Bank Account">
              <USelect v-model="form.bankAccountId" :items="bankAccountOptions" placeholder="Select bank account" />
            </UFormField>
          </div>

          <div v-if="decimal(form.refundAmount) > 0" class="grid gap-3 md:grid-cols-2">
            <UFormField label="Bank / Cheque / UTR Reference">
              <UInput v-model="form.referenceNumber" placeholder="Optional for cash" />
            </UFormField>
            <UFormField label="Remarks">
              <UInput v-model="form.remarks" placeholder="Settlement remarks" />
            </UFormField>
          </div>

          <div class="flex flex-wrap items-center justify-between gap-2">
            <div>
              <h3 class="text-sm font-semibold">Allocate Debit Note To Outstanding Purchases</h3>
              <p class="text-xs text-muted">Only invoice tracking changes here; the debit-note journal is not posted again.</p>
            </div>
            <div class="flex gap-2">
              <UButton color="neutral" variant="subtle" icon="i-lucide-wand-sparkles" label="Allocate Available" @click="allocateAvailable" />
              <UButton color="neutral" variant="ghost" icon="i-lucide-x" label="Clear" @click="clearAllocations" />
            </div>
          </div>

          <div v-if="form.allocations.length" class="simple-table-wrap">
            <table class="simple-table">
              <thead>
                <tr>
                  <th>Purchase Invoice</th>
                  <th>Date / Due</th>
                  <th>Bill Amount</th>
                  <th>Already Paid</th>
                  <th>Outstanding</th>
                  <th>Adjust Amount</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="invoice in form.allocations" :key="invoice.purchaseInvoiceId">
                  <td>
                    <div class="font-medium">{{ invoice.invoiceNumber }}</div>
                    <div class="text-xs text-muted">{{ invoice.invoiceStatus }}</div>
                  </td>
                  <td>
                    <div>{{ formatDate(invoice.onDate) }}</div>
                    <div class="text-xs text-muted">Due {{ formatDate(invoice.dueDate) }}</div>
                  </td>
                  <td>{{ money(invoice.billAmount) }}</td>
                  <td>{{ money(invoice.paidAmount) }}</td>
                  <td>{{ money(invoice.outstandingAmount) }}</td>
                  <td class="min-w-40">
                    <UInput
                      v-model="invoice.amount"
                      type="number"
                      min="0"
                      :max="decimal(invoice.outstandingAmount)"
                      step="0.01"
                    />
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
          <UiCrudEmptyState
            v-else
            title="No outstanding purchase invoices"
            description="Use the refund amount when the vendor must return this debit-note value in cash or bank."
            icon="i-lucide-receipt-text"
          />
        </div>
      </UiFormSlideover>

      <UModal v-model:open="detailOpen" title="Vendor Settlement Details" :ui="{ content: 'sm:max-w-4xl' }">
        <template #body>
          <div v-if="detailLoading" class="py-10 text-center text-sm text-muted">
            Loading settlement...
          </div>
          <div v-else-if="selectedSettlement" class="space-y-4">
            <UAlert
              color="success"
              variant="subtle"
              icon="i-lucide-badge-check"
              :title="`${selectedSettlement.settlementNumber} / ${selectedSettlement.settlementType}`"
              :description="`${selectedSettlement.vendorName} | Return ${selectedSettlement.returnNumber} | Debit note ${selectedSettlement.debitNoteNumber}`"
            />
            <div class="payroll-summary">
              <span>Date</span><strong>{{ formatDate(selectedSettlement.onDate) }}</strong>
              <span>Adjusted</span><strong>{{ money(selectedSettlement.adjustedAmount) }}</strong>
              <span>Refunded</span><strong>{{ money(selectedSettlement.refundAmount) }}</strong>
              <span>Total</span><strong>{{ money(selectedSettlement.totalAmount) }}</strong>
              <span>Mode</span><strong>{{ selectedSettlement.paymentMode || '-' }}</strong>
              <span>Bank</span><strong>{{ selectedSettlement.bankAccountName || '-' }}</strong>
              <span>Voucher</span><strong>{{ selectedSettlement.voucherNumber || '-' }}</strong>
              <span>Journal</span><strong>{{ selectedSettlement.journalEntryNumber || '-' }}</strong>
            </div>
            <div v-if="selectedSettlement.allocations?.length" class="planner-table-wrap">
              <table class="planner-table">
                <thead><tr><th>Purchase Invoice</th><th>Adjusted Amount</th></tr></thead>
                <tbody>
                  <tr v-for="allocation in selectedSettlement.allocations" :key="allocation.id">
                    <td>{{ allocation.purchaseInvoiceNumber }}</td>
                    <td>{{ money(allocation.amount) }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
            <UAlert
              v-if="selectedSettlement.remarks"
              color="neutral"
              variant="subtle"
              title="Remarks"
              :description="selectedSettlement.remarks"
            />
          </div>
        </template>
        <template #footer>
          <div class="flex w-full justify-between gap-2">
            <UButton color="neutral" variant="outline" label="Close" @click="detailOpen = false" />
            <UButton
              v-if="selectedSettlement?.voucherId"
              icon="i-lucide-printer"
              label="Print Refund Receipt"
              @click="printRefundVoucher(selectedSettlement.voucherId)"
            />
          </div>
        </template>
      </UModal>
    </section>
  </AppShell>
</template>
