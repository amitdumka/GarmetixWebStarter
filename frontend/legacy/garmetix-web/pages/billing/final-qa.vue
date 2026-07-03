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

useHead({ title: 'Billing Final QA | Garmetix' })

const selectedCompanyId = computed(() => workspace.companyId.value || companies.value[0]?.id || '')
const selectedStoreGroupId = computed(() => workspace.storeGroupId.value || '')
const selectedStoreId = computed(() => workspace.storeId.value || '')
const metrics = computed(() => report.value?.metrics || [])
const checks = computed(() => report.value?.checks || [])
const issues = computed(() => report.value?.issues || [])
const paymentModes = computed(() => report.value?.paymentModes || [])
const gstRows = computed(() => report.value?.gstRows || [])
const invoiceEvidence = computed(() => report.value?.invoiceEvidence || [])
const checklist = computed(() => report.value?.closeoutChecklist || [])
const knownLimitations = computed(() => report.value?.knownLimitations || [])
const operatorRules = computed(() => report.value?.operatorRules || [])
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

function number(value: number | string | null | undefined) {
  return new Intl.NumberFormat('en-IN', { maximumFractionDigits: 2 }).format(Number(value || 0))
}

function metricValue(metric: any) {
  return metric?.amount !== null && metric?.amount !== undefined ? money(metric.amount) : number(metric?.count)
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
    loadError.value ||= feedback.errorMessage(error, 'Workspace options could not be loaded.', 'Billing final QA workspace load failed')
  }
}

