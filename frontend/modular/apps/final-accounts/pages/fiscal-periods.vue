<template>
  <section class="space-y-5">
    <div class="flex flex-wrap items-start justify-between gap-3">
      <div>
        <p class="garmetix-kicker"><UIcon name="i-lucide-calendar-range" class="size-4" /> Final Accounts</p>
        <h1 class="garmetix-dashboard-title">Fiscal Periods</h1>
      </div>
      <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="loadAll">Refresh</UButton>
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

    <UTabs v-model="activeTab" :items="tabs" class="w-full max-w-md" />

    <div v-if="activeTab === 'years'" class="grid gap-4 xl:grid-cols-[minmax(20rem,24rem)_1fr]">
      <UCard :ui="{ body: 'p-5' }">
        <form class="space-y-4" @submit.prevent="saveYear">
          <div class="flex items-center justify-between gap-3">
            <h2 class="text-base font-semibold text-highlighted">{{ selectedYearId ? 'Edit Year' : 'New Year' }}</h2>
            <UButton type="button" icon="i-lucide-eraser" color="neutral" variant="ghost" size="sm" @click="resetYearForm">Clear</UButton>
          </div>

          <UFormField label="Name" name="name">
            <UInput v-model="yearForm.name" icon="i-lucide-calendar-days" required />
          </UFormField>

          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Start date" name="startDate">
              <UInput v-model="yearForm.startDate" type="date" icon="i-lucide-calendar" required />
            </UFormField>
            <UFormField label="End date" name="endDate">
              <UInput v-model="yearForm.endDate" type="date" icon="i-lucide-calendar-check" required />
            </UFormField>
          </div>

          <UFormField label="Status" name="status">
            <USelect v-model="yearForm.status" :items="statuses" />
          </UFormField>

          <UButton type="submit" icon="i-lucide-save" :loading="saving" block>{{ selectedYearId ? 'Save Year' : 'Create Year' }}</UButton>
        </form>
      </UCard>

      <UCard class="overflow-hidden" :ui="{ body: 'p-0' }">
        <UTable :data="years" :columns="yearColumns" :loading="loading" class="w-full">
          <template #startDate-cell="{ row }">
            {{ formatFinalAccountsDateOnly(row.original.startDate) }}
          </template>
          <template #endDate-cell="{ row }">
            {{ formatFinalAccountsDateOnly(row.original.endDate) }}
          </template>
          <template #status-cell="{ row }">
            <UBadge :color="statusColor(row.original.status)" variant="subtle">{{ row.original.status }}</UBadge>
          </template>
          <template #actions-cell="{ row }">
            <div class="flex justify-end gap-1">
              <UTooltip text="Edit Year">
                <UButton icon="i-lucide-pencil" color="primary" variant="ghost" size="sm" @click="editYear(row.original)" />
              </UTooltip>
              <UTooltip text="New Period">
                <UButton icon="i-lucide-calendar-plus" color="neutral" variant="ghost" size="sm" @click="newPeriodForYear(row.original)" />
              </UTooltip>
            </div>
          </template>
        </UTable>
      </UCard>
    </div>

    <div v-else class="grid gap-4 xl:grid-cols-[minmax(20rem,24rem)_1fr]">
      <UCard :ui="{ body: 'p-5' }">
        <form class="space-y-4" @submit.prevent="savePeriod">
          <div class="flex items-center justify-between gap-3">
            <h2 class="text-base font-semibold text-highlighted">{{ selectedPeriodId ? 'Edit Period' : 'New Period' }}</h2>
            <UButton type="button" icon="i-lucide-eraser" color="neutral" variant="ghost" size="sm" @click="resetPeriodForm">Clear</UButton>
          </div>

          <UFormField label="Fiscal year" name="fiscalYearId">
            <USelectMenu v-model="periodForm.fiscalYearId" :items="yearSelectItems" value-key="value" class="w-full" />
          </UFormField>

          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Period number" name="periodNumber">
              <UInput v-model.number="periodForm.periodNumber" type="number" min="1" icon="i-lucide-list-ordered" required />
            </UFormField>
            <UFormField label="Status" name="status">
              <USelect v-model="periodForm.status" :items="statuses" />
            </UFormField>
          </div>

          <UFormField label="Name" name="name">
            <UInput v-model="periodForm.name" icon="i-lucide-calendar-clock" required />
          </UFormField>

          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Start date" name="startDate">
              <UInput v-model="periodForm.startDate" type="date" icon="i-lucide-calendar" required />
            </UFormField>
            <UFormField label="End date" name="endDate">
              <UInput v-model="periodForm.endDate" type="date" icon="i-lucide-calendar-check" required />
            </UFormField>
          </div>

          <UButton type="submit" icon="i-lucide-save" :loading="saving" block>{{ selectedPeriodId ? 'Save Period' : 'Create Period' }}</UButton>
        </form>
      </UCard>

      <UCard class="overflow-hidden" :ui="{ body: 'p-0' }">
        <UTable :data="periodRows" :columns="periodColumns" :loading="loading" class="w-full">
          <template #startDate-cell="{ row }">
            {{ formatFinalAccountsDateOnly(row.original.startDate) }}
          </template>
          <template #endDate-cell="{ row }">
            {{ formatFinalAccountsDateOnly(row.original.endDate) }}
          </template>
          <template #status-cell="{ row }">
            <UBadge :color="statusColor(row.original.status)" variant="subtle">{{ row.original.status }}</UBadge>
          </template>
          <template #actions-cell="{ row }">
            <div class="flex justify-end gap-1">
              <UTooltip text="Edit Period">
                <UButton icon="i-lucide-pencil" color="primary" variant="ghost" size="sm" @click="editPeriod(row.original)" />
              </UTooltip>
            </div>
          </template>
        </UTable>
      </UCard>
    </div>
  </section>
