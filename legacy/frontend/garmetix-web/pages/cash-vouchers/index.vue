<script setup lang="ts">
import { h, resolveComponent } from 'vue'
import type { TableColumn } from '@nuxt/ui'

const api = useGarmetixApi()
const auth = useAuth()
const workspace = useWorkspace()
const feedback = useUiFeedback()
const documentPrint = useServerDocumentPrint()
const config = useRuntimeConfig()
const isAuthenticated = auth.isAuthenticated
const canEdit = auth.canEdit
const canDelete = auth.canDelete
const canConvert = auth.canSeeAdmin

const UBadge = resolveComponent('UBadge')
const UButton = resolveComponent('UButton')

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const transactions = ref<any[]>([])
const employees = ref<any[]>([])
const ledgers = ref<any[]>([])
const cashVouchers = ref<any[]>([])
const eligibleOnBookVouchers = ref<any[]>([])
const conversions = ref<any[]>([])
const setupStatus = ref<any | null>(null)
const loading = ref(false)
const loadError = ref('')
const saving = ref(false)
const deleting = ref(false)
const downloadingPdf = ref(false)
const search = ref('')
const voucherTypeFilter = ref('__all__')
const formOpen = ref(false)
const deleteOpen = ref(false)
const pendingDelete = ref<any | null>(null)
const selectedPrintVoucher = ref<any | null>(null)
const editMode = ref<'create' | 'edit'>('create')
const printFormat = ref<'a4-two' | 'a5-one'>('a4-two')
const isReprint = ref(false)
const includeSignatures = ref(true)
const conversionOpen = ref(false)
const converting = ref(false)
const conversionDirection = ref<'toBooks' | 'toOffBook'>('toBooks')
const conversionSource = ref<any | null>(null)
const conversionForm = reactive({
  ledgerId: null as string | null,
  employeeId: null as string | null,
  transactionId: null as string | null,
  reason: ''
})

