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
const payments = ref<any[]>([])
const lookup = ref<any>({ vendors: [] })
const bankAccounts = ref<any[]>([])
const loading = ref(false)
const saving = ref(false)
const search = ref('')
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
const filteredPayments = computed(() => {
  const term = search.value.trim().toLowerCase()
  if (!term) return payments.value
  return payments.value.filter((row) => JSON.stringify(row).toLowerCase().includes(term))
})
const summary = computed(() => ({
  count: payments.value.length,
  total: payments.value.reduce((sum, item) => sum + Number(item.amount || 0), 0),
  advance: payments.value.filter((item) => item.paymentKind === 'Advance').reduce((sum, item) => sum + Number(item.amount || 0), 0),
  invoice: payments.value.filter((item) => item.paymentKind !== 'Advance').reduce((sum, item) => sum + Number(item.amount || 0), 0)
}))
const columns: TableColumn<any>[] = [
  { accessorKey: 'onDate', header: 'Date' },
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
    header: '',
    cell: ({ row }) => row.original.voucherId ? h(UButton, { color: 'neutral', variant: 'ghost', icon: 'i-lucide-file-text', label: 'Voucher', to: '/vouchers' }) : null
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
      api.get<any[]>('purchase/payments/recent?take=200'),
      api.get<any>('purchase/lookup-options'),
      api.list<any>('bank-accounts')
    ])
    companies.value = companyRows
    stores.value = storeRows
    invoices.value = invoiceRows
    payments.value = paymentRows.map((item) => ({ ...item, amountDisplay: money(Number(item.amount || 0)), onDate: formatDate(item.onDate) }))
    lookup.value = lookupRows
    bankAccounts.value = bankRows
  } catch (error) {
    feedback.failed('Vendor payment refresh failed', error)
  } finally {
    loading.value = false
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
    resetForm()
    await refresh()
  } catch (error) {
    feedback.failed('Could not save vendor payment', error)
  } finally {
    saving.value = false
  }
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
watch(() => form.purchaseInvoiceId, applyInvoice)
watch(() => form.paymentMode, () => { if (requiresBankAccount.value && !form.bankAccountId) form.bankAccountId = bankAccounts.value[0]?.id || null })
watch(() => form.amount, () => { if (requiresBankAccount.value && !form.bankAccountId) form.bankAccountId = bankAccounts.value[0]?.id || null })
onMounted(async () => { auth.restore(); await refresh() })
</script>

<template>
  <AuthScreen v-if="!isAuthenticated" @authenticated="refresh" />
  <AppShell v-else title="Vendor Payments" :companies="companies" :stores="stores" @refresh="refresh" @workspace-change="refresh">
    <section class="planner-dashboard">
      <UiModulePageHeader title="Vendor Payments" description="Record supplier invoice payments, advance payments and review vendor payment history." icon="i-lucide-hand-coins" primary-label="Save Payment" primary-icon="i-lucide-save" @primary="savePayment">
        <template #actions><UButton icon="i-lucide-refresh-cw" variant="subtle" :loading="loading" label="Refresh" @click="refresh" /></template>
      </UiModulePageHeader>
      <div class="planner-metric-grid">
        <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-list" color="primary" variant="subtle" /><div><p>Payments</p><strong>{{ summary.count }}</strong><span>Recent entries</span></div></div></UCard>
        <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-indian-rupee" color="success" variant="subtle" /><div><p>Total paid</p><strong>{{ money(summary.total) }}</strong><span>Invoice + advance</span></div></div></UCard>
        <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-file-check-2" color="neutral" variant="subtle" /><div><p>Invoice linked</p><strong>{{ money(summary.invoice) }}</strong><span>Against bills</span></div></div></UCard>
        <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-wallet-cards" color="warning" variant="subtle" /><div><p>Advance</p><strong>{{ money(summary.advance) }}</strong><span>Vendor advance</span></div></div></UCard>
      </div>

      <UCard class="setup-card">
        <template #header><h2 class="section-title">New vendor payment</h2></template>
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
        <div class="inline-action-row"><UButton icon="i-lucide-save" :loading="saving" label="Save Vendor Payment" @click="savePayment" /></div>
      </UCard>

      <UiRegisterPanel title="Vendor Payment Register" :description="`${filteredPayments.length} of ${payments.length} payments`" :loading="loading" :empty="filteredPayments.length === 0" empty-title="No vendor payments found" empty-description="Record invoice or advance payment to continue." empty-icon="i-lucide-hand-coins" @retry="refresh">
        <template #actions><UiCrudToolbar v-model:search="search" search-placeholder="Search vendor, invoice, reference" refresh-label="Sync" :loading="loading" @refresh="refresh" /></template>
        <div class="planner-table-wrap"><UTable :data="filteredPayments" :columns="columns" /></div>
      </UiRegisterPanel>
    </section>
  </AppShell>
</template>
