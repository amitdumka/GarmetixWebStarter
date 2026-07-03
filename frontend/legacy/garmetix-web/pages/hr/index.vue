<script setup lang="ts">
import { h, resolveComponent } from 'vue'
import type { TableColumn } from '@nuxt/ui'

type HrTab = 'employees' | 'attendance' | 'monthly'

const api = useGarmetixApi()
const auth = useAuth()
const workspace = useWorkspace()
const feedback = useUiFeedback()
const isAuthenticated = auth.isAuthenticated
const canEdit = auth.canEdit
const canDelete = auth.canDelete
const canManageAttendanceTimes = auth.canSeeAdmin

const UBadge = resolveComponent('UBadge')
const UButton = resolveComponent('UButton')

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const employees = ref<any[]>([])
const attendanceRows = ref<any[]>([])
const monthlyRows = ref<any[]>([])
const employeeSummary = ref<any | null>(null)
const idCardOpen = ref(false)
const selectedIdCard = ref<any | null>(null)
const setupStatus = ref<any | null>(null)
const loading = ref(false)
const loadError = ref('')
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

const employeeStatusOptions = [
  { value: 'Active', label: 'Active' },
  { value: 'On Leave', label: 'On Leave' },
  { value: 'Resigned', label: 'Resigned' },
  { value: 'Terminated', label: 'Terminated' },
  { value: 'Inactive', label: 'Inactive' }
]

const salaryTypeOptions = [
  { value: 'Monthly', label: 'Monthly salary' },
  { value: 'Daily', label: 'Daily wage' },
  { value: 'PieceRate', label: 'Piece rate' }
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

const ALL_STORES_VALUE = 'all'

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
  storeId: ALL_STORES_VALUE
})

const currentDate = new Date()
const attendanceFilters = reactive({
  year: currentDate.getFullYear(),
  month: currentDate.getMonth() + 1,
  page: 1,
  pageSize: 50,
  employeeId: ALL_STORES_VALUE,
  status: ALL_STORES_VALUE
})
const attendanceTotal = ref(0)

const monthOptions = Array.from({ length: 12 }, (_, index) => ({
  value: index + 1,
  label: new Date(2000, index, 1).toLocaleString(undefined, { month: 'long' })
}))

const yearOptions = computed(() => {
  const currentYear = new Date().getFullYear()
  return Array.from({ length: Math.max(3, currentYear - 2023 + 2) }, (_, index) => {
    const year = 2024 + index
    return { value: year, label: String(year) }
  })
})

const pageSizeOptions = [25, 50, 100, 200].map((value) => ({ value, label: String(value) }))

const attendanceStatusFilterOptions = computed(() => [
  { value: ALL_STORES_VALUE, label: 'All statuses' },
  ...attendanceStatusOptions.map((item) => ({ value: String(item.value), label: item.label }))
])

const attendanceEmployeeFilterOptions = computed(() => [
  { value: ALL_STORES_VALUE, label: 'All employees' },
  ...employeeOptions.value
])

const attendancePageCount = computed(() => Math.max(1, Math.ceil(attendanceTotal.value / Number(attendanceFilters.pageSize || 50))))
const attendanceShownFrom = computed(() => attendanceTotal.value === 0 ? 0 : ((Number(attendanceFilters.page || 1) - 1) * Number(attendanceFilters.pageSize || 50)) + 1)
const attendanceShownTo = computed(() => Math.min(attendanceTotal.value, Number(attendanceFilters.page || 1) * Number(attendanceFilters.pageSize || 50)))

const activeEmployees = computed(() => employees.value.filter(isActiveEmployee))

const employeeOptions = computed(() => activeEmployees.value.map((employee) => ({
  value: employee.id,
  label: employeeName(employee.id)
})))

