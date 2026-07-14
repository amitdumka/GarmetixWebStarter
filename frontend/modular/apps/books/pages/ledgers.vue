<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-book-open" class="size-4" /> Chart of accounts</p>
          <h2 class="garmetix-dashboard-title">Ledgers</h2>
          <p class="garmetix-dashboard-subtitle">
            Ledgers, ledger groups and ledger synchronization moved out of Accounting for a cleaner workspace.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton v-if="activeTab === 'ledgers'" icon="i-lucide-plus" color="primary" variant="solid" @click="startCreate">New Ledger</UButton>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
          <UBadge :color="syncTone" variant="subtle">{{ syncLabel }}</UBadge>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />
    <UAlert v-if="message" color="success" variant="subtle" icon="i-lucide-circle-check" :description="message" />

    <section class="grid gap-3 md:grid-cols-3">
      <div class="garmetix-metric-card">
        <p class="garmetix-metric-label">Ledgers</p>
        <p class="garmetix-metric-value">{{ ledgers.length }}</p>
        <p class="garmetix-metric-caption">Chart of accounts</p>
      </div>
      <div class="garmetix-metric-card">
        <p class="garmetix-metric-label">Ledger Groups</p>
        <p class="garmetix-metric-value">{{ ledgerGroups.length }}</p>
        <p class="garmetix-metric-caption">Indian accounting categories</p>
      </div>
      <div class="garmetix-metric-card">
        <p class="garmetix-metric-label">Sync Issues</p>
        <p class="garmetix-metric-value">{{ readNumber(ledgerSync, ['issueCount']) }}</p>
        <p class="garmetix-metric-caption">Party / bank ledger links</p>
      </div>
    </section>

    <div class="flex flex-wrap gap-2">
      <UButton
        v-for="tab in tabs"
        :key="tab.key"
        :icon="tab.icon"
        size="sm"
        color="neutral"
        :variant="activeTab === tab.key ? 'soft' : 'ghost'"
        @click="activeTab = tab.key"
      >
        {{ tab.label }}
      </UButton>
    </div>

    <section class="garmetix-section-card">
      <div class="mb-3 flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h3 class="garmetix-panel-title">{{ currentTab.label }}</h3>
          <p class="garmetix-panel-subtitle">{{ currentTab.description }}</p>
        </div>
        <div class="flex flex-col gap-2 sm:flex-row">
          <UInput v-model="search" icon="i-lucide-search" placeholder="Search" class="sm:w-72" />
          <UButton
            v-if="activeTab === 'ledgerSync'"
            icon="i-lucide-wrench"
            color="warning"
            variant="soft"
            :loading="repairing"
            @click="repairLedgerSync"
          >
            Repair Sync
          </UButton>
        </div>
      </div>

      <div v-if="activeTab === 'ledgerSync'" class="mb-3 flex flex-wrap items-center justify-between gap-3 rounded-lg border border-default bg-muted/30 p-3 text-sm">
        <div>
          <p class="font-medium">Ledger synchronization</p>
          <p class="text-muted">{{ readNumber(ledgerSync, ['partyCount']) }} parties - {{ readNumber(ledgerSync, ['bankAccountCount']) }} bank accounts - {{ readNumber(ledgerSync, ['issueCount']) }} issue(s)</p>
        </div>
      </div>

      <BooksMasterTable :columns="currentColumns" :rows="pagedRows" empty-text="No ledger rows found.">
        <template v-if="activeTab === 'ledgers'" #actions="{ row }">
          <div class="flex flex-wrap items-center gap-1">
            <UButton icon="i-lucide-pencil" size="xs" color="neutral" variant="ghost" @click="startEdit(findRowById(row.id))" />
            <UButton icon="i-lucide-trash-2" size="xs" color="error" variant="ghost" @click="askDelete(findRowById(row.id))" />
          </div>
        </template>
      </BooksMasterTable>

      <div v-if="filteredRows.length" class="mt-3 flex flex-wrap items-center justify-between gap-2 text-sm text-muted">
        <p>Showing {{ pagedRows.length }} of {{ filteredRows.length }} row(s)</p>
        <div class="flex items-center gap-2">
          <USelect v-model="pageSize" :items="pageSizeOptions" class="w-28" />
          <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-left" :disabled="page <= 1" @click="page--">Prev</UButton>
          <span>{{ page }} / {{ totalPages }}</span>
          <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-right" :disabled="page >= totalPages" @click="page++">Next</UButton>
        </div>
      </div>
    </section>

    <UModal v-model:open="formOpen" :title="formMode === 'edit' ? 'Edit Ledger' : 'New Ledger'">
      <template #body>
        <form class="grid gap-3 sm:grid-cols-2" @submit.prevent="saveLedger">
          <label class="space-y-1 text-sm sm:col-span-2">
            <span class="text-muted">Name</span>
            <UInput v-model="ledgerForm.name" placeholder="Ledger name" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Ledger Group</span>
            <USelect v-model="ledgerForm.ledgerGroupId" :items="ledgerGroupSelectItems" placeholder="Select group" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Ledger Type</span>
            <USelect v-model="ledgerForm.ledgerType" :items="ledgerTypeSelectItems" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Opening Date</span>
            <UInput v-model="ledgerForm.openingDate" type="date" />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Opening Balance</span>
            <UInput v-model="ledgerForm.openingBalance" type="number" min="0" step="0.01" placeholder="0.00" />
          </label>

          <div class="flex justify-end gap-2 sm:col-span-2">
            <UButton type="submit" icon="i-lucide-save" color="primary" :loading="saving">
              {{ formMode === 'edit' ? 'Update' : 'Save' }}
            </UButton>
          </div>
        </form>
      </template>
    </UModal>

    <section class="garmetix-section-card">
      <div class="mb-3 flex flex-col gap-2 lg:flex-row lg:items-center lg:justify-between">
        <div>
          <h3 class="garmetix-panel-title">Ledger Statement</h3>
          <p class="garmetix-panel-subtitle">Audit posted entries for a selected ledger without exposing internal party flags.</p>
        </div>
        <div class="flex flex-col gap-2 sm:flex-row">
          <USelectMenu v-model="selectedLedgerId" value-key="value" :items="ledgerSelectItems" placeholder="Search ledger..." class="sm:w-72" />
          <UButton icon="i-lucide-file-search" color="neutral" variant="soft" :loading="statementLoading" @click="loadLedgerStatement">View</UButton>
        </div>
      </div>

      <BooksMasterTable :columns="ledgerStatementColumns" :rows="ledgerStatementRows" empty-text="Select a ledger to view posted entries." />
    </section>
  </section>
