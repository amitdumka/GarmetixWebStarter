<template>
  <UApp>
    <main class="min-h-screen bg-default text-default">
      <section class="mx-auto flex min-h-screen w-full max-w-7xl flex-col px-4 py-4 lg:px-6">
        <header class="flex flex-col gap-4 border-b border-default pb-4 lg:flex-row lg:items-center lg:justify-between">
          <div class="flex min-w-0 items-center gap-3">
            <div class="flex size-11 shrink-0 items-center justify-center border border-primary/30 bg-primary/10">
              <UIcon name="i-lucide-book-open-check" class="size-5 text-primary" />
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
            <nav class="space-y-1" aria-label="Books routes">
              <UButton
                v-for="item in booksMenuRoutes"
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

const appId = 'books' as const
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
const baseBooksMenuRoutes = [
  { id: 'dashboard', label: 'Books Home', href: '/', icon: 'i-lucide-layout-dashboard', protected: true },
  { id: 'login', label: 'Login', href: '/login', icon: 'i-lucide-log-in', protected: false },
  { id: 'accounting', label: 'Accounting', href: '/accounting', icon: 'i-lucide-book-open-check', protected: true },
  { id: 'parties', label: 'Parties', href: '/parties', icon: 'i-lucide-contact-round', protected: true },
  { id: 'vouchers', label: 'Vouchers', href: '/vouchers', icon: 'i-lucide-file-signature', protected: true },
  { id: 'petty-cash', label: 'Petty Cash', href: '/petty-cash', icon: 'i-lucide-wallet', protected: true },
  { id: 'cash-details', label: 'Cash Details', href: '/cash-details', icon: 'i-lucide-banknote', protected: true },
  { id: 'vendor-payments', label: 'Vendor Payments', href: '/vendor-payments', icon: 'i-lucide-hand-coins', protected: true },
  { id: 'vendor-settlements', label: 'Vendor Settlements', href: '/vendor-settlements', icon: 'i-lucide-scale', protected: true },
  { id: 'debit-notes', label: 'Debit Notes', href: '/debit-notes', icon: 'i-lucide-file-minus-2', protected: true },
  { id: 'credit-notes', label: 'Credit Notes', href: '/credit-notes', icon: 'i-lucide-file-plus-2', protected: true },
  { id: 'commercial-notes', label: 'Commercial Notes', href: '/commercial-notes', icon: 'i-lucide-files', protected: true },
  { id: 'gst-returns', label: 'GST Returns', href: '/gst-returns', icon: 'i-lucide-file-check-2', protected: true },
  { id: 'gst-reports', label: 'GST Reports', href: '/gst-reports', icon: 'i-lucide-chart-column', protected: true },
  { id: 'gst-production', label: 'GST Readiness', href: '/gst-production', icon: 'i-lucide-factory', protected: true },
  { id: 'locks', label: 'FY Locks', href: '/financial-year-locks', icon: 'i-lucide-lock-keyhole', protected: true }
]
const booksMenuRoutes = computed(() => baseBooksMenuRoutes.filter(item => authSnapshot.value.hasToken ? item.id !== 'login' : !item.protected))
const activeUserLabel = computed(() => {
  if (!authSnapshot.value.hasToken) return 'Books module foundation - sign in to view accounting routes.'
  const user = authSnapshot.value.user
  return `${user?.name || user?.userName || 'Signed in'}${user?.storeId ? ' - store assigned' : ''}`
})
const statusCards = computed(() => buildShellStatusCards([
  {
    key: 'routes',
    label: 'Owned routes',
    value: String(shell.value.routeCount),
    detail: 'Route registry entries assigned to Books.',
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
    detail: authSnapshot.value.message.replace('POS flows', 'Books workflows'),
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
