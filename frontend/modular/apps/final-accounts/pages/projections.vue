<template>
  <section class="space-y-5">
    <div class="flex flex-wrap items-start justify-between gap-3">
      <div>
        <p class="garmetix-kicker"><UIcon name="i-lucide-trending-up" class="size-4" /> Final Accounts</p>
        <h1 class="garmetix-dashboard-title">Projection Engine</h1>
      </div>
      <div class="flex flex-wrap items-center gap-2">
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="loadAll">Refresh</UButton>
        <UButton to="/reports" icon="i-lucide-scale" color="neutral" variant="soft">Reports</UButton>
        <UButton to="/closeout" icon="i-lucide-lock-keyhole" color="neutral" variant="soft">Closeout</UButton>
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

    <div class="grid gap-4 2xl:grid-cols-[minmax(28rem,34rem)_1fr]">
      <UCard :ui="{ body: 'p-5' }">
        <form class="space-y-4" @submit.prevent="saveScenario">
          <div class="flex items-center justify-between gap-3">
            <h2 class="text-base font-semibold text-highlighted">{{ editingId ? 'Edit Scenario' : 'New Scenario' }}</h2>
            <UButton type="button" icon="i-lucide-eraser" color="neutral" variant="ghost" size="sm" @click="resetForm">Clear</UButton>
          </div>

          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Scenario type" name="scenarioType">
              <USelect v-model="form.scenarioType" :items="scenarioTypes" class="w-full" />
            </UFormField>
            <UFormField label="Horizon" name="horizonMonths">
              <USelect v-model.number="form.horizonMonths" :items="horizonItems" class="w-full" />
            </UFormField>
          </div>

          <UFormField label="Name" name="name">
            <UInput v-model="form.name" icon="i-lucide-file-chart-column" required />
          </UFormField>

          <UFormField label="Description" name="description">
            <UTextarea v-model="form.description" :rows="2" />
          </UFormField>

          <div class="grid gap-3 sm:grid-cols-3">
            <UFormField label="Start" name="projectionStart">
              <UInput v-model="form.projectionStart" type="date" icon="i-lucide-calendar" required />
            </UFormField>
            <UFormField label="Baseline from" name="baselineFrom">
              <UInput v-model="form.baselineFrom" type="date" icon="i-lucide-calendar-days" />
            </UFormField>
            <UFormField label="Baseline to" name="baselineTo">
              <UInput v-model="form.baselineTo" type="date" icon="i-lucide-calendar-days" />
            </UFormField>
          </div>

          <div class="grid gap-2 sm:grid-cols-2">
            <UButton type="button" icon="i-lucide-database-zap" color="neutral" variant="soft" :loading="importing" block @click="importActuals">Import Actuals</UButton>
            <UButton type="button" icon="i-lucide-copy-plus" color="neutral" variant="soft" :disabled="!selectedScenario" block @click="cloneSelected">Clone Selected</UButton>
          </div>

          <div class="space-y-3">
            <h3 class="text-sm font-semibold text-highlighted">Baseline</h3>
            <div class="grid gap-2 sm:grid-cols-2">
              <UInput v-model.number="form.baseline.monthlyRevenue" type="number" step="0.01" placeholder="Monthly revenue" />
              <UInput v-model.number="form.baseline.monthlyGrossProfit" type="number" step="0.01" placeholder="Monthly gross profit" />
              <UInput v-model.number="form.baseline.monthlyProfitAfterTax" type="number" step="0.01" placeholder="Monthly PAT" />
              <UInput v-model.number="form.baseline.cash" type="number" step="0.01" placeholder="Cash" />
              <UInput v-model.number="form.baseline.inventory" type="number" step="0.01" placeholder="Inventory" />
              <UInput v-model.number="form.baseline.debtors" type="number" step="0.01" placeholder="Debtors" />
              <UInput v-model.number="form.baseline.creditors" type="number" step="0.01" placeholder="Creditors" />
              <UInput v-model.number="form.baseline.fixedAssets" type="number" step="0.01" placeholder="Fixed assets" />
              <UInput v-model.number="form.baseline.debt" type="number" step="0.01" placeholder="Debt" />
              <UInput v-model.number="form.baseline.capital" type="number" step="0.01" placeholder="Capital" />
            </div>
          </div>

          <div class="space-y-3">
            <h3 class="text-sm font-semibold text-highlighted">Assumptions</h3>
            <div class="grid gap-2 sm:grid-cols-2">
              <UInput v-model.number="form.assumptions.revenueGrowthPercent" type="number" step="0.01" placeholder="Revenue growth %" />
              <UInput v-model.number="form.assumptions.grossMarginPercent" type="number" step="0.01" placeholder="Gross margin %" />
              <UInput v-model.number="form.assumptions.returnsDiscountPercent" type="number" step="0.01" placeholder="Returns/discount %" />
              <UInput v-model.number="form.assumptions.purchaseInflationPercent" type="number" step="0.01" placeholder="Purchase inflation %" />
              <UInput v-model.number="form.assumptions.inventoryDays" type="number" step="0.01" placeholder="Inventory days" />
              <UInput v-model.number="form.assumptions.debtorDays" type="number" step="0.01" placeholder="Debtor days" />
              <UInput v-model.number="form.assumptions.creditorDays" type="number" step="0.01" placeholder="Creditor days" />
              <UInput v-model.number="form.assumptions.employeeCostMonthly" type="number" step="0.01" placeholder="Employee cost" />
              <UInput v-model.number="form.assumptions.rentExpenseMonthly" type="number" step="0.01" placeholder="Rent expense" />
              <UInput v-model.number="form.assumptions.capexMonthly" type="number" step="0.01" placeholder="Capex" />
              <UInput v-model.number="form.assumptions.debtRepaymentMonthly" type="number" step="0.01" placeholder="Debt repayment" />
              <UInput v-model.number="form.assumptions.minimumCash" type="number" step="0.01" placeholder="Minimum cash" />
            </div>
            <UTextarea v-model="seasonalityText" :rows="2" placeholder="12 seasonality factors, comma separated" />
          </div>

          <UButton type="submit" icon="i-lucide-save" :loading="saving" block>{{ editingId ? 'Save Projection' : 'Create Projection' }}</UButton>
        </form>
      </UCard>

      <div class="space-y-4">
        <UCard class="overflow-hidden" :ui="{ body: 'p-0' }">
          <div class="flex flex-wrap items-center gap-2 border-b border-default p-3">
            <USelect v-model="statusFilter" :items="statusItems" class="w-full sm:w-40" />
            <USelect v-model="typeFilter" :items="typeItems" class="w-full sm:w-40" />
            <UButton icon="i-lucide-search" color="neutral" variant="soft" :loading="loading" @click="loadScenarios">Search</UButton>
            <div class="ml-auto flex items-center gap-1">
              <UButton icon="i-lucide-chevron-left" color="neutral" variant="ghost" size="sm" :disabled="page <= 1" @click="previousPage" />
              <UBadge color="neutral" variant="subtle">{{ page }} / {{ totalPages }}</UBadge>
              <UButton icon="i-lucide-chevron-right" color="neutral" variant="ghost" size="sm" :disabled="page >= totalPages" @click="nextPage" />
            </div>
          </div>

          <UTable :data="scenarios.rows" :columns="scenarioColumns" :loading="loading" class="w-full">
            <template #select-cell="{ row }">
              <UCheckbox :model-value="compareIds.includes(row.original.id)" @update:model-value="toggleCompare(row.original.id)" />
            </template>
            <template #scenarioNumber-cell="{ row }">
              <button class="font-mono text-sm text-primary hover:underline" type="button" @click="selectScenario(row.original.id)">{{ row.original.scenarioNumber }}</button>
            </template>
            <template #status-cell="{ row }">
              <UBadge :color="statusColor(row.original.status)" variant="subtle">{{ row.original.status }}</UBadge>
            </template>
            <template #totalRevenue-cell="{ row }">
              {{ money(row.original.totalRevenue) }}
            </template>
            <template #closingCash-cell="{ row }">
              {{ money(row.original.closingCash) }}
            </template>
            <template #actions-cell="{ row }">
              <div class="flex justify-end gap-1">
                <UTooltip text="View">
                  <UButton icon="i-lucide-eye" color="neutral" variant="ghost" size="sm" @click="selectScenario(row.original.id)" />
                </UTooltip>
                <UTooltip text="Submit">
                  <UButton icon="i-lucide-send" color="neutral" variant="ghost" size="sm" :disabled="row.original.status !== 'Draft'" @click="move(row.original.id, 'submit')" />
                </UTooltip>
                <UTooltip text="Approve">
                  <UButton icon="i-lucide-badge-check" color="success" variant="ghost" size="sm" :disabled="row.original.status !== 'Submitted'" @click="move(row.original.id, 'approve')" />
                </UTooltip>
                <UTooltip text="Archive">
                  <UButton icon="i-lucide-archive" color="warning" variant="ghost" size="sm" :disabled="row.original.status === 'Archived'" @click="move(row.original.id, 'archive')" />
                </UTooltip>
              </div>
            </template>
          </UTable>
        </UCard>

        <UCard v-if="selectedScenario" :ui="{ body: 'p-4' }">
          <div class="flex flex-wrap items-center justify-between gap-3">
            <div>
              <p class="font-mono text-sm text-muted">{{ selectedScenario.scenarioNumber }}</p>
              <h2 class="text-lg font-semibold text-highlighted">{{ selectedScenario.name }}</h2>
            </div>
            <div class="flex gap-2">
              <UButton icon="i-lucide-pencil" color="neutral" variant="soft" @click="editSelected">Edit</UButton>
              <UButton icon="i-lucide-download" color="neutral" variant="soft" @click="exportSelected">Export</UButton>
            </div>
          </div>

          <div class="mt-4 grid gap-3 md:grid-cols-5">
            <UCard v-for="card in selectedCards" :key="card.label" :ui="{ body: 'p-3' }">
              <p class="text-xs uppercase text-muted">{{ card.label }}</p>
              <p class="text-lg font-semibold text-highlighted">{{ card.value }}</p>
            </UCard>
          </div>

          <div class="mt-4 overflow-x-auto">
            <table class="min-w-full text-sm">
              <thead class="border-b border-default text-left text-xs uppercase text-muted">
                <tr>
                  <th class="px-3 py-2">Month</th>
                  <th class="px-3 py-2 text-right">Revenue</th>
                  <th class="px-3 py-2 text-right">PAT</th>
                  <th class="px-3 py-2 text-right">Cash</th>
                  <th class="px-3 py-2 text-right">WCR</th>
                  <th class="px-3 py-2 text-right">Balance diff</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="month in selectedScenario.months" :key="month.monthNumber" class="border-b border-default/60">
                  <td class="px-3 py-2">{{ formatMonth(month.monthStart) }}</td>
                  <td class="px-3 py-2 text-right">{{ money(month.revenue) }}</td>
                  <td class="px-3 py-2 text-right">{{ money(month.profitAfterTax) }}</td>
                  <td class="px-3 py-2 text-right">{{ money(month.cashBalance) }}</td>
                  <td class="px-3 py-2 text-right">{{ money(month.workingCapitalRequirement) }}</td>
                  <td class="px-3 py-2 text-right">{{ money(month.balanceDifference) }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </UCard>

        <UCard :ui="{ body: 'p-4' }">
          <div class="flex flex-wrap items-center justify-between gap-3">
            <h2 class="text-base font-semibold text-highlighted">Scenario Comparison</h2>
            <UButton icon="i-lucide-git-compare" color="neutral" variant="soft" :loading="comparing" :disabled="compareIds.length < 2" @click="compareScenarios">Compare</UButton>
          </div>
          <UTable :data="comparison?.rows ?? []" :columns="comparisonColumns" class="mt-3 w-full">
            <template #totalRevenue-cell="{ row }">{{ money(row.original.totalRevenue) }}</template>
            <template #profitAfterTax-cell="{ row }">{{ money(row.original.profitAfterTax) }}</template>
            <template #closingCash-cell="{ row }">{{ money(row.original.closingCash) }}</template>
          </UTable>
        </UCard>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import {
  formatFinalAccountsDateOnly,
  type FinalAccountsProjectionBaseline,
  type FinalAccountsProjectionComparison,
  type FinalAccountsProjectionScenario,
  type FinalAccountsProjectionScenarioList,
  type FinalAccountsProjectionScenarioPayload,
  useFinalAccountsApiClient
} from '../utils/final-accounts-api'

const api = useFinalAccountsApiClient()
const loading = ref(false)
const saving = ref(false)
const importing = ref(false)
const comparing = ref(false)
const message = ref('')
const messageTone = ref<'success' | 'warning' | 'error' | 'info'>('info')
const page = ref(1)
const pageSize = 10
const statusFilter = ref('All')
const typeFilter = ref('All')
const editingId = ref<string | null>(null)
const selectedScenario = ref<FinalAccountsProjectionScenario | null>(null)
const compareIds = ref<string[]>([])
const comparison = ref<FinalAccountsProjectionComparison | null>(null)
const seasonalityText = ref('1,1,1,1,1,1,1,1,1,1,1,1')

const today = new Date()
const defaultStart = `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, '0')}-01`

const form = reactive<FinalAccountsProjectionScenarioPayload>({
  name: 'Base 5 Year Projection',
  description: '',
  scenarioType: 'Base',
  baselineSource: 'Manual',
  baselineFrom: '',
  baselineTo: '',
  projectionStart: defaultStart,
  horizonMonths: 60,
  baseline: {
    monthlyRevenue: 100000,
    monthlyGrossProfit: 42000,
    monthlyProfitAfterTax: 12000,
    cash: 250000,
    inventory: 180000,
    debtors: 90000,
    creditors: 75000,
    fixedAssets: 600000,
    debt: 200000,
    capital: 845000
  },
  assumptions: {
    revenueGrowthPercent: 12,
    seasonalityFactors: Array(12).fill(1),
    newStoreMonthlyRevenue: 0,
    averageBillValue: 0,
    customerCountGrowthPercent: 5,
    returnsDiscountPercent: 3,
    grossMarginPercent: 42,
    purchaseInflationPercent: 4,
    inventoryDays: 45,
    debtorDays: 28,
    creditorDays: 35,
    employeeCostMonthly: 18000,
    salaryGrowthPercent: 8,
    rentExpenseMonthly: 15000,
    expenseEscalationPercent: 6,
    capexMonthly: 10000,
    depreciationRatePercent: 10,
    debtOpening: 200000,
    interestRatePercent: 12,
    debtRepaymentMonthly: 5000,
    capitalInjectionMonthly: 0,
    drawingsMonthly: 3000,
    taxRatePercent: 25,
    minimumCash: 50000,
    notes: ''
  }
})

const scenarios = ref<FinalAccountsProjectionScenarioList>({ page: 1, pageSize, totalCount: 0, rows: [] })
const scenarioTypes = ['Conservative', 'Base', 'Optimistic', 'Custom']
const scenarioTypeValues = ['All', ...scenarioTypes]
const statuses = ['All', 'Draft', 'Submitted', 'Approved', 'Archived']
const statusItems = statuses
const typeItems = scenarioTypeValues
const horizonItems = [
  { label: '1 year', value: 12 },
  { label: '2 years', value: 24 },
  { label: '3 years', value: 36 },
  { label: '5 years', value: 60 }
]

const scenarioColumns = [
  { accessorKey: 'select', header: '' },
  { accessorKey: 'scenarioNumber', header: 'Scenario' },
  { accessorKey: 'name', header: 'Name' },
  { accessorKey: 'scenarioType', header: 'Type' },
  { accessorKey: 'status', header: 'Status' },
  { accessorKey: 'totalRevenue', header: 'Revenue' },
  { accessorKey: 'closingCash', header: 'Cash' },
  { accessorKey: 'actions', header: '' }
]

const comparisonColumns = [
  { accessorKey: 'scenarioNumber', header: 'Scenario' },
  { accessorKey: 'name', header: 'Name' },
  { accessorKey: 'totalRevenue', header: 'Revenue' },
  { accessorKey: 'profitAfterTax', header: 'PAT' },
  { accessorKey: 'closingCash', header: 'Cash' },
  { accessorKey: 'maxBalanceDifference', header: 'Max diff' }
]

const totalPages = computed(() => Math.max(1, Math.ceil(scenarios.value.totalCount / pageSize)))
const messageTitle = computed(() => messageTone.value === 'success' ? 'Projection Updated' : messageTone.value === 'error' ? 'Projection Error' : 'Projection Notice')
const messageIcon = computed(() => messageTone.value === 'success' ? 'i-lucide-circle-check' : messageTone.value === 'error' ? 'i-lucide-circle-alert' : 'i-lucide-info')

const metricCards = computed(() => [
  { label: 'Scenarios', value: String(scenarios.value.totalCount), icon: 'i-lucide-file-chart-column' },
  { label: 'Approved', value: String(scenarios.value.rows.filter(item => item.status === 'Approved').length), icon: 'i-lucide-badge-check' },
  { label: 'Selected', value: selectedScenario.value?.scenarioNumber ?? '-', icon: 'i-lucide-eye' },
  { label: 'Compare', value: String(compareIds.value.length), icon: 'i-lucide-git-compare' }
])

const selectedCards = computed(() => {
  const summary = selectedScenario.value?.summary
  if (!summary) return []
  return [
    { label: 'Revenue', value: money(summary.totalRevenue) },
    { label: 'PAT', value: money(summary.totalProfitAfterTax) },
    { label: 'Cash', value: money(summary.closingCash) },
    { label: 'WCR', value: money(summary.closingWorkingCapitalRequirement) },
    { label: 'Balance', value: summary.balanceStatus }
  ]
})

onMounted(loadAll)

async function loadAll() {
  await loadScenarios()
}

async function loadScenarios() {
  loading.value = true
  try {
    const params = new URLSearchParams({ page: String(page.value), pageSize: String(pageSize) })
    if (statusFilter.value !== 'All') params.set('status', statusFilter.value)
    if (typeFilter.value !== 'All') params.set('scenarioType', typeFilter.value)
    scenarios.value = await api.get<FinalAccountsProjectionScenarioList>(`projections/scenarios?${params}`)
    if (selectedScenario.value && !scenarios.value.rows.some(item => item.id === selectedScenario.value?.id)) selectedScenario.value = null
  } catch (error) {
    showError(error)
  } finally {
    loading.value = false
  }
}

async function selectScenario(id: string) {
  loading.value = true
  try {
    selectedScenario.value = await api.get<FinalAccountsProjectionScenario>(`projections/scenarios/${id}`)
  } catch (error) {
    showError(error)
  } finally {
    loading.value = false
  }
}

async function saveScenario() {
  saving.value = true
  try {
    form.assumptions.seasonalityFactors = seasonalityText.value.split(',').map(value => Number(value.trim())).filter(value => Number.isFinite(value))
    const saved = editingId.value
      ? await api.put<FinalAccountsProjectionScenario>(`projections/scenarios/${editingId.value}`, form)
      : await api.post<FinalAccountsProjectionScenario>('projections/scenarios', form)
    selectedScenario.value = saved
    editingId.value = saved.id
    showSuccess('Projection scenario saved.')
    await loadScenarios()
  } catch (error) {
    showError(error)
  } finally {
    saving.value = false
  }
}

async function importActuals() {
  importing.value = true
  try {
    const baseline = await api.post<FinalAccountsProjectionBaseline>('projections/baseline/actuals', {
      baselineFrom: undefined,
      companyId: form.companyId,
      storeGroupId: form.storeGroupId,
      storeId: form.storeId,
      from: form.baselineFrom,
      to: form.baselineTo,
      entityType: 'Proprietorship'
    })
    form.baseline = baseline
    form.baselineSource = 'Actuals'
    showSuccess('Actual baseline imported.')
  } catch (error) {
    showError(error)
  } finally {
    importing.value = false
  }
}

async function move(id: string, action: 'submit' | 'approve' | 'archive') {
  try {
    const saved = await api.post<FinalAccountsProjectionScenario>(`projections/scenarios/${id}/${action}`, { notes: `${action} projection` })
    selectedScenario.value = saved
    showSuccess(`Projection ${action} completed.`)
    await loadScenarios()
  } catch (error) {
    showError(error)
  }
}

async function cloneSelected() {
  if (!selectedScenario.value) return
  try {
    const saved = await api.post<FinalAccountsProjectionScenario>(`projections/scenarios/${selectedScenario.value.id}/clone`, {
      name: `${selectedScenario.value.name} Copy`,
      description: selectedScenario.value.description,
      scenarioType: selectedScenario.value.scenarioType
    })
    selectedScenario.value = saved
    showSuccess('Projection scenario cloned.')
    await loadScenarios()
  } catch (error) {
    showError(error)
  }
}

async function compareScenarios() {
  comparing.value = true
  try {
    comparison.value = await api.post<FinalAccountsProjectionComparison>('projections/compare', { scenarioIds: compareIds.value })
  } catch (error) {
    showError(error)
  } finally {
    comparing.value = false
  }
}

async function exportSelected() {
  if (!selectedScenario.value) return
  await api.download(`projections/scenarios/${selectedScenario.value.id}/export?format=csv`, `${selectedScenario.value.scenarioNumber}.csv`)
}

function editSelected() {
  if (!selectedScenario.value) return
  editingId.value = selectedScenario.value.id
  form.name = selectedScenario.value.name
  form.description = selectedScenario.value.description ?? ''
  form.scenarioType = selectedScenario.value.scenarioType
  form.baselineSource = selectedScenario.value.baselineSource
  form.baselineFrom = selectedScenario.value.baselineFrom ?? ''
  form.baselineTo = selectedScenario.value.baselineTo ?? ''
  form.projectionStart = selectedScenario.value.projectionStart.slice(0, 10)
  form.horizonMonths = selectedScenario.value.horizonMonths
  form.baseline = { ...selectedScenario.value.baseline }
  form.assumptions = { ...selectedScenario.value.assumptions }
  seasonalityText.value = selectedScenario.value.assumptions.seasonalityFactors.join(',')
}

function resetForm() {
  editingId.value = null
  form.name = 'Base 5 Year Projection'
  form.description = ''
}

function previousPage() {
  if (page.value <= 1) return
  page.value -= 1
  loadScenarios()
}

function nextPage() {
  if (page.value >= totalPages.value) return
  page.value += 1
  loadScenarios()
}

function toggleCompare(id: string) {
  compareIds.value = compareIds.value.includes(id)
    ? compareIds.value.filter(item => item !== id)
    : [...compareIds.value, id].slice(-6)
}

function statusColor(status: string) {
  if (status === 'Approved') return 'success'
  if (status === 'Submitted') return 'primary'
  if (status === 'Archived') return 'warning'
  return 'neutral'
}

function money(value: number | null | undefined) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 0 }).format(Number(value ?? 0))
}

function formatMonth(value: string) {
  return formatFinalAccountsDateOnly(value)
}

function showSuccess(text: string) {
  message.value = text
  messageTone.value = 'success'
}

function showError(error: unknown) {
  message.value = error instanceof Error ? error.message : String(error)
  messageTone.value = 'error'
}
</script>