const storeOptions = computed(() => [
  { value: ALL_STORES_VALUE, label: 'All stores' },
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
    label: 'Missing Photos',
    value: employeeSummary.value?.missingPhoto || 0,
    meta: 'For ID cards / face attendance',
    icon: 'i-lucide-image-off',
    color: 'warning'
  },
  {
    label: 'Daily Attendance',
    value: activeTab.value === 'attendance' ? attendanceTotal.value : attendanceRows.value.length,
    meta: activeTab.value === 'attendance' ? `${monthOptions.find((item) => item.value === Number(attendanceFilters.month))?.label || 'Month'} ${attendanceFilters.year}` : 'Attendance rows',
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
  employeeCode: employee.employeeCode || `EMP-${String(employee.empId || 0).padStart(4, '0')}`,
  name: `${employee.title || ''} ${employee.firstName || ''} ${employee.lastName || ''}`.trim(),
  mobile: maskMobile(employee.mobile),
  email: employee.email || '-',
  department: employee.department || '-',
  designation: employee.designation || categoryLabel(employee.category),
  salary: employee.salaryType === 'Daily' ? money(employee.dailyWage || 0) + ' / day' : money(employee.monthlySalary || 0),
  joiningDate: formatDate(employee.joiningDate),
  status: employee.employeeStatus || (employee.working ? 'Active' : 'Inactive'),
  raw: employee
})))

const attendanceTableRows = computed(() => attendanceRows.value.map((row) => ({
  id: row.id,
  onDate: formatDate(row.onDate),
  employee: row.employeeName || employeeName(row.employeeId),
  status: statusLabel(row.status),
  checkInTime: toTimeInput(row.checkInTime) || '-',
  breakOutTime: toTimeInput(row.breakOutTime) || '-',
  breakInTime: toTimeInput(row.breakInTime) || '-',
  checkOutTime: toTimeInput(row.checkOutTime) || '-',
  remarks: row.remarks || '-',
  raw: row
})))

const monthlyTableRows = computed(() => monthlyRows.value.map((row) => ({
  id: row.id,
  month: formatMonth(row.onDate),
  employee: row.employeeName || employeeName(row.employeeId),
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
  { accessorKey: 'employeeCode', header: 'Code' },
  { accessorKey: 'name', header: 'Employee' },
  { accessorKey: 'mobile', header: 'Mobile' },
  { accessorKey: 'department', header: 'Department' },
  { accessorKey: 'designation', header: 'Designation' },
  { accessorKey: 'salary', header: 'Salary/Wage' },
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
  { accessorKey: 'breakOutTime', header: 'Break out' },
  { accessorKey: 'breakInTime', header: 'Break in' },
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
      canEdit.value ? h(UButton, {
        color: 'neutral',
        variant: 'ghost',
        icon: 'i-lucide-pencil',
        label: 'Edit',
        onClick: () => kind === 'employee' ? startEmployeeEdit(row.original.raw) : startAttendanceEdit(row.original.raw)
      }) : null,
      kind === 'employee' ? h(UButton, {
        color: 'primary',
        variant: 'ghost',
        icon: 'i-lucide-badge',
        label: 'ID Card',
        onClick: () => openIdCard(row.original.raw)
      }) : null,
      canDelete.value ? h(UButton, {
        color: 'error',
        variant: 'ghost',
        icon: 'i-lucide-trash-2',
        label: 'Delete',
        onClick: () => askDelete(kind, row.original.raw)
      }) : null
    ].filter(Boolean))
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
    employeeCode: '',
    fatherOrHusbandName: '',
    department: '',
    designation: '',
    salaryType: 'Monthly',
    monthlySalary: 0,
    dailyWage: 0,
    employeeStatus: 'Active',
    exitReason: '',
    bloodGroup: '',
    photoDataUrl: '',
    joiningDate: localDateInput(),
    leavingDate: '',
    working: true,
    category: 0,
    pan: '',
    aadhar: '',
    email: '',
    mobile: '',
    bankAccountName: '',
    bankAccountNumber: '',
    ifsc: '',
    esiNumber: '',
    pfNumber: '',
    emergencyContact: ''
  }
}

