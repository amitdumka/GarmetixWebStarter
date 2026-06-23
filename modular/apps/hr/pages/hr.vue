<template>
  <section class="space-y-4">
    <div class="border border-default bg-muted/10 p-5">
      <div class="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <p class="text-sm text-muted">Employee master</p>
          <h2 class="mt-1 text-2xl font-semibold">Employees</h2>
          <p class="mt-2 text-sm text-muted">Read-only employee summary from the existing HR API.</p>
        </div>
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="load">Refresh</UButton>
      </div>
    </div>

    <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="error" />

    <div class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
      <div v-for="card in cards" :key="card.label" class="border border-default bg-muted/20 p-4">
        <p class="text-sm text-muted">{{ card.label }}</p>
        <p class="mt-2 text-2xl font-semibold">{{ card.value }}</p>
        <p class="mt-1 text-xs text-muted">{{ card.detail }}</p>
      </div>
    </div>

    <div class="border border-default bg-muted/10 p-4">
      <h3 class="text-base font-semibold">Summary Payload</h3>
      <pre class="mt-3 max-h-[420px] overflow-auto border border-default bg-default/40 p-3 text-xs">{{ formattedSummary }}</pre>
    </div>
  </section>
</template>

<script setup lang="ts">
import { readNumber, type ApiRecord, useHrApiClient } from '../utils/hr-api'

useHead({ title: 'Employees - Garmetix HR' })

const { get } = useHrApiClient()
const loading = ref(false)
const error = ref('')
const summary = ref<ApiRecord | null>(null)
const cards = computed(() => [
  { label: 'Total Employees', value: readNumber(summary.value, ['totalEmployees', 'employeeCount', 'employees']), detail: 'All employee master records' },
  { label: 'Active', value: readNumber(summary.value, ['activeEmployees', 'active']), detail: 'Active staff count' },
  { label: 'Inactive', value: readNumber(summary.value, ['inactiveEmployees', 'inactive']), detail: 'Inactive or exited employees' },
  { label: 'Salesmen', value: readNumber(summary.value, ['salesmanCount', 'salesmen']), detail: 'Salesman category entries' }
])
const formattedSummary = computed(() => summary.value ? JSON.stringify(summary.value, null, 2) : 'No employee summary loaded.')

async function load() {
  loading.value = true
  error.value = ''
  try {
    summary.value = await get<ApiRecord>('api/hr/employee-master/summary')
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load employee summary.'
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>
