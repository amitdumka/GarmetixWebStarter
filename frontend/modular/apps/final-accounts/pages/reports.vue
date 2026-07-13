<template>
  <section class="space-y-5">
    <div class="flex flex-wrap items-start justify-between gap-3">
      <div>
        <p class="garmetix-kicker"><UIcon name="i-lucide-scale" class="size-4" /> Final Accounts</p>
        <h1 class="garmetix-dashboard-title">Reports</h1>
      </div>
      <div class="flex flex-wrap items-center gap-2">
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="loadReports">Refresh</UButton>
        <UButton to="/general-ledger" icon="i-lucide-book-open-check" color="neutral" variant="soft">Journals</UButton>
        <UButton to="/chart-of-accounts" icon="i-lucide-list-tree" color="neutral" variant="soft">Chart</UButton>
      </div>
    </div>

    <UAlert v-if="message" :icon="messageIcon" :color="messageTone" variant="subtle" :title="messageTitle" :description="message" />

    <UCard :ui="{ body: 'p-4' }">
      <div class="grid gap-3 lg:grid-cols-[1fr_1fr_14rem_12rem]">
        <UFormField label="From">
          <UInput v-model="filters.from" type="date" icon="i-lucide-calendar" />
        </UFormField>
        <UFormField label="To">
          <UInput v-model="filters.to" type="date" icon="i-lucide-calendar-days" />
        </UFormField>
        <UFormField label="Trial View">
          <USelect v-model="filters.trialView" :items="trialViewItems" />
        </UFormField>
        <UFormField label="Comparison">
          <USelect v-model="filters.comparison" :items="comparisonItems" />
        </UFormField>
      </div>
      <div class="mt-3 flex flex-wrap items-center gap-3">
        <USelectMenu v-model="filters.accountId" :items="accountSelectItems" value-key="value" class="w-full sm:w-80" />
        <UCheckbox v-model="filters.includeZeroBalances" label="Zero balances" />
        <UCheckbox v-model="filters.includeReversed" label="Reversed audit rows" />
        <UButton icon="i-lucide-search" :loading="loading" @click="loadReports">Apply</UButton>
      </div>
    </UCard>

    <UTabs v-model="activeTab" :items="tabs" class="w-full max-w-lg" />

    <div v-if="activeTab === 'ledger'" class="space-y-4">
      <div class="final-accounts-grid">
        <UCard v-for="card in ledgerCards" :key="card.label" :ui="{ body: 'p-4' }">
          <p class="text-xs font-medium uppercase text-muted">{{ card.label }}</p>
          <p class="text-xl font-semibold text-highlighted">{{ card.value }}</p>
        </UCard>
      </div>

      <UCard class="overflow-hidden" :ui="{ body: 'p-0' }">
        <div class="flex flex-wrap items-center gap-2 border-b border-default p-3">
          <UButton icon="i-lucide-file-spreadsheet" color="neutral" variant="soft" :loading="exporting" @click="exportLedger('csv')">Excel</UButton>
          <UButton icon="i-lucide-file-text" color="neutral" variant="soft" :loading="exporting" @click="exportLedger('pdf')">PDF</UButton>
          <div class="ml-auto flex items-center gap-1">
            <UButton icon="i-lucide-chevron-left" color="neutral" variant="ghost" size="sm" :disabled="ledgerPage <= 1" @click="previousLedgerPage" />
            <UBadge color="neutral" variant="subtle">{{ ledgerPage }} / {{ ledgerTotalPages }}</UBadge>
            <UButton icon="i-lucide-chevron-right" color="neutral" variant="ghost" size="sm" :disabled="ledgerPage >= ledgerTotalPages" @click="nextLedgerPage" />
          </div>
        </div>
        <UTable :data="ledger.rows" :columns="ledgerColumns" :loading="loading">
          <template #onDate-cell="{ row }">{{ formatFinalAccountsDateOnly(row.original.onDate) }}</template>
          <template #entryNumber-cell="{ row }">
            <UButton icon="i-lucide-eye" color="neutral" variant="ghost" size="xs" @click="openJournal(row.original.journalEntryId)">
              {{ row.original.entryNumber }}
            </UButton>
          </template>
          <template #account-cell="{ row }">
            <span class="font-mono text-sm">{{ row.original.accountCode }}</span>
            <span class="ml-2">{{ row.original.accountName }}</span>
          </template>
          <template #debit-cell="{ row }">{{ money(row.original.debit) }}</template>
          <template #credit-cell="{ row }">{{ money(row.original.credit) }}</template>
          <template #runningBalance-cell="{ row }">
            {{ money(row.original.runningBalance) }} {{ row.original.balanceType }}
          </template>
          <template #flags-cell="{ row }">
            <div class="flex gap-1">
              <UBadge v-if="row.original.isReversal" color="warning" variant="subtle">Reversal</UBadge>
              <UBadge v-if="row.original.isReversed" color="warning" variant="subtle">Reversed</UBadge>
              <UBadge v-if="row.original.sourceId" color="neutral" variant="subtle">{{ row.original.sourceType }}</UBadge>
            </div>
          </template>
        </UTable>
      </UCard>
    </div>

    <div v-else class="space-y-4">
      <div class="final-accounts-grid">
        <UCard v-for="card in trialCards" :key="card.label" :ui="{ body: 'p-4' }">
          <p class="text-xs font-medium uppercase text-muted">{{ card.label }}</p>
          <p class="text-xl font-semibold text-highlighted">{{ card.value }}</p>
        </UCard>
      </div>

      <UCard v-if="trial.diagnostics.length" :ui="{ body: 'p-4' }">
        <div class="grid gap-2">
          <UAlert v-for="issue in trial.diagnostics" :key="`${issue.code}-${issue.message}`" :color="issue.severity === 'Error' ? 'error' : 'warning'" variant="subtle" :title="issue.code" :description="issue.message" />
        </div>
      </UCard>

      <UCard class="overflow-hidden" :ui="{ body: 'p-0' }">
        <div class="flex flex-wrap items-center gap-2 border-b border-default p-3">
          <UBadge :color="trial.status === 'Balanced' ? 'success' : 'error'" variant="subtle">{{ trial.status }}</UBadge>
          <UButton icon="i-lucide-file-spreadsheet" color="neutral" variant="soft" :loading="exporting" @click="exportTrialBalance('csv')">Excel</UButton>
          <UButton icon="i-lucide-file-text" color="neutral" variant="soft" :loading="exporting" @click="exportTrialBalance('pdf')">PDF</UButton>
        </div>
        <UTable :data="trial.rows" :columns="trialColumns" :loading="loading">
          <template #accountName-cell="{ row }">
            <span v-if="row.original.accountCode" class="font-mono text-sm">{{ row.original.accountCode }}</span>
            <span class="ml-2">{{ row.original.accountName }}</span>
          </template>
          <template #opening-cell="{ row }">{{ pair(row.original.openingDebit, row.original.openingCredit) }}</template>
          <template #period-cell="{ row }">{{ pair(row.original.periodDebit, row.original.periodCredit) }}</template>
          <template #closing-cell="{ row }">{{ pair(row.original.closingDebit, row.original.closingCredit) }}</template>
        </UTable>
      </UCard>

      <UCard class="overflow-hidden" :ui="{ body: 'p-0' }">
        <div class="border-b border-default p-3">
          <h2 class="text-base font-semibold text-highlighted">Comparison</h2>
        </div>
        <UTable :data="trial.comparisons" :columns="comparisonColumns">
          <template #from-cell="{ row }">{{ formatFinalAccountsDateOnly(row.original.from) }}</template>
          <template #to-cell="{ row }">{{ formatFinalAccountsDateOnly(row.original.to) }}</template>
          <template #debit-cell="{ row }">{{ money(row.original.debit) }}</template>
          <template #credit-cell="{ row }">{{ money(row.original.credit) }}</template>
          <template #difference-cell="{ row }">{{ money(row.original.difference) }}</template>
          <template #status-cell="{ row }">
            <UBadge :color="row.original.status === 'Balanced' ? 'success' : 'error'" variant="subtle">{{ row.original.status }}</UBadge>
          </template>
        </UTable>
      </UCard>
    </div>
  </section>
