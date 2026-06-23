<template>
  <section class="space-y-4">
    <div class="border border-default bg-muted/10 p-5">
      <div class="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <p class="text-sm text-muted">Payroll</p>
          <h2 class="mt-1 text-2xl font-semibold">Payslips</h2>
          <p class="mt-2 max-w-3xl text-sm text-muted">
            Recent payslips are read from the payroll API. Month generation and PDF actions will be enabled after the HR module smoke test.
          </p>
        </div>
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="load">Refresh</UButton>
      </div>
    </div>

    <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="error" />

    <div class="grid gap-3 md:grid-cols-4">
      <div v-for="card in cards" :key="card.label" class="border border-default bg-muted/20 p-4">
        <p class="text-xs text-muted">{{ card.label }}</p>
        <p class="mt-1 text-xl font-semibold">{{ card.value }}</p>
      </div>
    </div>

    <div class="overflow-hidden border border-default bg-muted/10">
      <div class="border-b border-default p-4">
        <h3 class="text-base font-semibold">Recent Payslips</h3>
      </div>
      <div class="overflow-auto">
        <table class="w-full min-w-[820px] text-left text-sm">
          <thead class="bg-muted/30 text-xs uppercase text-muted">
            <tr>
              <th class="px-3 py-2">Employee</th>
              <th class="px-3 py-2">Month</th>
              <th class="px-3 py-2">Earnings</th>
              <th class="px-3 py-2">Deductions</th>
              <th class="px-3 py-2">Net</th>
              <th class="px-3 py-2">Status</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(slip, index) in slips" :key="readText(slip, ['id'], String(index))" class="border-t border-default">
              <td class="px-3 py-2 font-medium">{{ readText(slip, ['employeeName', 'employee']) }}</td>
              <td class="px-3 py-2">{{ readText(slip, ['monthYear', 'month']) }}</td>
              <td class="px-3 py-2">{{ formatIndianMoney(readNumber(slip, ['totalEarnings'])) }}</td>
              <td class="px-3 py-2">{{ formatIndianMoney(readNumber(slip, ['totalDeductions'])) }}</td>
              <td class="px-3 py-2 font-semibold">{{ formatIndianMoney(readNumber(slip, ['netSalary', 'payableAmount'])) }}</td>
              <td class="px-3 py-2">{{ readText(slip, ['status']) }}</td>
            </tr>
            <tr v-if="!slips.length">
              <td class="px-3 py-6 text-center text-muted" colspan="6">No recent payslips returned.</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney } from '@garmetix/shared-utils'
import { readNumber, readText, type ApiRecord, useHrApiClient } from '../utils/hr-api'

useHead({ title: 'Payroll - Garmetix HR' })

const { get } = useHrApiClient()
const loading = ref(false)
const error = ref('')
const slips = ref<ApiRecord[]>([])
const cards = computed(() => {
  const net = slips.value.reduce((total, item) => total + readNumber(item, ['netSalary', 'payableAmount']), 0)
  const deductions = slips.value.reduce((total, item) => total + readNumber(item, ['totalDeductions']), 0)
  return [
    { label: 'Payslips', value: slips.value.length },
    { label: 'Net Salary', value: formatIndianMoney(net) },
    { label: 'Deductions', value: formatIndianMoney(deductions) },
    { label: 'Ready', value: slips.value.filter(item => readText(item, ['status']).toLowerCase().includes('ready')).length }
  ]
})

async function load() {
  loading.value = true
  error.value = ''
  try {
    const response = await get<ApiRecord[]>('api/payroll/payslips/recent', { take: 25 })
    slips.value = Array.isArray(response) ? response : []
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load recent payslips.'
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>
