<template>
  <section class="space-y-4">
    <div class="border border-default bg-muted/10 p-5">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="text-sm text-muted">System diagnostics</p>
          <h2 class="mt-1 text-2xl font-semibold">Message Logs</h2>
          <p class="mt-2 max-w-3xl text-sm text-muted">Admin-wide view of errors, warnings, success events and frontend/client logs. Detailed diagnostics stay here instead of leaking into user save messages.</p>
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

    <section class="grid gap-4 xl:grid-cols-[minmax(0,1.5fr)_minmax(360px,0.8fr)]">
      <div class="border border-default bg-muted/10 p-4">
        <div class="mb-3 flex flex-col gap-2 xl:flex-row xl:items-center xl:justify-between">
          <div>
            <h3 class="text-base font-semibold">Log Register</h3>
            <p class="text-xs text-muted">{{ filteredRows.length }} log(s)</p>
          </div>
          <div class="flex flex-col gap-2 sm:flex-row">
            <USelect v-model="levelFilter" :items="levelItems" class="sm:w-40" />
            <UInput v-model="search" icon="i-lucide-search" placeholder="Search logs" class="sm:w-72" />
          </div>
        </div>
        <AdminMasterTable :columns="columns" :rows="filteredRows" empty-text="No message logs found." />
      </div>

      <aside class="border border-default bg-muted/10 p-4">
        <div class="flex items-start justify-between gap-3">
          <div>
            <h3 class="text-base font-semibold">Selected Log</h3>
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
        <pre v-if="selectedDetailsJson" class="mt-4 max-h-72 overflow-auto whitespace-pre-wrap border border-default bg-muted/20 p-3 text-xs text-muted">{{ selectedDetailsJson }}</pre>
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
const selectedLogId = ref('')
const logs = ref<ApiRecord[]>([])
const levelItems = computed(() => [
  { label: 'All Levels', value: 'all' },
  ...Array.from(new Set(logs.value.map(item => readText(item, ['level'])).filter(item => item !== '-'))).sort().map(item => ({ label: item, value: item }))
])
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
  user: readText(item, ['userName'], 'System')
})))
const filteredRows = computed(() => {
  const term = search.value.trim().toLowerCase()
  return rowItems.value.filter(row => {
    const levelMatches = levelFilter.value === 'all' || row.level === levelFilter.value
    const textMatches = !term || JSON.stringify(row).toLowerCase().includes(term)
    return levelMatches && textMatches
  })
})
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
    logs.value = toRows(await get<unknown>('message-logs', { take: 200 }))
    if (!selectedLogId.value && logs.value.length > 0) selectedLogId.value = readText(logs.value[0], ['id'], '')
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load message logs.'
  } finally {
    loading.value = false
  }
}

onMounted(refresh)
</script>
