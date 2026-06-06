<script setup lang="ts">
import { h, resolveComponent } from 'vue'
import type { TableColumn } from '@nuxt/ui'

const api = useGarmetixApi()
const auth = useAuth()
const workspace = useWorkspace()
const feedback = useUiFeedback()
const isAuthenticated = auth.isAuthenticated
const canEdit = auth.canEdit
const canDelete = auth.canDelete

const UBadge = resolveComponent('UBadge')
const UButton = resolveComponent('UButton')

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const transactions = ref<any[]>([])
const employees = ref<any[]>([])
const cashVouchers = ref<any[]>([])
const setupStatus = ref<any | null>(null)
const loading = ref(false)
const saving = ref(false)
const deleting = ref(false)
const search = ref('')
const formOpen = ref(false)
const deleteOpen = ref(false)
const pendingDelete = ref<any | null>(null)
const selectedPrintVoucher = ref<any | null>(null)
const editMode = ref<'create' | 'edit'>('create')
const printFormat = ref<'a4-two' | 'a5-one'>('a4-two')
const isReprint = ref(false)

const voucherTypeOptions = [
  { value: 0, label: 'Payment' },
  { value: 1, label: 'Receipt' },
  { value: 2, label: 'Expense' }
]

const form = reactive<any>(emptyCashVoucher())

const transactionOptions = computed(() => transactions.value
  .filter((item) => !workspace.companyId.value || item.companyId === workspace.companyId.value)
  .map((item) => ({
    value: item.id,
    label: item.name || 'Cash transaction'
  })))

const employeeOptions = computed(() => employees.value
  .filter((item) =>
    (!workspace.companyId.value || item.companyId === workspace.companyId.value)
    && (!workspace.storeId.value || item.storeId === workspace.storeId.value))
  .map((item) => ({
    value: item.id,
    label: employeeDisplayName(item)
  })))

const printCopies = computed(() => printFormat.value === 'a4-two'
  ? ['Office Copy', 'Recipient Copy']
  : ['Cash Voucher Copy'])

const printOpen = computed({
  get: () => Boolean(selectedPrintVoucher.value),
  set: (value: boolean) => {
    if (!value) {
      selectedPrintVoucher.value = null
    }
  }
})

const tableRows = computed(() => cashVouchers.value.map((voucher) => ({
  id: voucher.id,
  voucherNumber: voucher.voucherNumber || '-',
  onDate: formatDate(voucher.onDate),
  voucherType: voucherTypeLabel(voucher.voucherType),
  transaction: transactionName(voucher.transactionId),
  partyName: voucher.partyName || '-',
  particulars: voucher.particulars || '-',
  issuedBy: employeeName(voucher.employeeId),
  amount: money(Number(voucher.amount || 0)),
  raw: voucher
})))

const filteredRows = computed(() => {
  const term = search.value.trim().toLowerCase()
  return term
    ? tableRows.value.filter((row) => JSON.stringify(row).toLowerCase().includes(term))
    : tableRows.value
})

const summary = computed(() => cashVouchers.value.reduce((result, voucher) => {
  const amount = Number(voucher.amount || 0)
  const type = Number(voucher.voucherType)
  if (type === 0) result.payments += amount
  if (type === 1) result.receipts += amount
  if (type === 2) result.expenses += amount
  result.total += amount
  return result
}, { payments: 0, receipts: 0, expenses: 0, total: 0 }))

