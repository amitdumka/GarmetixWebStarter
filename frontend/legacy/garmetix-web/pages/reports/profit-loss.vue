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
const filters = reactive({ from: inputDate(new Date(today.getFullYear(), today.getMonth(), 1)), to: inputDate(today), asOf: inputDate(today) })

useHead({ title: 'Profit/Loss Invoice Item Reporting | Garmetix' })

const selectedCompanyId = computed(() => workspace.companyId.value || companies.value[0]?.id || '')
const selectedStoreGroupId = computed(() => workspace.storeGroupId.value || '')
const selectedStoreId = computed(() => workspace.storeId.value || '')
const metrics = computed(() => report.value?.metrics || [])
const issues = computed(() => report.value?.issues || [])
const primaryRows = computed(() => report.value?.invoiceRows || [])
const secondaryRows = computed(() => report.value?.itemRows || [])
const closeoutChecklist = computed(() => report.value?.closeoutChecklist || [])
const knownLimitations = computed(() => report.value?.knownLimitations || [])
const nextModules = computed(() => report.value?.nextModuleCandidates || [])
const criticalIssues = computed(() => report.value?.criticalIssues || issues.value.filter((item: any) => item.severity === 'Critical').length)
const warningIssues = computed(() => report.value?.warningIssues || issues.value.filter((item: any) => item.severity === 'Warning').length)

function inputDate(date: Date) { return date.toISOString().slice(0, 10) }
function query() {
  const params = new URLSearchParams()
  if (selectedCompanyId.value) params.set('companyId', selectedCompanyId.value)
  if (selectedStoreGroupId.value) params.set('storeGroupId', selectedStoreGroupId.value)
  if (selectedStoreId.value) params.set('storeId', selectedStoreId.value)
  if (filters.from) params.set('from', filters.from)
  if (filters.to) params.set('to', filters.to)
  return params.toString()
}
function money(value: number | string | null | undefined) { return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(Number(value || 0)) }
function numberValue(value: number | string | null | undefined) { return new Intl.NumberFormat('en-IN', { maximumFractionDigits: 2 }).format(Number(value || 0)) }
function formatDate(value: string | null | undefined) { return value ? new Date(value).toLocaleDateString('en-IN') : '-' }
function metricValue(metric: any) { return metric?.amount !== null && metric?.amount !== undefined ? money(metric.amount) : numberValue(metric?.count) }
function statusColor(status: string | undefined) {
  if (status === 'Complete' || status === 'OK') return 'success'
  if (status === 'Review' || status === 'Warning' || status === 'Loss review' || status === 'Needs cost proof') return 'warning'
  if (status === 'Critical' || status === 'Not Complete' || status === 'Negative stock' || status === 'Missing cost') return 'error'
  return 'neutral'
}
function statusIcon(status: string | undefined) {
  if (status === 'Complete' || status === 'OK') return 'i-lucide-circle-check'
  if (status === 'Review' || status === 'Warning' || status === 'Loss review' || status === 'Needs cost proof') return 'i-lucide-triangle-alert'
  if (status === 'Critical' || status === 'Not Complete' || status === 'Negative stock' || status === 'Missing cost') return 'i-lucide-circle-alert'
  return 'i-lucide-circle-help'
}

