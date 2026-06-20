<script setup lang="ts">
const api = useGarmetixApi()
const workspace = useWorkspace()
const feedback = useUiFeedback()

const loading = ref(false)
const data = ref<any | null>(null)
const companies = ref<any[]>([])
const stores = ref<any[]>([])
const selectedDate = ref(new Date().toISOString().slice(0, 10))
const lastRefreshedAt = ref<string | null>(null)
let autoRefreshTimer: ReturnType<typeof setInterval> | undefined

const scopeQuery = computed(() => {
  const params = new URLSearchParams()
  if (workspace.companyId.value) params.set('companyId', workspace.companyId.value)
  if (workspace.storeGroupId.value) params.set('storeGroupId', workspace.storeGroupId.value)
  if (workspace.storeId.value) params.set('storeId', workspace.storeId.value)
  if (selectedDate.value) params.set('date', selectedDate.value)
  const query = params.toString()
  return query ? `?${query}` : ''
})

const title = computed(() => "Today's")
const subtitle = computed(() => {
  const scope = [data.value?.scope?.companyName, data.value?.scope?.storeGroupName, data.value?.scope?.storeName].filter(Boolean).join(' · ')
  return scope || 'Sales, purchases, cash flow and attendance for the selected day.'
})

const recentColumns = [
  { key: 'title', label: 'Document' },
  { key: 'subtitle', label: 'Party / Details' },
  { key: 'amount', label: 'Amount' },
  { key: 'status', label: 'Status' }
]

const employeeColumns = [
  { key: 'employeeCode', label: 'Code' },
  { key: 'employeeName', label: 'Employee' },
  { key: 'department', label: 'Department' },
  { key: 'designation', label: 'Designation' },
  { key: 'status', label: 'Status' },
  { key: 'lastPunchType', label: 'Last Punch' }
]

const cashFlowItems = computed(() => {
  const cash = data.value?.cashFlow || {}
  return [
    { label: 'Sales collection', value: cash.salesCollections || 0, caption: 'Invoice payments received today.', icon: 'i-lucide-receipt-indian-rupee', color: 'success' },
    { label: 'Voucher receipts', value: cash.voucherReceipts || 0, caption: 'On-book receipt vouchers.', icon: 'i-lucide-arrow-down-left', color: 'success' },
    { label: 'Cash voucher receipts', value: cash.cashVoucherReceipts || 0, caption: 'Off-book cash receipt vouchers.', icon: 'i-lucide-wallet-cards', color: 'success' },
    { label: 'Purchase payments', value: cash.purchasePayments || 0, caption: 'Vendor / purchase payments today.', icon: 'i-lucide-package-check', color: 'warning' },
    { label: 'Voucher payments', value: cash.voucherPayments || 0, caption: 'On-book payment vouchers.', icon: 'i-lucide-arrow-up-right', color: 'warning' },
    { label: 'Cash voucher payments', value: cash.cashVoucherPayments || 0, caption: 'Off-book payment vouchers.', icon: 'i-lucide-banknote', color: 'warning' },
    { label: 'Expenses', value: (cash.voucherExpenses || 0) + (cash.cashVoucherExpenses || 0), caption: 'On-book and cash expense vouchers.', icon: 'i-lucide-wallet-minimal', color: 'error' },
    { label: 'Net cash flow', value: cash.netCashFlow || 0, caption: 'Receipts less payments and expenses.', icon: 'i-lucide-chart-no-axes-combined', color: (cash.netCashFlow || 0) >= 0 ? 'success' : 'error' }
  ]
})

const trendPoints = computed(() => data.value?.salesTrend || [])
const chartReady = computed(() => trendPoints.value.length > 0)
const chartMax = computed(() => Math.max(1, ...trendPoints.value.flatMap((row: any) => [Number(row.sales || 0), Number(row.purchase || 0), Number(row.profit || 0)].map(Math.abs))))
const salesPolyline = computed(() => polylineFor('sales'))
const purchasePolyline = computed(() => polylineFor('purchase'))
const profitPolyline = computed(() => polylineFor('profit'))

