<template>
  <section class="space-y-5">
    <div class="flex flex-wrap items-start justify-between gap-3">
      <div>
        <p class="garmetix-kicker"><UIcon name="i-lucide-book-open-check" class="size-4" /> Final Accounts</p>
        <h1 class="garmetix-dashboard-title">General Ledger</h1>
      </div>
      <div class="flex flex-wrap items-center gap-2">
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="loadAll">Refresh</UButton>
        <UButton to="/chart-of-accounts" icon="i-lucide-list-tree" color="neutral" variant="soft">Chart</UButton>
        <UButton to="/fiscal-periods" icon="i-lucide-calendar-range" color="neutral" variant="soft">Periods</UButton>
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

    <div class="grid gap-4 2xl:grid-cols-[minmax(24rem,30rem)_1fr]">
      <UCard :ui="{ body: 'p-5' }">
        <form class="space-y-4" @submit.prevent="saveDraft">
          <div class="flex items-center justify-between gap-3">
            <h2 class="text-base font-semibold text-highlighted">{{ editingJournalId ? 'Edit Draft' : 'New Adjustment' }}</h2>
            <UButton type="button" icon="i-lucide-eraser" color="neutral" variant="ghost" size="sm" @click="resetForm">Clear</UButton>
          </div>

          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Date" name="onDate">
              <UInput v-model="journalForm.onDate" type="date" icon="i-lucide-calendar" required />
            </UFormField>
            <UFormField label="Fiscal period" name="fiscalPeriodId">
              <USelectMenu v-model="journalForm.fiscalPeriodId" :items="periodSelectItems" value-key="value" class="w-full" />
            </UFormField>
          </div>

          <UFormField label="Reference" name="referenceNumber">
            <UInput v-model="journalForm.referenceNumber" icon="i-lucide-file-text" />
          </UFormField>

          <UFormField label="Narration" name="narration">
            <UTextarea v-model="journalForm.narration" :rows="3" />
          </UFormField>

          <UFormField label="Idempotency key" name="idempotencyKey">
            <UInput v-model="journalForm.idempotencyKey" icon="i-lucide-key-round" />
          </UFormField>

          <div class="space-y-3">
            <div class="flex items-center justify-between gap-3">
              <h3 class="text-sm font-semibold text-highlighted">Lines</h3>
              <UButton type="button" icon="i-lucide-plus" color="neutral" variant="soft" size="sm" @click="addLine">Add Line</UButton>
            </div>

            <div v-for="(line, index) in journalForm.lines" :key="index" class="grid gap-2 rounded-md border border-default p-3">
              <div class="flex items-center justify-between gap-2">
                <span class="text-sm font-medium text-muted">Line {{ index + 1 }}</span>
                <UTooltip text="Remove Line">
                  <UButton type="button" icon="i-lucide-trash-2" color="error" variant="ghost" size="xs" :disabled="journalForm.lines.length <= 2" @click="removeLine(index)" />
                </UTooltip>
              </div>
              <USelectMenu v-model="line.accountId" :items="accountSelectItems" value-key="value" class="w-full" />
              <div class="grid gap-2 sm:grid-cols-2">
                <UInput v-model.number="line.debit" type="number" min="0" step="0.01" icon="i-lucide-arrow-down-to-line" />
                <UInput v-model.number="line.credit" type="number" min="0" step="0.01" icon="i-lucide-arrow-up-from-line" />
              </div>
              <UInput v-model="line.narration" icon="i-lucide-message-square-text" />
            </div>
          </div>

          <UAlert
            v-if="validation"
            :icon="validation.canPost ? 'i-lucide-circle-check' : 'i-lucide-circle-alert'"
            :color="validation.canPost ? 'success' : 'warning'"
            variant="subtle"
            :title="validation.canPost ? 'Balanced' : 'Needs Review'"
            :description="validationSummary"
          />

          <div class="grid gap-2 sm:grid-cols-2">
            <UButton type="button" icon="i-lucide-shield-check" color="neutral" variant="soft" :loading="previewing" block @click="previewJournal">Preview</UButton>
            <UButton type="submit" icon="i-lucide-save" :loading="saving" block>{{ editingJournalId ? 'Save Draft' : 'Create Draft' }}</UButton>
          </div>
        </form>
      </UCard>

      <UCard class="overflow-hidden" :ui="{ body: 'p-0' }">
        <div class="flex flex-wrap items-center gap-2 border-b border-default p-3">
          <USelect v-model="statusFilter" :items="statusFilterItems" class="w-full sm:w-40" />
          <UInput v-model="sourceFilter" icon="i-lucide-filter" placeholder="Source type" class="w-full sm:w-48" />
          <UButton icon="i-lucide-search" color="neutral" variant="soft" :loading="loading" @click="applyFilters">Search</UButton>
          <div class="ml-auto flex items-center gap-1">
            <UButton icon="i-lucide-chevron-left" color="neutral" variant="ghost" size="sm" :disabled="page <= 1" @click="previousPage" />
            <UBadge color="neutral" variant="subtle">{{ page }} / {{ totalPages }}</UBadge>
            <UButton icon="i-lucide-chevron-right" color="neutral" variant="ghost" size="sm" :disabled="page >= totalPages" @click="nextPage" />
          </div>
        </div>

        <UTable :data="journals.rows" :columns="journalColumns" :loading="loading" class="w-full">
          <template #entryNumber-cell="{ row }">
            <span class="font-mono text-sm">{{ row.original.entryNumber }}</span>
          </template>
          <template #onDate-cell="{ row }">
            {{ formatFinalAccountsDateOnly(row.original.onDate) }}
          </template>
          <template #status-cell="{ row }">
            <UBadge :color="statusColor(row.original.status)" variant="subtle">{{ row.original.status }}</UBadge>
          </template>
          <template #totalDebit-cell="{ row }">
            {{ money(row.original.totalDebit) }}
          </template>
          <template #totalCredit-cell="{ row }">
            {{ money(row.original.totalCredit) }}
          </template>
          <template #actions-cell="{ row }">
            <div class="flex justify-end gap-1">
              <UTooltip text="View Journal">
                <UButton icon="i-lucide-eye" color="neutral" variant="ghost" size="sm" @click="viewJournal(row.original.id)" />
              </UTooltip>
              <UTooltip text="Post Draft">
                <UButton icon="i-lucide-badge-check" color="success" variant="ghost" size="sm" :disabled="row.original.status !== 'Draft'" @click="postJournal(row.original.id)" />
              </UTooltip>
              <UTooltip text="Reverse Journal">
                <UButton icon="i-lucide-rotate-ccw" color="warning" variant="ghost" size="sm" :disabled="row.original.status !== 'Posted'" @click="reverseJournal(row.original.id)" />
              </UTooltip>
              <UTooltip text="Delete Draft">
                <UButton icon="i-lucide-trash-2" color="error" variant="ghost" size="sm" :disabled="row.original.status !== 'Draft'" @click="deleteDraft(row.original.id)" />
              </UTooltip>
            </div>
          </template>
        </UTable>
      </UCard>
    </div>

    <UCard v-if="selectedJournal" class="overflow-hidden" :ui="{ body: 'p-0' }">
      <div class="flex flex-wrap items-start justify-between gap-3 border-b border-default p-4">
        <div>
          <p class="font-mono text-sm text-muted">{{ selectedJournal.entryNumber }}</p>
          <h2 class="text-lg font-semibold text-highlighted">{{ selectedJournal.narration }}</h2>
          <div class="mt-2 flex flex-wrap gap-2">
            <UBadge :color="statusColor(selectedJournal.status)" variant="subtle">{{ selectedJournal.status }}</UBadge>
            <UBadge color="neutral" variant="subtle">{{ selectedJournal.sourceType }}</UBadge>
            <UBadge v-if="selectedJournal.referenceNumber" color="neutral" variant="subtle">{{ selectedJournal.referenceNumber }}</UBadge>
          </div>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton v-if="selectedJournal.status === 'Draft'" icon="i-lucide-pencil" color="neutral" variant="soft" @click="editDraft(selectedJournal)">Edit Draft</UButton>
          <UButton v-if="selectedJournal.status === 'Draft'" icon="i-lucide-badge-check" color="success" variant="soft" @click="postJournal(selectedJournal.id)">Post</UButton>
          <UButton v-if="selectedJournal.status === 'Posted'" icon="i-lucide-rotate-ccw" color="warning" variant="soft" @click="reverseJournal(selectedJournal.id)">Reverse</UButton>
        </div>
      </div>

      <div class="grid gap-0 xl:grid-cols-[1fr_22rem]">
        <div class="overflow-auto">
          <UTable :data="selectedJournal.lines" :columns="lineColumns">
            <template #account-cell="{ row }">
              <span class="font-mono text-sm">{{ row.original.accountCode || '-' }}</span>
              <span class="ml-2">{{ row.original.accountName || row.original.accountId }}</span>
            </template>
            <template #debit-cell="{ row }">
              {{ money(row.original.debit) }}
            </template>
            <template #credit-cell="{ row }">
              {{ money(row.original.credit) }}
            </template>
          </UTable>
        </div>

        <div class="border-t border-default p-4 xl:border-l xl:border-t-0">
          <dl class="grid gap-3 text-sm">
            <div class="flex justify-between gap-3">
              <dt class="text-muted">Date</dt>
              <dd class="font-medium text-highlighted">{{ formatFinalAccountsDateOnly(selectedJournal.onDate) }}</dd>
            </div>
            <div class="flex justify-between gap-3">
              <dt class="text-muted">Debit</dt>
              <dd class="font-medium text-highlighted">{{ money(selectedJournal.totalDebit) }}</dd>
            </div>
            <div class="flex justify-between gap-3">
              <dt class="text-muted">Credit</dt>
              <dd class="font-medium text-highlighted">{{ money(selectedJournal.totalCredit) }}</dd>
            </div>
            <div class="flex justify-between gap-3">
              <dt class="text-muted">Source ID</dt>
              <dd class="max-w-40 truncate font-mono text-xs text-highlighted">{{ selectedJournal.sourceId || '-' }}</dd>
            </div>
            <div class="flex justify-between gap-3">
              <dt class="text-muted">Idempotency</dt>
              <dd class="max-w-40 truncate font-mono text-xs text-highlighted">{{ selectedJournal.idempotencyKey || '-' }}</dd>
            </div>
          </dl>

          <div class="mt-4 space-y-2">
            <h3 class="text-sm font-semibold text-highlighted">Audit</h3>
            <div v-for="event in selectedJournal.events" :key="`${event.event}-${event.at}`" class="rounded-md border border-default p-3">
              <div class="flex items-center justify-between gap-3">
                <span class="text-sm font-medium text-highlighted">{{ event.event }}</span>
                <span class="text-xs text-muted">{{ formatFinalAccountsDate(event.at) }}</span>
              </div>
              <p class="mt-1 text-sm text-muted">{{ event.detail }}</p>
              <p class="mt-1 text-xs text-muted">{{ event.actor || 'System' }}</p>
            </div>
          </div>
        </div>
      </div>
    </UCard>
  </section>
