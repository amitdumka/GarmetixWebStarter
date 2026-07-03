<script setup lang="ts">
import { h, resolveComponent } from 'vue'
import type { TableColumn } from '@nuxt/ui'

const api = useGarmetixApi()
const auth = useAuth()
const feedback = useUiFeedback()
const route = useRoute()
const isAuthenticated = auth.isAuthenticated

const UBadge = resolveComponent('UBadge')
const UButton = resolveComponent('UButton')

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const invoices = ref<any[]>([])
const payments = ref<any[]>([])
const lookup = ref<any>({ vendors: [] })
const bankAccounts = ref<any[]>([])
const loading = ref(false)
const saving = ref(false)
const deleting = ref(false)
const search = ref('')
const formOpen = ref(false)
const detailOpen = ref(false)
const editOpen = ref(false)
const deleteOpen = ref(false)
const routePaymentOpened = ref(false)
const selectedPayment = ref<any | null>(null)
const paymentType = ref<'invoice' | 'advance'>('invoice')
const form = reactive<any>({
  purchaseInvoiceId: '',
  vendorId: '',
  amount: 0,
  paymentMode: 0,
  bankAccountId: null,
  paymentDetails: '',
  slipNumber: '',
  remarks: ''
})
const editForm = reactive<any>({
  id: '',
  onDate: '',
  amount: 0,
  paymentMode: 0,
  bankAccountId: null,
  paymentDetails: '',
  referenceNumber: '',
  remarks: ''
})

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
const invoiceOptions = computed(() => invoices.value
  .filter((invoice) => Number(invoice.balanceAmount || 0) > 0 && invoice.invoiceStatus !== 'Cancelled')
  .map((invoice) => ({ value: invoice.id, label: `${invoice.invoiceNumber || invoice.inwardNumber} | ${invoice.vendorName} | Due ${money(Number(invoice.balanceAmount || 0))}` })))
const vendorOptions = computed(() => lookup.value.vendors?.map((vendor: any) => ({ value: vendor.id, label: `${vendor.name || 'Vendor'}${vendor.gstin ? ` | ${vendor.gstin}` : ''}` })) || [])
const bankAccountOptions = computed(() => bankAccounts.value.map((account) => ({ value: account.id, label: `${account.accountHolderName || 'Bank'} - ${account.accountNumber || ''}`.trim() })))
const selectedInvoice = computed(() => invoices.value.find((invoice) => invoice.id === form.purchaseInvoiceId) || null)
const requiresBankAccount = computed(() => Number(form.amount || 0) > 0 && Number(form.paymentMode) !== 0)
const editRequiresBankAccount = computed(() => Number(editForm.amount || 0) > 0 && Number(editForm.paymentMode) !== 0)
const filteredPayments = computed(() => {
  const term = search.value.trim().toLowerCase()
  if (!term) return payments.value
  return payments.value.filter((row) => [
    row.vendorName,
    row.purchaseInvoiceNumber,
    row.paymentKind,
    row.paymentMode,
    row.referenceNumber,
    row.voucherNumber,
    row.remarks,
    row.id
  ].join(' ').toLowerCase().includes(term))
})
const summary = computed(() => ({
  count: payments.value.length,
  total: payments.value.reduce((sum, item) => sum + Number(item.amount || 0), 0),
  advance: payments.value.filter((item) => item.paymentKind === 'Advance').reduce((sum, item) => sum + Number(item.amount || 0), 0),
  invoice: payments.value.filter((item) => item.paymentKind !== 'Advance').reduce((sum, item) => sum + Number(item.amount || 0), 0)
}))
const selectedPaymentRows = computed(() => selectedPayment.value ? [
  { label: 'ID', value: selectedPayment.value.id },
  { label: 'Date', value: formatDate(selectedPayment.value.onDate) },
  { label: 'Vendor', value: selectedPayment.value.vendorName },
  { label: 'Type', value: selectedPayment.value.paymentKind },
  { label: 'Invoice / Advance', value: selectedPayment.value.purchaseInvoiceNumber },
  { label: 'Amount', value: money(Number(selectedPayment.value.amount || 0)) },
  { label: 'Mode', value: selectedPayment.value.paymentMode },
  { label: 'Reference', value: selectedPayment.value.referenceNumber || '-' },
  { label: 'Payment details', value: selectedPayment.value.paymentDetails || '-' },
  { label: 'Voucher', value: selectedPayment.value.voucherNumber || '-' },
  { label: 'Remarks', value: selectedPayment.value.remarks || '-' }
] : [])
const columns: TableColumn<any>[] = [
  { accessorKey: 'onDateDisplay', header: 'Date' },
  { accessorKey: 'vendorName', header: 'Vendor' },
  { accessorKey: 'purchaseInvoiceNumber', header: 'Invoice / Advance' },
  {
    accessorKey: 'paymentKind',
    header: 'Type',
    cell: ({ row }) => h(UBadge, { color: row.original.paymentKind === 'Advance' ? 'warning' : 'primary', variant: 'subtle' }, () => row.original.paymentKind)
  },
  { accessorKey: 'paymentMode', header: 'Mode' },
  { accessorKey: 'referenceNumber', header: 'Reference' },
  { accessorKey: 'amountDisplay', header: 'Amount' },
  {
    id: 'actions',
    header: 'Actions',
    cell: ({ row }) => h('div', { class: 'flex flex-wrap gap-1' }, [
      h(UButton, { size: 'xs', color: 'neutral', variant: 'ghost', icon: 'i-lucide-eye', label: 'View', onClick: () => openPayment(row.original) }),
      h(UButton, { size: 'xs', color: 'primary', variant: 'ghost', icon: 'i-lucide-pencil', label: 'Edit', onClick: () => startEditPayment(row.original) }),
      row.original.voucherId ? h(UButton, { size: 'xs', color: 'neutral', variant: 'ghost', icon: 'i-lucide-file-text', label: 'Voucher', to: `/vouchers?voucherId=${row.original.voucherId}` }) : null,
      h(UButton, { size: 'xs', color: 'error', variant: 'ghost', icon: 'i-lucide-trash-2', label: 'Delete', onClick: () => askDeletePayment(row.original) })
    ])
  }
]

