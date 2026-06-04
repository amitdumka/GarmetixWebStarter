<script setup lang="ts">
import { CalendarCheck, CalendarDays, Pencil, Plus, RefreshCw, Trash2, UsersRound } from 'lucide-vue-next'

const api = useGarmetixApi()
const auth = useAuth()
const isAuthenticated = auth.isAuthenticated

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const employees = ref<any[]>([])
const attendanceRows = ref<any[]>([])
const monthlyRows = ref<any[]>([])
const setupStatus = ref<any | null>(null)
const loading = ref(false)
const activeTab = ref<'employees' | 'attendance' | 'monthly'>('employees')
const viewMode = ref<'list' | 'employeeForm' | 'attendanceForm'>('list')
const hrMessage = ref('')
const searchText = ref('')
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

const employeeForm = reactive<any>(emptyEmployee())
const attendanceForm = reactive<any>(emptyAttendance())
const generateForm = reactive({
  year: new Date().getFullYear(),
  month: new Date().getMonth() + 1,
  storeId: ''
})

const filteredEmployees = computed(() => {
  const query = searchText.value.trim().toLowerCase()
  if (!query) {
    return employees.value
  }

  return employees.value.filter((employee) => {
    return String(employee.firstName || '').toLowerCase().includes(query) ||
      String(employee.lastName || '').toLowerCase().includes(query) ||
      String(employee.mobile || '').toLowerCase().includes(query) ||
      String(employee.email || '').toLowerCase().includes(query)
  })
})

const filteredAttendance = computed(() => {
  const query = searchText.value.trim().toLowerCase()
  return attendanceRows.value.filter((row) => {
    return !query ||
      employeeName(row.employeeId).toLowerCase().includes(query) ||
      statusLabel(row.status).toLowerCase().includes(query) ||
      String(row.onDate || '').toLowerCase().includes(query)
  })
})

const filteredMonthly = computed(() => {
  const query = searchText.value.trim().toLowerCase()
  return monthlyRows.value.filter((row) => {
    return !query ||
      employeeName(row.employeeId).toLowerCase().includes(query) ||
      String(row.onDate || '').toLowerCase().includes(query)
  })
})

function emptyEmployee() {
  return {
    title: 'Mr.',
    firstName: '',
    lastName: '',
    gender: 0,
    dateOfBirth: '1990-01-01',
    empId: 0,
    joiningDate: new Date().toISOString().slice(0, 10),
    leavingDate: null,
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
    const [companyRows, storeRows, employeeRows, attendanceData, monthlyData] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores'),
      api.list<any>('employees'),
      api.list<any>('attendance'),
      api.list<any>('monthly-attendance')
    ])

    companies.value = companyRows
    stores.value = storeRows
    employees.value = employeeRows
    attendanceRows.value = attendanceData.sort((a, b) => String(b.onDate).localeCompare(String(a.onDate)))
    monthlyRows.value = monthlyData.sort((a, b) => String(b.onDate).localeCompare(String(a.onDate)))
  } finally {
    loading.value = false
  }
}

function showTab(tab: 'employees' | 'attendance' | 'monthly') {
  activeTab.value = tab
  viewMode.value = 'list'
  hrMessage.value = ''
}

function startEmployeeCreate() {
  Object.assign(employeeForm, emptyEmployee())
  editingEmployeeId.value = ''
  activeTab.value = 'employees'
  viewMode.value = 'employeeForm'
  hrMessage.value = ''
}

function startEmployeeEdit(employee: any) {
  Object.assign(employeeForm, {
    ...employee,
    title: employee.title || 'Mr.',
    dateOfBirth: String(employee.dateOfBirth || '1990-01-01').slice(0, 10),
    joiningDate: String(employee.joiningDate || new Date().toISOString()).slice(0, 10),
    leavingDate: employee.leavingDate ? String(employee.leavingDate).slice(0, 10) : null,
    salaryStructures: null,
    attendances: null,
    salaryPayments: null,
    employeeDetails: null
  })
  editingEmployeeId.value = employee.id
  activeTab.value = 'employees'
  viewMode.value = 'employeeForm'
  hrMessage.value = ''
}

function startAttendanceCreate() {
  Object.assign(attendanceForm, emptyAttendance())
  editingAttendanceId.value = ''
  activeTab.value = 'attendance'
  viewMode.value = 'attendanceForm'
  hrMessage.value = ''
}

function startAttendanceEdit(row: any) {
  Object.assign(attendanceForm, {
    ...row,
    onDate: String(row.onDate || new Date().toISOString()).slice(0, 10),
    checkInTime: toTimeInput(row.checkInTime),
    checkOutTime: toTimeInput(row.checkOutTime),
    employee: null
  })
  editingAttendanceId.value = row.id
  activeTab.value = 'attendance'
  viewMode.value = 'attendanceForm'
  hrMessage.value = ''
}

