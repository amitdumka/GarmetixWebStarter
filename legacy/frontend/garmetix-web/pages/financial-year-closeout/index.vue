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
const fyStartYear = today.getMonth() + 1 >= 4 ? today.getFullYear() : today.getFullYear() - 1
const filters = reactive({
  from: inputDate(new Date(fyStartYear, 3, 1)),
  to: inputDate(new Date(fyStartYear + 1, 2, 31))
})

useHead({ title: 'Financial Year Closeout | Garmetix' })

const selectedCompanyId = computed(() => workspace.companyId.value || companies.value[0]?.id || '')
const selectedStoreGroupId = computed(() => workspace.storeGroupId.value || '')
const selectedStoreId = computed(() => workspace.storeId.value || '')
const metrics = computed(() => report.value?.metrics || [])
const sections = computed(() => report.value?.sections || [])
const issues = computed(() => report.value?.issues || [])
const lockRows = computed(() => report.value?.lockEvidence?.rows || [])
const closeoutChecklist = computed(() => report.value?.closeoutChecklist || [])
const operatorRules = computed(() => report.value?.operatorRules || [])
const knownLimitations = computed(() => report.value?.knownLimitations || [])
const nextModules = computed(() => report.value?.nextModuleCandidates || [])
const criticalIssues = computed(() => report.value?.criticalIssues || issues.value.filter((item: any) => item.severity === 'Critical').length)
const warningIssues = computed(() => report.value?.warningIssues || issues.value.filter((item: any) => item.severity === 'Warning').length)

function inputDate(date: Date) {
  return date.toISOString().slice(0, 10)
}

function query() {
  const params = new URLSearchParams()
  if (selectedCompanyId.value) params.set('companyId', selectedCompanyId.value)
  if (selectedStoreGroupId.value) params.set('storeGroupId', selectedStoreGroupId.value)
  if (selectedStoreId.value) params.set('storeId', selectedStoreId.value)
  if (filters.from) params.set('from', filters.from)
  if (filters.to) params.set('to', filters.to)
  return params.toString()
}

function money(value: number | string | null | undefined) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(Number(value || 0))
}

function formatNumber(value: number | string | null | undefined) {
  return new Intl.NumberFormat('en-IN', { maximumFractionDigits: 2 }).format(Number(value || 0))
}

function formatDate(value: string | null | undefined) {
  return value ? new Date(value).toLocaleDateString('en-IN') : '-'
}

function metricValue(metric: any) {
  return metric?.amount !== null && metric?.amount !== undefined ? money(metric.amount) : formatNumber(metric?.count)
}

function statusColor(status: string | undefined) {
  if (status === 'Complete' || status === 'Pass') return 'success'
  if (status === 'Warning') return 'warning'
  if (status === 'Critical' || status === 'Not Complete') return 'error'
  return 'neutral'
}

function statusIcon(status: string | undefined) {
  if (status === 'Complete' || status === 'Pass') return 'i-lucide-circle-check'
  if (status === 'Warning') return 'i-lucide-triangle-alert'
  if (status === 'Critical' || status === 'Not Complete') return 'i-lucide-circle-alert'
  return 'i-lucide-circle-help'
}

async function refreshShell() {
  if (!auth.isAuthenticated.value) return
  try {
    const [companyRows, storeRows] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores')
    ])
    companies.value = companyRows
    stores.value = storeRows
  } catch (error: any) {
    loadError.value ||= feedback.errorMessage(error, 'Workspace options could not be loaded.', 'Financial year closeout workspace load failed')
  }
}

async function loadReport() {
  if (!auth.isAuthenticated.value) return
  loading.value = true
  loadError.value = ''
  try {
    report.value = await api.get<any>(`financial-year-closeout?${query()}`)
    const status = report.value?.status || 'Not Complete'
    feedback.notify('Financial year closeout refreshed', `${criticalIssues.value} critical and ${warningIssues.value} warning issue(s).`, status === 'Complete' ? 'success' : 'warning')
  } catch (error: any) {
    loadError.value = feedback.errorMessage(error, 'Financial year closeout dashboard could not be loaded. Check API logs and permissions.', 'Financial year closeout failed')
  } finally {
    loading.value = false
  }
}

