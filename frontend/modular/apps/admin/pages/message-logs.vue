<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-dashboard-kicker">
            <UIcon name="i-lucide-message-square-warning" class="size-4" />
            System diagnostics
          </p>
          <h2 class="garmetix-dashboard-title">Message Logs</h2>
          <p class="garmetix-dashboard-subtitle">Admin-wide view of errors, warnings, success events and frontend/client logs. Detailed diagnostics stay here instead of leaking into user save messages.</p>
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

    <section class="grid gap-4 xl:grid-cols-[minmax(0,1.5fr)_minmax(360px,0.8fr)]">
      <div class="garmetix-section-card">
        <div class="mb-3 flex flex-col gap-2 xl:flex-row xl:items-center xl:justify-between">
          <div>
            <h3 class="garmetix-panel-title">Log Register</h3>
            <p class="text-xs text-muted">{{ filteredRows.length }} log(s){{ filteredRows.length !== logs.length ? ` (of ${logs.length} loaded)` : '' }}</p>
          </div>
          <div class="flex flex-col gap-2 sm:flex-row sm:flex-wrap">
            <USelect v-model="levelFilter" :items="levelItems" class="sm:w-40" />
            <USelect v-model="sourceFilter" :items="sourceItems" class="sm:w-40" />
            <USelect v-model="successFilter" :items="successItems" class="sm:w-40" />
            <UInput v-model="search" icon="i-lucide-search" placeholder="Search logs" class="sm:w-64" />
            <UButton v-if="hasActiveFilters" size="sm" color="neutral" variant="ghost" icon="i-lucide-x" @click="clearFilters">Clear</UButton>
          </div>
        </div>
        <AdminMasterTable :columns="columns" :rows="pagedRows" empty-text="No message logs found." />
        <div v-if="filteredRows.length" class="mt-3 flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
          <div class="flex items-center gap-2 text-xs text-muted">
            <span>Rows per page</span>
            <USelect v-model="pageSize" :items="pageSizeItems" class="w-24" />
            <span>Showing {{ pageStartLabel }}-{{ pageEndLabel }} of {{ filteredRows.length }}</span>
          </div>
          <div class="flex items-center gap-2">
            <UButton size="sm" color="neutral" variant="soft" icon="i-lucide-chevron-left" :disabled="page <= 1" @click="page--">Prev</UButton>
            <span class="text-xs text-muted">Page {{ page }} of {{ totalPages }}</span>
            <UButton size="sm" color="neutral" variant="soft" icon="i-lucide-chevron-right" trailing :disabled="page >= totalPages" @click="page++">Next</UButton>
          </div>
        </div>
      </div>

      <aside class="garmetix-detail-panel">
        <div class="flex items-start justify-between gap-3">
          <div>
            <h3 class="garmetix-panel-title">Selected Log</h3>
            <p class="text-xs text-muted">{{ selectedLabel }}</p>
          </div>
          <UBadge :color="selectedLog?.success === false ? 'warning' : selectedLog ? 'success' : 'neutral'" variant="subtle">{{ selectedLog ? readText(selectedLog, ['level']) : 'None' }}</UBadge>
        </div>
        <USelect v-model="selectedLogId" :items="logSelectItems" class="mt-4" />
        <dl v-if="selectedLog" class="mt-4 grid gap-3 text-sm">
          <div v-for="item in selectedDetails" :key="item.label" class="border-b border-default pb-2">
            <dt class="text-xs text-muted">{{ item.label }}</dt>
            <dd class="mt-1 break-words font-medium">{{ item.value }}</dd>
          </div>
        </dl>
        <pre v-if="selectedDetailsJson" class="mt-4 max-h-72 overflow-auto whitespace-pre-wrap rounded-md bg-muted/30 p-3 text-xs text-muted">{{ selectedDetailsJson }}</pre>
      </aside>
    </section>
  </section>
</template>

<script setup lang="ts">
import { formatDateTime, readText, toRows, type ApiRecord, useAdminApiClient } from '../utils/admin-api'

useHead({ title: 'Message Logs - Garmetix Admin' })

const { get } = useAdminApiClient()
const loading = ref(true)
const error = ref('')
const search = ref('')
const levelFilter = ref('all')
const sourceFilter = ref('all')
const successFilter = ref('all')
const selectedLogId = ref('')
const logs = ref<ApiRecord[]>([])
const page = ref(1)
const pageSize = ref(25)
const pageSizeItems = [
  { label: '25', value: 25 },
  { label: '50', value: 50 },
  { label: '100', value: 100 },
  { label: '200', value: 200 }
]

