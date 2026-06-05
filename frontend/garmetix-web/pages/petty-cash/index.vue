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
const sheets = ref<any[]>([])
const loading = ref(false)
const saving = ref(false)
const deleting = ref(false)
const search = ref('')
const formOpen = ref(false)
const deleteOpen = ref(false)
const editMode = ref<'create' | 'edit'>('create')
const pendingDelete = ref<any | null>(null)

const form = reactive<any>(emptySheet())

const storeOptions = computed(() => stores.value.map((store) => ({
  value: store.id,
  label: store.name || 'Store'
})))

const filteredRows = computed(() => {
  const term = search.value.trim().toLowerCase()
  if (!term) {
    return tableRows.value
  }

  return tableRows.value.filter((row) => JSON.stringify(row).toLowerCase().includes(term))
})

const cashSummary = computed(() => {
  return sheets.value.reduce((summary, sheet) => {
    summary.opening += Number(sheet.openingBalance || 0)
    summary.sales += Number(sheet.sales || 0)
    summary.receipts += Number(sheet.receipts || 0) + Number(sheet.dueReceipts || 0)
    summary.expenses += Number(sheet.expenses || 0) + Number(sheet.payments || 0)
    summary.cashInHand += sheetCash(sheet)
    return summary
  }, {
    opening: 0,
    sales: 0,
    receipts: 0,
    expenses: 0,
    cashInHand: 0
  })
})

const metrics = computed(() => [
  {
    label: 'Sheets',
    value: sheets.value.length,
    meta: 'Daily cash records',
    icon: 'i-lucide-circle-dollar-sign',
    color: 'primary'
  },
  {
    label: 'Cash Sales',
    value: money(cashSummary.value.sales),
    meta: 'Recorded cash sales',
    icon: 'i-lucide-shopping-bag',
    color: 'success'
  },
  {
    label: 'Expenses',
    value: money(cashSummary.value.expenses),
    meta: 'Expenses and payments',
    icon: 'i-lucide-arrow-up-right',
    color: 'warning'
  },
  {
    label: 'Cash In Hand',
    value: money(cashSummary.value.cashInHand),
    meta: 'Across cash sheets',
    icon: 'i-lucide-wallet',
    color: cashSummary.value.cashInHand >= 0 ? 'neutral' : 'error'
  }
])

const totalIn = computed(() => {
  return Number(form.openingBalance || 0) +
    Number(form.sales || 0) +
    Number(form.receipts || 0) +
    Number(form.dueReceipts || 0) +
    Number(form.bankWithdrawal || 0)
})

const totalOut = computed(() => {
  return Number(form.expenses || 0) +
    Number(form.payments || 0) +
    Number(form.customerDue || 0) +
    Number(form.bankDeposit || 0) +
    Number(form.nonCashSale || 0)
})

const calculatedCash = computed(() => totalIn.value - totalOut.value)

const tableRows = computed(() => sheets.value.map((sheet) => ({
  id: sheet.id,
  onDate: formatDate(sheet.onDate),
  store: storeName(sheet.storeId),
  openingBalance: money(Number(sheet.openingBalance || 0)),
  sales: money(Number(sheet.sales || 0)),
  receipts: money(Number(sheet.receipts || 0)),
  expenses: money(Number(sheet.expenses || 0)),
  payments: money(Number(sheet.payments || 0)),
  cashInHand: sheetCash(sheet),
  cashInHandText: money(sheetCash(sheet)),
  raw: sheet
})))

const columns: TableColumn<any>[] = [
  { accessorKey: 'onDate', header: 'Date' },
  { accessorKey: 'store', header: 'Store' },
  { accessorKey: 'openingBalance', header: 'Opening' },
  { accessorKey: 'sales', header: 'Sales' },
  { accessorKey: 'receipts', header: 'Receipts' },
  { accessorKey: 'expenses', header: 'Expenses' },
  { accessorKey: 'payments', header: 'Payments' },
  {
    accessorKey: 'cashInHandText',
    header: 'Cash In Hand',
    cell: ({ row }) => h(UBadge, {
      color: row.original.cashInHand >= 0 ? 'success' : 'error',
      variant: 'subtle'
    }, () => row.original.cashInHandText)
  },
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

function emptySheet() {
  return {
    id: '',
    storeId: '',
    onDate: new Date().toISOString().slice(0, 10),
    openingBalance: 0,
    sales: 0,
    receipts: 0,
    dueReceipts: 0,
    bankWithdrawal: 0,
    expenses: 0,
    payments: 0,
    customerDue: 0,
    bankDeposit: 0,
    nonCashSale: 0,
    cashInHand: 0,
    createdBy: 'AutoAdmin'
  }
}

async function refresh() {
  if (!auth.isAuthenticated.value) {
    return
  }

  loading.value = true
  try {
    const [companyRows, storeRows, sheetRows] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores'),
      api.list<any>('petty-cash-sheets')
    ])

    companies.value = companyRows
    stores.value = storeRows
    sheets.value = sheetRows.sort((a, b) => String(b.onDate).localeCompare(String(a.onDate)))
  } catch (error) {
    feedback.failed('Petty cash refresh failed', error)
  } finally {
    loading.value = false
  }
}