async function refreshAll() {
  await refreshShell()
  await loadReport()
}

async function exportCsv() {
  exporting.value = true
  try {
    const response = await fetch(`${config.public.apiBase}/financial-year-closeout/evidence.csv?${query()}`, { headers: api.authHeaders() })
    if (!response.ok) throw new Error(await response.text())
    const blob = await response.blob()
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = `financial-year-closeout-${filters.from}-${filters.to}.csv`
    link.click()
    URL.revokeObjectURL(url)
  } catch (error: any) {
    feedback.fromError(error, 'Financial year closeout CSV export failed')
  } finally {
    exporting.value = false
  }
}

onMounted(async () => {
  auth.restore()
  if (auth.isAuthenticated.value) await refreshAll()
})
</script>

<template>
  <AuthScreen v-if="!auth.isAuthenticated.value" @authenticated="refreshAll" />

  <AppShell
    v-else
    title="FY Closeout"
    :companies="companies"
    :stores="stores"
    @refresh="refreshAll"
    @workspace-change="loadReport"
  >
    <section class="fy-closeout-page">
      <UiModulePageHeader
        title="Financial year closeout dashboard"
        description="One final period-close control page for GST/accounting, sales, purchases, customer dues, vendor payables, return credits, payroll, stock, print evidence and FY lock readiness."
        icon="i-lucide-lock-keyhole"
      >
        <template #actions>
          <div class="header-actions">
            <UBadge :label="report?.status || 'Not checked'" :color="statusColor(report?.status)" :icon="statusIcon(report?.status)" variant="subtle" />
            <UButton icon="i-lucide-refresh-cw" label="Run closeout" :loading="loading" @click="loadReport" />
            <UButton icon="i-lucide-download" label="CSV evidence" variant="subtle" :loading="exporting" @click="exportCsv" />
            <UButton to="/financial-year-locks" icon="i-lucide-lock" label="FY Locks" variant="ghost" />
          </div>
        </template>
      </UiModulePageHeader>

      <UAlert
        v-if="loadError"
        color="error"
        variant="subtle"
        icon="i-lucide-circle-alert"
        title="Financial year closeout is unavailable"
        :description="loadError"
        :actions="[{ label: 'Try again', icon: 'i-lucide-refresh-cw', onClick: refreshAll }]"
      />

      <UCard class="planner-card">
        <div class="validation-filters">
          <UFormField label="From date">
            <UInput v-model="filters.from" type="date" />
          </UFormField>
          <UFormField label="To / close date">
            <UInput v-model="filters.to" type="date" />
          </UFormField>
          <div class="filter-actions">
            <UButton icon="i-lucide-play" label="Apply" :loading="loading" @click="loadReport" />
          </div>
        </div>
      </UCard>

      <div class="status-grid">
        <UCard class="status-card hero-status">
          <p class="eyebrow">Final closeout status</p>
          <div class="status-line">
            <UBadge :label="report?.status || 'Not checked'" :color="statusColor(report?.status)" :icon="statusIcon(report?.status)" size="lg" />
          </div>
          <p class="muted">Complete only when all section checks are clean and a full FY lock covers the selected period.</p>
          <p class="fy-label">{{ report?.financialYear || '-' }} · {{ formatDate(report?.from) }} to {{ formatDate(report?.to) }}</p>
        </UCard>
        <UCard class="status-card">
          <p class="eyebrow">Critical</p>
          <strong>{{ formatNumber(criticalIssues) }}</strong>
          <p class="muted">Blocks FY closeout.</p>
        </UCard>
        <UCard class="status-card">
          <p class="eyebrow">Warnings</p>
          <strong>{{ formatNumber(warningIssues) }}</strong>
          <p class="muted">Review before lock.</p>
        </UCard>
      </div>

      <div class="metric-grid">
        <UCard v-for="metric in metrics" :key="metric.label" class="metric-card">
          <p>{{ metric.label }}</p>
          <strong>{{ metricValue(metric) }}</strong>
          <span>{{ metric.description }}</span>
        </UCard>
      </div>

      <UCard>
        <template #header>
          <div class="card-header-line">
            <div>
              <h2>Closeout section matrix</h2>
              <p>Each section links to the detailed acceptance/reconciliation page where the issue should be corrected.</p>
            </div>
          </div>
        </template>
        <div class="section-grid">
          <UCard v-for="section in sections" :key="section.name" class="section-card">
            <div class="section-title">
              <h3>{{ section.name }}</h3>
              <UBadge :label="section.status" :color="statusColor(section.status)" :icon="statusIcon(section.status)" variant="subtle" />
            </div>
            <p class="muted">{{ section.criticalIssues }} critical · {{ section.warningIssues }} warning</p>
            <div class="mini-metrics">
              <div v-for="metric in section.metrics" :key="`${section.name}-${metric.label}`">
                <span>{{ metric.label }}</span>
                <strong>{{ metricValue(metric) }}</strong>
              </div>
            </div>
            <UButton :to="section.actionPath" icon="i-lucide-arrow-up-right" label="Open section" variant="ghost" size="sm" />
          </UCard>
        </div>
      </UCard>

      <UCard>
        <template #header>
          <div class="card-header-line">
            <div>
              <h2>Blocking and warning issues</h2>
              <p>Clear Critical items first. Warning items should be accepted by owner/accountant before lock.</p>
            </div>
          </div>
        </template>
        <div class="planner-table-wrap">
          <table class="planner-table">
            <thead><tr><th>Severity</th><th>Code</th><th>Message</th><th>Action</th></tr></thead>
            <tbody>
              <tr v-for="issue in issues" :key="`${issue.severity}-${issue.code}-${issue.message}`">
                <td><UBadge :label="issue.severity" :color="statusColor(issue.severity)" variant="subtle" /></td>
                <td class="font-medium">{{ issue.code }}</td>
                <td>{{ issue.message }}</td>
                <td><UButton :to="issue.actionPath" label="Open" icon="i-lucide-arrow-up-right" size="xs" variant="ghost" /></td>
              </tr>
              <tr v-if="issues.length === 0"><td colspan="4" class="empty-row">No issues found for selected period.</td></tr>
            </tbody>
          </table>
        </div>
      </UCard>

      <UCard>
        <template #header>
          <div class="card-header-line">
            <div>
              <h2>FY lock evidence</h2>
              <p>The closeout becomes complete only when a matching lock covers Accounting, Sales, Purchase, Inventory and GST.</p>
            </div>
            <UBadge :label="report?.lockEvidence?.fullyLocked ? 'Fully locked' : 'Not fully locked'" :color="report?.lockEvidence?.fullyLocked ? 'success' : 'error'" variant="subtle" />
          </div>
        </template>
        <div class="planner-table-wrap">
          <table class="planner-table">
            <thead><tr><th>FY</th><th>Period</th><th>Accounting</th><th>Sales</th><th>Purchase</th><th>Inventory</th><th>GST</th><th>Locked by</th></tr></thead>
            <tbody>
              <tr v-for="row in lockRows" :key="row.id">
                <td class="font-medium">{{ row.financialYear }}</td>
                <td>{{ formatDate(row.periodStart) }} - {{ formatDate(row.periodEnd) }}</td>
                <td>{{ row.lockAccounting ? 'Yes' : 'No' }}</td>
                <td>{{ row.lockSales ? 'Yes' : 'No' }}</td>
                <td>{{ row.lockPurchase ? 'Yes' : 'No' }}</td>
                <td>{{ row.lockInventory ? 'Yes' : 'No' }}</td>
                <td>{{ row.lockGst ? 'Yes' : 'No' }}</td>
                <td>{{ row.lockedBy || '-' }}</td>
              </tr>
              <tr v-if="lockRows.length === 0"><td colspan="8" class="empty-row">No matching FY lock found for this period.</td></tr>
            </tbody>
          </table>
        </div>
      </UCard>

      <div class="info-grid">
        <UCard>
          <template #header><h2>Final closeout checklist</h2></template>
          <ul class="check-list"><li v-for="item in closeoutChecklist" :key="item">{{ item }}</li></ul>
        </UCard>
        <UCard>
          <template #header><h2>Operator rules</h2></template>
          <ul class="check-list"><li v-for="item in operatorRules" :key="item">{{ item }}</li></ul>
        </UCard>
        <UCard>
          <template #header><h2>Known limitations</h2></template>
          <ul class="check-list"><li v-for="item in knownLimitations" :key="item">{{ item }}</li></ul>
        </UCard>
        <UCard>
          <template #header><h2>Next module candidates</h2></template>
          <ul class="check-list"><li v-for="item in nextModules" :key="item">{{ item }}</li></ul>
        </UCard>
      </div>
    </section>
  </AppShell>