async function refresh() {
  if (!auth.isAuthenticated.value) return
  loading.value = true
  try {
    const [companyRows, storeRows, invoiceRows, paymentRows, lookupRows, bankRows] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores'),
      api.get<any[]>('purchase/invoices/recent?take=200'),
      api.get<any[]>('purchase/payments/recent?take=300'),
      api.get<any>('purchase/lookup-options'),
      api.list<any>('bank-accounts')
    ])
    companies.value = companyRows
    stores.value = storeRows
    invoices.value = invoiceRows
    payments.value = paymentRows.map(normalizePaymentRow)
    lookup.value = lookupRows
    bankAccounts.value = bankRows
    await openPaymentFromRoute()
  } catch (error) {
    feedback.failed('Vendor payment refresh failed', error)
  } finally {
    loading.value = false
  }
}

function normalizePaymentRow(item: any) {
  return {
    ...item,
    amountDisplay: money(Number(item.amount || 0)),
    onDateDisplay: formatDate(item.onDate),
    onDateInput: toInputDate(item.onDate)
  }
}

function applyInvoice() {
  if (!selectedInvoice.value) return
  form.vendorId = selectedInvoice.value.vendorId
  form.amount = Number(selectedInvoice.value.balanceAmount || 0)
}

async function savePayment() {
  saving.value = true
  try {
    if (Number(form.amount || 0) <= 0) throw new Error('Payment amount must be greater than zero.')
    if (requiresBankAccount.value && !form.bankAccountId) throw new Error('Select bank account for non-cash payment.')
    if (paymentType.value === 'invoice') {
      if (!form.purchaseInvoiceId) throw new Error('Select purchase invoice to pay.')
      await api.create<any>(`purchase/invoices/${form.purchaseInvoiceId}/payment-voucher`, {
        amount: Number(form.amount || 0),
        paymentMode: Number(form.paymentMode),
        bankAccountId: form.bankAccountId || null,
        paymentDetails: form.paymentDetails,
        slipNumber: form.slipNumber,
        remarks: form.remarks
      })
    } else {
      if (!form.vendorId) throw new Error('Select vendor for advance payment.')
      await api.create<any>('purchase/payments/advance', {
        vendorId: form.vendorId,
        amount: Number(form.amount || 0),
        paymentMode: Number(form.paymentMode),
        bankAccountId: form.bankAccountId || null,
        paymentDetails: form.paymentDetails,
        slipNumber: form.slipNumber,
        remarks: form.remarks
      })
    }
    feedback.notify('Vendor payment saved', undefined, 'success')
    formOpen.value = false
    resetForm()
    await refresh()
  } catch (error) {
    feedback.failed('Could not save vendor payment', error)
  } finally {
    saving.value = false
  }
}
function startCreatePayment(type: 'invoice' | 'advance' = 'invoice') {
  paymentType.value = type
  resetForm()
  formOpen.value = true
}

