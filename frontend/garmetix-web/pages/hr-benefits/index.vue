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
const UButton = resolveComponent('UButton')
const UBadge = resolveComponent('UBadge')

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const employees = ref<any[]>([])
const adjustments = ref<any[]>([])
const summary = ref<any | null>(null)
const setupStatus = ref<any | null>(null)
const loading = ref(false)
const saving = ref(false)
const deleting = ref(false)
const loadError = ref('')
const search = ref('')
const formOpen = ref(false)
const deleteOpen = ref(false)
const editingId = ref('')
const pendingDelete = ref<any | null>(null)

const adjustmentTypeOptions = [
  { value: 'SalaryAdvance', label: 'Salary Advance' },
  { value: 'AdvanceRecovery', label: 'Advance Recovery' },
  { value: 'Leave', label: 'Leave' },
  { value: 'Bonus', label: 'Bonus' },
  { value: 'LeaveEncashment', label: 'Leave Encashment' },
  { value: 'PF', label: 'PF' },
  { value: 'Gratuity', label: 'Gratuity' },
  { value: 'Other', label: 'Other' }
]

const statusOptions = [
  { value: 'Open', label: 'Open' },
  { value: 'Approved', label: 'Approved' },
  { value: 'Recovered', label: 'Recovered' },
  { value: 'Closed', label: 'Closed' }
]

const form = reactive<any>(emptyForm())

const employeeOptions = computed(() => employees.value.map((employee) => ({
  value: employee.id,
  label: `${employee.employeeCode || `EMP-${String(employee.empId || 0).padStart(4, '0')}`} - ${employee.firstName || ''} ${employee.lastName || ''}`.trim()
})))

const metrics = computed(() => [
  { label: 'Open Advances', value: summary.value?.openAdvances || 0, meta: money(summary.value?.openAdvanceAmount || 0), icon: 'i-lucide-hand-coins', color: 'warning' },
  { label: 'Recovered', value: money(summary.value?.recoveredAmount || 0), meta: 'Advance recovery', icon: 'i-lucide-circle-check', color: 'success' },
  { label: 'Leave Days', value: Number(summary.value?.leaveDays || 0).toFixed(1), meta: 'Leave / encashment rows', icon: 'i-lucide-calendar-minus', color: 'primary' },
  { label: 'Bonus', value: money(summary.value?.bonusAmount || 0), meta: 'Bonus provision/payment', icon: 'i-lucide-gift', color: 'neutral' },
  { label: 'PF', value: money((summary.value?.pfEmployee || 0) + (summary.value?.pfEmployer || 0)), meta: 'Employee + employer', icon: 'i-lucide-piggy-bank', color: 'primary' },
  { label: 'Gratuity', value: money(summary.value?.gratuityAmount || 0), meta: 'Provision / settlement', icon: 'i-lucide-badge-indian-rupee', color: 'success' }
])

const rows = computed(() => {
  const mapped = adjustments.value.map((item) => ({
    id: item.id,
    date: formatDate(item.onDate),
    employee: `${item.employeeCode || ''} ${item.employeeName || ''}`.trim(),
    type: typeLabel(item.adjustmentType),
    salaryMonth: item.salaryMonth || '-',
    amount: money(item.amount || 0),
    leaveDays: Number(item.leaveDays || 0).toFixed(1),
    recovered: money(item.recoveredAmount || 0),
    pf: money((item.pfEmployee || 0) + (item.pfEmployer || 0)),
    gratuity: money(item.gratuityAmount || 0),
    status: item.status || 'Open',
    raw: item
  }))
  const term = search.value.trim().toLowerCase()
  return term ? mapped.filter((row) => JSON.stringify(row).toLowerCase().includes(term)) : mapped
})

const columns: TableColumn<any>[] = [
  { accessorKey: 'date', header: 'Date' },
  { accessorKey: 'employee', header: 'Employee' },
  { accessorKey: 'type', header: 'Type' },
  { accessorKey: 'salaryMonth', header: 'Month' },
  { accessorKey: 'amount', header: 'Amount' },
  { accessorKey: 'leaveDays', header: 'Leave' },
  { accessorKey: 'recovered', header: 'Recovered' },
  { accessorKey: 'pf', header: 'PF' },
  { accessorKey: 'gratuity', header: 'Gratuity' },
  {
    accessorKey: 'status',
    header: 'Status',
    cell: ({ row }) => h(UBadge, { color: row.original.status === 'Closed' ? 'success' : 'warning', variant: 'subtle' }, () => row.original.status)
  },
  {
    id: 'actions',
    header: '',
    cell: ({ row }) => h('div', { class: 'table-action-buttons' }, [
      canEdit.value ? h(UButton, { icon: 'i-lucide-pencil', label: 'Edit', color: 'neutral', variant: 'ghost', onClick: () => startEdit(row.original.raw) }) : null,
      canDelete.value ? h(UButton, { icon: 'i-lucide-trash-2', label: 'Delete', color: 'error', variant: 'ghost', onClick: () => askDelete(row.original.raw) }) : null
    ].filter(Boolean))
  }
]

