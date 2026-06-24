<template>
  <section class="space-y-4">
    <div class="border border-default bg-muted/10 p-5">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="text-sm text-muted">Accounting audit</p>
          <h2 class="mt-1 text-2xl font-semibold">Books Audit Review</h2>
          <p class="mt-2 max-w-3xl text-sm text-muted">
            Accounting-scoped audit trail for ledgers, vouchers, banking, GST, petty cash, financial locks and vendor settlement activity.
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
            <h3 class="text-base font-semibold">Recent Audit Events</h3>
            <p class="text-xs text-muted">{{ filteredRows.length }} event(s) shown</p>
          </div>
          <div class="flex flex-col gap-2 sm:flex-row">
            <USelect v-model="moduleFilter" :items="moduleItems" class="sm:w-44" />
            <UInput v-model="search" icon="i-lucide-search" placeholder="Search audit" class="sm:w-72" />
          </div>
        </div>

        <div class="overflow-hidden border border-default">
          <div class="overflow-x-auto">
            <table class="w-full min-w-[960px] text-left text-sm">
              <thead class="bg-muted/30 text-xs uppercase text-muted">
                <tr>
                  <th class="whitespace-nowrap px-3 py-2 font-medium">When</th>
                  <th class="whitespace-nowrap px-3 py-2 font-medium">Module</th>
                  <th class="whitespace-nowrap px-3 py-2 font-medium">Action</th>
                  <th class="whitespace-nowrap px-3 py-2 font-medium">Entity</th>
                  <th class="whitespace-nowrap px-3 py-2 font-medium">Reference</th>
                  <th class="whitespace-nowrap px-3 py-2 font-medium">Actor</th>
                  <th class="whitespace-nowrap px-3 py-2 font-medium">Detail</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-default">
                <tr v-if="filteredRows.length === 0">
                  <td colspan="7" class="px-3 py-8 text-center text-muted">No accounting audit events found.</td>
                </tr>
                <tr v-for="row in filteredRows" :key="readText(row, ['id'])" class="bg-default/40">
                  <td class="whitespace-nowrap px-3 py-2">{{ formatDateTime(row.occurredAt) }}</td>
                  <td class="whitespace-nowrap px-3 py-2">{{ readText(row, ['module']) }}</td>
                  <td class="whitespace-nowrap px-3 py-2">{{ readText(row, ['action']) }}</td>
                  <td class="max-w-56 truncate px-3 py-2">{{ readText(row, ['entityDisplayName', 'entityName']) }}</td>
                  <td class="max-w-48 truncate px-3 py-2">{{ readText(row, ['reference', 'entityId']) }}</td>
                  <td class="max-w-40 truncate px-3 py-2">{{ readText(row, ['userName'], 'System') }}</td>
                  <td class="px-3 py-2">
                    <UButton icon="i-lucide-eye" size="xs" color="neutral" variant="soft" :loading="detailLoading === readText(row, ['id'])" @click="selectAudit(row)">View</UButton>
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
            <h3 class="text-base font-semibold">Event Detail</h3>
            <p class="text-xs text-muted">{{ selectedAuditLabel }}</p>
          </div>
          <UBadge :color="selectedAudit ? 'success' : 'neutral'" variant="subtle">{{ selectedAudit ? readText(selectedAudit, ['action']) : 'None' }}</UBadge>
        </div>

        <dl v-if="selectedAudit" class="mt-4 grid gap-3 text-sm">
          <div v-for="item in selectedDetails" :key="item.label" class="border-b border-default pb-2">
            <dt class="text-xs text-muted">{{ item.label }}</dt>
            <dd class="mt-1 break-words font-medium">{{ item.value }}</dd>
          </div>
        </dl>
        <div v-if="selectedChanges.length > 0" class="mt-4">
          <h4 class="text-sm font-semibold">Changed Fields</h4>
          <BooksMasterTable class="mt-2" :columns="changeColumns" :rows="selectedChanges" empty-text="No changed fields." />
        </div>
        <p v-if="!selectedAudit" class="mt-8 text-center text-sm text-muted">Select an audit event to inspect before/after changes.</p>
      </aside>
    </section>
  </section>
</template>

<script setup lang="ts">
import { readNumber, readText, toRows, type ApiRecord, useBooksApiClient } from '../utils/books-api'

useHead({ title: 'Audit - Garmetix Books' })