async function openPayment(row: any) {
  if (!row?.id) return
  try {
    selectedPayment.value = normalizePaymentRow(await api.get<any>(`purchase/payments/${row.id}`))
    detailOpen.value = true
  } catch (error) {
    feedback.failed('Could not open vendor payment', error)
  }
}

async function startEditPayment(row: any) {
  if (!row?.id) return
  try {
    const payment = normalizePaymentRow(await api.get<any>(`purchase/payments/${row.id}`))
    selectedPayment.value = payment
    editForm.id = payment.id
    editForm.onDate = payment.onDateInput
    editForm.amount = Number(payment.amount || 0)
    editForm.paymentMode = Number(payment.paymentModeValue ?? 0)
    editForm.bankAccountId = payment.bankAccountId || null
    editForm.paymentDetails = payment.paymentDetails || ''
    editForm.referenceNumber = payment.referenceNumber || ''
    editForm.remarks = payment.remarks || ''
    editOpen.value = true
  } catch (error) {
    feedback.failed('Could not prepare vendor payment edit', error)
  }
}

async function saveEditPayment() {
  if (!editForm.id) return
  saving.value = true
  try {
    if (Number(editForm.amount || 0) <= 0) throw new Error('Payment amount must be greater than zero.')
    if (editRequiresBankAccount.value && !editForm.bankAccountId) throw new Error('Select bank account for non-cash payment.')
    await api.update<any>('purchase/payments', editForm.id, {
      onDate: editForm.onDate,
      amount: Number(editForm.amount || 0),
      paymentMode: Number(editForm.paymentMode),
      bankAccountId: Number(editForm.paymentMode) === 0 ? null : editForm.bankAccountId,
      paymentDetails: editForm.paymentDetails,
      referenceNumber: editForm.referenceNumber,
      remarks: editForm.remarks
    })
    feedback.notify('Vendor payment updated', undefined, 'success')
    editOpen.value = false
    await refresh()
    const updated = payments.value.find((item) => item.id === editForm.id)
    if (updated) selectedPayment.value = updated
  } catch (error) {
    feedback.failed('Could not update vendor payment', error)
  } finally {
    saving.value = false
  }
}

function askDeletePayment(row: any) {
  selectedPayment.value = row
  deleteOpen.value = true
}

async function confirmDeletePayment() {
  if (!selectedPayment.value?.id) return
  deleting.value = true
  try {
    await api.remove('purchase/payments', selectedPayment.value.id)
    feedback.notify('Vendor payment deleted and linked voucher/accounting entries reversed', undefined, 'success')
    deleteOpen.value = false
    detailOpen.value = false
    await refresh()
  } catch (error) {
    feedback.failed('Could not delete vendor payment', error)
  } finally {
    deleting.value = false
  }
}

function openCreatePaymentFromRoute() {
  const createRequested = String(route.query.new || route.query.create || '')
  if (createRequested === 'advance') {
    startCreatePayment('advance')
  } else if (createRequested === 'invoice' || createRequested === '1' || createRequested.toLowerCase() === 'true') {
    startCreatePayment('invoice')
  }
}

async function openPaymentFromRoute() {
  const paymentId = String(route.query.paymentId || route.query.vendorPaymentId || '')
  if (!paymentId || routePaymentOpened.value) return
  routePaymentOpened.value = true
  const row = payments.value.find((item) => item.id === paymentId) || { id: paymentId }
  await openPayment(row)
}

function resetForm() {
  form.purchaseInvoiceId = ''
  form.vendorId = ''
  form.amount = 0
  form.paymentMode = 0
  form.bankAccountId = null
  form.paymentDetails = ''
  form.slipNumber = ''
  form.remarks = ''
}
function money(value: number) { return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(value || 0) }
function formatDate(value: string) { return value ? new Date(value).toLocaleDateString('en-IN') : '-' }
function toInputDate(value: string) {
  if (!value) return ''
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return ''
  date.setMinutes(date.getMinutes() - date.getTimezoneOffset())
  return date.toISOString().slice(0, 10)
}
watch(() => form.purchaseInvoiceId, applyInvoice)
watch(() => form.paymentMode, () => { if (requiresBankAccount.value && !form.bankAccountId) form.bankAccountId = bankAccounts.value[0]?.id || null })
watch(() => form.amount, () => { if (requiresBankAccount.value && !form.bankAccountId) form.bankAccountId = bankAccounts.value[0]?.id || null })
watch(() => editForm.paymentMode, () => { if (editRequiresBankAccount.value && !editForm.bankAccountId) editForm.bankAccountId = bankAccounts.value[0]?.id || null })
watch(() => editForm.amount, () => { if (editRequiresBankAccount.value && !editForm.bankAccountId) editForm.bankAccountId = bankAccounts.value[0]?.id || null })
onMounted(async () => { auth.restore(); await refresh(); openCreatePaymentFromRoute() })
watch(() => route.fullPath, async () => { routePaymentOpened.value = false; openCreatePaymentFromRoute(); await openPaymentFromRoute() })
</script>

