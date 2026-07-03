<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-dashboard-kicker">
            <UIcon name="i-lucide-bug" class="size-4" />
            Runtime
          </p>
          <h2 class="garmetix-dashboard-title">Runtime Diagnostics</h2>
          <p class="garmetix-dashboard-subtitle">Database probes, environment warnings and page/API contracts for deployment triage.</p>
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
        <h3 class="garmetix-panel-title mb-3">Probes</h3>
        <AdminMasterTable :columns="probeColumns" :rows="probeRows" empty-text="No runtime probes returned." />
      </div>
      <div class="garmetix-section-card">
        <h3 class="garmetix-panel-title mb-3">Page Contracts</h3>
        <AdminMasterTable :columns="pageColumns" :rows="pageRows" empty-text="No page contracts returned." />
      </div>
    </section>
  </section>
</template>

<script setup lang="ts">
import { formatDateTime, readArray, readNumber, readText, type ApiRecord, useAdminApiClient } from '../utils/admin-api'

useHead({ title: 'Runtime Diagnostics - Garmetix Admin' })

const { get } = useAdminApiClient()
const loading = ref(true)
const error = ref('')
const summary = ref<ApiRecord | null>(null)
const contracts = ref<ApiRecord | null>(null)
const probeColumns = [
  { key: 'area', label: 'Area' },
  { key: 'title', label: 'Check' },
  { key: 'status', label: 'Status' },
  { key: 'detail', label: 'Detail' }
]
const pageColumns = [
  { key: 'area', label: 'Area' },
  { key: 'page', label: 'Page' },
  { key: 'api', label: 'API' }
]
const probes = computed(() => readArray(summary.value, ['probes']))
const pages = computed(() => readArray(contracts.value, ['pages']))
const cards = computed(() => [
  { label: 'Status', value: readText(summary.value, ['status'], 'Pending'), detail: readText(summary.value, ['environment'], '-') },
  { label: 'Failed Probes', value: readNumber(summary.value, ['failedProbeCount']), detail: 'Database/runtime issues' },
  { label: 'Warnings', value: readNumber(summary.value, ['warningCount']), detail: 'Configuration warnings' },
  { label: 'Generated', value: formatDateTime(summary.value?.generatedAtUtc), detail: readText(summary.value, ['buildCode']) }
])
const probeRows = computed(() => probes.value.map(item => ({
  area: readText(item, ['area']),
  title: readText(item, ['title']),
  status: readText(item, ['status']),
  detail: readText(item, ['detail'])
})))
const pageRows = computed(() => pages.value.map(item => ({
  area: readText(item, ['area']),
  page: readText(item, ['pagePath']),
  api: readText(item, ['apiPath'])
})))

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const [summaryData, contractData] = await Promise.allSettled([
      get<unknown>('runtime-diagnostics'),
      get<unknown>('runtime-diagnostics/page-contracts')
    ])
    if (summaryData.status === 'fulfilled' && summaryData.value && typeof summaryData.value === 'object') summary.value = summaryData.value as ApiRecord
    if (contractData.status === 'fulfilled' && contractData.value && typeof contractData.value === 'object') contracts.value = contractData.value as ApiRecord
    const failed = [summaryData, contractData].filter(item => item.status === 'rejected').length
    if (failed) error.value = `${failed} runtime diagnostics request(s) could not be loaded.`
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load runtime diagnostics.'
  } finally {
    loading.value = false
  }
}

onMounted(refresh)
</script>
