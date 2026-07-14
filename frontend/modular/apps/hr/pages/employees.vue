<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 xl:flex-row xl:items-end xl:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-users-round" class="size-4" /> Employee master</p>
          <h2 class="garmetix-dashboard-title">Employees, Attendance And Monthly Generation</h2>
          <p class="garmetix-dashboard-subtitle">
            Modular counterpart for legacy `/hr`: employee master, older daily attendance register and monthly attendance generation.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="load">Refresh</UButton>
          <UButton icon="i-lucide-user-plus" color="primary" @click="startEmployeeCreate">New Employee</UButton>
          <UButton icon="i-lucide-calendar-plus" color="primary" variant="soft" @click="startAttendanceCreate">New Attendance</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="message" :color="messageTone" variant="subtle" :icon="messageIcon" :description="message" />
    <UAlert v-if="summaryMessages.length" color="primary" variant="subtle" icon="i-lucide-clipboard-check" title="Employee master readiness" :description="summaryMessages.join(' ')" />

    <div class="grid gap-3 md:grid-cols-2 xl:grid-cols-6">
      <div v-for="card in cards" :key="card.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label">{{ card.label }}</p>
        <p class="garmetix-metric-value text-xl">{{ card.value }}</p>
        <p class="mt-1 text-xs text-muted">{{ card.detail }}</p>
      </div>
    </div>

    <div class="flex flex-wrap gap-2">
      <UButton v-for="tab in tabs" :key="tab.key" :icon="tab.icon" :color="activeTab === tab.key ? 'primary' : 'neutral'" :variant="activeTab === tab.key ? 'solid' : 'soft'" @click="activeTab = tab.key">
        {{ tab.label }}
      </UButton>
    </div>

    <div v-if="activeTab === 'employees'" class="garmetix-table-panel">
      <div class="garmetix-panel-header">
        <div>
          <h3 class="garmetix-panel-title">Employee Register</h3>
          <p class="garmetix-panel-subtitle">{{ filteredEmployees.length }} of {{ employees.length }} employee(s).</p>
        </div>
        <div class="flex flex-wrap items-center gap-2">
          <USelect v-model="employeeStatusFilter" :items="employeeStatusFilterItems" class="w-44" />
          <UInput v-model="employeeSearch" icon="i-lucide-search" placeholder="Search employee" class="w-56" />
        </div>
      </div>
      <div class="overflow-auto">
        <table class="w-full min-w-[1080px] text-left text-sm">
          <thead class="bg-muted/30 text-xs uppercase text-muted">
            <tr>
              <th class="px-3 py-2">Code</th>
              <th class="px-3 py-2">Employee</th>
              <th class="px-3 py-2">Mobile</th>
              <th class="px-3 py-2">Department</th>
              <th class="px-3 py-2">Designation</th>
              <th class="px-3 py-2">Salary/Wage</th>
              <th class="px-3 py-2">Joining</th>
              <th class="px-3 py-2">Status</th>
              <th class="px-3 py-2 text-right">Action</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="employee in pagedEmployees" :key="readText(employee, ['id'])" class="border-t border-default align-top">
              <td class="px-3 py-2">{{ readText(employee, ['employeeCode'], `EMP-${String(readNumber(employee, ['empId'])).padStart(4, '0')}`) }}</td>
              <td class="px-3 py-2">
                <p class="font-medium">{{ employeeName(employee) }}</p>
                <p class="text-xs text-muted">{{ categoryLabel(readNumber(employee, ['category'])) }}</p>
              </td>
              <td class="px-3 py-2">{{ maskMobile(readText(employee, ['mobile'])) }}</td>
              <td class="px-3 py-2">{{ readText(employee, ['department']) }}</td>
              <td class="px-3 py-2">{{ readText(employee, ['designation']) }}</td>
              <td class="px-3 py-2">{{ formatIndianMoney(readNumber(employee, ['monthlySalary']) || readNumber(employee, ['dailyWage'])) }}</td>
              <td class="px-3 py-2">{{ formatDate(readText(employee, ['joiningDate'])) }}</td>
              <td class="px-3 py-2">
                <UBadge :color="isActiveEmployee(employee) ? 'success' : 'neutral'" variant="subtle">{{ readText(employee, ['employeeStatus'], isActiveEmployee(employee) ? 'Active' : 'Inactive') }}</UBadge>
              </td>
              <td class="px-3 py-2">
                <div class="flex justify-end gap-1">
                  <UButton size="xs" icon="i-lucide-pencil" color="neutral" variant="ghost" @click="startEmployeeEdit(employee)">Edit</UButton>
                  <UButton size="xs" icon="i-lucide-badge" color="primary" variant="ghost" :loading="idCardLoading === readText(employee, ['id'])" @click="openIdCard(employee)">ID Card</UButton>
                  <UButton size="xs" icon="i-lucide-trash-2" color="error" variant="ghost" :loading="deletingId === readText(employee, ['id'])" @click="removeEmployee(employee)">Delete</UButton>
                </div>
              </td>
            </tr>
            <tr v-if="!pagedEmployees.length">
              <td class="px-3 py-6 text-center text-muted" colspan="9">No employees found.</td>
            </tr>
          </tbody>
        </table>
      </div>
      <div class="mt-3 flex flex-wrap items-center justify-between gap-2 text-sm text-muted">
        <p>Page {{ employeePage }} of {{ employeeTotalPages }}</p>
        <div class="flex items-center gap-2">
          <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-left" :disabled="employeePage <= 1" @click="employeePage--">Prev</UButton>
          <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-right" :disabled="employeePage >= employeeTotalPages" @click="employeePage++">Next</UButton>
        </div>
      </div>
    </div>

    <div v-else-if="activeTab === 'attendance'" class="garmetix-table-panel">
      <div class="garmetix-panel-header">
        <div>
          <h3 class="garmetix-panel-title">Daily Attendance Register</h3>
          <p class="garmetix-panel-subtitle">{{ attendanceTotal }} row(s) for selected month.</p>
        </div>
        <form class="flex flex-wrap items-end gap-2" @submit.prevent="loadAttendance">
          <UInput v-model.number="attendanceFilters.year" type="number" class="w-24" />
          <UInput v-model.number="attendanceFilters.month" type="number" min="1" max="12" class="w-20" />
          <USelect v-model.number="attendanceStatusFilter" :items="attendanceStatusFilterItems" class="w-40" />
          <UButton type="submit" size="sm" :loading="loadingAttendance">Load</UButton>
        </form>
      </div>
      <div class="overflow-auto">
        <table class="w-full min-w-[980px] text-left text-sm">
          <thead class="bg-muted/30 text-xs uppercase text-muted">
            <tr>
              <th class="px-3 py-2">Date</th>
              <th class="px-3 py-2">Employee</th>
              <th class="px-3 py-2">Status</th>
              <th class="px-3 py-2">In</th>
              <th class="px-3 py-2">Break</th>
              <th class="px-3 py-2">Out</th>
              <th class="px-3 py-2">Remarks</th>
              <th class="px-3 py-2 text-right">Action</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="row in attendanceRows" :key="readText(row, ['id'])" class="border-t border-default">
              <td class="px-3 py-2">{{ formatDate(readText(row, ['onDate'])) }}</td>
              <td class="px-3 py-2">{{ readText(row, ['employeeName']) }}</td>
              <td class="px-3 py-2">{{ attendanceStatusLabel(readNumber(row, ['status'])) }}</td>
              <td class="px-3 py-2">{{ readText(row, ['checkInTime']) }}</td>
              <td class="px-3 py-2">{{ readText(row, ['breakOutTime']) }} / {{ readText(row, ['breakInTime']) }}</td>
              <td class="px-3 py-2">{{ readText(row, ['checkOutTime']) }}</td>
              <td class="px-3 py-2">{{ readText(row, ['remarks']) }}</td>
              <td class="px-3 py-2 text-right"><UButton size="xs" icon="i-lucide-pencil" color="primary" variant="soft" @click="startAttendanceEdit(row)">Edit</UButton></td>
            </tr>
            <tr v-if="!attendanceRows.length">
              <td class="px-3 py-6 text-center text-muted" colspan="8">No daily attendance rows found.</td>
            </tr>
          </tbody>
        </table>
      </div>
      <div class="mt-3 flex flex-wrap items-center justify-between gap-2 text-sm text-muted">
        <p>Page {{ attendanceFilters.page }} of {{ attendanceTotalPages }}</p>
        <div class="flex items-center gap-2">
          <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-left" :disabled="attendanceFilters.page <= 1" :loading="loadingAttendance" @click="changeAttendancePage(-1)">Prev</UButton>
          <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-right" :disabled="attendanceFilters.page >= attendanceTotalPages" :loading="loadingAttendance" @click="changeAttendancePage(1)">Next</UButton>
        </div>
      </div>
    </div>

    <div v-else class="grid gap-4 xl:grid-cols-[minmax(360px,0.7fr)_minmax(0,1.5fr)]">
      <form class="garmetix-section-card space-y-3" @submit.prevent="generateMonthlyAttendance">
        <h3 class="garmetix-panel-title">Generate Monthly Attendance</h3>
        <p class="garmetix-panel-subtitle">Runs `/api/hr/monthly-attendance/generate` for the selected period.</p>
        <div class="grid gap-3 md:grid-cols-2">
          <UFormField label="Year"><UInput v-model.number="monthlyForm.year" type="number" min="2020" max="2100" /></UFormField>
          <UFormField label="Month"><UInput v-model.number="monthlyForm.month" type="number" min="1" max="12" /></UFormField>
        </div>
        <UButton type="submit" color="primary" icon="i-lucide-wand-sparkles" :loading="generatingMonthly">Generate Month</UButton>
      </form>

      <div class="garmetix-section-card">
        <h3 class="garmetix-panel-title">Monthly Generation Result</h3>
        <pre class="mt-3 max-h-[360px] overflow-auto rounded-lg border border-default bg-default/40 p-3 text-xs">{{ monthlyResultText }}</pre>
      </div>
    </div>

    <USlideover v-model:open="employeeFormOpen" :title="editingEmployeeId ? 'Edit Employee' : 'New Employee'">
      <template #body>
        <form class="space-y-3" @submit.prevent="saveEmployee">
          <div class="garmetix-panel-header">
            <p class="garmetix-panel-subtitle">Photo, bank and document fields are used by ID card, payroll and future biometric attendance.</p>
            <UBadge color="primary" variant="subtle">{{ employeeForm.employeeStatus }}</UBadge>
          </div>

          <div class="grid gap-3 md:grid-cols-2">
            <UFormField label="Title"><UInput v-model="employeeForm.title" /></UFormField>
            <UFormField label="First name" required><UInput v-model="employeeForm.firstName" /></UFormField>
            <UFormField label="Last name" required><UInput v-model="employeeForm.lastName" /></UFormField>
            <UFormField label="Employee code"><UInput v-model="employeeForm.employeeCode" placeholder="Auto if blank" /></UFormField>
            <UFormField label="Gender"><USelect v-model="employeeForm.gender" :items="genderOptions" /></UFormField>
            <UFormField label="Category"><USelect v-model="employeeForm.category" :items="categoryOptions" /></UFormField>
            <UFormField label="Department"><UInput v-model="employeeForm.department" /></UFormField>
            <UFormField label="Designation"><UInput v-model="employeeForm.designation" /></UFormField>
            <UFormField label="Status"><USelect v-model="employeeForm.employeeStatus" :items="employeeStatusOptions" /></UFormField>
            <UFormField label="Joining date"><UInput v-model="employeeForm.joiningDate" type="date" /></UFormField>
            <UFormField label="Leaving date"><UInput v-model="employeeForm.leavingDate" type="date" /></UFormField>
            <UFormField label="Date of birth"><UInput v-model="employeeForm.dateOfBirth" type="date" /></UFormField>
            <UFormField label="Salary type"><USelect v-model="employeeForm.salaryType" :items="salaryTypeOptions" /></UFormField>
            <UFormField label="Monthly salary"><UInput v-model.number="employeeForm.monthlySalary" type="number" min="0" /></UFormField>
            <UFormField label="Daily wage"><UInput v-model.number="employeeForm.dailyWage" type="number" min="0" /></UFormField>
          </div>

          <div class="grid gap-3 md:grid-cols-2">
            <UFormField label="Father/Husband"><UInput v-model="employeeForm.fatherOrHusbandName" /></UFormField>
            <UFormField label="Blood group"><UInput v-model="employeeForm.bloodGroup" /></UFormField>
            <UFormField label="Mobile"><UInput v-model="employeeForm.mobile" /></UFormField>
            <UFormField label="Email"><UInput v-model="employeeForm.email" type="email" /></UFormField>
            <UFormField label="PAN"><UInput v-model="employeeForm.pan" /></UFormField>
            <UFormField label="Aadhaar"><UInput v-model="employeeForm.aadhar" /></UFormField>
            <UFormField label="Bank account name"><UInput v-model="employeeForm.bankAccountName" /></UFormField>
            <UFormField label="Bank account number"><UInput v-model="employeeForm.bankAccountNumber" /></UFormField>
            <UFormField label="IFSC"><UInput v-model="employeeForm.ifsc" /></UFormField>
            <UFormField label="ESI number"><UInput v-model="employeeForm.esiNumber" /></UFormField>
            <UFormField label="PF number"><UInput v-model="employeeForm.pfNumber" /></UFormField>
            <UFormField label="Emergency contact"><UInput v-model="employeeForm.emergencyContact" /></UFormField>
          </div>

          <UFormField label="Photo Data URL"><UTextarea v-model="employeeForm.photoDataUrl" :rows="2" placeholder="Paste base64/data URL or keep blank" /></UFormField>
          <UFormField label="Exit reason"><UInput v-model="employeeForm.exitReason" /></UFormField>

          <div class="flex flex-wrap justify-end gap-2">
            <UButton type="button" color="neutral" variant="soft" @click="resetEmployeeForm">Clear</UButton>
            <UButton type="submit" color="primary" icon="i-lucide-save" :loading="savingEmployee" :disabled="!canSaveEmployee">Save Employee</UButton>
          </div>
        </form>
      </template>
    </USlideover>

    <UModal v-model:open="attendanceFormOpen" :title="editingAttendanceId ? 'Edit Attendance' : 'New Attendance'">
      <template #body>
        <form class="space-y-3" @submit.prevent="saveAttendance">
          <p class="garmetix-panel-subtitle">Older attendance register entry used by legacy HR and monthly generation.</p>
          <UFormField label="Employee" required><USelectMenu v-model="attendanceForm.employeeId" value-key="value" :items="employeeOptions" placeholder="Search employee..." /></UFormField>
          <div class="grid gap-3 md:grid-cols-2">
            <UFormField label="Date"><UInput v-model="attendanceForm.onDate" type="date" /></UFormField>
            <UFormField label="Status"><USelect v-model="attendanceForm.status" :items="attendanceStatusOptions" /></UFormField>
            <UFormField label="Check in"><UInput v-model="attendanceForm.checkInTime" type="time" /></UFormField>
            <UFormField label="Break out"><UInput v-model="attendanceForm.breakOutTime" type="time" /></UFormField>
            <UFormField label="Break in"><UInput v-model="attendanceForm.breakInTime" type="time" /></UFormField>
            <UFormField label="Check out"><UInput v-model="attendanceForm.checkOutTime" type="time" /></UFormField>
          </div>
          <UFormField label="Remarks"><UTextarea v-model="attendanceForm.remarks" :rows="3" /></UFormField>
          <div class="flex flex-wrap justify-end gap-2">
            <UButton type="button" color="neutral" variant="soft" @click="resetAttendanceForm">Clear</UButton>
            <UButton type="submit" color="primary" icon="i-lucide-save" :loading="savingAttendance" :disabled="!attendanceForm.employeeId">Save Attendance</UButton>
          </div>
        </form>
      </template>
    </UModal>

    <UModal v-model:open="idCardOpen" title="Employee ID Card">
      <template #body>
        <div v-if="idCardData" class="employee-id-card-print">
          <div class="employee-id-card">
            <div class="employee-id-card-header">
              <strong>{{ readText(idCardData, ['companyName']) }}</strong>
              <span>{{ readText(idCardData, ['storeName']) }}</span>
            </div>
            <div class="employee-id-card-body">
              <img v-if="readText(idCardData, ['photoDataUrl'])" :src="readText(idCardData, ['photoDataUrl'])" alt="Employee">
              <div v-else class="employee-id-card-photo">PHOTO</div>
              <div>
                <h2>{{ readText(idCardData, ['fullName']) }}</h2>
                <p>{{ readText(idCardData, ['employeeCode']) }}</p>
                <p>{{ readText(idCardData, ['designation']) }} / {{ readText(idCardData, ['department']) }}</p>
                <p>Mobile: {{ readText(idCardData, ['mobile']) }}</p>
                <p>Emergency: {{ readText(idCardData, ['emergencyContact']) }}</p>
                <p>Blood: {{ readText(idCardData, ['bloodGroup']) }}</p>
              </div>
            </div>
          </div>
          <UButton class="mt-3" icon="i-lucide-printer" color="primary" @click="printIdCard">Print ID Card</UButton>
        </div>
      </template>
    </UModal>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney, isActiveEmployee } from '@garmetix/shared-utils'
