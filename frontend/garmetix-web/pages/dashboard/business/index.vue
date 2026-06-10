<script setup lang="ts">
const api = useGarmetixApi()
const workspace = useWorkspace()
const feedback = useUiFeedback()
const auth = useAuth()

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
      api.get<any>(`dashboard/business${scopeQuery.value}`),
      api.list<any>('companies'),
      api.list<any>('stores')
    ])
    data.value = dashboard
    companies.value = companyRows
    stores.value = storeRows
  } catch (error) {
    feedback.failed('Business dashboard load failed', error)
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
  if (value.includes('ready') || value.includes('paid') || value.includes('dashboard')) return 'success'
  if (value.includes('report') || value.includes('control')) return 'warning'
  if (value.includes('cancel') || value.includes('fail')) return 'error'
  return 'neutral'
}

function trendHeight(value: number, key: 'sales' | 'purchase') {
  const rows = data.value?.trend || []
  const max = Math.max(1, ...rows.map((row: any) => Number(row[key] || 0)))
  return `${Math.max(6, Math.round((Number(value || 0) / max) * 100))}%`
}

const userRole = computed(() => auth.user.value?.role || auth.user.value?.userType || 'User')

onMounted(refresh)
</script>

<template>
  <AppShell title="Owner / Admin Dashboard" :companies="companies" :stores="stores" @refresh="refresh" @workspace-change="refresh">
    <section class="dashboard-v3-page">
      <div class="dashboard-v3-hero business">
        <div>
          <UBadge color="primary" variant="subtle" icon="i-lucide-building-2">{{ userRole }}</UBadge>
          <h1>Company & Store Group Dashboard</h1>
          <p>{{ data?.scope?.companyName || 'Company scope' }} · {{ data?.scope?.storeGroupName || 'Store group scope' }}</p>
        </div>
        <div class="dashboard-v3-hero-actions">
          <UBadge :color="loading ? 'warning' : 'success'" variant="subtle">{{ loading ? 'Loading' : 'Live' }}</UBadge>
          <UButton icon="i-lucide-refresh-cw" label="Refresh" :loading="loading" @click="refresh" />
        </div>
      </div>

      <div class="dashboard-v3-metric-grid business">
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

      <div class="dashboard-v3-grid business">
        <UCard class="dashboard-v3-card dashboard-v3-wide">
          <template #header>
            <div class="dashboard-v3-card-header">
              <div>
                <h2>7-day business trend</h2>
                <p>Sales and purchase across permitted stores.</p>
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

        <UCard class="dashboard-v3-card dashboard-v3-wide">
          <template #header>
            <div class="dashboard-v3-card-header">
              <div>
                <h2>Store performance</h2>
                <p>Current month sales, purchase and stock valuation.</p>
              </div>
            </div>
          </template>
          <div class="planner-table-wrap">
            <table class="planner-table">
              <thead>
                <tr>
                  <th>Store</th>
                  <th>Sales</th>
                  <th>Purchase</th>
                  <th>Stock Value</th>
                  <th>Invoices</th>
                  <th>Stock Qty</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="store in data?.stores || []" :key="store.storeId">
                  <td>{{ store.storeName }}</td>
                  <td>{{ money(store.salesMonth) }}</td>
                  <td>{{ money(store.purchaseMonth) }}</td>
                  <td>{{ money(store.stockValue) }}</td>
                  <td>{{ store.invoiceCount }}</td>
                  <td>{{ store.currentStockQty }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </UCard>

        <UCard class="dashboard-v3-card">
          <template #header>
            <div class="dashboard-v3-card-header">
              <div>
                <h2>Admin queue</h2>
                <p>Control checkpoints and reports.</p>
              </div>
            </div>
          </template>
          <div class="dashboard-v3-list">
            <NuxtLink v-for="item in data?.adminQueue || []" :key="item.title" :to="`/${item.resource}`" class="dashboard-v3-list-item">
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
                <p>Latest billing activity.</p>
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
                <h2>Recent purchases</h2>
                <p>Latest inward activity.</p>
              </div>
            </div>
          </template>
          <div class="dashboard-v3-list">
            <div v-for="item in data?.recentPurchases || []" :key="item.resourceId || item.title" class="dashboard-v3-list-item">
              <UIcon name="i-lucide-package-plus" class="h-4 w-4" />
              <span>
                <strong>{{ item.title }}</strong>
                <small>{{ item.subtitle }} · {{ dateTime(item.onDate) }}</small>
              </span>
              <strong>{{ item.amount }}</strong>
            </div>
          </div>
        </UCard>
      </div>
    </section>
  </AppShell>
</template>