async function refreshShell() {
  if (!auth.isAuthenticated.value) return
  try {
    const [companyRows, storeRows] = await Promise.all([api.list<any>('companies'), api.list<any>('stores')])
    companies.value = companyRows
    stores.value = storeRows
  } catch (error: any) {
    loadError.value ||= feedback.errorMessage(error, 'Workspace options could not be loaded.', 'Profit/Loss Invoice Item Reporting workspace load failed')
  }
}
async function loadReport() {
  if (!auth.isAuthenticated.value) return
  loading.value = true
  loadError.value = ''
  try {
    report.value = await api.get<any>(`reports/profit-loss?${query()}`)
    feedback.notify('Profit/Loss Invoice Item Reporting refreshed', `${criticalIssues.value} critical and ${warningIssues.value} warning issue(s).`, report.value?.status === 'Complete' ? 'success' : 'warning')
  } catch (error: any) {
    loadError.value = feedback.errorMessage(error, 'Profit/Loss Invoice Item Reporting could not be loaded. Check API logs and permissions.', 'Profit/Loss Invoice Item Reporting failed')
  } finally { loading.value = false }
}
async function refreshAll() { await refreshShell(); await loadReport() }
async function exportCsv() {
  exporting.value = true
  try {
    const response = await fetch(`${config.public.apiBase}/reports/profit-loss/evidence.csv?${query()}`, { headers: api.authHeaders() })
    if (!response.ok) throw new Error(await response.text())
    const blob = await response.blob()
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = `profit-loss-${filters.from || filters.asOf}-${filters.to || filters.asOf}.csv`
    link.click()
    URL.revokeObjectURL(url)
  } catch (error: any) { feedback.fromError(error, 'Profit/Loss Invoice Item Reporting CSV export failed') }
  finally { exporting.value = false }
}
onMounted(async () => { auth.restore(); if (auth.isAuthenticated.value) await refreshAll() })
</script>