import { currentYearMonth, readArray, readNumber, readText, toLocalDateInput, type ApiRecord, useHrApiClient } from '../utils/hr-api'

type HrTab = 'employees' | 'attendance' | 'monthly'

useHead({ title: 'Employees - Garmetix HR' })

const { get, post, put, del } = useHrApiClient()
const current = currentYearMonth()
const loading = ref(false)
const loadingAttendance = ref(false)
const savingEmployee = ref(false)
const savingAttendance = ref(false)
const generatingMonthly = ref(false)
const deletingId = ref('')
const message = ref('')
const messageTone = ref<'success' | 'error' | 'warning' | 'neutral'>('neutral')
const activeTab = ref<HrTab>('employees')
const employeeSearch = ref('')
const employeeStatusFilter = ref<'all' | 'active' | 'inactive'>('all')
const editingEmployeeId = ref('')
const editingAttendanceId = ref('')
const employeeFormOpen = ref(false)
const attendanceFormOpen = ref(false)
const idCardOpen = ref(false)
const idCardData = ref<ApiRecord | null>(null)
const idCardLoading = ref('')
const employeePage = ref(1)
const employeePageSize = ref(20)
const attendanceTotal = ref(0)
const attendanceStatusFilter = ref<number | null>(null)
const employees = ref<ApiRecord[]>([])
const stores = ref<ApiRecord[]>([])
const setupStatus = ref<ApiRecord | null>(null)
const attendanceRows = ref<ApiRecord[]>([])
const summary = ref<ApiRecord | null>(null)
const monthlyResult = ref<ApiRecord | null>(null)
const employeeForm = reactive(emptyEmployee())
const attendanceForm = reactive(emptyAttendance())
const attendanceFilters = reactive({ year: current.year, month: current.month, page: 1, pageSize: 50 })
const monthlyForm = reactive({ year: current.year, month: current.month })

