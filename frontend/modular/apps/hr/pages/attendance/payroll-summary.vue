<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
        <div>
          <p class="garmetix-dashboard-kicker">
            <UIcon name="i-lucide-file-spreadsheet" class="size-4" />
            Payroll attendance
          </p>
          <h2 class="garmetix-dashboard-title">Payroll Summary</h2>
          <p class="garmetix-dashboard-subtitle">Attendance totals used for salary draft and payslip review.</p>
        </div>
        <form class="flex flex-wrap items-end gap-2" @submit.prevent="load">
          <UFormField label="Year" name="year">
            <UInput v-model.number="year" type="number" min="2020" max="2100" />
          </UFormField>
          <UFormField label="Month" name="month">
            <UInput v-model.number="month" type="number" min="1" max="12" />
          </UFormField>
          <UButton type="submit" icon="i-lucide-refresh-cw" :loading="loading">Load</UButton>
          <UButton icon="i-lucide-file-down" color="primary" variant="soft" :disabled="!summary" @click="exportSummary">Export CSV</UButton>
        </form>
      </div>
    </div>

    <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="error" />

    <div class="grid gap-3 md:grid-cols-3 xl:grid-cols-6">
      <div v-for="card in cards" :key="card.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label">{{ card.label }}</p>
        <p class="garmetix-metric-value text-xl">{{ card.value }}</p>
      </div>
    </div>

    <div class="garmetix-table-panel">
      <div class="garmetix-panel-header">
        <div>
          <h3 class="garmetix-panel-title">Employee Breakdown</h3>
          <p class="garmetix-panel-subtitle">{{ rows.length }} employee(s) for {{ monthLabel }}</p>
        </div>
        <UInput v-model="search" icon="i-lucide-search" placeholder="Search employee" class="w-56" />
      </div>
      <div class="overflow-auto">
        <table class="w-full min-w-[900px] text-left text-sm">
          <thead class="bg-muted/30 text-xs uppercase text-muted">
            <tr>
              <th class="px-3 py-2">Employee</th>
              <th class="px-3 py-2">Present</th>
              <th class="px-3 py-2">Absent</th>
              <th class="px-3 py-2">Late</th>
              <th class="px-3 py-2">Half Day</th>
              <th class="px-3 py-2">Leave</th>
              <th class="px-3 py-2">Overtime</th>
              <th class="px-3 py-2">Status</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="row in pagedRows" :key="row.employeeId" class="border-t border-default">
              <td class="px-3 py-2 font-medium">{{ row.employeeName }}</td>
              <td class="px-3 py-2">{{ row.presentDays }}</td>
              <td class="px-3 py-2">{{ row.absentDays }}</td>
              <td class="px-3 py-2">{{ row.lateDays }}</td>
              <td class="px-3 py-2">{{ row.halfDays }}</td>
              <td class="px-3 py-2">{{ row.leaveDays }}</td>
              <td class="px-3 py-2">{{ formatMinutes(row.overtimeMinutes) }}</td>
              <td class="px-3 py-2">
                <UBadge :color="row.locked ? 'warning' : 'success'" variant="subtle">{{ row.locked ? 'Locked' : 'Open' }}</UBadge>
              </td>
            </tr>
            <tr v-if="!pagedRows.length">
              <td class="px-3 py-6 text-center text-muted" colspan="8">No payroll summary rows for this month yet. Generate monthly attendance first.</td>
            </tr>
          </tbody>
        </table>
      </div>
      <div v-if="rows.length" class="mt-3 flex flex-wrap items-center justify-between gap-2 text-sm text-muted">
        <p>Page {{ page }} of {{ totalPages }}</p>
        <div class="flex items-center gap-2">
          <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-left" :disabled="page <= 1" @click="page--">Prev</UButton>
          <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-right" :disabled="page >= totalPages" @click="page++">Next</UButton>
        </div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { currentYearMonth, downloadCsvFile, readNumber, readText, type ApiRecord, useHrApiClient } from '../../utils/hr-api'

useHead({ title: 'Payroll Summary - Garmetix HR' })