function emptyForm() {
  return {
    employeeId: '',
    adjustmentType: 'SalaryAdvance',
    onDate: localDateInput(),
    salaryMonth: salaryMonthInput(),
    amount: 0,
    leaveDays: 0,
    recoverFromSalary: true,
    recoveredAmount: 0,
    pfEmployee: 0,
    pfEmployer: 0,
    gratuityAmount: 0,
    status: 'Open',
    remarks: ''
  }
}

async function refresh() {
  if (!auth.isAuthenticated.value) return
  loading.value = true
  loadError.value = ''
  try {
    setupStatus.value = await api.get<any>('setup/status')
    const [companyRows, storeRows, employeeRows, adjustmentRows, summaryRow] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores'),
      api.list<any>('employees'),
      api.list<any>('hr-payroll/adjustments'),
      api.get<any>('hr-payroll/adjustments/summary')
    ])
    companies.value = companyRows
    stores.value = storeRows
    employees.value = employeeRows
    adjustments.value = adjustmentRows
    summary.value = summaryRow
  } catch (error) {
    loadError.value = feedback.cleanMessage(error instanceof Error ? error.message : 'Please check the service and try again.')
    feedback.failed('HR benefits refresh failed', error)
  } finally {
    loading.value = false
  }
}

function startCreate() {
  Object.assign(form, emptyForm())
  editingId.value = ''
  formOpen.value = true
}

function startEdit(item: any) {
  Object.assign(form, {
    ...item,
    onDate: toDateInput(item.onDate || localDateInput()),
    salaryMonth: item.salaryMonth || salaryMonthInput()
  })
  editingId.value = item.id
  formOpen.value = true
}

function askDelete(item: any) {
  pendingDelete.value = item
  deleteOpen.value = true
}

async function saveAdjustment() {
  saving.value = true
  try {
    const employee = employees.value.find((item) => item.id === form.employeeId)
    if (!employee) throw new Error('Select employee before saving.')
    const payload = {
      ...form,
      onDate: toApiDate(form.onDate),
      salaryMonth: form.salaryMonth ? Number(form.salaryMonth) : null,
      amount: Number(form.amount || 0),
      leaveDays: Number(form.leaveDays || 0),
      recoverFromSalary: Boolean(form.recoverFromSalary),
      recoveredAmount: Number(form.recoveredAmount || 0),
      pfEmployee: Number(form.pfEmployee || 0),
      pfEmployer: Number(form.pfEmployer || 0),
      gratuityAmount: Number(form.gratuityAmount || 0),
      status: String(form.status || 'Open'),
      remarks: String(form.remarks || '').trim() || null,
      companyId: employee.companyId,
      storeGroupId: employee.storeGroupId,
      storeId: employee.storeId
    }
    if (editingId.value) {
      await api.update<any>('hr-payroll/adjustments', editingId.value, payload)
      feedback.updated('HR payroll adjustment')
    } else {
      await api.create<any>('hr-payroll/adjustments', payload)
      feedback.saved('HR payroll adjustment')
    }
    formOpen.value = false
    await refresh()
  } catch (error) {
    feedback.failed('Could not save HR payroll adjustment', error)
  } finally {
    saving.value = false
  }
}

async function confirmDelete() {
  if (!pendingDelete.value) return
  deleting.value = true
  try {
    await api.remove('hr-payroll/adjustments', pendingDelete.value.id)
    feedback.deleted('HR payroll adjustment')
    pendingDelete.value = null
    deleteOpen.value = false
    await refresh()
  } catch (error) {
    feedback.failed('Could not delete HR payroll adjustment', error)
  } finally {
    deleting.value = false
  }
}

function typeLabel(value: string) {
  return adjustmentTypeOptions.find((item) => item.value === value)?.label || value
}
function money(value: number) {
  return new Intl.NumberFormat(undefined, { style: 'currency', currency: 'INR', maximumFractionDigits: 0 }).format(Number(value || 0))
}
function formatDate(value: string) { return value ? new Date(value).toLocaleDateString() : '-' }
function toApiDate(value: string) { return `${value}T00:00:00` }
function toDateInput(value: string) { return String(value || localDateInput()).slice(0, 10) }
function localDateInput(date = new Date()) {
  const local = new Date(date.getTime() - date.getTimezoneOffset() * 60_000)
  return local.toISOString().slice(0, 10)
}
function salaryMonthInput(date = new Date()) { return date.getFullYear() * 100 + date.getMonth() + 1 }

