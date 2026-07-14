<template>
  <section class="space-y-5">
    <div class="flex flex-wrap items-start justify-between gap-3">
      <div>
        <p class="garmetix-kicker"><UIcon name="i-lucide-clipboard-check" class="size-4" /> Final Accounts</p>
        <h1 class="garmetix-dashboard-title">CA Workspace</h1>
      </div>
      <div class="flex flex-wrap items-center gap-2">
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="loadAll">Refresh</UButton>
        <UButton to="/reports" icon="i-lucide-scale" color="neutral" variant="soft">Reports</UButton>
        <UButton to="/general-ledger" icon="i-lucide-book-open-check" color="neutral" variant="soft">Ledger</UButton>
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

    <div class="grid gap-4 2xl:grid-cols-[minmax(26rem,32rem)_1fr]">
      <UCard :ui="{ body: 'p-5' }">
        <form class="space-y-4" @submit.prevent="saveAdjustment">
          <div class="flex items-center justify-between gap-3">
            <h2 class="text-base font-semibold text-highlighted">{{ editingId ? 'Edit Adjustment' : 'New Adjustment' }}</h2>
            <UButton type="button" icon="i-lucide-eraser" color="neutral" variant="ghost" size="sm" @click="resetForm">Clear</UButton>
          </div>

          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Date" name="adjustmentDate">
              <UInput v-model="form.adjustmentDate" type="date" icon="i-lucide-calendar" required />
            </UFormField>
            <UFormField label="Fiscal period" name="fiscalPeriodId">
              <USelectMenu v-model="form.fiscalPeriodId" :items="periodSelectItems" value-key="value" class="w-full" />
            </UFormField>
          </div>

          <UFormField label="Title" name="title">
            <UInput v-model="form.title" icon="i-lucide-file-pen-line" required />
          </UFormField>

          <UFormField label="Reference" name="referenceNumber">
            <UInput v-model="form.referenceNumber" icon="i-lucide-file-text" />
          </UFormField>

          <UFormField label="Description" name="description">
            <UTextarea v-model="form.description" :rows="3" />
          </UFormField>

          <div class="grid gap-3 sm:grid-cols-[1fr_10rem]">
            <UCheckbox v-model="form.autoReverse" label="Auto reversing" />
            <UInput v-model="form.autoReverseDate" type="date" icon="i-lucide-rotate-ccw" :disabled="!form.autoReverse" />
          </div>

          <div class="space-y-3">
            <div class="flex items-center justify-between gap-3">
              <h3 class="text-sm font-semibold text-highlighted">Lines</h3>
              <UButton type="button" icon="i-lucide-plus" color="neutral" variant="soft" size="sm" @click="addLine">Add Line</UButton>
            </div>

            <div v-for="(line, index) in form.lines" :key="index" class="grid gap-2 rounded-md border border-default p-3">
              <div class="flex items-center justify-between gap-2">
                <span class="text-sm font-medium text-muted">Line {{ index + 1 }}</span>
                <UTooltip text="Remove Line">
                  <UButton type="button" icon="i-lucide-trash-2" color="error" variant="ghost" size="xs" :disabled="form.lines.length <= 2" @click="removeLine(index)" />
                </UTooltip>
              </div>
              <USelectMenu v-model="line.accountId" :items="accountSelectItems" value-key="value" class="w-full" />
              <div class="grid gap-2 sm:grid-cols-2">
                <UInput v-model.number="line.debit" type="number" min="0" step="0.01" icon="i-lucide-arrow-down-to-line" />
                <UInput v-model.number="line.credit" type="number" min="0" step="0.01" icon="i-lucide-arrow-up-from-line" />
              </div>
              <UInput v-model="line.statementLineKey" icon="i-lucide-list-checks" placeholder="Statement line" />
              <UInput v-model="line.narration" icon="i-lucide-message-square-text" placeholder="Narration" />
            </div>
          </div>

          <UAlert
            v-if="preview"
            :icon="preview.canPost ? 'i-lucide-circle-check' : 'i-lucide-circle-alert'"
            :color="preview.canPost ? 'success' : 'warning'"
            variant="subtle"
            :title="preview.canPost ? 'Balanced' : 'Needs Review'"
            :description="previewSummary"
          />

          <div class="grid gap-2 sm:grid-cols-2">
            <UButton type="button" icon="i-lucide-shield-check" color="neutral" variant="soft" :loading="previewing" block @click="previewDraft">Preview</UButton>
            <UButton type="submit" icon="i-lucide-save" :loading="saving" block>{{ editingId ? 'Save' : 'Create' }}</UButton>
          </div>
        </form>
      </UCard>

      <UCard class="overflow-hidden" :ui="{ body: 'p-0' }">
        <div class="flex flex-wrap items-center gap-2 border-b border-default p-3">
          <USelect v-model="statusFilter" :items="statusItems" class="w-full sm:w-40" />
          <UButton icon="i-lucide-search" color="neutral" variant="soft" :loading="loading" @click="loadAdjustments">Search</UButton>
          <div class="ml-auto flex items-center gap-1">
            <UButton icon="i-lucide-chevron-left" color="neutral" variant="ghost" size="sm" :disabled="page <= 1" @click="previousPage" />
            <UBadge color="neutral" variant="subtle">{{ page }} / {{ totalPages }}</UBadge>
            <UButton icon="i-lucide-chevron-right" color="neutral" variant="ghost" size="sm" :disabled="page >= totalPages" @click="nextPage" />
          </div>
        </div>

        <UTable :data="adjustments.rows" :columns="batchColumns" :loading="loading" class="w-full">
          <template #batchNumber-cell="{ row }">
            <button class="font-mono text-sm text-primary hover:underline" type="button" @click="selectAdjustment(row.original.id)">{{ row.original.batchNumber }}</button>
          </template>
          <template #adjustmentDate-cell="{ row }">
            {{ formatFinalAccountsDateOnly(row.original.adjustmentDate) }}
          </template>
          <template #status-cell="{ row }">
            <UBadge :color="statusColor(row.original.status)" variant="subtle">{{ row.original.status }}</UBadge>
          </template>
          <template #reportVersion-cell="{ row }">
            <UBadge color="neutral" variant="subtle">{{ row.original.reportVersion }}</UBadge>
          </template>
          <template #totalDebit-cell="{ row }">
            {{ money(row.original.totalDebit) }}
          </template>
          <template #actions-cell="{ row }">
            <div class="flex justify-end gap-1">
              <UTooltip text="View">
                <UButton icon="i-lucide-eye" color="neutral" variant="ghost" size="sm" @click="selectAdjustment(row.original.id)" />
              </UTooltip>
              <UTooltip text="Submit">
                <UButton icon="i-lucide-send" color="neutral" variant="ghost" size="sm" :disabled="row.original.status !== 'Draft'" @click="move(row.original.id, 'submit')" />
              </UTooltip>
              <UTooltip text="Approve">
                <UButton icon="i-lucide-badge-check" color="success" variant="ghost" size="sm" :disabled="row.original.status !== 'Review'" @click="move(row.original.id, 'approve')" />
              </UTooltip>
              <UTooltip text="Post">
                <UButton icon="i-lucide-upload" color="primary" variant="ghost" size="sm" :disabled="row.original.status !== 'Approved'" @click="postAdjustment(row.original.id)" />
              </UTooltip>
              <UTooltip text="Reverse">
                <UButton icon="i-lucide-rotate-ccw" color="warning" variant="ghost" size="sm" :disabled="row.original.status !== 'Posted'" @click="reverseAdjustment(row.original.id)" />
              </UTooltip>
            </div>
          </template>
        </UTable>
      </UCard>
    </div>

    <div v-if="selected" class="grid gap-4 2xl:grid-cols-[1fr_24rem]">
      <UCard class="overflow-hidden" :ui="{ body: 'p-0' }">
        <div class="flex flex-wrap items-start justify-between gap-3 border-b border-default p-4">
          <div>
            <p class="font-mono text-sm text-muted">{{ selected.batchNumber }}</p>
            <h2 class="text-lg font-semibold text-highlighted">{{ selected.title }}</h2>
            <div class="mt-2 flex flex-wrap gap-2">
              <UBadge :color="statusColor(selected.status)" variant="subtle">{{ selected.status }}</UBadge>
              <UBadge color="neutral" variant="subtle">{{ selected.reportVersion }}</UBadge>
              <UBadge color="neutral" variant="subtle">{{ selected.auditStatus }}</UBadge>
            </div>
          </div>
          <div class="flex flex-wrap gap-2">
            <UButton v-if="selected.status === 'Draft' || selected.status === 'Rejected'" icon="i-lucide-pencil" color="neutral" variant="soft" @click="editSelected">Edit</UButton>
            <UButton v-if="selected.status === 'Submitted'" icon="i-lucide-search-check" color="neutral" variant="soft" @click="move(selected.id, 'review')">Review</UButton>
            <UButton v-if="selected.status === 'Review'" icon="i-lucide-circle-x" color="error" variant="soft" @click="move(selected.id, 'reject')">Reject</UButton>
          </div>
        </div>

        <UTable :data="selected.lines" :columns="lineColumns">
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
      </UCard>

      <div class="space-y-4">
        <UCard :ui="{ body: 'p-4' }">
          <h3 class="text-sm font-semibold text-highlighted">Impact Preview</h3>
          <dl class="mt-3 grid gap-2 text-sm">
            <div class="flex justify-between gap-3"><dt class="text-muted">P&L</dt><dd class="font-medium text-highlighted">{{ money(savedPreview?.profitLossImpact || 0) }}</dd></div>
            <div class="flex justify-between gap-3"><dt class="text-muted">Balance Sheet</dt><dd class="font-medium text-highlighted">{{ money(savedPreview?.balanceSheetImpact || 0) }}</dd></div>
            <div class="flex justify-between gap-3"><dt class="text-muted">Debit</dt><dd class="font-medium text-highlighted">{{ money(selected.totalDebit) }}</dd></div>
            <div class="flex justify-between gap-3"><dt class="text-muted">Credit</dt><dd class="font-medium text-highlighted">{{ money(selected.totalCredit) }}</dd></div>
          </dl>
        </UCard>

        <UCard :ui="{ body: 'p-4' }">
          <form class="space-y-3" @submit.prevent="addComment">
            <h3 class="text-sm font-semibold text-highlighted">Comments</h3>
            <UTextarea v-model="commentBody" :rows="3" />
            <UButton type="submit" icon="i-lucide-message-square-plus" :loading="saving" block>Add Comment</UButton>
          </form>
          <div class="mt-3 space-y-2">
            <div v-for="comment in selected.comments" :key="comment.id" class="rounded-md border border-default p-3">
              <p class="text-sm text-highlighted">{{ comment.body }}</p>
              <p class="mt-1 text-xs text-muted">{{ comment.createdBy || 'System' }} | {{ formatFinalAccountsDate(comment.createdAt) }}</p>
            </div>
          </div>
        </UCard>

        <UCard :ui="{ body: 'p-4' }">
          <form class="space-y-3" @submit.prevent="addAttachment">
            <h3 class="text-sm font-semibold text-highlighted">Attachments</h3>
            <UInput v-model="attachment.fileName" icon="i-lucide-file" placeholder="File name" />
            <UInput v-model="attachment.storageReference" icon="i-lucide-link" placeholder="Storage reference" />
            <UButton type="submit" icon="i-lucide-paperclip" :loading="saving" block>Add Attachment</UButton>
          </form>
          <div class="mt-3 space-y-2">
            <div v-for="file in selected.attachments" :key="file.id" class="rounded-md border border-default p-3">
              <p class="text-sm font-medium text-highlighted">{{ file.fileName }}</p>
              <p class="truncate text-xs text-muted">{{ file.storageReference }}</p>
            </div>
          </div>
        </UCard>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import {
  formatFinalAccountsDate,
  formatFinalAccountsDateOnly,
  type FinalAccountsAccount,
  type FinalAccountsAdjustment,
  type FinalAccountsAdjustmentList,
  type FinalAccountsAdjustmentPayload,
  type FinalAccountsAdjustmentPreview,
  type FinalAccountsAdjustmentStatus,
  type FinalAccountsFiscalPeriod,
  useFinalAccountsApiClient
} from '../utils/final-accounts-api'

