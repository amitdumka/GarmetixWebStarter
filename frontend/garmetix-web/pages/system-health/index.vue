<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const feedback = useUiFeedback()
const config = useRuntimeConfig()
const isAuthenticated = auth.isAuthenticated
const canSeeAdmin = auth.canSeeAdmin

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const health = ref<any | null>(null)
const bootstrap = ref<any | null>(null)
const loading = ref(false)
const publicHost = ref('')
const lastChecked = ref('')

const metrics = computed(() => [
  {
    label: 'API Service',
    value: health.value?.status || 'Unknown',
    meta: lastChecked.value ? `Checked ${lastChecked.value}` : 'Waiting for check',
    icon: health.value?.databaseReady ? 'i-lucide-wifi' : 'i-lucide-wifi-off',
    color: health.value?.databaseReady ? 'success' : 'error'
  },
  {
    label: 'Database',
    value: health.value?.databaseReady ? 'Ready' : 'Unavailable',
    meta: health.value?.environment || 'Environment unknown',
    icon: 'i-lucide-database',
    color: health.value?.databaseReady ? 'success' : 'error'
  },
  {
    label: 'API Route',
    value: String(config.public.apiBase || '/api'),
    meta: publicHost.value || 'Public host loading',
    icon: 'i-lucide-route',
    color: String(config.public.apiBase || '').startsWith('/api') ? 'primary' : 'warning'
  },
  {
    label: 'Admin Setup',
    value: bootstrap.value?.hasAdmin ? 'Configured' : 'Required',
    meta: bootstrap.value?.message || 'Bootstrap status',
    icon: 'i-lucide-shield-check',
    color: bootstrap.value?.hasAdmin ? 'success' : 'warning'
  }
])

const detailRows = computed(() => [
  ['Public host', publicHost.value || '-'],
  ['Public API base', String(config.public.apiBase || '/api')],
  ['Backend proxy route', '/api/health'],
  ['API application', health.value?.application || '-'],
  ['API environment', health.value?.environment || '-'],
  ['Database ready', health.value?.databaseReady ? 'Yes' : 'No'],
  ['Checked at UTC', health.value?.checkedAtUtc || '-'],
  ['Admin exists', bootstrap.value?.hasAdmin ? 'Yes' : 'No'],
  ['Users exist', bootstrap.value?.hasUsers ? 'Yes' : 'No']
])

async function refresh() {
  if (!auth.isAuthenticated.value || !auth.canSeeAdmin.value) {
    return
  }

  loading.value = true
  try {
    const [healthResponse, bootstrapResponse, companyRows, storeRows] = await Promise.all([
      $fetch<any>('/api/health'),
      $fetch<any>('/api/auth/bootstrap-status'),
      api.list<any>('companies'),
      api.list<any>('stores')
    ])

    health.value = healthResponse
    bootstrap.value = bootstrapResponse
    companies.value = companyRows
    stores.value = storeRows
    lastChecked.value = new Date().toLocaleTimeString('en-IN')
  } catch (error) {
    feedback.failed('System health refresh failed', error)
  } finally {
    loading.value = false
  }
}

onMounted(async () => {
  auth.restore()
  publicHost.value = window.location.origin
  await refresh()
})
</script>

<template>
  <AuthScreen v-if="!isAuthenticated" @authenticated="refresh" />

  <AppShell
    v-else
    title="System Health"
    :companies="companies"
    :stores="stores"
    @refresh="refresh"
  >
    <section class="planner-dashboard">
      <UiModulePageHeader
        title="System Health"
        description="Check deployment, API proxy, database, and first-admin readiness."
        icon="i-lucide-activity"
        primary-label="Refresh"
        primary-icon="i-lucide-refresh-cw"
        @primary="refresh"
      >
        <template #actions>
          <UBadge :color="health?.databaseReady ? 'success' : loading ? 'warning' : 'error'" variant="subtle">
            {{ loading ? 'Checking' : health?.databaseReady ? 'Live' : 'Needs Attention' }}
          </UBadge>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="subtle" :loading="loading" label="Refresh" @click="refresh" />
        </template>
      </UiModulePageHeader>

      <UAlert
        v-if="!canSeeAdmin"
        class="dashboard-alert"
        color="error"
        variant="subtle"
        icon="i-lucide-shield-alert"
        title="Admin access required"
        description="Only Owner and Admin users can view deployment health."
      />

      <template v-else>
        <div class="planner-metric-grid">
          <UCard v-for="metric in metrics" :key="metric.label" class="planner-metric-card">
            <div class="planner-metric-body">
              <UAvatar :icon="metric.icon" :color="metric.color" variant="subtle" />
              <div>
                <p>{{ metric.label }}</p>
                <strong>{{ metric.value }}</strong>
                <span>{{ metric.meta }}</span>
              </div>
            </div>
          </UCard>
        </div>

        <UCard class="planner-card">
          <template #header>
            <div class="planner-card-header">
              <div>
                <h2>Deployment Details</h2>
                <p>Use this page after Docker restart or Cloudflare tunnel changes.</p>
              </div>
              <UBadge :color="health?.databaseReady ? 'success' : 'error'" variant="subtle">
                {{ health?.databaseReady ? 'Healthy' : 'Not Ready' }}
              </UBadge>
            </div>
          </template>

          <div class="system-health-grid">
            <div v-for="[label, value] in detailRows" :key="label" class="system-health-row">
              <span>{{ label }}</span>
              <strong>{{ value }}</strong>
            </div>
          </div>
        </UCard>
      </template>
    </section>
  </AppShell>
</template>
