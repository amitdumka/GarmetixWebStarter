<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const feedback = useUiFeedback()

const isAuthenticated = auth.isAuthenticated
const canSeeAdmin = auth.canSeeAdmin
const loading = ref(false)
const logs = ref<any[]>([])
const options = ref<any | null>(null)
const localLogs = feedback.logs

const filters = reactive({
  level: '',
  source: '',
  success: '',
  search: '',
  fromUtc: '',
  toUtc: '',
  take: 100
})

const levelOptions = computed(() => [
  { label: 'All levels', value: '' },
  ...(options.value?.levels || ['Info', 'Success', 'Warning', 'Error']).map((item: string) => ({ label: item, value: item }))
])

const sourceOptions = computed(() => [
  { label: 'All sources', value: '' },
  ...(options.value?.sources || []).map((item: string) => ({ label: item, value: item }))
])

const successOptions = [
  { label: 'All results', value: '' },
  { label: 'Success only', value: 'true' },
  { label: 'Failed only', value: 'false' }
]

const totalErrorCount = computed(() => logs.value.filter((item) => !item.success || item.level === 'Error').length)
const filteredLocalLogs = computed(() => {
  const search = filters.search.trim().toLowerCase()
  const level = filters.level.toLowerCase()
  return localLogs.value.filter((item: any) => {
    if (level && String(item.color || '').toLowerCase() !== level.toLowerCase()) return false
    if (search) {
      const text = `${item.title || ''} ${item.message || ''} ${item.details || ''} ${item.resource || ''}`.toLowerCase()
      if (!text.includes(search)) return false
    }
    return true
  })
})

function toQuery() {
  const params = new URLSearchParams()
  if (filters.level) params.set('level', filters.level)
  if (filters.source) params.set('source', filters.source)
  if (filters.success) params.set('success', filters.success)
  if (filters.search.trim()) params.set('search', filters.search.trim())
  if (filters.fromUtc) params.set('fromUtc', new Date(filters.fromUtc).toISOString())
  if (filters.toUtc) params.set('toUtc', new Date(filters.toUtc).toISOString())
  params.set('take', String(filters.take || 100))
  return params.toString()
}

async function refresh() {
  if (!auth.isAuthenticated.value || !auth.canSeeAdmin.value) return
  loading.value = true
  try {
    const query = toQuery()
    const [opts, rows] = await Promise.all([
      api.get<any>('message-logs/options'),
      api.get<any[]>(`message-logs${query ? `?${query}` : ''}`)
    ])
    options.value = opts
    logs.value = rows || []
  } catch (error) {
    feedback.failed('Message logs load failed', error)
  } finally {
    loading.value = false
  }
}

function resetFilters() {
  filters.level = ''
  filters.source = ''
  filters.success = ''
  filters.search = ''
  filters.fromUtc = ''
  filters.toUtc = ''
  filters.take = 100
  refresh()
}

function formatDate(value: string) {
  return value ? new Date(value).toLocaleString('en-IN') : '-'
}

function badgeColor(entry: any) {
  if (!entry.success || entry.level === 'Error') return 'error'
  if (entry.level === 'Warning') return 'warning'
  if (entry.level === 'Success') return 'success'
  return 'info'
}

async function copyDetails(entry: any) {
  const text = [
    `${entry.level || entry.color || 'Info'} · ${entry.source || entry.title || 'Message'}`,
    entry.eventName || '',
    entry.message || '',
    entry.resource ? `Resource: ${entry.resource}` : '',
    entry.operationId ? `Operation: ${entry.operationId}` : '',
    entry.detailsJson || entry.details || ''
  ].filter(Boolean).join('\n\n')

  if (import.meta.client && navigator.clipboard) {
    await navigator.clipboard.writeText(text)
    feedback.notify('Message details copied', undefined, 'neutral')
  }
}

onMounted(refresh)
</script>

