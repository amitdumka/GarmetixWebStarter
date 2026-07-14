<template>
  <section class="space-y-5">
    <div class="flex flex-wrap items-start justify-between gap-3">
      <div>
        <p class="garmetix-kicker"><UIcon name="i-lucide-lock-keyhole" class="size-4" /> Final Accounts</p>
        <h1 class="garmetix-dashboard-title">Period Closeout</h1>
      </div>
      <div class="flex flex-wrap items-center gap-2">
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="loadAll">Refresh</UButton>
        <UButton to="/fiscal-periods" icon="i-lucide-calendar-range" color="neutral" variant="soft">Periods</UButton>
        <UButton to="/reports" icon="i-lucide-scale" color="neutral" variant="soft">Reports</UButton>
      </div>
    </div>

    <UAlert v-if="message" :icon="messageIcon" :color="messageTone" variant="subtle" :title="messageTitle" :description="message" />

    <div class="final-accounts-grid">
      <UCard v-for="card in metricCards" :key="card.label" :ui="{ body: 'p-4' }">
        <div class="flex items-center justify-between gap-3">
          <div>
            <p class="text-xs font-medium uppercase text-muted">{{ card.label }}</p>
            <p class="text-2xl font-semibold text-highlighted">{{ card.value }}</p>
          </div>
          <UIcon :name="card.icon" class="size-5 text-primary" />
        </div>
      </UCard>
    </div>

    <div class="grid gap-4 2xl:grid-cols-[minmax(25rem,31rem)_1fr]">
      <UCard :ui="{ body: 'p-5' }">
        <form class="space-y-4" @submit.prevent="previewClose">
          <h2 class="text-base font-semibold text-highlighted">Close Setup</h2>

          <UFormField label="Close type" name="closeType">
            <USelect v-model="form.closeType" :items="closeTypes" />
          </UFormField>

          <UFormField label="Fiscal year" name="fiscalYearId">
            <USelectMenu v-model="form.fiscalYearId" :items="yearItems" value-key="value" class="w-full" />
          </UFormField>

          <UFormField v-if="form.closeType === 'Period'" label="Fiscal period" name="fiscalPeriodId">
            <USelectMenu v-model="form.fiscalPeriodId" :items="periodItems" value-key="value" class="w-full" />
          </UFormField>

          <UFormField label="Close date" name="closeDate">
            <UInput v-model="form.closeDate" type="date" icon="i-lucide-calendar-check" />
          </UFormField>

          <div class="grid gap-2">
            <UCheckbox v-model="form.lockPeriod" label="Create accounting period lock" />
            <UCheckbox v-model="form.transferCurrentYearResult" label="Prepare current-year result transfer evidence" />
            <UCheckbox v-model="form.generateOpeningJournal" label="Prepare next-year opening journal evidence" />
            <UCheckbox v-model="confirmAllGates" label="Confirm all close gates before commit" />
          </div>

          <UFormField label="Approval notes" name="approvalNotes">
            <UTextarea v-model="approvalNotes" :rows="3" />
          </UFormField>

          <div class="grid gap-2 sm:grid-cols-2">
            <UButton type="submit" icon="i-lucide-shield-check" color="neutral" variant="soft" :loading="previewing" block>Preview</UButton>
            <UButton type="button" icon="i-lucide-lock-keyhole" :disabled="!preview?.canClose" :loading="saving" block @click="commitClose">Commit Close</UButton>
          </div>
        </form>
      </UCard>

      <UCard class="overflow-hidden" :ui="{ body: 'p-0' }">
        <div class="flex flex-wrap items-center gap-2 border-b border-default p-3">
          <USelect v-model="statusFilter" :items="statusItems" class="w-full sm:w-44" />
          <UButton icon="i-lucide-search" color="neutral" variant="soft" :loading="loading" @click="loadRuns">Search</UButton>
          <div class="ml-auto flex items-center gap-1">
            <UButton icon="i-lucide-chevron-left" color="neutral" variant="ghost" size="sm" :disabled="page <= 1" @click="previousPage" />
            <UBadge color="neutral" variant="subtle">{{ page }} / {{ totalPages }}</UBadge>
            <UButton icon="i-lucide-chevron-right" color="neutral" variant="ghost" size="sm" :disabled="page >= totalPages" @click="nextPage" />
          </div>
        </div>

        <UTable :data="runs.rows" :columns="runColumns" :loading="loading">
          <template #runNumber-cell="{ row }">
            <button class="font-mono text-sm text-primary hover:underline" type="button" @click="selectRun(row.original.id)">{{ row.original.runNumber }}</button>
          </template>
          <template #status-cell="{ row }">
            <UBadge :color="statusColor(row.original.status)" variant="subtle">{{ row.original.status }}</UBadge>
          </template>
          <template #periodEnd-cell="{ row }">
            {{ formatFinalAccountsDateOnly(row.original.periodEnd) }}
          </template>
          <template #actions-cell="{ row }">
            <div class="flex justify-end gap-1">
              <UTooltip text="View">
                <UButton icon="i-lucide-eye" color="neutral" variant="ghost" size="sm" @click="selectRun(row.original.id)" />
              </UTooltip>
              <UTooltip text="Reopen">
                <UButton icon="i-lucide-lock-open" color="warning" variant="ghost" size="sm" :disabled="row.original.status !== 'Closed'" @click="reopenRun(row.original.id)" />
              </UTooltip>
            </div>
          </template>
        </UTable>
      </UCard>
    </div>

    <div v-if="preview" class="grid gap-4 xl:grid-cols-[1fr_22rem]">
      <UCard class="overflow-hidden" :ui="{ body: 'p-0' }">
        <div class="border-b border-default p-4">
          <h2 class="text-base font-semibold text-highlighted">Preview Checklist</h2>
          <p class="mt-1 text-sm text-muted">{{ formatFinalAccountsDateOnly(preview.periodStart) }} to {{ formatFinalAccountsDateOnly(preview.periodEnd) }}</p>
        </div>
        <UTable :data="preview.checklist" :columns="checkColumns">
          <template #status-cell="{ row }">
            <UBadge :color="gateColor(row.original.status)" variant="subtle">{{ row.original.status }}</UBadge>
          </template>
          <template #amount-cell="{ row }">
            {{ row.original.amount === null || row.original.amount === undefined ? '-' : money(row.original.amount) }}
          </template>
        </UTable>
      </UCard>

      <UCard :ui="{ body: 'p-4' }">
        <h3 class="text-sm font-semibold text-highlighted">Close Metrics</h3>
        <dl class="mt-3 grid gap-2 text-sm">
          <div class="flex justify-between gap-3"><dt class="text-muted">Status</dt><dd class="font-medium text-highlighted">{{ preview.status }}</dd></div>
          <div class="flex justify-between gap-3"><dt class="text-muted">TB difference</dt><dd class="font-medium text-highlighted">{{ money(preview.trialBalanceDifference) }}</dd></div>
          <div class="flex justify-between gap-3"><dt class="text-muted">BS difference</dt><dd class="font-medium text-highlighted">{{ money(preview.balanceSheetDifference) }}</dd></div>
          <div class="flex justify-between gap-3"><dt class="text-muted">Pending</dt><dd class="font-medium text-highlighted">{{ preview.pendingPostingCount }}</dd></div>
          <div class="flex justify-between gap-3"><dt class="text-muted">Inventory</dt><dd class="font-medium text-highlighted">{{ money(preview.inventorySnapshotTotal) }}</dd></div>
          <div class="flex justify-between gap-3"><dt class="text-muted">PAT</dt><dd class="font-medium text-highlighted">{{ money(preview.profitAfterTax) }}</dd></div>
        </dl>
      </UCard>
    </div>

    <div v-if="selected" class="grid gap-4 2xl:grid-cols-[1fr_24rem]">
      <UCard class="overflow-hidden" :ui="{ body: 'p-0' }">
        <div class="border-b border-default p-4">
          <p class="font-mono text-sm text-muted">{{ selected.runNumber }}</p>
          <h2 class="text-lg font-semibold text-highlighted">{{ selected.closeType }} Close</h2>
          <div class="mt-2 flex flex-wrap gap-2">
            <UBadge :color="statusColor(selected.status)" variant="subtle">{{ selected.status }}</UBadge>
            <UBadge color="neutral" variant="subtle">{{ selected.reportSnapshotStatus }}</UBadge>
            <UBadge v-if="selected.financialYearLockId" color="primary" variant="subtle">Locked</UBadge>
          </div>
        </div>
        <UTabs v-model="selectedTab" :items="selectedTabs" class="p-4" />
        <div v-if="selectedTab === 'checklist'">
          <UTable :data="selected.checklist" :columns="checkColumns">
            <template #status-cell="{ row }">
              <UBadge :color="gateColor(row.original.status)" variant="subtle">{{ row.original.status }}</UBadge>
            </template>
            <template #amount-cell="{ row }">
              {{ row.original.amount === null || row.original.amount === undefined ? '-' : money(row.original.amount) }}
            </template>
          </UTable>
        </div>
        <div v-else-if="selectedTab === 'reports'">
          <UTable :data="selected.reports" :columns="reportColumns">
            <template #createdAt-cell="{ row }">
              {{ formatFinalAccountsDate(row.original.createdAt) }}
            </template>
          </UTable>
        </div>
        <div v-else>
          <UTable :data="selected.balances" :columns="balanceColumns">
            <template #closingBalance-cell="{ row }">
              {{ money(row.original.closingBalance) }}
            </template>
          </UTable>
        </div>
      </UCard>

      <UCard :ui="{ body: 'p-4' }">
        <h3 class="text-sm font-semibold text-highlighted">Audit Trail</h3>
        <div class="mt-3 space-y-2">
          <div v-for="event in selected.events" :key="`${event.event}-${event.at}`" class="rounded-md border border-default p-3">
            <div class="flex items-center justify-between gap-3">
              <span class="text-sm font-medium text-highlighted">{{ event.event }}</span>
              <span class="text-xs text-muted">{{ formatFinalAccountsDate(event.at) }}</span>
            </div>
            <p class="mt-1 text-sm text-muted">{{ event.detail }}</p>
            <p class="mt-1 text-xs text-muted">{{ event.actor || 'System' }}</p>
          </div>
        </div>
      </UCard>
    </div>
  </section>
