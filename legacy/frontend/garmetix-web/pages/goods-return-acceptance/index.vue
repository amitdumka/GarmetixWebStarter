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

useHead({ title: 'Goods Return Acceptance | Garmetix' })

const selectedCompanyId = computed(() => workspace.companyId.value || companies.value[0]?.id || '')
const selectedStoreGroupId = computed(() => workspace.storeGroupId.value || '')
const selectedStoreId = computed(() => workspace.storeId.value || '')
const metrics = computed(() => report.value?.metrics || [])
const buckets = computed(() => report.value?.creditNoteBuckets || [])
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

function formatDate(value: string | null | undefined) {
  return value ? new Date(value).toLocaleDateString('en-IN') : '-'
}

function metricValue(metric: any) {
  return metric?.amount !== null && metric?.amount !== undefined ? money(metric.amount) : formatNumber(metric?.count)
}

function statusColor(status: string | undefined) {
  if (status === 'Complete' || status === 'Pass' || status === 'Within 7 days' || status === 'Fully adjusted' || status === 'Valid open') return 'success'
  if (status === 'Warning' || status === 'Expiring soon' || status === 'No credit note') return 'warning'
  if (status === 'Critical' || status === 'Not Complete' || status === 'Expired open' || status === 'Outside 7 days' || status === 'Missing original') return 'error'
  return 'neutral'
}

function statusIcon(status: string | undefined) {
  if (status === 'Complete' || status === 'Pass') return 'i-lucide-circle-check'
  if (status === 'Warning' || status === 'Expiring soon') return 'i-lucide-triangle-alert'
  if (status === 'Critical' || status === 'Not Complete' || status === 'Expired open') return 'i-lucide-circle-alert'
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
    loadError.value ||= feedback.errorMessage(error, 'Workspace options could not be loaded.', 'Goods return acceptance workspace load failed')
  }
}

