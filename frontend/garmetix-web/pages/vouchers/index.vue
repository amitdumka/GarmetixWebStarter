<script setup lang="ts">
import { h, resolveComponent } from 'vue'
import type { TableColumn } from '@nuxt/ui'

const api = useGarmetixApi()
const auth = useAuth()
const feedback = useUiFeedback()
const isAuthenticated = auth.isAuthenticated
const canEdit = auth.canEdit
const canDelete = auth.canDelete

const UBadge = resolveComponent('UBadge')
const UButton = resolveComponent('UButton')

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const vouchers = ref<any[]>([])
const ledgers = ref<any[]>([])
const parties = ref<any[]>([])
const employees = ref<any[]>([])
const bankAccounts = ref<any[]>([])
const setupStatus = ref<any | null>(null)
const loading = ref(false)
const saving = ref(false)
const deleting = ref(false)
const search = ref('')
const formOpen = ref(false)
const deleteOpen = ref(false)
const selectedPrintVoucher = ref<any | null>(null)
const pendingDelete = ref<any | null>(null)
const editMode = ref<'create' | 'edit'>('create')

const voucherTypeOptions = [
  { value: 0, label: 'Payment' },
  { value: 1, label: 'Receipt' },
  { value: 2, label: 'Expense' }
]

const paymentModeOptions = [
  { value: 0, label: 'Cash' },
  { value: 1, label: 'Card' },
  { value: 2, label: 'UPI' },
  { value: 3, label: 'Wallets' },
  { value: 4, label: 'IMPS' },
  { value: 5, label: 'RTGS' },
  { value: 6, label: 'NEFT' },
  { value: 7, label: 'Cheque' },
  { value: 8, label: 'Demand Draft' }
]

const form = reactive<any>(emptyVoucher())

const ledgerOptions = computed(() => ledgers.value.map((ledger) => ({
  value: ledger.id,
  label: ledger.name || 'Ledger'
})))

const partyOptions = computed(() => parties.value.map((party) => ({
  value: party.id,
  label: party.name || 'Party'
})))

const employeeOptions = computed(() => employees.value.map((employee) => ({
  value: employee.id,
  label: `${employee.firstName || ''} ${employee.lastName || ''}`.trim() || employee.staffName || 'Employee'
})))

const bankAccountOptions = computed(() => bankAccounts.value.map((account) => ({
  value: account.id,
  label: `${account.accountHolderName || 'Bank'} - ${account.accountNumber || ''}`.trim()
})))

const requiresBankAccount = computed(() => [1, 2, 3, 4, 5, 6, 7, 8].includes(Number(form.paymentMode)))

const printOpen = computed({
  get: () => Boolean(selectedPrintVoucher.value),
  set: (value: boolean) => {
    if (!value) {
      selectedPrintVoucher.value = null
    }
  }
})

const filteredRows = computed(() => {
  const term = search.value.trim().toLowerCase()
  if (!term) {
    return tableRows.value
  }

  return tableRows.value.filter((row) => JSON.stringify(row).toLowerCase().includes(term))
})

const voucherSummary = computed(() => {
  return vouchers.value.reduce((summary, voucher) => {
    const type = Number(voucher.voucherType)
    const amount = Number(voucher.amount || 0)

    if (type === 0) {
      summary.payments += amount
    } else if (type === 1) {
      summary.receipts += amount
    } else {
      summary.expenses += amount
    }

    summary.total += amount
    return summary
  }, {
    payments: 0,
    receipts: 0,
    expenses: 0,
    total: 0
  })
})