function employeePayload() {
  const ids = selectedScopeIds()

  return {
    ...employeeForm,
    title: String(employeeForm.title || '').trim(),
    firstName: String(employeeForm.firstName || '').trim(),
    lastName: String(employeeForm.lastName || '').trim(),
    gender: Number(employeeForm.gender),
    dateOfBirth: new Date(employeeForm.dateOfBirth).toISOString(),
    empId: Number(employeeForm.empId || 0),
    joiningDate: new Date(employeeForm.joiningDate).toISOString(),
    leavingDate: employeeForm.leavingDate ? new Date(employeeForm.leavingDate).toISOString() : null,
    working: Boolean(employeeForm.working),
    category: Number(employeeForm.category),
    pan: String(employeeForm.pan || '').trim() || null,
    aadhar: String(employeeForm.aadhar || '').trim(),
    email: String(employeeForm.email || '').trim() || null,
    mobile: String(employeeForm.mobile || '').trim(),
    ...ids
  }
}

function attendancePayload() {
  const employee = employees.value.find((item) => item.id === attendanceForm.employeeId)
  if (!employee) {
    throw new Error('Select employee before saving attendance.')
  }

  return {
    ...attendanceForm,
    employeeId: attendanceForm.employeeId,
    onDate: new Date(attendanceForm.onDate).toISOString(),
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
  hrMessage.value = ''
  try {
    const payload = employeePayload()
    if (editingEmployeeId.value) {
      await api.update<any>('employees', editingEmployeeId.value, payload)
      hrMessage.value = 'Employee updated.'
    } else {
      await api.create<any>('employees', payload)
      hrMessage.value = 'Employee saved.'
    }

    viewMode.value = 'list'
    await refresh()
  } catch (error: any) {
    hrMessage.value = error?.data?.message || error?.message || 'Could not save employee.'
  }
}

async function saveAttendance() {
  hrMessage.value = ''
  try {
    const payload = attendancePayload()
    if (editingAttendanceId.value) {
      await api.update<any>('attendance', editingAttendanceId.value, payload)
      hrMessage.value = 'Attendance updated.'
    } else {
      await api.create<any>('attendance', payload)
      hrMessage.value = 'Attendance saved.'
    }

    viewMode.value = 'list'
    await refresh()
  } catch (error: any) {
    hrMessage.value = error?.data?.message || error?.message || 'Could not save attendance.'
  }
}

async function deleteEmployee(employee: any) {
  const confirmed = window.confirm(`Delete employee ${employee.firstName} ${employee.lastName}?`)
  if (!confirmed) {
    return
  }

  await api.remove('employees', employee.id)
  hrMessage.value = 'Employee deleted.'
  await refresh()
}

async function deleteAttendance(row: any) {
  const confirmed = window.confirm(`Delete attendance for ${employeeName(row.employeeId)}?`)
  if (!confirmed) {
    return
  }

  await api.remove('attendance', row.id)
  hrMessage.value = 'Attendance deleted.'
  await refresh()
}

async function generateMonthlyAttendance() {
  hrMessage.value = ''

  const selectedStore = stores.value.find((store) => store.id === generateForm.storeId)
  const response = await api.create<any>('hr/monthly-attendance/generate', {
    year: Number(generateForm.year),
    month: Number(generateForm.month),
    companyId: selectedStore?.companyId || setupStatus.value?.companyId || companies.value[0]?.id || null,
    storeGroupId: selectedStore?.storeGroupId || setupStatus.value?.storeGroupId || null,
    storeId: selectedStore?.id || null
  })

  hrMessage.value = `Monthly attendance generated: ${response.recordsCreated} created, ${response.recordsUpdated} updated.`
  await refresh()
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
    <section class="content">
      <section class="panel">
        <div class="panel-header">
          <h2 class="panel-title">HR</h2>
          <div class="panel-actions">
            <span class="status" :class="loading ? 'warn' : 'ok'">{{ loading ? 'Loading' : 'Ready' }}</span>
            <button class="button secondary" type="button" @click="showTab('employees')">Employees</button>
            <button class="button secondary" type="button" @click="showTab('attendance')">Attendance</button>
            <button class="button secondary" type="button" @click="showTab('monthly')">Monthly</button>
            <button v-if="activeTab === 'employees'" class="button" type="button" @click="startEmployeeCreate">
              <Plus :size="16" />
              New Employee
            </button>
            <button v-else-if="activeTab === 'attendance'" class="button" type="button" @click="startAttendanceCreate">
              <CalendarCheck :size="16" />
              New Attendance
            </button>
            <button v-else class="button" type="button" @click="generateMonthlyAttendance">
              <RefreshCw :size="16" />
              Generate Month
            </button>
          </div>
        </div>

        <div v-if="viewMode === 'list'" class="panel-body">
          <div class="table-toolbar">
            <input v-model="searchText" class="search" aria-label="Search HR" placeholder="Search" />
            <p v-if="hrMessage" class="inline-message">{{ hrMessage }}</p>
          </div>

          <table v-if="activeTab === 'employees'" class="table">
            <thead>
              <tr>
                <th>Employee</th>
                <th>Mobile</th>
                <th>Email</th>
                <th>Category</th>
                <th>Joining</th>
                <th>Status</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="employee in filteredEmployees" :key="employee.id">
                <td>{{ employee.title }} {{ employee.firstName }} {{ employee.lastName }}</td>
                <td>{{ employee.mobile }}</td>
                <td>{{ employee.email }}</td>
                <td>{{ categoryLabel(employee.category) }}</td>
                <td>{{ new Date(employee.joiningDate).toLocaleDateString() }}</td>
                <td><span class="status" :class="employee.working ? 'ok' : 'warn'">{{ employee.working ? 'Working' : 'Inactive' }}</span></td>
                <td>
                  <button class="button secondary" type="button" @click="startEmployeeEdit(employee)">
                    <Pencil :size="16" />
                    Edit
                  </button>
                  <button class="button danger-button" type="button" @click="deleteEmployee(employee)">
                    <Trash2 :size="16" />
                    Delete
                  </button>
                </td>
              </tr>
              <tr v-if="filteredEmployees.length === 0">
                <td colspan="7">No employees</td>
              </tr>
            </tbody>
          </table>

          <table v-else-if="activeTab === 'attendance'" class="table">
            <thead>
              <tr>
                <th>Date</th>
                <th>Employee</th>
                <th>Status</th>
                <th>In</th>
                <th>Out</th>
                <th>Remarks</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="row in filteredAttendance" :key="row.id">
                <td>{{ new Date(row.onDate).toLocaleDateString() }}</td>
                <td>{{ employeeName(row.employeeId) }}</td>
                <td><span class="status ok">{{ statusLabel(row.status) }}</span></td>
                <td>{{ toTimeInput(row.checkInTime) }}</td>
                <td>{{ toTimeInput(row.checkOutTime) }}</td>
                <td>{{ row.remarks }}</td>
                <td>
                  <button class="button secondary" type="button" @click="startAttendanceEdit(row)">
                    <Pencil :size="16" />
                    Edit
                  </button>
                  <button class="button danger-button" type="button" @click="deleteAttendance(row)">
                    <Trash2 :size="16" />
                    Delete
                  </button>
                </td>
              </tr>
              <tr v-if="filteredAttendance.length === 0">
                <td colspan="7">No attendance rows</td>
              </tr>
            </tbody>
          </table>

          <div v-else>
            <div class="setup-grid">
              <div class="field">
                <label for="generateMonth">Month</label>
                <input id="generateMonth" v-model="generateForm.month" min="1" max="12" type="number" />
              </div>
              <div class="field">
                <label for="generateYear">Year</label>
                <input id="generateYear" v-model="generateForm.year" min="2000" type="number" />
              </div>
              <div class="field">
                <label for="generateStore">Store</label>
                <select id="generateStore" v-model="generateForm.storeId">
                  <option value="">All stores</option>
                  <option v-for="store in stores" :key="store.id" :value="store.id">{{ store.name }}</option>
                </select>
              </div>
              <button class="button" type="button" @click="generateMonthlyAttendance">
                <RefreshCw :size="16" />
                Generate
              </button>
            </div>

            <table class="table">
              <thead>
                <tr>
                  <th>Month</th>
                  <th>Employee</th>
                  <th>Present</th>
                  <th>Half</th>
                  <th>Paid Leave</th>
                  <th>Absent</th>
                  <th>Working Days</th>
                  <th>Billable</th>
                  <th>Valid</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="row in filteredMonthly" :key="row.id">
                  <td>{{ new Date(row.onDate).toLocaleDateString(undefined, { month: 'short', year: 'numeric' }) }}</td>
                  <td>{{ employeeName(row.employeeId) }}</td>
                  <td>{{ row.present }}</td>
                  <td>{{ row.halfDay }}</td>
                  <td>{{ row.paidLeave }}</td>
                  <td>{{ row.absent + row.casualLeave }}</td>
                  <td>{{ row.noOfWorkingDays }}</td>
                  <td>{{ Number(row.billableDays || 0).toFixed(1) }}</td>
                  <td><span class="status" :class="row.valid ? 'ok' : 'warn'">{{ row.valid ? 'Yes' : 'No' }}</span></td>
                </tr>
                <tr v-if="filteredMonthly.length === 0">
                  <td colspan="9">No monthly attendance rows</td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>

        <form v-else-if="viewMode === 'employeeForm'" class="form-grid wide-form" @submit.prevent="saveEmployee">
          <div class="field">
            <label for="title">Title</label>
            <input id="title" v-model="employeeForm.title" />
          </div>
          <div class="field">
            <label for="firstName">First name</label>
            <input id="firstName" v-model="employeeForm.firstName" required />
          </div>
          <div class="field">
            <label for="lastName">Last name</label>
            <input id="lastName" v-model="employeeForm.lastName" required />
          </div>
          <div class="field">
            <label for="gender">Gender</label>
            <select id="gender" v-model="employeeForm.gender">
              <option v-for="item in genderOptions" :key="item.value" :value="item.value">{{ item.label }}</option>
            </select>
          </div>
          <div class="field">
            <label for="dateOfBirth">Date of birth</label>
            <input id="dateOfBirth" v-model="employeeForm.dateOfBirth" required type="date" />
          </div>
          <div class="field">
            <label for="joiningDate">Joining date</label>
            <input id="joiningDate" v-model="employeeForm.joiningDate" required type="date" />
          </div>
          <div class="field">
            <label for="category">Category</label>
            <select id="category" v-model="employeeForm.category">
              <option v-for="item in categoryOptions" :key="item.value" :value="item.value">{{ item.label }}</option>
            </select>
          </div>
          <div class="field">
            <label for="empId">Employee ID</label>
            <input id="empId" v-model="employeeForm.empId" min="0" type="number" />
          </div>
          <div class="field">
            <label for="mobile">Mobile</label>
            <input id="mobile" v-model="employeeForm.mobile" required />
          </div>
          <div class="field">
            <label for="email">Email</label>
            <input id="email" v-model="employeeForm.email" type="email" />
          </div>
          <div class="field">
            <label for="aadhar">Aadhar</label>
            <input id="aadhar" v-model="employeeForm.aadhar" required />
          </div>
          <div class="field">
            <label for="pan">PAN</label>
            <input id="pan" v-model="employeeForm.pan" />
          </div>
          <label class="checkbox-field">
            <input v-model="employeeForm.working" type="checkbox" />
            <span>Working</span>
          </label>
          <div class="form-actions">
            <button class="button secondary" type="button" @click="viewMode = 'list'">Cancel</button>
            <button class="button" type="submit">
              <UsersRound :size="16" />
              Save Employee
            </button>
          </div>
          <p v-if="hrMessage" class="setup-message">{{ hrMessage }}</p>
        </form>

        <form v-else class="form-grid wide-form" @submit.prevent="saveAttendance">
          <div class="field">
            <label for="attendanceEmployee">Employee</label>
            <select id="attendanceEmployee" v-model="attendanceForm.employeeId" required>
              <option value="">Select employee</option>
              <option v-for="employee in employees" :key="employee.id" :value="employee.id">
                {{ employee.firstName }} {{ employee.lastName }}
              </option>
            </select>
          </div>
          <div class="field">
            <label for="attendanceDate">Date</label>
            <input id="attendanceDate" v-model="attendanceForm.onDate" required type="date" />
          </div>
          <div class="field">
            <label for="attendanceStatus">Status</label>
            <select id="attendanceStatus" v-model="attendanceForm.status">
              <option v-for="item in attendanceStatusOptions" :key="item.value" :value="item.value">{{ item.label }}</option>
            </select>
          </div>
          <div class="field">
            <label for="checkInTime">Check in</label>
            <input id="checkInTime" v-model="attendanceForm.checkInTime" type="time" />
          </div>
          <div class="field">
            <label for="checkOutTime">Check out</label>
            <input id="checkOutTime" v-model="attendanceForm.checkOutTime" type="time" />
          </div>
          <div class="field">
            <label for="remarks">Remarks</label>
            <input id="remarks" v-model="attendanceForm.remarks" />
          </div>
          <div class="form-actions">
            <button class="button secondary" type="button" @click="viewMode = 'list'">Cancel</button>
            <button class="button" type="submit">
              <CalendarDays :size="16" />
              Save Attendance
            </button>
          </div>
          <p v-if="hrMessage" class="setup-message">{{ hrMessage }}</p>
        </form>
      </section>
    </section>
  </AppShell>
</template>