</template>

<script setup lang="ts">
import { formatIndianMoney } from '@garmetix/shared-utils'
import {
  ledgerTypeOptions,
  optionLabel,
  pageSizeOptions,
  paginateRows,
  readArray,
  readNumber,
  readText,
  toRows,
  type ApiRecord,
  useBooksApiClient
} from '../utils/books-api'

useHead({ title: 'Ledgers - Garmetix Books' })

type LedgerTab = 'ledgers' | 'ledgerGroups' | 'ledgerSync'

interface LedgerForm {
  id: string
  name: string
  ledgerGroupId: string
  ledgerType: number
  openingDate: string
  openingBalance: number | string
}

function localDateInput(value: unknown = new Date()) {
  if (typeof value === 'string' && /^\d{4}-\d{2}-\d{2}/.test(value)) return value.slice(0, 10)
  const date = value instanceof Date ? value : new Date(String(value || new Date()))
  if (Number.isNaN(date.getTime())) return localDateInput(new Date())
  const offsetMs = date.getTimezoneOffset() * 60_000
  return new Date(date.getTime() - offsetMs).toISOString().slice(0, 10)
}

function toApiDate(value: string) {
  return `${localDateInput(value)}T00:00:00`
}

function emptyLedgerForm(): LedgerForm {
  return { id: '', name: '', ledgerGroupId: '', ledgerType: 4, openingDate: localDateInput(), openingBalance: 0 }
}

