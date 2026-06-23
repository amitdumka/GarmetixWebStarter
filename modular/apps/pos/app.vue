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

        <section class="grid gap-4 md:grid-cols-3">
          <div class="border border-default bg-muted/20 p-4">
            <p class="text-sm text-muted">Owned route entries</p>
            <p class="mt-2 text-3xl font-semibold">{{ shell.routeCount }}</p>
          </div>
          <div class="border border-default bg-muted/20 p-4">
            <p class="text-sm text-muted">API base</p>
            <p class="mt-2 truncate text-sm font-medium">{{ apiBaseUrl }}</p>
          </div>
          <div class="border border-default bg-muted/20 p-4">
            <p class="text-sm text-muted">Current stage</p>
            <p class="mt-2 text-sm font-medium">Stage 12A.4 menu foundation</p>
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
import { buildAppShellModel } from '@garmetix/shared-ui'
import { buildAppTargetLinks, garmetixRoutes } from '../../config/routes'

const appId = 'pos' as const
const runtimeConfig = useRuntimeConfig()
const apiBaseUrl = computed(() => String(runtimeConfig.public.apiBaseUrl || ''))
const appUrls = computed(() => (runtimeConfig.public.appUrls ?? {}) as Record<string, string | undefined>)
const shell = computed(() => buildAppShellModel({
  appId,
  routes: garmetixRoutes,
  env: appUrls.value,
  appLinks: buildAppTargetLinks(appUrls.value, appId)
}))
</script>
