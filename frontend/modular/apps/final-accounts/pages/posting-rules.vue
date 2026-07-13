<template>
  <section class="space-y-5">
    <div class="flex flex-wrap items-start justify-between gap-3">
      <div>
        <p class="garmetix-kicker"><UIcon name="i-lucide-route" class="size-4" /> Final Accounts</p>
        <h1 class="garmetix-dashboard-title">Posting Rules</h1>
      </div>
      <div class="flex flex-wrap items-center gap-2">
        <USelect v-model="sourceFilter" :items="sourceFilterItems" class="w-44" />
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="loadAll">Refresh</UButton>
        <UButton icon="i-lucide-file-search" color="neutral" variant="soft" :loading="previewing" @click="previewPosting">Preview</UButton>
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

    <UAlert
      v-if="preview"
      :icon="preview.canPost ? 'i-lucide-circle-check' : 'i-lucide-circle-alert'"
      :color="preview.canPost ? 'success' : 'warning'"
      variant="subtle"
      :title="preview.canPost ? 'Preview Ready' : 'Preview Blocked'"
      :description="previewSummary"
    />

    <div class="grid gap-4 2xl:grid-cols-[minmax(22rem,28rem)_1fr]">
      <UCard :ui="{ body: 'p-5' }">
        <form class="space-y-4" @submit.prevent="saveMapping">
          <div class="flex items-center justify-between gap-3">
            <h2 class="text-base font-semibold text-highlighted">{{ mappingForm.mappingId ? 'Edit Mapping' : 'Configure Mapping' }}</h2>
            <UButton type="button" icon="i-lucide-eraser" color="neutral" variant="ghost" size="sm" @click="resetForm">Clear</UButton>
          </div>

          <UFormField label="Requirement" name="mappingKey">
            <USelectMenu v-model="selectedRequirementKey" :items="requirementSelectItems" value-key="value" class="w-full" @update:model-value="selectRequirement" />
          </UFormField>

          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Source" name="sourceType">
              <UInput v-model="mappingForm.sourceType" icon="i-lucide-plug" readonly />
            </UFormField>
            <UFormField label="Direction" name="direction">
              <UInput v-model="mappingForm.direction" icon="i-lucide-arrow-left-right" readonly />
            </UFormField>
          </div>

          <UFormField label="Mapping key" name="mappingKey">
            <UInput v-model="mappingForm.mappingKey" icon="i-lucide-key-round" readonly />
          </UFormField>

          <UFormField label="Display name" name="displayName">
            <UInput v-model="mappingForm.displayName" icon="i-lucide-tag" required />
          </UFormField>

          <UFormField label="Account" name="accountId">
            <USelectMenu v-model="mappingForm.accountId" :items="accountSelectItems" value-key="value" class="w-full" />
          </UFormField>

          <UFormField label="Notes" name="notes">
            <UTextarea v-model="mappingForm.notes" :rows="3" />
          </UFormField>

          <USwitch v-model="mappingForm.isActive" label="Active" />

          <UButton type="submit" icon="i-lucide-save" :loading="saving" block>{{ mappingForm.mappingId ? 'Save Mapping' : 'Create Mapping' }}</UButton>
        </form>
      </UCard>

      <UCard class="overflow-hidden" :ui="{ body: 'p-0' }">
        <div class="flex flex-wrap items-center gap-2 border-b border-default p-3">
          <UInput v-model="search" icon="i-lucide-search" placeholder="Search mapping key, category or account" class="w-full sm:w-80" />
          <USelect v-model="statusFilter" :items="statusFilterItems" class="w-full sm:w-40" />
        </div>

        <UTable :data="filteredRequirements" :columns="requirementColumns" :loading="loading" class="w-full">
          <template #mappingKey-cell="{ row }">
            <span class="font-mono text-sm">{{ row.original.mappingKey }}</span>
          </template>
          <template #mappingCategory-cell="{ row }">
            <UBadge color="neutral" variant="subtle">{{ row.original.mappingCategory }}</UBadge>
          </template>
          <template #account-cell="{ row }">
            <span v-if="row.original.accountCode" class="font-mono text-sm">{{ row.original.accountCode }}</span>
            <span class="ml-2">{{ row.original.accountName || '-' }}</span>
          </template>
          <template #status-cell="{ row }">
            <UBadge :color="statusColor(row.original.status)" variant="subtle">{{ row.original.status }}</UBadge>
          </template>
          <template #actions-cell="{ row }">
            <div class="flex justify-end gap-1">
              <UTooltip text="Configure Mapping">
                <UButton icon="i-lucide-pencil" color="primary" variant="ghost" size="sm" @click="editRequirement(row.original)" />
              </UTooltip>
            </div>
          </template>
        </UTable>
      </UCard>
    </div>

    <div class="grid gap-4 xl:grid-cols-2">
      <div class="rounded-md border border-default p-4">
        <h2 class="text-base font-semibold text-highlighted">Rule Versions</h2>
        <div class="mt-3 grid gap-2">
          <div v-for="rule in rules" :key="`${rule.sourceType}-${rule.ruleCode}-${rule.version}`" class="flex items-center justify-between gap-3 rounded-md border border-default p-3">
            <div class="min-w-0">
              <p class="truncate text-sm font-medium text-highlighted">{{ rule.name }}</p>
              <p class="truncate text-xs text-muted">{{ rule.sourceType }} | {{ rule.ruleCode }}</p>
            </div>
            <UBadge color="primary" variant="subtle">{{ rule.version }}</UBadge>
          </div>
        </div>
      </div>

      <div class="rounded-md border border-default p-4">
        <h2 class="text-base font-semibold text-highlighted">Preview Lines</h2>
        <div class="mt-3 grid gap-2">
          <div v-for="line in preview?.lines || []" :key="line.mappingKey" class="rounded-md border border-default p-3">
            <div class="flex items-center justify-between gap-3">
              <span class="font-mono text-sm text-highlighted">{{ line.mappingKey }}</span>
              <UBadge color="neutral" variant="subtle">{{ line.direction }}</UBadge>
            </div>
            <p class="mt-1 text-sm text-muted">{{ line.accountCode || '-' }} {{ line.accountName || '' }}</p>
          </div>
          <p v-if="!preview?.lines?.length" class="text-sm text-muted">No preview lines yet.</p>
        </div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import {
  type FinalAccountsAccount,
  type FinalAccountsAccountMapping,
  type FinalAccountsAccountMappingPayload,
  type FinalAccountsMappingRequirement,
  type FinalAccountsMappingSourceType,
  type FinalAccountsMappingValidation,
  type FinalAccountsPostingPreview,
  type FinalAccountsPostingPreviewPayload,
  type FinalAccountsPostingRule,
  useFinalAccountsApiClient
} from '../utils/final-accounts-api'

