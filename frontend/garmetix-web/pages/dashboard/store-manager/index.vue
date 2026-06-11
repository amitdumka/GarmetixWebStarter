<script setup lang="ts">
const api = useGarmetixApi()
const workspace = useWorkspace()
const feedback = useUiFeedback()

const loading = ref(false)
const data = ref<any | null>(null)
const companies = ref<any[]>([])
const stores = ref<any[]>([])

const scopeQuery = computed(() => {
  const params = new URLSearchParams()
  if (workspace.companyId.value) params.set('companyId', workspace.companyId.value)
  if (workspace.storeGroupId.value) params.set('storeGroupId', workspace.storeGroupId.value)
  if (workspace.storeId.value) params.set('storeId', workspace.storeId.value)
  const query = params.toString()
  return query ? `?${query}` : ''
})

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
  } catch (error) {
    feedback.failed('Store manager dashboard load failed', error)
  } finally {
    loading.value = false
  }
}

onMounted(refresh)
</script>

<template>
  <AppShell title="Store Manager Dashboard" :companies="companies" :stores="stores" @refresh="refresh" @workspace-change="refresh">
    <section class="dashboard-v3-page">
      <DashboardPageHero
        badge="Current Store"
        badge-icon="i-lucide-store"
        title="Store Manager Dashboard"
        :subtitle="`${data?.scope?.storeName || 'Store scoped operational dashboard'} · ${data?.scope?.companyName || 'Company'}`"
        :loading="loading"
        @refresh="refresh"
      />

      <DashboardMetricGrid :metrics="data?.metrics || []" :loading="loading" />

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
