<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
        <div>
          <p class="garmetix-dashboard-kicker">
            <UIcon name="i-lucide-calendar-range" class="size-4" />
            Monthly attendance
          </p>
          <h2 class="garmetix-dashboard-title">Monthly Attendance</h2>
          <p class="garmetix-dashboard-subtitle">Review monthly attendance totals before payroll generation.</p>
        </div>
        <form class="flex flex-wrap items-end gap-2" @submit.prevent="load">
          <UFormField label="Year" name="year">
            <UInput v-model.number="year" type="number" min="2020" max="2100" />
          </UFormField>
          <UFormField label="Month" name="month">
            <UInput v-model.number="month" type="number" min="1" max="12" />
          </UFormField>
          <UButton type="submit" icon="i-lucide-refresh-cw" :loading="loading">Load</UButton>
        </form>
      </div>
    </div>

    <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="error" />

    <div class="grid gap-3 md:grid-cols-4 xl:grid-cols-8">
      <div v-for="card in cards" :key="card.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label">{{ card.label }}</p>
        <p class="garmetix-metric-value text-xl">{{ card.value }}</p>
      </div>
    </div>

    <div class="grid gap-3 lg:grid-cols-[1.2fr_.8fr]">
      <div class="garmetix-section-card">
        <div class="garmetix-panel-header">
          <div>
            <h3 class="garmetix-panel-title">Generation Readiness</h3>
            <p class="garmetix-panel-subtitle">{{ generationStatus }}</p>
          </div>
          <UBadge :color="locked ? 'warning' : 'success'" variant="subtle">{{ locked ? 'Locked' : 'Open' }}</UBadge>
        </div>
        <div class="mt-3 grid gap-2 text-sm text-muted md:grid-cols-3">
          <div class="rounded-lg border border-default bg-default/40 p-3">
            <p class="font-medium text-highlighted">Recalculate endpoint</p>
            <p class="mt-1 font-mono text-xs">{{ recalculateEndpoint }}</p>
          </div>
          <div class="rounded-lg border border-default bg-default/40 p-3">
            <p class="font-medium text-highlighted">Lock endpoint</p>
            <p class="mt-1 font-mono text-xs">{{ lockEndpoint }}</p>
          </div>
          <div class="rounded-lg border border-default bg-default/40 p-3">
            <p class="font-medium text-highlighted">Delete endpoint</p>
            <p class="mt-1 font-mono text-xs">{{ deleteEndpoint }}</p>
          </div>
        </div>
      </div>

      <div class="garmetix-section-card">
        <h3 class="garmetix-panel-title">Preview Request</h3>
        <pre class="mt-3 max-h-[220px] overflow-auto rounded-lg border border-default bg-default/40 p-3 text-xs">{{ formattedPreviewRequest }}</pre>
      </div>
    </div>

    <div class="garmetix-section-card">
      <div class="garmetix-panel-header">
        <div>
          <h3 class="garmetix-panel-title">Guarded Live Actions</h3>
          <p class="garmetix-panel-subtitle">Type <span class="font-mono">{{ confirmationPhrase }}</span> before changing attendance data.</p>
        </div>
        <UBadge color="warning" variant="subtle">Live write</UBadge>
      </div>
      <UAlert
        class="mt-3"
        color="warning"
        variant="subtle"
        icon="i-lucide-shield-alert"
        title="Attendance changes affect payroll"
        description="Recalculate writes monthly summaries, lock/unlock changes payroll readiness, and delete selected rows removes attendance correction data inside the shared backend."
      />
      <UAlert v-if="actionMessage" class="mt-3" :color="actionTone" variant="subtle" :icon="actionIcon" :description="actionMessage" />
      <div class="mt-4 grid gap-3 xl:grid-cols-[1fr_1.2fr]">
        <div class="space-y-3">
          <UFormField label="Confirmation phrase" name="liveConfirmText">
            <UInput v-model="liveConfirmText" :placeholder="confirmationPhrase" />
          </UFormField>
          <UFormField label="Delete reason" name="deleteReason">
            <UInput v-model="deleteReason" placeholder="Reason required for selected-row delete" />
          </UFormField>
          <div class="grid gap-2 text-sm text-muted md:grid-cols-2">
            <UCheckbox v-model="deleteDailyAttendance" label="Delete daily attendance rows" />
            <UCheckbox v-model="deletePunches" label="Delete punches and photo proofs" />
          </div>
        </div>
        <div class="grid gap-2 md:grid-cols-3">
          <UButton color="primary" variant="soft" icon="i-lucide-refresh-cw" :loading="recalculating" :disabled="!canRunLiveAction" @click="recalculateMonth">
            Recalculate
          </UButton>
          <UButton color="warning" variant="soft" icon="i-lucide-lock-keyhole" :loading="locking" :disabled="!canRunLiveAction" @click="setMonthLock(!locked)">
            {{ locked ? 'Unlock Month' : 'Lock Month' }}
          </UButton>
          <UButton color="error" variant="soft" icon="i-lucide-trash-2" :loading="deletingRows" :disabled="!canDeleteSelected" @click="deleteSelectedRows">
            Delete Selected
          </UButton>
          <div class="rounded-lg border border-default bg-default/40 p-3 text-sm md:col-span-3">
            <p class="font-medium text-highlighted">{{ selectedItems.length }} selected row(s)</p>
            <p class="mt-1 text-muted">Delete uses selected employee/date pairs and is blocked by the backend when the month is locked.</p>
          </div>
        </div>
      </div>
    </div>

    <div class="garmetix-table-panel">
      <div class="garmetix-panel-header">
        <div>
          <h3 class="garmetix-panel-title">Month Days</h3>
          <p class="garmetix-panel-subtitle">{{ days.length }} generated day row(s) for {{ monthLabel }}</p>
        </div>
      </div>
      <div class="overflow-auto">
        <table class="w-full min-w-[1180px] text-left text-sm">
          <thead class="bg-muted/30 text-xs uppercase text-muted">
            <tr>
              <th class="px-3 py-2">
                <input
                  class="size-4"
                  type="checkbox"
                  :checked="allRowsSelected"
                  :disabled="!days.length"
                  aria-label="Select all monthly attendance rows"
                  @change="toggleAllRows"
                />
              </th>
              <th class="px-3 py-2">Date</th>
              <th class="px-3 py-2">Employee</th>
              <th class="px-3 py-2">Status</th>
              <th class="px-3 py-2">Shift</th>
              <th class="px-3 py-2">In</th>
              <th class="px-3 py-2">Out</th>
              <th class="px-3 py-2">Work</th>
              <th class="px-3 py-2">OT</th>
              <th class="px-3 py-2">Late</th>
              <th class="px-3 py-2">Mode</th>
              <th class="px-3 py-2">Review</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(row, index) in days" :key="`${readText(row, ['employeeId', 'id'], String(index))}-${readText(row, ['onDate'])}`" class="border-t border-default">
              <td class="px-3 py-2">
                <input
                  class="size-4"
                  type="checkbox"
                  :checked="selectedRowKeys.has(rowKey(row, index))"
                  :aria-label="`Select ${readText(row, ['employeeName', 'name', 'employee'])}`"
                  @change="toggleRow(row, index)"
                />
              </td>
              <td class="px-3 py-2">{{ formatDate(readText(row, ['onDate'])) }}</td>
              <td class="px-3 py-2 font-medium">{{ readText(row, ['employeeName', 'name', 'employee']) }}</td>
              <td class="px-3 py-2">
                <UBadge :color="statusColor(row)" variant="subtle">{{ readText(row, ['status', 'attendanceStatus']) }}</UBadge>
              </td>
              <td class="px-3 py-2">{{ readText(row, ['shiftName']) }}</td>
              <td class="px-3 py-2">{{ formatDateTime(readText(row, ['checkIn', 'inTime', 'firstIn'])) }}</td>
              <td class="px-3 py-2">{{ formatDateTime(readText(row, ['checkOut', 'outTime', 'lastOut'])) }}</td>
              <td class="px-3 py-2">{{ formatMinutes(readNumber(row, ['workingMinutes'])) }}</td>
              <td class="px-3 py-2">{{ formatMinutes(readNumber(row, ['overtimeMinutes'])) }}</td>
              <td class="px-3 py-2">{{ readNumber(row, ['lateMinutes', 'late']) }}</td>
              <td class="px-3 py-2">{{ readText(row, ['attendanceMode']) }}</td>
              <td class="px-3 py-2">
                <UBadge :color="readBoolean(row, ['needsReview']) ? 'warning' : 'neutral'" variant="subtle">
                  {{ readBoolean(row, ['needsReview']) ? 'Needs review' : 'Clear' }}
                </UBadge>
              </td>
            </tr>
            <tr v-if="!days.length">
              <td class="px-3 py-6 text-center text-muted" colspan="12">No monthly attendance rows returned.</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { currentYearMonth, readArray, readBoolean, readNumber, readText, type ApiRecord, useHrApiClient } from '../../utils/hr-api'