const voucherTypeOptions = [
  { value: 0, label: 'Payment' },
  { value: 1, label: 'Receipt' },
  { value: 2, label: 'Expense' }
]
const voucherTypeFilterOptions = [
  { value: '__all__', label: 'All voucher types' },
  ...voucherTypeOptions
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

const ledgerOptions = computed(() => ledgers.value
  .filter((item) => !workspace.companyId.value || item.companyId === workspace.companyId.value)
  .map((item) => ({
    value: item.id,
    label: item.name || 'Ledger'
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
  return tableRows.value.filter((row) => {
    const matchesSearch = !term || JSON.stringify(row).toLowerCase().includes(term)
    const matchesType = voucherTypeFilter.value === '__all__'
      || Number(row.raw.voucherType) === Number(voucherTypeFilter.value)
    return matchesSearch && matchesType
  })
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
      canConvert.value ? h(UButton, {
        color: 'primary',
        variant: 'soft',
        icon: 'i-lucide-book-up',
        label: 'Move to Books',
        onClick: () => startConversionToBooks(row.original.raw)
      }) : null,
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
    onDate: localDateValue(),
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
  const stamp = localDateValue().replaceAll('-', '')
  return `CV-${stamp}-${String(Date.now()).slice(-4)}`
}

async function refresh() {
  if (!auth.isAuthenticated.value) return

  loading.value = true
  loadError.value = ''
  try {
    try {
      setupStatus.value = await api.get<any>('setup/status')
    } catch {
      setupStatus.value = null
    }

    const query = new URLSearchParams()
    if (workspace.companyId.value) query.set('companyId', workspace.companyId.value)
    if (workspace.storeGroupId.value) query.set('storeGroupId', workspace.storeGroupId.value)
    if (workspace.storeId.value) query.set('storeId', workspace.storeId.value)
    const cashVoucherResource = query.size ? `cash-vouchers?${query.toString()}` : 'cash-vouchers'

    const voucherRows = await api.get<any[]>(cashVoucherResource)
    cashVouchers.value = Array.isArray(voucherRows) ? voucherRows : []

    const [companyRows, storeRows, transactionRows, employeeRows, ledgerRows] = await Promise.all([
      safeList('companies'),
      safeList('stores'),
      safeList('transactions'),
      safeList('employees'),
      safeList('ledgers')
    ])

    companies.value = companyRows
    stores.value = storeRows
    transactions.value = transactionRows
    employees.value = employeeRows
    ledgers.value = ledgerRows

    if (!transactionOptions.value.length) {
      const companyId = workspace.companyId.value || auth.user.value?.companyId || setupStatus.value?.companyId || companies.value[0]?.id
      const resource = companyId
        ? `setup/accounting-defaults?companyId=${companyId}`
        : 'setup/accounting-defaults'
      try {
        await api.create<any>(resource, {})
        transactions.value = await safeList('transactions')
      } catch {
        // Keep the register visible even if default cash categories cannot be repaired for this role.
      }
    }

    if (canConvert.value) {
      const [conversionRows, onBookRows] = await Promise.all([
        safeGetArray(query.size ? `cash-vouchers/conversions?${query.toString()}` : 'cash-vouchers/conversions'),
        safeGetArray(query.size ? `cash-vouchers/eligible-on-book?${query.toString()}` : 'cash-vouchers/eligible-on-book')
      ])
      conversions.value = conversionRows
      eligibleOnBookVouchers.value = onBookRows
    } else {
      conversions.value = []
      eligibleOnBookVouchers.value = []
    }
  } catch (error) {
    loadError.value = 'Cash voucher records could not be loaded. Check the selected workspace and try again.'
    feedback.failed('Cash vouchers refresh failed', error)
  } finally {
    loading.value = false
  }
}

async function safeList(resource: string) {
  try {
    const rows = await api.list<any>(resource)
    return Array.isArray(rows) ? rows : []
  } catch {
    return []
  }
}

async function safeGetArray(resource: string) {
  try {
    const rows = await api.get<any[]>(resource)
    return Array.isArray(rows) ? rows : []
  } catch {
    return []
  }
}

function startConversionToBooks(voucher: any) {
  conversionDirection.value = 'toBooks'
  conversionSource.value = voucher
  conversionForm.ledgerId = ledgerOptions.value[0]?.value || null
  conversionForm.employeeId = normalizeGuid(voucher.employeeId) || employeeOptions.value[0]?.value || null
  conversionForm.transactionId = null
  conversionForm.reason = ''
  conversionOpen.value = true
}

function startConversionToOffBook(voucher: any) {
  conversionDirection.value = 'toOffBook'
  conversionSource.value = voucher
  conversionForm.ledgerId = null
  conversionForm.employeeId = null
  conversionForm.transactionId = transactionOptions.value[0]?.value || null
  conversionForm.reason = ''
  conversionOpen.value = true
}

async function submitConversion() {
  if (!conversionSource.value?.id) return
  if (conversionForm.reason.trim().length < 8) {
    feedback.notify('Enter an audit reason of at least 8 characters.', undefined, 'warning')
    return
  }

  converting.value = true
  try {
    if (conversionDirection.value === 'toBooks') {
      if (!conversionForm.ledgerId || !conversionForm.employeeId) {
        throw new Error('Select the accounting ledger and issued-by employee.')
      }
      await api.create<any>(`cash-vouchers/${conversionSource.value.id}/convert-to-books`, {
        ledgerId: conversionForm.ledgerId,
        employeeId: conversionForm.employeeId,
        reason: conversionForm.reason.trim()
      })
      feedback.saved('Cash voucher moved to Books')
    } else {
      if (!conversionForm.transactionId) {
        throw new Error('Select the Off Book cash category.')
      }
      await api.create<any>(`cash-vouchers/from-voucher/${conversionSource.value.id}`, {
        transactionId: conversionForm.transactionId,
        reason: conversionForm.reason.trim()
      })
      feedback.saved('Voucher moved to Off Book')
    }
    conversionOpen.value = false
    conversionSource.value = null
    await refresh()
  } catch (error) {
    feedback.failed('Could not convert voucher', error)
  } finally {
    converting.value = false
  }
}

function directionLabel(value: string) {
  return value === 'OffBookToOnBook' ? 'Off Book to Books' : 'Books to Off Book'
}

function formatDateTime(value: unknown) {
  const date = new Date(String(value || ''))
  return Number.isNaN(date.getTime()) ? '-' : date.toLocaleString('en-IN')
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
    onDate: dateInputValue(voucher.onDate),
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
    onDate: accountingDateTimeForApi(form.onDate),
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
    let createdVoucher: any | null = null
    if (editMode.value === 'edit' && form.id) {
      await api.update<any>('cash-vouchers', form.id, payload)
      feedback.updated('Cash voucher')
    } else {
      createdVoucher = await api.create<any>('cash-vouchers', payload)
      feedback.saved('Cash voucher')
    }
    formOpen.value = false
    await refresh()
    if (createdVoucher?.id) {
      openPrint(createdVoucher)
      await printCashVoucher()
    }
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
  printFormat.value = 'a4-two'
  isReprint.value = false
  includeSignatures.value = true
}

async function printCashVoucher() {
  if (!selectedPrintVoucher.value?.id) return
  const query = new URLSearchParams({
    format: printFormat.value,
    reprint: String(isReprint.value),
    signatures: String(includeSignatures.value)
  })
  try {
    await documentPrint.printPdf(`cash-vouchers/${selectedPrintVoucher.value.id}/pdf?${query.toString()}`)
  } catch (error) {
    feedback.failed('Could not print cash voucher PDF', error)
  }
}


async function downloadCashVoucherPdf() {
  if (!selectedPrintVoucher.value?.id) {
    return
  }

  downloadingPdf.value = true
  try {
    const query = new URLSearchParams({
      format: printFormat.value,
      reprint: String(isReprint.value),
      signatures: String(includeSignatures.value)
    })
    const response = await fetch(
      `${config.public.apiBase}/cash-vouchers/${selectedPrintVoucher.value.id}/pdf?${query.toString()}`,
      { headers: api.authHeaders() }
    )
    if (!response.ok) {
      throw new Error(`Cash voucher PDF could not be generated (${response.status}).`)
    }

    const blob = await response.blob()
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = `${selectedPrintVoucher.value.voucherNumber || 'cash-voucher'}.pdf`
    document.body.appendChild(link)
    link.click()
    link.remove()
    URL.revokeObjectURL(url)
    feedback.notify('Cash voucher PDF downloaded')
  } catch (error) {
    feedback.failed('Could not download cash voucher PDF', error)
  } finally {
    downloadingPdf.value = false
  }
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
  const date = parseLocalDate(value)
  return date ? date.toLocaleDateString('en-IN') : '-'
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

function parseLocalDate(value: unknown) {
  const text = dateInputValue(value)
  const [year, month, day] = text.split('-').map(Number)
  return year && month && day ? new Date(year, month - 1, day) : null
}

function accountingDateTimeForApi(value: unknown) {
  return `${dateInputValue(value)}T00:00:00`
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

      <UiRegisterPanel
        title="Cash Voucher Register"
        description="Search voucher number, category, person, issuer, particulars, or voucher type."
        :loading="loading"
        :error="loadError"
        :empty="!filteredRows.length"
        empty-title="No cash vouchers found"
        empty-description="Create the first independent off-book cash record."
        empty-icon="i-lucide-wallet-cards"
        @retry="refresh"
      >
        <template #actions>
            <UBadge color="neutral" variant="subtle">{{ filteredRows.length }} shown</UBadge>
        </template>

        <UiCrudToolbar
          v-model:search="search"
          search-placeholder="Search cash vouchers"
          :loading="loading"
          refresh-label="Sync"
          create-label="New Cash Voucher"
          @refresh="refresh"
          @create="startCreate"
        >
          <template #filters>
            <USelect
              v-model="voucherTypeFilter"
              :items="voucherTypeFilterOptions"
              aria-label="Filter cash vouchers by type"
              class="min-w-44"
            />
          </template>
        </UiCrudToolbar>

        <UTable v-if="filteredRows.length" :data="filteredRows" :columns="columns" :loading="loading" />
      </UiRegisterPanel>

      <UiRegisterPanel
        v-if="canConvert"
        title="Owner Conversion Audit"
        description="Move eligible cash records between Off Book and accounting with a permanent reason and operator trail."
        :loading="loading"
        :empty="!eligibleOnBookVouchers.length && !conversions.length"
        empty-title="No conversion activity"
        empty-description="Eligible cash accounting vouchers and completed conversions will appear here."
        empty-icon="i-lucide-shield-check"
        @retry="refresh"
      >
        <div v-if="eligibleOnBookVouchers.length" class="conversion-section">
          <div class="conversion-section-heading">
            <div>
              <h3>Eligible cash accounting vouchers</h3>
              <p>Only cash payment, receipt, and expense vouchers can move Off Book.</p>
            </div>
            <UBadge color="primary" variant="subtle">{{ eligibleOnBookVouchers.length }} eligible</UBadge>
          </div>
          <div class="conversion-table-wrap">
            <table class="conversion-table">
              <thead>
                <tr>
                  <th>Voucher</th>
                  <th>Date</th>
                  <th>Type</th>
                  <th>Party</th>
                  <th>Amount</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                <tr v-for="voucher in eligibleOnBookVouchers" :key="voucher.id">
                  <td>{{ voucher.voucherNumber }}</td>
                  <td>{{ formatDate(voucher.onDate) }}</td>
                  <td>{{ voucherTypeLabel(voucher.voucherType) }}</td>
                  <td>{{ voucher.partyName }}</td>
                  <td>{{ money(Number(voucher.amount || 0)) }}</td>
                  <td>
                    <UButton
                      size="sm"
                      color="warning"
                      variant="soft"
                      icon="i-lucide-book-down"
                      label="Move Off Book"
                      @click="startConversionToOffBook(voucher)"
                    />
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>

        <div v-if="conversions.length" class="conversion-section">
          <div class="conversion-section-heading">
            <div>
              <h3>Immutable conversion history</h3>
              <p>Source and destination numbers remain linked even after the source leaves the active register.</p>
            </div>
            <UBadge color="neutral" variant="subtle">{{ conversions.length }} audited</UBadge>
          </div>
          <div class="conversion-table-wrap">
            <table class="conversion-table">
              <thead>
                <tr>
                  <th>Converted</th>
                  <th>Direction</th>
                  <th>Off Book</th>
                  <th>On Book</th>
                  <th>Amount</th>
                  <th>Operator</th>
                  <th>Reason</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="entry in conversions" :key="entry.id">
                  <td>{{ formatDateTime(entry.convertedAt) }}</td>
                  <td><UBadge color="primary" variant="subtle">{{ directionLabel(entry.direction) }}</UBadge></td>
                  <td>{{ entry.cashVoucherNumber }}</td>
                  <td>{{ entry.voucherNumber }}</td>
                  <td>{{ money(Number(entry.amount || 0)) }}</td>
                  <td>{{ entry.convertedByUserName }}</td>
                  <td>{{ entry.reason }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </UiRegisterPanel>

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

      <UModal v-model:open="conversionOpen" :title="conversionDirection === 'toBooks' ? 'Move Cash Voucher to Books' : 'Move Voucher Off Book'">
        <template #body>
          <div class="conversion-dialog">
            <UAlert
              color="warning"
              variant="subtle"
              icon="i-lucide-shield-alert"
              title="Owner approval required"
              description="The source will leave the active register but remain retained for audit. This action does not duplicate the amount."
            />
            <div class="conversion-source-summary">
              <span>{{ conversionSource?.voucherNumber || '-' }}</span>
              <strong>{{ money(Number(conversionSource?.amount || 0)) }}</strong>
              <small>{{ conversionSource?.partyName || '-' }}</small>
            </div>
            <template v-if="conversionDirection === 'toBooks'">
              <UFormField label="Accounting ledger" required>
                <USelect v-model="conversionForm.ledgerId" :items="ledgerOptions" placeholder="Select ledger" />
              </UFormField>
              <UFormField label="Issued by" required>
                <USelect v-model="conversionForm.employeeId" :items="employeeOptions" placeholder="Select employee" />
              </UFormField>
            </template>
            <UFormField v-else label="Off Book cash category" required>
              <USelect v-model="conversionForm.transactionId" :items="transactionOptions" placeholder="Select category" />
            </UFormField>
            <UFormField label="Audit reason" required hint="Minimum 8 characters">
              <UTextarea v-model="conversionForm.reason" :rows="4" autoresize placeholder="Explain why this voucher is being moved." />
            </UFormField>
          </div>
        </template>
        <template #footer>
          <div class="modal-actions">
            <UButton color="neutral" variant="outline" label="Cancel" @click="conversionOpen = false" />
            <UButton
              :color="conversionDirection === 'toBooks' ? 'primary' : 'warning'"
              icon="i-lucide-shield-check"
              label="Approve and Move"
              :loading="converting"
              @click="submitConversion"
            />
          </div>
        </template>
      </UModal>

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
            <UCheckbox v-model="includeSignatures" label="Signature lines" />
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

              <div v-if="includeSignatures" class="voucher-signatures">
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
            <UButton
              color="neutral"
              variant="subtle"
              icon="i-lucide-file-down"
              label="Download PDF"
              :loading="downloadingPdf"
              @click="downloadCashVoucherPdf"
            />
            <UButton icon="i-lucide-printer" label="Print" @click="printCashVoucher" />
          </div>
        </template>
      </UModal>
    </section>
  </AppShell>
</template>

<style scoped>
.conversion-section {
  display: grid;
  gap: 0.75rem;
}

.conversion-section + .conversion-section {
  margin-top: 1.5rem;
  padding-top: 1.5rem;
  border-top: 1px solid var(--ui-border);
}

.conversion-section-heading {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 1rem;
}

.conversion-section-heading h3 {
  font-size: 0.95rem;
  font-weight: 650;
}

.conversion-section-heading p {
  margin-top: 0.2rem;
  color: var(--ui-text-muted);
  font-size: 0.82rem;
}

.conversion-table-wrap {
  max-width: 100%;
  overflow-x: auto;
  border: 1px solid var(--ui-border);
  border-radius: 0.5rem;
}

.conversion-table {
  width: 100%;
  min-width: 760px;
  border-collapse: collapse;
  font-size: 0.82rem;
}

.conversion-table th,
.conversion-table td {
  padding: 0.7rem 0.8rem;
  border-bottom: 1px solid var(--ui-border);
  text-align: left;
  vertical-align: top;
}

.conversion-table th {
  color: var(--ui-text-muted);
  font-weight: 600;
  background: var(--ui-bg-elevated);
}

.conversion-table tbody tr:last-child td {
  border-bottom: 0;
}

.conversion-dialog {
  display: grid;
  gap: 1rem;
}

.conversion-source-summary {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto;
  gap: 0.2rem 1rem;
  padding: 0.85rem;
  border: 1px solid var(--ui-border);
  border-radius: 0.5rem;
  background: var(--ui-bg-elevated);
}

.conversion-source-summary span,
.conversion-source-summary strong {
  font-weight: 650;
}

.conversion-source-summary small {
  grid-column: 1 / -1;
  color: var(--ui-text-muted);
}

@media (max-width: 640px) {
  .conversion-section-heading {
    align-items: stretch;
    flex-direction: column;
  }
}
</style>