</template>

<script setup lang="ts">
import {
  formatFinalAccountsDateOnly,
  type FinalAccountsAccount,
  type FinalAccountsGeneralLedgerReport,
  type FinalAccountsTrialBalanceReport,
  useFinalAccountsApiClient
} from '../utils/final-accounts-api'

useHead({ title: 'Final Accounts Reports' })

type Tone = 'success' | 'warning' | 'error' | 'neutral'
type SelectItem = { label: string; value: string }

const api = useFinalAccountsApiClient()
const loading = ref(false)
const exporting = ref(false)
const message = ref('')
const messageTone = ref<Tone>('neutral')
const accounts = ref<FinalAccountsAccount[]>([])
const activeTab = ref('ledger')
const ledgerPage = ref(1)
const ledgerPageSize = ref(50)
const filters = reactive({
  from: '',
  to: '',
  accountId: '',
  trialView: 'Ledger',
  comparison: 'Monthly',
  includeZeroBalances: false,
  includeReversed: false
})
const ledger = ref<FinalAccountsGeneralLedgerReport>(emptyLedger())
const trial = ref<FinalAccountsTrialBalanceReport>(emptyTrial())

const tabs = [
  { label: 'General Ledger', value: 'ledger', icon: 'i-lucide-book-open-check' },
  { label: 'Trial Balance', value: 'trial-balance', icon: 'i-lucide-scale' }
]
const trialViewItems = ['Ledger', 'Group']
const comparisonItems = ['Monthly', 'Quarterly']
const messageTitle = computed(() => messageTone.value === 'error' ? 'Report unavailable' : 'Reports')
const messageIcon = computed(() => messageTone.value === 'error' ? 'i-lucide-circle-alert' : 'i-lucide-info')
const accountSelectItems = computed<SelectItem[]>(() => [
  { label: 'All active accounts', value: '' },
  ...accounts.value.map(item => ({ label: `${item.code} - ${item.name}`, value: item.id }))
])
const ledgerTotalPages = computed(() => Math.max(1, Math.ceil(ledger.value.totalCount / ledgerPageSize.value)))
const ledgerCards = computed(() => [
  { label: 'Opening Dr / Cr', value: pair(ledger.value.openingDebit, ledger.value.openingCredit) },
  { label: 'Period Dr / Cr', value: pair(ledger.value.periodDebit, ledger.value.periodCredit) },
  { label: 'Closing Dr / Cr', value: pair(ledger.value.closingDebit, ledger.value.closingCredit) },
  { label: 'Rows', value: String(ledger.value.totalCount) }
])
const trialCards = computed(() => [
  { label: 'Opening Dr / Cr', value: pair(trial.value.totalOpeningDebit, trial.value.totalOpeningCredit) },
  { label: 'Period Dr / Cr', value: pair(trial.value.totalPeriodDebit, trial.value.totalPeriodCredit) },
  { label: 'Closing Dr / Cr', value: pair(trial.value.totalClosingDebit, trial.value.totalClosingCredit) },
  { label: 'Difference', value: money(trial.value.difference) }
])

