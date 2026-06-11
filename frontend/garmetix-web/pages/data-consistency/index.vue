<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const feedback = useUiFeedback()
const config = useRuntimeConfig()
const isAuthenticated = computed(() => auth.isAuthenticated.value)

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const loading = ref(false)
const summary = ref<any | null>(null)
const issues = ref<any[]>([])
const filters = reactive({
  severity: 'All',
  area: 'All'
})

const repairActions = ref<any[]>([])
const selectedRepairAction = ref('BACKFILL_SALE_ITEM_SNAPSHOTS')
const repairLimit = ref(100)
const repairConfirm = ref(false)
const repairReason = ref('')
const repairLoading = ref(false)
const repairPreview = ref<any | null>(null)

const severityOptions = [
  { label: 'All', value: 'All' },
  { label: 'Critical', value: 'Critical' },
  { label: 'Warning', value: 'Warning' },
  { label: 'Info', value: 'Info' }
]

const areaOptions = computed(() => {
  const areas = Array.from(new Set((summary.value?.sections || []).map((section: any) => section.area))).sort()
  return [{ label: 'All', value: 'All' }, ...areas.map((area) => ({ label: area, value: area }))]
})

const repairActionOptions = computed(() => repairActions.value.map((action) => ({
  label: `${action.title} (${action.riskLevel})`,
  value: action.code
})))

const selectedRepair = computed(() => repairActions.value.find((action) => action.code === selectedRepairAction.value))

const filteredIssues = computed(() => issues.value.filter((issue) => {
  const severityMatch = filters.severity === 'All' || issue.severity === filters.severity
  const areaMatch = filters.area === 'All' || issue.area === filters.area
  return severityMatch && areaMatch
}))

const healthBadge = computed(() => {
  if (!summary.value) {
    return { label: 'Not checked', color: 'neutral' as const, icon: 'i-lucide-circle-help' }
  }
  if (summary.value.criticalIssues > 0) {
    return { label: 'Critical action required', color: 'error' as const, icon: 'i-lucide-circle-alert' }
  }
  if (summary.value.warningIssues > 0) {
    return { label: 'Warnings found', color: 'warning' as const, icon: 'i-lucide-triangle-alert' }
  }
  return { label: 'No issues found', color: 'success' as const, icon: 'i-lucide-circle-check' }
})

function query() {
  const params = new URLSearchParams()
  if (filters.severity !== 'All') {
    params.set('severity', filters.severity)
  }
  if (filters.area !== 'All') {
    params.set('area', filters.area)
  }
  return params.toString()
}

async function refreshShell() {
  if (!auth.isAuthenticated.value) {
    return
  }
  const [companyRows, storeRows] = await Promise.all([
    api.list<any>('companies'),
    api.list<any>('stores')
  ])
  companies.value = companyRows
  stores.value = storeRows
}

async function loadRepairActions() {
  try {
    repairActions.value = await api.get<any[]>('data-consistency/repairs/actions')
    if (repairActions.value.length && !repairActions.value.some((action) => action.code === selectedRepairAction.value)) {
      selectedRepairAction.value = repairActions.value[0].code
    }
  } catch (error: any) {
    feedback.fromError(error, 'Unable to load repair actions')
  }
}

async function loadChecks() {
  loading.value = true
  try {
    const result = await api.get<any>('data-consistency/issues')
    summary.value = result.summary
    issues.value = result.issues || []
    feedback.notify('Data consistency check completed', `${result.summary?.totalIssues || 0} issue(s) found.`, result.summary?.criticalIssues ? 'warning' : 'success')
  } catch (error: any) {
    feedback.fromError(error, 'Unable to run data consistency checks')
  } finally {
    loading.value = false
  }
}

async function refreshAll() {
  await refreshShell()
  await loadRepairActions()
  await loadChecks()
}

async function previewRepair() {
  repairLoading.value = true
  try {
    repairPreview.value = await api.create<any>('data-consistency/repairs/preview', {
      actionCode: selectedRepairAction.value,
      limit: Number(repairLimit.value || 100),
      confirm: repairConfirm.value,
      reason: repairReason.value || null
    })
    feedback.notify('Repair preview ready', `${repairPreview.value?.changes?.length || 0} field change(s) found.`, 'info')
  } catch (error: any) {
    feedback.fromError(error, 'Repair preview failed')
  } finally {
    repairLoading.value = false
  }
}

