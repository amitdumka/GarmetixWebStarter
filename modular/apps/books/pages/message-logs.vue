<template>
  <section class="space-y-4">
    <div class="border border-default bg-muted/10 p-5">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="text-sm text-muted">Accounting diagnostics</p>
          <h2 class="mt-1 text-2xl font-semibold">Books Message Logs</h2>
          <p class="mt-2 max-w-3xl text-sm text-muted">
            Finance-scoped error, warning, success and event logs for voucher, banking, GST, petty cash, purchase and settlement workflows.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
          <UBadge color="primary" variant="subtle">Read only</UBadge>
        </div>
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
            <p class="text-xs text-muted">{{ filteredRows.length }} log(s) shown</p>
          </div>
          <div class="flex flex-col gap-2 sm:flex-row">
            <USelect v-model="levelFilter" :items="levelItems" class="sm:w-40" />
            <UInput v-model="search" icon="i-lucide-search" placeholder="Search logs" class="sm:w-72" />
          </div>
        </div>

        <div class="overflow-hidden border border-default">
          <div class="overflow-x-auto">
            <table class="w-full min-w-[980px] text-left text-sm">
              <thead class="bg-muted/30 text-xs uppercase text-muted">
                <tr>
                  <th class="whitespace-nowrap px-3 py-2 font-medium">When</th>
                  <th class="whitespace-nowrap px-3 py-2 font-medium">Level</th>
                  <th class="whitespace-nowrap px-3 py-2 font-medium">Source</th>
                  <th class="whitespace-nowrap px-3 py-2 font-medium">Event</th>
                  <th class="whitespace-nowrap px-3 py-2 font-medium">Message</th>
                  <th class="whitespace-nowrap px-3 py-2 font-medium">User</th>
                  <th class="whitespace-nowrap px-3 py-2 font-medium">Action</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-default">
                <tr v-if="filteredRows.length === 0">
                  <td colspan="7" class="px-3 py-8 text-center text-muted">No accounting message logs found.</td>
                </tr>
                <tr v-for="row in filteredRows" :key="readText(row, ['id'])" class="bg-default/40">
                  <td class="whitespace-nowrap px-3 py-2">{{ formatDateTime(row.createdAtUtc) }}</td>
                  <td class="whitespace-nowrap px-3 py-2">{{ readText(row, ['level']) }}</td>
                  <td class="max-w-44 truncate px-3 py-2">{{ readText(row, ['source']) }}</td>
                  <td class="max-w-44 truncate px-3 py-2">{{ readText(row, ['eventName']) }}</td>
                  <td class="max-w-80 truncate px-3 py-2">{{ readText(row, ['message']) }}</td>
                  <td class="max-w-40 truncate px-3 py-2">{{ readText(row, ['userName'], 'System') }}</td>
                  <td class="px-3 py-2">
                    <UButton icon="i-lucide-eye" size="xs" color="neutral" variant="soft" @click="selectedLogId = readText(row, ['id'], '')">View</UButton>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>

      <aside class="border border-default bg-muted/10 p-4">
        <div class="flex items-start justify-between gap-3">
          <div>
            <h3 class="text-base font-semibold">Log Detail</h3>
            <p class="text-xs text-muted">{{ selectedLogLabel }}</p>
          </div>
          <UBadge :color="selectedLog?.success === false ? 'warning' : selectedLog ? 'success' : 'neutral'" variant="subtle">{{ selectedLog ? readText(selectedLog, ['level']) : 'None' }}</UBadge>
        </div>

        <dl v-if="selectedLog" class="mt-4 grid gap-3 text-sm">
          <div v-for="item in selectedDetails" :key="item.label" class="border-b border-default pb-2">
            <dt class="text-xs text-muted">{{ item.label }}</dt>
            <dd class="mt-1 break-words font-medium">{{ item.value }}</dd>
          </div>
        </dl>
        <div v-if="selectedDetailsJson" class="mt-4 border border-default bg-muted/20 p-3">
          <h4 class="text-sm font-semibold">Details JSON</h4>
          <pre class="mt-2 max-h-72 overflow-auto whitespace-pre-wrap text-xs text-muted">{{ selectedDetailsJson }}</pre>
        </div>
        <p v-if="!selectedLog" class="mt-8 text-center text-sm text-muted">Select a log row to view details.</p>
      </aside>
    </section>
  </section>