function startCreate() {
  editMode.value = 'create'
  Object.assign(form, emptySheet())
  form.storeId = stores.value[0]?.id || ''
  formOpen.value = true
}

function startEdit(sheet: any) {
  editMode.value = 'edit'
  Object.assign(form, {
    ...sheet,
    onDate: String(sheet.onDate || new Date().toISOString()).slice(0, 10)
  })
  formOpen.value = true
}

function buildPayload() {
  if (!form.storeId) {
    throw new Error('Select store before saving petty cash.')
  }

  const payload: any = {
    storeId: form.storeId,
    onDate: new Date(`${form.onDate}T00:00:00`).toISOString(),
    openingBalance: Number(form.openingBalance || 0),
    sales: Number(form.sales || 0),
    receipts: Number(form.receipts || 0),
    dueReceipts: Number(form.dueReceipts || 0),
    bankWithdrawal: Number(form.bankWithdrawal || 0),
    expenses: Number(form.expenses || 0),
    payments: Number(form.payments || 0),
    customerDue: Number(form.customerDue || 0),
    bankDeposit: Number(form.bankDeposit || 0),
    nonCashSale: Number(form.nonCashSale || 0),
    cashInHand: roundMoney(calculatedCash.value),
    createdBy: String(form.createdBy || 'AutoAdmin').trim()
  }

  if (editMode.value === 'edit' && form.id) {
    payload.id = form.id
  }

  return payload
}

async function saveSheet() {
  saving.value = true
  try {
    const payload = buildPayload()
    if (editMode.value === 'edit' && form.id) {
      await api.update<any>('petty-cash-sheets', form.id, payload)
      feedback.updated('Petty cash sheet')
    } else {
      await api.create<any>('petty-cash-sheets', payload)
      feedback.saved('Petty cash sheet')
    }

    formOpen.value = false
    await refresh()
  } catch (error) {
    feedback.failed('Could not save petty cash sheet', error)
  } finally {
    saving.value = false
  }
}

function askDelete(sheet: any) {
  pendingDelete.value = sheet
  deleteOpen.value = true
}

async function confirmDelete() {
  if (!pendingDelete.value) {
    return
  }

  deleting.value = true
  try {
    await api.remove('petty-cash-sheets', pendingDelete.value.id)
    feedback.deleted('Petty cash sheet')
    deleteOpen.value = false
    pendingDelete.value = null
    await refresh()
  } catch (error) {
    feedback.failed('Could not delete petty cash sheet', error)
  } finally {
    deleting.value = false
  }
}

function storeName(storeId: string) {
  return stores.value.find((item) => item.id === storeId)?.name || 'Store'
}

function sheetCash(sheet: any) {
  return roundMoney(
    Number(sheet.openingBalance || 0) +
    Number(sheet.sales || 0) +
    Number(sheet.receipts || 0) +
    Number(sheet.dueReceipts || 0) +
    Number(sheet.bankWithdrawal || 0) -
    Number(sheet.expenses || 0) -
    Number(sheet.payments || 0) -
    Number(sheet.customerDue || 0) -
    Number(sheet.bankDeposit || 0) -
    Number(sheet.nonCashSale || 0)
  )
}

