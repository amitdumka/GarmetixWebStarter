<template>
  <UApp>
    <main class="min-h-screen bg-default text-default">
      <section class="mx-auto flex min-h-screen w-full max-w-7xl flex-col px-4 py-4 lg:px-6">
        <header class="flex flex-col gap-4 border-b border-default pb-4 lg:flex-row lg:items-center lg:justify-between">
          <div class="flex min-w-0 items-center gap-3">
            <div class="flex size-11 shrink-0 items-center justify-center border border-primary/30 bg-primary/10">
              <UIcon name="i-lucide-scan-barcode" class="size-5 text-primary" />
            </div>
            <div class="min-w-0">
              <div class="flex flex-wrap items-center gap-2">
                <h1 class="truncate text-xl font-semibold">{{ shell.title }}</h1>
                <UBadge color="primary" variant="subtle">{{ shell.badge }}</UBadge>
              </div>
              <p class="truncate text-sm text-muted">{{ activeUserLabel }}</p>
            </div>
          </div>

          <nav class="flex flex-wrap items-center gap-2" aria-label="Garmetix apps">
            <UButton
              v-for="link in shell.appLinks"
              :key="link.id"
              size="sm"
              :color="link.current ? 'primary' : 'neutral'"
              :variant="link.current ? 'solid' : 'soft'"
              :to="link.href || undefined"
              :disabled="link.current || !link.configured"
              :external="Boolean(link.href && !link.href.startsWith('/'))"
            >
              {{ link.label }}
            </UButton>
            <UButton v-if="authSnapshot.hasToken" size="sm" color="neutral" variant="ghost" icon="i-lucide-log-out" @click="logout">
              Logout
            </UButton>
          </nav>
        </header>

        <section class="grid gap-3 py-4 md:grid-cols-2 xl:grid-cols-4">
          <div v-for="status in statusCards" :key="status.key" class="border border-default bg-muted/20 p-4">
            <div class="flex items-center justify-between gap-3">
              <p class="text-sm text-muted">{{ status.label }}</p>
              <UBadge size="xs" :color="status.tone" variant="soft">
                <UIcon :name="status.icon" class="size-3" />
              </UBadge>
            </div>
            <p class="mt-2 truncate text-sm font-semibold">{{ status.value }}</p>
            <p class="mt-1 line-clamp-2 text-xs text-muted">{{ status.detail }}</p>
          </div>
        </section>

        <div class="grid flex-1 gap-4 lg:grid-cols-[220px_minmax(0,1fr)]">
          <aside class="border border-default bg-muted/10 p-3 lg:sticky lg:top-4 lg:self-start">
            <nav class="space-y-1" aria-label="POS routes">
              <UButton
                v-for="route in posMenuRoutes"
                :key="route.id"
                :to="route.href"
                :icon="route.icon"
                color="neutral"
                :variant="currentPath === route.href ? 'soft' : 'ghost'"
                class="w-full justify-start"
              >
                {{ route.label }}
              </UButton>
            </nav>
          </aside>

          <section class="min-w-0">
            <NuxtPage />
          </section>
        </div>
      </section>
    </main>
  </UApp>
</template>

<script setup lang="ts">
import { checkApiHealth, type ApiHealthResult } from '@garmetix/shared-api'
import { clearStoredSession, getAuthSessionSnapshot, type AuthSessionSnapshot } from '@garmetix/shared-auth'
import { buildAppShellModel, buildShellStatusCards } from '@garmetix/shared-ui'
import { buildAppTargetLinks, garmetixRoutes } from '../../config/routes'

const appId = 'pos' as const
const route = useRoute()
const runtimeConfig = useRuntimeConfig()
const apiBaseUrl = computed(() => String(runtimeConfig.public.apiBaseUrl || ''))
const appUrls = computed(() => (runtimeConfig.public.appUrls ?? {}) as Record<string, string | undefined>)
const apiHealth = ref<ApiHealthResult>({
  state: 'checking',
  label: 'Checking API',
  message: 'Waiting for the health endpoint.'
})
const authSnapshot = ref<AuthSessionSnapshot>({
  state: 'anonymous',
  hasToken: false,
  label: 'Checking auth',
  message: 'Reading browser token storage.'
})
const shell = computed(() => buildAppShellModel({
  appId,
  routes: garmetixRoutes,
  env: appUrls.value,
  appLinks: buildAppTargetLinks(appUrls.value, appId)
}))
const currentPath = computed(() => route.path)
const posMenuRoutes = [
  { id: 'dashboard', label: 'Counter', href: '/', icon: 'i-lucide-layout-dashboard' },
  { id: 'login', label: 'Login', href: '/login', icon: 'i-lucide-log-in' },
  { id: 'day-open', label: 'Day Open', href: '/day-open', icon: 'i-lucide-sunrise' },
  { id: 'sale', label: 'Sale', href: '/sale', icon: 'i-lucide-scan-barcode' },
  { id: 'hold-bills', label: 'Hold Bills', href: '/hold-bills', icon: 'i-lucide-pause-circle' },
  { id: 'returns', label: 'Returns', href: '/returns', icon: 'i-lucide-undo-2' },
  { id: 'print', label: 'Print Queue', href: '/print', icon: 'i-lucide-printer' },
  { id: 'day-close', label: 'Day Close', href: '/day-close', icon: 'i-lucide-sunset' }
]
const activeUserLabel = computed(() => {
  if (!authSnapshot.value.hasToken) return 'POS module foundation - sign in to start protected counter flows.'
  const user = authSnapshot.value.user
  return `${user?.name || user?.userName || 'Signed in'}${user?.storeId ? ' - store assigned' : ''}`
})
const statusCards = computed(() => buildShellStatusCards([
  {
    key: 'routes',
    label: 'Owned routes',
    value: String(shell.value.routeCount),
    detail: 'Route registry entries assigned to this app.',
    tone: 'primary'
  },
  {
    key: 'api',
    label: 'API service',
    value: apiHealth.value.label,
    detail: apiHealth.value.message,
    tone: apiHealth.value.state === 'live' ? 'success' : apiHealth.value.state === 'checking' ? 'warning' : 'error'
  },
  {
    key: 'auth',
    label: 'Auth session',
    value: authSnapshot.value.label,
    detail: authSnapshot.value.message,
    tone: authSnapshot.value.hasToken ? 'success' : 'neutral'
  },
  {
    key: 'stage',
    label: 'Current stage',
    value: 'Stage 12B.1',
    detail: 'POS route shell, login bridge, and counter page foundation.',
    tone: 'neutral'
  }
]))

function refreshAuthSnapshot() {
  authSnapshot.value = getAuthSessionSnapshot(window.localStorage)
}

function logout() {
  clearStoredSession(window.localStorage)
  refreshAuthSnapshot()
  navigateTo('/login')
}

onMounted(async () => {
  refreshAuthSnapshot()
  apiHealth.value = await checkApiHealth(apiBaseUrl.value)
})
</script>