</template>

<script setup lang="ts">
import {
  formatFinalAccountsDate,
  formatFinalAccountsDateOnly,
  type FinalAccountsCloseCommitRequest,
  type FinalAccountsClosePreview,
  type FinalAccountsCloseRequest,
  type FinalAccountsCloseRun,
  type FinalAccountsCloseRunList,
  type FinalAccountsFiscalPeriod,
  type FinalAccountsFiscalYear,
  useFinalAccountsApiClient
} from '../utils/final-accounts-api'

useHead({ title: 'Period Closeout' })

type SelectItem = { label: string; value: string }
type Tone = 'success' | 'warning' | 'error' | 'neutral'

const api = useFinalAccountsApiClient()
const loading = ref(false)
const saving = ref(false)
const previewing = ref(false)
const message = ref('')
const messageTone = ref<Tone>('neutral')
const years = ref<FinalAccountsFiscalYear[]>([])
const periods = ref<FinalAccountsFiscalPeriod[]>([])
const runs = ref<FinalAccountsCloseRunList>({ page: 1, pageSize: 25, totalCount: 0, rows: [] })
const preview = ref<FinalAccountsClosePreview | null>(null)
const selected = ref<FinalAccountsCloseRun | null>(null)
const selectedTab = ref('checklist')
const page = ref(1)
const pageSize = ref(25)
const statusFilter = ref('All')
const confirmAllGates = ref(false)
const approvalNotes = ref('')
const closeTypes = ['Period', 'Year']
const statusItems = ['All', 'Closed', 'Reopened']
const selectedTabs = [
  { label: 'Checklist', value: 'checklist', icon: 'i-lucide-list-checks' },
  { label: 'Reports', value: 'reports', icon: 'i-lucide-file-check' },
  { label: 'Balances', value: 'balances', icon: 'i-lucide-scale' }
]

