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
const filters = reactive({
  from: inputDate(new Date(today.getFullYear(), today.getMonth(), 1)),
  to: inputDate(today)
})

useHead({ title: 'Bank Reconciliation Closure | Garmetix' })

const selectedCompanyId = computed(() => workspace.companyId.value || companies.value[0]?.id || '')
const selectedStoreGroupId = computed(() => workspace.storeGroupId.value || '')
const selectedStoreId = computed(() => workspace.storeId.value || '')
const metrics = computed(() => report.value?.metrics || [])
const issues = computed(() => report.value?.issues || [])
const modeSummary = computed(() => report.value?.paymentModeSummary || [])
const bankAccounts = computed(() => report.value?.bankAccounts || [])
const settlementRows = computed(() => report.value?.settlementRows || [])
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

function numberValue(value: number | string | null | undefined) {
  return new Intl.NumberFormat('en-IN', { maximumFractionDigits: 2 }).format(Number(value || 0))
}

function formatDate(value: string | null | undefined) {
  return value ? new Date(value).toLocaleDateString('en-IN') : '-'
}

function metricValue(metric: any) {
  return metric?.amount !== null && metric?.amount !== undefined ? money(metric.amount) : numberValue(metric?.count)
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
    loadError.value ||= feedback.errorMessage(error, 'Workspace options could not be loaded.', 'Bank reconciliation workspace load failed')
  }
}