async function applyRepair() {
  repairLoading.value = true
  try {
    const result = await api.create<any>('data-consistency/repairs/apply', {
      actionCode: selectedRepairAction.value,
      limit: Number(repairLimit.value || 100),
      confirm: repairConfirm.value,
      reason: repairReason.value || null
    })
    repairPreview.value = result
    feedback.notify('Repair applied', `${result.affectedRows || 0} row(s) updated.`, 'success')
    await loadChecks()
  } catch (error: any) {
    feedback.fromError(error, 'Repair apply failed')
  } finally {
    repairLoading.value = false
  }
}

async function downloadCsv() {
  try {
    const qs = query()
    const path = `${config.public.apiBase}/data-consistency/csv${qs ? `?${qs}` : ''}`
    const response = await fetch(path, { headers: api.authHeaders() })
    if (!response.ok) {
      throw new Error(await response.text())
    }
    const blob = await response.blob()
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = `Garmetix-data-consistency-${new Date().toISOString().slice(0, 10)}.csv`
    link.click()
    URL.revokeObjectURL(url)
  } catch (error: any) {
    feedback.fromError(error, 'CSV download failed')
  }
}

function severityColor(severity: string) {
  if (severity === 'Critical') return 'error'
  if (severity === 'Warning') return 'warning'
  return 'neutral'
}

function money(value: number | null | undefined) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(Number(value || 0))
}

function repairRiskColor(risk: string | undefined) {
  if (risk === 'High') return 'error'
  if (risk === 'Medium') return 'warning'
  return 'success'
}

function valueOrDash(value: any) {
  return value === null || value === undefined || value === '' ? '-' : value
}

function shortId(value: string | null | undefined) {
  return value ? value.slice(0, 8) : '-'
}

function formatDate(value: string | null | undefined) {
  return value ? new Date(value).toLocaleString('en-IN') : '-'
}

onMounted(async () => {
  auth.restore()
  await refreshShell()
  if (auth.isAuthenticated.value) {
    await loadRepairActions()
    await loadChecks()
  }
})
</script>

