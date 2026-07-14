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
      <div class="grid gap-3 lg:grid-cols-[1fr_1fr_12rem_12rem_12rem_12rem_12rem_12rem]">
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
        <UFormField label="P&L View">
          <USelect v-model="filters.profitLossView" :items="profitLossViewItems" />
        </UFormField>
        <UFormField label="Rounding">
          <USelect v-model="filters.roundingUnit" :items="roundingItems" />
        </UFormField>
        <UFormField label="Entity">
          <USelect v-model="filters.entityType" :items="entityItems" />
        </UFormField>
        <UFormField label="Schedule">
          <USelect v-model="filters.schedule" :items="scheduleItems" />
        </UFormField>
      </div>
      <div class="mt-3 flex flex-wrap items-center gap-3">
        <USelectMenu v-model="filters.accountId" :items="accountSelectItems" value-key="value" class="w-full sm:w-80" />
        <UCheckbox v-model="filters.includeZeroBalances" label="Zero balances" />
        <UCheckbox v-model="filters.includeReversed" label="Reversed audit rows" />
        <UCheckbox v-model="filters.hideZeroStatementLines" label="Hide zero P&L lines" />
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

    <div v-else-if="activeTab === 'trial-balance'" class="space-y-4">
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

    <div v-else-if="activeTab === 'profit-loss'" class="space-y-4">
      <div class="final-accounts-grid">
        <UCard v-for="card in profitCards" :key="card.label" :ui="{ body: 'p-4' }">
          <p class="text-xs font-medium uppercase text-muted">{{ card.label }}</p>
          <p class="text-xl font-semibold text-highlighted">{{ card.value }}</p>
        </UCard>
      </div>

      <UCard v-if="profitLoss.mappingIssues.length" :ui="{ body: 'p-4' }">
        <div class="grid gap-2">
          <UAlert v-for="issue in profitLoss.mappingIssues" :key="`${issue.code}-${issue.message}`" color="warning" variant="subtle" :title="issue.code" :description="issue.message" />
        </div>
      </UCard>

      <UCard class="overflow-hidden" :ui="{ body: 'p-0' }">
        <div class="flex flex-wrap items-center gap-2 border-b border-default p-3">
          <UBadge color="neutral" variant="subtle">{{ profitLoss.template.templateCode }} {{ profitLoss.template.version }}</UBadge>
          <UBadge color="neutral" variant="subtle">{{ profitLoss.roundingUnit }}</UBadge>
          <UButton icon="i-lucide-file-spreadsheet" color="neutral" variant="soft" :loading="exporting" @click="exportProfitLoss('csv')">Excel</UButton>
          <UButton icon="i-lucide-file-text" color="neutral" variant="soft" :loading="exporting" @click="exportProfitLoss('pdf')">PDF</UButton>
        </div>

        <UTable v-if="profitLoss.view === 'Horizontal'" :data="profitLoss.horizontal" :columns="profitHorizontalColumns" :loading="loading">
          <template #current-cell="{ row }">{{ money(row.original.current) }}</template>
          <template #previous-cell="{ row }">{{ money(row.original.previous) }}</template>
          <template #variance-cell="{ row }">{{ money(row.original.variance) }}</template>
          <template #variancePercent-cell="{ row }">{{ percent(row.original.variancePercent) }}</template>
        </UTable>

        <UTable v-else :data="profitLoss.lines" :columns="profitColumns" :loading="loading">
          <template #label-cell="{ row }">
            <div>
              <p :class="row.original.nodeType === 'Formula' ? 'font-semibold text-highlighted' : 'text-highlighted'">{{ row.original.label }}</p>
              <p class="text-xs text-muted">{{ row.original.scheduleReference }} · {{ row.original.note }}</p>
            </div>
          </template>
          <template #current-cell="{ row }">{{ money(row.original.current) }}</template>
          <template #previous-cell="{ row }">{{ money(row.original.previous) }}</template>
          <template #variance-cell="{ row }">{{ money(row.original.variance) }}</template>
          <template #variancePercent-cell="{ row }">{{ percent(row.original.variancePercent) }}</template>
          <template #percentOfSales-cell="{ row }">{{ percent(row.original.percentOfSales) }}</template>
          <template #mappings-cell="{ row }">
            <UBadge color="neutral" variant="subtle">{{ row.original.mappings.length }}</UBadge>
          </template>
        </UTable>
      </UCard>
    </div>

    <div v-else-if="activeTab === 'balance-sheet'" class="space-y-4">
      <div class="final-accounts-grid">
        <UCard v-for="card in balanceCards" :key="card.label" :ui="{ body: 'p-4' }">
          <p class="text-xs font-medium uppercase text-muted">{{ card.label }}</p>
          <p class="text-xl font-semibold text-highlighted">{{ card.value }}</p>
        </UCard>
      </div>

      <UCard v-if="balanceSheet.diagnostics.length" :ui="{ body: 'p-4' }">
        <div class="grid gap-2">
          <UAlert v-for="issue in balanceSheet.diagnostics" :key="`${issue.code}-${issue.message}`" :color="issue.severity === 'Error' ? 'error' : 'warning'" variant="subtle" :title="issue.code" :description="issue.message" />
        </div>
      </UCard>

      <UCard class="overflow-hidden" :ui="{ body: 'p-0' }">
        <div class="flex flex-wrap items-center gap-2 border-b border-default p-3">
          <UBadge :color="balanceSheet.status === 'Balanced' ? 'success' : 'error'" variant="subtle">{{ balanceSheet.status }}</UBadge>
          <UBadge color="neutral" variant="subtle">{{ balanceSheet.entityType }}</UBadge>
          <UButton icon="i-lucide-file-spreadsheet" color="neutral" variant="soft" :loading="exporting" @click="exportBalanceSheet('csv')">Excel</UButton>
          <UButton icon="i-lucide-file-text" color="neutral" variant="soft" :loading="exporting" @click="exportBalanceSheet('pdf')">PDF</UButton>
        </div>
        <UTable :data="balanceSheet.lines" :columns="balanceColumns" :loading="loading">
          <template #label-cell="{ row }">
            <div>
              <p :class="row.original.key.startsWith('Total') ? 'font-semibold text-highlighted' : 'text-highlighted'">{{ row.original.label }}</p>
              <p class="text-xs text-muted">{{ row.original.classification }} · {{ row.original.note }}</p>
            </div>
          </template>
          <template #current-cell="{ row }">{{ money(row.original.current) }}</template>
          <template #previous-cell="{ row }">{{ money(row.original.previous) }}</template>
          <template #variance-cell="{ row }">{{ money(row.original.variance) }}</template>
          <template #mappings-cell="{ row }"><UBadge color="neutral" variant="subtle">{{ row.original.mappings.length }}</UBadge></template>
        </UTable>
      </UCard>
    </div>

    <div v-else-if="activeTab === 'cash-flow'" class="space-y-4">
      <div class="final-accounts-grid">
        <UCard v-for="card in cashFlowCards" :key="card.label" :ui="{ body: 'p-4' }">
          <p class="text-xs font-medium uppercase text-muted">{{ card.label }}</p>
          <p class="text-xl font-semibold text-highlighted">{{ card.value }}</p>
        </UCard>
      </div>

      <UCard class="overflow-hidden" :ui="{ body: 'p-0' }">
        <div class="flex flex-wrap items-center gap-2 border-b border-default p-3">
          <UBadge :color="cashFlow.status === 'Balanced' ? 'success' : 'error'" variant="subtle">{{ cashFlow.method }} · {{ cashFlow.status }}</UBadge>
          <UButton icon="i-lucide-file-spreadsheet" color="neutral" variant="soft" :loading="exporting" @click="exportCashFlow('csv')">Excel</UButton>
          <UButton icon="i-lucide-file-text" color="neutral" variant="soft" :loading="exporting" @click="exportCashFlow('pdf')">PDF</UButton>
        </div>
        <UTable :data="cashFlow.lines" :columns="cashFlowColumns" :loading="loading">
          <template #amount-cell="{ row }">{{ money(row.original.amount) }}</template>
        </UTable>
      </UCard>
    </div>

    <div v-else class="space-y-4">
      <div class="final-accounts-grid">
        <UCard :ui="{ body: 'p-4' }">
          <p class="text-xs font-medium uppercase text-muted">Schedule Total</p>
          <p class="text-xl font-semibold text-highlighted">{{ money(schedules.total) }}</p>
        </UCard>
        <UCard :ui="{ body: 'p-4' }">
          <p class="text-xs font-medium uppercase text-muted">Sections</p>
          <p class="text-xl font-semibold text-highlighted">{{ schedules.sections.length }}</p>
        </UCard>
      </div>

      <UCard class="overflow-hidden" :ui="{ body: 'p-0' }">
        <div class="flex flex-wrap items-center gap-2 border-b border-default p-3">
          <UBadge color="neutral" variant="subtle">{{ schedules.schedule }}</UBadge>
          <UButton icon="i-lucide-file-spreadsheet" color="neutral" variant="soft" :loading="exporting" @click="exportSchedules('csv')">Excel</UButton>
          <UButton icon="i-lucide-file-text" color="neutral" variant="soft" :loading="exporting" @click="exportSchedules('pdf')">PDF</UButton>
        </div>
        <div v-for="section in schedules.sections" :key="section.key" class="border-b border-default">
          <div class="flex items-center justify-between gap-3 bg-muted/30 px-3 py-2">
            <h2 class="text-sm font-semibold text-highlighted">{{ section.label }}</h2>
            <span class="text-sm font-medium text-highlighted">{{ money(section.total) }}</span>
          </div>
          <UTable :data="section.rows" :columns="scheduleColumns" :loading="loading">
            <template #balance-cell="{ row }">{{ money(row.original.balance) }}</template>
          </UTable>
        </div>
      </UCard>
    </div>
  </section>
