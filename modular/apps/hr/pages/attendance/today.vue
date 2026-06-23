<template>
  <section class="space-y-4">
    <div class="border border-default bg-muted/10 p-5">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
        <div>
          <p class="text-sm text-muted">Daily attendance</p>
          <h2 class="mt-1 text-2xl font-semibold">Today Attendance</h2>
          <p class="mt-2 text-sm text-muted">Review present, late, absent, and exception rows for a selected date.</p>
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
      <div v-for="card in cards" :key="card.label" class="border border-default bg-muted/20 p-4">
        <p class="text-xs text-muted">{{ card.label }}</p>
        <p class="mt-1 text-xl font-semibold">{{ card.value }}</p>
      </div>
    </div>

    <div class="overflow-hidden border border-default bg-muted/10">
      <div class="border-b border-default p-4">
        <h3 class="text-base font-semibold">Rows</h3>
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
