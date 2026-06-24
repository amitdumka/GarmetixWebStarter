<template>
  <section class="space-y-4">
    <div class="border border-default bg-muted/10 p-5">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="text-sm text-muted">Data diagnostics</p>
          <h2 class="mt-1 text-2xl font-semibold">Data Consistency</h2>
          <p class="mt-2 max-w-3xl text-sm text-muted">Read-only consistency checks for inventory, documents, GST, payments, accounting and cleanup risks. Repair actions stay outside this foundation page.</p>
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

    <section class="grid gap-4 xl:grid-cols-2">
      <div class="border border-default bg-muted/10 p-4">
        <h3 class="mb-3 text-base font-semibold">Issue Sections</h3>
        <AdminMasterTable :columns="sectionColumns" :rows="sectionRows" empty-text="No consistency sections returned." />
      </div>
      <div class="border border-default bg-muted/10 p-4">
        <h3 class="mb-3 text-base font-semibold">Top Issues</h3>
        <AdminMasterTable :columns="issueColumns" :rows="issueRows" empty-text="No consistency issues found." />
      </div>
    </section>
  </section>
</template>

<script setup lang="ts">
import { formatDateTime, readArray, readNumber, readText, type ApiRecord, useAdminApiClient } from '../utils/admin-api'

useHead({ title: 'Data Consistency - Garmetix Admin' })

const { get } = useAdminApiClient()
const loading = ref(true)
const error = ref('')
const summary = ref<ApiRecord | null>(null)
const run = ref<ApiRecord | null>(null)
const sectionColumns = [
  { key: 'area', label: 'Area' },
  { key: 'total', label: 'Total' },
  { key: 'critical', label: 'Critical' },
  { key: 'warning', label: 'Warning' },
  { key: 'info', label: 'Info' }
]
const issueColumns = [
  { key: 'severity', label: 'Severity' },
  { key: 'area', label: 'Area' },
  { key: 'code', label: 'Code' },
  { key: 'reference', label: 'Reference' },
  { key: 'description', label: 'Description' }
]
const sections = computed(() => readArray(summary.value, ['sections']))
const issues = computed(() => readArray(run.value, ['issues']))
const cards = computed(() => [
  { label: 'Issues', value: readNumber(summary.value, ['issueCount', 'totalIssues']), detail: `Generated ${formatDateTime(summary.value?.generatedAt)}` },
  { label: 'Critical', value: readNumber(summary.value, ['criticalCount']), detail: 'Must review before go-live' },
  { label: 'Warnings', value: readNumber(summary.value, ['warningCount']), detail: 'Operational risk' },
  { label: 'Info', value: readNumber(summary.value, ['infoCount']), detail: 'Cleanup guidance' }
])
const sectionRows = computed(() => sections.value.map(item => ({
  area: readText(item, ['area']),
  total: readNumber(item, ['issueCount', 'total']),
  critical: readNumber(item, ['criticalCount']),
  warning: readNumber(item, ['warningCount']),
  info: readNumber(item, ['infoCount'])
})))
const issueRows = computed(() => issues.value.slice(0, 50).map(item => ({
  severity: readText(item, ['severity']),
  area: readText(item, ['area']),
  code: readText(item, ['checkCode']),
  reference: readText(item, ['referenceNumber', 'entityId']),
  description: readText(item, ['description'])
})))

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const [summaryData, issueData] = await Promise.allSettled([
      get<unknown>('data-consistency/summary'),
      get<unknown>('data-consistency/issues')
    ])
    if (summaryData.status === 'fulfilled' && summaryData.value && typeof summaryData.value === 'object') summary.value = summaryData.value as ApiRecord
    if (issueData.status === 'fulfilled' && issueData.value && typeof issueData.value === 'object') run.value = issueData.value as ApiRecord
    const failed = [summaryData, issueData].filter(item => item.status === 'rejected').length
    if (failed) error.value = `${failed} data consistency request(s) could not be loaded.`
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load data consistency.'
  } finally {
    loading.value = false
  }
}

onMounted(refresh)
</script>