</template>

<script setup lang="ts">
import {
  formatFinalAccountsDateOnly,
  type FinalAccountsFiscalPeriod,
  type FinalAccountsFiscalPeriodPayload,
  type FinalAccountsFiscalYear,
  type FinalAccountsFiscalYearPayload,
  type FinalAccountsPeriodStatus,
  type FinalAccountsValidationSummary,
  useFinalAccountsApiClient
} from '../utils/final-accounts-api'

useHead({ title: 'Fiscal Periods' })

type SelectItem = { label: string; value: string }
type Tone = 'success' | 'error' | 'neutral'

const api = useFinalAccountsApiClient()
const activeTab = ref('years')
const loading = ref(false)
const saving = ref(false)
const message = ref('')
const messageTone = ref<Tone>('neutral')
const years = ref<FinalAccountsFiscalYear[]>([])
const periods = ref<FinalAccountsFiscalPeriod[]>([])
const validation = ref<FinalAccountsValidationSummary | null>(null)
const selectedYearId = ref('')
const selectedPeriodId = ref('')
const statuses: FinalAccountsPeriodStatus[] = ['Draft', 'Open', 'Closed', 'Locked']
const defaultRange = currentFiscalRange()

const yearForm = reactive({
  name: defaultRange.name,
  startDate: defaultRange.start,
  endDate: defaultRange.end,
  status: 'Draft' as FinalAccountsPeriodStatus
})

const periodForm = reactive({
  fiscalYearId: '',
  periodNumber: 1,
  name: 'Period 1',
  startDate: defaultRange.start,
  endDate: defaultRange.end,
  status: 'Draft' as FinalAccountsPeriodStatus
})

const tabs = [
  { label: 'Fiscal Years', value: 'years', icon: 'i-lucide-calendar-range' },
  { label: 'Periods', value: 'periods', icon: 'i-lucide-calendar-clock' }
]
const messageTitle = computed(() => messageTone.value === 'success' ? 'Saved' : messageTone.value === 'error' ? 'Fiscal setup unavailable' : 'Fiscal setup')
const messageIcon = computed(() => messageTone.value === 'success' ? 'i-lucide-circle-check' : messageTone.value === 'error' ? 'i-lucide-circle-alert' : 'i-lucide-info')
const metricCards = computed(() => [
  { label: 'Fiscal Years', value: validation.value?.fiscalYearCount ?? years.value.length, icon: 'i-lucide-calendar-range' },
  { label: 'Periods', value: validation.value?.fiscalPeriodCount ?? periods.value.length, icon: 'i-lucide-calendar-clock' },
  { label: 'Open Years', value: years.value.filter(item => item.status === 'Open').length, icon: 'i-lucide-lock-open' },
  { label: 'Issues', value: fiscalIssues.value.length, icon: 'i-lucide-shield-alert' }
])
const fiscalIssues = computed(() => (validation.value?.issues || []).filter(issue => issue.code.toLowerCase().includes('fiscal')))
const yearSelectItems = computed<SelectItem[]>(() => years.value.map(year => ({ label: `${year.name} (${toDateOnly(year.startDate)} - ${toDateOnly(year.endDate)})`, value: year.id })))
const periodRows = computed(() => periods.value.map(period => ({
  ...period,
  yearName: years.value.find(year => year.id === period.fiscalYearId)?.name || '-'
})))

const yearColumns = [
  { accessorKey: 'name', header: 'Name' },
  { accessorKey: 'startDate', header: 'Start' },
  { accessorKey: 'endDate', header: 'End' },
  { accessorKey: 'status', header: 'Status' },
  { accessorKey: 'actions', header: '', enableSorting: false }
]
const periodColumns = [
  { accessorKey: 'periodNumber', header: 'No.' },
  { accessorKey: 'name', header: 'Name' },
  { accessorKey: 'yearName', header: 'Year' },
  { accessorKey: 'startDate', header: 'Start' },
  { accessorKey: 'endDate', header: 'End' },
  { accessorKey: 'status', header: 'Status' },
  { accessorKey: 'actions', header: '', enableSorting: false }
]