function emptyAttendance() {
  return {
    employeeId: '',
    onDate: localDateInput(),
    status: 0,
    checkInTime: '',
    breakOutTime: '',
    breakInTime: '',
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
  loadError.value = ''
  try {
    setupStatus.value = await api.get<any>('setup/status')
    const [companyRows, storeRows, employeeData, monthlyData] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores'),
      api.list<any>('employees'),
      api.list<any>('monthly-attendance')
    ])

    companies.value = companyRows
    stores.value = storeRows
    employees.value = employeeData
    employeeSummary.value = await api.get<any>('hr/employee-master/summary')
    monthlyRows.value = monthlyData.sort((a, b) => String(b.onDate).localeCompare(String(a.onDate)))

    if (activeTab.value === 'attendance') {
      await refreshAttendancePage(false)
    }
  } catch (error) {
    loadError.value = feedback.cleanMessage(error instanceof Error ? error.message : 'Please check the service and try again.')
    feedback.failed('HR refresh failed', error)
  } finally {
    loading.value = false
  }
}

async function refreshAttendancePage(showLoader = true) {
  if (!auth.isAuthenticated.value) {
    return
  }

  if (showLoader) {
    loading.value = true
    loadError.value = ''
  }

  try {
    const query = new URLSearchParams({
      year: String(attendanceFilters.year || new Date().getFullYear()),
      month: String(attendanceFilters.month || new Date().getMonth() + 1),
      page: String(Math.max(1, Number(attendanceFilters.page || 1))),
      pageSize: String(Number(attendanceFilters.pageSize || 50))
    })

    if (attendanceFilters.employeeId && attendanceFilters.employeeId !== ALL_STORES_VALUE) {
      query.set('employeeId', String(attendanceFilters.employeeId))
    }

    if (attendanceFilters.status && attendanceFilters.status !== ALL_STORES_VALUE) {
      query.set('status', String(attendanceFilters.status))
    }

    const response = await api.get<any>(`hr/attendance?${query.toString()}`)
    attendanceRows.value = response.items || []
    attendanceTotal.value = Number(response.total || 0)
    attendanceFilters.page = Number(response.page || attendanceFilters.page || 1)
    attendanceFilters.pageSize = Number(response.pageSize || attendanceFilters.pageSize || 50)
  } catch (error) {
    loadError.value = feedback.cleanMessage(error instanceof Error ? error.message : 'Please check attendance filters and try again.')
    feedback.failed('Attendance refresh failed', error)
  } finally {
    if (showLoader) {
      loading.value = false
    }
  }
}

async function showTab(tab: HrTab) {
  activeTab.value = tab
  search.value = ''
  if (tab === 'attendance') {
    attendanceFilters.page = 1
    await refreshAttendancePage()
  }
}

async function onAttendanceFilterChanged() {
  attendanceFilters.page = 1
  if (activeTab.value === 'attendance') {
    await refreshAttendancePage()
  }
}