useHead({ title: 'Monthly Attendance - Garmetix HR' })

const { get, post } = useHrApiClient()
const current = currentYearMonth()
const year = ref(current.year)
const month = ref(current.month)
const loading = ref(false)
const error = ref('')
const data = ref<ApiRecord | null>(null)
const recalculating = ref(false)
const locking = ref(false)
const deletingRows = ref(false)
const actionMessage = ref('')
const actionTone = ref<'success' | 'error' | 'warning' | 'neutral'>('neutral')
const liveConfirmText = ref('')
const deleteReason = ref('')
const deleteDailyAttendance = ref(true)
const deletePunches = ref(false)
const selectedRowKeys = ref(new Set<string>())
const days = computed<ApiRecord[]>(() => readArray(data.value, ['days', 'Days']))
const locked = computed(() => readBoolean(data.value, ['locked']))
const monthLabel = computed(() => `${String(month.value).padStart(2, '0')}/${year.value}`)
const confirmationPhrase = computed(() => `CONFIRM ${monthLabel.value}`)
const recalculateEndpoint = 'api/attendance/recalculate'
const lockEndpoint = 'api/attendance/lock-month'
const deleteEndpoint = 'api/attendance/monthly/delete-selected'
const recalculatePayload = computed(() => ({
  year: year.value,
  month: month.value,
  employeeId: null,
  companyId: null,
  storeGroupId: null,
  storeId: null
}))
const lockPayload = computed(() => ({
  year: year.value,
  month: month.value,
  companyId: null,
  storeGroupId: null,
  storeId: null,
  locked: !locked.value
}))
const selectedItems = computed(() => days.value
  .map((row, index) => ({ row, index, key: rowKey(row, index) }))
  .filter(item => selectedRowKeys.value.has(item.key))
  .map(item => ({
    employeeId: readText(item.row, ['employeeId'], ''),
    onDate: readText(item.row, ['onDate'], '')
  }))
  .filter(item => item.employeeId && item.onDate))