const metrics = computed(() => [
  {
    label: 'Vouchers',
    value: vouchers.value.length,
    meta: 'Accounting entries',
    icon: 'i-lucide-banknote',
    color: 'primary'
  },
  {
    label: 'Payments',
    value: money(voucherSummary.value.payments),
    meta: 'Payment vouchers',
    icon: 'i-lucide-arrow-up-right',
    color: 'warning'
  },
  {
    label: 'Receipts',
    value: money(voucherSummary.value.receipts),
    meta: 'Receipt vouchers',
    icon: 'i-lucide-arrow-down-left',
    color: 'success'
  },
  {
    label: 'Expenses',
    value: money(voucherSummary.value.expenses),
    meta: 'Expense vouchers',
    icon: 'i-lucide-receipt',
    color: 'neutral'
  }
])

const tableRows = computed(() => vouchers.value.map((voucher) => ({
  id: voucher.id,
  voucherNumber: voucher.voucherNumber || '-',
  onDate: formatDate(voucher.onDate),
  voucherType: voucherTypeLabel(voucher.voucherType),
  partyName: voucher.partyName || '-',
  particulars: voucher.particulars || '-',
  paymentMode: paymentModeLabel(voucher.paymentMode),
  amount: money(Number(voucher.amount || 0)),
  issuedBy: employeeName(voucher.employeeId),
  ledgerName: ledgerName(voucher.ledgerId),
  bankAccount: bankAccountName(voucher.accountNumber),
  raw: voucher
})))

const columns: TableColumn<any>[] = [
  { accessorKey: 'voucherNumber', header: 'Voucher' },
  { accessorKey: 'onDate', header: 'Date' },
  {
    accessorKey: 'voucherType',
    header: 'Type',
    cell: ({ row }) => h(UBadge, {
      color: voucherTypeColor(row.original.raw.voucherType),
      variant: 'subtle'
    }, () => row.original.voucherType)
  },
  { accessorKey: 'partyName', header: 'Party' },
  { accessorKey: 'particulars', header: 'Particulars' },
  { accessorKey: 'ledgerName', header: 'Ledger' },
  { accessorKey: 'issuedBy', header: 'Issued By' },
  { accessorKey: 'bankAccount', header: 'Bank' },
  { accessorKey: 'paymentMode', header: 'Mode' },
  { accessorKey: 'amount', header: 'Amount' },
  {
    id: 'actions',
    header: '',
    cell: ({ row }) => h('div', { class: 'table-action-buttons' }, [
      h(UButton, {
        color: 'neutral',
        variant: 'ghost',
        icon: 'i-lucide-printer',
        label: 'Print',
        onClick: () => openPrintVoucher(row.original.raw)
      }),
      canEdit.value ? h(UButton, {
        color: 'neutral',
        variant: 'ghost',
        icon: 'i-lucide-pencil',
        label: 'Edit',
        onClick: () => startEdit(row.original.raw)
      }) : null,
      canDelete.value ? h(UButton, {
        color: 'error',
        variant: 'ghost',
        icon: 'i-lucide-trash-2',
        label: 'Delete',
        onClick: () => askDelete(row.original.raw)
      }) : null
    ].filter(Boolean))
  }
]

function emptyVoucher() {
  return {
    id: '',
    voucherNumber: createVoucherNumber(),
    onDate: new Date().toISOString().slice(0, 10),
    voucherType: 0,
    partyName: '',
    particulars: '',
    amount: 0,
    remarks: '',
    slipNumber: '',
    paymentMode: 0,
    paymentDetails: '',
    isParty: false,
    partyId: null,
    ledgerId: null,
    employeeId: null,
    accountNumber: null
  }
}

function createVoucherNumber() {
  const date = new Date()
  const stamp = date.toISOString().slice(0, 10).replaceAll('-', '')
  const suffix = String(Date.now()).slice(-4)
  return `V-${stamp}-${suffix}`
}