const { get } = useHrApiClient()
const current = currentYearMonth()
const year = ref(current.year)
const month = ref(current.month)
const loading = ref(false)
const error = ref('')
const search = ref('')
const page = ref(1)
const pageSize = ref(20)
const summary = ref<ApiRecord | null>(null)
const employees = ref<ApiRecord[]>([])

const monthLabel = computed(() => `${String(month.value).padStart(2, '0')}/${year.value}`)
const cards = computed(() => [
  { label: 'Employees', value: readNumber(summary.value, ['employees']) },
  { label: 'Present Days', value: readNumber(summary.value, ['presentDays']) },
  { label: 'Absent Days', value: readNumber(summary.value, ['absentDays']) },
  { label: 'Late Days', value: readNumber(summary.value, ['lateDays']) },
  { label: 'Half Days', value: readNumber(summary.value, ['halfDays']) },
  { label: 'Locked Rows', value: readText(summary.value, ['hasLockedRows'], 'false') === 'true' ? 'Yes' : 'No' }
])

function employeeName(id: string) {
  const employee = employees.value.find(item => readText(item, ['id'], '') === id)
  if (!employee) return 'Employee'
  const fullName = readText(employee, ['staffName', 'fullName'], '')
  return fullName || `${readText(employee, ['firstName'], '')} ${readText(employee, ['lastName'], '')}`.trim() || 'Employee'
}

const rows = computed(() => {
  const term = search.value.trim().toLowerCase()
  const source = Array.isArray((summary.value as ApiRecord | null)?.rows) ? (summary.value!.rows as ApiRecord[]) : []
  return source
    .map(row => ({
      employeeId: readText(row, ['employeeId'], ''),
      employeeName: employeeName(readText(row, ['employeeId'], '')),
      presentDays: readNumber(row, ['presentDays']),
      absentDays: readNumber(row, ['absentDays']),
      lateDays: readNumber(row, ['lateDays']),
      halfDays: readNumber(row, ['halfDays']),
      leaveDays: readNumber(row, ['leaveDays']),
      overtimeMinutes: readNumber(row, ['overtimeMinutes']),
      locked: readText(row, ['locked'], 'false') === 'true'
    }))
    .filter(row => !term || row.employeeName.toLowerCase().includes(term))
    .sort((a, b) => a.employeeName.localeCompare(b.employeeName))
})
const totalPages = computed(() => Math.max(1, Math.ceil(rows.value.length / pageSize.value)))
const pagedRows = computed(() => {
  const start = (page.value - 1) * pageSize.value
  return rows.value.slice(start, start + pageSize.value)
})

watch(search, () => { page.value = 1 })

function formatMinutes(value: number) {
  if (!value) return '0m'
  const hours = Math.floor(value / 60)
  const minutes = value % 60
  return hours ? `${hours}h ${minutes}m` : `${minutes}m`
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    const [summaryRow, employeeRows] = await Promise.all([
      get<ApiRecord>('api/attendance/payroll-summary', { year: year.value, month: month.value }),
      get<ApiRecord[]>('api/employees')
    ])
    summary.value = summaryRow
    employees.value = Array.isArray(employeeRows) ? employeeRows : []
    page.value = 1
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load payroll summary.'
  } finally {
    loading.value = false
  }
}

function exportSummary() {
  if (!summary.value) return
  downloadCsvFile(`garmetix-payroll-summary-${year.value}${String(month.value).padStart(2, '0')}.csv`, rows.value.map(row => ({
    employee: row.employeeName,
    presentDays: row.presentDays,
    absentDays: row.absentDays,
    lateDays: row.lateDays,
    halfDays: row.halfDays,
    leaveDays: row.leaveDays,
    overtimeMinutes: row.overtimeMinutes,
    locked: row.locked ? 'Yes' : 'No'
  })), [
    { key: 'employee', label: 'Employee' },
    { key: 'presentDays', label: 'Present Days' },
    { key: 'absentDays', label: 'Absent Days' },
    { key: 'lateDays', label: 'Late Days' },
    { key: 'halfDays', label: 'Half Days' },
    { key: 'leaveDays', label: 'Leave Days' },
    { key: 'overtimeMinutes', label: 'Overtime Minutes' },
    { key: 'locked', label: 'Locked' }
  ])
}

onMounted(load)
</script>
