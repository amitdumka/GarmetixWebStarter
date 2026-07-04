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

    <div class="garmetix-section-card">
      <div class="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <h3 class="garmetix-panel-title">Report Snapshot</h3>
          <p class="garmetix-panel-subtitle">Payroll attendance evidence for salary draft review and export handoff.</p>
        </div>
        <UBadge color="primary" variant="subtle">CSV export ready</UBadge>
      </div>
      <pre class="mt-3 max-h-[560px] overflow-auto rounded-lg border border-default bg-default/40 p-3 text-xs">{{ formattedSummary }}</pre>
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
const summary = ref<ApiRecord | null>(null)
const cards = computed(() => [
  { label: 'Employees', value: readNumber(summary.value, ['employees']) },
  { label: 'Present Days', value: readNumber(summary.value, ['presentDays']) },
  { label: 'Absent Days', value: readNumber(summary.value, ['absentDays']) },
  { label: 'Late Days', value: readNumber(summary.value, ['lateDays']) },
  { label: 'Half Days', value: readNumber(summary.value, ['halfDays']) },
  { label: 'Locked Rows', value: readText(summary.value, ['hasLockedRows'], 'false') === 'true' ? 'Yes' : 'No' }
])
const formattedSummary = computed(() => summary.value ? JSON.stringify(summary.value, null, 2) : 'No payroll summary loaded.')

async function load() {
  loading.value = true
  error.value = ''
  try {
    summary.value = await get<ApiRecord>('api/attendance/payroll-summary', { year: year.value, month: month.value })
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load payroll summary.'
  } finally {
    loading.value = false
  }
}

function exportSummary() {
  if (!summary.value) return
  downloadCsvFile(`garmetix-payroll-summary-${year.value}${String(month.value).padStart(2, '0')}.csv`, [
    {
      salaryMonth: `${year.value}-${String(month.value).padStart(2, '0')}`,
      employees: readNumber(summary.value, ['employees']),
      presentDays: readNumber(summary.value, ['presentDays']),
      absentDays: readNumber(summary.value, ['absentDays']),
      lateDays: readNumber(summary.value, ['lateDays']),
      halfDays: readNumber(summary.value, ['halfDays']),
      lockedRows: readText(summary.value, ['hasLockedRows'], 'false')
    }
  ], [
    { key: 'salaryMonth', label: 'Salary Month' },
    { key: 'employees', label: 'Employees' },
    { key: 'presentDays', label: 'Present Days' },
    { key: 'absentDays', label: 'Absent Days' },
    { key: 'lateDays', label: 'Late Days' },
    { key: 'halfDays', label: 'Half Days' },
    { key: 'lockedRows', label: 'Locked Rows' }
  ])
}

onMounted(load)
</script>