function polylineFor(key: string) {
  const rows = trendPoints.value
  if (!rows.length) return ''
  const width = 640
  const height = 180
  const padX = 18
  const padY = 18
  const usableWidth = width - (padX * 2)
  const usableHeight = height - (padY * 2)
  return rows.map((row: any, index: number) => {
    const x = padX + (rows.length === 1 ? usableWidth : (index / (rows.length - 1)) * usableWidth)
    const value = Number(row[key] || 0)
    const normalized = Math.max(-1, Math.min(1, value / chartMax.value))
    const y = padY + usableHeight - ((normalized + 1) / 2) * usableHeight
    return `${x.toFixed(1)},${y.toFixed(1)}`
  }).join(' ')
}

function money(value: unknown) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 0 }).format(Number(value || 0))
}

function time(value?: string | null) {
  if (!value) return '-'
  return new Date(value).toLocaleTimeString('en-IN', { hour: '2-digit', minute: '2-digit', hour12: true })
}

async function refresh() {
  loading.value = true
  try {
    const [dashboard, companyRows, storeRows] = await Promise.all([
      api.get<any>(`dashboard/todays${scopeQuery.value}`),
      api.list<any>('companies'),
      api.list<any>('stores')
    ])
    data.value = dashboard
    companies.value = companyRows
    stores.value = storeRows
    lastRefreshedAt.value = new Date().toISOString()
  } catch (error) {
    feedback.failed("Today's dashboard refresh failed", error)
  } finally {
    loading.value = false
  }
}

function configureAutoRefresh() {
  if (autoRefreshTimer) clearInterval(autoRefreshTimer)
  autoRefreshTimer = setInterval(() => {
    if (!loading.value) refresh()
  }, 60_000)
}

onMounted(() => {
  refresh()
  configureAutoRefresh()
})

onBeforeUnmount(() => {
  if (autoRefreshTimer) clearInterval(autoRefreshTimer)
})
</script>

