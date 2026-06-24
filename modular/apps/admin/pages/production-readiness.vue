<template>
  <section class="space-y-4">
    <div class="border border-default bg-muted/10 p-5">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="text-sm text-muted">Deployment</p>
          <h2 class="mt-1 text-2xl font-semibold">Production Readiness</h2>
          <p class="mt-2 max-w-3xl text-sm text-muted">Read-only production gate covering environment, secrets, CORS, email, backup and reverse proxy readiness.</p>
        </div>
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
      </div>
    </div>

    <UAlert v-if="error" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />

    <section class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
      <div v-for="card in cards" :key="card.label" class="border border-default bg-muted/20 p-4">
        <p class="text-sm text-muted">{{ card.label }}</p>
        <p class="mt-2 text-2xl font-semibold">{{ card.value }}</p>
        <p class="mt-1 text-xs text-muted">{{ card.detail }}</p>
      </div>
    </section>

    <div class="border border-default bg-muted/10 p-4">
      <AdminMasterTable :columns="columns" :rows="rows" empty-text="No production readiness checks returned." />
    </div>
  </section>
</template>

<script setup lang="ts">
import { readArray, readNumber, readText, type ApiRecord, useAdminApiClient } from '../utils/admin-api'

useHead({ title: 'Production Readiness - Garmetix Admin' })

const { get } = useAdminApiClient()
const loading = ref(true)
const error = ref('')
const summary = ref<ApiRecord | null>(null)
const columns = [
  { key: 'code', label: 'Code' },
  { key: 'title', label: 'Check' },
  { key: 'status', label: 'Status' },
  { key: 'severity', label: 'Severity' },
  { key: 'message', label: 'Message' }
]
const checks = computed(() => readArray(summary.value, ['checks']))
const cards = computed(() => [
  { label: 'Status', value: readText(summary.value, ['status'], 'Pending'), detail: readText(summary.value, ['environment']) },
  { label: 'Passed', value: readNumber(summary.value, ['passed']), detail: 'Production checks' },
  { label: 'Warnings', value: readNumber(summary.value, ['warnings']), detail: 'Review before go-live' },
  { label: 'Critical', value: readNumber(summary.value, ['critical']), detail: 'Must fix' }
])
const rows = computed(() => checks.value.map(item => ({
  code: readText(item, ['code']),
  title: readText(item, ['title']),
  status: readText(item, ['status']),
  severity: readText(item, ['severity']),
  message: readText(item, ['message'])
})))

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const data = await get<unknown>('production-readiness/summary')
    if (data && typeof data === 'object') summary.value = data as ApiRecord
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load production readiness.'
  } finally {
    loading.value = false
  }
}

onMounted(refresh)
</script>
