<script setup lang="ts">
const api = useGarmetixApi()
const workspace = useWorkspace()
const feedback = useUiFeedback()
const auth = useAuth()

const loading = ref(false)
const data = ref<any | null>(null)
const preferences = useDashboardPreferences('business-dashboard')
const lastRefreshedAt = ref<string | null>(null)
let autoRefreshTimer: ReturnType<typeof setInterval> | undefined
const companies = ref<any[]>([])
const stores = ref<any[]>([])

const scopeQuery = computed(() => {
  const params = new URLSearchParams()
  if (workspace.companyId.value) params.set('companyId', workspace.companyId.value)
  if (workspace.storeGroupId.value) params.set('storeGroupId', workspace.storeGroupId.value)
  if (workspace.storeId.value) params.set('storeId', workspace.storeId.value)
  preferences.toQueryParams(params)
  const query = params.toString()
  return query ? `?${query}` : ''
})

const userRole = computed(() => auth.user.value?.role || auth.user.value?.userType || 'User')

const storeColumns = [
  { key: 'storeName', label: 'Store' },
  { key: 'salesMonth', label: 'Sales', type: 'money' },
  { key: 'purchaseMonth', label: 'Purchase', type: 'money' },
  { key: 'stockValue', label: 'Stock Value', type: 'money' },
  { key: 'invoiceCount', label: 'Invoices', type: 'number' },
  { key: 'currentStockQty', label: 'Stock Qty', type: 'number' }
]

const storeGroupColumns = [
  { key: 'storeGroupName', label: 'Store Group' },
  { key: 'storeCount', label: 'Stores', type: 'number' },
  { key: 'salesMonth', label: 'Sales', type: 'money' },
  { key: 'purchaseMonth', label: 'Purchase', type: 'money' },
  { key: 'stockValue', label: 'Stock Value', type: 'money' },
  { key: 'invoiceCount', label: 'Invoices', type: 'number' },
  { key: 'currentStockQty', label: 'Stock Qty', type: 'number' }
]


const exportTables = computed(() => [
  { name: 'Store Performance', rows: data.value?.stores || [], columns: storeColumns },
  { name: 'Store Group Performance', rows: data.value?.storeGroups || [], columns: storeGroupColumns },
  { name: 'Recent Sales', rows: data.value?.recentSales || [], columns: [
    { key: 'title', label: 'Invoice' },
    { key: 'description', label: 'Customer / Details' },
    { key: 'amount', label: 'Amount' },
    { key: 'date', label: 'Date' }
  ]},
  { name: 'Recent Purchases', rows: data.value?.recentPurchases || [], columns: [
    { key: 'title', label: 'Invoice' },
    { key: 'description', label: 'Vendor / Details' },
    { key: 'amount', label: 'Amount' },
    { key: 'date', label: 'Date' }
  ]}
])

async function refresh() {
  loading.value = true
  try {
    const [dashboard, companyRows, storeRows] = await Promise.all([
      api.get<any>(`dashboard/business${scopeQuery.value}`),
      api.list<any>('companies'),
      api.list<any>('stores')
    ])
    data.value = dashboard
    companies.value = companyRows
    stores.value = storeRows
    lastRefreshedAt.value = new Date().toISOString()
  } catch (error) {
    feedback.failed('Business dashboard load failed', error)
  } finally {
    loading.value = false
  }
}

function configureAutoRefresh() {
  if (autoRefreshTimer) {
    clearInterval(autoRefreshTimer)
    autoRefreshTimer = undefined
  }
  if (!preferences.autoRefresh.value) return
  autoRefreshTimer = setInterval(() => {
    if (!loading.value) refresh()
  }, Math.max(30, Number(preferences.refreshIntervalSeconds.value || 60)) * 1000)
}

function applyPreset(value: string) {
  preferences.applyPreset(value as any)
  refresh()
}

function setRangeKey(value: string) {
  preferences.rangeKey.value = value as any
  preferences.save()
}

function setFromDate(value: string) {
  preferences.fromDate.value = value
  preferences.rangeKey.value = 'custom'
  preferences.save()
}