async function loadReport() {
  if (!auth.isAuthenticated.value) return
  loading.value = true
  loadError.value = ''
  try {
    report.value = await api.get<any>(`bank-reconciliation/settlement-closure?${query()}`)
    const status = report.value?.status || 'Not Complete'
    feedback.notify('Bank reconciliation closure refreshed', `${criticalIssues.value} critical and ${warningIssues.value} warning issue(s).`, status === 'Complete' ? 'success' : 'warning')
  } catch (error: any) {
    loadError.value = feedback.errorMessage(error, 'Bank reconciliation closure could not be loaded. Check API logs and permissions.', 'Bank reconciliation closure failed')
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
    const response = await fetch(`${config.public.apiBase}/bank-reconciliation/settlement-closure/evidence.csv?${query()}`, { headers: api.authHeaders() })
    if (!response.ok) throw new Error(await response.text())
    const blob = await response.blob()
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = `bank-reconciliation-closure-${filters.from}-${filters.to}.csv`
    link.click()
    URL.revokeObjectURL(url)
  } catch (error: any) {
    feedback.fromError(error, 'Bank reconciliation CSV export failed')
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
    title="Bank Reco Closure"
    :companies="companies"
    :stores="stores"
    @refresh="refreshAll"
    @workspace-change="loadReport"
  >
    <section class="bank-reco-page">
      <UiModulePageHeader
        title="Bank reconciliation / payment settlement closure"
        description="Final settlement control for UPI, card, NEFT, RTGS, IMPS, cheque, wallet, customer advance, vendor payment, voucher and salary bank proof before month/FY close."
        icon="i-lucide-banknote"
      >
        <template #actions>
          <div class="header-actions">
            <UBadge :label="report?.status || 'Not checked'" :color="statusColor(report?.status)" :icon="statusIcon(report?.status)" variant="subtle" />
            <UButton icon="i-lucide-refresh-cw" label="Run closure" :loading="loading" @click="loadReport" />
            <UButton icon="i-lucide-download" label="CSV evidence" variant="subtle" :loading="exporting" @click="exportCsv" />
            <UButton to="/financial-year-closeout" icon="i-lucide-lock-keyhole" label="FY Closeout" variant="ghost" />
          </div>
        </template>
      </UiModulePageHeader>

      <UAlert
        v-if="loadError"
        color="error"
        variant="subtle"
        icon="i-lucide-circle-alert"
        title="Bank reconciliation closure is unavailable"
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
        <UCard class="status-card hero-status">
          <p class="eyebrow">Settlement closure status</p>
          <div class="status-line">
            <UBadge :label="report?.status || 'Not checked'" :color="statusColor(report?.status)" :icon="statusIcon(report?.status)" size="lg" />
          </div>
          <p class="muted">Complete only when all Critical settlement mapping and bank-transaction journal issues are clear.</p>
          <p class="fy-label">{{ formatDate(report?.from) }} to {{ formatDate(report?.to) }}</p>
        </UCard>
        <UCard class="status-card">
          <p class="eyebrow">Critical</p>
          <strong>{{ numberValue(criticalIssues) }}</strong>
          <p class="muted">Blocks closeout.</p>
        </UCard>
        <UCard class="status-card">
          <p class="eyebrow">Warnings</p>
          <strong>{{ numberValue(warningIssues) }}</strong>
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
              <h2>Payment mode reconciliation</h2>
              <p>Non-cash modes must have bank mapping, reference and settlement proof.</p>
            </div>
          </div>
        </template>
        <div class="planner-table-wrap">
          <table class="planner-table">
            <thead><tr><th>Mode</th><th>Rows</th><th>Amount</th><th>Missing bank</th><th>Missing ref</th><th>Matched</th><th>Reconciled</th></tr></thead>
            <tbody>
              <tr v-for="row in modeSummary" :key="row.paymentMode">
                <td>{{ row.paymentMode }}</td>
                <td>{{ numberValue(row.count) }}</td>
                <td>{{ money(row.amount) }}</td>
                <td><UBadge :label="String(row.missingBankAccount)" :color="row.missingBankAccount ? 'error' : 'success'" variant="subtle" /></td>
                <td><UBadge :label="String(row.missingReference)" :color="row.missingReference ? 'warning' : 'success'" variant="subtle" /></td>
                <td>{{ numberValue(row.matched) }}</td>
                <td>{{ numberValue(row.reconciled) }}</td>
              </tr>
              <tr v-if="!modeSummary.length"><td colspan="7" class="empty-cell">No non-cash settlement rows found for this period.</td></tr>
            </tbody>
          </table>
        </div>
      </UCard>

      <UCard>
        <template #header>
          <div class="card-header-line">
            <div>
              <h2>Bank account evidence</h2>
              <p>Account-wise settlement, transaction and statement evidence.</p>
            </div>
          </div>
        </template>
        <div class="planner-table-wrap">
          <table class="planner-table">
            <thead><tr><th>Account</th><th>Active</th><th>Settlement rows</th><th>Settlement amount</th><th>Bank tx</th><th>Deposit</th><th>Withdraw</th><th>Statement rows</th><th>Unreconciled</th></tr></thead>
            <tbody>
              <tr v-for="row in bankAccounts" :key="row.bankAccountId">
                <td><strong>{{ row.accountHolderName || '-' }}</strong><br><span class="muted">{{ row.maskedAccountNumber }}</span></td>
                <td><UBadge :label="row.active ? 'Active' : 'Inactive'" :color="row.active ? 'success' : 'neutral'" variant="subtle" /></td>
                <td>{{ numberValue(row.settlementRows) }}</td>
                <td>{{ money(row.settlementAmount) }}</td>
                <td>{{ numberValue(row.bankTransactionRows) }}</td>
                <td>{{ money(row.bankTransactionDeposit) }}</td>
                <td>{{ money(row.bankTransactionWithdraw) }}</td>
                <td>{{ numberValue(row.statementRows) }}</td>
                <td>{{ numberValue(row.unreconciledBankTransactions + row.unmatchedStatementLines) }}</td>
              </tr>
              <tr v-if="!bankAccounts.length"><td colspan="9" class="empty-cell">No bank accounts found for this workspace.</td></tr>
            </tbody>
          </table>
        </div>
      </UCard>

      <UCard>
        <template #header>
          <div class="card-header-line">
            <div>
              <h2>Blocking and warning issues</h2>
              <p>Clear Critical items before locking the month or financial year.</p>
            </div>
          </div>
        </template>
        <div class="planner-table-wrap">
          <table class="planner-table">
            <thead><tr><th>Severity</th><th>Code</th><th>Message</th><th>Action</th></tr></thead>
            <tbody>
              <tr v-for="issue in issues" :key="`${issue.code}-${issue.sourceId}`">
                <td><UBadge :label="issue.severity" :color="statusColor(issue.severity)" :icon="statusIcon(issue.severity)" variant="subtle" /></td>
                <td>{{ issue.code }}</td>
                <td>{{ issue.message }}</td>
                <td><UButton :to="issue.actionPath" icon="i-lucide-arrow-up-right" label="Open" size="xs" variant="ghost" /></td>
              </tr>
              <tr v-if="!issues.length"><td colspan="4" class="empty-cell">No blocking or warning issues found.</td></tr>
            </tbody>
          </table>
        </div>
      </UCard>

      <UCard>
        <template #header>
          <div class="card-header-line">
            <div>
              <h2>Settlement evidence rows</h2>
              <p>Latest non-cash rows requiring bank proof, capped for fast page load. Export CSV for full evidence packet.</p>
            </div>
          </div>
        </template>
        <div class="planner-table-wrap">
          <table class="planner-table compact">
            <thead><tr><th>Date</th><th>Direction</th><th>Source</th><th>Party</th><th>Mode</th><th>Amount</th><th>Bank</th><th>Reference</th><th>Matched</th><th>Notes</th></tr></thead>
            <tbody>
              <tr v-for="row in settlementRows" :key="`${row.sourceType}-${row.sourceId}`">
                <td>{{ formatDate(row.onDate) }}</td>
                <td><UBadge :label="row.direction" :color="row.direction === 'In' ? 'success' : 'warning'" variant="subtle" /></td>
                <td><strong>{{ row.sourceType }}</strong><br><span class="muted">{{ row.sourceNumber }}</span></td>
                <td>{{ row.partyName }}</td>
                <td>{{ row.paymentMode }}</td>
                <td>{{ money(row.amount) }}</td>
                <td><UBadge :label="row.hasBankAccount ? 'Mapped' : 'Missing'" :color="row.hasBankAccount ? 'success' : 'error'" variant="subtle" /></td>
                <td><UBadge :label="row.hasReference ? 'Yes' : 'No'" :color="row.hasReference ? 'success' : 'warning'" variant="subtle" /></td>
                <td><UBadge :label="row.reconciled ? 'Reconciled' : row.matchedBankTransaction ? 'Matched' : 'Pending'" :color="row.reconciled ? 'success' : row.matchedBankTransaction ? 'warning' : 'neutral'" variant="subtle" /></td>
                <td>{{ row.notes }}</td>
              </tr>
              <tr v-if="!settlementRows.length"><td colspan="10" class="empty-cell">No non-cash settlement evidence rows found.</td></tr>
            </tbody>
          </table>
        </div>
      </UCard>

      <div class="info-grid">
        <UCard>
          <template #header><h2>Final closeout checklist</h2></template>
          <ul class="info-list"><li v-for="item in closeoutChecklist" :key="item">{{ item }}</li></ul>
        </UCard>
        <UCard>
          <template #header><h2>Operator rules</h2></template>
          <ul class="info-list"><li v-for="item in operatorRules" :key="item">{{ item }}</li></ul>
        </UCard>
        <UCard>
          <template #header><h2>Known limitations</h2></template>
          <ul class="info-list"><li v-for="item in knownLimitations" :key="item">{{ item }}</li></ul>
        </UCard>
        <UCard>
          <template #header><h2>Next module candidates</h2></template>
          <ul class="info-list"><li v-for="item in nextModules" :key="item">{{ item }}</li></ul>
        </UCard>
      </div>
    </section>
  </AppShell>
</template>

<style scoped>
.bank-reco-page { display: grid; gap: 1rem; }
.header-actions { display: flex; gap: .5rem; flex-wrap: wrap; align-items: center; justify-content: flex-end; }
.validation-filters { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: .75rem; align-items: end; }
.filter-actions { display: flex; gap: .5rem; align-items: end; }
.status-grid { display: grid; grid-template-columns: minmax(260px, 2fr) repeat(2, minmax(160px, 1fr)); gap: 1rem; }
.status-card { display: grid; gap: .35rem; }
.status-card strong { font-size: 1.8rem; }
.hero-status { min-height: 150px; }
.status-line { margin: .35rem 0; }
.eyebrow { text-transform: uppercase; letter-spacing: .08em; font-size: .72rem; color: rgb(var(--ui-color-neutral-500)); font-weight: 700; }
.muted { color: rgb(var(--ui-color-neutral-500)); font-size: .88rem; }
.fy-label { font-weight: 700; margin-top: .25rem; }
.metric-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(210px, 1fr)); gap: 1rem; }
.metric-card { min-height: 130px; }
.metric-card p { color: rgb(var(--ui-color-neutral-500)); font-size: .82rem; }
.metric-card strong { display: block; margin: .35rem 0; font-size: 1.35rem; }
.metric-card span { color: rgb(var(--ui-color-neutral-500)); font-size: .82rem; }
.card-header-line { display: flex; justify-content: space-between; gap: 1rem; align-items: flex-start; }
.card-header-line h2 { margin: 0; font-size: 1rem; }
.card-header-line p { margin: .25rem 0 0; color: rgb(var(--ui-color-neutral-500)); }
.planner-table-wrap { overflow-x: auto; }
.planner-table { width: 100%; border-collapse: collapse; font-size: .86rem; }
.planner-table th, .planner-table td { padding: .65rem .75rem; border-bottom: 1px solid rgb(var(--ui-color-neutral-200)); text-align: left; vertical-align: top; }
.planner-table th { color: rgb(var(--ui-color-neutral-500)); font-size: .72rem; text-transform: uppercase; letter-spacing: .04em; white-space: nowrap; }
.planner-table.compact { font-size: .8rem; }
.empty-cell { text-align: center !important; color: rgb(var(--ui-color-neutral-500)); padding: 1.25rem !important; }
.info-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(260px, 1fr)); gap: 1rem; }
.info-list { margin: 0; padding-left: 1.1rem; color: rgb(var(--ui-color-neutral-700)); display: grid; gap: .45rem; }
@media (max-width: 820px) { .status-grid { grid-template-columns: 1fr; } .header-actions { justify-content: flex-start; } }
</style>