const ledgerColumns = [
  { accessorKey: 'onDate', header: 'Date' },
  { accessorKey: 'entryNumber', header: 'Journal' },
  { accessorKey: 'account', header: 'Account' },
  { accessorKey: 'referenceNumber', header: 'Reference' },
  { accessorKey: 'debit', header: 'Debit' },
  { accessorKey: 'credit', header: 'Credit' },
  { accessorKey: 'runningBalance', header: 'Running' },
  { accessorKey: 'flags', header: '' }
]
const trialColumns = [
  { accessorKey: 'groupName', header: 'Group' },
  { accessorKey: 'accountName', header: 'Ledger' },
  { accessorKey: 'opening', header: 'Opening' },
  { accessorKey: 'period', header: 'Period' },
  { accessorKey: 'closing', header: 'Closing' },
  { accessorKey: 'balanceType', header: 'Type' }
]
const comparisonColumns = [
  { accessorKey: 'periodKey', header: 'Period' },
  { accessorKey: 'from', header: 'From' },
  { accessorKey: 'to', header: 'To' },
  { accessorKey: 'debit', header: 'Debit' },
  { accessorKey: 'credit', header: 'Credit' },
  { accessorKey: 'difference', header: 'Difference' },
  { accessorKey: 'status', header: 'Status' }
]