useHead({ title: 'CA Workspace' })

type SelectItem = { label: string; value: string }
type Tone = 'success' | 'warning' | 'error' | 'neutral'
type AdjustmentFormLine = { accountId: string; debit: number; credit: number; narration: string; statementLineKey: string }
type WorkflowAction = 'submit' | 'review' | 'approve' | 'reject'

const EMPTY_GUID = '00000000-0000-0000-0000-000000000000'
const api = useFinalAccountsApiClient()
const loading = ref(false)
const saving = ref(false)
const previewing = ref(false)
const message = ref('')
const messageTone = ref<Tone>('neutral')
const accounts = ref<FinalAccountsAccount[]>([])
const periods = ref<FinalAccountsFiscalPeriod[]>([])
const adjustments = ref<FinalAccountsAdjustmentList>({ page: 1, pageSize: 25, totalCount: 0, rows: [] })
const selected = ref<FinalAccountsAdjustment | null>(null)
const preview = ref<FinalAccountsAdjustmentPreview | null>(null)
const savedPreview = ref<FinalAccountsAdjustmentPreview | null>(null)
const editingId = ref('')
const page = ref(1)
const pageSize = ref(25)
const statusFilter = ref('All')
const commentBody = ref('')
const attachment = reactive({ fileName: '', storageReference: '' })
const statusItems = ['All', 'Draft', 'Submitted', 'Review', 'Approved', 'Rejected', 'Posted', 'Reversed']

