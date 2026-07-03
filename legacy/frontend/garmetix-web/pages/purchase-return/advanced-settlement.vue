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

useHead({ title: 'Purchase Return Advanced Settlement | Garmetix' })

const selectedCompanyId = computed(() => workspace.companyId.value || companies.value[0]?.id || '')
const selectedStoreGroupId = computed(() => workspace.storeGroupId.value || '')
const selectedStoreId = computed(() => workspace.storeId.value || '')
const metrics = computed(() => report.value?.metrics || [])
const issues = computed(() => report.value?.issues || [])
const evidenceRows = computed(() => report.value?.evidenceRows || [])
const closeoutChecklist = computed(() => report.value?.closeoutChecklist || [])
const operatorRules = computed(() => report.value?.operatorRules || [])
const knownLimitations = computed(() => report.value?.knownLimitations || [])
const nextModules = computed(() => report.value?.nextModuleCandidates || [])
const criticalIssues = computed(() => report.value?.criticalIssues || issues.value.filter((item: any) => item.severity === 'Critical').length)
const warningIssues = computed(() => report.value?.warningIssues || issues.value.filter((item: any) => item.severity === 'Warning').length)

const amountDiffTotal = computed(() => evidenceRows.value.reduce((sum: number, row: any) => sum + rowDiff(row, 'headerReturnAmount', 'itemReturnAmount'), 0))
const itcDiffTotal = computed(() => evidenceRows.value.reduce((sum: number, row: any) => sum + rowDiff(row, 'itcReversalAmount', 'journalItcCreditAmount'), 0))

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
  if (!value) return '-'
  return new Date(value).toLocaleDateString('en-IN')
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

function rowDiff(row: any, left: string, right: string) {
  return Number(row?.[left] || 0) - Number(row?.[right] || 0)
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
    loadError.value ||= feedback.errorMessage(error, 'Workspace options could not be loaded.', 'Purchase return advanced settlement workspace load failed')
  }
}

