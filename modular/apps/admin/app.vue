<template>
  <UApp>
    <main class="min-h-screen bg-default text-default">
      <section class="mx-auto flex min-h-screen w-full max-w-7xl flex-col px-4 py-4 lg:px-6">
        <header class="flex flex-col gap-4 border-b border-default pb-4 lg:flex-row lg:items-center lg:justify-between">
          <div class="flex min-w-0 items-center gap-3">
            <div class="flex size-11 shrink-0 items-center justify-center border border-primary/30 bg-primary/10">
              <UIcon name="i-lucide-shield-check" class="size-5 text-primary" />
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
            <nav class="space-y-1" aria-label="Admin routes">
              <UButton
                v-for="item in adminMenuRoutes"
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
import { isAdminSession } from './utils/admin-api'

const appId = 'admin' as const
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
const baseAdminMenuRoutes = [
  { id: 'dashboard', label: 'Admin Home', href: '/', icon: 'i-lucide-layout-dashboard', protected: true },
  { id: 'login', label: 'Login', href: '/login', icon: 'i-lucide-log-in', protected: false },
  { id: 'setup', label: 'Company', href: '/setup', icon: 'i-lucide-building-2', protected: true },
  { id: 'client-onboarding', label: 'Onboarding', href: '/client-onboarding', icon: 'i-lucide-handshake', protected: true },
  { id: 'access', label: 'Users & Roles', href: '/access', icon: 'i-lucide-shield-check', protected: true },
  { id: 'license', label: 'License', href: '/license-activation', icon: 'i-lucide-key-round', protected: true },
  { id: 'message-logs', label: 'Message Logs', href: '/message-logs', icon: 'i-lucide-message-square-warning', protected: true },
  { id: 'import-export', label: 'Import Export', href: '/import-export', icon: 'i-lucide-arrow-up-down', protected: true },
  { id: 'data-consistency', label: 'Data Consistency', href: '/data-consistency', icon: 'i-lucide-database-zap', protected: true },
  { id: 'backup', label: 'Backup', href: '/backup-maintenance', icon: 'i-lucide-hard-drive-download', protected: true },
  { id: 'drive', label: 'Google Drive', href: '/google-drive-backup', icon: 'i-lucide-cloud-upload', protected: true },
  { id: 'system-health', label: 'System Health', href: '/system-health', icon: 'i-lucide-heart-pulse', protected: true },
  { id: 'runtime', label: 'Runtime', href: '/runtime-diagnostics', icon: 'i-lucide-bug', protected: true },
  { id: 'production', label: 'Production', href: '/production-readiness', icon: 'i-lucide-rocket', protected: true },
  { id: 'support', label: 'Support', href: '/production-support', icon: 'i-lucide-life-buoy', protected: true },
  { id: 'rehearsal', label: 'Rehearsal', href: '/production-rehearsal', icon: 'i-lucide-play-circle', protected: true }
]
const adminMenuRoutes = computed(() => baseAdminMenuRoutes.filter(item => authSnapshot.value.hasToken ? item.id !== 'login' : !item.protected))
const activeUserLabel = computed(() => {
  if (!authSnapshot.value.hasToken) return 'Admin/SaaS module foundation - sign in with Owner, Admin or SuperAdmin access.'
  const user = authSnapshot.value.user
  const role = user?.role || user?.userType || (user?.isSuperAdmin ? 'SuperAdmin' : user?.admin ? 'Admin' : 'User')
  return `${user?.name || user?.userName || 'Signed in'} - ${role}${isAdminSession(user) ? '' : ' access review needed'}`
})
const statusCards = computed(() => buildShellStatusCards([
  {
    key: 'routes',
    label: 'Owned routes',
    value: String(shell.value.routeCount),
    detail: 'Route registry entries assigned to Admin/SaaS.',
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
    detail: authSnapshot.value.message.replace('POS flows', 'Admin workflows'),
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
