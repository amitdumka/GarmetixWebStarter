<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-calendar-check" class="size-4" /> Attendance Core</p>
          <h2 class="garmetix-dashboard-title">Attendance Dashboard</h2>
          <p class="garmetix-dashboard-subtitle">
            Attendance core with web kiosk, photo review, manual punch and today's attendance in one view.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton icon="i-lucide-refresh-cw" color="primary" :loading="loading" @click="refresh">Refresh</UButton>
          <UButton to="/attendance/kiosk" icon="i-lucide-camera" color="neutral" variant="soft">Open Web Kiosk</UButton>
          <UButton to="/attendance/kiosk-monitor" icon="i-lucide-monitor-check" color="neutral" variant="soft">Kiosk Monitor</UButton>
          <UButton to="/attendance/photo-review" icon="i-lucide-user-check" color="neutral" variant="soft">Photo Review</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="error" />
    <UAlert v-if="message" :color="messageTone" variant="subtle" :icon="messageIcon" :description="message" />

    <div class="grid gap-3 md:grid-cols-4">
      <div v-for="card in cards" :key="card.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label">{{ card.label }}</p>
        <p class="garmetix-metric-value">{{ card.value }}</p>
      </div>
    </div>

    <div class="garmetix-section-card">
      <h3 class="garmetix-panel-title">Manual Punch</h3>
      <p class="garmetix-panel-subtitle">Record a CheckIn, BreakOut, BreakIn, CheckOut or Auto punch for an active employee.</p>
      <form class="mt-3 grid gap-3 md:grid-cols-4" @submit.prevent="savePunch">
        <UFormField label="Employee" name="employeeId">
          <USelect v-model="punchForm.employeeId" :items="employeeOptions" placeholder="Select employee" />
        </UFormField>
        <UFormField label="Punch Type" name="punchType">
          <USelect v-model="punchForm.punchType" :items="punchTypeOptions" />
        </UFormField>
        <UFormField label="Source" name="source">
          <USelect v-model="punchForm.source" :items="sourceOptions" />
        </UFormField>
        <UFormField label="Reason" name="reason">
          <UInput v-model="punchForm.reason" placeholder="Missed punch, manager correction..." />
        </UFormField>
        <div class="flex items-end md:col-span-4">
          <UButton type="submit" icon="i-lucide-save" :loading="punchSaving" :disabled="!canSavePunch">Save Punch</UButton>
        </div>
      </form>
    </div>

    <div class="garmetix-table-panel">
      <div class="garmetix-panel-header">
        <div>
          <h3 class="garmetix-panel-title">Today Attendance</h3>
          <p class="garmetix-panel-subtitle">{{ rows.length }} attendance row(s) for {{ loadedDate }}</p>
        </div>
      </div>
      <div class="overflow-auto">
        <table class="w-full min-w-[1080px] text-left text-sm">
          <thead class="bg-muted/30 text-xs uppercase text-muted">
            <tr>
              <th class="px-3 py-2">Employee</th>
              <th class="px-3 py-2">Status</th>
              <th class="px-3 py-2">Shift</th>
              <th class="px-3 py-2">In</th>
              <th class="px-3 py-2">Out</th>
              <th class="px-3 py-2">Work</th>
              <th class="px-3 py-2">OT</th>
              <th class="px-3 py-2">Late</th>
              <th class="px-3 py-2">Mode</th>
              <th class="px-3 py-2">Session</th>
              <th class="px-3 py-2">Review</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(row, index) in rows" :key="readText(row, ['employeeId', 'id'], String(index))" class="border-t border-default">
              <td class="px-3 py-2 font-medium">{{ readText(row, ['employeeName', 'name', 'employee']) }}</td>
              <td class="px-3 py-2">
                <UBadge :color="statusColor(row)" variant="subtle">{{ readText(row, ['status', 'attendanceStatus']) }}</UBadge>
              </td>
              <td class="px-3 py-2">{{ readText(row, ['shiftName']) }}</td>
              <td class="px-3 py-2">{{ formatDateTime(readText(row, ['inTime', 'firstIn', 'checkIn'])) }}</td>
              <td class="px-3 py-2">{{ formatDateTime(readText(row, ['outTime', 'lastOut', 'checkOut'])) }}</td>
              <td class="px-3 py-2">{{ formatMinutes(readNumber(row, ['workingMinutes'])) }}</td>
              <td class="px-3 py-2">{{ formatMinutes(readNumber(row, ['overtimeMinutes'])) }}</td>
              <td class="px-3 py-2">{{ readNumber(row, ['lateMinutes', 'late']) }}</td>
              <td class="px-3 py-2">{{ readText(row, ['attendanceMode']) }}</td>
              <td class="px-3 py-2">{{ readNumber(row, ['completedSessions']) }}/{{ readNumber(row, ['requiredSessionsForFullDay']) || 1 }}</td>
              <td class="px-3 py-2">
                <UBadge :color="readBoolean(row, ['needsReview']) ? 'warning' : 'neutral'" variant="subtle">
                  {{ readBoolean(row, ['needsReview']) ? 'Needs review' : 'Clear' }}
                </UBadge>
              </td>
            </tr>
            <tr v-if="!rows.length">
              <td class="px-3 py-6 text-center text-muted" colspan="11">No attendance rows returned.</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { readArray, readBoolean, readNumber, readText, toLocalDateInput, type ApiRecord, useHrApiClient } from '../utils/hr-api'

useHead({ title: 'Attendance Dashboard - Garmetix HR' })

const { get, post } = useHrApiClient()
const loading = ref(false)
const error = ref('')
const data = ref<ApiRecord | null>(null)
const employees = ref<ApiRecord[]>([])
const punchSaving = ref(false)
const message = ref('')
const messageTone = ref<'success' | 'error' | 'warning' | 'neutral'>('neutral')