const metrics = computed(() => [
  {
    label: 'Cash Vouchers',
    value: cashVouchers.value.length,
    meta: 'Off-book records',
    icon: 'i-lucide-wallet-cards',
    color: 'primary'
  },
  {
    label: 'Cash Paid',
    value: money(summary.value.payments),
    meta: 'Payment records',
    icon: 'i-lucide-arrow-up-right',
    color: 'warning'
  },
  {
    label: 'Cash Received',
    value: money(summary.value.receipts),
    meta: 'Receipt records',
    icon: 'i-lucide-arrow-down-left',
    color: 'success'
  },
  {
    label: 'Expenses',
    value: money(summary.value.expenses),
    meta: 'Expense records',
    icon: 'i-lucide-receipt',
    color: 'neutral'
  }
])

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
  { accessorKey: 'transaction', header: 'Category' },
  { accessorKey: 'partyName', header: 'Paid To / From' },
  { accessorKey: 'particulars', header: 'Particulars' },
  { accessorKey: 'issuedBy', header: 'Issued By' },
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
        onClick: () => openPrint(row.original.raw)
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

function emptyCashVoucher() {
  return {
    id: '',
    voucherNumber: createVoucherNumber(),
    onDate: new Date().toISOString().slice(0, 10),
    voucherType: 0,
    transactionId: null,
    partyName: '',
    particulars: '',
    amount: 0,
    remarks: '',
    slipNumber: '',
    employeeId: null
  }
}

function createVoucherNumber() {
  const stamp = new Date().toISOString().slice(0, 10).replaceAll('-', '')
  return `CV-${stamp}-${String(Date.now()).slice(-4)}`
}

async function refresh() {
  if (!auth.isAuthenticated.value) return

  loading.value = true
  try {
    setupStatus.value = await api.get<any>('setup/status')
    const query = new URLSearchParams()
    if (workspace.companyId.value) query.set('companyId', workspace.companyId.value)
    if (workspace.storeGroupId.value) query.set('storeGroupId', workspace.storeGroupId.value)
    if (workspace.storeId.value) query.set('storeId', workspace.storeId.value)
    const cashVoucherResource = query.size ? `cash-vouchers?${query.toString()}` : 'cash-vouchers'

    const [companyRows, storeRows, voucherRows, transactionRows, employeeRows] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores'),
      api.get<any[]>(cashVoucherResource),
      api.list<any>('transactions'),
      api.list<any>('employees')
    ])

    companies.value = companyRows
    stores.value = storeRows
    cashVouchers.value = voucherRows
    transactions.value = transactionRows
    employees.value = employeeRows

    if (!transactionOptions.value.length) {
      const companyId = workspace.companyId.value || auth.user.value?.companyId || setupStatus.value?.companyId || companies.value[0]?.id
      const resource = companyId
        ? `setup/accounting-defaults?companyId=${companyId}`
        : 'setup/accounting-defaults'
      await api.create<any>(resource, {})
      transactions.value = await api.list<any>('transactions')
    }
  } catch (error) {
    feedback.failed('Cash vouchers refresh failed', error)
  } finally {
    loading.value = false
  }
}

function startCreate() {
  editMode.value = 'create'
  Object.assign(form, emptyCashVoucher())
  form.transactionId = transactionOptions.value[0]?.value || null
  form.employeeId = employeeOptions.value[0]?.value || null
  formOpen.value = true
}

function startEdit(voucher: any) {
  editMode.value = 'edit'
  Object.assign(form, {
    ...voucher,
    onDate: String(voucher.onDate || new Date().toISOString()).slice(0, 10),
    transaction: null,
    employee: null,
    ledger: null,
    transactionId: normalizeGuid(voucher.transactionId),
    employeeId: normalizeGuid(voucher.employeeId)
  })
  formOpen.value = true
}