function roundMoney(value: number) {
  return Math.round((Number(value || 0) + Number.EPSILON) * 100) / 100
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

watch(calculatedCash, (value) => {
  form.cashInHand = roundMoney(value)
}, { immediate: true })
</script>

<template>
  <AuthScreen v-if="!isAuthenticated" @authenticated="refresh" />

  <AppShell
    v-else
    title="Petty Cash"
    :companies="companies"
    :stores="stores"
    @refresh="refresh"
  >
    <section class="planner-dashboard">
      <UiModulePageHeader
        title="Daily Cash Sheets"
        description="Track opening cash, sales, receipts, expenses, deposits, and calculated cash in hand."
        icon="i-lucide-circle-dollar-sign"
        primary-label="New Sheet"
        primary-icon="i-lucide-plus"
        @primary="startCreate"
      >
        <template #actions>
          <UBadge :color="loading ? 'warning' : 'success'" variant="subtle">
            {{ loading ? 'Loading' : `${sheets.length} sheets` }}
          </UBadge>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="subtle" :loading="loading" label="Refresh" @click="refresh" />
          <UButton icon="i-lucide-plus" label="New Sheet" @click="startCreate" />
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
              <h2>Cash Register</h2>
              <p>Search by store, date, or creator and review daily cash position.</p>
            </div>
            <UBadge color="neutral" variant="subtle">{{ filteredRows.length }} shown</UBadge>
          </div>
        </template>

        <UiCrudToolbar
          v-model:search="search"
          search-placeholder="Search store or date"
          :loading="loading"
          refresh-label="Sync"
          create-label="New Sheet"
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
          title="No petty cash sheets found"
          description="Create the first daily cash sheet for a store."
          icon="i-lucide-wallet"
          action-label="New Sheet"
          @action="startCreate"
        />
      </UCard>

      <UiFormSlideover
        v-model:open="formOpen"
        :title="editMode === 'create' ? 'New Petty Cash Sheet' : 'Edit Petty Cash Sheet'"
        description="Enter daily cash in, cash out, and calculated cash in hand."
        :submit-label="editMode === 'create' ? 'Save Sheet' : 'Update Sheet'"
        :loading="saving"
        @submit="saveSheet"
      >
        <UFormField label="Store" required>
          <USelect v-model="form.storeId" :items="storeOptions" placeholder="Select store" />
        </UFormField>
        <UFormField label="Date" required>
          <UInput v-model="form.onDate" required type="date" />
        </UFormField>

        <div class="form-two-column">
          <UFormField label="Opening balance">
            <UInput v-model="form.openingBalance" step="0.01" type="number" />
          </UFormField>
          <UFormField label="Cash sales">
            <UInput v-model="form.sales" step="0.01" type="number" />
          </UFormField>
        </div>

        <div class="form-two-column">
          <UFormField label="Receipts">
            <UInput v-model="form.receipts" step="0.01" type="number" />
          </UFormField>
          <UFormField label="Due receipts">
            <UInput v-model="form.dueReceipts" step="0.01" type="number" />
          </UFormField>
        </div>

        <UFormField label="Bank withdrawal">
          <UInput v-model="form.bankWithdrawal" step="0.01" type="number" />
        </UFormField>

        <div class="form-two-column">
          <UFormField label="Expenses">
            <UInput v-model="form.expenses" step="0.01" type="number" />
          </UFormField>
          <UFormField label="Payments">
            <UInput v-model="form.payments" step="0.01" type="number" />
          </UFormField>
        </div>

        <div class="form-two-column">
          <UFormField label="Customer due">
            <UInput v-model="form.customerDue" step="0.01" type="number" />
          </UFormField>
          <UFormField label="Bank deposit">
            <UInput v-model="form.bankDeposit" step="0.01" type="number" />
          </UFormField>
        </div>

        <div class="form-two-column">
          <UFormField label="Non-cash sale">
            <UInput v-model="form.nonCashSale" step="0.01" type="number" />
          </UFormField>
          <UFormField label="Cash in hand">
            <UInput v-model="form.cashInHand" readonly step="0.01" type="number" />
          </UFormField>
        </div>

        <div class="payroll-summary">
          <span>Total in</span><strong>{{ money(totalIn) }}</strong>
          <span>Total out</span><strong>{{ money(totalOut) }}</strong>
          <span>Calculated cash</span><strong>{{ money(calculatedCash) }}</strong>
        </div>
      </UiFormSlideover>

      <UiConfirmDeleteModal
        v-model:open="deleteOpen"
        title="Delete Petty Cash Sheet"
        :description="`Delete petty cash sheet for ${pendingDelete ? formatDate(pendingDelete.onDate) : ''}?`"
        :loading="deleting"
        @confirm="confirmDelete"
      />
    </section>
  </AppShell>
</template>