const form = reactive({
  title: '',
  description: '',
  adjustmentDate: today(),
  fiscalPeriodId: '',
  referenceNumber: '',
  autoReverse: false,
  autoReverseDate: '',
  lines: [
    emptyLine(),
    emptyLine()
  ] as AdjustmentFormLine[]
})

const batchColumns = [
  { accessorKey: 'batchNumber', header: 'Batch' },
  { accessorKey: 'title', header: 'Title' },
  { accessorKey: 'adjustmentDate', header: 'Date' },
  { accessorKey: 'status', header: 'Status' },
  { accessorKey: 'reportVersion', header: 'Version' },
  { accessorKey: 'totalDebit', header: 'Debit' },
  { accessorKey: 'actions', header: '' }
]

const lineColumns = [
  { accessorKey: 'lineNumber', header: '#' },
  { accessorKey: 'account', header: 'Account' },
  { accessorKey: 'debit', header: 'Debit' },
  { accessorKey: 'credit', header: 'Credit' },
  { accessorKey: 'statementLineKey', header: 'Statement Line' },
  { accessorKey: 'narration', header: 'Narration' }
]

const accountSelectItems = computed<SelectItem[]>(() => [
  { label: 'Select account', value: EMPTY_GUID },
  ...accounts.value.map(item => ({ label: `${item.code} | ${item.name}`, value: item.id }))
])

