<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const workspace = useWorkspace()
const feedback = useUiFeedback()
const config = useRuntimeConfig()
const isAuthenticated = computed(() => auth.isAuthenticated.value)

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const loading = ref(false)
const exporting = ref(false)
const loadError = ref('')
const report = ref<any | null>(null)

const today = new Date()
const firstDay = new Date(today.getFullYear(), today.getMonth(), 1)
const lastDay = new Date(today.getFullYear(), today.getMonth() + 1, 0)
const filters = reactive({
  from: inputDate(firstDay),
  to: inputDate(lastDay)
})

useHead({ title: 'Accounting/GST Post-Import Validation | Garmetix' })

const selectedCompanyId = computed(() => workspace.companyId.value || companies.value[0]?.id || '')
const selectedStoreId = computed(() => workspace.storeId.value || '')
const metrics = computed(() => report.value?.metrics || [])
const checks = computed(() => report.value?.checks || [])
const issues = computed(() => report.value?.issues || [])
const gstRows = computed(() => report.value?.gstRows || [])
const paymentRows = computed(() => report.value?.paymentRows || [])
const checklist = computed(() => report.value?.closeoutChecklist || [])
const knownLimitations = computed(() => report.value?.knownLimitations || [])
const nextModules = computed(() => report.value?.nextModuleCandidates || [])
const criticalIssues = computed(() => issues.value.filter((item: any) => item.severity === 'Critical').length)
const warningIssues = computed(() => issues.value.filter((item: any) => item.severity === 'Warning').length)

function inputDate(date: Date) {
  return date.toISOString().slice(0, 10)
}

function query() {
  const params = new URLSearchParams()
  if (selectedCompanyId.value) params.set('companyId', selectedCompanyId.value)
  if (selectedStoreId.value) params.set('storeId', selectedStoreId.value)
  if (filters.from) params.set('from', filters.from)
  if (filters.to) params.set('to', filters.to)
  return params.toString()
}

function money(value: number | string | null | undefined) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(Number(value || 0))
}

function number(value: number | string | null | undefined) {
  return new Intl.NumberFormat('en-IN', { maximumFractionDigits: 2 }).format(Number(value || 0))
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
    loadError.value ||= feedback.errorMessage(error, 'Workspace options could not be loaded.', 'Validation workspace load failed')
  }
}

async function loadReport() {
  if (!auth.isAuthenticated.value) return
  loading.value = true
  loadError.value = ''
  try {
    report.value = await api.get<any>(`post-import-validation/accounting-gst?${query()}`)
    const status = report.value?.status || 'Not Complete'
    feedback.notify('Accounting/GST validation refreshed', `${issues.value.length} issue(s), status: ${status}.`, status === 'Complete' ? 'success' : 'warning')
  } catch (error: any) {
    loadError.value = feedback.errorMessage(error, 'Post-import validation could not be loaded. Check API logs and permissions.', 'Accounting/GST validation failed')
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
    const response = await fetch(`${config.public.apiBase}/post-import-validation/accounting-gst.csv?${query()}`, { headers: api.authHeaders() })
    if (!response.ok) throw new Error(await response.text())
    const blob = await response.blob()
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = `accounting-gst-post-import-validation-${filters.from}-${filters.to}.csv`
    link.click()
    URL.revokeObjectURL(url)
  } catch (error: any) {
    feedback.fromError(error, 'CSV export failed')
  } finally {
    exporting.value = false
  }
}

onMounted(async () => {
  auth.restore()
  if (auth.isAuthenticated.value) {
    await refreshAll()
  }
})
</script>