async function loadReport() {
  if (!auth.isAuthenticated.value) return
  loading.value = true
  loadError.value = ''
  try {
    report.value = await api.get<any>(`billing/final-qa?${query()}`)
    const status = report.value?.status || 'Not Complete'
    feedback.notify('Billing final QA refreshed', `${criticalIssues.value} critical and ${warningIssues.value} warning issue(s).`, status === 'Complete' ? 'success' : 'warning')
  } catch (error: any) {
    loadError.value = feedback.errorMessage(error, 'Billing final QA could not be loaded. Check API logs and permissions.', 'Billing final QA failed')
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
    const response = await fetch(`${config.public.apiBase}/billing/final-qa/evidence.csv?${query()}`, { headers: api.authHeaders() })
    if (!response.ok) throw new Error(await response.text())
    const blob = await response.blob()
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = `billing-final-qa-${filters.from}-${filters.to}.csv`
    link.click()
    URL.revokeObjectURL(url)
  } catch (error: any) {
    feedback.fromError(error, 'Billing final QA CSV export failed')
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
    title="Billing Final QA"
    :companies="companies"
    :stores="stores"
    @refresh="refreshAll"
    @workspace-change="loadReport"
  >
    <section class="billing-final-qa-page">
      <UiModulePageHeader
        title="Sale/Billing final QA closure"
        description="Validate real sale invoices, mixed-payment rows, invoice replacement evidence, GST snapshots, stock-out movements, accounting journals, barcode evidence and bank mapping before billing closeout."
        icon="i-lucide-badge-check"
      >
        <template #actions>
          <div class="header-actions">
            <UBadge
              :label="report?.status || 'Not checked'"
              :color="statusColor(report?.status)"
              :icon="statusIcon(report?.status)"
              variant="subtle"
            />
            <UButton icon="i-lucide-refresh-cw" label="Run QA" :loading="loading" @click="loadReport" />
            <UButton icon="i-lucide-download" label="CSV evidence" variant="subtle" :loading="exporting" @click="exportCsv" />
            <UButton to="/billing" icon="i-lucide-receipt-indian-rupee" label="Billing" variant="ghost" />
          </div>
        </template>
      </UiModulePageHeader>

      <UAlert
        v-if="loadError"
        color="error"
        variant="subtle"
        icon="i-lucide-circle-alert"
        title="Billing final QA is unavailable"
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
          <p class="muted">Complete only when every critical and warning item is cleared for the selected period.</p>
        </UCard>
        <UCard class="status-card">
          <p class="eyebrow">Critical</p>
          <strong>{{ number(criticalIssues) }}</strong>
          <p class="muted">Blocks billing closeout.</p>
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
          <strong>{{ metricValue(metric) }}</strong>
          <span>{{ metric.detail }}</span>
        </UCard>
      </div>

      <UCard class="planner-card">
        <template #header>
          <div class="card-header-row">
            <div>
              <h2>Closure checks</h2>
              <p>These checks turn the sale/billing module into a visible Complete / Not Complete closeout.</p>
            </div>
          </div>
        </template>
        <div class="check-grid">
          <div v-for="check in checks" :key="check.name" class="check-row">
            <UBadge :label="check.status" :color="statusColor(check.status)" :icon="statusIcon(check.status)" variant="subtle" />
            <div>
              <strong>{{ check.name }}</strong>
              <p>{{ check.detail }}</p>
            </div>
          </div>
        </div>
      </UCard>

      <UCard class="planner-card">
        <template #header>
          <div class="card-header-row">
            <div>
              <h2>Blocking and warning issues</h2>
              <p>Open the invoice path, correct using controlled revision/replacement/repair, then rerun QA.</p>
            </div>
          </div>
        </template>
        <div v-if="issues.length" class="table-scroll">
          <table class="qa-table">
            <thead>
              <tr>
                <th>Severity</th>
                <th>Code</th>
                <th>Invoice</th>
                <th>Issue</th>
                <th>Amount</th>
                <th>Open</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="issue in issues" :key="`${issue.code}-${issue.sourceNumber}-${issue.message}`">
                <td><UBadge :label="issue.severity" :color="statusColor(issue.severity)" variant="subtle" /></td>
                <td>{{ issue.code }}</td>
                <td>{{ issue.sourceNumber || '-' }}</td>
                <td>{{ issue.message }}</td>
                <td>{{ issue.amount ? money(issue.amount) : '-' }}</td>
                <td>
                  <UButton v-if="issue.sourcePath" :to="issue.sourcePath" label="Open" icon="i-lucide-external-link" size="xs" variant="ghost" />
                  <span v-else>-</span>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
        <UAlert v-else color="success" variant="subtle" icon="i-lucide-circle-check" title="No billing QA issues found" description="The selected period currently has no critical or warning billing closure issues." />
      </UCard>

      <div class="two-column-grid">
        <UCard class="planner-card">
          <template #header><h2>Payment mode reconciliation</h2></template>
          <div class="table-scroll">
            <table class="qa-table compact">
              <thead>
                <tr>
                  <th>Mode</th>
                  <th>Rows</th>
                  <th>Amount</th>
                  <th>Missing bank</th>
                  <th>Refs</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="row in paymentModes" :key="row.paymentMode">
                  <td>{{ row.paymentMode }}</td>
                  <td>{{ number(row.rowCount) }}</td>
                  <td>{{ money(row.amount) }}</td>
                  <td>{{ number(row.missingBankMappingRows) }}</td>
                  <td>{{ number(row.referenceRows) }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </UCard>

        <UCard class="planner-card">
          <template #header><h2>GST snapshot by rate</h2></template>
          <div class="table-scroll">
            <table class="qa-table compact">
              <thead>
                <tr>
                  <th>GST %</th>
                  <th>Taxable</th>
                  <th>Tax</th>
                  <th>Total</th>
                  <th>Qty</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="row in gstRows" :key="row.taxRate">
                  <td>{{ number(row.taxRate) }}%</td>
                  <td>{{ money(row.taxableAmount) }}</td>
                  <td>{{ money(row.taxAmount) }}</td>
                  <td>{{ money(row.amount) }}</td>
                  <td>{{ number(row.quantity) }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </UCard>
      </div>

      <UCard class="planner-card">
        <template #header>
          <div class="card-header-row">
            <div>
              <h2>Invoice-wise evidence</h2>
              <p>Recent invoices with payment, stock, journal, barcode and replacement evidence.</p>
            </div>
          </div>
        </template>
        <div class="table-scroll">
          <table class="qa-table">
            <thead>
              <tr>
                <th>Date</th>
                <th>Invoice</th>
                <th>Customer</th>
                <th>Status</th>
                <th>Payment</th>
                <th>Bill</th>
                <th>Paid rows</th>
                <th>Balance</th>
                <th>Evidence</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="row in invoiceEvidence" :key="row.invoiceId">
                <td>{{ row.onDate?.slice?.(0, 10) || row.onDate }}</td>
                <td><NuxtLink :to="row.sourcePath" class="link">{{ row.invoiceNumber }}</NuxtLink></td>
                <td>{{ row.customerName }}</td>
                <td>{{ row.status }}</td>
                <td>
                  <div>{{ row.paymentMode }}</div>
                  <small>{{ row.paymentRowCount }} row(s), mixed: {{ row.hasMixedPayment ? 'Yes' : 'No' }}</small>
                </td>
                <td>{{ money(row.billAmount) }}</td>
                <td>{{ money(row.paymentRowsTotal) }}</td>
                <td>{{ money(row.balanceAmount) }}</td>
                <td>
                  <div class="mini-checks">
                    <UBadge :label="row.hasAccountingJournal ? 'Journal' : 'No journal'" :color="row.hasAccountingJournal ? 'success' : 'error'" variant="subtle" />
                    <UBadge :label="row.hasStockOut ? 'Stock' : 'No stock'" :color="row.hasStockOut ? 'success' : 'error'" variant="subtle" />
                    <UBadge :label="row.missingBankMappingRows ? 'Bank gap' : 'Bank OK'" :color="row.missingBankMappingRows ? 'error' : 'success'" variant="subtle" />
                    <UBadge :label="row.missingBarcodeLines ? 'Barcode gap' : 'Barcode OK'" :color="row.missingBarcodeLines ? 'error' : 'success'" variant="subtle" />
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </UCard>

      <div class="two-column-grid">
        <UCard class="planner-card">
          <template #header><h2>Final closeout checklist</h2></template>
          <ul class="bullet-list">
            <li v-for="item in checklist" :key="item">{{ item }}</li>
          </ul>
        </UCard>

        <UCard class="planner-card">
          <template #header><h2>Operator rules</h2></template>
          <ul class="bullet-list">
            <li v-for="item in operatorRules" :key="item">{{ item }}</li>
          </ul>
        </UCard>
      </div>

      <div class="two-column-grid">
        <UCard class="planner-card">
          <template #header><h2>Known limitations</h2></template>
          <ul class="bullet-list muted-list">
            <li v-for="item in knownLimitations" :key="item">{{ item }}</li>
          </ul>
        </UCard>

        <UCard class="planner-card">
          <template #header><h2>Next module candidates</h2></template>
          <ul class="bullet-list">
            <li v-for="item in nextModules" :key="item">{{ item }}</li>
          </ul>
        </UCard>
      </div>
    </section>
  </AppShell>
</template>

<style scoped>
.billing-final-qa-page {
  display: grid;
  gap: 1rem;
}

.header-actions,
.card-header-row,
.validation-filters,
.filter-actions {
  display: flex;
  align-items: center;
  gap: .75rem;
  flex-wrap: wrap;
}

.card-header-row {
  justify-content: space-between;
}

.card-header-row h2,
.planner-card h2 {
  font-size: 1rem;
  font-weight: 700;
}

.card-header-row p,
.muted,
.metric-card span,
.check-row p,
small {
  color: rgb(var(--color-gray-500));
  font-size: .8rem;
}

.validation-filters {
  align-items: end;
}

.status-grid,
.metric-grid,
.two-column-grid {
  display: grid;
  gap: 1rem;
}

.status-grid {
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
}

.metric-grid {
  grid-template-columns: repeat(auto-fit, minmax(190px, 1fr));
}

.two-column-grid {
  grid-template-columns: repeat(auto-fit, minmax(320px, 1fr));
}

.status-card strong,
.metric-card strong {
  display: block;
  font-size: 1.4rem;
  margin: .25rem 0;
}

.eyebrow,
.metric-card p {
  text-transform: uppercase;
  letter-spacing: .05em;
  font-size: .72rem;
  color: rgb(var(--color-gray-500));
}

.status-line {
  margin: .5rem 0;
}

.check-grid {
  display: grid;
  gap: .75rem;
}

.check-row {
  display: flex;
  gap: .75rem;
  align-items: flex-start;
  padding: .75rem;
  border: 1px solid rgb(var(--color-gray-200));
  border-radius: .85rem;
}

.dark .check-row {
  border-color: rgb(var(--color-gray-800));
}

.table-scroll {
  overflow-x: auto;
}

.qa-table {
  width: 100%;
  border-collapse: collapse;
  min-width: 860px;
}

.qa-table.compact {
  min-width: 520px;
}

.qa-table th,
.qa-table td {
  text-align: left;
  padding: .65rem;
  border-bottom: 1px solid rgb(var(--color-gray-200));
  vertical-align: top;
  font-size: .85rem;
}

.dark .qa-table th,
.dark .qa-table td {
  border-color: rgb(var(--color-gray-800));
}

.qa-table th {
  font-size: .72rem;
  text-transform: uppercase;
  letter-spacing: .04em;
  color: rgb(var(--color-gray-500));
}

.link {
  color: rgb(var(--color-primary-600));
  font-weight: 700;
}

.mini-checks {
  display: flex;
  flex-wrap: wrap;
  gap: .25rem;
}

.bullet-list {
  display: grid;
  gap: .55rem;
  padding-left: 1.2rem;
  list-style: disc;
  font-size: .9rem;
}

.muted-list {
  color: rgb(var(--color-gray-600));
}

@media (max-width: 720px) {
  .header-actions,
  .validation-filters {
    align-items: stretch;
    flex-direction: column;
  }
}
</style>