async function loadReport() {
  if (!auth.isAuthenticated.value) return
  loading.value = true
  loadError.value = ''
  try {
    report.value = await api.get<any>(`purchase-return/advanced-settlement?${query()}`)
    const status = report.value?.status || 'Not Complete'
    feedback.notify('Purchase return advanced settlement refreshed', `${criticalIssues.value} critical and ${warningIssues.value} warning issue(s).`, status === 'Complete' ? 'success' : 'warning')
  } catch (error: any) {
    loadError.value = feedback.errorMessage(error, 'Purchase return advanced settlement could not be loaded. Check API logs and permissions.', 'Purchase return advanced settlement failed')
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
    const response = await fetch(`${config.public.apiBase}/purchase-return/advanced-settlement/evidence.csv?${query()}`, { headers: api.authHeaders() })
    if (!response.ok) throw new Error(await response.text())
    const blob = await response.blob()
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = `purchase-return-advanced-settlement-${filters.from}-${filters.to}.csv`
    link.click()
    URL.revokeObjectURL(url)
  } catch (error: any) {
    feedback.fromError(error, 'Purchase return advanced settlement CSV export failed')
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
    title="Purchase Return Settlement"
    :companies="companies"
    :stores="stores"
    @refresh="refreshAll"
    @workspace-change="loadReport"
  >
    <section class="purchase-return-advanced-page">
      <UiModulePageHeader
        title="Purchase return advanced settlement acceptance"
        description="Formal supplier return document, exact ITC reversal, vendor debit-note settlement, refund bank proof and purchase-return accounting audit in one closeout page."
        icon="i-lucide-undo-2"
      >
        <template #actions>
          <div class="header-actions">
            <UBadge :label="report?.status || 'Not checked'" :color="statusColor(report?.status)" :icon="statusIcon(report?.status)" variant="subtle" />
            <UButton icon="i-lucide-refresh-cw" label="Run acceptance" :loading="loading" @click="loadReport" />
            <UButton icon="i-lucide-download" label="CSV evidence" variant="subtle" :loading="exporting" @click="exportCsv" />
            <UButton to="/purchase-return" icon="i-lucide-undo-2" label="Purchase Return" variant="ghost" />
            <UButton to="/vendor-settlements" icon="i-lucide-hand-coins" label="Vendor Settlements" variant="ghost" />
          </div>
        </template>
      </UiModulePageHeader>

      <UAlert
        v-if="loadError"
        color="error"
        variant="subtle"
        icon="i-lucide-circle-alert"
        title="Purchase return advanced settlement is unavailable"
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
        <UCard class="status-card">
          <p class="eyebrow">Final status</p>
          <div class="status-line">
            <UBadge :label="report?.status || 'Not checked'" :color="statusColor(report?.status)" :icon="statusIcon(report?.status)" size="lg" />
          </div>
          <p class="muted">Complete only when document, stock, ITC, debit note, settlement, refund and journal evidence has no issue.</p>
        </UCard>
        <UCard class="status-card">
          <p class="eyebrow">Critical</p>
          <strong>{{ formatNumber(criticalIssues) }}</strong>
          <p class="muted">Blocks purchase/GST closeout.</p>
        </UCard>
        <UCard class="status-card">
          <p class="eyebrow">Warnings</p>
          <strong>{{ formatNumber(warningIssues) }}</strong>
          <p class="muted">Review before owner sign-off.</p>
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
              <h2>Acceptance issues</h2>
              <p>Critical issues must be fixed before purchase, GST or FY closeout.</p>
            </div>
            <UBadge :label="`${issues.length} issue(s)`" color="neutral" variant="subtle" />
          </div>
        </template>
        <div v-if="!issues.length" class="empty-panel">
          <UIcon name="i-lucide-circle-check" />
          <p>No issues found for the selected period.</p>
        </div>
        <div v-else class="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Severity</th>
                <th>Code</th>
                <th>Return</th>
                <th>Date</th>
                <th>Vendor</th>
                <th>Message</th>
                <th class="right">Amount/Diff</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="issue in issues" :key="`${issue.purchaseReturnId}-${issue.code}`">
                <td><UBadge :label="issue.severity" :color="statusColor(issue.severity)" variant="subtle" /></td>
                <td>{{ issue.code }}</td>
                <td>{{ issue.returnNumber }}</td>
                <td>{{ formatDate(issue.onDate) }}</td>
                <td>{{ issue.vendorName }}</td>
                <td>{{ issue.message }}</td>
                <td class="right">{{ issue.amount === null || issue.amount === undefined ? '-' : money(issue.amount) }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </UCard>

      <UCard>
        <template #header>
          <div class="card-header-line">
            <div>
              <h2>Purchase return evidence</h2>
              <p>Document, ITC reversal, debit-note settlement, refund and journal audit by return number.</p>
            </div>
            <UBadge :label="`${evidenceRows.length} row(s)`" color="neutral" variant="subtle" />
          </div>
        </template>
        <div class="table-wrap evidence-table">
          <table>
            <thead>
              <tr>
                <th>Return</th>
                <th>Date</th>
                <th>Vendor</th>
                <th class="right">Amount</th>
                <th class="right">GST</th>
                <th class="right">ITC</th>
                <th class="right">Journal ITC</th>
                <th class="right">Stock Out</th>
                <th>Debit Note</th>
                <th class="right">Settled</th>
                <th class="right">Refund</th>
                <th>Print</th>
                <th>Status</th>
                <th>Open</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="row in evidenceRows" :key="row.purchaseReturnId">
                <td>{{ row.returnNumber }}</td>
                <td>{{ formatDate(row.onDate) }}</td>
                <td>{{ row.vendorName }}</td>
                <td class="right">{{ money(row.headerReturnAmount) }}</td>
                <td class="right">{{ money(row.headerTaxAmount) }}</td>
                <td class="right">{{ money(row.itcReversalAmount) }}</td>
                <td class="right">{{ money(row.journalItcCreditAmount) }}</td>
                <td class="right">{{ formatNumber(row.stockOutQuantity) }}</td>
                <td>{{ row.debitNoteNumber || '-' }}</td>
                <td class="right">{{ money(row.settlementTotalAmount) }}</td>
                <td class="right">{{ money(row.settlementRefundAmount) }}</td>
                <td><UBadge :label="row.printed ? `Printed ${row.printCount}` : 'Not printed'" :color="row.printed ? 'success' : 'warning'" variant="subtle" /></td>
                <td><UBadge :label="row.evidenceStatus" :color="statusColor(row.evidenceStatus)" variant="subtle" /></td>
                <td>
                  <UButton :to="`/purchase-return?returnId=${row.purchaseReturnId}`" size="xs" variant="ghost" icon="i-lucide-eye" label="Return" />
                </td>
              </tr>
            </tbody>
          </table>
        </div>
        <template #footer>
          <p class="muted">Difference helpers: amount diff {{ money(amountDiffTotal) }}, ITC diff {{ money(itcDiffTotal) }}.</p>
        </template>
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
.purchase-return-advanced-page { display: flex; flex-direction: column; gap: 1rem; }
.header-actions { display: flex; flex-wrap: wrap; gap: .5rem; align-items: center; justify-content: flex-end; }
.validation-filters { display: grid; grid-template-columns: repeat(3, minmax(180px, 1fr)); gap: 1rem; align-items: end; }
.filter-actions { display: flex; align-items: end; }
.status-grid, .metric-grid, .info-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(230px, 1fr)); gap: 1rem; }
.status-card strong, .metric-card strong { display: block; font-size: 1.8rem; line-height: 1; margin: .35rem 0; }
.metric-card p, .eyebrow { color: rgb(var(--color-gray-500)); font-size: .78rem; font-weight: 700; letter-spacing: .08em; text-transform: uppercase; }
.metric-card span, .muted { color: rgb(var(--color-gray-500)); font-size: .9rem; }
.status-line { margin: .5rem 0; }
.card-header-line { display: flex; justify-content: space-between; gap: 1rem; align-items: flex-start; }
.card-header-line h2 { font-size: 1.05rem; font-weight: 700; }
.card-header-line p { color: rgb(var(--color-gray-500)); font-size: .9rem; margin-top: .15rem; }
.table-wrap { overflow-x: auto; }
table { width: 100%; border-collapse: collapse; font-size: .86rem; }
th, td { padding: .65rem .75rem; border-bottom: 1px solid rgb(var(--color-gray-200)); vertical-align: top; white-space: nowrap; }
th { text-align: left; color: rgb(var(--color-gray-500)); font-size: .72rem; letter-spacing: .08em; text-transform: uppercase; }
.right { text-align: right; }
.evidence-table td, .evidence-table th { white-space: nowrap; }
.empty-panel { display: flex; gap: .6rem; align-items: center; color: rgb(var(--color-gray-500)); padding: 1rem; }
.empty-panel .iconify { font-size: 1.2rem; color: rgb(var(--color-primary-500)); }
.check-list { display: grid; gap: .55rem; padding-left: 1.1rem; color: rgb(var(--color-gray-600)); font-size: .92rem; }
@media (max-width: 760px) { .validation-filters { grid-template-columns: 1fr; } }
</style>