async function refresh() {
  if (!auth.isAuthenticated.value) {
    return
  }

  loading.value = true
  try {
    setupStatus.value = await api.get<any>('setup/status')
    const [companyRows, storeRows, voucherRows, ledgerRows, partyRows, employeeRows, bankAccountRows] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores'),
      api.list<any>('vouchers'),
      api.list<any>('ledgers'),
      api.list<any>('parties'),
      api.list<any>('employees'),
      api.list<any>('bank-accounts')
    ])

    companies.value = companyRows
    stores.value = storeRows
    vouchers.value = voucherRows
    ledgers.value = ledgerRows
    parties.value = partyRows
    employees.value = employeeRows
    bankAccounts.value = bankAccountRows

    if (!ledgers.value.length || !parties.value.length || !bankAccounts.value.length) {
      await api.create<any>('setup/accounting-defaults', {})
      const [refreshedLedgers, refreshedParties, refreshedBankAccounts] = await Promise.all([
        api.list<any>('ledgers'),
        api.list<any>('parties'),
        api.list<any>('bank-accounts')
      ])
      ledgers.value = refreshedLedgers
      parties.value = refreshedParties
      bankAccounts.value = refreshedBankAccounts
    }
  } catch (error) {
    feedback.failed('Vouchers refresh failed', error)
  } finally {
    loading.value = false
  }
}

function startCreate() {
  editMode.value = 'create'
  Object.assign(form, emptyVoucher())
  form.ledgerId = ledgers.value.find((item) => item.name === 'Cash In Hand')?.id || ledgers.value[0]?.id || null
  form.employeeId = employees.value[0]?.id || null
  form.accountNumber = bankAccounts.value[0]?.id || null
  formOpen.value = true
}

function startEdit(voucher: any) {
  editMode.value = 'edit'
  Object.assign(form, {
    ...voucher,
    onDate: String(voucher.onDate || new Date().toISOString()).slice(0, 10),
    ledger: null,
    employee: null,
    party: null,
    bankAccount: null,
    partyId: normalizeGuid(voucher.partyId),
    ledgerId: normalizeGuid(voucher.ledgerId),
    employeeId: normalizeGuid(voucher.employeeId),
    accountNumber: normalizeGuid(voucher.accountNumber)
  })
  formOpen.value = true
}

function buildPayload() {
  const companyId = setupStatus.value?.companyId || companies.value[0]?.id
  const storeGroupId = setupStatus.value?.storeGroupId || stores.value[0]?.storeGroupId
  const storeId = setupStatus.value?.storeId || stores.value[0]?.id

  if (!companyId || !storeGroupId || !storeId) {
    throw new Error('Run quick setup before saving vouchers.')
  }

  if (!String(form.voucherNumber || '').trim()) {
    throw new Error('Voucher number is required.')
  }

  const partyLedger = parties.value.find((item) => item.ledgerId === form.ledgerId)
  if (!String(form.partyName || partyLedger?.name || '').trim()) {
    throw new Error('Enter party name before saving voucher.')
  }

  if (!form.ledgerId) {
    throw new Error('Select ledger before saving voucher.')
  }

  if (!form.employeeId) {
    throw new Error('Select who issued this voucher.')
  }

  if (requiresBankAccount.value && !form.accountNumber) {
    throw new Error('Select bank account for non-cash voucher.')
  }

  const party = parties.value.find((item) => item.ledgerId === form.ledgerId)

  return {
    ...form,
    voucherNumber: String(form.voucherNumber || '').trim(),
    onDate: new Date(`${form.onDate}T00:00:00`).toISOString(),
    voucherType: Number(form.voucherType),
    partyName: String(party?.name || form.partyName || '').trim(),
    particulars: String(form.particulars || '').trim(),
    amount: Number(form.amount || 0),
    paymentMode: Number(form.paymentMode),
    partyId: null,
    ledgerId: nullableGuid(form.ledgerId),
    employeeId: nullableGuid(form.employeeId),
    accountNumber: requiresBankAccount.value ? nullableGuid(form.accountNumber) : null,
    companyId,
    storeGroupId,
    storeId,
    ledger: null,
    employee: null,
    party: null,
    bankAccount: null
  }
}