const periodSelectItems = computed<SelectItem[]>(() => [
  { label: 'Auto period', value: '' },
  ...periods.value.map(item => ({ label: `${item.name} (${item.status})`, value: item.id }))
])

const totalPages = computed(() => Math.max(1, Math.ceil(adjustments.value.totalCount / adjustments.value.pageSize)))
const messageTitle = computed(() => messageTone.value === 'error' ? 'Action Failed' : messageTone.value === 'success' ? 'Done' : 'Notice')
const messageIcon = computed(() => messageTone.value === 'error' ? 'i-lucide-circle-alert' : messageTone.value === 'success' ? 'i-lucide-circle-check' : 'i-lucide-info')
const previewSummary = computed(() => preview.value ? `Debit ${money(preview.value.totalDebit)} | Credit ${money(preview.value.totalCredit)} | P&L ${money(preview.value.profitLossImpact)} | Balance Sheet ${money(preview.value.balanceSheetImpact)}` : '')

const metricCards = computed(() => [
  { label: 'Batches', value: String(adjustments.value.totalCount), icon: 'i-lucide-files' },
  { label: 'Approved', value: String(adjustments.value.rows.filter(item => item.status === 'Approved').length), icon: 'i-lucide-badge-check' },
  { label: 'Posted', value: String(adjustments.value.rows.filter(item => item.status === 'Posted').length), icon: 'i-lucide-upload' },
  { label: 'Audit Status', value: selected.value?.auditStatus || 'Unaudited', icon: 'i-lucide-shield' }
])