<template>
  <AuthScreen v-if="!auth.isAuthenticated.value" @authenticated="refreshAll" />
  <AppShell v-else title="Profit/Loss" :companies="companies" :stores="stores" @refresh="refreshAll" @workspace-change="loadReport">
    <section class="closure-page">
      <UiModulePageHeader title="Profit/Loss Invoice Item Reporting" description="Invoice-wise and item-wise gross profit report using sale item revenue and linked stock-out cost evidence." icon="i-lucide-chart-no-axes-combined">
        <template #actions>
          <div class="header-actions">
            <UBadge :label="report?.status || 'Not checked'" :color="statusColor(report?.status)" :icon="statusIcon(report?.status)" variant="subtle" />
            <UButton icon="i-lucide-refresh-cw" label="Run" :loading="loading" @click="loadReport" />
            <UButton icon="i-lucide-download" label="CSV evidence" variant="subtle" :loading="exporting" @click="exportCsv" />
          </div>
        </template>
      </UiModulePageHeader>

      <UAlert v-if="loadError" color="error" variant="subtle" icon="i-lucide-circle-alert" title="Report unavailable" :description="loadError" :actions="[{ label: 'Try again', icon: 'i-lucide-refresh-cw', onClick: refreshAll }]" />

      <UCard class="planner-card">
        <div class="validation-filters">
          <UFormField label="From date"><UInput v-model="filters.from" type="date" /></UFormField><UFormField label="To date"><UInput v-model="filters.to" type="date" /></UFormField>
          <div class="filter-actions"><UButton icon="i-lucide-play" label="Apply" :loading="loading" @click="loadReport" /></div>
        </div>
      </UCard>

      <div class="status-grid">
        <UCard class="status-card hero-status">
          <p class="eyebrow">Closure status</p>
          <div class="status-line"><UBadge :label="report?.status || 'Not checked'" :color="statusColor(report?.status)" :icon="statusIcon(report?.status)" size="lg" /></div>
          <p class="muted">{{ formatDate(report?.from || report?.asOf) }} <span v-if="report?.to">to {{ formatDate(report?.to) }}</span></p>
        </UCard>
        <UCard class="status-card"><p class="eyebrow">Critical</p><strong>{{ numberValue(criticalIssues) }}</strong><p class="muted">Blocks closeout.</p></UCard>
        <UCard class="status-card"><p class="eyebrow">Warnings</p><strong>{{ numberValue(warningIssues) }}</strong><p class="muted">Review before lock.</p></UCard>
      </div>

      <div class="metric-grid">
        <UCard v-for="metric in metrics" :key="metric.label" class="metric-card"><p>{{ metric.label }}</p><strong>{{ metricValue(metric) }}</strong><span>{{ metric.description }}</span></UCard>
      </div>

      <UCard v-if="issues.length" class="section-card">
        <template #header><h3>Issues</h3></template>
        <div class="table-wrap"><table><thead><tr><th>Severity</th><th>Title</th><th>Message</th><th>Open</th></tr></thead><tbody><tr v-for="issue in issues" :key="`${issue.title}-${issue.message}`"><td><UBadge :label="issue.severity" :color="statusColor(issue.severity)" variant="subtle" /></td><td>{{ issue.title }}</td><td>{{ issue.message }}</td><td><UButton v-if="issue.actionPath" :to="issue.actionPath" size="xs" variant="ghost" icon="i-lucide-external-link" label="Open" /></td></tr></tbody></table></div>
      </UCard>

      <UCard class="section-card">
        <template #header><h3>Invoice-wise profit</h3></template>
        <div class="table-wrap"><table><thead><tr><th>Date</th><th>Invoice</th><th>Customer</th><th>Revenue ex GST</th><th>COGS</th><th>Gross profit</th><th>Margin</th><th>Status</th></tr></thead><tbody><tr v-for="row in primaryRows" :key="row.id || row.invoiceId || row.stockId || row.module"><td>{{ formatDate(row.invoiceDate) }}</td><td>{{ row.invoiceNumber }}</td><td>{{ row.customerName }}</td><td>{{ money(row.revenueExTax) }}</td><td>{{ money(row.costAmount) }}</td><td>{{ money(row.grossProfit) }}</td><td>{{ numberValue(row.marginPercent) }}%</td><td><UBadge :label="row.evidenceStatus" :color="statusColor(row.evidenceStatus)" variant="subtle" /></td></tr></tbody></table></div>
      </UCard>

      <UCard v-if="secondaryRows.length" class="section-card">
        <template #header><h3>Item-wise profit evidence</h3></template>
        <div class="table-wrap"><table><thead><tr><th>Date</th><th>Invoice</th><th>Product</th><th>Barcode</th><th>Qty</th><th>Revenue ex GST</th><th>COGS</th><th>Gross profit</th><th>Status</th></tr></thead><tbody><tr v-for="row in secondaryRows" :key="row.id || row.productId || `${row.invoiceId}-${row.barcode}`"><td>{{ formatDate(row.invoiceDate) }}</td><td>{{ row.invoiceNumber }}</td><td>{{ row.productName }}</td><td>{{ row.barcode }}</td><td>{{ numberValue(row.quantity) }}</td><td>{{ money(row.revenueExTax) }}</td><td>{{ money(row.costAmount) }}</td><td>{{ money(row.grossProfit) }}</td><td><UBadge :label="row.evidenceStatus" :color="statusColor(row.evidenceStatus)" variant="subtle" /></td></tr></tbody></table></div>
      </UCard>

      <div class="info-grid">
        <UCard><template #header><h3>Closeout checklist</h3></template><ul><li v-for="item in closeoutChecklist" :key="item">{{ item }}</li></ul></UCard>
        <UCard><template #header><h3>Known limitations</h3></template><ul><li v-for="item in knownLimitations" :key="item">{{ item }}</li></ul></UCard>
        <UCard><template #header><h3>Next modules</h3></template><ul><li v-for="item in nextModules" :key="item">{{ item }}</li></ul></UCard>
      </div>
    </section>
  </AppShell>
</template>

<style scoped>
.closure-page { display: grid; gap: 1rem; }
.header-actions { display: flex; gap: .5rem; flex-wrap: wrap; align-items: center; }
.validation-filters { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: .75rem; align-items: end; }
.filter-actions { display: flex; gap: .5rem; align-items: center; }
.status-grid { display: grid; grid-template-columns: minmax(260px, 2fr) repeat(2, minmax(140px, 1fr)); gap: 1rem; }
.status-card strong { display: block; font-size: 1.8rem; margin-top: .25rem; }
.hero-status { min-height: 130px; }
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