async function loadReports() {
  loading.value = true
  message.value = ''
  try {
    if (accounts.value.length === 0) {
      accounts.value = await api.get<FinalAccountsAccount[]>('accounts')
    }
    const [ledgerReport, trialReport] = await Promise.all([
      api.get<FinalAccountsGeneralLedgerReport>(`reports/general-ledger?${ledgerQuery()}`),
      api.get<FinalAccountsTrialBalanceReport>(`reports/trial-balance?${trialQuery()}`)
    ])
    ledger.value = ledgerReport
    trial.value = trialReport
  } catch (err) {
    showError(err)
  } finally {
    loading.value = false
  }
}

async function exportLedger(format: 'csv' | 'pdf') {
  await download(`reports/general-ledger/export?${ledgerQuery()}&format=${format}`, `final-accounts-general-ledger.${format}`)
}

async function exportTrialBalance(format: 'csv' | 'pdf') {
  await download(`reports/trial-balance/export?${trialQuery()}&format=${format}`, `final-accounts-trial-balance.${format}`)
}

async function download(path: string, fileName: string) {
  exporting.value = true
  message.value = ''
  try {
    await api.download(path, fileName)
  } catch (err) {
    showError(err)
  } finally {
    exporting.value = false
  }
}

function ledgerQuery() {
  const params = new URLSearchParams({
    page: String(ledgerPage.value),
    pageSize: String(ledgerPageSize.value),
    includeReversed: String(filters.includeReversed)
  })
  if (filters.from) params.set('from', filters.from)
  if (filters.to) params.set('to', filters.to)
  if (filters.accountId) params.set('accountId', filters.accountId)
  return params.toString()
}

function trialQuery() {
  const params = new URLSearchParams({
    view: filters.trialView,
    comparison: filters.comparison,
    includeZeroBalances: String(filters.includeZeroBalances)
  })
  if (filters.from) params.set('from', filters.from)
  if (filters.to) params.set('to', filters.to)
  return params.toString()
}

function previousLedgerPage() {
  if (ledgerPage.value <= 1) return
  ledgerPage.value--
  loadReports()
}

function nextLedgerPage() {
  if (ledgerPage.value >= ledgerTotalPages.value) return
  ledgerPage.value++
  loadReports()
}

function openJournal(id: string) {
  navigateTo(`/general-ledger?journalId=${id}`)
}

function pair(debit: number, credit: number) {
  return `${money(debit)} / ${money(credit)}`
}

function money(value: number) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(Number(value) || 0)
}

function showError(err: unknown) {
  messageTone.value = 'error'
  message.value = err instanceof Error ? err.message : 'Reports could not be loaded.'
}

function emptyLedger(): FinalAccountsGeneralLedgerReport {
  return {
    page: 1,
    pageSize: 50,
    totalCount: 0,
    openingDebit: 0,
    openingCredit: 0,
    periodDebit: 0,
    periodCredit: 0,
    closingDebit: 0,
    closingCredit: 0,
    rows: [],
    issues: []
  }
}

function emptyTrial(): FinalAccountsTrialBalanceReport {
  return {
    view: 'Ledger',
    comparison: 'Monthly',
    includeZeroBalances: false,
    totalOpeningDebit: 0,
    totalOpeningCredit: 0,
    totalPeriodDebit: 0,
    totalPeriodCredit: 0,
    totalClosingDebit: 0,
    totalClosingCredit: 0,
    difference: 0,
    status: 'Balanced',
    rows: [],
    comparisons: [],
    diagnostics: []
  }
}

onMounted(loadReports)
</script>