</template>

<script setup lang="ts">
import { readText, toRows, type ApiRecord, useBooksApiClient } from '../utils/books-api'

useHead({ title: 'Message Logs - Garmetix Books' })

const { get } = useBooksApiClient()
const loading = ref(true)
const error = ref('')
const search = ref('')
const levelFilter = ref('all')
const selectedLogId = ref('')
const rows = ref<ApiRecord[]>([])
const levelItems = computed(() => [
  { label: 'All Levels', value: 'all' },
  ...Array.from(new Set(rows.value.map(row => readText(row, ['level'])).filter(value => value !== '-'))).sort().map(value => ({ label: value, value }))
])
const filteredRows = computed(() => {
  const term = search.value.trim().toLowerCase()
  return rows.value.filter(row => {
    const levelMatches = levelFilter.value === 'all' || readText(row, ['level']) === levelFilter.value
    const textMatches = !term || [
      readText(row, ['level']),
      readText(row, ['source']),
      readText(row, ['eventName']),
      readText(row, ['message']),
      readText(row, ['userName']),
      readText(row, ['resource']),
      readText(row, ['operationId'])
    ].join(' ').toLowerCase().includes(term)
    return levelMatches && textMatches
  })
})
const selectedLog = computed(() => rows.value.find(row => readText(row, ['id'], '') === selectedLogId.value) ?? null)
const selectedLogLabel = computed(() => selectedLog.value ? readText(selectedLog.value, ['eventName', 'message']) : 'No log selected')
const selectedDetailsJson = computed(() => prettyJson(readText(selectedLog.value, ['detailsJson'], '')))
const cards = computed(() => [
  { label: 'Logs', value: rows.value.length, detail: 'Accounting scope' },
  { label: 'Errors', value: rows.value.filter(item => readText(item, ['level']).toLowerCase().includes('error')).length, detail: 'Needs review' },
  { label: 'Warnings', value: rows.value.filter(item => readText(item, ['level']).toLowerCase().includes('warning')).length, detail: 'Potential issues' },
  { label: 'Failures', value: rows.value.filter(item => item.success === false).length, detail: 'Operation failures' }
])
const selectedDetails = computed(() => {
  const item = selectedLog.value
  if (!item) return []
  return [
    { label: 'Created At', value: formatDateTime(item.createdAtUtc) },
    { label: 'Level', value: readText(item, ['level']) },
    { label: 'Source', value: readText(item, ['source']) },
    { label: 'Event', value: readText(item, ['eventName']) },
    { label: 'Message', value: readText(item, ['message']) },
    { label: 'Resource', value: readText(item, ['resource']) },
    { label: 'User', value: readText(item, ['userName'], 'System') },
    { label: 'Operation Id', value: readText(item, ['operationId']) },
    { label: 'Success', value: item.success === false ? 'No' : 'Yes' }
  ]
})

function formatDateTime(value: unknown) {
  if (!value) return '-'
  const date = new Date(String(value))
  if (Number.isNaN(date.getTime())) return String(value)
  return new Intl.DateTimeFormat('en-IN', { dateStyle: 'medium', timeStyle: 'short' }).format(date)
}

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
    rows.value = toRows(await get<unknown>('accounting/message-logs', { take: 200 }))
    if (!selectedLogId.value && rows.value.length > 0) selectedLogId.value = readText(rows.value[0], ['id'], '')
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load accounting message logs.'
  } finally {
    loading.value = false
  }
}

onMounted(refresh)
</script>
