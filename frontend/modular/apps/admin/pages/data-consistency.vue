<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-dashboard-kicker">
            <UIcon name="i-lucide-database-zap" class="size-4" />
            Data diagnostics
          </p>
          <h2 class="garmetix-dashboard-title">Data Consistency</h2>
          <p class="garmetix-dashboard-subtitle">Read-only consistency checks for inventory, documents, GST, payments, accounting and cleanup risks. Repair actions stay outside this foundation page.</p>
        </div>
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
      </div>
    </div>

    <UAlert v-if="error" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />

    <section class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
      <div v-for="card in cards" :key="card.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label">{{ card.label }}</p>
        <p class="garmetix-metric-value">{{ card.value }}</p>
        <p class="garmetix-metric-caption">{{ card.detail }}</p>
      </div>
    </section>

    <section class="grid gap-4 xl:grid-cols-2">
      <div class="garmetix-section-card">
        <h3 class="garmetix-panel-title mb-3">Issue Sections</h3>
        <AdminMasterTable :columns="sectionColumns" :rows="sectionRows" empty-text="No consistency sections returned." />
      </div>
      <div class="garmetix-section-card">
        <h3 class="garmetix-panel-title mb-3">Top Issues</h3>
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