async function changeAttendancePage(page: number) {
  const nextPage = Math.min(Math.max(1, page), attendancePageCount.value)
  if (nextPage === Number(attendanceFilters.page)) {
    return
  }

  attendanceFilters.page = nextPage
  await refreshAttendancePage()
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
    joiningDate: toDateInput(employee.joiningDate || localDateInput()),
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
    onDate: toDateInput(row.onDate || localDateInput()),
    checkInTime: toTimeInput(row.checkInTime),
    breakOutTime: toTimeInput(row.breakOutTime),
    breakInTime: toTimeInput(row.breakInTime),
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
    employeeCode: String(employeeForm.employeeCode || '').trim().toUpperCase() || null,
    fatherOrHusbandName: String(employeeForm.fatherOrHusbandName || '').trim() || null,
    department: String(employeeForm.department || '').trim() || null,
    designation: String(employeeForm.designation || '').trim() || null,
    salaryType: String(employeeForm.salaryType || 'Monthly'),
    monthlySalary: Number(employeeForm.monthlySalary || 0),
    dailyWage: Number(employeeForm.dailyWage || 0),
    employeeStatus: String(employeeForm.employeeStatus || 'Active'),
    exitReason: String(employeeForm.exitReason || '').trim() || null,
    bloodGroup: String(employeeForm.bloodGroup || '').trim() || null,
    photoDataUrl: String(employeeForm.photoDataUrl || '').trim() || null,
    joiningDate: toApiDate(employeeForm.joiningDate),
    leavingDate: employeeForm.leavingDate ? toApiDate(employeeForm.leavingDate) : null,
    working: ['Active', 'On Leave'].includes(String(employeeForm.employeeStatus || 'Active')),
    category: Number(employeeForm.category),
    pan: pan || null,
    aadhar,
    email: String(employeeForm.email || '').trim() || null,
    mobile,
    bankAccountName: String(employeeForm.bankAccountName || '').trim() || null,
    bankAccountNumber: digitsOnly(employeeForm.bankAccountNumber),
    ifsc: String(employeeForm.ifsc || '').trim().toUpperCase() || null,
    esiNumber: String(employeeForm.esiNumber || '').trim() || null,
    pfNumber: String(employeeForm.pfNumber || '').trim() || null,
    emergencyContact: String(employeeForm.emergencyContact || '').trim() || null,
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
    checkInTime: canManageAttendanceTimes.value ? toApiTime(attendanceForm.checkInTime) : undefined,
    breakOutTime: canManageAttendanceTimes.value ? toApiTime(attendanceForm.breakOutTime) : undefined,
    breakInTime: canManageAttendanceTimes.value ? toApiTime(attendanceForm.breakInTime) : undefined,
    checkOutTime: canManageAttendanceTimes.value ? toApiTime(attendanceForm.checkOutTime) : undefined,
    entryTime: String(attendanceForm.entryTime || ''),
    remarks: String(attendanceForm.remarks || '').trim() || null,
    employee: null,
    companyId: employee.companyId,
    storeGroupId: employee.storeGroupId,
    storeId: employee.storeId
  }
}

function selectedScopeIds() {
  const selectedStore = stores.value.find((store) => store.id === workspace.storeId.value)
  const companyId = workspace.companyId.value || setupStatus.value?.companyId || selectedStore?.companyId || companies.value[0]?.id
  const storeGroupId = workspace.storeGroupId.value || selectedStore?.storeGroupId || setupStatus.value?.storeGroupId || stores.value[0]?.storeGroupId
  const storeId = workspace.storeId.value || setupStatus.value?.storeId || stores.value[0]?.id

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
    if (activeTab.value === 'attendance') {
      await refreshAttendancePage()
    } else {
      await refresh()
    }
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
    await refreshAttendancePage()
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
    if (formKind.value === 'attendance') {
      await refreshAttendancePage()
    } else {
      await refresh()
    }
  } catch (error) {
    feedback.failed('Could not delete HR record', error)
  } finally {
    deleting.value = false
  }
}