onMounted(async () => {
  auth.restore()
  await refresh()
})
</script>

<template>
  <AuthScreen v-if="!isAuthenticated" @authenticated="refresh" />

  <AppShell v-else title="HR Benefits" :companies="companies" :stores="stores" @refresh="refresh">
    <section class="planner-dashboard">
      <UiModulePageHeader
        title="HR Benefits & Salary Adjustments"
        description="Manage salary advances, recovery, leave, bonus, leave encashment, PF and gratuity adjustments before payroll finalization."
        icon="i-lucide-hand-coins"
        primary-label="New Adjustment"
        primary-icon="i-lucide-plus"
        @primary="startCreate"
      >
        <template #actions>
          <UBadge :color="loading ? 'warning' : 'success'" variant="subtle">{{ loading ? 'Loading' : 'Ready' }}</UBadge>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="subtle" :loading="loading" label="Refresh" @click="refresh" />
        </template>
      </UiModulePageHeader>

      <UAlert v-if="summary?.readinessMessages?.length" icon="i-lucide-clipboard-check" color="primary" variant="subtle" title="Payroll adjustment readiness" :description="summary.readinessMessages.join(' ')" />

      <div class="planner-metric-grid">
        <UCard v-for="metric in metrics" :key="metric.label" class="planner-metric-card">
          <div class="planner-metric-body">
            <UAvatar :icon="metric.icon" :color="metric.color" variant="subtle" />
            <div><p>{{ metric.label }}</p><strong>{{ metric.value }}</strong><span>{{ metric.meta }}</span></div>
          </div>
        </UCard>
      </div>

      <UiRegisterPanel title="HR Benefits Register" :description="`${rows.length} adjustment records shown`" :loading="loading" :error="loadError" :empty="rows.length === 0" empty-title="No HR benefits records" empty-description="Create salary advance, leave, PF, gratuity or bonus rows to continue." empty-icon="i-lucide-inbox" @retry="refresh">
        <UiCrudToolbar v-model:search="search" search-placeholder="Search HR benefits" :loading="loading" refresh-label="Sync" create-label="New Adjustment" @refresh="refresh" @create="startCreate" />
        <div class="planner-table-wrap"><UTable :data="rows" :columns="columns" /></div>
      </UiRegisterPanel>

      <UiFormSlideover v-model:open="formOpen" :title="editingId ? 'Edit HR Payroll Adjustment' : 'New HR Payroll Adjustment'" description="Record salary advance, recovery, leave, bonus, PF or gratuity row." submit-label="Save Adjustment" :loading="saving" @submit="saveAdjustment">
        <UFormField label="Employee" required><USelect v-model="form.employeeId" :items="employeeOptions" placeholder="Select employee" /></UFormField>
        <div class="form-two-column">
          <UFormField label="Type"><USelect v-model="form.adjustmentType" :items="adjustmentTypeOptions" /></UFormField>
          <UFormField label="Status"><USelect v-model="form.status" :items="statusOptions" /></UFormField>
        </div>
        <div class="form-two-column">
          <UFormField label="Date"><UInput v-model="form.onDate" type="date" /></UFormField>
          <UFormField label="Salary month"><UInput v-model="form.salaryMonth" type="number" placeholder="YYYYMM" /></UFormField>
        </div>
        <div class="form-two-column">
          <UFormField label="Amount"><UInput v-model="form.amount" min="0" type="number" /></UFormField>
          <UFormField label="Recovered amount"><UInput v-model="form.recoveredAmount" min="0" type="number" /></UFormField>
        </div>
        <div class="form-two-column">
          <UFormField label="Leave days"><UInput v-model="form.leaveDays" min="0" step="0.5" type="number" /></UFormField>
          <UFormField label="Gratuity amount"><UInput v-model="form.gratuityAmount" min="0" type="number" /></UFormField>
        </div>
        <div class="form-two-column">
          <UFormField label="PF employee"><UInput v-model="form.pfEmployee" min="0" type="number" /></UFormField>
          <UFormField label="PF employer"><UInput v-model="form.pfEmployer" min="0" type="number" /></UFormField>
        </div>
        <UCheckbox v-model="form.recoverFromSalary" label="Recover from salary" />
        <UFormField label="Remarks"><UTextarea v-model="form.remarks" autoresize /></UFormField>
      </UiFormSlideover>

      <UiConfirmDeleteModal v-model:open="deleteOpen" title="Delete HR Payroll Adjustment" :description="`Delete ${pendingDelete?.employeeName || 'selected'} adjustment?`" :loading="deleting" @confirm="confirmDelete" />
    </section>
  </AppShell>
</template>
