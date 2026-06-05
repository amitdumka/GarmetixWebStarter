<script setup lang="ts">
import { h, resolveComponent } from 'vue'
import type { TableColumn } from '@nuxt/ui'

type HrTab = 'employees' | 'attendance' | 'monthly'

const api = useGarmetixApi()
const auth = useAuth()
const feedback = useUiFeedback()
const isAuthenticated = auth.isAuthenticated

const UBadge = resolveComponent('UBadge')
const UButton = resolveComponent('UButton')

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const employees = ref<any[]>([])
const attendanceRows = ref<any[]>([])
const monthlyRows = ref<any[]>([])
const setupStatus = ref<any | null>(null)
const loading = ref(false)
const saving = ref(false)
const deleting = ref(false)
const generating = ref(false)
const activeTab = ref<HrTab>('employees')
const search = ref('')
const formOpen = ref(false)
const formKind = ref<'employee' | 'attendance'>('employee')
const deleteOpen = ref(false)
const pendingDelete = ref<any | null>(null)
const editingEmployeeId = ref('')
const editingAttendanceId = ref('')

const genderOptions = [
  { value: 0, label: 'Male' },
  { value: 1, label: 'Female' },
  { value: 2, label: 'TransGender' }
]

const categoryOptions = [
  { value: 0, label: 'Salesman' },
  { value: 1, label: 'Store Manager' },
  { value: 2, label: 'House Keeping' },
  { value: 3, label: 'Owner' },
  { value: 4, label: 'Accounts' },
  { value: 8, label: 'Others' }
]

const attendanceStatusOptions = [
  { value: 0, label: 'Present' },
  { value: 1, label: 'Absent' },
  { value: 2, label: 'Half Day' },
  { value: 3, label: 'Sunday' },
  { value: 4, label: 'Holiday' },
  { value: 5, label: 'Store Closed' },
  { value: 6, label: 'Sunday Holiday' },
  { value: 7, label: 'Sick Leave' },
  { value: 8, label: 'Paid Leave' },
  { value: 9, label: 'Casual Leave' },
  { value: 10, label: 'On Leave' },
  { value: 11, label: 'Leave' },
  { value: 12, label: 'Work From Home' }
]

const tabs = [
  { key: 'employees' as const, label: 'Employees', icon: 'i-lucide-users-round' },
  { key: 'attendance' as const, label: 'Attendance', icon: 'i-lucide-calendar-check' },
  { key: 'monthly' as const, label: 'Monthly', icon: 'i-lucide-calendar-days' }
]

const employeeForm = reactive<any>(emptyEmployee())
const attendanceForm = reactive<any>(emptyAttendance())
const generateForm = reactive({
  year: new Date().getFullYear(),
  month: new Date().getMonth() + 1,
  storeId: ''
})

const employeeOptions = computed(() => employees.value.map((employee) => ({
  value: employee.id,
  label: employeeName(employee.id)
})))

const storeOptions = computed(() => [
  { value: '', label: 'All stores' },
  ...stores.value.map((store) => ({ value: store.id, label: store.name || 'Store' }))
])

const metrics = computed(() => [
  {
    label: 'Employees',
    value: employees.value.length,
    meta: `${employees.value.filter((item) => item.working).length} working`,
    icon: 'i-lucide-users-round',
    color: 'primary'
  },
  {
    label: 'Daily Attendance',
    value: attendanceRows.value.length,
    meta: 'Attendance rows',
    icon: 'i-lucide-calendar-check',
    color: 'success'
  },
  {
    label: 'Monthly Rows',
    value: monthlyRows.value.length,
    meta: 'Generated summaries',
    icon: 'i-lucide-calendar-days',
    color: 'warning'
  },
  {
    label: 'Present Days',
    value: monthlyRows.value.reduce((sum, row) => sum + Number(row.present || 0), 0),
    meta: 'From monthly attendance',
    icon: 'i-lucide-circle-check',
    color: 'neutral'
  }
])

const activeLabel = computed(() => tabs.find((tab) => tab.key === activeTab.value)?.label || 'HR')

const employeeRows = computed(() => employees.value.map((employee) => ({
  id: employee.id,
  name: `${employee.title || ''} ${employee.firstName || ''} ${employee.lastName || ''}`.trim(),
  mobile: employee.mobile || '-',
  email: employee.email || '-',
  category: categoryLabel(employee.category),
  joiningDate: formatDate(employee.joiningDate),
  status: employee.working ? 'Working' : 'Inactive',
  raw: employee
})))