const tabs = [
  { key: 'employees' as HrTab, label: 'Employees', icon: 'i-lucide-users-round' },
  { key: 'attendance' as HrTab, label: 'Daily Attendance', icon: 'i-lucide-calendar-check' },
  { key: 'monthly' as HrTab, label: 'Monthly Generation', icon: 'i-lucide-calendar-range' }
]
const genderOptions = [{ value: 0, label: 'Male' }, { value: 1, label: 'Female' }, { value: 2, label: 'TransGender' }]
const categoryOptions = [{ value: 0, label: 'Salesman' }, { value: 1, label: 'Store Manager' }, { value: 2, label: 'House Keeping' }, { value: 3, label: 'Owner' }, { value: 4, label: 'Accounts' }, { value: 8, label: 'Others' }]
const employeeStatusOptions = ['Active', 'On Leave', 'Resigned', 'Terminated', 'Inactive']
const salaryTypeOptions = ['Monthly', 'Daily', 'PieceRate']
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
const attendanceStatusFilterItems = [{ value: null, label: 'All statuses' }, ...attendanceStatusOptions]
const employeeStatusFilterItems = [
  { value: 'all', label: 'All employees' },
  { value: 'active', label: 'Active only' },
  { value: 'inactive', label: 'Inactive only' }
]
const messageIcon = computed(() => messageTone.value === 'success' ? 'i-lucide-circle-check' : messageTone.value === 'warning' ? 'i-lucide-triangle-alert' : messageTone.value === 'error' ? 'i-lucide-circle-alert' : 'i-lucide-info')
const summaryMessages = computed(() => readArray(summary.value, ['readinessMessages']).map(item => String(item)))
const employeeOptions = computed(() => employees.value.filter(isActiveEmployee).map(employee => ({ label: `${readText(employee, ['employeeCode'], 'EMP')} - ${employeeName(employee)}`, value: readText(employee, ['id'], '') })))
const filteredEmployees = computed(() => {
  const term = employeeSearch.value.trim().toLowerCase()
  return employees.value.filter(row => {
    const matchesStatus = employeeStatusFilter.value === 'all'
      || (employeeStatusFilter.value === 'active' && isActiveEmployee(row))
      || (employeeStatusFilter.value === 'inactive' && !isActiveEmployee(row))
    const matchesSearch = !term || JSON.stringify(row).toLowerCase().includes(term)
    return matchesStatus && matchesSearch
  })
})
const employeeTotalPages = computed(() => Math.max(1, Math.ceil(filteredEmployees.value.length / employeePageSize.value)))
const pagedEmployees = computed(() => {
  const start = (employeePage.value - 1) * employeePageSize.value
  return filteredEmployees.value.slice(start, start + employeePageSize.value)
})
const attendanceTotalPages = computed(() => Math.max(1, Math.ceil(attendanceTotal.value / attendanceFilters.pageSize)))