async function loadAll() {
  loading.value = true
  message.value = ''
  try {
    const [yearRows, periodItems, validationRow] = await Promise.all([
      api.get<FinalAccountsFiscalYear[]>('fiscal-years'),
      api.get<FinalAccountsFiscalPeriod[]>('fiscal-periods'),
      api.get<FinalAccountsValidationSummary>('validation/summary')
    ])
    years.value = yearRows
    periods.value = periodItems
    validation.value = validationRow
    if (!periodForm.fiscalYearId && years.value[0]) {
      periodForm.fiscalYearId = years.value[0].id
    }
  } catch (err) {
    showError(err, 'Fiscal periods could not be loaded.')
  } finally {
    loading.value = false
  }
}

async function saveYear() {
  await saveEntity(
    () => selectedYearId.value
      ? api.put<FinalAccountsFiscalYear>(`fiscal-years/${selectedYearId.value}`, yearPayload())
      : api.post<FinalAccountsFiscalYear>('fiscal-years', yearPayload()),
    resetYearForm,
    'Fiscal year saved.'
  )
}

async function savePeriod() {
  await saveEntity(
    () => selectedPeriodId.value
      ? api.put<FinalAccountsFiscalPeriod>(`fiscal-periods/${selectedPeriodId.value}`, periodPayload())
      : api.post<FinalAccountsFiscalPeriod>('fiscal-periods', periodPayload()),
    resetPeriodForm,
    'Fiscal period saved.'
  )
}

async function saveEntity(action: () => Promise<unknown>, reset: () => void, successMessage: string) {
  saving.value = true
  message.value = ''
  try {
    await action()
    messageTone.value = 'success'
    message.value = successMessage
    reset()
    await loadAll()
  } catch (err) {
    showError(err, 'Save failed.')
  } finally {
    saving.value = false
  }
}

function editYear(row: FinalAccountsFiscalYear) {
  selectedYearId.value = row.id
  yearForm.name = row.name
  yearForm.startDate = toDateOnly(row.startDate)
  yearForm.endDate = toDateOnly(row.endDate)
  yearForm.status = row.status
  activeTab.value = 'years'
}

function editPeriod(row: FinalAccountsFiscalPeriod) {
  selectedPeriodId.value = row.id
  periodForm.fiscalYearId = row.fiscalYearId
  periodForm.periodNumber = row.periodNumber
  periodForm.name = row.name
  periodForm.startDate = toDateOnly(row.startDate)
  periodForm.endDate = toDateOnly(row.endDate)
  periodForm.status = row.status
  activeTab.value = 'periods'
}

function newPeriodForYear(row: FinalAccountsFiscalYear) {
  resetPeriodForm()
  const nextNumber = periods.value.filter(period => period.fiscalYearId === row.id).length + 1
  periodForm.fiscalYearId = row.id
  periodForm.periodNumber = nextNumber
  periodForm.name = `Period ${nextNumber}`
  periodForm.startDate = toDateOnly(row.startDate)
  periodForm.endDate = toDateOnly(row.endDate)
  activeTab.value = 'periods'
}

function yearPayload(): FinalAccountsFiscalYearPayload {
  return {
    name: yearForm.name,
    startDate: yearForm.startDate,
    endDate: yearForm.endDate,
    status: yearForm.status
  }
}

function periodPayload(): FinalAccountsFiscalPeriodPayload {
  return {
    fiscalYearId: periodForm.fiscalYearId,
    periodNumber: Number(periodForm.periodNumber) || 1,
    name: periodForm.name,
    startDate: periodForm.startDate,
    endDate: periodForm.endDate,
    status: periodForm.status
  }
}

function resetYearForm() {
  selectedYearId.value = ''
  const range = currentFiscalRange()
  yearForm.name = range.name
  yearForm.startDate = range.start
  yearForm.endDate = range.end
  yearForm.status = 'Draft'
}

function resetPeriodForm() {
  selectedPeriodId.value = ''
  const firstYear = years.value[0]
  periodForm.fiscalYearId = firstYear?.id || ''
  periodForm.periodNumber = 1
  periodForm.name = 'Period 1'
  periodForm.startDate = firstYear ? toDateOnly(firstYear.startDate) : defaultRange.start
  periodForm.endDate = firstYear ? toDateOnly(firstYear.endDate) : defaultRange.end
  periodForm.status = 'Draft'
}

function statusColor(status: FinalAccountsPeriodStatus) {
  if (status === 'Open') return 'success'
  if (status === 'Closed') return 'warning'
  if (status === 'Locked') return 'error'
  return 'neutral'
}

function currentFiscalRange() {
  const now = new Date()
  const startYear = now.getMonth() >= 3 ? now.getFullYear() : now.getFullYear() - 1
  return {
    name: `${startYear}-${String(startYear + 1).slice(-2)}`,
    start: `${startYear}-04-01`,
    end: `${startYear + 1}-03-31`
  }
}

function toDateOnly(value: string | null | undefined) {
  if (!value) return ''
  return String(value).slice(0, 10)
}

function showError(err: unknown, fallback: string) {
  messageTone.value = 'error'
  message.value = err instanceof Error ? err.message : fallback
}

onMounted(loadAll)
</script>