onMounted(loadAll)

async function loadAll() {
  loading.value = true
  try {
    const [accountRows, periodRows] = await Promise.all([
      api.get<FinalAccountsAccount[]>('accounts'),
      api.get<FinalAccountsFiscalPeriod[]>('fiscal-periods')
    ])
    accounts.value = accountRows
    periods.value = periodRows
    await loadAdjustments()
  } catch (error) {
    notify(error, 'error')
  } finally {
    loading.value = false
  }
}

async function loadAdjustments() {
  loading.value = true
  try {
    const params = new URLSearchParams({ page: String(page.value), pageSize: String(pageSize.value), status: statusFilter.value })
    adjustments.value = await api.get<FinalAccountsAdjustmentList>(`ca/adjustments?${params}`)
  } catch (error) {
    notify(error, 'error')
  } finally {
    loading.value = false
  }
}

async function selectAdjustment(id: string) {
  selected.value = await api.get<FinalAccountsAdjustment>(`ca/adjustments/${id}`)
  savedPreview.value = await api.post<FinalAccountsAdjustmentPreview>(`ca/adjustments/${id}/preview`, {})
}

async function saveAdjustment() {
  saving.value = true
  try {
    const body = payload()
    selected.value = editingId.value
      ? await api.put<FinalAccountsAdjustment>(`ca/adjustments/${editingId.value}`, body)
      : await api.post<FinalAccountsAdjustment>('ca/adjustments', body)
    editingId.value = ''
    preview.value = null
    await loadAdjustments()
    notify('CA adjustment saved.', 'success')
  } catch (error) {
    notify(error, 'error')
  } finally {
    saving.value = false
  }
}

async function previewDraft() {
  previewing.value = true
  try {
    preview.value = await api.post<FinalAccountsAdjustmentPreview>('ca/adjustments/preview', payload())
  } catch (error) {
    notify(error, 'error')
  } finally {
    previewing.value = false
  }
}

async function move(id: string, action: WorkflowAction) {
  saving.value = true
  try {
    selected.value = await api.post<FinalAccountsAdjustment>(`ca/adjustments/${id}/${action}`, { notes: `${action} from CA workspace` })
    await loadAdjustments()
    notify(`Adjustment ${action} complete.`, 'success')
  } catch (error) {
    notify(error, 'error')
  } finally {
    saving.value = false
  }
}

async function postAdjustment(id: string) {
  saving.value = true
  try {
    selected.value = await api.post<FinalAccountsAdjustment>(`ca/adjustments/${id}/post`, { idempotencyKey: `ca-${id}` })
    await loadAdjustments()
    notify('Approved adjustment posted.', 'success')
  } catch (error) {
    notify(error, 'error')
  } finally {
    saving.value = false
  }
}

