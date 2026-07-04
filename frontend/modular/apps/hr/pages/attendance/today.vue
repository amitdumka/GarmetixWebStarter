<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-calendar-check-2" class="size-4" /> Daily attendance</p>
          <h2 class="garmetix-dashboard-title">Today Attendance</h2>
          <p class="garmetix-dashboard-subtitle">Review present, late, absent, and exception rows for a selected date.</p>
        </div>
        <form class="flex flex-wrap items-end gap-2" @submit.prevent="load">
          <UFormField label="Date" name="onDate">
            <UInput v-model="onDate" type="date" />
          </UFormField>
          <UButton type="submit" icon="i-lucide-refresh-cw" :loading="loading">Load</UButton>
        </form>
      </div>
    </div>

    <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="error" />

    <div class="grid gap-3 md:grid-cols-4 xl:grid-cols-8">
      <div v-for="card in cards" :key="card.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label">{{ card.label }}</p>
        <p class="garmetix-metric-value">{{ card.value }}</p>
      </div>
    </div>

    <div class="garmetix-table-panel">
      <div class="garmetix-panel-header">
        <div>
          <h3 class="garmetix-panel-title">Rows</h3>
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
import { readArray, readBoolean, readNumber, readText, toLocalDateInput, type ApiRecord, useHrApiClient } from '../../utils/hr-api'

useHead({ title: 'Today Attendance - Garmetix HR' })

const { get } = useHrApiClient()
const onDate = ref(toLocalDateInput())
const loading = ref(false)
const error = ref('')
const data = ref<ApiRecord | null>(null)
const rows = computed<ApiRecord[]>(() => readArray(data.value, ['rows', 'Rows']))
const loadedDate = computed(() => readText(data.value, ['onDate', 'OnDate'], onDate.value))
const cards = computed(() => [
  { label: 'Employees', value: readNumber(data.value, ['employeeCount', 'employees']) },
  { label: 'Present', value: readNumber(data.value, ['present']) },
  { label: 'Late', value: readNumber(data.value, ['late']) },
  { label: 'Half Day', value: readNumber(data.value, ['halfDay', 'halfDays']) },
  { label: 'Absent', value: readNumber(data.value, ['absent']) },
  { label: 'Review', value: readNumber(data.value, ['needsReview', 'reviewCount']) },
  { label: 'Work', value: formatMinutes(totalWorkingMinutes.value) },
  { label: 'OT', value: formatMinutes(totalOvertimeMinutes.value) }
])
const totalWorkingMinutes = computed(() => rows.value.reduce((total, row) => total + readNumber(row, ['workingMinutes']), 0))
const totalOvertimeMinutes = computed(() => rows.value.reduce((total, row) => total + readNumber(row, ['overtimeMinutes']), 0))

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

async function load() {
  loading.value = true
  error.value = ''
  try {
    data.value = await get<ApiRecord>('api/attendance/today', { onDate: onDate.value })
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load daily attendance.'
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>