const { get, post, put, del } = useBooksApiClient()
const loading = ref(true)
const repairing = ref(false)
const statementLoading = ref(false)
const saving = ref(false)
const error = ref('')
const message = ref('')
const search = ref('')
const activeTab = ref<LedgerTab>('ledgers')
const selectedLedgerId = ref('')
const formMode = ref<'create' | 'edit'>('create')
const formOpen = ref(false)

const ledgerGroups = ref<ApiRecord[]>([])
const ledgers = ref<ApiRecord[]>([])
const ledgerSync = ref<ApiRecord | null>(null)
const ledgerStatement = ref<ApiRecord[]>([])
const setupStatus = ref<ApiRecord | null>(null)
const companies = ref<ApiRecord[]>([])
const stores = ref<ApiRecord[]>([])

const ledgerForm = reactive<LedgerForm>(emptyLedgerForm())

const ledgerGroupSelectItems = computed(() => ledgerGroups.value
  .map(item => ({ label: readText(item, ['name']), value: readText(item, ['id'], '') }))
  .filter(item => item.value))
const ledgerTypeSelectItems = ledgerTypeOptions.map(item => ({ label: item.label, value: item.value }))
const ledgerSelectItems = computed(() => ledgers.value
  .map(item => ({ label: readText(item, ['name']), value: readText(item, ['id'], '') }))
  .filter(item => item.value))

const tabs: Array<{ key: LedgerTab, label: string, icon: string, description: string }> = [
  { key: 'ledgers', label: 'Ledger', icon: 'i-lucide-book-open', description: 'Chart of accounts with protected internal party/bank flags hidden.' },
  { key: 'ledgerGroups', label: 'Ledger Group', icon: 'i-lucide-folder-tree', description: 'Indian accounting ledger groups and categories.' },
  { key: 'ledgerSync', label: 'Ledger Sync', icon: 'i-lucide-link', description: 'Party and bank ledger synchronization issues.' }
]
const currentTab = computed(() => tabs.find(item => item.key === activeTab.value) ?? tabs[0])

const groupName = (id: unknown) => readText(ledgerGroups.value.find(item => item.id === id), ['name'])

const syncIssues = computed(() => readArray(ledgerSync.value, ['issues']))
const syncLabel = computed(() => {
  const count = readNumber(ledgerSync.value, ['issueCount'])
  if (!ledgerSync.value) return 'Sync checking'
  return count > 0 ? `${count} sync issue(s)` : 'Ledger sync clear'
})
const syncTone = computed<'success' | 'warning' | 'neutral'>(() => {
  if (!ledgerSync.value) return 'neutral'
  return readNumber(ledgerSync.value, ['issueCount']) > 0 ? 'warning' : 'success'
})

function findRowById(id: unknown): ApiRecord | null {
  const source: Record<LedgerTab, ApiRecord[]> = {
    ledgers: ledgers.value,
    ledgerGroups: ledgerGroups.value,
    ledgerSync: []
  }
  return (source[activeTab.value] ?? []).find(item => readText(item, ['id'], '') === id) ?? null
}

const tableRows = computed<Record<LedgerTab, ApiRecord[]>>(() => ({
  ledgers: ledgers.value.map(item => ({
    id: readText(item, ['id'], ''),
    name: readText(item, ['name']),
    group: groupName(item.ledgerGroupId),
    type: optionLabel(ledgerTypeOptions, item.ledgerType),
    opening: formatIndianMoney(readNumber(item, ['openingBalance'])),
    status: readText(item, ['active'], 'Active')
  })),
  ledgerGroups: ledgerGroups.value.map(item => ({
    name: readText(item, ['name']),
    category: readText(item, ['category']),
    description: readText(item, ['description'])
  })),
  ledgerSync: syncIssues.value.map(item => ({
    area: readText(item, ['area']),
    entity: readText(item, ['entityName']),
    severity: readText(item, ['severity']),
    issue: readText(item, ['message']),
    action: readText(item, ['fixAction'])
  }))
}))

