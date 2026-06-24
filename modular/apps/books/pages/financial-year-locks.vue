<template>
  <section class="space-y-4">
    <div class="border border-default bg-muted/10 p-5">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="text-sm text-muted">Accounting controls</p>
          <h2 class="mt-1 text-2xl font-semibold">Financial Year Locks</h2>
          <p class="mt-2 max-w-3xl text-sm text-muted">
            Review active and historical period locks, module lock coverage, operator trace and journal balance checks. Lock and unlock actions remain in the audited legacy/admin flow for now.
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

    <section class="grid gap-4 xl:grid-cols-[minmax(0,1.5fr)_minmax(340px,0.8fr)]">
      <div class="border border-default bg-muted/10 p-4">
        <div class="mb-3 flex flex-col gap-2 lg:flex-row lg:items-center lg:justify-between">
          <div>
            <h3 class="text-base font-semibold">Lock Register</h3>
            <p class="text-xs text-muted">{{ filteredLocks.length }} period lock(s) shown</p>
          </div>
          <div class="flex flex-col gap-2 sm:flex-row">
            <USelect v-model="statusFilter" :items="statusItems" class="sm:w-40" />
            <UInput v-model="search" icon="i-lucide-search" placeholder="Search locks" class="sm:w-72" />
          </div>
        </div>

        <BooksMasterTable :columns="lockColumns" :rows="lockRows" empty-text="No financial year locks found." />
      </div>

      <aside class="space-y-4">
        <div class="border border-default bg-muted/10 p-4">
          <div class="flex items-start justify-between gap-3">
            <div>
              <h3 class="text-base font-semibold">Selected Period</h3>
              <p class="text-xs text-muted">{{ selectedLockLabel }}</p>
            </div>
            <UBadge :color="selectedLock?.active === false ? 'neutral' : 'success'" variant="subtle">{{ selectedLock?.active === false ? 'Unlocked' : 'Active' }}</UBadge>
          </div>

          <USelect v-model="selectedLockId" :items="lockSelectItems" class="mt-4" placeholder="Select lock" />

          <dl v-if="selectedLock" class="mt-4 grid gap-3 text-sm">
            <div v-for="item in selectedDetails" :key="item.label" class="border-b border-default pb-2">
              <dt class="text-xs text-muted">{{ item.label }}</dt>
              <dd class="mt-1 break-words font-medium">{{ item.value }}</dd>
            </div>
          </dl>
          <p v-else class="mt-6 text-sm text-muted">Select a lock row to review its period and operator trace.</p>
        </div>

        <div class="border border-default bg-muted/10 p-4">
          <div class="mb-3 flex items-start justify-between gap-3">
            <div>
              <h3 class="text-base font-semibold">Journal Balance Check</h3>
              <p class="text-xs text-muted">Last 5000 journal entries in current scope.</p>
            </div>
            <UBadge :color="journalIssueCount > 0 ? 'warning' : 'success'" variant="subtle">{{ journalIssueCount }} issue(s)</UBadge>
          </div>
          <BooksMasterTable :columns="journalColumns" :rows="journalRows" empty-text="No journal validation issues." />
        </div>
      </aside>
    </section>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney } from '@garmetix/shared-utils'
import { formatDate, readArray, readNumber, readText, toRows, type ApiRecord, useBooksApiClient } from '../utils/books-api'

useHead({ title: 'Financial Year Locks - Garmetix Books' })

const { get } = useBooksApiClient()
const loading = ref(true)
const error = ref('')
const search = ref('')
const statusFilter = ref('active')
const selectedLockId = ref('')
const locks = ref<ApiRecord[]>([])
const journalValidation = ref<ApiRecord | null>(null)

