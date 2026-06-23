<script setup lang="ts">
const api = useGarmetixApi()
const workspace = useWorkspace()
const feedback = useUiFeedback()

const loading = ref(false)
const data = ref<any | null>(null)
const preferences = useDashboardPreferences('store-manager-dashboard')
const lastRefreshedAt = ref<string | null>(null)
let autoRefreshTimer: ReturnType<typeof setInterval> | undefined
const companies = ref<any[]>([])
const stores = ref<any[]>([])

const storeDashboardTitle = computed(() => data.value?.scope?.storeName || 'Store Dashboard')
const storeDashboardSubtitle = computed(() => {
  const parts = [data.value?.period?.label || preferences.rangeLabel.value, data.value?.scope?.companyName].filter(Boolean)
  return parts.length ? parts.join(' · ') : 'Current store sales, stock and daily work queue.'
})

const scopeQuery = computed(() => {
  const params = new URLSearchParams()
  if (workspace.companyId.value) params.set('companyId', workspace.companyId.value)
  if (workspace.storeGroupId.value) params.set('storeGroupId', workspace.storeGroupId.value)
  if (workspace.storeId.value) params.set('storeId', workspace.storeId.value)
  preferences.toQueryParams(params)
  const query = params.toString()
  return query ? `?${query}` : ''
})


const exportTables = computed(() => [
  { name: 'Recent Sales', rows: data.value?.recentSales || [], columns: [
    { key: 'title', label: 'Invoice' },
    { key: 'description', label: 'Customer / Details' },
    { key: 'amount', label: 'Amount' },
    { key: 'date', label: 'Date' }
  ]},
  { name: 'Low Stock Alerts', rows: data.value?.stockAlerts || [], columns: [
    { key: 'title', label: 'Item' },
    { key: 'description', label: 'Details' },
    { key: 'amount', label: 'Quantity' }
  ]},
  { name: 'Work Queue', rows: data.value?.workQueue || [], columns: [
    { key: 'title', label: 'Task' },
    { key: 'description', label: 'Description' },
    { key: 'amount', label: 'Value' }
  ]}
])

async function refresh() {
  loading.value = true
  try {
    const [dashboard, companyRows, storeRows] = await Promise.all([
      api.get<any>(`dashboard/store-manager${scopeQuery.value}`),
      api.list<any>('companies'),
      api.list<any>('stores')
    ])
    data.value = dashboard
    companies.value = companyRows
    stores.value = storeRows
    lastRefreshedAt.value = new Date().toISOString()
  } catch (error) {
    feedback.failed('Store manager dashboard load failed', error)
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
  <AppShell :title="storeDashboardTitle" :companies="companies" :stores="stores" @refresh="refresh" @workspace-change="refresh">
    <section class="dashboard-v3-page">
      <DashboardPageHero
        badge="Store"
        badge-icon="i-lucide-store"
        :title="storeDashboardTitle"
        :subtitle="storeDashboardSubtitle"
        :loading="loading"
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

      <DashboardMetricGrid :metrics="data?.metrics || []" :loading="loading" />

      <DashboardExportActions
        :title="storeDashboardTitle"
        file-name="store-dashboard"
        :data="data"
        :tables="exportTables"
        :loading="loading"
      />

      <div class="dashboard-v3-insight-grid dashboard-v3-breakdown-grid">
        <DashboardBreakdownGrid
          title="Sales split"
          description="Regular GST sales and separate Non-GST sales for the selected period."
          :items="data?.revenueBreakdown || []"
        />
        <DashboardBreakdownGrid
          title="Stock split"
          description="Regular inventory and separate Non-GST stock valuation."
          :items="data?.stockBreakdown || []"
        />
        <DashboardBreakdownGrid
          title="Profit split"
          description="Period margin separated by register for operational review."
          :items="data?.profitBreakdown || []"
        />
      </div>

      <div class="dashboard-v3-insight-grid">
        <DashboardActionGrid
          title="Quick actions"
          description="Common store manager tasks."
          :actions="data?.quickActions || []"
        />

        <DashboardHealthGrid
          title="Store health"
          description="Signals for daily close."
          :signals="data?.healthSignals || []"
        />
      </div>

      <div class="dashboard-v3-grid">
        <DashboardTrendChart
          title="7-day sales / purchase trend"
          description="Operational movement for the selected store."
          :points="data?.trend || []"
        />

        <DashboardItemList
          title="Work queue"
          description="Daily operating shortcuts."
          :items="data?.workQueue || []"
          icon="i-lucide-arrow-up-right"
          item-to="dashboard"
          empty-title="No pending work"
          empty-description="Your daily work queue is clear for the selected store."
        />

        <DashboardItemList
          title="Recent sales"
          description="Latest bills for the selected store."
          :items="data?.recentSales || []"
          icon="i-lucide-receipt-indian-rupee"
          :show-date="true"
          empty-title="No recent sales"
          empty-description="Recent sales will appear after billing activity is recorded."
        />

        <DashboardItemList
          title="Low stock alerts"
          description="Items at or below reorder threshold."
          :items="data?.stockAlerts || []"
          icon="i-lucide-triangle-alert"
          amount-prefix="Qty "
          empty-title="No low stock"
          empty-description="All selected-store items are above the alert threshold."
        >
          <template #action>
            <UButton to="/inventory" size="xs" variant="ghost" icon="i-lucide-boxes" label="Inventory" />
          </template>
        </DashboardItemList>
      </div>
    </section>
  </AppShell>
</template>
