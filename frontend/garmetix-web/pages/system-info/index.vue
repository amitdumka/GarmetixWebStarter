<script setup lang="ts">
import { APP_BUILD_CODE, APP_RELEASE_NAME, APP_STAGE, APP_VERSION } from '~/utils/appVersion'

const api = useGarmetixApi()
const auth = useAuth()
const access = useAccessControl()
const feedback = useUiFeedback()

const loading = ref(false)
const backendInfo = ref<any | null>(null)
const systemInfo = ref<any | null>(null)
const health = ref<any | null>(null)
const lastLoadedAt = ref<Date | null>(null)

const shellMode = computed(() => useRuntimeConfig().public.dashboardShell || 'dashboard')
const frontendVersion = computed(() => ({
  version: APP_VERSION,
  stage: APP_STAGE,
  releaseName: APP_RELEASE_NAME,
  buildCode: APP_BUILD_CODE
}))
const backendVersion = computed(() => ({
  version: backendInfo.value?.version || systemInfo.value?.version || '-',
  stage: backendInfo.value?.stage || systemInfo.value?.stage || '-',
  releaseName: backendInfo.value?.releaseName || systemInfo.value?.releaseName || '-',
  buildCode: backendInfo.value?.buildCode || systemInfo.value?.buildCode || '-'
}))
const versionMatched = computed(() => backendVersion.value.version === APP_VERSION && backendVersion.value.buildCode === APP_BUILD_CODE)
const visibleRouteCount = computed(() => access.routeRules.filter((rule) => access.canAccessPath(rule.path)).length)
const hiddenRouteCount = computed(() => access.routeRules.length - visibleRouteCount.value)
const routeRows = computed(() => access.routeRules.map((rule) => ({
  ...rule,
  allowed: access.canAccessPath(rule.path),
  rolesLabel: rule.roles.join(', ')
})))
const groupedRouteSummary = computed(() => {
  const groups = new Map<string, { module: string, total: number, allowed: number }>()
  for (const row of routeRows.value) {
    const current = groups.get(row.module) || { module: row.module, total: 0, allowed: 0 }
    current.total++
    if (row.allowed) current.allowed++
    groups.set(row.module, current)
  }
  return [...groups.values()]
})
const userRows = computed(() => [
  { label: 'User', value: auth.user.value?.name || auth.user.value?.userName || '-' },
  { label: 'Role', value: auth.user.value?.role || auth.user.value?.userType || '-' },
  { label: 'User Type', value: auth.user.value?.userType || '-' },
  { label: 'Admin', value: auth.user.value?.admin ? 'Yes' : 'No' },
  { label: 'Detected Roles', value: access.userRoles.value.join(', ') || '-' }
])
const apiRows = computed(() => [
  { label: 'API Health', value: health.value?.databaseReady ? 'Database ready' : (health.value ? 'API reachable' : 'Not checked') },
  { label: 'Environment', value: systemInfo.value?.environment || backendInfo.value?.environment || '-' },
  { label: 'Server Time UTC', value: formatDate(systemInfo.value?.serverTimeUtc) },
  { label: 'Process Started UTC', value: formatDate(systemInfo.value?.processStartedAtUtc) },
  { label: 'Uptime', value: formatDuration(systemInfo.value?.uptimeSeconds) },
  { label: 'Assembly Version', value: systemInfo.value?.assemblyVersion || backendInfo.value?.assemblyVersion || '-' }
])

function formatDate(value?: string) {
  return value ? new Date(value).toLocaleString('en-IN') : '-'
}

function formatDuration(seconds?: number) {
  const total = Number(seconds || 0)
  if (!total) return '-'
  const days = Math.floor(total / 86400)
  const hours = Math.floor((total % 86400) / 3600)
  const minutes = Math.floor((total % 3600) / 60)
  return [days ? `${days}d` : '', hours ? `${hours}h` : '', `${minutes}m`].filter(Boolean).join(' ')
}

async function refresh() {
  loading.value = true
  try {
    const [info, system, healthStatus] = await Promise.all([
      api.get<any>('app-info/version'),
      api.get<any>('app-info/system'),
      $fetch<any>('/api/health').catch(() => null)
    ])
    backendInfo.value = info
    systemInfo.value = system
    health.value = healthStatus
    lastLoadedAt.value = new Date()
  } catch (error) {
    feedback.failed('System info load failed', error)
  } finally {
    loading.value = false
  }
}

onMounted(refresh)
</script>