<template>
  <AuthScreen v-if="!isAuthenticated" @authenticated="refreshAll" />

  <AppShell
    v-else
    title="Accounting/GST Post-Import Validation"
    :companies="companies"
    :stores="stores"
    @refresh="refreshAll"
    @workspace-change="loadReport"
  >
    <section class="post-import-validation-page">
      <UiModulePageHeader
        title="Accounting/GST post-import live validation"
        description="Cross-check Vyapar sale import, purchase import, accounting journals, GST snapshots, stock movements, payment rows, and closeout evidence for the selected period."
        icon="i-lucide-shield-check"
      >
        <template #actions>
          <div class="header-actions">
            <UBadge
              :label="report?.status || 'Not checked'"
              :color="statusColor(report?.status)"
              :icon="statusIcon(report?.status)"
              variant="subtle"
            />
            <UButton icon="i-lucide-refresh-cw" label="Run validation" :loading="loading" @click="loadReport" />
            <UButton icon="i-lucide-download" label="CSV evidence" variant="subtle" :loading="exporting" @click="exportCsv" />
          </div>
        </template>
      </UiModulePageHeader>

      <UAlert
        v-if="loadError"
        color="error"
        variant="subtle"
        icon="i-lucide-circle-alert"
        title="Validation is unavailable"
        :description="loadError"
        :actions="[{ label: 'Try again', icon: 'i-lucide-refresh-cw', onClick: refreshAll }]"
      />

      <UCard class="planner-card">
        <div class="validation-filters">
          <UFormField label="From date">
            <UInput v-model="filters.from" type="date" />
          </UFormField>
          <UFormField label="To date">
            <UInput v-model="filters.to" type="date" />
          </UFormField>
          <div class="filter-actions">
            <UButton icon="i-lucide-play" label="Apply" :loading="loading" @click="loadReport" />
          </div>
        </div>
      </UCard>

      <div class="status-grid">
        <UCard class="status-card">
          <p class="eyebrow">Final status</p>
          <div class="status-line">
            <UBadge :label="report?.status || 'Not checked'" :color="statusColor(report?.status)" :icon="statusIcon(report?.status)" size="lg" />
          </div>
          <p class="muted">Complete only when no Critical or Warning validation issues remain.</p>
        </UCard>
        <UCard class="status-card">
          <p class="eyebrow">Critical</p>
          <strong>{{ number(criticalIssues) }}</strong>
          <p class="muted">Blocks month/import closeout.</p>
        </UCard>
        <UCard class="status-card">
          <p class="eyebrow">Warnings</p>
          <strong>{{ number(warningIssues) }}</strong>
          <p class="muted">Needs review before sign-off.</p>
        </UCard>
      </div>

      <div class="metric-grid">
        <UCard v-for="metric in metrics" :key="metric.label" class="metric-card">
          <p>{{ metric.label }}</p>
          <strong>{{ metric.amount ? money(metric.amount) : number(metric.count) }}</strong>
          <span>{{ metric.detail }}</span>
        </UCard>
      </div>

      <UCard class="planner-card">
        <template #header>
          <div class="card-header-row">
            <div>
              <h3>Validation checks</h3>
              <p>Pass/Warning/Critical checks across accounting, GST, inventory, payments, and import acceptance.</p>
            </div>
            <UBadge :label="`${checks.length} checks`" color="primary" variant="subtle" />
          </div>
        </template>
        <div class="table-wrap">
          <table>
            <thead><tr><th>Status</th><th>Check</th><th>Expected</th><th>Actual</th><th>Difference</th><th>Description</th></tr></thead>
            <tbody>
              <tr v-for="check in checks" :key="check.code">
                <td><UBadge :label="check.status" :color="statusColor(check.status)" :icon="statusIcon(check.status)" variant="subtle" /></td>
                <td><strong>{{ check.title }}</strong><small>{{ check.code }}</small></td>
                <td>{{ number(check.expectedValue) }}</td>
                <td>{{ number(check.actualValue) }}</td>
                <td>{{ number(check.difference) }}</td>
                <td>{{ check.description }}</td>
              </tr>
              <tr v-if="!checks.length"><td colspan="6" class="empty-cell">No checks loaded yet.</td></tr>
            </tbody>
          </table>
        </div>
      </UCard>

      <UCard class="planner-card">
        <template #header>
          <div class="card-header-row">
            <div>
              <h3>Issues</h3>
              <p>Fix Critical issues first, then clear Warnings before marking this period closed.</p>
            </div>
            <UBadge :label="`${issues.length} issue(s)`" :color="issues.length ? 'warning' : 'success'" variant="subtle" />
          </div>
        </template>
        <div class="issue-list" v-if="issues.length">
          <UAlert
            v-for="issue in issues"
            :key="`${issue.code}-${issue.reference || issue.title}`"
            :color="statusColor(issue.severity)"
            variant="subtle"
            :icon="statusIcon(issue.severity)"
            :title="`${issue.area}: ${issue.title}`"
            :description="issue.reference ? `${issue.reference} — ${issue.description}` : issue.description"
          />
        </div>
        <UAlert v-else color="success" variant="subtle" icon="i-lucide-circle-check" title="No blocking issues" description="The selected period has no Critical or Warning validation issues from this report." />
      </UCard>

      <div class="two-column">
        <UCard class="planner-card">
          <template #header><h3>GST snapshot</h3></template>
          <div class="table-wrap compact">
            <table>
              <thead><tr><th>Direction</th><th>Rate</th><th>Taxable</th><th>Tax</th><th>Lines</th></tr></thead>
              <tbody>
                <tr v-for="row in gstRows" :key="`${row.direction}-${row.taxRate}`">
                  <td>{{ row.direction }}</td>
                  <td>{{ number(row.taxRate) }}%</td>
                  <td>{{ money(row.taxableValue) }}</td>
                  <td>{{ money(row.taxAmount) }}</td>
                  <td>{{ number(row.lineCount) }}</td>
                </tr>
                <tr v-if="!gstRows.length"><td colspan="5" class="empty-cell">No GST rows for this period.</td></tr>
              </tbody>
            </table>
          </div>
        </UCard>

        <UCard class="planner-card">
          <template #header><h3>Payment reconciliation</h3></template>
          <div class="table-wrap compact">
            <table>
              <thead><tr><th>Direction</th><th>Mode</th><th>Rows</th><th>Amount</th><th>Missing bank</th></tr></thead>
              <tbody>
                <tr v-for="row in paymentRows" :key="`${row.direction}-${row.paymentMode}`">
                  <td>{{ row.direction }}</td>
                  <td>{{ row.paymentMode }}</td>
                  <td>{{ number(row.rowCount) }}</td>
                  <td>{{ money(row.amount) }}</td>
                  <td>{{ number(row.missingBankMappingCount) }}</td>
                </tr>
                <tr v-if="!paymentRows.length"><td colspan="5" class="empty-cell">No payment rows for this period.</td></tr>
              </tbody>
            </table>
          </div>
        </UCard>
      </div>

      <div class="three-column">
        <UCard class="planner-card">
          <template #header><h3>Closeout checklist</h3></template>
          <ul class="check-list"><li v-for="item in checklist" :key="item">{{ item }}</li></ul>
        </UCard>
        <UCard class="planner-card">
          <template #header><h3>Known limitations</h3></template>
          <ul class="check-list"><li v-for="item in knownLimitations" :key="item">{{ item }}</li></ul>
        </UCard>
        <UCard class="planner-card">
          <template #header><h3>Next module candidates</h3></template>
          <ul class="check-list"><li v-for="item in nextModules" :key="item">{{ item }}</li></ul>
        </UCard>
      </div>
    </section>
  </AppShell>