</template>

<script setup lang="ts">
import {
  formatFinalAccountsDateOnly,
  type FinalAccountsAccount,
  type FinalAccountsBalanceSheetReport,
  type FinalAccountsCashFlowReport,
  type FinalAccountsGeneralLedgerReport,
  type FinalAccountsProfitLossReport,
  type FinalAccountsSchedulesReport,
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
  profitLossView: 'Vertical',
  roundingUnit: 'Ones',
  entityType: 'Proprietorship',
  schedule: 'All',
  includeZeroBalances: false,
  includeReversed: false,
  hideZeroStatementLines: false
})
const ledger = ref<FinalAccountsGeneralLedgerReport>(emptyLedger())
const trial = ref<FinalAccountsTrialBalanceReport>(emptyTrial())
const profitLoss = ref<FinalAccountsProfitLossReport>(emptyProfitLoss())
const balanceSheet = ref<FinalAccountsBalanceSheetReport>(emptyBalanceSheet())
const cashFlow = ref<FinalAccountsCashFlowReport>(emptyCashFlow())
const schedules = ref<FinalAccountsSchedulesReport>(emptySchedules())

const tabs = [
  { label: 'General Ledger', value: 'ledger', icon: 'i-lucide-book-open-check' },
  { label: 'Trial Balance', value: 'trial-balance', icon: 'i-lucide-scale' },
  { label: 'Profit & Loss', value: 'profit-loss', icon: 'i-lucide-chart-no-axes-combined' },
  { label: 'Balance Sheet', value: 'balance-sheet', icon: 'i-lucide-landmark' },
  { label: 'Cash Flow', value: 'cash-flow', icon: 'i-lucide-arrow-left-right' },
  { label: 'Schedules', value: 'schedules', icon: 'i-lucide-paperclip' }
]
const trialViewItems = ['Ledger', 'Group']
const comparisonItems = ['Monthly', 'Quarterly']
const profitLossViewItems = ['Vertical', 'Horizontal']
const roundingItems = ['Ones', 'Thousands', 'Lakhs']
const entityItems = ['Proprietorship', 'Partnership', 'Company']
const scheduleItems = ['All', 'DebtorAgeing', 'CreditorAgeing', 'Inventory', 'FixedAssets', 'CashBank', 'GstTds', 'Loans', 'Capital', 'NotesAttachments']
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
const profitCards = computed(() => [
  { label: 'Revenue', value: money(profitLoss.value.revenue) },
  { label: 'Gross Profit', value: money(profitLoss.value.grossProfit) },
  { label: 'EBITDA', value: money(profitLoss.value.ebitda) },
  { label: 'Profit After Tax', value: money(profitLoss.value.profitAfterTax) }
])
const balanceCards = computed(() => [
  { label: 'Assets', value: money(balanceSheet.value.totalAssets) },
  { label: 'Liabilities', value: money(balanceSheet.value.totalLiabilities) },
  { label: 'Equity', value: money(balanceSheet.value.totalEquity) },
  { label: 'Difference', value: money(balanceSheet.value.difference) }
])
const cashFlowCards = computed(() => [
  { label: 'Operating', value: money(cashFlow.value.operatingActivities) },
  { label: 'Investing', value: money(cashFlow.value.investingActivities) },
  { label: 'Financing', value: money(cashFlow.value.financingActivities) },
  { label: 'Closing Cash', value: money(cashFlow.value.closingCash) }
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
const profitColumns = [
  { accessorKey: 'label', header: 'Line' },
  { accessorKey: 'current', header: 'Current' },
  { accessorKey: 'previous', header: 'Previous' },
  { accessorKey: 'variance', header: 'Variance' },
  { accessorKey: 'variancePercent', header: 'Variance %' },
  { accessorKey: 'percentOfSales', header: '% Sales' },
  { accessorKey: 'mappings', header: 'Maps' }
]
const profitHorizontalColumns = [
  { accessorKey: 'metric', header: 'Metric' },
  { accessorKey: 'current', header: 'Current' },
  { accessorKey: 'previous', header: 'Previous' },
  { accessorKey: 'variance', header: 'Variance' },
  { accessorKey: 'variancePercent', header: 'Variance %' }
]
const balanceColumns = [
  { accessorKey: 'section', header: 'Section' },
  { accessorKey: 'label', header: 'Line' },
  { accessorKey: 'current', header: 'Current' },
  { accessorKey: 'previous', header: 'Previous' },
  { accessorKey: 'variance', header: 'Variance' },
  { accessorKey: 'mappings', header: 'Maps' }
]
const cashFlowColumns = [
  { accessorKey: 'section', header: 'Section' },
  { accessorKey: 'label', header: 'Line' },
  { accessorKey: 'amount', header: 'Amount' },
  { accessorKey: 'note', header: 'Note' }
]
const scheduleColumns = [
  { accessorKey: 'accountCode', header: 'Code' },
  { accessorKey: 'accountName', header: 'Account' },
  { accessorKey: 'ageBucket', header: 'Age' },
  { accessorKey: 'balance', header: 'Balance' },
  { accessorKey: 'note', header: 'Note' }
]

async function loadReports() {
  loading.value = true
  message.value = ''
  try {
    if (accounts.value.length === 0) {
      accounts.value = await api.get<FinalAccountsAccount[]>('accounts')
    }
    const [ledgerReport, trialReport, profitLossReport, balanceSheetReport, cashFlowReport, schedulesReport] = await Promise.all([
      api.get<FinalAccountsGeneralLedgerReport>(`reports/general-ledger?${ledgerQuery()}`),
      api.get<FinalAccountsTrialBalanceReport>(`reports/trial-balance?${trialQuery()}`),
      api.get<FinalAccountsProfitLossReport>(`reports/profit-loss?${profitLossQuery()}`),
      api.get<FinalAccountsBalanceSheetReport>(`reports/balance-sheet?${balanceSheetQuery()}`),
      api.get<FinalAccountsCashFlowReport>(`reports/cash-flow?${cashFlowQuery()}`),
      api.get<FinalAccountsSchedulesReport>(`reports/schedules?${schedulesQuery()}`)
    ])
    ledger.value = ledgerReport
    trial.value = trialReport
    profitLoss.value = profitLossReport
    balanceSheet.value = balanceSheetReport
    cashFlow.value = cashFlowReport
    schedules.value = schedulesReport
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

async function exportProfitLoss(format: 'csv' | 'pdf') {
  await download(`reports/profit-loss/export?${profitLossQuery()}&format=${format}`, `final-accounts-profit-loss.${format}`)
}

async function exportBalanceSheet(format: 'csv' | 'pdf') {
  await download(`reports/balance-sheet/export?${balanceSheetQuery()}&format=${format}`, `final-accounts-balance-sheet.${format}`)
}

async function exportCashFlow(format: 'csv' | 'pdf') {
  await download(`reports/cash-flow/export?${cashFlowQuery()}&format=${format}`, `final-accounts-cash-flow.${format}`)
}

async function exportSchedules(format: 'csv' | 'pdf') {
  await download(`reports/schedules/export?${schedulesQuery()}&format=${format}`, `final-accounts-schedules.${format}`)
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

function profitLossQuery() {
  const params = new URLSearchParams({
    view: filters.profitLossView,
    roundingUnit: filters.roundingUnit,
    hideZero: String(filters.hideZeroStatementLines)
  })
  if (filters.from) params.set('from', filters.from)
  if (filters.to) params.set('to', filters.to)
  return params.toString()
}

function balanceSheetQuery() {
  const params = new URLSearchParams({
    entityType: filters.entityType,
    roundingUnit: filters.roundingUnit,
    hideZero: String(filters.hideZeroStatementLines)
  })
  if (filters.to) params.set('asOf', filters.to)
  if (filters.from) params.set('previousAsOf', filters.from)
  return params.toString()
}

function cashFlowQuery() {
  const params = new URLSearchParams({ roundingUnit: filters.roundingUnit })
  if (filters.from) params.set('from', filters.from)
  if (filters.to) params.set('to', filters.to)
  return params.toString()
}

function schedulesQuery() {
  const params = new URLSearchParams({
    schedule: filters.schedule,
    includeZeroBalances: String(filters.includeZeroBalances)
  })
  if (filters.to) params.set('asOf', filters.to)
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

function percent(value: number | null | undefined) {
  if (value === null || value === undefined) return '-'
  return `${Number(value).toFixed(2)}%`
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

function emptyProfitLoss(): FinalAccountsProfitLossReport {
  return {
    template: {
      templateCode: 'GarmentRetailProfitLoss',
      version: 'v1',
      name: 'Garment Retail Profit And Loss',
      statementType: 'ProfitLoss',
      defaultView: 'Vertical',
      defaultRoundingUnit: 'Ones',
      hideZeroDefault: false,
      nodes: []
    },
    view: 'Vertical',
    roundingUnit: 'Ones',
    hideZero: false,
    revenue: 0,
    grossProfit: 0,
    ebitda: 0,
    profitBeforeTax: 0,
    profitAfterTax: 0,
    lines: [],
    horizontal: [],
    mappingIssues: []
  }
}

function emptyBalanceSheet(): FinalAccountsBalanceSheetReport {
  return {
    template: {
      templateCode: 'GarmentRetailBalanceSheetProprietorship',
      version: 'v1',
      name: 'Non-Corporate Balance Sheet',
      statementType: 'BalanceSheet',
      defaultView: 'Vertical',
      defaultRoundingUnit: 'Ones',
      hideZeroDefault: false,
      nodes: []
    },
    entityType: 'Proprietorship',
    roundingUnit: 'Ones',
    asOf: '',
    hideZero: false,
    totalAssets: 0,
    totalLiabilities: 0,
    totalEquity: 0,
    currentYearProfit: 0,
    difference: 0,
    status: 'Balanced',
    lines: [],
    diagnostics: []
  }
}

function emptyCashFlow(): FinalAccountsCashFlowReport {
  return {
    method: 'Indirect',
    roundingUnit: 'Ones',
    profitAfterTax: 0,
    nonCashAdjustments: 0,
    workingCapitalChanges: 0,
    operatingActivities: 0,
    investingActivities: 0,
    financingActivities: 0,
    netCashFlow: 0,
    openingCash: 0,
    closingCash: 0,
    reconciliationDifference: 0,
    status: 'Balanced',
    lines: [],
    diagnostics: []
  }
}

function emptySchedules(): FinalAccountsSchedulesReport {
  return {
    asOf: '',
    schedule: 'All',
    total: 0,
    sections: [],
    diagnostics: []
  }
}

onMounted(loadReports)
</script>