const allRowsSelected = computed(() => days.value.length > 0 && selectedItems.value.length === days.value.length)
const canRunLiveAction = computed(() => liveConfirmText.value.trim() === confirmationPhrase.value)
const canDeleteSelected = computed(() => canRunLiveAction.value && selectedItems.value.length > 0 && Boolean(deleteReason.value.trim()) && (deleteDailyAttendance.value || deletePunches.value))
const actionIcon = computed(() => actionTone.value === 'success' ? 'i-lucide-circle-check' : actionTone.value === 'warning' ? 'i-lucide-triangle-alert' : actionTone.value === 'error' ? 'i-lucide-circle-alert' : 'i-lucide-info')
const cards = computed(() => [
  { label: 'Employees', value: readNumber(data.value, ['employeeCount', 'employees']) },
  { label: 'Present Days', value: readNumber(data.value, ['presentDays']) },
  { label: 'Late Days', value: readNumber(data.value, ['lateDays']) },
  { label: 'Half Days', value: readNumber(data.value, ['halfDays']) },
  { label: 'Absent Days', value: readNumber(data.value, ['absentDays']) },
  { label: 'Overtime', value: formatMinutes(readNumber(data.value, ['overtimeMinutes'])) },
  { label: 'Locked', value: locked.value ? 'Yes' : 'No' },
  { label: 'Day Rows', value: days.value.length }
])
const generationStatus = computed(() => {
  if (!data.value) return 'Load a month to verify generated attendance rows.'
  if (locked.value) return 'Month is locked; backend recalculation will skip locked summaries.'
  if (!days.value.length) return 'No generated day rows returned yet for this month.'
  return 'Monthly rows are available for payroll review. Live actions require the confirmation phrase.'
})
const formattedPreviewRequest = computed(() => JSON.stringify(recalculatePayload.value, null, 2))