const attendanceTableRows = computed(() => attendanceRows.value.map((row) => ({
  id: row.id,
  onDate: formatDate(row.onDate),
  employee: employeeName(row.employeeId),
  status: statusLabel(row.status),
  checkInTime: toTimeInput(row.checkInTime) || '-',
  checkOutTime: toTimeInput(row.checkOutTime) || '-',
  remarks: row.remarks || '-',
  raw: row
})))

const monthlyTableRows = computed(() => monthlyRows.value.map((row) => ({
  id: row.id,
  month: formatMonth(row.onDate),
  employee: employeeName(row.employeeId),
  present: Number(row.present || 0),
  halfDay: Number(row.halfDay || 0),
  paidLeave: Number(row.paidLeave || 0),
  absent: Number(row.absent || 0) + Number(row.casualLeave || 0),
  workingDays: Number(row.noOfWorkingDays || 0),
  billableDays: Number(row.billableDays || 0).toFixed(1),
  valid: row.valid ? 'Yes' : 'No',
  raw: row
})))

const currentRows = computed(() => {
  const rows = activeTab.value === 'employees'
    ? employeeRows.value
    : activeTab.value === 'attendance' ? attendanceTableRows.value : monthlyTableRows.value

  const term = search.value.trim().toLowerCase()
  if (!term) {
    return rows
  }

  return rows.filter((row) => JSON.stringify(row).toLowerCase().includes(term))
})

const employeeColumns: TableColumn<any>[] = [
  { accessorKey: 'name', header: 'Employee' },
  { accessorKey: 'mobile', header: 'Mobile' },
  { accessorKey: 'email', header: 'Email' },
  { accessorKey: 'category', header: 'Category' },
  { accessorKey: 'joiningDate', header: 'Joining' },
  {
    accessorKey: 'status',
    header: 'Status',
    cell: ({ row }) => h(UBadge, {
      color: row.original.status === 'Working' ? 'success' : 'warning',
      variant: 'subtle'
    }, () => row.original.status)
  },
  actionColumn('employee')
]

const attendanceColumns: TableColumn<any>[] = [
  { accessorKey: 'onDate', header: 'Date' },
  { accessorKey: 'employee', header: 'Employee' },
  {
    accessorKey: 'status',
    header: 'Status',
    cell: ({ row }) => h(UBadge, { color: 'primary', variant: 'subtle' }, () => row.original.status)
  },
  { accessorKey: 'checkInTime', header: 'In' },
  { accessorKey: 'checkOutTime', header: 'Out' },
  { accessorKey: 'remarks', header: 'Remarks' },
  actionColumn('attendance')
]

const monthlyColumns: TableColumn<any>[] = [
  { accessorKey: 'month', header: 'Month' },
  { accessorKey: 'employee', header: 'Employee' },
  { accessorKey: 'present', header: 'Present' },
  { accessorKey: 'halfDay', header: 'Half' },
  { accessorKey: 'paidLeave', header: 'Paid Leave' },
  { accessorKey: 'absent', header: 'Absent' },
  { accessorKey: 'workingDays', header: 'Working Days' },
  { accessorKey: 'billableDays', header: 'Billable' },
  {
    accessorKey: 'valid',
    header: 'Valid',
    cell: ({ row }) => h(UBadge, {
      color: row.original.valid === 'Yes' ? 'success' : 'warning',
      variant: 'subtle'
    }, () => row.original.valid)
  }
]

const activeColumns = computed(() => {
  if (activeTab.value === 'employees') {
    return employeeColumns
  }

  if (activeTab.value === 'attendance') {
    return attendanceColumns
  }

  return monthlyColumns
})

function actionColumn(kind: 'employee' | 'attendance'): TableColumn<any> {
  return {
    id: 'actions',
    header: '',
    cell: ({ row }) => h('div', { class: 'table-action-buttons' }, [
      h(UButton, {
        color: 'neutral',
        variant: 'ghost',
        icon: 'i-lucide-pencil',
        label: 'Edit',
        onClick: () => kind === 'employee' ? startEmployeeEdit(row.original.raw) : startAttendanceEdit(row.original.raw)
      }),
      h(UButton, {
        color: 'error',
        variant: 'ghost',
        icon: 'i-lucide-trash-2',
        label: 'Delete',
        onClick: () => askDelete(kind, row.original.raw)
      })
    ])
  }
}

