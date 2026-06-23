<template>
  <section class="space-y-4">
    <div class="border border-default bg-muted/10 p-5">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
        <div>
          <p class="text-sm text-muted">Monthly attendance</p>
          <h2 class="mt-1 text-2xl font-semibold">Monthly Attendance</h2>
          <p class="mt-2 text-sm text-muted">Review monthly attendance totals before payroll generation.</p>
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
      <div v-for="card in cards" :key="card.label" class="border border-default bg-muted/20 p-4">
        <p class="text-xs text-muted">{{ card.label }}</p>
        <p class="mt-1 text-xl font-semibold">{{ card.value }}</p>
      </div>
    </div>

    <div class="border border-default bg-muted/10 p-4">
      <h3 class="text-base font-semibold">Monthly Payload</h3>
      <pre class="mt-3 max-h-[460px] overflow-auto border border-default bg-default/40 p-3 text-xs">{{ formattedData }}</pre>
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