watch(employeeSearch, () => { employeePage.value = 1 })
watch(employeeStatusFilter, () => { employeePage.value = 1 })
const cards = computed(() => [
  { label: 'Employees', value: readNumber(summary.value, ['totalEmployees']) || employees.value.length, detail: 'All employee master records' },
  { label: 'Active', value: readNumber(summary.value, ['activeEmployees']) || employees.value.filter(isActiveEmployee).length, detail: 'Working employees' },
  { label: 'Missing Photo', value: readNumber(summary.value, ['missingPhoto']), detail: 'ID card / face readiness' },
  { label: 'Missing Bank', value: readNumber(summary.value, ['missingBank']), detail: 'Payroll payout readiness' },
  { label: 'Salary Structure', value: readNumber(summary.value, ['withSalaryStructure']), detail: 'Current structure exists' },
  { label: 'Open Advances', value: readNumber(summary.value, ['openAdvanceCount']), detail: formatIndianMoney(readNumber(summary.value, ['openAdvanceAmount'])) }
])
const canSaveEmployee = computed(() => Boolean(employeeForm.firstName.trim() && employeeForm.lastName.trim() && employeeForm.mobile.trim()))
const monthlyResultText = computed(() => monthlyResult.value ? JSON.stringify(monthlyResult.value, null, 2) : 'Monthly generation has not run in this session.')

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
    joiningDate: toLocalDateInput(),
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
    onDate: toLocalDateInput(),
    status: 0,
    checkInTime: '',
    breakOutTime: '',
    breakInTime: '',
    checkOutTime: '',
    entryTime: '',
    remarks: ''
  }
}

