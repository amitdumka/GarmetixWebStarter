<template>
  <UApp>
    <main class="min-h-screen bg-default text-default">
      <section class="mx-auto flex min-h-screen w-full max-w-7xl flex-col px-4 py-4 lg:px-6">
        <header class="flex flex-col gap-4 border-b border-default pb-4 lg:flex-row lg:items-center lg:justify-between">
          <div class="flex min-w-0 items-center gap-3">
            <div class="flex size-11 shrink-0 items-center justify-center border border-primary/30 bg-primary/10">
              <UIcon name="i-lucide-briefcase-business" class="size-5 text-primary" />
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

        <div class="grid flex-1 gap-4 lg:grid-cols-[252px_minmax(0,1fr)]">
          <aside class="border border-default bg-muted/10 p-3 lg:sticky lg:top-4 lg:self-start">
            <nav class="space-y-1" aria-label="Back Office routes">
              <UButton
                v-for="item in mainMenuRoutes"
                :key="item.id"
                :to="item.href"
                :icon="item.icon"
                color="neutral"
                :variant="currentPath === item.href ? 'soft' : 'ghost'"
                class="w-full justify-start"
              >
                {{ item.label }}
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
import { garmetixModularVersion } from '../../config/version'

const appId = 'main' as const
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
const baseMainMenuRoutes = [
  { id: 'dashboard', label: 'Dashboard', href: '/', icon: 'i-lucide-layout-dashboard', protected: true },
  { id: 'login', label: 'Login', href: '/login', icon: 'i-lucide-log-in', protected: false },
  { id: 'todays', label: "Today's Dashboard", href: '/dashboard/todays', icon: 'i-lucide-calendar-days', protected: true },
  { id: 'store-manager', label: 'Store Manager', href: '/dashboard/store-manager', icon: 'i-lucide-store', protected: true },
  { id: 'store-day', label: 'Store Day', href: '/store-day', icon: 'i-lucide-sun-medium', protected: true },
  { id: 'billing', label: 'Sale Invoices', href: '/billing', icon: 'i-lucide-receipt-text', protected: true },
  { id: 'purchase', label: 'Purchase', href: '/purchase', icon: 'i-lucide-shopping-bag', protected: true },
  { id: 'inventory', label: 'Inventory', href: '/inventory', icon: 'i-lucide-boxes', protected: true },
  { id: 'customers', label: 'Customers', href: '/customers', icon: 'i-lucide-users', protected: true },
  { id: 'reports', label: 'Reports', href: '/reports', icon: 'i-lucide-chart-column', protected: true },
  { id: 'profile', label: 'Profile', href: '/profile', icon: 'i-lucide-circle-user-round', protected: true }
]
const mainMenuRoutes = computed(() => baseMainMenuRoutes.filter(item => authSnapshot.value.hasToken ? item.id !== 'login' : !item.protected))
const activeUserLabel = computed(() => {
  if (!authSnapshot.value.hasToken) return 'Back Office module foundation - sign in to view store operations.'
  const user = authSnapshot.value.user
  return `${user?.name || user?.userName || 'Signed in'}${user?.storeId ? ' - store assigned' : ''}`
})
const statusCards = computed(() => buildShellStatusCards([
  {
    key: 'routes',
    label: 'Owned routes',
    value: String(shell.value.routeCount),
    detail: 'Route registry entries still assigned to Back Office.',
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
    detail: authSnapshot.value.message.replace('POS flows', 'Back Office workflows'),
    tone: authSnapshot.value.hasToken ? 'success' : 'neutral'
  },
  {
    key: 'stage',
    label: 'Current stage',
    value: garmetixModularVersion.stage,
    detail: garmetixModularVersion.summary,
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

watch(() => route.fullPath, () => {
  if (import.meta.client) refreshAuthSnapshot()
})
</script>
