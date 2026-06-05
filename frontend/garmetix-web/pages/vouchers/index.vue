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
const vouchers = ref<any[]>([])
const setupStatus = ref<any | null>(null)
const loading = ref(false)
const saving = ref(false)
const deleting = ref(false)
const search = ref('')
const formOpen = ref(false)
const deleteOpen = ref(false)
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
  { value: 6, label: 'NEFT' },
  { value: 7, label: 'Cheque' }
]

const form = reactive<any>(emptyVoucher())

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
  { accessorKey: 'paymentMode', header: 'Mode' },
  { accessorKey: 'amount', header: 'Amount' },
  {
    id: 'actions',
    header: '',
    cell: ({ row }) => h('div', { class: 'table-action-buttons' }, [
      h(UButton, {
        color: 'neutral',
        variant: 'ghost',
        icon: 'i-lucide-pencil',
        label: 'Edit',
        onClick: () => startEdit(row.original.raw)
      }),
      h(UButton, {
        color: 'error',
        variant: 'ghost',
        icon: 'i-lucide-trash-2',
        label: 'Delete',
        onClick: () => askDelete(row.original.raw)
      })
    ])
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
    isParty: false
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
    const [companyRows, storeRows, voucherRows] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores'),
      api.list<any>('vouchers')
    ])

    companies.value = companyRows
    stores.value = storeRows
    vouchers.value = voucherRows
  } catch (error) {
    feedback.failed('Vouchers refresh failed', error)
  } finally {
    loading.value = false
  }
}

function startCreate() {
  editMode.value = 'create'
  Object.assign(form, emptyVoucher())
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
    bankAccount: null
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

  return {
    ...form,
    voucherNumber: String(form.voucherNumber || '').trim(),
    onDate: new Date(`${form.onDate}T00:00:00`).toISOString(),
    voucherType: Number(form.voucherType),
    partyName: String(form.partyName || '').trim(),
    particulars: String(form.particulars || '').trim(),
    amount: Number(form.amount || 0),
    paymentMode: Number(form.paymentMode),
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
        <UFormField label="Party" required>
          <UInput v-model="form.partyName" required />
        </UFormField>
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
        <UCheckbox v-model="form.isParty" label="Party ledger voucher" />
      </UiFormSlideover>

      <UiConfirmDeleteModal
        v-model:open="deleteOpen"
        title="Delete Voucher"
        :description="`Delete voucher ${pendingDelete?.voucherNumber || ''}?`"
        :loading="deleting"
        @confirm="confirmDelete"
      />
    </section>
  </AppShell>
</template>