const levelItems = computed(() => [
  { label: 'All Levels', value: 'all' },
  ...Array.from(new Set(logs.value.map(item => readText(item, ['level'])).filter(item => item !== '-'))).sort().map(item => ({ label: item, value: item }))
])
const sourceItems = computed(() => [
  { label: 'All Sources', value: 'all' },
  ...Array.from(new Set(logs.value.map(item => readText(item, ['source'])).filter(item => item !== '-'))).sort().map(item => ({ label: item, value: item }))
])
const successItems = [
  { label: 'Success + Failure', value: 'all' },
  { label: 'Success Only', value: 'success' },
  { label: 'Failure Only', value: 'failure' }
]

const cards = computed(() => [
  { label: 'Logs', value: logs.value.length, detail: 'Latest returned rows' },
  { label: 'Errors', value: logs.value.filter(item => readText(item, ['level']).toLowerCase().includes('error')).length, detail: 'Failed operations' },
  { label: 'Warnings', value: logs.value.filter(item => readText(item, ['level']).toLowerCase().includes('warning')).length, detail: 'Needs attention' },
  { label: 'Failures', value: logs.value.filter(item => item.success === false).length, detail: 'Operation failures' }
])
const columns = [
  { key: 'when', label: 'When' },
  { key: 'level', label: 'Level' },
  { key: 'source', label: 'Source' },
  { key: 'event', label: 'Event' },
  { key: 'message', label: 'Message' },
  { key: 'user', label: 'User' }
]
const rowItems = computed(() => logs.value.map(item => ({
  id: readText(item, ['id']),
  when: formatDateTime(item.createdAtUtc),
  level: readText(item, ['level']),
  source: readText(item, ['source']),
  event: readText(item, ['eventName']),
  message: readText(item, ['message']),
  user: readText(item, ['userName'], 'System'),
  successRaw: item.success
})))
const hasActiveFilters = computed(() => levelFilter.value !== 'all' || sourceFilter.value !== 'all' || successFilter.value !== 'all' || search.value.trim() !== '')
const filteredRows = computed(() => {
  const term = search.value.trim().toLowerCase()
  return rowItems.value.filter(row => {
    const levelMatches = levelFilter.value === 'all' || row.level === levelFilter.value
    const sourceMatches = sourceFilter.value === 'all' || row.source === sourceFilter.value
    const successMatches = successFilter.value === 'all'
      || (successFilter.value === 'success' && row.successRaw !== false)
      || (successFilter.value === 'failure' && row.successRaw === false)
    const textMatches = !term || JSON.stringify(row).toLowerCase().includes(term)
    return levelMatches && sourceMatches && successMatches && textMatches
  })
})
const totalPages = computed(() => Math.max(1, Math.ceil(filteredRows.value.length / pageSize.value)))
const pagedRows = computed(() => {
  const start = (page.value - 1) * pageSize.value
  return filteredRows.value.slice(start, start + pageSize.value)
})
const pageStartLabel = computed(() => filteredRows.value.length === 0 ? 0 : (page.value - 1) * pageSize.value + 1)
const pageEndLabel = computed(() => Math.min(page.value * pageSize.value, filteredRows.value.length))

watch([levelFilter, sourceFilter, successFilter, search, pageSize], () => { page.value = 1 })
watch(totalPages, value => { if (page.value > value) page.value = value })

function clearFilters() {
  levelFilter.value = 'all'
  sourceFilter.value = 'all'
  successFilter.value = 'all'
  search.value = ''
}

const logSelectItems = computed(() => logs.value.slice(0, 100).map(item => ({
  label: `${readText(item, ['level'])} - ${readText(item, ['eventName'])}`,
  value: readText(item, ['id'], '')
})))
const selectedLog = computed(() => logs.value.find(item => readText(item, ['id'], '') === selectedLogId.value) ?? null)
const selectedLabel = computed(() => selectedLog.value ? readText(selectedLog.value, ['eventName', 'message']) : 'No log selected')
const selectedDetailsJson = computed(() => prettyJson(readText(selectedLog.value, ['detailsJson'], '')))
const selectedDetails = computed(() => {
  const item = selectedLog.value
  if (!item) return []
  return [
    { label: 'Created At', value: formatDateTime(item.createdAtUtc) },
    { label: 'Source', value: readText(item, ['source']) },
    { label: 'Event', value: readText(item, ['eventName']) },
    { label: 'Message', value: readText(item, ['message']) },
    { label: 'Resource', value: readText(item, ['resource']) },
    { label: 'Operation Id', value: readText(item, ['operationId']) },
    { label: 'User', value: readText(item, ['userName'], 'System') }
  ]
})

function prettyJson(value: string) {
  if (!value || value === '-') return ''
  try {
    return JSON.stringify(JSON.parse(value), null, 2)
  } catch {
    return value
  }
}

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    logs.value = toRows(await get<unknown>('message-logs', { take: 500 }))
    if (!selectedLogId.value && logs.value.length > 0) selectedLogId.value = readText(logs.value[0], ['id'], '')
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load message logs.'
  } finally {
    loading.value = false
  }
}

onMounted(refresh)
</script>