function emptyEmployee() {
  return {
    title: 'Mr.',
    firstName: '',
    lastName: '',
    gender: 0,
    dateOfBirth: '1990-01-01',
    empId: 0,
    joiningDate: new Date().toISOString().slice(0, 10),
    leavingDate: '',
    working: true,
    category: 0,
    pan: '',
    aadhar: '',
    email: '',
    mobile: ''
  }
}

function emptyAttendance() {
  return {
    employeeId: '',
    onDate: new Date().toISOString().slice(0, 10),
    status: 0,
    checkInTime: '',
    checkOutTime: '',
    entryTime: '',
    remarks: ''
  }
}

async function refresh() {
  if (!auth.isAuthenticated.value) {
    return
  }

  loading.value = true
  try {
    setupStatus.value = await api.get<any>('setup/status')
    const [companyRows, storeRows, employeeData, attendanceData, monthlyData] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores'),
      api.list<any>('employees'),
      api.list<any>('attendance'),
      api.list<any>('monthly-attendance')
    ])

    companies.value = companyRows
    stores.value = storeRows
    employees.value = employeeData
    attendanceRows.value = attendanceData.sort((a, b) => String(b.onDate).localeCompare(String(a.onDate)))
    monthlyRows.value = monthlyData.sort((a, b) => String(b.onDate).localeCompare(String(a.onDate)))
  } catch (error) {
    feedback.failed('HR refresh failed', error)
  } finally {
    loading.value = false
  }
}

function showTab(tab: HrTab) {
  activeTab.value = tab
  search.value = ''
}

function startEmployeeCreate() {
  Object.assign(employeeForm, emptyEmployee())
  editingEmployeeId.value = ''
  activeTab.value = 'employees'
  formKind.value = 'employee'
  formOpen.value = true
}

function startEmployeeEdit(employee: any) {
  Object.assign(employeeForm, {
    ...employee,
    title: employee.title || 'Mr.',
    dateOfBirth: toDateInput(employee.dateOfBirth || '1990-01-01'),
    joiningDate: toDateInput(employee.joiningDate || new Date().toISOString()),
    leavingDate: employee.leavingDate ? toDateInput(employee.leavingDate) : '',
    salaryStructures: null,
    attendances: null,
    salaryPayments: null,
    employeeDetails: null
  })
  editingEmployeeId.value = employee.id
  activeTab.value = 'employees'
  formKind.value = 'employee'
  formOpen.value = true
}

function startAttendanceCreate() {
  Object.assign(attendanceForm, emptyAttendance())
  editingAttendanceId.value = ''
  activeTab.value = 'attendance'
  formKind.value = 'attendance'
  formOpen.value = true
}

function startAttendanceEdit(row: any) {
  Object.assign(attendanceForm, {
    ...row,
    onDate: toDateInput(row.onDate || new Date().toISOString()),
    checkInTime: toTimeInput(row.checkInTime),
    checkOutTime: toTimeInput(row.checkOutTime),
    employee: null
  })
  editingAttendanceId.value = row.id
  activeTab.value = 'attendance'
  formKind.value = 'attendance'
  formOpen.value = true
}

async function saveCurrentForm() {
  if (formKind.value === 'employee') {
    await saveEmployee()
  } else {
    await saveAttendance()
  }
}

function employeePayload() {
  const ids = selectedScopeIds()
  const pan = String(employeeForm.pan || '').trim().toUpperCase()
  const aadhar = digitsOnly(employeeForm.aadhar)
  const mobile = digitsOnly(employeeForm.mobile)

  if (!String(employeeForm.firstName || '').trim() || !String(employeeForm.lastName || '').trim()) {
    throw new Error('Employee first name and last name are required.')
  }

  if (aadhar.length !== 12) {
    throw new Error('Aadhaar number must be exactly 12 digits.')
  }

  if (pan && pan.length !== 10) {
    throw new Error('PAN number must be exactly 10 characters.')
  }

  if (mobile.length < 10 || mobile.length > 15) {
    throw new Error('Mobile number must be 10 to 15 digits.')
  }

  return {
    ...employeeForm,
    title: String(employeeForm.title || '').trim(),
    firstName: String(employeeForm.firstName || '').trim(),
    lastName: String(employeeForm.lastName || '').trim(),
    gender: Number(employeeForm.gender),
    dateOfBirth: toApiDate(employeeForm.dateOfBirth),
    empId: Number(employeeForm.empId || 0),
    joiningDate: toApiDate(employeeForm.joiningDate),
    leavingDate: employeeForm.leavingDate ? toApiDate(employeeForm.leavingDate) : null,
    working: Boolean(employeeForm.working),
    category: Number(employeeForm.category),
    pan: pan || null,
    aadhar,
    email: String(employeeForm.email || '').trim() || null,
    mobile,
    ...ids
  }
}