useHead({ title: 'Posting Rules' })

type SelectItem = { label: string; value: string }
type Tone = 'success' | 'warning' | 'error' | 'neutral'

const api = useFinalAccountsApiClient()
const sourceTypes: FinalAccountsMappingSourceType[] = ['CashBank', 'Sales', 'Purchase', 'Inventory', 'Gst', 'Payroll', 'Expense', 'InterStore', 'Adjustment']
const sourceFilterItems = ['All', ...sourceTypes]
const statusFilterItems = ['All', 'Mapped', 'Missing', 'Invalid', 'Inactive', 'Optional']
const loading = ref(false)
const saving = ref(false)
const previewing = ref(false)
const sourceFilter = ref('All')
const statusFilter = ref('All')
const search = ref('')
const message = ref('')
const messageTone = ref<Tone>('neutral')
const rules = ref<FinalAccountsPostingRule[]>([])
const validation = ref<FinalAccountsMappingValidation | null>(null)
const accounts = ref<FinalAccountsAccount[]>([])
const mappings = ref<FinalAccountsAccountMapping[]>([])
const preview = ref<FinalAccountsPostingPreview | null>(null)
const selectedRequirementKey = ref('')

const mappingForm = reactive({
  mappingId: '',
  sourceType: 'Sales',
  mappingKey: '',
  displayName: '',
  direction: '',
  accountId: '',
  isActive: true,
  notes: ''
})