const punchForm = reactive({
  employeeId: null as string | null,
  punchType: 'Auto',
  reason: '',
  source: 'Manual'
})

const punchTypeOptions = [
  { value: 'Auto', label: 'Auto' },
  { value: 'CheckIn', label: 'Check In' },
  { value: 'BreakOut', label: 'Break Out' },
  { value: 'BreakIn', label: 'Break In' },
  { value: 'CheckOut', label: 'Check Out' }
]
const sourceOptions = [
  { value: 'Manual', label: 'Manual' },
  { value: 'ManagerCorrection', label: 'Manager Correction' },
  { value: 'DeviceOffline', label: 'Device Offline' }
]

const rows = computed<ApiRecord[]>(() => readArray(data.value, ['rows', 'Rows']))
const loadedDate = computed(() => readText(data.value, ['onDate', 'OnDate'], toLocalDateInput()))
const cards = computed(() => [
  { label: 'Employees', value: readNumber(data.value, ['employeeCount', 'employees']) },
  { label: 'Present', value: readNumber(data.value, ['present']) },
  { label: 'Late', value: readNumber(data.value, ['late']) },
  { label: 'Needs Review', value: readNumber(data.value, ['needsReview', 'reviewCount']) }
])
const messageIcon = computed(() => messageTone.value === 'success' ? 'i-lucide-circle-check' : messageTone.value === 'warning' ? 'i-lucide-triangle-alert' : messageTone.value === 'error' ? 'i-lucide-circle-alert' : 'i-lucide-info')
const employeeOptions = computed(() => employees.value.map(employee => ({
  value: readText(employee, ['id'], ''),
  label: `${employeeName(employee)} | ${readText(employee, ['employeeCode', 'code'])}`.replace(/\s+\|\s+-$/, '')
})))
const canSavePunch = computed(() => Boolean(punchForm.employeeId && punchForm.punchType && !punchSaving.value))

function isActiveEmployee(employee: ApiRecord) {
  const status = readText(employee, ['employeeStatus', 'status'], '').toLowerCase()
  return readBoolean(employee, ['working']) && !['resigned', 'terminated', 'inactive'].includes(status)
}

function employeeName(employee: ApiRecord) {
  const fullName = readText(employee, ['staffName', 'fullName'], '')
  if (fullName) return fullName
  return `${readText(employee, ['firstName'], '')} ${readText(employee, ['lastName'], '')}`.trim() || 'Employee'
}

function formatMinutes(value: number) {
  if (!value) return '0m'
  const hours = Math.floor(value / 60)
  const minutes = value % 60
  return hours ? `${hours}h ${minutes}m` : `${minutes}m`
}

function formatDateTime(value: string) {
  if (!value || value === '-') return '-'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
}

function statusColor(row: ApiRecord) {
  const status = readText(row, ['status', 'attendanceStatus'], '').toLowerCase()
  if (status.includes('absent')) return 'error'
  if (status.includes('late') || status.includes('half')) return 'warning'
  if (status.includes('present')) return 'success'
  return 'neutral'
}

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const [attendanceData, employeeData] = await Promise.allSettled([
      get<ApiRecord>('api/attendance/today', { onDate: toLocalDateInput() }),
      get<ApiRecord[] | { rows?: ApiRecord[], items?: ApiRecord[], data?: ApiRecord[] }>('api/employees')
    ])
    if (attendanceData.status === 'fulfilled') data.value = attendanceData.value
    if (employeeData.status === 'fulfilled') {
      const response = employeeData.value
      const allEmployees = Array.isArray(response) ? response : (response.rows ?? response.items ?? response.data ?? [])
      employees.value = allEmployees.filter(isActiveEmployee)
      if (!punchForm.employeeId && employees.value.length) punchForm.employeeId = readText(employees.value[0], ['id'], '')
    }
    const failed = [attendanceData, employeeData].filter(item => item.status === 'rejected').length
    if (failed) error.value = `${failed} attendance dashboard request(s) could not be loaded.`
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load attendance dashboard.'
  } finally {
    loading.value = false
  }
}

async function savePunch() {
  if (!canSavePunch.value) {
    messageTone.value = 'warning'
    message.value = 'Select employee and punch type before saving.'
    return
  }

  punchSaving.value = true
  message.value = ''
  try {
    const employee = employees.value.find(item => readText(item, ['id'], '') === punchForm.employeeId)
    const now = new Date()
    const localPunchTime = `${toLocalDateInput(now)}T${String(now.getHours()).padStart(2, '0')}:${String(now.getMinutes()).padStart(2, '0')}`
    const response = await post<ApiRecord>('api/attendance/manual-punch', {
      employeeId: punchForm.employeeId,
      punchType: punchForm.punchType,
      localPunchTime,
      punchTimeUtc: new Date(localPunchTime).toISOString(),
      source: punchForm.source,
      reason: punchForm.reason || null,
      companyId: employee ? readText(employee, ['companyId'], '') : '',
      storeGroupId: employee ? readText(employee, ['storeGroupId'], '') : '',
      storeId: employee ? readText(employee, ['storeId'], '') : ''
    })
    const duplicate = readBoolean(response, ['duplicate'])
    messageTone.value = duplicate ? 'warning' : 'success'
    message.value = readText(response, ['message'], duplicate ? 'Duplicate punch ignored.' : 'Manual punch saved.')
    punchForm.reason = ''
    await refresh()
  } catch (caught) {
    messageTone.value = 'error'
    message.value = caught instanceof Error ? caught.message : 'Unable to save manual punch.'
  } finally {
    punchSaving.value = false
  }
}

onMounted(refresh)
</script>