<template>
  <AppShell :title="title" :companies="companies" :stores="stores" @refresh="refresh" @workspace-change="refresh">
    <section class="dashboard-v3-page todays-dashboard-page">
      <DashboardPageHero
        badge="Daily control"
        badge-icon="i-lucide-sun"
        :title="title"
        :subtitle="subtitle"
        :loading="loading"
        business
        @refresh="refresh"
      />

      <div class="todays-toolbar">
        <div>
          <p class="todays-toolbar-label">Business date</p>
          <UInput v-model="selectedDate" type="date" @change="refresh" />
        </div>
        <div class="todays-toolbar-status">
          <UBadge color="neutral" variant="subtle" icon="i-lucide-clock-3">
            {{ lastRefreshedAt ? `Refreshed ${time(lastRefreshedAt)}` : 'Not refreshed yet' }}
          </UBadge>
          <UButton icon="i-lucide-refresh-cw" label="Refresh" :loading="loading" @click="refresh" />
        </div>
      </div>

      <DashboardMetricGrid :metrics="data?.metrics || []" :loading="loading" business />

      <div class="todays-grid two">
        <UCard class="dashboard-v3-card dashboard-v3-wide todays-line-card">
          <template #header>
            <div class="dashboard-v3-card-header">
              <div>
                <h2>Sales trend</h2>
                <p>Last 14 days trend for sales, purchases and gross margin.</p>
              </div>
              <UBadge color="primary" variant="subtle">Line graph</UBadge>
            </div>
          </template>
          <div v-if="chartReady" class="todays-line-wrap">
            <svg viewBox="0 0 640 180" role="img" aria-label="Sales trend line graph">
              <line x1="18" y1="90" x2="622" y2="90" class="axis" />
              <polyline :points="salesPolyline" class="sales-line" fill="none" />
              <polyline :points="purchasePolyline" class="purchase-line" fill="none" />
              <polyline :points="profitPolyline" class="profit-line" fill="none" />
            </svg>
            <div class="todays-trend-labels">
              <span v-for="point in trendPoints" :key="point.label">{{ point.label }}</span>
            </div>
            <div class="todays-legend">
              <span><i class="sales" /> Sales</span>
              <span><i class="purchase" /> Purchase</span>
              <span><i class="profit" /> Gross margin</span>
            </div>
          </div>
          <UiCrudEmptyState v-else title="No trend data" description="Sales and purchase trends will appear after activity is available." icon="i-lucide-chart-no-axes-combined" />
        </UCard>

        <UCard class="dashboard-v3-card dashboard-v3-wide">
          <template #header>
            <div class="dashboard-v3-card-header">
              <div>
                <h2>Cash flow today</h2>
                <p>Receipts, payments, expenses and cash vouchers.</p>
              </div>
              <UBadge :color="(data?.cashFlow?.netCashFlow || 0) >= 0 ? 'success' : 'error'" variant="subtle">
                {{ money(data?.cashFlow?.netCashFlow || 0) }} net
              </UBadge>
            </div>
          </template>
          <div class="todays-cash-grid">
            <div v-for="item in cashFlowItems" :key="item.label" class="todays-cash-card">
              <UBadge :icon="item.icon" :color="item.color" variant="subtle" />
              <div>
                <p>{{ item.label }}</p>
                <strong>{{ money(item.value) }}</strong>
                <span>{{ item.caption }}</span>
              </div>
            </div>
          </div>
        </UCard>
      </div>

      <div class="todays-grid two">
        <UCard class="dashboard-v3-card dashboard-v3-wide">
          <template #header>
            <div class="dashboard-v3-card-header">
              <div>
                <h2>Employee present</h2>
                <p>Active employees with attendance punch today.</p>
              </div>
              <UBadge color="success" variant="subtle">{{ data?.attendance?.presentEmployees || 0 }} present</UBadge>
            </div>
          </template>
          <div v-if="data?.attendance?.present?.length" class="todays-employee-list">
            <div v-for="employee in data.attendance.present" :key="employee.employeeId" class="todays-employee-card">
              <div>
                <strong>{{ employee.employeeName }}</strong>
                <span>{{ employee.employeeCode }} · {{ employee.department || 'No department' }} · {{ employee.designation || 'No designation' }}</span>
              </div>
              <div class="todays-employee-times">
                <UBadge :color="employee.status === 'Needs Review' ? 'warning' : 'success'" variant="subtle">{{ employee.status }}</UBadge>
                <small>{{ employee.lastPunchType || 'Punch' }} · {{ time(employee.lastPunchAt) }}</small>
              </div>
            </div>
          </div>
          <UiCrudEmptyState v-else title="No employee present yet" description="Present employees will appear after check-in or manual punch." icon="i-lucide-user-check" />
        </UCard>

        <UCard class="dashboard-v3-card dashboard-v3-wide">
          <template #header>
            <div class="dashboard-v3-card-header">
              <div>
                <h2>Employee absent</h2>
                <p>Only active employees without a punch today.</p>
              </div>
              <UBadge :color="(data?.attendance?.absentEmployees || 0) > 0 ? 'warning' : 'success'" variant="subtle">{{ data?.attendance?.absentEmployees || 0 }} absent</UBadge>
            </div>
          </template>
          <div v-if="data?.attendance?.absent?.length" class="todays-employee-list">
            <div v-for="employee in data.attendance.absent" :key="employee.employeeId" class="todays-employee-card absent">
              <div>
                <strong>{{ employee.employeeName }}</strong>
                <span>{{ employee.employeeCode }} · {{ employee.department || 'No department' }} · {{ employee.designation || 'No designation' }}</span>
              </div>
              <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-camera" to="/attendance/kiosk">Punch</UButton>
            </div>
          </div>
          <UiCrudEmptyState v-else title="No active employee absent" description="All active employees have attendance for the selected date." icon="i-lucide-circle-check" />
        </UCard>
      </div>

      <div class="todays-grid two">
        <DashboardDataTable title="Today activity" description="Latest sales, purchases, vouchers and cash vouchers." :rows="data?.recentActivities || []" :columns="recentColumns" />
        <DashboardDataTable title="Absent employee table" description="Export-friendly absent employee list." :rows="data?.attendance?.absent || []" :columns="employeeColumns" />
      </div>
    </section>
  </AppShell>