function employeeName(employee: ApiRecord) {
  return readText(employee, ['staffName', 'fullName'], `${readText(employee, ['firstName'], '')} ${readText(employee, ['lastName'], '')}`.trim() || 'Employee')
}

function categoryLabel(value: number) {
  return categoryOptions.find(item => item.value === value)?.label || 'Employee'
}

function attendanceStatusLabel(value: number) {
  return attendanceStatusOptions.find(item => item.value === value)?.label || 'Status'
}

function formatDate(value: string) {
  if (!value || value === '-') return '-'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? value : date.toLocaleDateString()
}

function toApiDate(value: string) {
  return `${String(value || toLocalDateInput()).slice(0, 10)}T00:00:00`
}

function timeOrNull(value: string) {
  return value ? `${value}:00` : null
}

function resetEmployeeForm() {
  editingEmployeeId.value = ''
  Object.assign(employeeForm, emptyEmployee())
}

function resetAttendanceForm() {
  editingAttendanceId.value = ''
  Object.assign(attendanceForm, emptyAttendance())
}

function startEmployeeCreate() {
  activeTab.value = 'employees'
  resetEmployeeForm()
  employeeFormOpen.value = true
}

function startEmployeeEdit(employee: ApiRecord) {
  activeTab.value = 'employees'
  editingEmployeeId.value = readText(employee, ['id'], '')
  Object.assign(employeeForm, emptyEmployee(), employee, {
    dateOfBirth: readText(employee, ['dateOfBirth'], '1990-01-01').slice(0, 10),
    joiningDate: readText(employee, ['joiningDate'], toLocalDateInput()).slice(0, 10),
    leavingDate: readText(employee, ['leavingDate'], '').slice(0, 10)
  })
  employeeFormOpen.value = true
}