</template>

<style scoped>
.fy-closeout-page {
  display: grid;
  gap: 1rem;
}
.header-actions,
.validation-filters,
.card-header-line,
.status-line,
.section-title {
  display: flex;
  align-items: center;
  gap: .75rem;
  flex-wrap: wrap;
}
.header-actions,
.filter-actions {
  justify-content: flex-end;
}
.validation-filters {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  align-items: end;
}
.status-grid,
.metric-grid,
.info-grid,
.section-grid {
  display: grid;
  gap: 1rem;
}
.status-grid {
  grid-template-columns: minmax(260px, 2fr) repeat(2, minmax(160px, 1fr));
}
.metric-grid {
  grid-template-columns: repeat(auto-fit, minmax(190px, 1fr));
}
.section-grid {
  grid-template-columns: repeat(auto-fit, minmax(260px, 1fr));
}
.info-grid {
  grid-template-columns: repeat(auto-fit, minmax(260px, 1fr));
}
.status-card strong,
.metric-card strong {
  display: block;
  font-size: 1.65rem;
  margin-top: .25rem;
}
.metric-card p,
.eyebrow {
  margin: 0;
  font-size: .78rem;
  color: rgb(var(--color-gray-500));
  text-transform: uppercase;
  letter-spacing: .04em;
}
.metric-card span,
.muted {
  color: rgb(var(--color-gray-500));
  font-size: .88rem;
}
.fy-label {
  font-weight: 700;
  margin-top: .5rem;
}
.mini-metrics {
  display: grid;
  gap: .35rem;
  margin: .75rem 0;
}
.mini-metrics div {
  display: flex;
  justify-content: space-between;
  gap: .5rem;
  border-bottom: 1px dashed rgb(var(--color-gray-200));
  padding-bottom: .25rem;
}
.planner-table-wrap {
  overflow-x: auto;
}
.planner-table {
  width: 100%;
  border-collapse: collapse;
  font-size: .9rem;
}
.planner-table th,
.planner-table td {
  border-bottom: 1px solid rgb(var(--color-gray-200));
  padding: .65rem .5rem;
  text-align: left;
  vertical-align: top;
}
.planner-table th {
  color: rgb(var(--color-gray-500));
  font-size: .75rem;
  text-transform: uppercase;
  letter-spacing: .04em;
}
.empty-row {
  text-align: center !important;
  color: rgb(var(--color-gray-500));
  padding: 1.5rem !important;
}
.check-list {
  margin: 0;
  padding-left: 1.1rem;
  display: grid;
  gap: .45rem;
  color: rgb(var(--color-gray-700));
}
@media (max-width: 860px) {
  .status-grid {
    grid-template-columns: 1fr;
  }
}
</style>