<template>
  <AppShell title="Message Logs" @refresh="refresh">
    <div v-if="!isAuthenticated" class="rounded-3xl border border-dashed border-slate-300 p-8 text-center text-sm text-slate-500 dark:border-slate-700 dark:text-slate-400">
      Login as admin to view message logs.
    </div>

    <div v-else-if="!canSeeAdmin" class="rounded-3xl border border-dashed border-amber-300 bg-amber-50 p-8 text-center text-sm text-amber-700 dark:border-amber-700 dark:bg-amber-950/40 dark:text-amber-200">
      Message logs are available only for admin users.
    </div>

    <div v-else class="space-y-6">
      <section class="rounded-3xl border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <div class="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
          <div class="space-y-3">
            <div class="flex items-center gap-3">
              <UIcon name="i-lucide-list-collapse" class="h-8 w-8 text-primary" />
              <div>
                <p class="text-xs font-semibold uppercase tracking-[0.3em] text-slate-500">Stage 6B</p>
                <h1 class="text-2xl font-bold text-slate-950 dark:text-white">Message logs</h1>
              </div>
            </div>
            <p class="max-w-3xl text-sm text-slate-500 dark:text-slate-400">
              Review saved backend operation messages from onboarding, AF/SS seeding and other admin actions. Use filters to find failures, source modules, or a specific operation id.
            </p>
          </div>
          <div class="flex gap-2">
            <UBadge color="success" variant="soft">{{ logs.length }} loaded</UBadge>
            <UBadge v-if="totalErrorCount" color="error" variant="soft">{{ totalErrorCount }} failed</UBadge>
          </div>
        </div>
      </section>

      <UCard>
        <template #header>
          <div class="flex items-center justify-between gap-3">
            <div class="flex items-center gap-2">
              <UIcon name="i-lucide-filter" class="h-5 w-5" />
              <h2 class="font-semibold">Filters</h2>
            </div>
            <div class="flex gap-2">
              <UButton color="neutral" variant="outline" icon="i-lucide-rotate-ccw" @click="resetFilters">Reset</UButton>
              <UButton icon="i-lucide-search" :loading="loading" @click="refresh">Apply</UButton>
            </div>
          </div>
        </template>

        <div class="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
          <UFormField label="Level"><USelect v-model="filters.level" :items="levelOptions" /></UFormField>
          <UFormField label="Source"><USelect v-model="filters.source" :items="sourceOptions" /></UFormField>
          <UFormField label="Result"><USelect v-model="filters.success" :items="successOptions" /></UFormField>
          <UFormField label="Search"><UInput v-model="filters.search" placeholder="message, source, details" @keyup.enter="refresh" /></UFormField>
          <UFormField label="From"><UInput v-model="filters.fromUtc" type="datetime-local" /></UFormField>
          <UFormField label="To"><UInput v-model="filters.toUtc" type="datetime-local" /></UFormField>
          <UFormField label="Limit"><UInput v-model.number="filters.take" type="number" min="1" max="500" /></UFormField>
        </div>
      </UCard>

      <UCard>
        <template #header>
          <div class="flex items-center gap-2">
            <UIcon name="i-lucide-server-cog" class="h-5 w-5" />
            <h2 class="font-semibold">Saved backend logs</h2>
          </div>
        </template>

        <div v-if="logs.length" class="space-y-3">
          <article v-for="entry in logs" :key="entry.id" class="rounded-2xl border border-slate-200 p-4 text-sm dark:border-slate-800">
            <div class="flex flex-col gap-2 lg:flex-row lg:items-start lg:justify-between">
              <div class="space-y-1">
                <div class="flex flex-wrap items-center gap-2">
                  <UBadge :color="badgeColor(entry)" variant="subtle">{{ entry.level }}</UBadge>
                  <UBadge color="neutral" variant="outline">{{ entry.source }}</UBadge>
                  <span class="font-semibold text-slate-950 dark:text-white">{{ entry.eventName }}</span>
                </div>
                <p class="text-slate-600 dark:text-slate-300">{{ entry.message }}</p>
                <p class="text-xs text-slate-500">Operation: {{ entry.operationId }}</p>
              </div>
              <div class="flex items-center gap-2 text-xs text-slate-500">
                <span>{{ formatDate(entry.createdAtUtc) }}</span>
                <UButton v-if="entry.detailsJson" color="neutral" variant="ghost" size="xs" icon="i-lucide-copy" label="Copy" @click="copyDetails(entry)" />
              </div>
            </div>
            <details v-if="entry.detailsJson" class="mt-3 rounded-xl bg-slate-50 p-3 dark:bg-slate-950/50">
              <summary class="cursor-pointer font-medium">Details</summary>
              <pre class="mt-2 overflow-auto text-xs">{{ entry.detailsJson }}</pre>
            </details>
          </article>
        </div>
        <UiCrudEmptyState v-else title="No saved backend logs" description="Run onboarding or AF/SS seeding, then refresh this page." icon="i-lucide-list-collapse" />
      </UCard>

      <UCard>
        <template #header>
          <div class="flex items-center justify-between gap-3">
            <div class="flex items-center gap-2">
              <UIcon name="i-lucide-monitor" class="h-5 w-5" />
              <h2 class="font-semibold">This browser session logs</h2>
            </div>
            <UButton color="warning" variant="subtle" icon="i-lucide-trash-2" @click="feedback.clearLogs">Clear browser logs</UButton>
          </div>
        </template>

        <div v-if="filteredLocalLogs.length" class="space-y-3">
          <article v-for="entry in filteredLocalLogs" :key="entry.id" class="rounded-2xl border border-slate-200 p-4 text-sm dark:border-slate-800">
            <div class="flex flex-col gap-2 lg:flex-row lg:items-start lg:justify-between">
              <div class="space-y-1">
                <div class="flex flex-wrap items-center gap-2">
                  <UBadge :color="entry.color" variant="subtle">{{ entry.title }}</UBadge>
                  <UBadge v-if="entry.statusCode" color="neutral" variant="outline">{{ entry.statusCode }}</UBadge>
                  <span v-if="entry.resource" class="text-xs text-slate-500">{{ entry.resource }}</span>
                </div>
                <p class="text-slate-600 dark:text-slate-300">{{ entry.message }}</p>
              </div>
              <div class="flex items-center gap-2 text-xs text-slate-500">
                <span>{{ formatDate(entry.at) }}</span>
                <UButton v-if="entry.details" color="neutral" variant="ghost" size="xs" icon="i-lucide-copy" label="Copy" @click="copyDetails(entry)" />
              </div>
            </div>
            <details v-if="entry.details" class="mt-3 rounded-xl bg-slate-50 p-3 dark:bg-slate-950/50">
              <summary class="cursor-pointer font-medium">Technical details</summary>
              <pre class="mt-2 overflow-auto text-xs">{{ entry.details }}</pre>
            </details>
          </article>
        </div>
        <UiCrudEmptyState v-else title="No browser logs" description="UI success and failure messages from this tab appear here." icon="i-lucide-monitor" />
      </UCard>
    </div>
  </AppShell>
</template>