function startAttendanceCreate() {
  activeTab.value = 'attendance'
  resetAttendanceForm()
  attendanceFormOpen.value = true
}

function startAttendanceEdit(row: ApiRecord) {
  activeTab.value = 'attendance'
  editingAttendanceId.value = readText(row, ['id'], '')
  Object.assign(attendanceForm, emptyAttendance(), {
    employeeId: readText(row, ['employeeId'], ''),
    onDate: readText(row, ['onDate'], toLocalDateInput()).slice(0, 10),
    status: readNumber(row, ['status']),
    checkInTime: readText(row, ['checkInTime'], '').slice(0, 5),
    breakOutTime: readText(row, ['breakOutTime'], '').slice(0, 5),
    breakInTime: readText(row, ['breakInTime'], '').slice(0, 5),
    checkOutTime: readText(row, ['checkOutTime'], '').slice(0, 5),
    remarks: readText(row, ['remarks'], '')
  })
  attendanceFormOpen.value = true
}

function maskMobile(value: string) {
  const digits = String(value || '').replace(/\D/g, '')
  return digits.length > 4 ? `${'•'.repeat(digits.length - 4)}${digits.slice(-4)}` : (digits || '-')
}

async function openIdCard(employee: ApiRecord) {
  const id = readText(employee, ['id'], '')
  if (!id) return
  idCardLoading.value = id
  message.value = ''
  try {
    idCardData.value = await get<ApiRecord>(`api/hr/employees/${id}/id-card`)
    idCardOpen.value = true
  } catch (caught) {
    messageTone.value = 'error'
    message.value = caught instanceof Error ? caught.message : 'Unable to open employee ID card.'
  } finally {
    idCardLoading.value = ''
  }
}