async function loadReport() {
  if (!auth.isAuthenticated.value) return
  loading.value = true
  loadError.value = ''
  try {
    report.value = await api.get<any>(`goods-return/acceptance?${query()}`)
    const status = report.value?.status || 'Not Complete'
    feedback.notify('Goods return acceptance refreshed', `${criticalIssues.value} critical and ${warningIssues.value} warning issue(s).`, status === 'Complete' ? 'success' : 'warning')
  } catch (error: any) {
    loadError.value = feedback.errorMessage(error, 'Goods return acceptance could not be loaded. Check API logs and permissions.', 'Goods return acceptance failed')
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
    const response = await fetch(`${config.public.apiBase}/goods-return/acceptance/evidence.csv?${query()}`, { headers: api.authHeaders() })
    if (!response.ok) throw new Error(await response.text())
    const blob = await response.blob()
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = `goods-return-acceptance-${filters.from}-${filters.to}.csv`
    link.click()
    URL.revokeObjectURL(url)
  } catch (error: any) {
    feedback.fromError(error, 'Goods return acceptance CSV export failed')
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
    title="Goods Return Acceptance"
    :companies="companies"
    :stores="stores"
    @refresh="refreshAll"
    @workspace-change="loadReport"
  >
    <section class="goods-return-acceptance-page">
      <UiModulePageHeader
        title="Goods return / exchange operational acceptance"
        description="Validate 7-day exchange eligibility, no-refund policy, return-generated credit notes, replacement exchange linkage and credit-note expiry before closing customer return operations."
        icon="i-lucide-shield-check"
      >
        <template #actions>
          <div class="header-actions">
            <UBadge :label="report?.status || 'Not checked'" :color="statusColor(report?.status)" :icon="statusIcon(report?.status)" variant="subtle" />
            <UButton icon="i-lucide-refresh-cw" label="Run acceptance" :loading="loading" @click="loadReport" />
            <UButton icon="i-lucide-download" label="CSV evidence" variant="subtle" :loading="exporting" @click="exportCsv" />
            <UButton to="/sales-return" icon="i-lucide-rotate-ccw" label="Sales Return" variant="ghost" />
          </div>
        </template>
      </UiModulePageHeader>

      <UAlert
        v-if="loadError"
        color="error"
        variant="subtle"
        icon="i-lucide-circle-alert"
        title="Goods return acceptance is unavailable"
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
          <p class="muted">Complete only when returns follow the 7-day exchange/no-refund policy and every open credit note has valid evidence.</p>
        </UCard>
        <UCard class="status-card">
          <p class="eyebrow">Critical</p>
          <strong>{{ formatNumber(criticalIssues) }}</strong>
          <p class="muted">Blocks goods-return closeout.</p>
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
              <h2>Credit-note expiry tracking</h2>
              <p>Policy: April-December credit notes are valid up to 31 March of the financial year. Jan/Feb/Mar credit notes get six-month validity.</p>
            </div>
          </div>
        </template>
        <div class="planner-table-wrap">
          <table class="planner-table">
            <thead><tr><th>Bucket</th><th class="text-right">Rows</th><th class="text-right">Open amount</th><th>Description</th></tr></thead>
            <tbody>
              <tr v-for="bucket in buckets" :key="bucket.bucket">
                <td class="font-medium">{{ bucket.bucket }}</td>
                <td class="text-right">{{ formatNumber(bucket.count) }}</td>
                <td class="text-right">{{ money(bucket.openAmount) }}</td>
                <td>{{ bucket.description }}</td>
              </tr>
              <tr v-if="!buckets.length"><td colspan="4" class="empty-cell">No credit-note bucket evidence found.</td></tr>
            </tbody>
          </table>
        </div>
      </UCard>

      <UCard>
        <template #header>
          <div class="card-header-line">
            <div>
              <h2>Blocking / warning issues</h2>
              <p>Correct these through controlled return/exchange correction, credit-note adjustment, customer settlement or documented owner approval.</p>
            </div>
          </div>
        </template>
        <div class="planner-table-wrap">
          <table class="planner-table">
            <thead><tr><th>Severity</th><th>Code</th><th>Return</th><th>Date</th><th>Customer</th><th>Message</th><th class="text-right">Amount</th></tr></thead>
            <tbody>
              <tr v-for="issue in issues" :key="`${issue.returnInvoiceId}-${issue.code}-${issue.message}`">
                <td><UBadge :label="issue.severity" :color="statusColor(issue.severity)" variant="subtle" /></td>
                <td class="font-mono text-xs">{{ issue.code }}</td>
                <td class="font-medium">{{ issue.returnInvoiceNumber }}</td>
                <td>{{ formatDate(issue.returnDate) }}</td>
                <td>{{ issue.customerName }}<br><span class="muted">{{ issue.mobileNumber || '-' }}</span></td>
                <td>{{ issue.message }}</td>
                <td class="text-right">{{ money(issue.amount) }}</td>
              </tr>
              <tr v-if="!issues.length"><td colspan="7" class="empty-cell">No blocking or warning issue in this range.</td></tr>
            </tbody>
          </table>
        </div>
      </UCard>

      <UCard>
        <template #header>
          <div class="card-header-line">
            <div>
              <h2>Return / exchange evidence</h2>
              <p>Invoice-wise proof matrix for sale date, return date, credit note expiry and replacement exchange linkage.</p>
            </div>
          </div>
        </template>
        <div class="planner-table-wrap">
          <table class="planner-table evidence-table">
            <thead>
              <tr>
                <th>Status</th><th>Return</th><th>Original sale</th><th>Days</th><th>Customer</th><th class="text-right">Return</th><th class="text-right">Refund</th><th>Credit note</th><th>Expiry</th><th>Exchange</th><th class="text-right">Open credit</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="row in evidenceRows" :key="row.returnInvoiceId">
                <td><UBadge :label="row.status" :color="statusColor(row.status)" variant="subtle" /></td>
                <td class="font-medium">{{ row.returnInvoiceNumber }}<br><span class="muted">{{ formatDate(row.returnDate) }}</span></td>
                <td>{{ row.originalInvoiceNumber || '-' }}<br><span class="muted">{{ formatDate(row.originalInvoiceDate) }}</span></td>
                <td><UBadge :label="row.policyDaysStatus" :color="statusColor(row.policyDaysStatus)" variant="subtle" /><br><span class="muted">{{ row.daysFromSale ?? '-' }} day(s)</span></td>
                <td>{{ row.customerName }}<br><span class="muted">{{ row.mobileNumber || '-' }}</span></td>
                <td class="text-right">{{ money(row.returnAmount) }}</td>
                <td class="text-right">{{ money(row.refundAmount) }}</td>
                <td>{{ row.creditNoteNumber || '-' }}<br><span class="muted">{{ row.creditNoteCount }} note(s)</span></td>
                <td><UBadge :label="row.expiryStatus" :color="statusColor(row.expiryStatus)" variant="subtle" /><br><span class="muted">{{ formatDate(row.creditNoteExpiryDate) }}</span></td>
                <td>{{ row.exchangeInvoiceNumbers || '-' }}<br><span class="muted">{{ money(row.exchangeInvoiceValue) }} / credit {{ money(row.exchangeCreditApplied) }}</span></td>
                <td class="text-right">{{ money(row.creditNoteOpenAmount) }}</td>
              </tr>
              <tr v-if="!evidenceRows.length"><td colspan="11" class="empty-cell">No goods return evidence found in this range.</td></tr>
            </tbody>
          </table>
        </div>
      </UCard>

      <div class="closure-grid">
        <UCard>
          <template #header><h2>Final closeout checklist</h2></template>
          <ul class="check-list"><li v-for="item in closeoutChecklist" :key="item">{{ item }}</li></ul>
        </UCard>
        <UCard>
          <template #header><h2>Operator rules</h2></template>
          <ul class="check-list"><li v-for="item in operatorRules" :key="item">{{ item }}</li></ul>
        </UCard>
      </div>

      <div class="closure-grid">
        <UCard>
          <template #header><h2>Known limitations</h2></template>
          <ul class="check-list muted-list"><li v-for="item in knownLimitations" :key="item">{{ item }}</li></ul>
        </UCard>
        <UCard>
          <template #header><h2>Recommended next modules</h2></template>
          <ul class="check-list"><li v-for="item in nextModules" :key="item">{{ item }}</li></ul>
        </UCard>
      </div>
    </section>
  </AppShell>
</template>

<style scoped>
.goods-return-acceptance-page { display: grid; gap: 1rem; padding: 1rem; }
.header-actions { display: flex; flex-wrap: wrap; align-items: center; gap: 0.5rem; }
.validation-filters { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 1rem; align-items: end; }
.filter-actions { display: flex; justify-content: flex-start; }
.status-grid { display: grid; grid-template-columns: 2fr 1fr 1fr; gap: 1rem; }
.status-card { min-height: 9rem; }
.status-card strong { display: block; margin-top: 0.4rem; font-size: 2rem; }
.status-line { margin: 0.8rem 0; }
.metric-grid { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 1rem; }
.metric-card p { margin: 0; color: rgb(100 116 139); font-size: 0.8rem; text-transform: uppercase; letter-spacing: 0.08em; }
.metric-card strong { display: block; margin-top: 0.35rem; font-size: 1.4rem; }
.metric-card span, .muted { color: rgb(100 116 139); font-size: 0.82rem; }
.eyebrow { color: rgb(100 116 139); font-size: 0.75rem; text-transform: uppercase; letter-spacing: 0.1em; }
.card-header-line { display: flex; justify-content: space-between; gap: 1rem; align-items: flex-start; }
.card-header-line h2 { margin: 0; font-size: 1.05rem; font-weight: 700; }
.card-header-line p { margin: 0.2rem 0 0; color: rgb(100 116 139); }
.planner-table-wrap { max-width: 100%; overflow-x: auto; }
.planner-table { width: 100%; border-collapse: collapse; font-size: 0.86rem; }
.planner-table th, .planner-table td { border-bottom: 1px solid rgb(226 232 240); padding: 0.65rem 0.55rem; text-align: left; vertical-align: top; }
.planner-table th { color: rgb(71 85 105); font-size: 0.74rem; text-transform: uppercase; letter-spacing: 0.08em; white-space: nowrap; }
.text-right { text-align: right !important; }
.font-medium { font-weight: 600; }
.font-mono { font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace; }
.text-xs { font-size: 0.72rem; }
.empty-cell { padding: 1.2rem !important; text-align: center !important; color: rgb(100 116 139); }
.evidence-table { min-width: 1180px; }
.closure-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 1rem; }
.check-list { margin: 0; padding-left: 1.1rem; display: grid; gap: 0.55rem; }
.muted-list { color: rgb(71 85 105); }
@media (max-width: 1100px) { .status-grid, .metric-grid, .closure-grid, .validation-filters { grid-template-columns: 1fr; } }
</style>