async function generateMonthlyAttendance() {
  generating.value = true
  try {
  const selectedStore = generateForm.storeId === ALL_STORES_VALUE
    ? null
    : stores.value.find((store) => store.id === generateForm.storeId)
    const response = await api.create<any>('hr/monthly-attendance/generate', {
      year: Number(generateForm.year),
      month: Number(generateForm.month),
      companyId: selectedStore?.companyId || workspace.companyId.value || setupStatus.value?.companyId || companies.value[0]?.id || null,
      storeGroupId: selectedStore?.storeGroupId || workspace.storeGroupId.value || setupStatus.value?.storeGroupId || null,
      storeId: selectedStore?.id || workspace.storeId.value || null
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

async function openIdCard(employee: any) {
  try {
    selectedIdCard.value = await api.get<any>(`hr/employees/${employee.id}/id-card`)
    idCardOpen.value = true
  } catch (error) {
    feedback.failed('Could not open employee ID card', error)
  }
}

function onEmployeePhotoSelected(event: Event) {
  const file = (event.target as HTMLInputElement).files?.[0]
  if (!file) return
  if (file.size > 512 * 1024) {
    feedback.notify('Photo too large', 'Use a compressed photo under 512 KB for now.')
    return
  }

  const reader = new FileReader()
  reader.onload = () => {
    employeeForm.photoDataUrl = String(reader.result || '')
  }
  reader.readAsDataURL(file)
}

function printIdCard() {
  if (!import.meta.client) return
  window.print()
}

function isActiveEmployee(employee: any) {
  const status = String(employee?.employeeStatus || '').trim().toLowerCase()
  return Boolean(employee?.working) && !['resigned', 'terminated', 'inactive'].includes(status)
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

function money(value: number) {
  return new Intl.NumberFormat(undefined, { style: 'currency', currency: 'INR', maximumFractionDigits: 0 }).format(Number(value || 0))
}

function maskMobile(value: string) {
  const digits = digitsOnly(value)
  return digits.length > 4 ? `${'•'.repeat(digits.length - 4)}${digits.slice(-4)}` : digits || '-'
}

function formatDate(value: string) {
  return value ? new Date(value).toLocaleDateString() : '-'
}

function formatMonth(value: string) {
  return value ? new Date(value).toLocaleDateString(undefined, { month: 'short', year: 'numeric' }) : '-'
}

function toApiDate(value: string) {
  return `${value}T00:00:00`
}

function toDateInput(value: string) {
  return String(value || localDateInput()).slice(0, 10)
}

function localDateInput(date = new Date()) {
  const local = new Date(date.getTime() - date.getTimezoneOffset() * 60_000)
  return local.toISOString().slice(0, 10)
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
  generateForm.storeId = ALL_STORES_VALUE
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
        description="Manage employee master, photo/ID-card readiness, daily attendance, lifecycle and monthly attendance summaries."
        icon="i-lucide-users-round"
        :primary-label="activeTab === 'employees' ? 'New Employee' : activeTab === 'attendance' ? 'New Attendance' : 'Generate Month'"
        :primary-icon="activeTab === 'monthly' ? 'i-lucide-refresh-cw' : 'i-lucide-plus'"
        @primary="primaryAction"
      >
        <template #actions>
          <UBadge :color="loading ? 'warning' : 'success'" variant="subtle">
            {{ loading ? 'Loading' : 'Ready' }}
          </UBadge>
          <UButton v-if="activeTab === 'attendance'" icon="i-lucide-calendar-plus" color="primary" variant="solid" label="Add Attendance" @click="startAttendanceCreate" />
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="subtle" :loading="loading" label="Refresh" @click="refresh" />
        </template>
      </UiModulePageHeader>

      <UAlert
        v-if="employeeSummary?.readinessMessages?.length"
        icon="i-lucide-clipboard-check"
        color="primary"
        variant="subtle"
        title="Employee master readiness"
        :description="employeeSummary.readinessMessages.join(' ')"
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
        :title="`${activeLabel} Register`"
        :description="activeTab === 'attendance' ? `Showing ${attendanceShownFrom}-${attendanceShownTo} of ${attendanceTotal} attendance rows` : `${currentRows.length} records shown`"
        :loading="loading"
        :error="loadError"
        :empty="currentRows.length === 0"
        :empty-title="`No ${activeLabel.toLowerCase()} found`"
        empty-description="Create records or generate monthly attendance to continue."
        empty-icon="i-lucide-inbox"
        @retry="refresh"
      >
        <template #actions>
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
            <UBadge color="neutral" variant="subtle">{{ activeTab === 'attendance' ? `${attendanceTotal} total` : `${currentRows.length} shown` }}</UBadge>
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

        <div v-if="activeTab === 'attendance'" class="attendance-filter-bar">
          <UFormField label="Month">
            <USelect v-model="attendanceFilters.month" :items="monthOptions" @update:model-value="onAttendanceFilterChanged" />
          </UFormField>
          <UFormField label="Year">
            <USelect v-model="attendanceFilters.year" :items="yearOptions" @update:model-value="onAttendanceFilterChanged" />
          </UFormField>
          <UFormField label="Employee">
            <USelect v-model="attendanceFilters.employeeId" :items="attendanceEmployeeFilterOptions" @update:model-value="onAttendanceFilterChanged" />
          </UFormField>
          <UFormField label="Status">
            <USelect v-model="attendanceFilters.status" :items="attendanceStatusFilterOptions" @update:model-value="onAttendanceFilterChanged" />
          </UFormField>
          <UFormField label="Page size">
            <USelect v-model="attendanceFilters.pageSize" :items="pageSizeOptions" @update:model-value="onAttendanceFilterChanged" />
          </UFormField>
          <UButton icon="i-lucide-refresh-cw" :loading="loading" label="Apply" @click="refreshAttendancePage" />
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

        <div class="planner-table-wrap">
          <UTable :data="currentRows" :columns="activeColumns" />
        </div>

        <div v-if="activeTab === 'attendance'" class="attendance-pagination-bar">
          <span>Showing {{ attendanceShownFrom }}-{{ attendanceShownTo }} of {{ attendanceTotal }}</span>
          <div class="attendance-pagination-actions">
            <UButton
              color="neutral"
              variant="subtle"
              icon="i-lucide-chevron-left"
              label="Previous"
              :disabled="attendanceFilters.page <= 1 || loading"
              @click="changeAttendancePage(Number(attendanceFilters.page) - 1)"
            />
            <UBadge color="neutral" variant="subtle">Page {{ attendanceFilters.page }} / {{ attendancePageCount }}</UBadge>
            <UButton
              color="neutral"
              variant="subtle"
              trailing-icon="i-lucide-chevron-right"
              label="Next"
              :disabled="attendanceFilters.page >= attendancePageCount || loading"
              @click="changeAttendancePage(Number(attendanceFilters.page) + 1)"
            />
          </div>
        </div>
      </UiRegisterPanel>

      <UiFormSlideover
        v-model:open="formOpen"
        :title="formKind === 'employee' ? (editingEmployeeId ? 'Edit Employee' : 'New Employee') : (editingAttendanceId ? 'Edit Attendance' : 'New Attendance')"
        :description="formKind === 'employee' ? 'Maintain employee master details.' : 'Record daily attendance status and times.'"
        :submit-label="formKind === 'employee' ? 'Save Employee' : 'Save Attendance'"
        :layout="formKind === 'employee' ? 'modal' : 'slideover'"
        :content-class="formKind === 'employee' ? 'w-[calc(100vw-2rem)] sm:max-w-5xl lg:max-w-6xl' : undefined"
        :loading="saving"
        @submit="saveCurrentForm"
      >
        <template v-if="formKind === 'employee'">
          <div class="form-two-column">
            <UFormField label="Title"><UInput v-model="employeeForm.title" /></UFormField>
            <UFormField label="Employee code"><UInput v-model="employeeForm.employeeCode" placeholder="Auto if blank" /></UFormField>
          </div>
          <div class="form-two-column">
            <UFormField label="First name" required><UInput v-model="employeeForm.firstName" required /></UFormField>
            <UFormField label="Last name" required><UInput v-model="employeeForm.lastName" required /></UFormField>
          </div>
          <div class="form-two-column">
            <UFormField label="Father/Husband name"><UInput v-model="employeeForm.fatherOrHusbandName" /></UFormField>
            <UFormField label="Employee photo"><UInput type="file" accept="image/*" @change="onEmployeePhotoSelected" /></UFormField>
          </div>
          <img v-if="employeeForm.photoDataUrl" :src="employeeForm.photoDataUrl" class="employee-photo-preview" alt="Employee photo preview">
          <div class="form-two-column">
            <UFormField label="Gender"><USelect v-model="employeeForm.gender" :items="genderOptions" /></UFormField>
            <UFormField label="Category"><USelect v-model="employeeForm.category" :items="categoryOptions" /></UFormField>
          </div>
          <div class="form-two-column">
            <UFormField label="Department"><UInput v-model="employeeForm.department" /></UFormField>
            <UFormField label="Designation"><UInput v-model="employeeForm.designation" /></UFormField>
          </div>
          <div class="form-two-column">
            <UFormField label="Date of birth" required><UInput v-model="employeeForm.dateOfBirth" required type="date" /></UFormField>
            <UFormField label="Joining date" required><UInput v-model="employeeForm.joiningDate" required type="date" /></UFormField>
          </div>
          <div class="form-two-column">
            <UFormField label="Status"><USelect v-model="employeeForm.employeeStatus" :items="employeeStatusOptions" /></UFormField>
            <UFormField label="Leaving date"><UInput v-model="employeeForm.leavingDate" type="date" /></UFormField>
          </div>
          <UFormField label="Exit reason"><UTextarea v-model="employeeForm.exitReason" autoresize /></UFormField>
          <div class="form-two-column">
            <UFormField label="Salary type"><USelect v-model="employeeForm.salaryType" :items="salaryTypeOptions" /></UFormField>
            <UFormField label="Monthly salary"><UInput v-model="employeeForm.monthlySalary" min="0" type="number" /></UFormField>
          </div>
          <div class="form-two-column">
            <UFormField label="Daily wage"><UInput v-model="employeeForm.dailyWage" min="0" type="number" /></UFormField>
            <UFormField label="Blood group"><UInput v-model="employeeForm.bloodGroup" /></UFormField>
          </div>
          <div class="form-two-column">
            <UFormField label="Mobile" required><UInput v-model="employeeForm.mobile" inputmode="numeric" maxlength="15" placeholder="10 to 15 digits" required /></UFormField>
            <UFormField label="Email"><UInput v-model="employeeForm.email" type="email" /></UFormField>
          </div>
          <div class="form-two-column">
            <UFormField label="Aadhaar" required><UInput v-model="employeeForm.aadhar" inputmode="numeric" maxlength="14" placeholder="12 digits" required /></UFormField>
            <UFormField label="PAN"><UInput v-model="employeeForm.pan" maxlength="10" placeholder="10 characters" /></UFormField>
          </div>
          <div class="form-two-column">
            <UFormField label="Bank account name"><UInput v-model="employeeForm.bankAccountName" /></UFormField>
            <UFormField label="Bank account number"><UInput v-model="employeeForm.bankAccountNumber" inputmode="numeric" /></UFormField>
          </div>
          <div class="form-two-column">
            <UFormField label="IFSC"><UInput v-model="employeeForm.ifsc" maxlength="20" /></UFormField>
            <UFormField label="Emergency contact"><UInput v-model="employeeForm.emergencyContact" /></UFormField>
          </div>
          <div class="form-two-column">
            <UFormField label="ESI number"><UInput v-model="employeeForm.esiNumber" /></UFormField>
            <UFormField label="PF number"><UInput v-model="employeeForm.pfNumber" /></UFormField>
          </div>
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
          <div v-if="canManageAttendanceTimes" class="space-y-3">
            <div class="rounded-lg border border-warning/30 bg-warning/5 p-3 text-xs text-muted">
              Admin/Owner timing correction. Saving these times also creates or updates the linked punch rows. Clearing a time removes the synced HR Attendance punch for that punch type.
            </div>
            <div class="form-two-column">
              <UFormField label="Check in">
                <UInput v-model="attendanceForm.checkInTime" type="time" />
              </UFormField>
              <UFormField label="Break out">
                <UInput v-model="attendanceForm.breakOutTime" type="time" />
              </UFormField>
            </div>
            <div class="form-two-column">
              <UFormField label="Break in">
                <UInput v-model="attendanceForm.breakInTime" type="time" />
              </UFormField>
              <UFormField label="Check out">
                <UInput v-model="attendanceForm.checkOutTime" type="time" />
              </UFormField>
            </div>
          </div>
          <div v-else class="rounded-lg border border-default p-3 text-xs text-muted">
            Check-in, break and check-out timing can be corrected only by Admin/Owner.
          </div>
          <UFormField label="Remarks">
            <UTextarea v-model="attendanceForm.remarks" autoresize />
          </UFormField>
        </template>
      </UiFormSlideover>

      <UModal v-model:open="idCardOpen" title="Employee ID Card">
        <template #body>
          <div v-if="selectedIdCard" class="employee-id-card-print">
            <div class="employee-id-card">
              <div class="employee-id-card-header">
                <strong>{{ selectedIdCard.companyName }}</strong>
                <span>{{ selectedIdCard.storeName }}</span>
              </div>
              <div class="employee-id-card-body">
                <img v-if="selectedIdCard.photoDataUrl" :src="selectedIdCard.photoDataUrl" alt="Employee">
                <div v-else class="employee-id-card-photo">PHOTO</div>
                <div>
                  <h2>{{ selectedIdCard.fullName }}</h2>
                  <p>{{ selectedIdCard.employeeCode }}</p>
                  <p>{{ selectedIdCard.designation }} / {{ selectedIdCard.department }}</p>
                  <p>Mobile: {{ selectedIdCard.mobile }}</p>
                  <p>Emergency: {{ selectedIdCard.emergencyContact }}</p>
                  <p>Blood: {{ selectedIdCard.bloodGroup }}</p>
                </div>
              </div>
            </div>
            <UButton icon="i-lucide-printer" label="Print ID Card" @click="printIdCard" />
          </div>
        </template>
      </UModal>

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


<style scoped>
.attendance-filter-bar,
.attendance-pagination-bar {
  display: flex;
  flex-wrap: wrap;
  align-items: end;
  gap: 0.75rem;
  padding: 0.85rem;
  border: 1px solid rgb(226 232 240);
  border-radius: 14px;
  background: rgb(248 250 252 / 0.8);
}
.attendance-pagination-bar {
  justify-content: space-between;
  align-items: center;
  margin-top: 0.75rem;
  font-size: 0.875rem;
  color: rgb(71 85 105);
}
.attendance-pagination-actions {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}
.employee-photo-preview {
  width: 96px;
  height: 96px;
  object-fit: cover;
  border-radius: 14px;
  border: 1px solid rgb(226 232 240);
}
.employee-id-card {
  width: 360px;
  max-width: 100%;
  border: 1px solid rgb(203 213 225);
  border-radius: 16px;
  overflow: hidden;
  background: white;
  color: #0f172a;
}
.employee-id-card-header {
  padding: 12px 16px;
  display: flex;
  justify-content: space-between;
  background: #f8fafc;
  border-bottom: 1px solid rgb(226 232 240);
}
.employee-id-card-body {
  padding: 16px;
  display: grid;
  grid-template-columns: 96px 1fr;
  gap: 14px;
}
.employee-id-card-body img,
.employee-id-card-photo {
  width: 96px;
  height: 112px;
  object-fit: cover;
  border-radius: 12px;
  border: 1px solid rgb(203 213 225);
  display: grid;
  place-items: center;
  font-size: 12px;
  color: #64748b;
}
@media print {
  body * { visibility: hidden; }
  .employee-id-card-print, .employee-id-card-print * { visibility: visible; }
  .employee-id-card-print { position: fixed; inset: 24px auto auto 24px; }
}
</style>
