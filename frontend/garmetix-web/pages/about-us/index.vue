<script setup lang="ts">
import { APP_BUILD_CODE, APP_BUILD_DATE, APP_HIGHLIGHTS, APP_RELEASE_NAME, APP_VERSION } from '~/utils/appVersion'

const api = useGarmetixApi()
const feedback = useUiFeedback()

const loading = ref(false)
const serverInfo = ref<any | null>(null)

const versionRows = computed(() => [
  { label: 'Product', value: serverInfo.value?.productName || 'Garmetix' },
  { label: 'Version', value: serverInfo.value?.version || APP_VERSION },
  { label: 'Release', value: serverInfo.value?.releaseName || APP_RELEASE_NAME },
  { label: 'Build date', value: serverInfo.value?.buildDate || APP_BUILD_DATE },
  { label: 'Build code', value: serverInfo.value?.buildCode || APP_BUILD_CODE },
  { label: 'API environment', value: serverInfo.value?.environment || 'Not loaded' }
])

const highlights = computed(() => serverInfo.value?.highlights?.length ? serverInfo.value.highlights : APP_HIGHLIGHTS)

async function refresh() {
  loading.value = true
  try {
    serverInfo.value = await api.get<any>('app-info')
  } catch (error) {
    serverInfo.value = null
    feedback.failed('App version info load failed', error)
  } finally {
    loading.value = false
  }
}

onMounted(refresh)
</script>

<template>
  <AppShell title="About Us" @refresh="refresh">
    <div class="space-y-6">
      <section class="rounded-3xl border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <div class="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
          <div class="space-y-3">
            <div class="flex items-center gap-3">
              <div class="rounded-2xl bg-primary/10 p-3 text-primary">
                <UIcon name="i-lucide-shirt" class="h-8 w-8" />
              </div>
              <div>
                <p class="text-xs font-semibold uppercase tracking-[0.3em] text-slate-500">{{ serverInfo?.releaseName || APP_RELEASE_NAME }}</p>
                <h1 class="text-2xl font-bold text-slate-950 dark:text-white">About Garmetix</h1>
              </div>
            </div>
            <p class="max-w-4xl text-sm leading-6 text-slate-500 dark:text-slate-400">
              Garmetix is a garment store management system for billing, purchase, inventory, GST reporting, customer/party management, stock operations, onboarding, seeding, backup and admin monitoring.
            </p>
          </div>
          <div class="flex flex-col items-start gap-2 rounded-2xl border border-primary/20 bg-primary/5 p-4 lg:min-w-64">
            <span class="text-xs font-semibold uppercase tracking-[0.25em] text-primary">Running version</span>
            <strong class="text-2xl text-slate-950 dark:text-white">v{{ serverInfo?.version || APP_VERSION }}</strong>
            <span class="text-xs text-slate-500">{{ serverInfo?.buildCode || APP_BUILD_CODE }}</span>
          </div>
        </div>
      </section>

      <div class="grid gap-6 xl:grid-cols-[1.1fr_0.9fr]">
        <UCard>
          <template #header>
            <div class="flex items-center justify-between gap-3">
              <div class="flex items-center gap-2">
                <UIcon name="i-lucide-badge-info" class="h-5 w-5" />
                <h2 class="font-semibold">Version identity</h2>
              </div>
              <UButton icon="i-lucide-refresh-cw" variant="subtle" :loading="loading" @click="refresh">Refresh</UButton>
            </div>
          </template>
          <div class="divide-y divide-slate-200 dark:divide-slate-800">
            <div v-for="row in versionRows" :key="row.label" class="grid gap-2 py-3 text-sm md:grid-cols-3">
              <span class="font-medium text-slate-500 dark:text-slate-400">{{ row.label }}</span>
              <span class="md:col-span-2 font-semibold text-slate-900 dark:text-white">{{ row.value }}</span>
            </div>
          </div>
          <template #footer>
            <p class="text-xs text-slate-500 dark:text-slate-400">
              Every code package should update this version so you can identify exactly which build is running.
            </p>
          </template>
        </UCard>

        <UCard>
          <template #header>
            <div class="flex items-center gap-2">
              <UIcon name="i-lucide-sparkles" class="h-5 w-5" />
              <h2 class="font-semibold">Current release highlights</h2>
            </div>
          </template>
          <ul class="space-y-3 text-sm text-slate-600 dark:text-slate-300">
            <li v-for="item in highlights" :key="item" class="flex gap-2">
              <UIcon name="i-lucide-check-circle-2" class="mt-0.5 h-4 w-4 shrink-0 text-primary" />
              <span>{{ item }}</span>
            </li>
          </ul>
        </UCard>
      </div>
    </div>
  </AppShell>
</template>