function buildPayload() {
  const selectedStore = stores.value.find((item) => item.id === workspace.storeId.value)
  const companyId = workspace.companyId.value || auth.user.value?.companyId || setupStatus.value?.companyId || companies.value[0]?.id
  const storeGroupId = workspace.storeGroupId.value || auth.user.value?.storeGroupId || selectedStore?.storeGroupId || setupStatus.value?.storeGroupId
  const storeId = workspace.storeId.value || auth.user.value?.storeId || setupStatus.value?.storeId || stores.value[0]?.id

  if (!companyId || !storeGroupId || !storeId) {
    throw new Error('Select a working company and store before saving.')
  }
  if (!String(form.voucherNumber || '').trim()) {
    throw new Error('Voucher number is required.')
  }
  if (!form.transactionId) {
    throw new Error('Select a cash transaction category.')
  }
  if (!String(form.partyName || '').trim()) {
    throw new Error('Enter paid to or received from.')
  }
  if (!String(form.particulars || '').trim()) {
    throw new Error('Enter particulars.')
  }
  if (Number(form.amount || 0) <= 0) {
    throw new Error('Amount must be greater than zero.')
  }

  return {
    voucherNumber: String(form.voucherNumber).trim(),
    onDate: new Date(`${form.onDate}T00:00:00`).toISOString(),
    voucherType: Number(form.voucherType),
    transactionId: normalizeGuid(form.transactionId),
    partyName: String(form.partyName).trim(),
    particulars: String(form.particulars).trim(),
    amount: Number(form.amount),
    remarks: String(form.remarks || '').trim(),
    slipNumber: String(form.slipNumber || '').trim() || null,
    employeeId: normalizeGuid(form.employeeId) || null,
    companyId,
    storeGroupId,
    storeId
  }
}

async function saveCashVoucher() {
  saving.value = true
  try {
    const payload = buildPayload()
    if (editMode.value === 'edit' && form.id) {
      await api.update<any>('cash-vouchers', form.id, payload)
      feedback.updated('Cash voucher')
    } else {
      await api.create<any>('cash-vouchers', payload)
      feedback.saved('Cash voucher')
    }
    formOpen.value = false
    await refresh()
  } catch (error) {
    feedback.failed('Could not save cash voucher', error)
  } finally {
    saving.value = false
  }
}

function askDelete(voucher: any) {
  pendingDelete.value = voucher
  deleteOpen.value = true
}

async function confirmDelete() {
  if (!pendingDelete.value) return

  deleting.value = true
  try {
    await api.remove('cash-vouchers', pendingDelete.value.id)
    feedback.deleted('Cash voucher')
    deleteOpen.value = false
    pendingDelete.value = null
    await refresh()
  } catch (error) {
    feedback.failed('Could not delete cash voucher', error)
  } finally {
    deleting.value = false
  }
}

function openPrint(voucher: any) {
  selectedPrintVoucher.value = voucher
  isReprint.value = false
}

function printCashVoucher() {
  const style = document.createElement('style')
  style.id = 'garmetix-cash-voucher-page-size'
  style.textContent = printFormat.value === 'a5-one'
    ? '@page { size: A5 portrait; margin: 8mm; }'
    : '@page { size: A4 portrait; margin: 8mm; }'
  document.getElementById(style.id)?.remove()
  document.head.appendChild(style)
  window.print()
  window.setTimeout(() => style.remove(), 1000)
}

function voucherTypeLabel(value: number) {
  return voucherTypeOptions.find((item) => item.value === Number(value))?.label || 'Cash'
}

function voucherTypeColor(value: number) {
  if (Number(value) === 1) return 'success'
  if (Number(value) === 2) return 'warning'
  return 'primary'
}

function transactionName(id: string | null | undefined) {
  return transactions.value.find((item) => item.id === normalizeGuid(id))?.name || '-'
}

function employeeDisplayName(employee: any) {
  return `${employee.firstName || ''} ${employee.lastName || ''}`.trim()
    || employee.staffName
    || employee.name
    || 'Employee'
}

function employeeName(id: string | null | undefined) {
  const employee = employees.value.find((item) => item.id === normalizeGuid(id))
  return employee ? employeeDisplayName(employee) : '-'
}

function companyName(id: string | null | undefined) {
  const company = companies.value.find((item) => item.id === normalizeGuid(id)) || companies.value[0]
  return company?.name || company?.companyName || 'Garmetix'
}

function storeName(id: string | null | undefined) {
  const store = stores.value.find((item) => item.id === normalizeGuid(id)) || stores.value[0]
  return store?.name || store?.storeName || 'Store'
}