</template>

<script setup lang="ts">
import {
  formatFinalAccountsDate,
  formatFinalAccountsDateOnly,
  type FinalAccountsAccount,
  type FinalAccountsFiscalPeriod,
  type FinalAccountsJournal,
  type FinalAccountsJournalList,
  type FinalAccountsJournalPayload,
  type FinalAccountsJournalStatus,
  type FinalAccountsJournalValidation,
  useFinalAccountsApiClient
} from '../utils/final-accounts-api'

useHead({ title: 'General Ledger' })

type SelectItem = { label: string; value: string }
type Tone = 'success' | 'warning' | 'error' | 'neutral'
type JournalFormLine = { accountId: string; debit: number; credit: number; narration: string }

const EMPTY_GUID = '00000000-0000-0000-0000-000000000000'
const api = useFinalAccountsApiClient()
const loading = ref(false)
const saving = ref(false)
const previewing = ref(false)
const message = ref('')
const messageTone = ref<Tone>('neutral')
const accounts = ref<FinalAccountsAccount[]>([])
const periods = ref<FinalAccountsFiscalPeriod[]>([])
const journals = ref<FinalAccountsJournalList>({ page: 1, pageSize: 25, totalCount: 0, rows: [] })
const selectedJournal = ref<FinalAccountsJournal | null>(null)
const validation = ref<FinalAccountsJournalValidation | null>(null)
const editingJournalId = ref('')
const page = ref(1)
const pageSize = ref(25)
const statusFilter = ref('All')
const sourceFilter = ref('')
const statusFilterItems = ['All', 'Draft', 'Posted', 'Reversed']