const messageTitle = computed(() => messageTone.value === 'success' ? 'Saved' : messageTone.value === 'error' ? 'Posting rules unavailable' : messageTone.value === 'warning' ? 'Review' : 'Posting rules')
const messageIcon = computed(() => messageTone.value === 'success' ? 'i-lucide-circle-check' : messageTone.value === 'error' ? 'i-lucide-circle-alert' : messageTone.value === 'warning' ? 'i-lucide-triangle-alert' : 'i-lucide-info')
const metricCards = computed(() => [
  { label: 'Rules', value: validation.value?.ruleCount ?? rules.value.length, icon: 'i-lucide-route' },
  { label: 'Requirements', value: validation.value?.requirementCount ?? 0, icon: 'i-lucide-list-checks' },
  { label: 'Mapped', value: validation.value?.mappedCount ?? 0, icon: 'i-lucide-circle-check' },
  { label: 'Issues', value: validation.value?.issueCount ?? 0, icon: 'i-lucide-shield-alert' }
])
const requirements = computed(() => validation.value?.requirements || [])
const filteredRequirements = computed(() => {
  const term = search.value.trim().toLowerCase()
  return requirements.value
    .filter(item => statusFilter.value === 'All' || item.status === statusFilter.value)
    .filter(item => !term
      || item.mappingKey.toLowerCase().includes(term)
      || item.displayName.toLowerCase().includes(term)
      || item.mappingCategory.toLowerCase().includes(term)
      || (item.accountCode || '').toLowerCase().includes(term)
      || (item.accountName || '').toLowerCase().includes(term))
})
const requirementSelectItems = computed<SelectItem[]>(() => requirements.value.map(item => ({
  label: `${item.sourceType} | ${item.mappingKey} (${item.status})`,
  value: `${item.sourceType}|${item.mappingKey}`
})))
const accountSelectItems = computed<SelectItem[]>(() => [
  { label: 'Select account', value: '' },
  ...accounts.value.filter(item => item.isActive).map(item => ({ label: `${item.code} - ${item.name}`, value: item.id }))
])
const previewSummary = computed(() => {
  if (!preview.value) return ''
  const issueText = preview.value.issues.map(item => item.message).join(' ')
  return issueText || `Rule ${preview.value.ruleCode} ${preview.value.ruleVersion}; source hash ${preview.value.sourceHash}; mapping version ${preview.value.mappingVersion}.`
})

const requirementColumns = [
  { accessorKey: 'mappingKey', header: 'Key' },
  { accessorKey: 'displayName', header: 'Name' },
  { accessorKey: 'mappingCategory', header: 'Category' },
  { accessorKey: 'direction', header: 'Direction' },
  { accessorKey: 'account', header: 'Account' },
  { accessorKey: 'status', header: 'Status' },
  { accessorKey: 'actions', header: '', enableSorting: false }
]

watch(sourceFilter, () => {
  resetForm()
  loadAll()
})

async function loadAll() {
  loading.value = true
  message.value = ''
  try {
    const suffix = sourceFilter.value === 'All' ? '' : `?sourceType=${encodeURIComponent(sourceFilter.value)}`
    const [ruleRows, validationRow, accountRows, mappingRows] = await Promise.all([
      api.get<FinalAccountsPostingRule[]>(`posting-rules${suffix}`),
      api.get<FinalAccountsMappingValidation>(`posting-rules/mapping-validation${suffix}`),
      api.get<FinalAccountsAccount[]>('accounts'),
      api.get<FinalAccountsAccountMapping[]>('account-mappings')
    ])
    rules.value = ruleRows
    validation.value = validationRow
    accounts.value = accountRows
    mappings.value = mappingRows
    if (!selectedRequirementKey.value && requirements.value[0]) {
      editRequirement(requirements.value[0])
    }
  } catch (err) {
    showError(err, 'Posting rules could not be loaded.')
  } finally {
    loading.value = false
  }
}

