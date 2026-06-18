<script setup lang="ts">
import { APP_BUILD_CODE, APP_BUILD_DATE, APP_HIGHLIGHTS, APP_RELEASE_NAME, APP_VERSION } from '~/utils/appVersion'

const api = useGarmetixApi()
const feedback = useUiFeedback()

const loading = ref(false)
const serverInfo = ref<any | null>(null)
const loadError = ref('')

useHead({ title: 'About Garmetix' })

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
  loadError.value = ''
  try {
    serverInfo.value = await api.get<any>('app-info')
  } catch (error) {
    serverInfo.value = null
    loadError.value = feedback.errorMessage(error, 'Current product information could not be loaded. Try again.', 'App version info load failed')
  } finally {
    loading.value = false
  }
}

onMounted(refresh)
</script>

<template>
  <AppShell title="About Garmetix" @refresh="refresh">
    <div class="space-y-6">
      <UiModulePageHeader
        title="About Garmetix"
        description="Garment store management for billing, purchase, inventory, accounting, GST, people, payroll, and controlled administration."
        icon="i-lucide-shirt"
      >
        <template #actions>
          <UBadge color="primary" variant="subtle">v{{ serverInfo?.version || APP_VERSION }}</UBadge>
          <UButton icon="i-lucide-refresh-cw" variant="subtle" :loading="loading" label="Refresh" @click="refresh" />
        </template>
      </UiModulePageHeader>

      <UAlert
        v-if="loadError"
        color="error"
        variant="subtle"
        icon="i-lucide-circle-alert"
        title="Product information is unavailable"
        :description="loadError"
        :actions="[{ label: 'Try again', icon: 'i-lucide-refresh-cw', onClick: refresh }]"
      />

      <div class="grid gap-6 xl:grid-cols-[1.1fr_0.9fr]">
        <UCard>
          <template #header>
            <div class="flex items-center justify-between gap-3">
              <div class="flex items-center gap-2">
                <UIcon name="i-lucide-badge-info" class="h-5 w-5" />
                <h2 class="font-semibold">Version identity</h2>
              </div>
            </div>
          </template>
          <div v-if="loading && !serverInfo" class="space-y-3">
            <USkeleton v-for="row in 6" :key="row" class="h-10 w-full" />
          </div>
          <div v-else class="divide-y divide-slate-200 dark:divide-slate-800">
            <div v-for="row in versionRows" :key="row.label" class="grid gap-2 py-3 text-sm md:grid-cols-3">
              <span class="font-medium text-slate-500 dark:text-slate-400">{{ row.label }}</span>
              <span class="md:col-span-2 font-semibold text-slate-900 dark:text-white">{{ row.value }}</span>
            </div>
          </div>
          <template #footer>
            <p class="text-xs text-slate-500 dark:text-slate-400">
              Use the version and build code when requesting support or confirming an update.
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