function printIdCard() {
  if (import.meta.client) window.print()
}

function changeAttendancePage(delta: number) {
  attendanceFilters.page = Math.max(1, attendanceFilters.page + delta)
  loadAttendance()
}

function employeePayload() {
  const mobile = String(employeeForm.mobile || '').replace(/\D/g, '')
  const aadhar = String(employeeForm.aadhar || '').replace(/\D/g, '')
  const pan = String(employeeForm.pan || '').trim().toUpperCase()
  const scope = firstScope()
  return {
    ...employeeForm,
    firstName: employeeForm.firstName.trim(),
    lastName: employeeForm.lastName.trim(),
    gender: Number(employeeForm.gender),
    dateOfBirth: toApiDate(employeeForm.dateOfBirth),
    empId: Number(employeeForm.empId || 0),
    employeeCode: employeeForm.employeeCode.trim().toUpperCase() || null,
    monthlySalary: Number(employeeForm.monthlySalary || 0),
    dailyWage: Number(employeeForm.dailyWage || 0),
    joiningDate: toApiDate(employeeForm.joiningDate),
    leavingDate: employeeForm.leavingDate ? toApiDate(employeeForm.leavingDate) : null,
    working: ['Active', 'On Leave'].includes(employeeForm.employeeStatus),
    category: Number(employeeForm.category),
    pan: pan || null,
    aadhar,
    mobile,
    ifsc: employeeForm.ifsc.trim().toUpperCase() || null,
    companyId: scope.companyId,
    storeGroupId: scope.storeGroupId,
    storeId: scope.storeId
  }
}

function firstScope() {
  const employeeScope = employees.value.find(item => readText(item, ['companyId'], '') && readText(item, ['storeId'], ''))
  const storeScope = stores.value.find(item => readText(item, ['companyId'], '') && readText(item, ['id'], ''))
  const companyId = readText(employeeScope, ['companyId'], '') || readText(setupStatus.value, ['companyId'], '') || readText(storeScope, ['companyId'], '')
  const storeGroupId = readText(employeeScope, ['storeGroupId'], '') || readText(setupStatus.value, ['storeGroupId'], '') || readText(storeScope, ['storeGroupId'], '')
  const storeId = readText(employeeScope, ['storeId'], '') || readText(setupStatus.value, ['storeId'], '') || readText(storeScope, ['id'], '')
  if (!companyId || !storeGroupId || !storeId) throw new Error('Run setup and create a store before saving employees.')
  return { companyId, storeGroupId, storeId }
}

function attendancePayload() {
  const employee = employees.value.find(item => readText(item, ['id'], '') === attendanceForm.employeeId)
  return {
    employeeId: attendanceForm.employeeId,
    onDate: toApiDate(attendanceForm.onDate),
    status: Number(attendanceForm.status),
    checkInTime: timeOrNull(attendanceForm.checkInTime),
    breakOutTime: timeOrNull(attendanceForm.breakOutTime),
    breakInTime: timeOrNull(attendanceForm.breakInTime),
    checkOutTime: timeOrNull(attendanceForm.checkOutTime),
    entryTime: '',
    remarks: attendanceForm.remarks?.trim() || null,
    companyId: readText(employee, ['companyId'], ''),
    storeGroupId: readText(employee, ['storeGroupId'], ''),
    storeId: readText(employee, ['storeId'], '')
  }
}

