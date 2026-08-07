<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-dashboard-kicker">
            <UIcon name="i-lucide-database-zap" class="size-4" />
            Data diagnostics
          </p>
          <h2 class="garmetix-dashboard-title">Data Consistency</h2>
          <p class="garmetix-dashboard-subtitle">Consistency checks across inventory, documents, GST, payments and accounting, plus guarded repair actions for the ones that have one.</p>
        </div>
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
      </div>
    </div>

    <UAlert v-if="error" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />

    <section class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
      <div v-for="card in cards" :key="card.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label">{{ card.label }}</p>
        <p class="garmetix-metric-value">{{ card.value }}</p>
        <p class="garmetix-metric-caption">{{ card.detail }}</p>
      </div>
    </section>

    <section class="grid gap-4 xl:grid-cols-2">
      <div class="garmetix-section-card">
        <h3 class="garmetix-panel-title mb-3">Issue Sections</h3>
        <AdminMasterTable :columns="sectionColumns" :rows="sectionRows" empty-text="No consistency sections returned." />
      </div>
      <div class="garmetix-section-card">
        <h3 class="garmetix-panel-title mb-3">Top Issues</h3>
        <AdminMasterTable :columns="issueColumns" :rows="issueRows" empty-text="No consistency issues found." />
      </div>
    </section>

    <section class="garmetix-section-card overflow-hidden p-0">
      <div class="p-4">
        <h3 class="garmetix-panel-title">Repair Actions</h3>
        <p class="text-xs text-muted">Preview before applying. Medium/High risk actions require typed confirmation.</p>
      </div>
      <UTable :data="actionRows" :columns="actionColumns" :loading="loadingActions" class="w-full">
        <template #riskLevel-cell="{ row }">
          <UBadge variant="subtle" :color="riskColor(row.original.riskLevel)">{{ row.original.riskLevel }}</UBadge>
        </template>
        <template #actions-cell="{ row }">
          <div class="flex items-center justify-end gap-1">
            <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-eye" :loading="previewingCode === row.original.code" @click="preview(row.original)">Preview</UButton>
            <UButton size="xs" color="warning" variant="soft" icon="i-lucide-wrench" @click="openApply(row.original)">Apply</UButton>
          </div>
        </template>
      </UTable>
    </section>

    <!-- Modal: Preview/Apply Result -->
    <UModal v-model:open="resultModalOpen" :title="resultModalTitle">
      <template #body>
        <div v-if="resultModalData" class="space-y-3 text-sm">
          <div class="grid grid-cols-3 gap-3">
            <div class="garmetix-row-card"><p class="text-xs text-muted">Scanned</p><p class="font-semibold text-highlighted">{{ resultModalData.scannedRows }}</p></div>
            <div class="garmetix-row-card"><p class="text-xs text-muted">Affected</p><p class="font-semibold text-highlighted">{{ resultModalData.affectedRows }}</p></div>
            <div class="garmetix-row-card"><p class="text-xs text-muted">Applied</p><p class="font-semibold text-highlighted">{{ resultModalData.applied ? 'Yes' : 'No (preview)' }}</p></div>
          </div>
          <p class="text-muted">{{ resultModalData.message }}</p>
          <div v-if="resultModalData.changes?.length" class="max-h-72 overflow-auto rounded-md border border-default">
            <table class="w-full text-left text-xs">
              <thead class="bg-muted/30"><tr>
                <th class="p-2">Entity</th><th class="p-2">Reference</th><th class="p-2">Field</th><th class="p-2">Before</th><th class="p-2">After</th>
              </tr></thead>
              <tbody>
                <tr v-for="(change, idx) in resultModalData.changes" :key="idx" class="border-t border-default">
                  <td class="p-2">{{ change.entityType }}</td>
                  <td class="p-2">{{ change.referenceNumber || change.entityId }}</td>
                  <td class="p-2">{{ change.field }}</td>
                  <td class="p-2">{{ change.before ?? '-' }}</td>
                  <td class="p-2">{{ change.after ?? '-' }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
        <div class="mt-4 flex justify-end">
          <UButton color="neutral" variant="ghost" label="Close" @click="resultModalOpen = false" />
        </div>
      </template>
    </UModal>

    <!-- Modal: Apply confirmation -->
    <UModal v-model:open="applyModalOpen" :title="`Apply Repair - ${applyTarget?.title ?? ''}`">
      <template #body>
        <div class="space-y-4">
          <UAlert :color="applyTarget && applyTarget.riskLevel !== 'Low' ? 'error' : 'warning'" variant="subtle" icon="i-lucide-triangle-alert" :description="applyTarget?.description" />
          <UFormField label="Row Limit">
            <UInput v-model.number="applyLimit" type="number" min="1" max="5000" class="w-full" />
          </UFormField>
          <UFormField v-if="applyTarget?.requiresConfirmation" :label="`Type &quot;${applyTarget.code}&quot; to confirm`">
            <UInput v-model="applyConfirmationText" :placeholder="applyTarget?.code" class="w-full" />
          </UFormField>
          <div class="flex justify-end gap-3">
            <UButton color="neutral" variant="ghost" label="Cancel" @click="applyModalOpen = false" />
            <UButton
              color="error"
              :loading="applying"
              :disabled="applyTarget?.requiresConfirmation && applyConfirmationText !== applyTarget.code"
              label="Apply Repair"
              @click="executeApply"
            />
          </div>
        </div>
      </template>
    </UModal>
  </section>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { formatDateTime, readArray, readNumber, readText, toRows, type ApiRecord, useAdminApiClient } from '../utils/admin-api'

useHead({ title: 'Data Consistency - Garmetix Admin' })

const toast = useToast()
const { get, post } = useAdminApiClient()

const loading = ref(true)
const loadingActions = ref(true)
const error = ref('')
const summary = ref<ApiRecord | null>(null)
const run = ref<ApiRecord | null>(null)
const actions = ref<ApiRecord[]>([])

const sectionColumns = [
  { key: 'area', label: 'Area' },
  { key: 'total', label: 'Total' },
  { key: 'critical', label: 'Critical' },
  { key: 'warning', label: 'Warning' },
  { key: 'info', label: 'Info' }
]
const issueColumns = [
  { key: 'severity', label: 'Severity' },
  { key: 'area', label: 'Area' },
  { key: 'code', label: 'Code' },
  { key: 'reference', label: 'Reference' },
  { key: 'description', label: 'Description' }
]
const actionColumns = [
  { accessorKey: 'title', header: 'Repair Action' },
  { accessorKey: 'riskLevel', header: 'Risk' },
  { accessorKey: 'fixesCodesLabel', header: 'Fixes' },
  { accessorKey: 'actions', header: '', enableSorting: false }
]

const sections = computed(() => readArray(summary.value, ['sections']))
const issues = computed(() => readArray(run.value, ['issues']))
const cards = computed(() => [
  { label: 'Issues', value: readNumber(summary.value, ['issueCount', 'totalIssues']), detail: `Generated ${formatDateTime(summary.value?.generatedAt)}` },
  { label: 'Critical', value: readNumber(summary.value, ['criticalCount']), detail: 'Must review before go-live' },
  { label: 'Warnings', value: readNumber(summary.value, ['warningCount']), detail: 'Operational risk' },
  { label: 'Info', value: readNumber(summary.value, ['infoCount']), detail: 'Cleanup guidance' }
])
const sectionRows = computed(() => sections.value.map(item => ({
  area: readText(item, ['area']),
  total: readNumber(item, ['issueCount', 'total']),
  critical: readNumber(item, ['criticalCount']),
  warning: readNumber(item, ['warningCount']),
  info: readNumber(item, ['infoCount'])
})))
const issueRows = computed(() => issues.value.slice(0, 50).map(item => ({
  severity: readText(item, ['severity']),
  area: readText(item, ['area']),
  code: readText(item, ['checkCode']),
  reference: readText(item, ['referenceNumber', 'entityId']),
  description: readText(item, ['description'])
})))
const actionRows = computed(() => actions.value.map(item => ({
  code: readText(item, ['code']),
  title: readText(item, ['title']),
  description: readText(item, ['description']),
  riskLevel: readText(item, ['riskLevel'], 'Low'),
  requiresConfirmation: Boolean(item.requiresConfirmation),
  fixesCodesLabel: readArray(item, ['fixesCheckCodes']).join(', ') || '-'
})))

function riskColor(level: string) {
  if (level === 'High') return 'error'
  if (level === 'Medium') return 'warning'
  return 'neutral'
}

async function refresh() {
  loading.value = true
  loadingActions.value = true
  error.value = ''
  try {
    const [summaryData, issueData, actionData] = await Promise.allSettled([
      get<unknown>('data-consistency/summary'),
      get<unknown>('data-consistency/issues'),
      get<unknown>('data-consistency/repairs/actions')
    ])
    if (summaryData.status === 'fulfilled' && summaryData.value && typeof summaryData.value === 'object') summary.value = summaryData.value as ApiRecord
    if (issueData.status === 'fulfilled' && issueData.value && typeof issueData.value === 'object') run.value = issueData.value as ApiRecord
    if (actionData.status === 'fulfilled') actions.value = toRows(actionData.value)
    const failed = [summaryData, issueData, actionData].filter(item => item.status === 'rejected').length
    if (failed) error.value = `${failed} data consistency request(s) could not be loaded.`
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load data consistency.'
  } finally {
    loading.value = false
    loadingActions.value = false
  }
}

const resultModalOpen = ref(false)
const resultModalTitle = ref('')
const resultModalData = ref<ApiRecord | null>(null)
const previewingCode = ref('')

async function preview(action: ApiRecord) {
  previewingCode.value = String(action.code)
  try {
    const result = await post<ApiRecord>('data-consistency/repairs/preview', { actionCode: action.code, limit: 100 })
    resultModalTitle.value = `Preview - ${action.title}`
    resultModalData.value = result
    resultModalOpen.value = true
  } catch (caught) {
    toast.add({ title: 'Error', description: caught instanceof Error ? caught.message : 'Preview failed.', color: 'error' })
  } finally {
    previewingCode.value = ''
  }
}

const applyModalOpen = ref(false)
const applyTarget = ref<ApiRecord | null>(null)
const applyLimit = ref(100)
const applyConfirmationText = ref('')
const applying = ref(false)

function openApply(action: ApiRecord) {
  applyTarget.value = action
  applyLimit.value = 100
  applyConfirmationText.value = ''
  applyModalOpen.value = true
}

async function executeApply() {
  if (!applyTarget.value) return
  applying.value = true
  try {
    const result = await post<ApiRecord>('data-consistency/repairs/apply', {
      actionCode: applyTarget.value.code,
      limit: applyLimit.value,
      confirm: true
    })
    applyModalOpen.value = false
    resultModalTitle.value = `Applied - ${applyTarget.value.title}`
    resultModalData.value = result
    resultModalOpen.value = true
    toast.add({ title: 'Repair Applied', description: readText(result, ['message'], 'Repair applied.'), color: 'success' })
    await refresh()
  } catch (caught) {
    toast.add({ title: 'Error', description: caught instanceof Error ? caught.message : 'Apply failed.', color: 'error' })
  } finally {
    applying.value = false
  }
}

onMounted(refresh)
</script>