const statusItems = [
  { label: 'Active', value: 'active' },
  { label: 'Unlocked', value: 'unlocked' },
  { label: 'All', value: 'all' }
]
const lockColumns = [
  { key: 'financialYear', label: 'Financial Year' },
  { key: 'period', label: 'Period' },
  { key: 'scope', label: 'Scope' },
  { key: 'modules', label: 'Modules' },
  { key: 'status', label: 'Status' },
  { key: 'operator', label: 'Operator' }
]
const journalColumns = [
  { key: 'date', label: 'Date' },
  { key: 'entry', label: 'Entry' },
  { key: 'source', label: 'Source' },
  { key: 'severity', label: 'Severity' },
  { key: 'message', label: 'Message' },
  { key: 'difference', label: 'Difference' }
]
const moduleKeys = [
  ['lockAccounting', 'Accounting'],
  ['lockSales', 'Sales'],
  ['lockPurchase', 'Purchase'],
  ['lockInventory', 'Inventory'],
  ['lockGst', 'GST']
] as const
const activeLocks = computed(() => locks.value.filter(item => item.active !== false))
const inactiveLocks = computed(() => locks.value.filter(item => item.active === false))
const journalIssues = computed(() => readArray(journalValidation.value, ['issues']))
const journalIssueCount = computed(() => readNumber(journalValidation.value, ['issueCount']))
const cards = computed(() => [
  { label: 'Active Locks', value: activeLocks.value.length, detail: 'Currently blocking protected periods' },
  { label: 'Unlocked History', value: inactiveLocks.value.length, detail: 'Retained for audit review' },
  { label: 'GST Locked', value: locks.value.filter(item => item.active !== false && item.lockGst !== false).length, detail: 'Active GST lock coverage' },
  { label: 'Journal Difference', value: formatIndianMoney(readNumber(journalValidation.value, ['difference'])), detail: `${journalIssueCount.value} journal issue(s)` }
])
const filteredLocks = computed(() => {
  const term = search.value.trim().toLowerCase()
  return locks.value.filter(item => {
    const statusMatches = statusFilter.value === 'all'
      || (statusFilter.value === 'active' && item.active !== false)
      || (statusFilter.value === 'unlocked' && item.active === false)
    const textMatches = !term || [
      readText(item, ['financialYear']),
      readText(item, ['lockedBy']),
      readText(item, ['lockReason']),
      readText(item, ['unlockedBy']),
      readText(item, ['unlockReason'])
    ].join(' ').toLowerCase().includes(term)
    return statusMatches && textMatches
  })
})
const lockRows = computed(() => filteredLocks.value.map(item => ({
  id: readText(item, ['id']),
  financialYear: readText(item, ['financialYear']),
  period: `${formatDate(item.periodStart)} to ${formatDate(item.periodEnd)}`,
  scope: lockScope(item),
  modules: lockedModules(item),
  status: item.active === false ? 'Unlocked' : 'Active',
  operator: item.active === false ? readText(item, ['unlockedBy'], 'System') : readText(item, ['lockedBy'], 'System')
})))
const lockSelectItems = computed(() => filteredLocks.value.map(item => ({
  label: `${readText(item, ['financialYear'])} - ${formatDate(item.periodStart)} to ${formatDate(item.periodEnd)}`,
  value: readText(item, ['id'], '')
})))
const selectedLock = computed(() => locks.value.find(item => readText(item, ['id'], '') === selectedLockId.value) ?? null)
const selectedLockLabel = computed(() => selectedLock.value ? readText(selectedLock.value, ['financialYear']) : 'No period selected')
const selectedDetails = computed(() => {
  const item = selectedLock.value
  if (!item) return []
  return [
    { label: 'Period', value: `${formatDate(item.periodStart)} to ${formatDate(item.periodEnd)}` },
    { label: 'Scope', value: lockScope(item) },
    { label: 'Modules', value: lockedModules(item) },
    { label: 'Locked At', value: formatDateTime(item.lockedAt) },
    { label: 'Locked By', value: readText(item, ['lockedBy'], 'System') },
    { label: 'Lock Reason', value: readText(item, ['lockReason']) },
    { label: 'Unlocked At', value: formatDateTime(item.unlockedAt) },
    { label: 'Unlocked By', value: readText(item, ['unlockedBy']) },
    { label: 'Unlock Reason', value: readText(item, ['unlockReason']) }
  ]
})
const journalRows = computed(() => journalIssues.value.map(item => ({
  id: readText(item, ['journalEntryId']),
  date: formatDate(item.onDate),
  entry: readText(item, ['entryNumber']),
  source: readText(item, ['sourceType']),
  severity: readText(item, ['severity']),
  message: readText(item, ['message']),
  difference: formatIndianMoney(readNumber(item, ['difference']))
})))

function lockedModules(item: ApiRecord) {
  const modules = moduleKeys.filter(([key]) => item[key] !== false).map(([, label]) => label)
  return modules.length > 0 ? modules.join(', ') : '-'
}

function lockScope(item: ApiRecord) {
  if (item.storeId) return 'Store'
  if (item.storeGroupId) return 'Store Group'
  return 'Company'
}

function formatDateTime(value: unknown) {
  if (!value) return '-'
  const date = new Date(String(value))
  if (Number.isNaN(date.getTime())) return String(value)
  return new Intl.DateTimeFormat('en-IN', { dateStyle: 'medium', timeStyle: 'short' }).format(date)
}

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const [lockData, journalData] = await Promise.allSettled([
      get<unknown>('accounting/financial-year-locks', { activeOnly: false }),
      get<unknown>('accounting/journal/validation')
    ])

    if (lockData.status === 'fulfilled') locks.value = toRows(lockData.value)
    if (journalData.status === 'fulfilled' && journalData.value && typeof journalData.value === 'object') journalValidation.value = journalData.value as ApiRecord
    const failed = [lockData, journalData].filter(item => item.status === 'rejected').length
    if (failed) error.value = `${failed} financial control request(s) could not be loaded.`
    if (!selectedLockId.value && locks.value.length > 0) selectedLockId.value = readText(locks.value[0], ['id'], '')
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load financial year locks.'
  } finally {
    loading.value = false
  }
}

onMounted(refresh)
</script>