const form = reactive({
  closeType: 'Period',
  fiscalYearId: '',
  fiscalPeriodId: '',
  closeDate: today(),
  lockPeriod: true,
  transferCurrentYearResult: true,
  generateOpeningJournal: true
})

const runColumns = [
  { accessorKey: 'runNumber', header: 'Run' },
  { accessorKey: 'closeType', header: 'Type' },
  { accessorKey: 'periodEnd', header: 'End' },
  { accessorKey: 'status', header: 'Status' },
  { accessorKey: 'pendingPostingCount', header: 'Pending' },
  { accessorKey: 'actions', header: '' }
]
const checkColumns = [
  { accessorKey: 'label', header: 'Gate' },
  { accessorKey: 'status', header: 'Status' },
  { accessorKey: 'detail', header: 'Detail' },
  { accessorKey: 'amount', header: 'Amount' }
]
const reportColumns = [
  { accessorKey: 'reportType', header: 'Report' },
  { accessorKey: 'status', header: 'Status' },
  { accessorKey: 'payloadHash', header: 'Hash' },
  { accessorKey: 'createdAt', header: 'Created' }
]
const balanceColumns = [
  { accessorKey: 'accountCode', header: 'Code' },
  { accessorKey: 'accountName', header: 'Account' },
  { accessorKey: 'accountType', header: 'Type' },
  { accessorKey: 'closingBalance', header: 'Closing' }
]

const yearItems = computed<SelectItem[]>(() => years.value.map(item => ({ label: `${item.name} (${item.status})`, value: item.id })))
const periodItems = computed<SelectItem[]>(() => periods.value
  .filter(item => !form.fiscalYearId || item.fiscalYearId === form.fiscalYearId)
  .map(item => ({ label: `${item.name} (${item.status})`, value: item.id })))