function normalizeGuid(value: unknown) {
  const text = String(value || '').trim()
  return text && text !== '00000000-0000-0000-0000-000000000000' ? text : ''
}

function formatDate(value: string) {
  return value ? new Date(value).toLocaleDateString('en-IN') : '-'
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
</script>

<template>
  <AuthScreen v-if="!isAuthenticated" @authenticated="refresh" />

  <AppShell
    v-else
    title="Cash Vouchers"
    :companies="companies"
    :stores="stores"
    @refresh="refresh"
    @workspace-change="refresh"
  >
    <section class="planner-dashboard">
      <UiModulePageHeader
        title="Cash Vouchers"
        description="Maintain internal off-book cash payment, receipt, and expense records."
        icon="i-lucide-wallet-cards"
        primary-label="New Cash Voucher"
        primary-icon="i-lucide-plus"
        @primary="startCreate"
      >
        <template #actions>
          <UBadge color="warning" variant="subtle" icon="i-lucide-book-lock">
            Off Book
          </UBadge>
          <UBadge :color="loading ? 'warning' : 'success'" variant="subtle">
            {{ loading ? 'Loading' : `${cashVouchers.length} records` }}
          </UBadge>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="subtle" :loading="loading" label="Refresh" @click="refresh" />
          <UButton icon="i-lucide-plus" label="New Cash Voucher" @click="startCreate" />
        </template>
      </UiModulePageHeader>

      <UAlert
        color="warning"
        variant="subtle"
        icon="i-lucide-info"
        title="Independent cash register"
        description="Cash vouchers in this module do not create voucher, ledger, journal, bank, or accounting entries."
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
        <template #header>
          <div class="planner-card-header">
            <div>
              <h2>Cash Voucher Register</h2>
              <p>Search voucher number, category, person, issuer, or particulars.</p>
            </div>
            <UBadge color="neutral" variant="subtle">{{ filteredRows.length }} shown</UBadge>
          </div>
        </template>

        <UiCrudToolbar
          v-model:search="search"
          search-placeholder="Search cash vouchers"
          :loading="loading"
          refresh-label="Sync"
          create-label="New Cash Voucher"
          @refresh="refresh"
          @create="startCreate"
        />

        <UTable v-if="filteredRows.length" :data="filteredRows" :columns="columns" :loading="loading" />

        <UiCrudEmptyState
          v-else
          title="No cash vouchers found"
          description="Create the first independent off-book cash record."
          icon="i-lucide-wallet-cards"
          action-label="New Cash Voucher"
          @action="startCreate"
        />
      </UCard>

      <UiFormSlideover
        v-model:open="formOpen"
        :title="editMode === 'create' ? 'New Cash Voucher' : 'Edit Cash Voucher'"
        description="This record remains outside the accounting books."
        :submit-label="editMode === 'create' ? 'Save Cash Voucher' : 'Update Cash Voucher'"
        layout="modal"
        content-class="w-[calc(100vw-2rem)] sm:max-w-4xl"
        :loading="saving"
        @submit="saveCashVoucher"
      >
        <div class="form-two-column">
          <UFormField label="Voucher number" required>
            <UInput v-model="form.voucherNumber" required />
          </UFormField>
          <UFormField label="Date" required>
            <UInput v-model="form.onDate" required type="date" />
          </UFormField>
        </div>
        <div class="form-two-column">
          <UFormField label="Type" required>
            <USelect v-model="form.voucherType" :items="voucherTypeOptions" />
          </UFormField>
          <UFormField label="Cash category" required>
            <USelect v-model="form.transactionId" :items="transactionOptions" placeholder="Select category" />
          </UFormField>
        </div>
        <div class="form-two-column">
          <UFormField label="Paid to / Received from" required>
            <UInput v-model="form.partyName" required />
          </UFormField>
          <UFormField label="Issued by">
            <USelect v-model="form.employeeId" :items="employeeOptions" placeholder="Select employee" />
          </UFormField>
        </div>
        <div class="form-two-column">
          <UFormField label="Amount" required>
            <UInput v-model="form.amount" min="0.01" step="0.01" required type="number" />
          </UFormField>
          <UFormField label="Slip number">
            <UInput v-model="form.slipNumber" />
          </UFormField>
        </div>
        <UFormField label="Particulars" required>
          <UTextarea v-model="form.particulars" :rows="4" autoresize required />
        </UFormField>
        <UFormField label="Remarks">
          <UTextarea v-model="form.remarks" :rows="3" autoresize />
        </UFormField>
      </UiFormSlideover>

      <UiConfirmDeleteModal
        v-model:open="deleteOpen"
        title="Delete Cash Voucher"
        :description="`Delete cash voucher ${pendingDelete?.voucherNumber || ''}?`"
        :loading="deleting"
        @confirm="confirmDelete"
      />

      <UModal v-model:open="printOpen" title="Cash Voucher Print" :ui="{ content: 'max-w-5xl' }">
        <template #body>
          <div class="cash-voucher-print-controls no-print">
            <USelect
              v-model="printFormat"
              :items="[
                { value: 'a4-two', label: 'A4 - Two copies' },
                { value: 'a5-one', label: 'A5 - Single copy' }
              ]"
              aria-label="Cash voucher print size"
            />
            <UButton
              color="neutral"
              :variant="isReprint ? 'solid' : 'outline'"
              icon="i-lucide-stamp"
              :label="isReprint ? 'Reprint marked' : 'Mark as reprint'"
              @click="isReprint = !isReprint"
            />
          </div>

          <div
            v-if="selectedPrintVoucher"
            class="receipt-print cash-voucher-print-document"
            :class="`cash-voucher-format-${printFormat}`"
          >
            <section v-for="copyLabel in printCopies" :key="copyLabel" class="voucher-print cash-voucher-copy">
              <div v-if="isReprint" class="cash-voucher-reprint-stamp">REPRINT</div>
              <header class="receipt-header">
                <h2>{{ companyName(selectedPrintVoucher.companyId) }}</h2>
                <p>{{ storeName(selectedPrintVoucher.storeId) }}</p>
                <p>{{ voucherTypeLabel(selectedPrintVoucher.voucherType) }} Cash Voucher</p>
                <strong>{{ copyLabel }} | OFF BOOK</strong>
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
                  <span>Category</span>
                  <strong>{{ transactionName(selectedPrintVoucher.transactionId) }}</strong>
                </div>
                <div>
                  <span>Amount</span>
                  <strong>{{ money(Number(selectedPrintVoucher.amount || 0)) }}</strong>
                </div>
              </div>

              <table class="receipt-table voucher-table">
                <tbody>
                  <tr>
                    <td>Paid to / Received from</td>
                    <td>{{ selectedPrintVoucher.partyName || '-' }}</td>
                  </tr>
                  <tr>
                    <td>Issued by</td>
                    <td>{{ employeeName(selectedPrintVoucher.employeeId) }}</td>
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
                <span>Total cash amount</span>
                <strong>{{ money(Number(selectedPrintVoucher.amount || 0)) }}</strong>
              </div>

              <div class="voucher-signatures">
                <div>Prepared by</div>
                <div>Approved by</div>
                <div>Paid by</div>
                <div>Received by</div>
              </div>

              <footer class="receipt-footer">
                Internal off-book cash record generated by Garmetix.
              </footer>
            </section>
          </div>
        </template>
        <template #footer>
          <div class="modal-actions no-print">
            <UButton color="neutral" variant="outline" label="Close" @click="printOpen = false" />
            <UButton icon="i-lucide-printer" label="Print" @click="printCashVoucher" />
          </div>
        </template>
      </UModal>
    </section>
  </AppShell>
</template>