async function reverseAdjustment(id: string) {
  saving.value = true
  try {
    selected.value = await api.post<FinalAccountsAdjustment>(`ca/adjustments/${id}/reverse`, { reason: 'CA adjustment reversal', idempotencyKey: `ca-rev-${id}` })
    await loadAdjustments()
    notify('Posted adjustment reversed.', 'success')
  } catch (error) {
    notify(error, 'error')
  } finally {
    saving.value = false
  }
}

async function addComment() {
  if (!selected.value || !commentBody.value.trim()) return
  selected.value = await api.post<FinalAccountsAdjustment>(`ca/adjustments/${selected.value.id}/comments`, { body: commentBody.value, visibility: 'Internal' })
  commentBody.value = ''
}

async function addAttachment() {
  if (!selected.value || !attachment.fileName.trim() || !attachment.storageReference.trim()) return
  selected.value = await api.post<FinalAccountsAdjustment>(`ca/adjustments/${selected.value.id}/attachments`, { ...attachment })
  attachment.fileName = ''
  attachment.storageReference = ''
}

function editSelected() {
  if (!selected.value) return
  editingId.value = selected.value.id
  form.title = selected.value.title
  form.description = selected.value.description || ''
  form.adjustmentDate = isoDate(selected.value.adjustmentDate)
  form.fiscalPeriodId = selected.value.fiscalPeriodId || ''
  form.referenceNumber = selected.value.referenceNumber || ''
  form.autoReverse = selected.value.autoReverse
  form.autoReverseDate = selected.value.autoReverseDate ? isoDate(selected.value.autoReverseDate) : ''
  form.lines.splice(0, form.lines.length, ...selected.value.lines.map(item => ({
    accountId: item.accountId,
    debit: item.debit,
    credit: item.credit,
    narration: item.narration || '',
    statementLineKey: item.statementLineKey || ''
  })))
}

function payload(): FinalAccountsAdjustmentPayload {
  return {
    title: form.title,
    description: form.description || null,
    adjustmentDate: form.adjustmentDate,
    fiscalPeriodId: form.fiscalPeriodId || null,
    referenceNumber: form.referenceNumber || null,
    autoReverse: form.autoReverse,
    autoReverseDate: form.autoReverse ? form.autoReverseDate || null : null,
    lines: form.lines.map(line => ({
      accountId: line.accountId || EMPTY_GUID,
      debit: Number(line.debit || 0),
      credit: Number(line.credit || 0),
      narration: line.narration || null,
      statementLineKey: line.statementLineKey || null
    }))
  }
}

function addLine() {
  form.lines.push(emptyLine())
}

function removeLine(index: number) {
  if (form.lines.length > 2) form.lines.splice(index, 1)
}

function resetForm() {
  editingId.value = ''
  form.title = ''
  form.description = ''
  form.adjustmentDate = today()
  form.fiscalPeriodId = ''
  form.referenceNumber = ''
  form.autoReverse = false
  form.autoReverseDate = ''
  form.lines.splice(0, form.lines.length, emptyLine(), emptyLine())
  preview.value = null
}

function previousPage() {
  if (page.value > 1) {
    page.value--
    loadAdjustments()
  }
}

function nextPage() {
  if (page.value < totalPages.value) {
    page.value++
    loadAdjustments()
  }
}

function emptyLine(): AdjustmentFormLine {
  return { accountId: EMPTY_GUID, debit: 0, credit: 0, narration: '', statementLineKey: '' }
}

function today() {
  return new Date().toISOString().slice(0, 10)
}

function isoDate(value: string) {
  return String(value || '').slice(0, 10)
}

function money(value: number) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(Number(value || 0))
}

function statusColor(status: FinalAccountsAdjustmentStatus | string) {
  if (status === 'Posted') return 'success'
  if (status === 'Approved') return 'primary'
  if (status === 'Rejected' || status === 'Reversed') return 'error'
  if (status === 'Review' || status === 'Submitted') return 'warning'
  return 'neutral'
}

function notify(value: unknown, tone: Tone) {
  messageTone.value = tone
  message.value = value instanceof Error ? value.message : String(value || '')
}
</script>