const columns: Record<LedgerTab, Array<{ key: string, label: string }>> = {
  ledgers: [
    { key: 'name', label: 'Ledger' },
    { key: 'group', label: 'Group' },
    { key: 'type', label: 'Type' },
    { key: 'opening', label: 'Opening' },
    { key: 'status', label: 'Status' }
  ],
  ledgerGroups: [
    { key: 'name', label: 'Group' },
    { key: 'category', label: 'Category' },
    { key: 'description', label: 'Description' }
  ],
  ledgerSync: [
    { key: 'area', label: 'Area' },
    { key: 'entity', label: 'Party / Bank' },
    { key: 'severity', label: 'Severity' },
    { key: 'issue', label: 'Issue' },
    { key: 'action', label: 'Action' }
  ]
}
const currentColumns = computed(() => columns[activeTab.value])
const currentRows = computed(() => tableRows.value[activeTab.value])
const filteredRows = computed(() => {
  const term = search.value.trim().toLowerCase()
  if (!term) return currentRows.value
  return currentRows.value.filter(row => JSON.stringify(row).toLowerCase().includes(term))
})

const page = ref(1)
const pageSize = ref<number>(pageSizeOptions[0].value)
const totalPages = computed(() => Math.max(1, Math.ceil(filteredRows.value.length / Number(pageSize.value || 25))))
const pagedRows = computed(() => paginateRows(filteredRows.value, page.value, pageSize.value))
watch([search, pageSize], () => { page.value = 1 })
watch(totalPages, (value) => { if (page.value > value) page.value = value })

const ledgerStatementColumns = [
  { key: 'date', label: 'Date' },
  { key: 'entry', label: 'Entry' },
  { key: 'source', label: 'Source' },
  { key: 'particulars', label: 'Particulars' },
  { key: 'debit', label: 'Debit' },
  { key: 'credit', label: 'Credit' },
  { key: 'balance', label: 'Balance' }
]
const ledgerStatementRows = computed(() => ledgerStatement.value.map(item => ({
  date: readText(item, ['onDate']),
  entry: readText(item, ['entryNumber', 'referenceNumber']),
  source: readText(item, ['sourceType']),
  particulars: readText(item, ['particulars']),
  debit: formatIndianMoney(readNumber(item, ['debit'])),
  credit: formatIndianMoney(readNumber(item, ['credit'])),
  balance: `${formatIndianMoney(readNumber(item, ['balance']))} ${readText(item, ['balanceType'], '')}`.trim()
})))

function resolveCompanyId() {
  const companyId = readText(setupStatus.value, ['companyId'], '')
    || readText(stores.value[0], ['companyId'], '')
    || readText(companies.value[0], ['id'], '')
  if (!companyId) throw new Error('Run quick setup before saving ledgers.')
  return companyId
}

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const [setupData, companyData, storeData, groupData, ledgerData, syncData] = await Promise.allSettled([
      get<unknown>('setup/status'),
      get<unknown>('companies'),
      get<unknown>('stores'),
      get<unknown>('ledger-groups'),
      get<unknown>('ledgers'),
      get<unknown>('accounting/ledger-sync/status')
    ])
    if (setupData.status === 'fulfilled' && setupData.value && typeof setupData.value === 'object') setupStatus.value = setupData.value as ApiRecord
    if (companyData.status === 'fulfilled') companies.value = toRows(companyData.value)
    if (storeData.status === 'fulfilled') stores.value = toRows(storeData.value)
    if (groupData.status === 'fulfilled') ledgerGroups.value = toRows(groupData.value)
    if (ledgerData.status === 'fulfilled') ledgers.value = toRows(ledgerData.value)
    if (syncData.status === 'fulfilled' && syncData.value && typeof syncData.value === 'object') ledgerSync.value = syncData.value as ApiRecord

    if (!selectedLedgerId.value && ledgers.value.length) selectedLedgerId.value = readText(ledgers.value[0], ['id'], '')

    const failedReasons = [groupData, ledgerData, syncData]
      .filter((item): item is PromiseRejectedResult => item.status === 'rejected')
      .map(item => item.reason instanceof Error ? item.reason.message : String(item.reason))
    if (failedReasons.length) error.value = failedReasons[0]
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load ledgers.'
  } finally {
    loading.value = false
  }
}