const journalForm = reactive({
  onDate: today(),
  fiscalPeriodId: '',
  referenceNumber: '',
  narration: 'Manual adjustment',
  idempotencyKey: '',
  lines: defaultLines()
})

const messageTitle = computed(() => messageTone.value === 'success' ? 'Saved' : messageTone.value === 'error' ? 'Ledger unavailable' : messageTone.value === 'warning' ? 'Review' : 'General ledger')
const messageIcon = computed(() => messageTone.value === 'success' ? 'i-lucide-circle-check' : messageTone.value === 'error' ? 'i-lucide-circle-alert' : messageTone.value === 'warning' ? 'i-lucide-triangle-alert' : 'i-lucide-info')
const totalPages = computed(() => Math.max(1, Math.ceil(journals.value.totalCount / pageSize.value)))
const metricCards = computed(() => [
  { label: 'Journals', value: journals.value.totalCount, icon: 'i-lucide-book-open-check' },
  { label: 'Drafts', value: journals.value.rows.filter(item => item.status === 'Draft').length, icon: 'i-lucide-file-pen-line' },
  { label: 'Posted', value: journals.value.rows.filter(item => item.status === 'Posted').length, icon: 'i-lucide-badge-check' },
  { label: 'Accounts', value: accounts.value.length, icon: 'i-lucide-list-tree' }
])
const accountSelectItems = computed<SelectItem[]>(() => [
  { label: 'Select account', value: '' },
  ...accounts.value.filter(item => item.isActive).map(item => ({ label: `${item.code} - ${item.name}`, value: item.id }))
])
const periodSelectItems = computed<SelectItem[]>(() => [
  { label: 'Auto by date', value: '' },
  ...periods.value.map(period => ({ label: `${period.name} (${period.status})`, value: period.id }))
])
const validationSummary = computed(() => {
  const row = validation.value
  if (!row) return ''
  const issues = row.issues.map(item => item.message).join(' ')
  return issues || `Debit ${money(row.totalDebit)} and credit ${money(row.totalCredit)} are balanced.`
})