function digitsOnly(value: unknown) {
  return String(value || '').replace(/\D/g, '')
}

function attendancePayload() {
  const employee = employees.value.find((item) => item.id === attendanceForm.employeeId)
  if (!employee) {
    throw new Error('Select employee before saving attendance.')
  }

  return {
    ...attendanceForm,
    employeeId: attendanceForm.employeeId,
    onDate: toApiDate(attendanceForm.onDate),
    status: Number(attendanceForm.status),
    checkInTime: toApiTime(attendanceForm.checkInTime),
    checkOutTime: toApiTime(attendanceForm.checkOutTime),
    entryTime: String(attendanceForm.entryTime || ''),
    remarks: String(attendanceForm.remarks || '').trim() || null,
    employee: null,
    companyId: employee.companyId,
    storeGroupId: employee.storeGroupId,
    storeId: employee.storeId
  }
}

function selectedScopeIds() {
  const companyId = setupStatus.value?.companyId || companies.value[0]?.id
  const storeGroupId = setupStatus.value?.storeGroupId || stores.value[0]?.storeGroupId
  const storeId = setupStatus.value?.storeId || stores.value[0]?.id

  if (!companyId || !storeGroupId || !storeId) {
    throw new Error('Run quick setup before saving HR records.')
  }

  return { companyId, storeGroupId, storeId }
}

async function saveEmployee() {
  saving.value = true
  try {
    const payload = employeePayload()
    if (editingEmployeeId.value) {
      await api.update<any>('employees', editingEmployeeId.value, payload)
      feedback.updated('Employee')
    } else {
      await api.create<any>('employees', payload)
      feedback.saved('Employee')
    }

    formOpen.value = false
    await refresh()
  } catch (error) {
    feedback.failed('Could not save employee', error)
  } finally {
    saving.value = false
  }
}

async function saveAttendance() {
  saving.value = true
  try {
    const payload = attendancePayload()
    if (editingAttendanceId.value) {
      await api.update<any>('attendance', editingAttendanceId.value, payload)
      feedback.updated('Attendance')
    } else {
      await api.create<any>('attendance', payload)
      feedback.saved('Attendance')
    }

    formOpen.value = false
    await refresh()
  } catch (error) {
    feedback.failed('Could not save attendance', error)
  } finally {
    saving.value = false
  }
}

function askDelete(kind: 'employee' | 'attendance', item: any) {
  formKind.value = kind
  pendingDelete.value = item
  deleteOpen.value = true
}

async function confirmDelete() {
  if (!pendingDelete.value) {
    return
  }

  deleting.value = true
  try {
    if (formKind.value === 'employee') {
      await api.remove('employees', pendingDelete.value.id)
      feedback.deleted('Employee')
    } else {
      await api.remove('attendance', pendingDelete.value.id)
      feedback.deleted('Attendance')
    }

    deleteOpen.value = false
    pendingDelete.value = null
    await refresh()
  } catch (error) {
    feedback.failed('Could not delete HR record', error)
  } finally {
    deleting.value = false
  }
}

async function generateMonthlyAttendance() {
  generating.value = true
  try {
    const selectedStore = stores.value.find((store) => store.id === generateForm.storeId)
    const response = await api.create<any>('hr/monthly-attendance/generate', {
      year: Number(generateForm.year),
      month: Number(generateForm.month),
      companyId: selectedStore?.companyId || setupStatus.value?.companyId || companies.value[0]?.id || null,
      storeGroupId: selectedStore?.storeGroupId || setupStatus.value?.storeGroupId || null,
      storeId: selectedStore?.id || null
    })

    feedback.notify('Monthly attendance generated', `${response.recordsCreated} created, ${response.recordsUpdated} updated.`)
    await refresh()
  } catch (error) {
    feedback.failed('Could not generate monthly attendance', error)
  } finally {
    generating.value = false
  }
}

