<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-calendar-days" class="size-4" /> Employee review</p>
          <h2 class="garmetix-dashboard-title">Employee Attendance Calendar</h2>
          <p class="garmetix-dashboard-subtitle">One employee, one month: every day on a calendar with the summary below. Pick an employee, a month and a year.</p>
        </div>
        <UButton color="neutral" variant="soft" icon="i-lucide-refresh-cw" :loading="loading" :disabled="!employeeId" @click="load">Refresh</UButton>
      </div>
    </div>

    <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="error" />

    <div class="garmetix-section-card grid gap-3 md:grid-cols-2 xl:grid-cols-[minmax(240px,1.4fr)_160px_120px_auto]">
      <UFormField label="Employee">
        <USelectMenu v-model="employeeId" :items="employeeItems" value-key="value" placeholder="Search employee" class="w-full" />
      </UFormField>
      <UFormField label="Month"><USelect v-model="month" :items="monthItems" class="w-full" /></UFormField>
      <UFormField label="Year"><USelect v-model="year" :items="yearItems" class="w-full" /></UFormField>
      <div class="flex flex-wrap items-end gap-2">
        <UButton color="neutral" variant="soft" icon="i-lucide-chevron-left" @click="shiftMonth(-1)">Prev</UButton>
        <UButton color="neutral" variant="soft" icon="i-lucide-calendar-check" @click="goToday">This month</UButton>
        <UButton color="neutral" variant="soft" trailing-icon="i-lucide-chevron-right" @click="shiftMonth(1)">Next</UButton>
        <UCheckbox v-model="includeInactive" label="Include inactive" />
      </div>
    </div>

    <div v-if="!employeeId" class="garmetix-section-card p-8 text-center text-muted">Select an employee to see their attendance.</div>

    <template v-else>
      <div class="garmetix-section-card grid gap-3">
        <div class="flex flex-wrap items-center justify-between gap-2">
          <div>
            <h3 class="text-lg font-semibold text-highlighted">{{ employeeName }}</h3>
            <p class="text-sm text-muted">{{ monthLabel }} {{ year }}<span v-if="employeeCode"> | {{ employeeCode }}</span></p>
          </div>
          <div class="flex flex-wrap gap-2 text-xs">
            <span v-for="item in legend" :key="item.group" class="inline-flex items-center gap-1 rounded-md border px-2 py-1" :class="item.cls">{{ item.label }}</span>
          </div>
        </div>

        <div v-if="loading" class="p-8 text-center text-muted">Loading attendance...</div>
        <div v-else class="overflow-x-auto">
          <div class="grid min-w-[720px] grid-cols-7 gap-1">
            <div v-for="(name, index) in weekdayNames" :key="name" class="p-2 text-center text-xs font-semibold uppercase" :class="index === 0 ? 'text-blue-400' : 'text-muted'">{{ name }}</div>
            <div v-for="n in leadingBlanks" :key="`b${n}`" />
            <div
              v-for="day in days"
              :key="day.date"
              class="min-h-[84px] rounded-md border p-2 text-sm"
              :class="cellClass(day.group)"
              :title="[day.label, day.checkIn ? `In ${day.checkIn}` : '', day.checkOut ? `Out ${day.checkOut}` : '', day.remarks || ''].filter(Boolean).join(' | ')"
            >
              <div class="flex items-start justify-between">
                <span class="text-base font-semibold">{{ day.day }}</span>
                <span v-if="day.date === todayIso" class="rounded bg-primary/20 px-1 text-[10px] font-semibold text-primary">TODAY</span>
              </div>
              <p class="mt-1 text-xs font-semibold leading-tight">{{ day.label }}</p>
              <p v-if="day.checkIn || day.checkOut" class="mt-1 text-[11px] opacity-80">{{ day.checkIn || '--' }} - {{ day.checkOut || '--' }}</p>
            </div>
          </div>
        </div>
      </div>

      <section v-if="summary" class="grid gap-3 md:grid-cols-3 xl:grid-cols-5">
        <div v-for="card in summaryCards" :key="card.label" class="garmetix-metric-card">
          <p class="garmetix-metric-label">{{ card.label }}</p>
          <p class="garmetix-metric-value" :class="card.cls">{{ card.value }}</p>
          <p class="garmetix-metric-caption">{{ card.caption }}</p>
        </div>
      </section>
    </template>
  </section>
</template>

<script setup lang="ts">
import { isActiveEmployee } from '@garmetix/shared-utils'
import { currentYearMonth, readNumber, readText, type ApiRecord, useHrApiClient } from '../../utils/hr-api'

useHead({ title: 'Employee Attendance Calendar - Garmetix HR' })

type Day = { date: string, day: number, group: string, label: string, checkIn?: string | null, checkOut?: string | null, remarks?: string | null }

const { get } = useHrApiClient()
const current = currentYearMonth()
const year = ref(current.year)
const month = ref(current.month)
const employeeId = ref('')
const includeInactive = ref(false)
const employees = ref<ApiRecord[]>([])
const loading = ref(false)
const error = ref('')
const calendar = ref<ApiRecord | null>(null)

const todayIso = new Date().toISOString().slice(0, 10)
const weekdayNames = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat']
const monthNames = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December']
const monthItems = monthNames.map((label, index) => ({ label, value: index + 1 }))
const yearItems = Array.from({ length: 8 }, (_, index) => current.year - 6 + index).map(value => ({ label: String(value), value }))
const monthLabel = computed(() => monthNames[month.value - 1])

