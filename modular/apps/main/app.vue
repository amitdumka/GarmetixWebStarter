<template>
  <UApp>
    <main class="min-h-screen bg-default text-default">
      <section class="mx-auto flex min-h-screen w-full max-w-7xl flex-col gap-8 px-5 py-6 lg:px-8">
        <header class="flex flex-col gap-5 border-b border-default pb-5 lg:flex-row lg:items-end lg:justify-between">
          <div class="space-y-3">
            <UBadge color="primary" variant="subtle" class="w-fit">{{ shell.badge }}</UBadge>
            <div class="space-y-2">
              <h1 class="text-3xl font-semibold">{{ shell.title }}</h1>
              <p class="max-w-2xl text-sm text-muted">{{ shell.subtitle }}</p>
            </div>
          </div>

          <nav class="flex flex-wrap gap-2" aria-label="Garmetix apps">
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
          </nav>
        </header>

        <section class="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
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

        <section class="grid gap-4 lg:grid-cols-2">
          <article v-for="group in shell.groups" :key="group.key" class="border border-default">
            <div class="border-b border-default px-4 py-3">
              <h2 class="text-base font-semibold">{{ group.label }}</h2>
            </div>
            <div class="divide-y divide-default">
              <div v-for="route in group.routes" :key="route.id" class="flex items-center justify-between gap-3 px-4 py-3">
                <div class="min-w-0">
                  <div class="flex items-center gap-2">
                    <UIcon :name="route.icon" class="size-4 shrink-0 text-muted" />
                    <p class="truncate text-sm font-medium">{{ route.label }}</p>
                  </div>
                  <p class="mt-1 truncate text-xs text-muted">{{ route.href }}</p>
                </div>
                <UBadge size="xs" color="neutral" variant="soft">{{ route.status }}</UBadge>
              </div>
            </div>
          </article>
        </section>
      </section>
    </main>
  </UApp>
</template>

<script setup lang="ts">
import { checkApiHealth, type ApiHealthResult } from '@garmetix/shared-api'
import { getAuthSessionSnapshot, type AuthSessionSnapshot } from '@garmetix/shared-auth'
import { buildAppShellModel, buildShellStatusCards } from '@garmetix/shared-ui'
import { buildAppTargetLinks, garmetixRoutes } from '../../config/routes'

const appId = 'main' as const
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
    value: 'Stage 12A.5',
    detail: 'Shared shell layout and status contracts.',
    tone: 'neutral'
  }
]))

onMounted(async () => {
  authSnapshot.value = getAuthSessionSnapshot(window.localStorage)
  apiHealth.value = await checkApiHealth(apiBaseUrl.value)
})
</script>