async function autoGenerateIfMonthEnd() {
  if (!import.meta.client || !auth.isAuthenticated.value) {
    return
  }

  const today = new Date()
  const tomorrow = new Date(today)
  tomorrow.setDate(today.getDate() + 1)
  if (tomorrow.getDate() !== 1) {
    return
  }

  const key = `garmetix.monthlyAttendance.${today.getFullYear()}.${today.getMonth() + 1}`
  if (localStorage.getItem(key)) {
    return
  }

  generateForm.year = today.getFullYear()
  generateForm.month = today.getMonth() + 1
  await generateMonthlyAttendance()
  localStorage.setItem(key, 'generated')
}

function primaryAction() {
  if (activeTab.value === 'employees') {
    startEmployeeCreate()
  } else if (activeTab.value === 'attendance') {
    startAttendanceCreate()
  } else {
    generateMonthlyAttendance()
  }
}

function employeeName(employeeId: string) {
  const employee = employees.value.find((item) => item.id === employeeId)
  return employee ? `${employee.firstName} ${employee.lastName}`.trim() : 'Employee'
}

function statusLabel(value: number) {
  return attendanceStatusOptions.find((item) => item.value === Number(value))?.label || 'Status'
}

function categoryLabel(value: number) {
  return categoryOptions.find((item) => item.value === Number(value))?.label || 'Employee'
}

function formatDate(value: string) {
  return value ? new Date(value).toLocaleDateString() : '-'
}

function formatMonth(value: string) {
  return value ? new Date(value).toLocaleDateString(undefined, { month: 'short', year: 'numeric' }) : '-'
}

function toApiDate(value: string) {
  return new Date(`${value}T00:00:00`).toISOString()
}

function toDateInput(value: string) {
  return String(value || new Date().toISOString()).slice(0, 10)
}

function toApiTime(value: string | null) {
  return value ? `${value}:00` : null
}

function toTimeInput(value: string | null) {
  return value ? String(value).slice(0, 5) : ''
}

onMounted(async () => {
  auth.restore()
  await refresh()
  generateForm.storeId = stores.value[0]?.id || ''
  await autoGenerateIfMonthEnd()
})
</script>