</template>

<style scoped>
.todays-dashboard-page { gap: 1rem; }
.todays-toolbar { display: flex; align-items: end; justify-content: space-between; gap: 1rem; padding: 1rem; border: 1px solid rgb(var(--ui-border)); border-radius: 1rem; background: rgb(var(--ui-bg)); }
.todays-toolbar-label { font-size: .75rem; color: rgb(var(--ui-text-muted)); margin-bottom: .35rem; }
.todays-toolbar-status { display: flex; align-items: center; gap: .75rem; flex-wrap: wrap; }
.todays-grid.two { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 1rem; }
.todays-line-wrap svg { width: 100%; min-height: 220px; }
.todays-line-wrap .axis { stroke: rgb(var(--ui-border)); stroke-width: 1; }
.todays-line-wrap polyline { stroke-width: 4; stroke-linecap: round; stroke-linejoin: round; }
.sales-line { stroke: rgb(var(--ui-primary)); }
.purchase-line { stroke: rgb(var(--ui-warning)); }
.profit-line { stroke: rgb(var(--ui-success)); }
.todays-trend-labels { display: grid; grid-template-columns: repeat(7, minmax(0, 1fr)); gap: .35rem; font-size: .72rem; color: rgb(var(--ui-text-muted)); }
.todays-trend-labels span:nth-child(odd) { display: none; }
.todays-legend { display: flex; gap: 1rem; flex-wrap: wrap; margin-top: .75rem; font-size: .8rem; color: rgb(var(--ui-text-muted)); }
.todays-legend span { display: inline-flex; align-items: center; gap: .35rem; }
.todays-legend i { width: .7rem; height: .7rem; border-radius: 999px; display: inline-block; }
.todays-legend .sales { background: rgb(var(--ui-primary)); }
.todays-legend .purchase { background: rgb(var(--ui-warning)); }
.todays-legend .profit { background: rgb(var(--ui-success)); }
.todays-cash-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: .75rem; }
.todays-cash-card { display: flex; gap: .75rem; align-items: flex-start; padding: .85rem; border: 1px solid rgb(var(--ui-border)); border-radius: .9rem; background: rgb(var(--ui-bg-muted)); }
.todays-cash-card p { font-size: .75rem; color: rgb(var(--ui-text-muted)); }
.todays-cash-card strong { display: block; margin-top: .15rem; font-size: 1.05rem; }
.todays-cash-card span { display: block; margin-top: .2rem; font-size: .72rem; color: rgb(var(--ui-text-muted)); }
.todays-employee-list { display: grid; gap: .65rem; }
.todays-employee-card { display: flex; align-items: center; justify-content: space-between; gap: .75rem; padding: .85rem; border: 1px solid rgb(var(--ui-border)); border-radius: .9rem; background: rgb(var(--ui-bg-muted)); }
.todays-employee-card.absent { background: rgb(var(--ui-bg)); }
.todays-employee-card strong { display: block; }
.todays-employee-card span { display: block; margin-top: .2rem; font-size: .78rem; color: rgb(var(--ui-text-muted)); }
.todays-employee-times { text-align: right; display: grid; gap: .25rem; }
.todays-employee-times small { color: rgb(var(--ui-text-muted)); }
@media (max-width: 1100px) { .todays-grid.two, .todays-cash-grid { grid-template-columns: 1fr; } }
@media (max-width: 720px) { .todays-toolbar, .todays-employee-card { align-items: stretch; flex-direction: column; } .todays-toolbar-status { align-items: stretch; } .todays-employee-times { text-align: left; } }
</style>
