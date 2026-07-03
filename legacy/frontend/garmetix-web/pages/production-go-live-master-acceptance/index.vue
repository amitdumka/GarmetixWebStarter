<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const workspace = useWorkspace()
const feedback = useUiFeedback()
const config = useRuntimeConfig()

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const report = ref<any | null>(null)
const loading = ref(false)
const exporting = ref(false)
const loadError = ref('')
const today = new Date()
const filters = reactive({ from: inputDate(new Date(today.getFullYear(), today.getMonth(), 1)), to: inputDate(today) })

useHead({ title: 'Production Go-Live Master Acceptance | Garmetix' })

const selectedCompanyId = computed(() => workspace.companyId.value || companies.value[0]?.id || '')
const selectedStoreGroupId = computed(() => workspace.storeGroupId.value || '')
const selectedStoreId = computed(() => workspace.storeId.value || '')
const metrics = computed(() => report.value?.metrics || [])
const checks = computed(() => report.value?.checks || [])
const issues = computed(() => report.value?.issues || [])
const closeoutChecklist = computed(() => report.value?.closeoutChecklist || [])
const knownLimitations = computed(() => report.value?.knownLimitations || [])
const nextModules = computed(() => report.value?.nextModuleCandidates || [])
const backupProof = computed(() => report.value?.manualBackupRestoreProof || [])

function inputDate(date: Date) { return date.toISOString().slice(0, 10) }
function query(extra: Record<string, string> = {}) {
  const params = new URLSearchParams()
  if (selectedCompanyId.value) params.set('companyId', selectedCompanyId.value)
  if (selectedStoreGroupId.value) params.set('storeGroupId', selectedStoreGroupId.value)
  if (selectedStoreId.value) params.set('storeId', selectedStoreId.value)
  if (filters.from) params.set('from', filters.from)
  if (filters.to) params.set('to', filters.to)
  Object.entries(extra).forEach(([key, value]) => { if (value) params.set(key, value) })
  return params.toString()
}
function money(value: number | string | null | undefined) { return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(Number(value || 0)) }
function numberValue(value: number | string | null | undefined) { return new Intl.NumberFormat('en-IN', { maximumFractionDigits: 2 }).format(Number(value || 0)) }
function formatDate(value: string | null | undefined) { return value ? new Date(value).toLocaleDateString('en-IN') : '-' }
function metricValue(metric: any) { return metric?.amount !== null && metric?.amount !== undefined ? money(metric.amount) : numberValue(metric?.count) }
function statusColor(status: string | undefined) {
  if (status === 'Complete' || status === 'Ready for Owner Sign-off') return 'success'
  if (status === 'Review' || status === 'Warning') return 'warning'
  if (status === 'Critical' || status === 'Not Complete') return 'error'
  return 'neutral'
}
function statusIcon(status: string | undefined) {
  if (status === 'Complete' || status === 'Ready for Owner Sign-off') return 'i-lucide-circle-check'
  if (status === 'Review' || status === 'Warning') return 'i-lucide-triangle-alert'
  if (status === 'Critical' || status === 'Not Complete') return 'i-lucide-circle-alert'
  return 'i-lucide-circle-help'
}

async function refreshShell() {
  if (!auth.isAuthenticated.value) return
  try {
    const [companyRows, storeRows] = await Promise.all([api.list<any>('companies'), api.list<any>('stores')])
    companies.value = companyRows
    stores.value = storeRows
  } catch (error: any) {
    loadError.value ||= feedback.errorMessage(error, 'Workspace options could not be loaded.', 'Production Go-Live workspace load failed')
  }
}
async function loadReport() {
  if (!auth.isAuthenticated.value) return
  loading.value = true
  loadError.value = ''
  try {
    report.value = await api.get<any>(`production-go-live/master-acceptance?${query()}`)
    feedback.notify('Production go-live acceptance refreshed', `${report.value?.criticalIssues || 0} critical and ${report.value?.warningIssues || 0} warning issue(s).`, report.value?.status === 'Ready for Owner Sign-off' ? 'success' : 'warning')
  } catch (error: any) {
    loadError.value = feedback.errorMessage(error, 'Production Go-Live Master Acceptance could not be loaded. Check API logs and permissions.', 'Production Go-Live Master Acceptance failed')
  } finally { loading.value = false }
}
async function refreshAll() { await refreshShell(); await loadReport() }
async function exportCsv() {
  exporting.value = true
  try {
    const response = await fetch(`${config.public.apiBase}/production-go-live/master-acceptance/evidence.csv?${query()}`, { headers: api.authHeaders() })
    if (!response.ok) throw new Error(await response.text())
    const blob = await response.blob()
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = `production-go-live-master-acceptance-${filters.from}-${filters.to}.csv`
    link.click()
    URL.revokeObjectURL(url)
  } catch (error: any) { feedback.fromError(error, 'Production Go-Live CSV export failed') }
  finally { exporting.value = false }
}
onMounted(async () => { auth.restore(); if (auth.isAuthenticated.value) await refreshAll() })
</script>