async function saveMapping() {
  if (!mappingForm.mappingKey || !mappingForm.sourceType) return
  saving.value = true
  message.value = ''
  try {
    const payload = mappingPayload()
    if (mappingForm.mappingId) {
      await api.put<FinalAccountsAccountMapping>(`account-mappings/${mappingForm.mappingId}`, payload)
    } else {
      await api.post<FinalAccountsAccountMapping>('account-mappings', payload)
    }
    messageTone.value = 'success'
    message.value = 'Posting mapping saved.'
    await loadAll()
  } catch (err) {
    showError(err, 'Posting mapping could not be saved.')
  } finally {
    saving.value = false
  }
}

async function previewPosting() {
  const sourceType = sourceFilter.value === 'All' ? (rules.value[0]?.sourceType || 'Sales') : sourceFilter.value
  previewing.value = true
  message.value = ''
  try {
    const payload: FinalAccountsPostingPreviewPayload = {
      sourceType,
      ruleCode: 'Standard',
      ruleVersion: 'GarmentRetail.v1',
      sourceReference: `BS-04-${sourceType}`,
      mappingKeys: requirements.value.filter(item => item.sourceType === sourceType && item.isRequired).map(item => item.mappingKey)
    }
    preview.value = await api.post<FinalAccountsPostingPreview>('posting/preview', payload)
    if (!preview.value.canPost) {
      messageTone.value = 'warning'
      message.value = previewSummary.value
    }
  } catch (err) {
    showError(err, 'Posting preview could not be generated.')
  } finally {
    previewing.value = false
  }
}

function editRequirement(row: FinalAccountsMappingRequirement) {
  selectedRequirementKey.value = `${row.sourceType}|${row.mappingKey}`
  mappingForm.mappingId = row.mappingId || ''
  mappingForm.sourceType = row.sourceType
  mappingForm.mappingKey = row.mappingKey
  mappingForm.displayName = row.displayName
  mappingForm.direction = row.direction
  mappingForm.accountId = row.accountId || ''
  mappingForm.isActive = row.mappingActive || !row.mappingId
  mappingForm.notes = row.issueMessage || ''
}

function selectRequirement(value: string) {
  const [sourceType, mappingKey] = String(value || '').split('|')
  const row = requirements.value.find(item => item.sourceType === sourceType && item.mappingKey === mappingKey)
  if (row) editRequirement(row)
}

function resetForm() {
  selectedRequirementKey.value = ''
  mappingForm.mappingId = ''
  mappingForm.sourceType = sourceFilter.value === 'All' ? 'Sales' : sourceFilter.value
  mappingForm.mappingKey = ''
  mappingForm.displayName = ''
  mappingForm.direction = ''
  mappingForm.accountId = ''
  mappingForm.isActive = true
  mappingForm.notes = ''
}

function mappingPayload(): FinalAccountsAccountMappingPayload {
  return {
    sourceType: mappingForm.sourceType as FinalAccountsMappingSourceType,
    mappingKey: mappingForm.mappingKey,
    displayName: mappingForm.displayName,
    accountId: mappingForm.accountId,
    isRequired: true,
    isActive: mappingForm.isActive,
    notes: blankToNull(mappingForm.notes)
  }
}

function statusColor(status: string) {
  if (status === 'Mapped') return 'success'
  if (status === 'Missing') return 'warning'
  if (status === 'Invalid' || status === 'Inactive') return 'error'
  return 'neutral'
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