<template>
  <AppShell title="System Info" @refresh="refresh">
    <section class="dashboard-v3-page">
      <div class="dashboard-v3-hero business">
        <div>
          <UBadge color="primary" variant="subtle" icon="i-lucide-monitor-cog">Stage 7H</UBadge>
          <h1>System Info</h1>
          <p>Confirm frontend/backend version identity, shell mode, API status and permission route coverage before support or rollback.</p>
        </div>
        <div class="dashboard-v3-hero-actions">
          <UBadge :color="versionMatched ? 'success' : 'warning'" variant="subtle" :icon="versionMatched ? 'i-lucide-check-circle-2' : 'i-lucide-triangle-alert'">
            {{ versionMatched ? 'Version matched' : 'Version mismatch' }}
          </UBadge>
          <UButton icon="i-lucide-refresh-cw" :loading="loading" label="Refresh" @click="refresh" />
        </div>
      </div>

      <div class="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        <UCard>
          <div class="space-y-1">
            <p class="text-xs font-semibold uppercase tracking-[0.2em] text-muted">Frontend</p>
            <h2 class="text-2xl font-bold">v{{ frontendVersion.version }}</h2>
            <p class="text-sm text-muted">{{ frontendVersion.stage }} · {{ frontendVersion.buildCode }}</p>
          </div>
        </UCard>
        <UCard>
          <div class="space-y-1">
            <p class="text-xs font-semibold uppercase tracking-[0.2em] text-muted">Backend</p>
            <h2 class="text-2xl font-bold">v{{ backendVersion.version }}</h2>
            <p class="text-sm text-muted">{{ backendVersion.stage }} · {{ backendVersion.buildCode }}</p>
          </div>
        </UCard>
        <UCard>
          <div class="space-y-1">
            <p class="text-xs font-semibold uppercase tracking-[0.2em] text-muted">Shell Mode</p>
            <h2 class="text-2xl font-bold capitalize">{{ shellMode }}</h2>
            <p class="text-sm text-muted">Set NUXT_PUBLIC_DASHBOARD_SHELL=legacy to revert.</p>
          </div>
        </UCard>
        <UCard>
          <div class="space-y-1">
            <p class="text-xs font-semibold uppercase tracking-[0.2em] text-muted">Routes</p>
            <h2 class="text-2xl font-bold">{{ visibleRouteCount }}/{{ access.routeRules.length }}</h2>
            <p class="text-sm text-muted">Visible to current user · {{ hiddenRouteCount }} hidden.</p>
          </div>
        </UCard>
      </div>

      <div class="grid gap-6 xl:grid-cols-[1fr_1fr]">
        <UCard>
          <template #header>
            <div class="flex items-center gap-2">
              <UIcon name="i-lucide-server-cog" class="h-5 w-5" />
              <h2 class="font-semibold">API and runtime status</h2>
            </div>
          </template>
          <div class="divide-y divide-default">
            <div v-for="row in apiRows" :key="row.label" class="grid gap-2 py-3 text-sm md:grid-cols-3">
              <span class="font-medium text-muted">{{ row.label }}</span>
              <strong class="md:col-span-2">{{ row.value }}</strong>
            </div>
          </div>
        </UCard>

        <UCard>
          <template #header>
            <div class="flex items-center gap-2">
              <UIcon name="i-lucide-user-check" class="h-5 w-5" />
              <h2 class="font-semibold">Current user access</h2>
            </div>
          </template>
          <div class="divide-y divide-default">
            <div v-for="row in userRows" :key="row.label" class="grid gap-2 py-3 text-sm md:grid-cols-3">
              <span class="font-medium text-muted">{{ row.label }}</span>
              <strong class="md:col-span-2">{{ row.value }}</strong>
            </div>
          </div>
        </UCard>
      </div>

      <UCard>
        <template #header>
          <div class="flex flex-col gap-2 md:flex-row md:items-center md:justify-between">
            <div class="flex items-center gap-2">
              <UIcon name="i-lucide-route" class="h-5 w-5" />
              <h2 class="font-semibold">Dashboard route audit</h2>
            </div>
            <p class="text-xs text-muted">Last loaded: {{ lastLoadedAt ? lastLoadedAt.toLocaleString('en-IN') : '-' }}</p>
          </div>
        </template>
        <div class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
          <div v-for="group in groupedRouteSummary" :key="group.module" class="rounded-2xl border border-default p-4">
            <p class="text-sm font-semibold">{{ group.module }}</p>
            <p class="mt-1 text-xs text-muted">{{ group.allowed }} of {{ group.total }} visible</p>
            <UProgress class="mt-3" :model-value="Math.round((group.allowed / Math.max(group.total, 1)) * 100)" />
          </div>
        </div>
        <div class="mt-5 overflow-x-auto rounded-2xl border border-default">
          <table class="min-w-full divide-y divide-default text-sm">
            <thead class="bg-muted/40">
              <tr>
                <th class="px-4 py-3 text-left font-semibold">Page</th>
                <th class="px-4 py-3 text-left font-semibold">Route</th>
                <th class="px-4 py-3 text-left font-semibold">Module</th>
                <th class="px-4 py-3 text-left font-semibold">Allowed Roles</th>
                <th class="px-4 py-3 text-left font-semibold">Current User</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-default">
              <tr v-for="row in routeRows" :key="row.path">
                <td class="px-4 py-3 font-medium">{{ row.label }}</td>
                <td class="px-4 py-3 font-mono text-xs">{{ row.path }}</td>
                <td class="px-4 py-3">{{ row.module }}</td>
                <td class="px-4 py-3 text-muted">{{ row.rolesLabel }}</td>
                <td class="px-4 py-3">
                  <UBadge :color="row.allowed ? 'success' : 'neutral'" variant="subtle">{{ row.allowed ? 'Visible' : 'Hidden' }}</UBadge>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </UCard>

      <UCard>
        <template #header>
          <div class="flex items-center gap-2">
            <UIcon name="i-lucide-undo-2" class="h-5 w-5" />
            <h2 class="font-semibold">Safe rollback reminder</h2>
          </div>
        </template>
        <div class="space-y-3 text-sm text-muted">
          <p>No Stage 7H page removes existing modules. This page only audits shell/version/access state.</p>
          <pre class="overflow-x-auto rounded-xl bg-slate-950 p-4 text-xs text-slate-100">NUXT_PUBLIC_DASHBOARD_SHELL=legacy</pre>
          <p>Use the legacy shell only for temporary rollback while keeping current pages and backend unchanged.</p>
        </div>
      </UCard>
    </section>
  </AppShell>
</template>