<template>
  <AuthScreen v-if="!auth.isAuthenticated.value" @authenticated="refreshAll" />
  <AppShell v-else title="Go-Live Acceptance" :companies="companies" :stores="stores" @refresh="refreshAll" @workspace-change="loadReport">
    <section class="closure-page">
      <UiModulePageHeader title="Production Go-Live Master Acceptance" description="Final master gate before owner sign-off. It combines master setup, sales, purchase, stock, accounting, bank, returns, payroll, FY lock and manual backup/restore evidence." icon="i-lucide-rocket">
        <template #actions>
          <div class="header-actions">
            <UBadge :label="report?.status || 'Not checked'" :color="statusColor(report?.status)" :icon="statusIcon(report?.status)" variant="subtle" />
            <UButton icon="i-lucide-refresh-cw" label="Run" :loading="loading" @click="loadReport" />
            <UButton icon="i-lucide-download" label="CSV evidence" variant="subtle" :loading="exporting" @click="exportCsv" />
            <UButton to="/final-owner-signoff" icon="i-lucide-pen-line" label="Owner Sign-off" variant="ghost" />
          </div>
        </template>
      </UiModulePageHeader>

      <UAlert v-if="loadError" color="error" variant="subtle" icon="i-lucide-circle-alert" title="Acceptance unavailable" :description="loadError" :actions="[{ label: 'Try again', icon: 'i-lucide-refresh-cw', onClick: refreshAll }]" />

      <UCard>
        <div class="validation-filters">
          <UFormField label="From date"><UInput v-model="filters.from" type="date" /></UFormField>
          <UFormField label="To date"><UInput v-model="filters.to" type="date" /></UFormField>
          <div class="filter-actions"><UButton icon="i-lucide-play" label="Run acceptance" :loading="loading" @click="loadReport" /></div>
        </div>
      </UCard>

      <div class="status-grid">
        <UCard class="status-card hero-status"><p class="eyebrow">Go-live status</p><div class="status-line"><UBadge :label="report?.status || 'Not checked'" :color="statusColor(report?.status)" :icon="statusIcon(report?.status)" size="lg" /></div><p class="muted">{{ formatDate(report?.from) }} to {{ formatDate(report?.to) }}</p><p class="muted">{{ report?.version }} / {{ report?.buildCode }}</p></UCard>
        <UCard class="status-card"><p class="eyebrow">Critical</p><strong>{{ numberValue(report?.criticalIssues) }}</strong><p class="muted">Blocks sign-off.</p></UCard>
        <UCard class="status-card"><p class="eyebrow">Warnings</p><strong>{{ numberValue(report?.warningIssues) }}</strong><p class="muted">Owner review.</p></UCard>
      </div>

      <div class="metric-grid"><UCard v-for="metric in metrics" :key="metric.label" class="metric-card"><p>{{ metric.label }}</p><strong>{{ metricValue(metric) }}</strong><span>{{ metric.description }}</span></UCard></div>

      <UCard v-if="issues.length" class="section-card"><template #header><h3>Blocking and review issues</h3></template><div class="table-wrap"><table><thead><tr><th>Severity</th><th>Title</th><th>Message</th><th>Open</th></tr></thead><tbody><tr v-for="issue in issues" :key="`${issue.title}-${issue.message}`"><td><UBadge :label="issue.severity" :color="statusColor(issue.severity)" variant="subtle" /></td><td>{{ issue.title }}</td><td>{{ issue.message }}</td><td><UButton v-if="issue.actionPath" :to="issue.actionPath" size="xs" variant="ghost" icon="i-lucide-external-link" label="Open" /></td></tr></tbody></table></div></UCard>

      <UCard class="section-card"><template #header><h3>Go-live checklist matrix</h3></template><div class="table-wrap"><table><thead><tr><th>Area</th><th>Status</th><th>Severity</th><th>Message</th><th>Open</th></tr></thead><tbody><tr v-for="row in checks" :key="row.area"><td>{{ row.area }}</td><td><UBadge :label="row.status" :color="statusColor(row.status)" variant="subtle" /></td><td>{{ row.severity }}</td><td>{{ row.message }}</td><td><UButton :to="row.actionPath" size="xs" variant="ghost" icon="i-lucide-external-link" label="Open" /></td></tr></tbody></table></div></UCard>

      <div class="info-grid">
        <UCard><template #header><h3>Final checklist</h3></template><ul><li v-for="item in closeoutChecklist" :key="item">{{ item }}</li></ul></UCard>
        <UCard><template #header><h3>Manual backup/restore proof</h3></template><ul><li v-for="item in backupProof" :key="item">{{ item }}</li></ul></UCard>
        <UCard><template #header><h3>Known limitations</h3></template><ul><li v-for="item in knownLimitations" :key="item">{{ item }}</li></ul></UCard>
        <UCard><template #header><h3>Next</h3></template><ul><li v-for="item in nextModules" :key="item">{{ item }}</li></ul></UCard>
      </div>
    </section>
  </AppShell>
</template>

<style scoped>
.closure-page { display: grid; gap: 1rem; }
.header-actions { display: flex; gap: .5rem; flex-wrap: wrap; align-items: center; }
.validation-filters { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: .75rem; align-items: end; }
.filter-actions { display: flex; align-items: center; }
.status-grid { display: grid; grid-template-columns: minmax(260px, 2fr) repeat(2, minmax(140px, 1fr)); gap: 1rem; }
.status-card strong { display: block; font-size: 1.8rem; margin-top: .25rem; }
.hero-status { min-height: 140px; }
.eyebrow { margin: 0; text-transform: uppercase; letter-spacing: .06em; font-size: .72rem; color: rgb(var(--color-gray-500)); }
.muted { color: rgb(var(--color-gray-500)); margin: .25rem 0 0; }
.metric-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 1rem; }
.metric-card p, .metric-card span { margin: 0; color: rgb(var(--color-gray-500)); }
.metric-card strong { display: block; margin: .25rem 0; font-size: 1.35rem; }
.section-card { overflow: hidden; }
.table-wrap { overflow-x: auto; }
table { width: 100%; border-collapse: collapse; font-size: .875rem; }
th, td { padding: .65rem; border-bottom: 1px solid rgb(var(--color-gray-200)); text-align: left; vertical-align: top; }
.info-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(260px, 1fr)); gap: 1rem; }
ul { margin: 0; padding-left: 1.1rem; }
@media (max-width: 780px) { .status-grid { grid-template-columns: 1fr; } }
</style>