</template>

<style scoped>
.post-import-validation-page { display: flex; flex-direction: column; gap: 1rem; }
.header-actions, .card-header-row, .status-line { display: flex; align-items: center; gap: .75rem; flex-wrap: wrap; }
.card-header-row { justify-content: space-between; }
.card-header-row h3 { margin: 0; font-size: 1rem; font-weight: 800; }
.card-header-row p { margin: .15rem 0 0; color: var(--ui-text-muted); font-size: .82rem; }
.validation-filters { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 1rem; align-items: end; }
.filter-actions { display: flex; align-items: end; }
.status-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 1rem; }
.status-card strong { display: block; font-size: 2rem; margin-top: .25rem; }
.eyebrow { margin: 0; color: var(--ui-text-muted); font-size: .72rem; font-weight: 800; letter-spacing: .08em; text-transform: uppercase; }
.muted { color: var(--ui-text-muted); font-size: .82rem; margin: .35rem 0 0; }
.metric-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(210px, 1fr)); gap: 1rem; }
.metric-card p { margin: 0; color: var(--ui-text-muted); font-size: .8rem; }
.metric-card strong { display: block; margin-top: .25rem; font-size: 1.25rem; }
.metric-card span { display: block; margin-top: .2rem; color: var(--ui-text-muted); font-size: .78rem; }
.table-wrap { overflow-x: auto; }
table { width: 100%; border-collapse: collapse; min-width: 760px; }
.compact table { min-width: 520px; }
th, td { padding: .65rem .75rem; border-bottom: 1px solid var(--ui-border); text-align: left; vertical-align: top; font-size: .84rem; }
th { color: var(--ui-text-muted); font-size: .72rem; text-transform: uppercase; letter-spacing: .06em; }
td small { display: block; color: var(--ui-text-muted); margin-top: .15rem; }
.empty-cell { color: var(--ui-text-muted); text-align: center; }
.issue-list { display: flex; flex-direction: column; gap: .65rem; }
.two-column { display: grid; grid-template-columns: repeat(auto-fit, minmax(330px, 1fr)); gap: 1rem; }
.three-column { display: grid; grid-template-columns: repeat(auto-fit, minmax(260px, 1fr)); gap: 1rem; }
.check-list { margin: 0; padding-left: 1.1rem; color: var(--ui-text-muted); display: grid; gap: .45rem; font-size: .84rem; }
</style>