async function repairLedgerSync() {
  const phrase = window.prompt('Type REPAIR LEDGER SYNC to create missing party and bank ledgers.')
  if (phrase !== 'REPAIR LEDGER SYNC') return

  repairing.value = true
  error.value = ''
  try {
    await post<unknown>('accounting/ledger-sync/repair', {})
    await refresh()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to repair ledger sync.'
  } finally {
    repairing.value = false
  }
}

async function loadLedgerStatement() {
  if (!selectedLedgerId.value) {
    error.value = 'Select ledger before loading statement.'
    return
  }

  statementLoading.value = true
  error.value = ''
  try {
    ledgerStatement.value = toRows(await get<unknown>(`accounting/ledger-statement/${selectedLedgerId.value}`))
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load ledger statement.'
  } finally {
    statementLoading.value = false
  }
}

function startCreate() {
  formMode.value = 'create'
  Object.assign(ledgerForm, emptyLedgerForm(), { ledgerGroupId: ledgerGroupSelectItems.value[0]?.value || '' })
  message.value = ''
  error.value = ''
  formOpen.value = true
}

function startEdit(item: ApiRecord | null) {
  if (!item) return
  formMode.value = 'edit'
  Object.assign(ledgerForm, emptyLedgerForm(), {
    id: readText(item, ['id'], ''),
    name: readText(item, ['name'], ''),
    ledgerGroupId: readText(item, ['ledgerGroupId'], '') || ledgerGroupSelectItems.value[0]?.value || '',
    ledgerType: Number(item?.ledgerType ?? 4),
    openingDate: localDateInput(item?.openingDate),
    openingBalance: readNumber(item, ['openingBalance'])
  })
  message.value = ''
  error.value = ''
  formOpen.value = true
}

async function saveLedger() {
  saving.value = true
  error.value = ''
  message.value = ''
  try {
    const companyId = resolveCompanyId()
    if (!ledgerForm.name.trim()) throw new Error('Enter ledger name.')
    if (!ledgerForm.ledgerGroupId) throw new Error('Select ledger group.')

    const payload = {
      companyId,
      name: ledgerForm.name.trim(),
      ledgerGroupId: ledgerForm.ledgerGroupId,
      ledgerType: Number(ledgerForm.ledgerType),
      openingDate: toApiDate(ledgerForm.openingDate),
      openingBalance: Number(ledgerForm.openingBalance || 0)
    }

    if (formMode.value === 'edit' && ledgerForm.id) {
      await put<unknown>(`ledgers/${ledgerForm.id}`, payload)
      message.value = 'Ledger updated.'
    } else {
      await post<unknown>('ledgers', payload)
      message.value = 'Ledger saved.'
    }

    formOpen.value = false
    await refresh()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to save ledger.'
  } finally {
    saving.value = false
  }
}

function askDelete(item: ApiRecord | null) {
  if (!item) return
  const id = readText(item, ['id'], '')
  if (!id) return
  if (!window.confirm('Delete this ledger?')) return
  confirmDelete(id)
}

async function confirmDelete(id: string) {
  error.value = ''
  message.value = ''
  try {
    await del<unknown>(`ledgers/${id}`)
    message.value = 'Ledger deleted.'
    await refresh()
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to delete ledger.'
  }
}

watch(activeTab, () => { search.value = '' })

onMounted(refresh)
</script>