const { get } = useBooksApiClient()
const loading = ref(true)
const error = ref('')
const search = ref('')
const moduleFilter = ref('all')
const rows = ref<ApiRecord[]>([])
const selectedAudit = ref<ApiRecord | null>(null)
const detailLoading = ref('')
const moduleItems = computed(() => [
  { label: 'All Modules', value: 'all' },
  ...Array.from(new Set(rows.value.map(row => readText(row, ['module'])).filter(value => value !== '-'))).sort().map(value => ({ label: value, value }))
])
const changeColumns = [
  { key: 'field', label: 'Field' },
  { key: 'before', label: 'Before' },
  { key: 'after', label: 'After' }
]
const filteredRows = computed(() => {
  const term = search.value.trim().toLowerCase()
  return rows.value.filter(row => {
    const moduleMatches = moduleFilter.value === 'all' || readText(row, ['module']) === moduleFilter.value
    const textMatches = !term || [
      readText(row, ['module']),
      readText(row, ['action']),
      readText(row, ['entityName']),
      readText(row, ['entityDisplayName']),
      readText(row, ['reference']),
      readText(row, ['userName']),
      readText(row, ['requestPath']),
      readText(row, ['reason'])
    ].join(' ').toLowerCase().includes(term)
    return moduleMatches && textMatches
  })
})
const cards = computed(() => {
  const changed = rows.value.filter(row => readNumber(row, ['changedFieldCount']) > 0).length
  const deletes = rows.value.filter(row => readText(row, ['action']).toLowerCase().includes('delete')).length
  return [
    { label: 'Audit Events', value: rows.value.length, detail: 'Accounting scope' },
    { label: 'Changed Records', value: changed, detail: 'Events with field changes' },
    { label: 'Delete Events', value: deletes, detail: 'Scrutiny required' },
    { label: 'Modules', value: moduleItems.value.length - 1, detail: 'Distinct audit modules' }
  ]
})
const selectedAuditLabel = computed(() => selectedAudit.value ? readText(selectedAudit.value, ['reference', 'entityDisplayName']) : 'No event selected')
const selectedDetails = computed(() => {
  const item = selectedAudit.value
  if (!item) return []
  return [
    { label: 'Occurred At', value: formatDateTime(item.occurredAt) },
    { label: 'Module', value: readText(item, ['module']) },
    { label: 'Action', value: readText(item, ['action']) },
    { label: 'Entity', value: readText(item, ['entityDisplayName', 'entityName']) },
    { label: 'Reference', value: readText(item, ['reference', 'entityId']) },
    { label: 'Actor', value: readText(item, ['userName'], 'System') },
    { label: 'Request Path', value: readText(item, ['requestPath']) },
    { label: 'Reason', value: readText(item, ['reason']) },
    { label: 'Trace', value: readText(item, ['traceIdentifier']) }
  ]
})
const selectedChanges = computed<ApiRecord[]>(() => parseChanges(readText(selectedAudit.value, ['changesJson'], '')))

function formatDateTime(value: unknown) {
  if (!value) return '-'
  const date = new Date(String(value))
  if (Number.isNaN(date.getTime())) return String(value)
  return new Intl.DateTimeFormat('en-IN', { dateStyle: 'medium', timeStyle: 'short' }).format(date)
}

function parseChanges(value: string) {
  if (!value || value === '-') return []
  try {
    const parsed = JSON.parse(value) as Array<Record<string, unknown>>
    if (!Array.isArray(parsed)) return []
    return parsed.map(item => ({
      field: readText(item, ['field', 'Field', 'name', 'Name']),
      before: readText(item, ['before', 'Before']),
      after: readText(item, ['after', 'After'])
    }))
  } catch {
    return []
  }
}

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    rows.value = toRows(await get<unknown>('accounting/audit/recent', { take: 200 }))
    if (!selectedAudit.value && rows.value.length > 0) await selectAudit(rows.value[0])
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load accounting audit.'
  } finally {
    loading.value = false
  }
}

async function selectAudit(row: ApiRecord) {
  const id = readText(row, ['id'], '')
  selectedAudit.value = row
  if (!id) return
  detailLoading.value = id
  try {
    const detail = await get<unknown>(`accounting/audit/events/${id}`)
    if (detail && typeof detail === 'object') selectedAudit.value = detail as ApiRecord
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load audit event.'
  } finally {
    detailLoading.value = ''
  }
}

onMounted(refresh)
</script>