const totalPages = computed(() => Math.max(1, Math.ceil(runs.value.totalCount / runs.value.pageSize)))
const messageTitle = computed(() => messageTone.value === 'error' ? 'Closeout Failed' : messageTone.value === 'success' ? 'Done' : 'Closeout')
const messageIcon = computed(() => messageTone.value === 'error' ? 'i-lucide-circle-alert' : messageTone.value === 'success' ? 'i-lucide-circle-check' : 'i-lucide-info')
const metricCards = computed(() => [
  { label: 'Close Runs', value: String(runs.value.totalCount), icon: 'i-lucide-file-check' },
  { label: 'Closed', value: String(runs.value.rows.filter(item => item.status === 'Closed').length), icon: 'i-lucide-lock-keyhole' },
  { label: 'Reopened', value: String(runs.value.rows.filter(item => item.status === 'Reopened').length), icon: 'i-lucide-lock-open' },
  { label: 'Preview', value: preview.value?.status || '-', icon: 'i-lucide-shield-check' }
])

onMounted(loadAll)

async function loadAll() {
  loading.value = true
  try {
    const [yearRows, periodRows] = await Promise.all([
      api.get<FinalAccountsFiscalYear[]>('fiscal-years'),
      api.get<FinalAccountsFiscalPeriod[]>('fiscal-periods')
    ])
    years.value = yearRows
    periods.value = periodRows
    if (!form.fiscalYearId && years.value[0]) form.fiscalYearId = years.value[0].id
    if (!form.fiscalPeriodId && periods.value[0]) form.fiscalPeriodId = periods.value[0].id
    await loadRuns()
  } catch (error) {
    notify(error, 'error')
  } finally {
    loading.value = false
  }
}

async function loadRuns() {
  loading.value = true
  try {
    const params = new URLSearchParams({ page: String(page.value), pageSize: String(pageSize.value), status: statusFilter.value })
    runs.value = await api.get<FinalAccountsCloseRunList>(`period-close/runs?${params}`)
  } catch (error) {
    notify(error, 'error')
  } finally {
    loading.value = false
  }
}

async function previewClose() {
  previewing.value = true
  try {
    preview.value = await api.post<FinalAccountsClosePreview>('period-close/preview', closePayload())
  } catch (error) {
    notify(error, 'error')
  } finally {
    previewing.value = false
  }
}

async function commitClose() {
  saving.value = true
  try {
    const body: FinalAccountsCloseCommitRequest = { ...closePayload(), confirmAllGates: confirmAllGates.value, approvalNotes: approvalNotes.value || null }
    selected.value = await api.post<FinalAccountsCloseRun>('period-close/close', body)
    await loadRuns()
    notify('Close run committed.', 'success')
  } catch (error) {
    notify(error, 'error')
  } finally {
    saving.value = false
  }
}

async function selectRun(id: string) {
  selected.value = await api.get<FinalAccountsCloseRun>(`period-close/runs/${id}`)
  selectedTab.value = 'checklist'
}

async function reopenRun(id: string) {
  saving.value = true
  try {
    selected.value = await api.post<FinalAccountsCloseRun>(`period-close/runs/${id}/reopen`, { reason: 'Authorised reopen from Final Accounts closeout', confirmReopenImpact: true })
    await loadRuns()
    notify('Close run reopened.', 'success')
  } catch (error) {
    notify(error, 'error')
  } finally {
    saving.value = false
  }
}

function closePayload(): FinalAccountsCloseRequest {
  return {
    fiscalYearId: form.fiscalYearId,
    fiscalPeriodId: form.closeType === 'Period' ? form.fiscalPeriodId || null : null,
    closeType: form.closeType,
    closeDate: form.closeDate || null,
    lockPeriod: form.lockPeriod,
    transferCurrentYearResult: form.transferCurrentYearResult,
    generateOpeningJournal: form.generateOpeningJournal
  }
}

function previousPage() {
  if (page.value > 1) {
    page.value--
    loadRuns()
  }
}

function nextPage() {
  if (page.value < totalPages.value) {
    page.value++
    loadRuns()
  }
}

function today() {
  return new Date().toISOString().slice(0, 10)
}

function money(value: number) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(Number(value || 0))
}

function statusColor(status: string) {
  if (status === 'Closed') return 'success'
  if (status === 'Reopened') return 'warning'
  return 'neutral'
}

function gateColor(status: string) {
  if (status === 'Pass' || status === 'Prepared') return 'success'
  if (status === 'Block') return 'error'
  if (status === 'Warning') return 'warning'
  return 'neutral'
}

function notify(value: unknown, tone: Tone) {
  messageTone.value = tone
  message.value = value instanceof Error ? value.message : String(value || '')
}
</script>