const journalColumns = [
  { accessorKey: 'entryNumber', header: 'Entry' },
  { accessorKey: 'onDate', header: 'Date' },
  { accessorKey: 'status', header: 'Status' },
  { accessorKey: 'referenceNumber', header: 'Reference' },
  { accessorKey: 'totalDebit', header: 'Debit' },
  { accessorKey: 'totalCredit', header: 'Credit' },
  { accessorKey: 'actions', header: '', enableSorting: false }
]
const lineColumns = [
  { accessorKey: 'lineNumber', header: 'No.' },
  { accessorKey: 'account', header: 'Account' },
  { accessorKey: 'debit', header: 'Debit' },
  { accessorKey: 'credit', header: 'Credit' },
  { accessorKey: 'narration', header: 'Narration' }
]

async function loadAll() {
  loading.value = true
  message.value = ''
  try {
    const [accountRows, periodRows] = await Promise.all([
      api.get<FinalAccountsAccount[]>('accounts'),
      api.get<FinalAccountsFiscalPeriod[]>('fiscal-periods')
    ])
    accounts.value = accountRows
    periods.value = periodRows
    await loadJournals(false)
  } catch (err) {
    showError(err, 'General ledger could not be loaded.')
  } finally {
    loading.value = false
  }
}

async function loadJournals(useSpinner = true) {
  if (useSpinner) loading.value = true
  try {
    const params = new URLSearchParams({
      page: String(page.value),
      pageSize: String(pageSize.value)
    })
    if (statusFilter.value !== 'All') params.set('status', statusFilter.value)
    if (sourceFilter.value.trim()) params.set('sourceType', sourceFilter.value.trim())
    journals.value = await api.get<FinalAccountsJournalList>(`journals?${params}`)
  } catch (err) {
    showError(err, 'Journal list could not be loaded.')
  } finally {
    if (useSpinner) loading.value = false
  }
}

async function previewJournal() {
  previewing.value = true
  message.value = ''
  try {
    validation.value = await api.post<FinalAccountsJournalValidation>('journals/preview', journalPayload())
    if (!validation.value.canPost) {
      messageTone.value = 'warning'
      message.value = validationSummary.value
    }
  } catch (err) {
    showError(err, 'Journal preview failed.')
  } finally {
    previewing.value = false
  }
}

async function saveDraft() {
  saving.value = true
  message.value = ''
  try {
    const saved = editingJournalId.value
      ? await api.put<FinalAccountsJournal>(`journals/${editingJournalId.value}`, journalPayload())
      : await api.post<FinalAccountsJournal>('journals', journalPayload())
    selectedJournal.value = saved
    validation.value = null
    messageTone.value = 'success'
    message.value = editingJournalId.value ? 'Draft journal saved.' : 'Draft journal created.'
    resetForm()
    await loadJournals(false)
  } catch (err) {
    showError(err, 'Draft journal could not be saved.')
  } finally {
    saving.value = false
  }
}

async function viewJournal(id: string) {
  loading.value = true
  try {
    selectedJournal.value = await api.get<FinalAccountsJournal>(`journals/${id}`)
  } catch (err) {
    showError(err, 'Journal detail could not be loaded.')
  } finally {
    loading.value = false
  }
}