<template>
  <AuthScreen v-if="!isAuthenticated" @authenticated="refreshAll" />

  <AppShell
    v-else
    title="Data Consistency"
    :companies="companies"
    :stores="stores"
    @refresh="refreshAll"
  >
    <section class="consistency-page">
      <UCard class="planner-card">
        <div class="consistency-header">
          <div>
            <p class="eyebrow">Data consistency</p>
            <h1>Data Consistency Verification</h1>
            <p>Run production-readiness checks for stock, document numbers, GST totals, payments, and accounting journals.</p>
          </div>
          <div class="header-actions">
            <UBadge :label="healthBadge.label" :color="healthBadge.color" :icon="healthBadge.icon" variant="subtle" />
            <UButton icon="i-lucide-refresh-cw" label="Run checks" :loading="loading" @click="loadChecks" />
            <UButton icon="i-lucide-download" label="CSV" variant="subtle" @click="downloadCsv" />
          </div>
        </div>
        <div class="filters">
          <UFormField label="Severity">
            <USelect v-model="filters.severity" :items="severityOptions" />
          </UFormField>
          <UFormField label="Area">
            <USelect v-model="filters.area" :items="areaOptions" />
          </UFormField>
          <UFormField label="Generated at">
            <UInput :model-value="formatDate(summary?.generatedAtUtc)" disabled />
          </UFormField>
        </div>
      </UCard>

      <div class="metric-grid">
        <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-list-checks" color="primary" variant="subtle" /><div><p>Total Issues</p><strong>{{ summary?.totalIssues || 0 }}</strong><span>{{ filteredIssues.length }} visible after filter</span></div></div></UCard>
        <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-circle-alert" color="error" variant="subtle" /><div><p>Critical</p><strong>{{ summary?.criticalIssues || 0 }}</strong><span>Fix before production</span></div></div></UCard>
        <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-triangle-alert" color="warning" variant="subtle" /><div><p>Warnings</p><strong>{{ summary?.warningIssues || 0 }}</strong><span>Review and clean</span></div></div></UCard>
        <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-info" color="neutral" variant="subtle" /><div><p>Info</p><strong>{{ summary?.infoIssues || 0 }}</strong><span>Optional cleanup</span></div></div></UCard>
      </div>

      <UCard class="planner-card">
        <template #header>
          <div class="section-header"><div><h2>Check Summary</h2><p>Issue counts by functional area.</p></div></div>
        </template>
        <div class="section-grid">
          <div v-for="section in summary?.sections || []" :key="section.area" class="section-card">
            <strong>{{ section.area }}</strong>
            <span>{{ section.totalIssues }} total</span>
            <div class="section-badges">
              <UBadge :label="`${section.criticalIssues} critical`" color="error" variant="subtle" />
              <UBadge :label="`${section.warningIssues} warning`" color="warning" variant="subtle" />
              <UBadge :label="`${section.infoIssues} info`" color="neutral" variant="subtle" />
            </div>
          </div>
          <div v-if="!(summary?.sections || []).length" class="empty-state">No check results yet.</div>
        </div>
      </UCard>

      <UCard class="planner-card">
        <template #header>
          <div class="section-header">
            <div>
              <h2>Controlled Repair Tools</h2>
              <p>Preview safe repair actions before applying changes. High/medium risk actions require confirmation.</p>
            </div>
            <UBadge v-if="selectedRepair" :label="selectedRepair.riskLevel" :color="repairRiskColor(selectedRepair.riskLevel)" variant="subtle" />
          </div>
        </template>

        <div class="repair-grid">
          <UFormField label="Repair action">
            <USelect v-model="selectedRepairAction" :items="repairActionOptions" @change="repairPreview = null" />
          </UFormField>
          <UFormField label="Row limit">
            <UInput v-model="repairLimit" type="number" min="1" max="500" />
          </UFormField>
          <UFormField label="Reason / operator note">
            <UInput v-model="repairReason" placeholder="Optional note for this repair run" />
          </UFormField>
        </div>

        <UAlert
          v-if="selectedRepair"
          class="repair-alert"
          icon="i-lucide-wrench"
          :color="repairRiskColor(selectedRepair.riskLevel)"
          variant="subtle"
          :title="selectedRepair.title"
          :description="`${selectedRepair.description} Fixes: ${(selectedRepair.fixesCheckCodes || []).join(', ')}`"
        />

        <div class="repair-actions">
          <UCheckbox v-model="repairConfirm" label="I confirm this repair can update data" />
          <UButton icon="i-lucide-search-check" label="Preview repair" variant="subtle" :loading="repairLoading" @click="previewRepair" />
          <UButton icon="i-lucide-wrench" label="Apply repair" color="warning" :loading="repairLoading" :disabled="selectedRepair?.requiresConfirmation && !repairConfirm" @click="applyRepair" />
        </div>

        <div v-if="repairPreview" class="repair-result">
          <div class="repair-result-header">
            <strong>{{ repairPreview.applied ? 'Applied result' : 'Preview result' }}</strong>
            <span>{{ repairPreview.affectedRows || 0 }} row(s), {{ repairPreview.changes?.length || 0 }} field change(s)</span>
          </div>
          <div class="table-wrap">
            <table class="repair-table">
              <thead><tr><th>Entity</th><th>Reference</th><th>Field</th><th>Before</th><th>After</th></tr></thead>
              <tbody>
                <tr v-for="change in repairPreview.changes || []" :key="`${change.entityType}-${change.entityId}-${change.field}-${change.after}`">
                  <td>{{ change.entityType }}<br><span>{{ shortId(change.entityId) }}</span></td>
                  <td>{{ valueOrDash(change.referenceNumber) }}</td>
                  <td>{{ change.field }}</td>
                  <td>{{ valueOrDash(change.before) }}</td>
                  <td>{{ valueOrDash(change.after) }}</td>
                </tr>
                <tr v-if="!(repairPreview.changes || []).length"><td colspan="5" class="empty">No changes found for this action and limit.</td></tr>
              </tbody>
            </table>
          </div>
        </div>
      </UCard>

      <UCard class="planner-card">
        <template #header>
          <div class="section-header"><div><h2>Issue Details</h2><p>Filtered issue list with reference number and expected/actual values.</p></div></div>
        </template>
        <div class="table-wrap">
          <table>
            <thead><tr><th>Severity</th><th>Area</th><th>Check</th><th>Reference</th><th>Entity</th><th>Expected</th><th>Actual</th><th>Diff</th><th>Description</th></tr></thead>
            <tbody>
              <tr v-for="issue in filteredIssues" :key="`${issue.checkCode}-${issue.entityId}-${issue.referenceNumber}`">
                <td><UBadge :label="issue.severity" :color="severityColor(issue.severity)" variant="subtle" /></td>
                <td>{{ issue.area }}</td>
                <td><strong>{{ issue.title }}</strong><br><span>{{ issue.checkCode }}</span></td>
                <td>{{ valueOrDash(issue.referenceNumber) }}</td>
                <td>{{ valueOrDash(issue.entityType) }}<br><span>{{ shortId(issue.entityId) }}</span></td>
                <td>{{ issue.expectedValue === null || issue.expectedValue === undefined ? '-' : money(issue.expectedValue) }}</td>
                <td>{{ issue.actualValue === null || issue.actualValue === undefined ? '-' : money(issue.actualValue) }}</td>
                <td>{{ issue.difference === null || issue.difference === undefined ? '-' : issue.difference }}</td>
                <td>{{ issue.description }}</td>
              </tr>
              <tr v-if="!filteredIssues.length"><td colspan="9" class="empty">No issues found for this filter.</td></tr>
            </tbody>
          </table>
        </div>
      </UCard>
    </section>
  </AppShell>