async function saveVoucher() {
  saving.value = true
  try {
    const payload = buildPayload()
    if (editMode.value === 'edit' && form.id) {
      await api.update<any>('vouchers', form.id, payload)
      feedback.updated('Voucher')
    } else {
      await api.create<any>('vouchers', payload)
      feedback.saved('Voucher')
    }

    formOpen.value = false
    await refresh()
  } catch (error) {
    feedback.failed('Could not save voucher', error)
  } finally {
    saving.value = false
  }
}

function askDelete(voucher: any) {
  pendingDelete.value = voucher
  deleteOpen.value = true
}

function openPrintVoucher(voucher: any) {
  selectedPrintVoucher.value = voucher
}

function printVoucher() {
  window.print()
}

async function confirmDelete() {
  if (!pendingDelete.value) {
    return
  }

  deleting.value = true
  try {
    await api.remove('vouchers', pendingDelete.value.id)
    feedback.deleted('Voucher')
    deleteOpen.value = false
    pendingDelete.value = null
    await refresh()
  } catch (error) {
    feedback.failed('Could not delete voucher', error)
  } finally {
    deleting.value = false
  }
}

function voucherTypeLabel(value: number) {
  return voucherTypeOptions.find((item) => item.value === Number(value))?.label || 'Voucher'
}

function voucherTypeColor(value: number) {
  if (Number(value) === 1) {
    return 'success'
  }

  if (Number(value) === 2) {
    return 'warning'
  }

  return 'primary'
}

function paymentModeLabel(value: number) {
  return paymentModeOptions.find((item) => item.value === Number(value))?.label || 'Other'
}

function employeeName(employeeId: string | null | undefined) {
  const id = normalizeGuid(employeeId)
  if (!id) {
    return '-'
  }

  const employee = employees.value.find((item) => item.id === id)
  return employee ? `${employee.firstName || ''} ${employee.lastName || ''}`.trim() || employee.staffName || 'Employee' : '-'
}

function ledgerName(ledgerId: string | null | undefined) {
  const id = normalizeGuid(ledgerId)
  if (!id) {
    return '-'
  }

  return ledgers.value.find((item) => item.id === id)?.name || '-'
}

function bankAccountName(bankAccountId: string | null | undefined) {
  const id = normalizeGuid(bankAccountId)
  if (!id) {
    return '-'
  }

  const account = bankAccounts.value.find((item) => item.id === id)
  return account ? `${account.accountHolderName || 'Bank'} - ${account.accountNumber || ''}`.trim() : '-'
}

function companyName(companyId: string | null | undefined) {
  const id = normalizeGuid(companyId)
  const company = companies.value.find((item) => item.id === id) || companies.value[0]
  return company?.name || company?.companyName || 'Garmetix'
}

function storeName(storeId: string | null | undefined) {
  const id = normalizeGuid(storeId)
  const store = stores.value.find((item) => item.id === id) || stores.value[0]
  return store?.name || store?.storeName || 'Store'
}

function nullableGuid(value: unknown) {
  return normalizeGuid(value) || null
}

function normalizeGuid(value: unknown) {
  const text = String(value || '').trim()
  return text && text !== '00000000-0000-0000-0000-000000000000' ? text : ''
}

