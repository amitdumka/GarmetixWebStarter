<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-dashboard-kicker">
            <UIcon name="i-lucide-clipboard-check" class="size-4" />
            {{ subtitle }}
          </p>
          <h2 class="garmetix-dashboard-title">{{ title }}</h2>
          <p class="garmetix-dashboard-subtitle">{{ description }}</p>
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

    <div class="garmetix-section-card">
      <AdminMasterTable :columns="columns" :rows="rows" empty-text="No support drill rows returned." />
    </div>
  </section>
</template>

<script setup lang="ts">
import { formatDateTime, readArray, readNumber, readText, type ApiRecord, useAdminApiClient } from '../utils/admin-api'

const props = defineProps<{
  title: string
  subtitle: string
  description: string
  summaryEndpoint: string
  listEndpoint: string
  listKey: string
}>()

const { get } = useAdminApiClient()
const loading = ref(true)
const error = ref('')
const summary = ref<ApiRecord | null>(null)
const list = ref<ApiRecord | null>(null)
const columns = [
  { key: 'group', label: 'Group' },
  { key: 'route', label: 'Route' },
  { key: 'severity', label: 'Severity' },
  { key: 'detail', label: 'Detail' }
]
const listItems = computed(() => readArray(list.value, [props.listKey]))
const cards = computed(() => [
  { label: 'Status', value: readText(summary.value, ['overallStatus', 'status'], 'Pending'), detail: readText(summary.value, ['warning']) },
  { label: 'Generated', value: formatDateTime(summary.value?.generatedAtUtc), detail: readText(summary.value, ['buildCode']) },
  { label: 'Count', value: readNumber(summary.value, ['drillCount', 'phaseCount']), detail: 'Summary groups' },
  { label: 'Rows', value: listItems.value.length, detail: 'Checklist rows' }
])
const rows = computed(() => listItems.value.map((item, index) => ({
  id: readText(item, ['id'], String(index)),
  group: readText(item, ['drill', 'phase', 'title'], `Row ${index + 1}`),
  route: readText(item, ['route']),
  severity: readText(item, ['severity', 'expectedEvidence']),
  detail: readText(item, ['step', 'action', 'message'])
})))

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const [summaryData, listData] = await Promise.allSettled([
      get<unknown>(props.summaryEndpoint),
      get<unknown>(props.listEndpoint)
    ])
    if (summaryData.status === 'fulfilled' && summaryData.value && typeof summaryData.value === 'object') summary.value = summaryData.value as ApiRecord
    if (listData.status === 'fulfilled' && listData.value && typeof listData.value === 'object') list.value = listData.value as ApiRecord
    const failed = [summaryData, listData].filter(item => item.status === 'rejected').length
    if (failed) error.value = `${failed} support drill request(s) could not be loaded.`
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : `Unable to load ${props.title}.`
  } finally {
    loading.value = false
  }
}

onMounted(refresh)
</script>
