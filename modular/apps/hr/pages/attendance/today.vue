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

    <div class="grid gap-3 md:grid-cols-5">
      <div v-for="card in cards" :key="card.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label">{{ card.label }}</p>
        <p class="garmetix-metric-value">{{ card.value }}</p>
      </div>
    </div>

    <div class="garmetix-table-panel">
      <div class="garmetix-panel-header">
        <div>
          <h3 class="garmetix-panel-title">Rows</h3>
          <p class="garmetix-panel-subtitle">{{ rows.length }} attendance row(s)</p>
        </div>
      </div>
      <div class="overflow-auto">
        <table class="w-full min-w-[760px] text-left text-sm">
          <thead class="bg-muted/30 text-xs uppercase text-muted">
            <tr>
              <th class="px-3 py-2">Employee</th>
              <th class="px-3 py-2">Status</th>
              <th class="px-3 py-2">In</th>
              <th class="px-3 py-2">Out</th>
              <th class="px-3 py-2">Late</th>
              <th class="px-3 py-2">Review</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(row, index) in rows" :key="readText(row, ['employeeId', 'id'], String(index))" class="border-t border-default">
              <td class="px-3 py-2 font-medium">{{ readText(row, ['employeeName', 'name', 'employee']) }}</td>
              <td class="px-3 py-2">{{ readText(row, ['status', 'attendanceStatus']) }}</td>
              <td class="px-3 py-2">{{ readText(row, ['inTime', 'firstIn', 'checkIn']) }}</td>
              <td class="px-3 py-2">{{ readText(row, ['outTime', 'lastOut', 'checkOut']) }}</td>
              <td class="px-3 py-2">{{ readText(row, ['lateMinutes', 'late']) }}</td>
              <td class="px-3 py-2">{{ readText(row, ['reviewStatus', 'needsReview']) }}</td>
            </tr>
            <tr v-if="!rows.length">
              <td class="px-3 py-6 text-center text-muted" colspan="6">No attendance rows returned.</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { readNumber, readText, toLocalDateInput, type ApiRecord, useHrApiClient } from '../../utils/hr-api'

useHead({ title: 'Today Attendance - Garmetix HR' })

const { get } = useHrApiClient()
const onDate = ref(toLocalDateInput())
const loading = ref(false)
const error = ref('')
const data = ref<ApiRecord | null>(null)
const rows = computed<ApiRecord[]>(() => Array.isArray(data.value?.rows) ? data.value.rows as ApiRecord[] : [])
const cards = computed(() => [
  { label: 'Employees', value: readNumber(data.value, ['employeeCount', 'employees']) },
  { label: 'Present', value: readNumber(data.value, ['present']) },
  { label: 'Late', value: readNumber(data.value, ['late']) },
  { label: 'Absent', value: readNumber(data.value, ['absent']) },
  { label: 'Review', value: readNumber(data.value, ['needsReview', 'reviewCount']) }
])

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
