<template>
  <section class="space-y-4">
    <div class="border border-default bg-muted/10 p-5">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="text-sm text-muted">{{ subtitle }}</p>
          <h2 class="mt-1 text-2xl font-semibold">{{ title }}</h2>
          <p class="mt-2 max-w-3xl text-sm text-muted">{{ description }}</p>
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