async function postJournal(id: string) {
  loading.value = true
  message.value = ''
  try {
    const posted = await api.post<FinalAccountsJournal>(`journals/${id}/post`, { idempotencyKey: `post-${id}` })
    selectedJournal.value = posted
    messageTone.value = 'success'
    message.value = 'Journal posted.'
    await loadJournals(false)
  } catch (err) {
    showError(err, 'Journal could not be posted.')
  } finally {
    loading.value = false
  }
}

async function reverseJournal(id: string) {
  const reason = window.prompt('Reversal reason')
  if (!reason?.trim()) return
  loading.value = true
  message.value = ''
  try {
    const reversed = await api.post<FinalAccountsJournal>(`journals/${id}/reverse`, {
      onDate: journalForm.onDate || today(),
      reason,
      idempotencyKey: `reverse-${id}-${Date.now()}`
    })
    selectedJournal.value = reversed
    messageTone.value = 'success'
    message.value = 'Reversal journal posted.'
    await loadJournals(false)
  } catch (err) {
    showError(err, 'Journal could not be reversed.')
  } finally {
    loading.value = false
  }
}

async function deleteDraft(id: string) {
  if (!window.confirm('Delete this draft journal?')) return
  loading.value = true
  message.value = ''
  try {
    await api.remove(`journals/${id}`)
    if (selectedJournal.value?.id === id) selectedJournal.value = null
    messageTone.value = 'success'
    message.value = 'Draft journal deleted.'
    await loadJournals(false)
  } catch (err) {
    showError(err, 'Draft journal could not be deleted.')
  } finally {
    loading.value = false
  }
}

function editDraft(journal: FinalAccountsJournal) {
  editingJournalId.value = journal.id
  journalForm.onDate = toDateOnly(journal.onDate)
  journalForm.fiscalPeriodId = journal.fiscalPeriodId || ''
  journalForm.referenceNumber = journal.referenceNumber || ''
  journalForm.narration = journal.narration
  journalForm.idempotencyKey = journal.idempotencyKey || ''
  journalForm.lines = journal.lines.map(line => ({
    accountId: line.accountId,
    debit: line.debit,
    credit: line.credit,
    narration: line.narration || ''
  }))
  validation.value = null
}

function applyFilters() {
  page.value = 1
  loadJournals()
}

function previousPage() {
  if (page.value <= 1) return
  page.value--
  loadJournals()
}

function nextPage() {
  if (page.value >= totalPages.value) return
  page.value++
  loadJournals()
}

function addLine() {
  journalForm.lines.push({ accountId: '', debit: 0, credit: 0, narration: '' })
}

function removeLine(index: number) {
  if (journalForm.lines.length <= 2) return
  journalForm.lines.splice(index, 1)
}

function resetForm() {
  editingJournalId.value = ''
  validation.value = null
  journalForm.onDate = today()
  journalForm.fiscalPeriodId = ''
  journalForm.referenceNumber = ''
  journalForm.narration = 'Manual adjustment'
  journalForm.idempotencyKey = ''
  journalForm.lines = defaultLines()
}

function journalPayload(): FinalAccountsJournalPayload {
  return {
    onDate: journalForm.onDate,
    fiscalPeriodId: blankToNull(journalForm.fiscalPeriodId),
    referenceNumber: blankToNull(journalForm.referenceNumber),
    narration: blankToNull(journalForm.narration),
    sourceType: 'ManualAdjustment',
    sourceId: null,
    idempotencyKey: blankToNull(journalForm.idempotencyKey),
    lines: journalForm.lines.map(line => ({
      accountId: line.accountId || EMPTY_GUID,
      debit: Number(line.debit) || 0,
      credit: Number(line.credit) || 0,
      narration: blankToNull(line.narration)
    }))
  }
}

function defaultLines(): JournalFormLine[] {
  return [
    { accountId: '', debit: 0, credit: 0, narration: '' },
    { accountId: '', debit: 0, credit: 0, narration: '' }
  ]
}

function statusColor(status: FinalAccountsJournalStatus) {
  if (status === 'Posted') return 'success'
  if (status === 'Reversed') return 'warning'
  return 'neutral'
}

function money(value: number) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(Number(value) || 0)
}

function today() {
  return new Date().toISOString().slice(0, 10)
}

function toDateOnly(value: string | null | undefined) {
  if (!value) return ''
  return String(value).slice(0, 10)
}

function blankToNull(value: string | null | undefined) {
  const trimmed = String(value || '').trim()
  return trimmed ? trimmed : null
}

function showError(err: unknown, fallback: string) {
  messageTone.value = 'error'
  message.value = err instanceof Error ? err.message : fallback
}

onMounted(loadAll)
</script>