function rowKey(row: ApiRecord, index: number) {
  return `${readText(row, ['employeeId', 'id'], String(index))}|${readText(row, ['onDate'], String(index))}`
}

function toggleRow(row: ApiRecord, index: number) {
  const next = new Set(selectedRowKeys.value)
  const key = rowKey(row, index)
  if (next.has(key)) next.delete(key)
  else next.add(key)
  selectedRowKeys.value = next
}

function toggleAllRows() {
  if (allRowsSelected.value) {
    selectedRowKeys.value = new Set()
    return
  }

  selectedRowKeys.value = new Set(days.value.map((row, index) => rowKey(row, index)))
}

function clearLiveGate() {
  liveConfirmText.value = ''
  selectedRowKeys.value = new Set()
}

function formatMinutes(value: number) {
  if (!value) return '0m'
  const hours = Math.floor(value / 60)
  const minutes = value % 60
  return hours ? `${hours}h ${minutes}m` : `${minutes}m`
}

function formatDate(value: string) {
  if (!value || value === '-') return '-'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  return date.toLocaleDateString()
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

async function load() {
  loading.value = true
  error.value = ''
  try {
    data.value = await get<ApiRecord>('api/attendance/monthly', { year: year.value, month: month.value })
    selectedRowKeys.value = new Set()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load monthly attendance.'
  } finally {
    loading.value = false
  }
}

async function recalculateMonth() {
  if (!canRunLiveAction.value) return
  recalculating.value = true
  actionMessage.value = ''
  try {
    const response = await post<ApiRecord>(recalculateEndpoint, recalculatePayload.value)
    actionTone.value = 'success'
    actionMessage.value = `Monthly attendance recalculated. Saved: ${readNumber(response, ['saved', 'Saved'])}, employees: ${readNumber(response, ['employees', 'Employees'])}.`
    clearLiveGate()
    await load()
  } catch (caught) {
    actionTone.value = 'error'
    actionMessage.value = caught instanceof Error ? caught.message : 'Unable to recalculate monthly attendance.'
  } finally {
    recalculating.value = false
  }
}

async function setMonthLock(nextLocked: boolean) {
  if (!canRunLiveAction.value) return
  locking.value = true
  actionMessage.value = ''
  try {
    const response = await post<ApiRecord>(lockEndpoint, { ...lockPayload.value, locked: nextLocked })
    actionTone.value = 'success'
    actionMessage.value = `Month ${nextLocked ? 'locked' : 'unlocked'}. Rows changed: ${readNumber(response, ['count', 'Count'])}.`
    clearLiveGate()
    await load()
  } catch (caught) {
    actionTone.value = 'error'
    actionMessage.value = caught instanceof Error ? caught.message : `Unable to ${nextLocked ? 'lock' : 'unlock'} month.`
  } finally {
    locking.value = false
  }
}

async function deleteSelectedRows() {
  if (!canDeleteSelected.value) return
  deletingRows.value = true
  actionMessage.value = ''
  try {
    const response = await post<ApiRecord>(deleteEndpoint, {
      items: selectedItems.value,
      deletePunches: deletePunches.value,
      deleteDailyAttendance: deleteDailyAttendance.value,
      reason: deleteReason.value.trim()
    })
    actionTone.value = 'success'
    actionMessage.value = readText(response, ['message'], `Deleted ${selectedItems.value.length} selected row(s).`)
    deleteReason.value = ''
    clearLiveGate()
    await load()
  } catch (caught) {
    actionTone.value = 'error'
    actionMessage.value = caught instanceof Error ? caught.message : 'Unable to delete selected attendance rows.'
  } finally {
    deletingRows.value = false
  }
}

onMounted(load)
</script>
