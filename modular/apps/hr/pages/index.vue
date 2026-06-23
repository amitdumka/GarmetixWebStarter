<template>
  <section class="space-y-4">
    <div class="border border-default bg-muted/10 p-5">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="text-sm text-muted">HR command center</p>
          <h2 class="mt-1 text-2xl font-semibold">Employees, Attendance And Payroll</h2>
          <p class="mt-2 max-w-3xl text-sm text-muted">
            This modular HR app reads from the existing Garmetix API and keeps payroll writes disabled in the first slice.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton to="/attendance/today" icon="i-lucide-calendar-check-2">Today</UButton>
          <UButton to="/payroll" color="neutral" variant="soft" icon="i-lucide-receipt-indian-rupee">Payroll</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="error" />

    <section class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
      <div v-for="card in cards" :key="card.label" class="border border-default bg-muted/20 p-4">
        <p class="text-sm text-muted">{{ card.label }}</p>
        <p class="mt-2 text-2xl font-semibold">{{ card.value }}</p>
        <p class="mt-1 text-xs text-muted">{{ card.detail }}</p>
      </div>
    </section>

    <section class="grid gap-4 xl:grid-cols-2">
      <div class="border border-default bg-muted/10 p-4">
        <div class="mb-3 flex items-center justify-between gap-3">
          <h3 class="text-base font-semibold">Today Attendance</h3>
          <UBadge :color="loading ? 'warning' : 'primary'" variant="subtle">{{ loading ? 'Loading' : todayDate }}</UBadge>
        </div>
        <div class="grid gap-2 sm:grid-cols-4">
          <div v-for="item in todayStats" :key="item.label" class="border border-default bg-default/40 p-3">
            <p class="text-xs text-muted">{{ item.label }}</p>
            <p class="mt-1 text-lg font-semibold">{{ item.value }}</p>
          </div>
        </div>
      </div>

      <div class="border border-default bg-muted/10 p-4">
        <div class="mb-3 flex items-center justify-between gap-3">
          <h3 class="text-base font-semibold">Recent Payslips</h3>
          <UButton to="/payroll" size="sm" color="neutral" variant="ghost" icon="i-lucide-arrow-right">Open</UButton>
        </div>
        <div v-if="recentPayslips.length" class="space-y-2">
          <div v-for="slip in recentPayslips.slice(0, 5)" :key="readText(slip, ['id'])" class="flex items-center justify-between gap-3 border border-default bg-default/40 p-3">
            <div class="min-w-0">
              <p class="truncate text-sm font-medium">{{ readText(slip, ['employeeName', 'employee']) }}</p>
              <p class="text-xs text-muted">{{ readText(slip, ['monthYear', 'month']) }}</p>
            </div>
            <p class="shrink-0 text-sm font-semibold">{{ formatIndianMoney(readNumber(slip, ['netSalary', 'payableAmount'])) }}</p>
          </div>
        </div>
        <p v-else class="text-sm text-muted">No recent payslips returned by the API.</p>
      </div>
    </section>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney } from '@garmetix/shared-utils'
import { currentYearMonth, readNumber, readText, toLocalDateInput, type ApiRecord, useHrApiClient } from '../utils/hr-api'

useHead({ title: 'HR Home - Garmetix HR' })

const { get } = useHrApiClient()
const loading = ref(true)
const error = ref('')
const todayDate = toLocalDateInput()
const month = currentYearMonth()
const employeeSummary = ref<ApiRecord | null>(null)
const today = ref<ApiRecord | null>(null)
const payrollSummary = ref<ApiRecord | null>(null)
const recentPayslips = ref<ApiRecord[]>([])

const cards = computed(() => [
  {
    label: 'Employees',
    value: readNumber(employeeSummary.value, ['totalEmployees', 'employeeCount', 'employees']),
    detail: 'Employee master summary'
  },
  {
    label: 'Present Today',
    value: readNumber(today.value, ['present', 'presentCount']),
    detail: 'Attendance marked today'
  },
  {
    label: 'Needs Review',
    value: readNumber(today.value, ['needsReview', 'reviewCount']),
    detail: 'Attendance entries needing review'
  },
  {
    label: 'Payroll Employees',
    value: readNumber(payrollSummary.value, ['employeeCount', 'employees']),
    detail: `${month.month}/${month.year} payroll summary`
  }
])
const todayStats = computed(() => [
  { label: 'Employees', value: readNumber(today.value, ['employeeCount', 'employees']) },
  { label: 'Present', value: readNumber(today.value, ['present']) },
  { label: 'Late', value: readNumber(today.value, ['late']) },
  { label: 'Absent', value: readNumber(today.value, ['absent']) }
])

onMounted(async () => {
  loading.value = true
  error.value = ''
  try {
    const [employeeData, todayData, payrollData, payslipData] = await Promise.all([
      get<ApiRecord>('api/hr/employee-master/summary'),
      get<ApiRecord>('api/attendance/today', { onDate: todayDate }),
      get<ApiRecord>('api/attendance/payroll-summary', { year: month.year, month: month.month }),
      get<ApiRecord[]>('api/payroll/payslips/recent', { take: 10 })
    ])
    employeeSummary.value = employeeData
    today.value = todayData
    payrollSummary.value = payrollData
    recentPayslips.value = Array.isArray(payslipData) ? payslipData : []
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load HR dashboard.'
  } finally {
    loading.value = false
  }
})
</script>