function setToDate(value: string) {
  preferences.toDate.value = value
  preferences.rangeKey.value = 'custom'
  preferences.save()
}


watch([preferences.autoRefresh, preferences.refreshIntervalSeconds], () => {
  preferences.save()
  configureAutoRefresh()
})

onMounted(() => {
  preferences.load()
  refresh()
  configureAutoRefresh()
})

onBeforeUnmount(() => {
  if (autoRefreshTimer) clearInterval(autoRefreshTimer)
})
</script>

<template>
  <AppShell title="Owner / Admin Dashboard" :companies="companies" :stores="stores" @refresh="refresh" @workspace-change="refresh">
    <section class="dashboard-v3-page">
      <DashboardPageHero
        :badge="userRole"
        badge-icon="i-lucide-building-2"
        title="Company & Store Group Dashboard"
        :subtitle="`${data?.scope?.companyName || 'Company scope'} · ${data?.scope?.storeGroupName || 'Store group scope'} · ${data?.period?.label || preferences.rangeLabel.value}`"
        :loading="loading"
        business
        @refresh="refresh"
      />

      <DashboardFilterBar
        :range-key="preferences.rangeKey.value"
        @update:range-key="setRangeKey"
        :from-date="preferences.fromDate.value"
        @update:from-date="setFromDate"
        :to-date="preferences.toDate.value"
        @update:to-date="setToDate"
        :auto-refresh="preferences.autoRefresh.value"
        @update:auto-refresh="preferences.autoRefresh.value = $event"
        :refresh-interval-seconds="preferences.refreshIntervalSeconds.value"
        @update:refresh-interval-seconds="preferences.refreshIntervalSeconds.value = $event"
        title="Dashboard period and refresh"
        :preset-options="preferences.presetOptions"
        :refresh-interval-options="preferences.refreshIntervalOptions"
        :last-refreshed-at="lastRefreshedAt"
        :loading="loading"
        @apply-preset="applyPreset"
        @refresh="refresh"
      />

      <DashboardMetricGrid :metrics="data?.metrics || []" :loading="loading" business />

      <DashboardExportActions
        title="Business Dashboard"
        file-name="business-dashboard"
        :data="data"
        :tables="exportTables"
        :loading="loading"
      />

      <div class="dashboard-v3-insight-grid">
        <DashboardActionGrid
          title="Executive actions"
          description="Role-aware shortcuts for owner, admin and accountant users."
          :actions="data?.quickActions || []"
        />

        <DashboardHealthGrid
          title="Business health"
          description="Company and store-group level signals."
          :signals="data?.healthSignals || []"
        />
      </div>

      <div class="dashboard-v3-grid business">
        <DashboardTrendChart
          title="7-day business trend"
          description="Sales and purchase across permitted stores."
          :points="data?.trend || []"
        />

        <DashboardDataTable
          title="Store performance"
          description="Current month sales, purchase and stock valuation."
          :rows="data?.stores || []"
          :columns="storeColumns"
        />

        <DashboardDataTable
          title="Store group performance"
          description="Group-wise sales, purchase and stock visibility."
          :rows="data?.storeGroups || []"
          :columns="storeGroupColumns"
        />

        <DashboardItemList
          title="Admin queue"
          description="Control checkpoints and reports."
          :items="data?.adminQueue || []"
          icon="i-lucide-arrow-up-right"
          item-to="dashboard"
          empty-title="No admin queue"
          empty-description="No pending dashboard control checkpoints for this scope."
        />

        <DashboardItemList
          title="Recent sales"
          description="Latest billing activity."
          :items="data?.recentSales || []"
          icon="i-lucide-receipt-indian-rupee"
          :show-date="true"
          empty-title="No recent sales"
          empty-description="Recent sales will appear after billing activity is recorded."
        />

        <DashboardItemList
          title="Recent purchases"
          description="Latest inward activity."
          :items="data?.recentPurchases || []"
          icon="i-lucide-package-plus"
          :show-date="true"
          empty-title="No recent purchases"
          empty-description="Recent purchases will appear after inward activity is recorded."
        />
      </div>
    </section>
  </AppShell>
</template>
