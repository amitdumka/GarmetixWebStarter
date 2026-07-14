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
const firstDay = new Date(today.getFullYear(), today.getMonth(), 1)
const filters = reactive({
  from: inputDate(firstDay),
  to: inputDate(today)
})

useHead({ title: 'Customer Dues Reconciliation | Garmetix' })

const selectedCompanyId = computed(() => workspace.companyId.value || companies.value[0]?.id || '')
const selectedStoreGroupId = computed(() => workspace.storeGroupId.value || '')
const selectedStoreId = computed(() => workspace.storeId.value || '')
const metrics = computed(() => report.value?.metrics || [])
const creditSources = computed(() => report.value?.creditSources || [])
const issues = computed(() => report.value?.issues || [])
const evidenceRows = computed(() => report.value?.evidenceRows || [])
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
    loadError.value ||= feedback.errorMessage(error, 'Workspace options could not be loaded.', 'Customer dues reconciliation workspace load failed')
  }
}

async function loadReport() {
  if (!auth.isAuthenticated.value) return
  loading.value = true
  loadError.value = ''
  try {
    report.value = await api.get<any>(`customers/dues-reconciliation?${query()}`)
    const status = report.value?.status || 'Not Complete'
    feedback.notify('Customer dues reconciliation refreshed', `${criticalIssues.value} critical and ${warningIssues.value} warning issue(s).`, status === 'Complete' ? 'success' : 'warning')
  } catch (error: any) {
    loadError.value = feedback.errorMessage(error, 'Customer dues reconciliation could not be loaded. Check API logs and permissions.', 'Customer dues reconciliation failed')
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
    const response = await fetch(`${config.public.apiBase}/customers/dues-reconciliation/evidence.csv?${query()}`, { headers: api.authHeaders() })
    if (!response.ok) throw new Error(await response.text())
    const blob = await response.blob()
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = `customer-dues-credit-reconciliation-${filters.from}-${filters.to}.csv`
    link.click()
    URL.revokeObjectURL(url)
  } catch (error: any) {
    feedback.fromError(error, 'Customer dues reconciliation CSV export failed')
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
    title="Customer Dues Reco"
    :companies="companies"
    :stores="stores"
    @refresh="refreshAll"
    @workspace-change="loadReport"
  >
    <section class="customer-dues-reco-page">
      <UiModulePageHeader
        title="Customer dues / credit balance reconciliation"
        description="Validate customer invoice dues, advance receipts, sale-return credit notes, store credit balance and non-cash advance bank mapping before receivable closeout."
        icon="i-lucide-wallet-cards"
      >
        <template #actions>
          <div class="header-actions">
            <UBadge :label="report?.status || 'Not checked'" :color="statusColor(report?.status)" :icon="statusIcon(report?.status)" variant="subtle" />
            <UButton icon="i-lucide-refresh-cw" label="Run reconciliation" :loading="loading" @click="loadReport" />
            <UButton icon="i-lucide-download" label="CSV evidence" variant="subtle" :loading="exporting" @click="exportCsv" />
            <UButton to="/customers" icon="i-lucide-users-round" label="Customers" variant="ghost" />
          </div>
        </template>
      </UiModulePageHeader>

      <UAlert
        v-if="loadError"
        color="error"
        variant="subtle"
        icon="i-lucide-circle-alert"
        title="Customer dues reconciliation is unavailable"
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
          <p class="muted">Complete only when customer dues, credit balance and adjustment evidence has no critical or warning issue.</p>
        </UCard>
        <UCard class="status-card">
          <p class="eyebrow">Critical</p>
          <strong>{{ formatNumber(criticalIssues) }}</strong>
          <p class="muted">Blocks receivable closeout.</p>
        </UCard>
        <UCard class="status-card">
          <p class="eyebrow">Warnings</p>
          <strong>{{ formatNumber(warningIssues) }}</strong>
          <p class="muted">Review before sign-off.</p>
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
              <h2>Credit source reconciliation</h2>
              <p>Open advances plus open credit notes should match customer master credit balance.</p>
            </div>
          </div>
        </template>
        <div class="planner-table-wrap">
          <table class="planner-table">
            <thead><tr><th>Source</th><th class="text-right">Rows</th><th class="text-right">Amount</th><th class="text-right">Adjusted</th><th class="text-right">Available</th><th class="text-right">Issues</th></tr></thead>
            <tbody>
              <tr v-for="source in creditSources" :key="source.source">
                <td class="font-medium">{{ source.source }}</td>
                <td class="text-right">{{ formatNumber(source.rowCount) }}</td>
                <td class="text-right">{{ money(source.amount) }}</td>
                <td class="text-right">{{ money(source.adjustedAmount) }}</td>
                <td class="text-right">{{ money(source.availableAmount) }}</td>
                <td class="text-right">{{ formatNumber(source.issueCount) }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </UCard>

      <UCard>
        <template #header>
          <div class="card-header-line">
            <div>
              <h2>Blocking / warning issues</h2>
              <p>Correct these through controlled sale settlement, advance receipt, credit note adjustment, or data consistency repair.</p>
            </div>
          </div>
        </template>
        <div class="planner-table-wrap">
          <table class="planner-table">
            <thead><tr><th>Severity</th><th>Code</th><th>Customer</th><th>Mobile</th><th>Message</th><th class="text-right">Amount</th></tr></thead>
            <tbody>
              <tr v-for="issue in issues" :key="`${issue.customerId}-${issue.code}-${issue.message}`">
                <td><UBadge :label="issue.severity" :color="statusColor(issue.severity)" variant="subtle" /></td>
                <td class="font-mono text-xs">{{ issue.code }}</td>
                <td class="font-medium">{{ issue.customerName }}</td>
                <td>{{ issue.mobileNumber || '-' }}</td>
                <td>{{ issue.message }}</td>
                <td class="text-right">{{ issue.amount === null || issue.amount === undefined ? '-' : money(issue.amount) }}</td>
              </tr>
              <tr v-if="issues.length === 0"><td colspan="6" class="empty-row">No issues for the selected period.</td></tr>
            </tbody>
          </table>
        </div>
      </UCard>

      <UCard>
        <template #header>
          <div class="card-header-line">
            <div>
              <h2>Customer-wise evidence</h2>
              <p>Limited to the strongest 250 evidence rows so the page remains fast on live data.</p>
            </div>
          </div>
        </template>
        <div class="planner-table-wrap wide-table">
          <table class="planner-table">
            <thead>
              <tr>
                <th>Status</th><th>Customer</th><th>Mobile</th><th class="text-right">Invoices</th><th class="text-right">Open</th><th class="text-right">Due</th><th class="text-right">Master Credit</th><th class="text-right">Advances</th><th class="text-right">Credit Notes</th><th class="text-right">Expected Credit</th><th class="text-right">Diff</th><th class="text-right">Payment Mismatch</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="row in evidenceRows" :key="row.customerId">
                <td><UBadge :label="row.status" :color="statusColor(row.status)" variant="subtle" /></td>
                <td class="font-medium">{{ row.customerName }}</td>
                <td>{{ row.mobileNumber || '-' }}</td>
                <td class="text-right">{{ formatNumber(row.invoiceCount) }}</td>
                <td class="text-right">{{ formatNumber(row.openInvoiceCount) }}</td>
                <td class="text-right">{{ money(row.invoiceDue) }}</td>
                <td class="text-right">{{ money(row.customerMasterCreditBalance) }}</td>
                <td class="text-right">{{ money(row.advanceOpen) }}</td>
                <td class="text-right">{{ money(row.creditNoteOpen) }}</td>
                <td class="text-right">{{ money(row.expectedCreditBalance) }}</td>
                <td class="text-right">{{ money(row.creditBalanceDifference) }}</td>
                <td class="text-right">{{ formatNumber(row.paymentMismatchCount) }}</td>
              </tr>
              <tr v-if="evidenceRows.length === 0"><td colspan="12" class="empty-row">No customer evidence found for this filter.</td></tr>
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
.customer-dues-reco-page { display: grid; gap: 1rem; }
.header-actions, .filter-actions, .card-header-line { display: flex; align-items: center; gap: .5rem; flex-wrap: wrap; }
.header-actions { justify-content: flex-end; }
.validation-filters { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: .75rem; align-items: end; }
.status-grid, .metric-grid, .info-grid { display: grid; gap: 1rem; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); }
.status-card strong, .metric-card strong { display: block; font-size: 1.7rem; line-height: 1.1; margin-top: .25rem; }
.metric-card p, .eyebrow { color: rgb(100 116 139); font-size: .78rem; font-weight: 700; letter-spacing: .08em; text-transform: uppercase; }
.metric-card span, .muted, .card-header-line p { color: rgb(100 116 139); font-size: .875rem; }
.status-line { margin: .5rem 0; }
.wide-table { overflow-x: auto; }
.empty-row { color: rgb(100 116 139); padding: 1rem; text-align: center; }
.check-list { display: grid; gap: .55rem; color: rgb(51 65 85); font-size: .92rem; margin: 0; padding-left: 1.1rem; }
:global(.dark) .check-list { color: rgb(203 213 225); }
</style>
