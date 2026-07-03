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

    <div class="grid gap-3 md:grid-cols-3 xl:grid-cols-6">
      <div v-for="card in cards" :key="card.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label">{{ card.label }}</p>
        <p class="garmetix-metric-value text-xl">{{ card.value }}</p>
      </div>
    </div>

    <div class="garmetix-section-card">
      <h3 class="garmetix-panel-title">Monthly Payload</h3>
      <pre class="mt-3 max-h-[460px] overflow-auto rounded-lg border border-default bg-default/40 p-3 text-xs">{{ formattedData }}</pre>
    </div>
  </section>
</template>

<script setup lang="ts">
import { currentYearMonth, readNumber, type ApiRecord, useHrApiClient } from '../../utils/hr-api'

useHead({ title: 'Monthly Attendance - Garmetix HR' })

const { get } = useHrApiClient()
const current = currentYearMonth()
const year = ref(current.year)
const month = ref(current.month)
const loading = ref(false)
const error = ref('')
const data = ref<ApiRecord | null>(null)
const cards = computed(() => [
  { label: 'Employees', value: readNumber(data.value, ['employeeCount', 'employees']) },
  { label: 'Present Days', value: readNumber(data.value, ['presentDays']) },
  { label: 'Late Days', value: readNumber(data.value, ['lateDays']) },
  { label: 'Half Days', value: readNumber(data.value, ['halfDays']) },
  { label: 'Absent Days', value: readNumber(data.value, ['absentDays']) },
  { label: 'Locked', value: readNumber(data.value, ['locked']) ? 'Yes' : 'No' }
])
const formattedData = computed(() => data.value ? JSON.stringify(data.value, null, 2) : 'No monthly attendance loaded.')

async function load() {
  loading.value = true
  error.value = ''
  try {
    data.value = await get<ApiRecord>('api/attendance/monthly', { year: year.value, month: month.value })
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load monthly attendance.'
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>