<template>
  <AuthScreen v-if="!isAuthenticated" @authenticated="refresh" />

  <AppShell
    v-else
    title="HR"
    :companies="companies"
    :stores="stores"
    @refresh="refresh"
  >
    <section class="planner-dashboard">
      <UiModulePageHeader
        title="HR"
        description="Manage employees, daily attendance, and generated monthly attendance summaries."
        icon="i-lucide-users-round"
        :primary-label="activeTab === 'employees' ? 'New Employee' : activeTab === 'attendance' ? 'New Attendance' : 'Generate Month'"
        :primary-icon="activeTab === 'monthly' ? 'i-lucide-refresh-cw' : 'i-lucide-plus'"
        @primary="primaryAction"
      >
        <template #actions>
          <UBadge :color="loading ? 'warning' : 'success'" variant="subtle">
            {{ loading ? 'Loading' : 'Ready' }}
          </UBadge>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="subtle" :loading="loading" label="Refresh" @click="refresh" />
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
          <div class="setup-list-header">
            <div class="setup-tabs">
              <UButton
                v-for="tab in tabs"
                :key="tab.key"
                :icon="tab.icon"
                :color="activeTab === tab.key ? 'primary' : 'neutral'"
                :variant="activeTab === tab.key ? 'solid' : 'subtle'"
                :label="tab.label"
                @click="showTab(tab.key)"
              />
            </div>
            <UBadge color="neutral" variant="subtle">{{ currentRows.length }} shown</UBadge>
          </div>
        </template>

        <div v-if="activeTab === 'monthly'" class="monthly-generate-bar">
          <UFormField label="Month">
            <UInput v-model="generateForm.month" min="1" max="12" type="number" />
          </UFormField>
          <UFormField label="Year">
            <UInput v-model="generateForm.year" min="2000" type="number" />
          </UFormField>
          <UFormField label="Store">
            <USelect v-model="generateForm.storeId" :items="storeOptions" />
          </UFormField>
          <UButton icon="i-lucide-refresh-cw" :loading="generating" label="Generate" @click="generateMonthlyAttendance" />
        </div>

        <UiCrudToolbar
          v-model:search="search"
          :search-placeholder="`Search ${activeLabel.toLowerCase()}`"
          :loading="loading"
          refresh-label="Sync"
          :create-label="activeTab === 'employees' ? 'New Employee' : activeTab === 'attendance' ? 'New Attendance' : undefined"
          @refresh="refresh"
          @create="primaryAction"
        />

        <UTable
          v-if="currentRows.length"
          :data="currentRows"
          :columns="activeColumns"
          :loading="loading"
        />

        <UiCrudEmptyState
          v-else
          :title="`No ${activeLabel.toLowerCase()} found`"
          description="Create records or generate monthly attendance to continue."
          icon="i-lucide-inbox"
          :action-label="activeTab === 'monthly' ? 'Generate Month' : `New ${activeTab === 'employees' ? 'Employee' : 'Attendance'}`"
          @action="primaryAction"
        />
      </UCard>

      <UiFormSlideover
        v-model:open="formOpen"
        :title="formKind === 'employee' ? (editingEmployeeId ? 'Edit Employee' : 'New Employee') : (editingAttendanceId ? 'Edit Attendance' : 'New Attendance')"
        :description="formKind === 'employee' ? 'Maintain employee master details.' : 'Record daily attendance status and times.'"
        :submit-label="formKind === 'employee' ? 'Save Employee' : 'Save Attendance'"
        :loading="saving"
        @submit="saveCurrentForm"
      >
        <template v-if="formKind === 'employee'">
          <div class="form-two-column">
            <UFormField label="Title">
              <UInput v-model="employeeForm.title" />
            </UFormField>
            <UFormField label="Employee ID">
              <UInput v-model="employeeForm.empId" min="0" type="number" />
            </UFormField>
          </div>
          <div class="form-two-column">
            <UFormField label="First name" required>
              <UInput v-model="employeeForm.firstName" required />
            </UFormField>
            <UFormField label="Last name" required>
              <UInput v-model="employeeForm.lastName" required />
            </UFormField>
          </div>
          <div class="form-two-column">
            <UFormField label="Gender">
              <USelect v-model="employeeForm.gender" :items="genderOptions" />
            </UFormField>
            <UFormField label="Category">
              <USelect v-model="employeeForm.category" :items="categoryOptions" />
            </UFormField>
          </div>
          <div class="form-two-column">
            <UFormField label="Date of birth" required>
              <UInput v-model="employeeForm.dateOfBirth" required type="date" />
            </UFormField>
            <UFormField label="Joining date" required>
              <UInput v-model="employeeForm.joiningDate" required type="date" />
            </UFormField>
          </div>
          <UFormField label="Leaving date">
            <UInput v-model="employeeForm.leavingDate" type="date" />
          </UFormField>
          <div class="form-two-column">
            <UFormField label="Mobile" required>
              <UInput v-model="employeeForm.mobile" inputmode="numeric" maxlength="15" placeholder="10 to 15 digits" required />
            </UFormField>
            <UFormField label="Email">
              <UInput v-model="employeeForm.email" type="email" />
            </UFormField>
          </div>
          <div class="form-two-column">
            <UFormField label="Aadhaar" required>
              <UInput v-model="employeeForm.aadhar" inputmode="numeric" maxlength="14" placeholder="12 digits" required />
            </UFormField>
            <UFormField label="PAN">
              <UInput v-model="employeeForm.pan" maxlength="10" placeholder="10 characters" />
            </UFormField>
          </div>
          <UCheckbox v-model="employeeForm.working" label="Working" />
        </template>

        <template v-else>
          <UFormField label="Employee" required>
            <USelect v-model="attendanceForm.employeeId" :items="employeeOptions" placeholder="Select employee" />
          </UFormField>
          <UFormField label="Date" required>
            <UInput v-model="attendanceForm.onDate" required type="date" />
          </UFormField>
          <UFormField label="Status">
            <USelect v-model="attendanceForm.status" :items="attendanceStatusOptions" />
          </UFormField>
          <div class="form-two-column">
            <UFormField label="Check in">
              <UInput v-model="attendanceForm.checkInTime" type="time" />
            </UFormField>
            <UFormField label="Check out">
              <UInput v-model="attendanceForm.checkOutTime" type="time" />
            </UFormField>
          </div>
          <UFormField label="Remarks">
            <UTextarea v-model="attendanceForm.remarks" autoresize />
          </UFormField>
        </template>
      </UiFormSlideover>

      <UiConfirmDeleteModal
        v-model:open="deleteOpen"
        :title="formKind === 'employee' ? 'Delete Employee' : 'Delete Attendance'"
        :description="formKind === 'employee'
          ? `Delete employee ${pendingDelete ? employeeName(pendingDelete.id) : ''}?`
          : `Delete attendance for ${pendingDelete ? employeeName(pendingDelete.employeeId) : ''}?`"
        :loading="deleting"
        @confirm="confirmDelete"
      />
    </section>
  </AppShell>
</template>