function fullName(employee: ApiRecord) {
  const full = readText(employee, ['fullName'], '').trim()
  if (full) return full
  return [readText(employee, ['title'], ''), readText(employee, ['firstName'], ''), readText(employee, ['lastName'], '')].filter(Boolean).join(' ').trim() || 'Employee'
}

const employeeItems = computed(() => employees.value
  .filter(item => includeInactive.value || isActiveEmployee(item))
  .map(item => ({ value: readText(item, ['id'], ''), label: `${fullName(item)} | ${readText(item, ['employeeCode', 'code'], '-')}` }))
  .filter(item => item.value)
  .sort((a, b) => a.label.localeCompare(b.label)))

const days = computed<Day[]>(() => ((calendar.value?.days as Day[]) || []))
const summary = computed(() => (calendar.value?.summary as ApiRecord) || null)
const leadingBlanks = computed(() => Number(calendar.value?.firstDayOfWeek ?? 0))
const employeeName = computed(() => readText((calendar.value?.employee as ApiRecord) || null, ['name'], employeeItems.value.find(item => item.value === employeeId.value)?.label || ''))
const employeeCode = computed(() => readText((calendar.value?.employee as ApiRecord) || null, ['employeeCode'], ''))

const legend = [
  { group: 'present', label: 'Present', cls: 'border-emerald-500/40 bg-emerald-500/10 text-emerald-400' },
  { group: 'half', label: 'Half day', cls: 'border-orange-500/40 bg-orange-500/10 text-orange-400' },
  { group: 'absent', label: 'Absent / Leave', cls: 'border-red-500/40 bg-red-500/10 text-red-400' },
  { group: 'sunday', label: 'Sunday', cls: 'border-blue-500/40 bg-blue-500/10 text-blue-400' },
  { group: 'closed', label: 'Store closed / Holiday', cls: 'border-yellow-500/40 bg-yellow-500/10 text-yellow-400' },
  { group: 'none', label: 'No record', cls: 'border-dashed border-default text-muted' }
]

function cellClass(group: string) {
  switch (group) {
    case 'present': return 'border-emerald-500/40 bg-emerald-500/10 text-emerald-400'
    case 'half': return 'border-orange-500/40 bg-orange-500/10 text-orange-400'
    case 'absent': return 'border-red-500/40 bg-red-500/10 text-red-400'
    case 'sunday': return 'border-blue-500/40 bg-blue-500/10 text-blue-400'
    case 'closed': return 'border-yellow-500/40 bg-yellow-500/10 text-yellow-400'
    case 'none': return 'border-dashed border-default text-muted'
    default: return 'border-default text-muted opacity-60'
  }
}

const summaryCards = computed(() => {
  const s = summary.value
  if (!s) return []
  const n = (key: string) => readNumber(s, [key])
  return [
    { label: 'Days in month', value: n('daysInMonth'), caption: `${monthLabel.value} ${year.value}`, cls: '' },
    { label: 'Sundays', value: n('sundays'), caption: 'Weekly off', cls: 'text-blue-400' },
    { label: 'Working days', value: n('workingDays'), caption: n('storeClosedDays') ? `${n('effectiveWorkingDays')} after ${n('storeClosedDays')} store-closed day(s)` : 'Days minus Sundays', cls: '' },
    { label: 'Present', value: n('present'), caption: 'Full days', cls: 'text-emerald-400' },
    { label: 'Half days', value: n('halfDays'), caption: 'Counted as 0.5 each', cls: 'text-orange-400' },
    { label: 'Absent', value: n('absent'), caption: 'Marked absent', cls: 'text-red-400' },
    { label: 'Leave', value: n('leaves'), caption: 'Sick, paid, casual, other', cls: 'text-red-400' },
    { label: 'Store closed / Holiday', value: n('storeClosedDays'), caption: 'Not counted as working', cls: 'text-yellow-400' },
    { label: 'No record', value: n('notRecorded'), caption: 'Past working days with no entry', cls: n('notRecorded') ? 'text-red-400' : '' },
    { label: 'Attendance', value: `${n('attendancePercent')}%`, caption: `${n('presentEquivalent')} day(s) of ${n('effectiveWorkingDays')} working`, cls: '' }
  ]
})

async function loadEmployees() {
  try {
    const response = await get<ApiRecord[] | { rows?: ApiRecord[], items?: ApiRecord[], data?: ApiRecord[] }>('api/employees')
    employees.value = Array.isArray(response) ? response : (response.rows || response.items || response.data || [])
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Could not load employees.'
  }
}

async function load() {
  if (!employeeId.value) return
  loading.value = true
  error.value = ''
  try {
    calendar.value = await get<ApiRecord>('api/hr/attendance-calendar', { employeeId: employeeId.value, year: year.value, month: month.value })
  } catch (caught) {
    calendar.value = null
    error.value = caught instanceof Error ? caught.message : 'Could not load attendance.'
  } finally {
    loading.value = false
  }
}

function shiftMonth(delta: number) {
  const date = new Date(year.value, month.value - 1 + delta, 1)
  year.value = date.getFullYear()
  month.value = date.getMonth() + 1
}

function goToday() {
  year.value = current.year
  month.value = current.month
}

watch([employeeId, year, month], load)
onMounted(loadEmployees)
</script>