</template>

<style scoped>
.consistency-page { display: grid; gap: 1rem; }
.consistency-header, .section-header { display: flex; justify-content: space-between; gap: 1rem; align-items: flex-start; }
.consistency-header h1, .section-header h2 { margin: 0; }
.consistency-header p, .section-header p { margin: .35rem 0 0; color: rgb(var(--color-gray-500)); }
.eyebrow { text-transform: uppercase; letter-spacing: .12em; font-size: .72rem; font-weight: 700; color: rgb(var(--color-primary-500)); }
.header-actions { display: flex; gap: .6rem; align-items: center; flex-wrap: wrap; justify-content: flex-end; }
.filters { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 1rem; margin-top: 1rem; }
.metric-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 1rem; }
.section-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(230px, 1fr)); gap: .8rem; }
.repair-grid { display: grid; grid-template-columns: 2fr minmax(120px, .5fr) 2fr; gap: 1rem; align-items: end; }
.repair-alert { margin-top: 1rem; }
.repair-actions { display: flex; gap: .75rem; flex-wrap: wrap; align-items: center; margin-top: 1rem; }
.repair-result { margin-top: 1rem; display: grid; gap: .75rem; }
.repair-result-header { display: flex; justify-content: space-between; gap: 1rem; align-items: center; }
.repair-result-header span { color: rgb(var(--color-gray-500)); }
.repair-table { min-width: 850px; }
.section-card { border: 1px solid rgb(var(--color-gray-200)); border-radius: 1rem; padding: 1rem; display: grid; gap: .55rem; }
.section-card span { color: rgb(var(--color-gray-500)); }
.section-badges { display: flex; gap: .4rem; flex-wrap: wrap; }
.table-wrap { overflow: auto; }
table { width: 100%; border-collapse: collapse; min-width: 1100px; }
th, td { padding: .65rem .7rem; border-bottom: 1px solid rgb(var(--color-gray-200)); text-align: left; font-size: .84rem; vertical-align: top; }
th { font-size: .75rem; text-transform: uppercase; letter-spacing: .05em; color: rgb(var(--color-gray-500)); background: rgb(var(--color-gray-50)); }
td span { color: rgb(var(--color-gray-500)); font-size: .78rem; }
.empty, .empty-state { text-align: center; color: rgb(var(--color-gray-500)); padding: 1.5rem; }
.dark th { background: rgb(var(--color-gray-900)); }
.dark th, .dark td, .dark .section-card { border-color: rgb(var(--color-gray-800)); }
@media (max-width: 720px) { .consistency-header, .section-header { flex-direction: column; } .header-actions { justify-content: flex-start; } .repair-grid { grid-template-columns: 1fr; } }
</style>