function formatDate(value: string) {
  return value ? new Date(value).toLocaleDateString() : '-'
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

watch(() => form.partyId, (partyId) => {
  const party = parties.value.find((item) => item.id === partyId)
  if (party) {
    form.partyName = party.name
  }
})

watch(() => form.ledgerId, (ledgerId) => {
  const party = parties.value.find((item) => item.ledgerId === ledgerId)
  if (party) {
    form.partyName = party.name
  }
})

watch(() => form.paymentMode, () => {
  if (requiresBankAccount.value && !form.accountNumber) {
    form.accountNumber = bankAccounts.value[0]?.id || null
  }
})
</script>

<template>
  <AuthScreen v-if="!isAuthenticated" @authenticated="refresh" />

  <AppShell
    v-else
    title="Vouchers"
    :companies="companies"
    :stores="stores"
    @refresh="refresh"
  >
    <section class="planner-dashboard">
      <UiModulePageHeader
        title="Accounting Vouchers"
        description="Manage payment, receipt, and expense vouchers with a compact accounting workflow."
        icon="i-lucide-banknote"
        primary-label="New Voucher"
        primary-icon="i-lucide-plus"
        @primary="startCreate"
      >
        <template #actions>
          <UBadge :color="loading ? 'warning' : 'success'" variant="subtle">
            {{ loading ? 'Loading' : `${vouchers.length} vouchers` }}
          </UBadge>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="subtle" :loading="loading" label="Refresh" @click="refresh" />
          <UButton icon="i-lucide-plus" label="New Voucher" @click="startCreate" />
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

      <UCard class="planner-card">
        <template #header>
          <div class="planner-card-header">
            <div>
              <h2>Voucher Register</h2>
              <p>Search voucher number, party name, particulars, or payment mode.</p>
            </div>
            <UBadge color="neutral" variant="subtle">{{ filteredRows.length }} shown</UBadge>
          </div>
        </template>

        <UiCrudToolbar
          v-model:search="search"
          search-placeholder="Search voucher, party, particulars"
          :loading="loading"
          refresh-label="Sync"
          create-label="New Voucher"
          @refresh="refresh"
          @create="startCreate"
        />

        <UTable
          v-if="filteredRows.length"
          :data="filteredRows"
          :columns="columns"
          :loading="loading"
        />

        <UiCrudEmptyState
          v-else
          title="No vouchers found"
          description="Create the first payment, receipt, or expense voucher."
          icon="i-lucide-banknote"
          action-label="New Voucher"
          @action="startCreate"
        />
      </UCard>

      <UiFormSlideover
        v-model:open="formOpen"
        :title="editMode === 'create' ? 'New Voucher' : 'Edit Voucher'"
        description="Save payment, receipt, or expense details."
        :submit-label="editMode === 'create' ? 'Save Voucher' : 'Update Voucher'"
        layout="modal"
        content-class="w-[calc(100vw-2rem)] sm:max-w-5xl lg:max-w-6xl"
        :loading="saving"
        @submit="saveVoucher"
      >
        <UFormField label="Voucher number" required>
          <UInput v-model="form.voucherNumber" required />
        </UFormField>
        <UFormField label="Date" required>
          <UInput v-model="form.onDate" required type="date" />
        </UFormField>
        <div class="form-two-column">
          <UFormField label="Type">
            <USelect v-model="form.voucherType" :items="voucherTypeOptions" />
          </UFormField>
          <UFormField label="Payment mode">
            <USelect v-model="form.paymentMode" :items="paymentModeOptions" />
          </UFormField>
        </div>
        <UFormField v-if="requiresBankAccount" label="Bank account" required>
          <USelect v-model="form.accountNumber" :items="bankAccountOptions" placeholder="Select bank account" />
        </UFormField>
        <UFormField label="Party name" required>
          <UInput v-model="form.partyName" required />
        </UFormField>
        <div class="form-two-column">
          <UFormField label="Ledger" required>
            <USelect v-model="form.ledgerId" :items="ledgerOptions" placeholder="Select ledger" />
          </UFormField>
          <UFormField label="Issued by" required>
            <USelect v-model="form.employeeId" :items="employeeOptions" placeholder="Select employee" />
          </UFormField>
        </div>
        <UFormField label="Amount" required>
          <UInput v-model="form.amount" min="0" step="0.01" required type="number" />
        </UFormField>
        <UFormField label="Particulars" required>
          <UTextarea v-model="form.particulars" autoresize required />
        </UFormField>
        <UFormField label="Payment details">
          <UInput v-model="form.paymentDetails" />
        </UFormField>
        <UFormField label="Slip number">
          <UInput v-model="form.slipNumber" />
        </UFormField>
        <UFormField label="Remarks">
          <UTextarea v-model="form.remarks" autoresize />
        </UFormField>
      </UiFormSlideover>

      <UiConfirmDeleteModal
        v-model:open="deleteOpen"
        title="Delete Voucher"
        :description="`Delete voucher ${pendingDelete?.voucherNumber || ''}?`"
        :loading="deleting"
        @confirm="confirmDelete"
      />

      <UModal v-model:open="printOpen" title="Voucher Print" :ui="{ content: 'max-w-4xl' }">
        <template #body>
          <div v-if="selectedPrintVoucher" class="receipt-print voucher-print">
            <header class="receipt-header">
              <h2>{{ companyName(selectedPrintVoucher.companyId) }}</h2>
              <p>{{ storeName(selectedPrintVoucher.storeId) }}</p>
              <p>{{ voucherTypeLabel(selectedPrintVoucher.voucherType) }} Voucher</p>
            </header>

            <div class="voucher-meta-grid">
              <div>
                <span>Voucher No.</span>
                <strong>{{ selectedPrintVoucher.voucherNumber || '-' }}</strong>
              </div>
              <div>
                <span>Date</span>
                <strong>{{ formatDate(selectedPrintVoucher.onDate) }}</strong>
              </div>
              <div>
                <span>Mode</span>
                <strong>{{ paymentModeLabel(selectedPrintVoucher.paymentMode) }}</strong>
              </div>
              <div>
                <span>Amount</span>
                <strong>{{ money(Number(selectedPrintVoucher.amount || 0)) }}</strong>
              </div>
            </div>

            <table class="receipt-table voucher-table">
              <tbody>
                <tr>
                  <td>Party</td>
                  <td>{{ selectedPrintVoucher.partyName || '-' }}</td>
                </tr>
                <tr>
                  <td>Ledger</td>
                  <td>{{ ledgerName(selectedPrintVoucher.ledgerId) }}</td>
                </tr>
                <tr>
                  <td>Issued by</td>
                  <td>{{ employeeName(selectedPrintVoucher.employeeId) }}</td>
                </tr>
                <tr>
                  <td>Bank account</td>
                  <td>{{ bankAccountName(selectedPrintVoucher.accountNumber) }}</td>
                </tr>
                <tr>
                  <td>Payment details</td>
                  <td>{{ selectedPrintVoucher.paymentDetails || '-' }}</td>
                </tr>
                <tr>
                  <td>Slip number</td>
                  <td>{{ selectedPrintVoucher.slipNumber || '-' }}</td>
                </tr>
                <tr>
                  <td>Particulars</td>
                  <td>{{ selectedPrintVoucher.particulars || '-' }}</td>
                </tr>
                <tr>
                  <td>Remarks</td>
                  <td>{{ selectedPrintVoucher.remarks || '-' }}</td>
                </tr>
              </tbody>
            </table>

            <div class="receipt-totals voucher-total">
              <span>Total voucher amount</span>
              <strong>{{ money(Number(selectedPrintVoucher.amount || 0)) }}</strong>
            </div>

            <div class="voucher-signatures">
              <div>Prepared by</div>
              <div>Checked by</div>
              <div>Approved by</div>
              <div>Received by</div>
            </div>

            <footer class="receipt-footer">
              Generated by Garmetix. Keep this voucher for accounting audit records.
            </footer>
          </div>
        </template>

        <template #footer>
          <div class="modal-actions">
            <UButton color="neutral" variant="outline" label="Close" @click="printOpen = false" />
            <UButton icon="i-lucide-printer" label="Print / PDF" @click="printVoucher" />
          </div>
        </template>
      </UModal>
    </section>
  </AppShell>
</template>