async function load() {
  loading.value = true
  message.value = ''
  try {
    const [employeeRows, summaryRow, storeRows, setupRow] = await Promise.all([
      get<ApiRecord[]>('api/employees'),
      get<ApiRecord>('api/hr/employee-master/summary'),
      get<ApiRecord[]>('api/stores'),
      get<ApiRecord>('api/setup/status')
    ])
    employees.value = Array.isArray(employeeRows) ? employeeRows : []
    summary.value = summaryRow
    stores.value = Array.isArray(storeRows) ? storeRows : []
    setupStatus.value = setupRow
    if (activeTab.value === 'attendance') await loadAttendance()
  } catch (caught) {
    messageTone.value = 'error'
    message.value = caught instanceof Error ? caught.message : 'Unable to load HR employee master.'
  } finally {
    loading.value = false
  }
}

async function loadAttendance() {
  loadingAttendance.value = true
  try {
    const response = await get<ApiRecord>('api/hr/attendance', {
      ...attendanceFilters,
      status: attendanceStatusFilter.value ?? undefined
    })
    attendanceRows.value = readArray(response, ['items'])
    attendanceTotal.value = readNumber(response, ['total'])
  } catch (caught) {
    messageTone.value = 'error'
    message.value = caught instanceof Error ? caught.message : 'Unable to load daily attendance.'
  } finally {
    loadingAttendance.value = false
  }
}

async function saveEmployee() {
  savingEmployee.value = true
  message.value = ''
  try {
    if (editingEmployeeId.value) {
      await put<ApiRecord>(`api/employees/${editingEmployeeId.value}`, employeePayload())
      message.value = 'Employee updated.'
    } else {
      await post<ApiRecord>('api/employees', employeePayload())
      message.value = 'Employee saved.'
    }
    messageTone.value = 'success'
    resetEmployeeForm()
    employeeFormOpen.value = false
    await load()
  } catch (caught) {
    messageTone.value = 'error'
    message.value = caught instanceof Error ? caught.message : 'Unable to save employee.'
  } finally {
    savingEmployee.value = false
  }
}

async function saveAttendance() {
  savingAttendance.value = true
  message.value = ''
  try {
    if (editingAttendanceId.value) {
      await put<ApiRecord>(`api/attendance/${editingAttendanceId.value}`, attendancePayload())
      message.value = 'Attendance updated.'
    } else {
      await post<ApiRecord>('api/attendance', attendancePayload())
      message.value = 'Attendance saved.'
    }
    messageTone.value = 'success'
    resetAttendanceForm()
    attendanceFormOpen.value = false
    await loadAttendance()
  } catch (caught) {
    messageTone.value = 'error'
    message.value = caught instanceof Error ? caught.message : 'Unable to save attendance.'
  } finally {
    savingAttendance.value = false
  }
}

async function removeEmployee(employee: ApiRecord) {
  const id = readText(employee, ['id'], '')
  if (!id || !window.confirm(`Delete employee ${employeeName(employee)}?`)) return
  deletingId.value = id
  message.value = ''
  try {
    await del<void>(`api/employees/${id}`)
    messageTone.value = 'success'
    message.value = 'Employee deleted.'
    await load()
  } catch (caught) {
    messageTone.value = 'error'
    message.value = caught instanceof Error ? caught.message : 'Unable to delete employee.'
  } finally {
    deletingId.value = ''
  }
}

async function generateMonthlyAttendance() {
  generatingMonthly.value = true
  message.value = ''
  try {
    monthlyResult.value = await post<ApiRecord>('api/hr/monthly-attendance/generate', {
      year: monthlyForm.year,
      month: monthlyForm.month,
      companyId: null,
      storeGroupId: null,
      storeId: null
    })
    messageTone.value = 'success'
    message.value = 'Monthly attendance generated.'
  } catch (caught) {
    messageTone.value = 'error'
    message.value = caught instanceof Error ? caught.message : 'Unable to generate monthly attendance.'
  } finally {
    generatingMonthly.value = false
  }
}

watch(activeTab, async tab => {
  if (tab === 'attendance' && attendanceRows.value.length === 0) await loadAttendance()
})

async function autoGenerateIfMonthEnd() {
  if (!import.meta.client) return

  const today = new Date()
  const tomorrow = new Date(today)
  tomorrow.setDate(today.getDate() + 1)
  if (tomorrow.getDate() !== 1) return

  const key = `garmetix.monthlyAttendance.${today.getFullYear()}.${today.getMonth() + 1}`
  if (localStorage.getItem(key)) return

  monthlyForm.year = today.getFullYear()
  monthlyForm.month = today.getMonth() + 1
  await generateMonthlyAttendance()
  localStorage.setItem(key, 'generated')
}

onMounted(async () => {
  await load()
  await autoGenerateIfMonthEnd()
})
</script>

<style scoped>
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