<template>
  <AuthScreen v-if="!isAuthenticated" @authenticated="refresh" />
  <AppShell v-else title="Vendor Payments" :companies="companies" :stores="stores" @refresh="refresh" @workspace-change="refresh">
    <section class="planner-dashboard">
      <UiModulePageHeader title="Vendor Payments" description="Record supplier invoice payments, advance payments and review vendor payment history." icon="i-lucide-hand-coins" primary-label="Add New Vendor Payment" primary-icon="i-lucide-plus" @primary="startCreatePayment()">
        <template #actions><div class="header-actions"><UButton to="/purchase/vendor-payable-reconciliation" icon="i-lucide-shield-check" variant="subtle" label="Payable Reco" /><UButton icon="i-lucide-refresh-cw" variant="subtle" :loading="loading" label="Refresh" @click="refresh" /></div></template>
      </UiModulePageHeader>

      <UiDayBookReturnButton />

      <div class="planner-metric-grid">
        <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-list" color="primary" variant="subtle" /><div><p>Payments</p><strong>{{ summary.count }}</strong><span>Recent entries</span></div></div></UCard>
        <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-indian-rupee" color="success" variant="subtle" /><div><p>Total paid</p><strong>{{ money(summary.total) }}</strong><span>Invoice + advance</span></div></div></UCard>
        <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-file-check-2" color="neutral" variant="subtle" /><div><p>Invoice linked</p><strong>{{ money(summary.invoice) }}</strong><span>Against bills</span></div></div></UCard>
        <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-wallet-cards" color="warning" variant="subtle" /><div><p>Advance</p><strong>{{ money(summary.advance) }}</strong><span>Vendor advance</span></div></div></UCard>
      </div>

      <UiFormSlideover
        v-model:open="formOpen"
        title="Add New Vendor Payment"
        description="Record supplier invoice payments or vendor advance payments."
        submit-label="Save Vendor Payment"
        :loading="saving"
        @submit="savePayment"
      >
        <div class="setup-tabs">
          <UButton label="Against invoice" icon="i-lucide-file-text" :color="paymentType === 'invoice' ? 'primary' : 'neutral'" :variant="paymentType === 'invoice' ? 'solid' : 'subtle'" @click="paymentType = 'invoice'" />
          <UButton label="Advance payment" icon="i-lucide-wallet" :color="paymentType === 'advance' ? 'primary' : 'neutral'" :variant="paymentType === 'advance' ? 'solid' : 'subtle'" @click="paymentType = 'advance'" />
        </div>
        <div class="form-three-column">
          <UFormField v-if="paymentType === 'invoice'" label="Purchase invoice"><USelect v-model="form.purchaseInvoiceId" :items="invoiceOptions" placeholder="Select due invoice" /></UFormField>
          <UFormField v-else label="Vendor"><USelect v-model="form.vendorId" :items="vendorOptions" placeholder="Select vendor" /></UFormField>
          <UFormField label="Amount"><UInput v-model="form.amount" type="number" min="0" /></UFormField>
          <UFormField label="Payment mode"><USelect v-model="form.paymentMode" :items="paymentModeOptions" /></UFormField>
        </div>
        <div class="form-three-column">
          <UFormField v-if="requiresBankAccount" label="Bank account"><USelect v-model="form.bankAccountId" :items="bankAccountOptions" /></UFormField>
          <UFormField label="Slip / reference"><UInput v-model="form.slipNumber" /></UFormField>
          <UFormField label="Payment details"><UInput v-model="form.paymentDetails" /></UFormField>
        </div>
        <UFormField label="Remarks"><UTextarea v-model="form.remarks" autoresize /></UFormField>
      </UiFormSlideover>

      <UiFormSlideover
        v-model:open="editOpen"
        title="Edit Vendor Payment"
        description="Correct vendor payment date, amount, mode, reference and remarks. Linked voucher/accounting state is recalculated after save."
        submit-label="Update Vendor Payment"
        :loading="saving"
        @submit="saveEditPayment"
      >
        <UAlert color="warning" variant="subtle" icon="i-lucide-info" title="Safe correction" description="Invoice balances, vendor paid totals and linked voucher details will be recalculated after this edit." />
        <div class="form-three-column">
          <UFormField label="Payment date"><UInput v-model="editForm.onDate" type="date" /></UFormField>
          <UFormField label="Amount"><UInput v-model="editForm.amount" type="number" min="0" /></UFormField>
          <UFormField label="Payment mode"><USelect v-model="editForm.paymentMode" :items="paymentModeOptions" /></UFormField>
        </div>
        <div class="form-three-column">
          <UFormField v-if="editRequiresBankAccount" label="Bank account"><USelect v-model="editForm.bankAccountId" :items="bankAccountOptions" /></UFormField>
          <UFormField label="Reference / slip"><UInput v-model="editForm.referenceNumber" /></UFormField>
          <UFormField label="Payment details"><UInput v-model="editForm.paymentDetails" /></UFormField>
        </div>
        <UFormField label="Remarks"><UTextarea v-model="editForm.remarks" autoresize /></UFormField>
      </UiFormSlideover>

      <USlideover v-model:open="detailOpen" :ui="{ content: 'w-full sm:max-w-3xl lg:max-w-5xl' }">
        <template #content>
          <div class="space-y-4 p-4">
            <div class="flex items-start justify-between gap-3">
              <div>
                <p class="text-xs uppercase text-muted">Vendor Payment Detail</p>
                <h2 class="text-lg font-semibold">{{ selectedPayment?.vendorName || 'Vendor Payment' }}</h2>
                <p class="text-sm text-muted">{{ selectedPayment?.paymentKind }} · {{ selectedPayment?.purchaseInvoiceNumber }}</p>
                <p v-if="selectedPayment?.id" class="text-xs text-muted">ID: <span class="font-mono">{{ selectedPayment.id }}</span></p>
              </div>
              <UButton icon="i-lucide-x" variant="ghost" @click="detailOpen = false" />
            </div>
            <div class="grid gap-3 md:grid-cols-3">
              <UCard><p class="text-xs text-muted">Amount</p><strong>{{ money(Number(selectedPayment?.amount || 0)) }}</strong></UCard>
              <UCard><p class="text-xs text-muted">Mode</p><strong>{{ selectedPayment?.paymentMode || '-' }}</strong></UCard>
              <UCard><p class="text-xs text-muted">Voucher</p><strong>{{ selectedPayment?.voucherNumber || '-' }}</strong></UCard>
            </div>
            <UCard>
              <div class="planner-table-wrap">
                <table class="min-w-full text-sm">
                  <tbody>
                    <tr v-for="item in selectedPaymentRows" :key="item.label" class="border-b">
                      <th class="w-44 p-2 text-left text-muted align-top">{{ item.label }}</th>
                      <td class="p-2 align-top break-all">{{ item.value }}</td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </UCard>
            <div class="flex flex-wrap justify-end gap-2">
              <UButton variant="subtle" label="Close" @click="detailOpen = false" />
              <UButton icon="i-lucide-pencil" label="Edit" @click="selectedPayment && startEditPayment(selectedPayment)" />
              <UButton v-if="selectedPayment?.voucherId" icon="i-lucide-file-text" label="Open Voucher" variant="subtle" :to="`/vouchers?voucherId=${selectedPayment.voucherId}`" />
              <UButton icon="i-lucide-trash-2" color="error" variant="subtle" label="Delete" @click="selectedPayment && askDeletePayment(selectedPayment)" />
            </div>
          </div>
        </template>
      </USlideover>

      <UiConfirmDeleteModal
        v-model:open="deleteOpen"
        title="Delete vendor payment?"
        :description="`Delete ${selectedPayment?.vendorName || 'this vendor payment'} ${selectedPayment?.amountDisplay || ''}? Linked voucher, bank and accounting entries will be reversed from active views.`"
        confirm-label="Delete Payment"
        :loading="deleting"
        @confirm="confirmDeletePayment"
      />

      <UiRegisterPanel title="Vendor Payment Register" :description="`${filteredPayments.length} of ${payments.length} payments`" :loading="loading" :empty="filteredPayments.length === 0" empty-title="No vendor payments found" empty-description="Record invoice or advance payment to continue." empty-icon="i-lucide-hand-coins" @retry="refresh">
        <template #actions><UiCrudToolbar v-model:search="search" search-placeholder="Search vendor, invoice, reference, ID" refresh-label="Sync" create-label="Add Payment" :loading="loading" @refresh="refresh" @create="startCreatePayment()" /></template>
        <div class="planner-table-wrap"><UTable :data="filteredPayments" :columns="columns" /></div>
      </UiRegisterPanel>
    </section>
  </AppShell>
</template>
