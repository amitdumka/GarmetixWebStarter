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

function money(value: number) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 0 }).format(Number(value || 0))
}

function dateTime(value: string) {
  return value ? new Date(value).toLocaleString('en-IN') : '-'
}

function statusColor(status: string) {
  const value = String(status || '').toLowerCase()
  if (value.includes('ready') || value.includes('paid') || value.includes('done')) return 'success'
  if (value.includes('required') || value.includes('low')) return 'warning'
  if (value.includes('cancel') || value.includes('fail')) return 'error'
  return 'neutral'
}

function trendHeight(value: number, key: 'sales' | 'purchase') {
  const rows = data.value?.trend || []
  const max = Math.max(1, ...rows.map((row: any) => Number(row[key] || 0)))
  return `${Math.max(6, Math.round((Number(value || 0) / max) * 100))}%`
}

onMounted(refresh)
</script>

<template>
  <AppShell title="Store Manager Dashboard" :companies="companies" :stores="stores" @refresh="refresh" @workspace-change="refresh">
    <section class="dashboard-v3-page">
      <div class="dashboard-v3-hero">
        <div>
          <UBadge color="primary" variant="subtle" icon="i-lucide-store">Current Store</UBadge>
          <h1>Store Manager Dashboard</h1>
          <p>{{ data?.scope?.storeName || 'Store scoped operational dashboard' }} · {{ data?.scope?.companyName || 'Company' }}</p>
        </div>
        <div class="dashboard-v3-hero-actions">
          <UBadge :color="loading ? 'warning' : 'success'" variant="subtle">{{ loading ? 'Loading' : 'Live' }}</UBadge>
          <UButton icon="i-lucide-refresh-cw" label="Refresh" :loading="loading" @click="refresh" />
        </div>
      </div>

      <div class="dashboard-v3-metric-grid">
        <UCard v-for="metric in data?.metrics || []" :key="metric.label" class="dashboard-v3-metric-card">
          <div class="dashboard-v3-metric-body">
            <div>
              <p>{{ metric.label }}</p>
              <strong>{{ metric.displayValue }}</strong>
              <span>{{ metric.caption }}</span>
            </div>
            <UBadge :color="metric.color" variant="subtle" :icon="metric.icon" />
          </div>
        </UCard>
      </div>

      <div class="dashboard-v3-grid">
        <UCard class="dashboard-v3-card dashboard-v3-wide">
          <template #header>
            <div class="dashboard-v3-card-header">
              <div>
                <h2>7-day sales / purchase trend</h2>
                <p>Operational movement for the selected store.</p>
              </div>
              <UBadge color="neutral" variant="subtle">Daily</UBadge>
            </div>
          </template>
          <div class="dashboard-v3-chart">
            <div v-for="point in data?.trend || []" :key="point.label" class="dashboard-v3-chart-day">
              <div class="dashboard-v3-chart-bars">
                <span class="sales" :style="{ height: trendHeight(point.sales, 'sales') }" />
                <span class="purchase" :style="{ height: trendHeight(point.purchase, 'purchase') }" />
              </div>
              <small>{{ point.label }}</small>
            </div>
          </div>
          <div class="dashboard-v3-legend">
            <span><i class="sales" /> Sales</span>
            <span><i class="purchase" /> Purchase</span>
          </div>
        </UCard>

        <UCard class="dashboard-v3-card">
          <template #header>
            <div class="dashboard-v3-card-header">
              <div>
                <h2>Work queue</h2>
                <p>Daily operating shortcuts.</p>
              </div>
            </div>
          </template>
          <div class="dashboard-v3-list">
            <NuxtLink v-for="item in data?.workQueue || []" :key="item.title" :to="`/${item.resource}`" class="dashboard-v3-list-item">
              <UIcon name="i-lucide-arrow-up-right" class="h-4 w-4" />
              <span>
                <strong>{{ item.title }}</strong>
                <small>{{ item.subtitle }}</small>
              </span>
              <UBadge :color="statusColor(item.status)" variant="subtle">{{ item.status }}</UBadge>
            </NuxtLink>
          </div>
        </UCard>

        <UCard class="dashboard-v3-card">
          <template #header>
            <div class="dashboard-v3-card-header">
              <div>
                <h2>Recent sales</h2>
                <p>Latest bills for the selected store.</p>
              </div>
            </div>
          </template>
          <div class="dashboard-v3-list">
            <div v-for="item in data?.recentSales || []" :key="item.resourceId || item.title" class="dashboard-v3-list-item">
              <UIcon name="i-lucide-receipt-indian-rupee" class="h-4 w-4" />
              <span>
                <strong>{{ item.title }}</strong>
                <small>{{ item.subtitle }} · {{ dateTime(item.onDate) }}</small>
              </span>
              <strong>{{ item.amount }}</strong>
            </div>
          </div>
        </UCard>

        <UCard class="dashboard-v3-card">
          <template #header>
            <div class="dashboard-v3-card-header">
              <div>
                <h2>Low stock alerts</h2>
                <p>Items at or below reorder threshold.</p>
              </div>
              <UButton to="/inventory" size="xs" variant="ghost" icon="i-lucide-boxes" label="Inventory" />
            </div>
          </template>
          <div class="dashboard-v3-list">
            <div v-for="item in data?.stockAlerts || []" :key="item.resourceId || item.title" class="dashboard-v3-list-item">
              <UIcon name="i-lucide-triangle-alert" class="h-4 w-4" />
              <span>
                <strong>{{ item.title }}</strong>
                <small>{{ item.subtitle }}</small>
              </span>
              <UBadge color="warning" variant="subtle">Qty {{ item.amount }}</UBadge>
            </div>
            <UiCrudEmptyState v-if="!data?.stockAlerts?.length" title="No low stock" description="All selected-store items are above the alert threshold." icon="i-lucide-check-circle" />
          </div>
        </UCard>
      </div>
    </section>
  </AppShell>
</template>
