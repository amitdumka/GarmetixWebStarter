<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { APP_BUILD_CODE, APP_STAGE, APP_VERSION } from '~/utils/appVersion'

definePageMeta({
  layout: 'dashboard',
  middleware: ['auth']
})

const api = useGarmetixApi()
const loading = ref(false)
const error = ref('')
const health = ref<any>(null)
const appInfo = ref<any>(null)
const readiness = ref<any>(null)
const runtimeSmoke = ref<any>(null)
const finalAcceptance = ref<any>(null)
const checklist = ref<any>(null)

const summaryCards = computed(() => [
  {
    label: 'Version',
    value: appInfo.value?.version || APP_VERSION,
    detail: appInfo.value?.buildCode || APP_BUILD_CODE,
    icon: 'i-lucide-badge-check'
  },
  {
    label: 'API Health',
    value: health.value?.status || '-',
    detail: health.value?.databaseReady ? 'Database ready' : 'Database status pending',
    icon: 'i-lucide-heart-pulse'
  },
  {
    label: 'Readiness',
    value: readiness.value?.overallStatus || readiness.value?.status || '-',
    detail: `${readiness.value?.critical || 0} critical / ${readiness.value?.warnings || 0} warnings`,
    icon: 'i-lucide-shield-check'
  },
  {
    label: 'Runtime Smoke',
    value: runtimeSmoke.value?.overallStatus || '-',
    detail: `${runtimeSmoke.value?.passed || 0} passed / ${runtimeSmoke.value?.critical || 0} critical`,
    icon: 'i-lucide-flask-conical'
  }
])

const missingManifest = computed(() => finalAcceptance.value?.missingManifestCodes || [])
const sections = computed(() => finalAcceptance.value?.sections || [])
const checklistItems = computed(() => checklist.value?.items || [])

async function refresh() {
  loading.value = true
  error.value = ''
  const requests = [
    api.get<any>('health'),
    api.get<any>('app-info'),
    api.get<any>('production-readiness/summary'),
    api.get<any>('test-automation/runtime-smoke'),
    api.get<any>('stage10a/final-acceptance'),
    api.get<any>('stage10a/final-acceptance/checklist')
  ]

  try {
    const [healthResult, appInfoResult, readinessResult, runtimeResult, finalResult, checklistResult] = await Promise.allSettled(requests)
    const failures: string[] = []

    if (healthResult.status === 'fulfilled') health.value = healthResult.value
    else failures.push('health')

    if (appInfoResult.status === 'fulfilled') appInfo.value = appInfoResult.value
    else failures.push('app-info')

    if (readinessResult.status === 'fulfilled') readiness.value = readinessResult.value
    else failures.push('production-readiness')

    if (runtimeResult.status === 'fulfilled') runtimeSmoke.value = runtimeResult.value
    else failures.push('runtime-smoke')

    if (finalResult.status === 'fulfilled') finalAcceptance.value = finalResult.value
    else failures.push('final-acceptance')

    if (checklistResult.status === 'fulfilled') checklist.value = checklistResult.value
    else failures.push('checklist')

    if (failures.length) {
      error.value = `Some final-acceptance checks failed to load: ${failures.join(', ')}. The page is still usable; check Message Logs for backend details.`
    }
  } catch (err: any) {
    error.value = err?.data?.message || err?.message || 'Could not load Production Final Acceptance status.'
  } finally {
    loading.value = false
  }
}

function badgeColor(status?: string) {
  const value = String(status || '').toLowerCase()
  if (value.includes('ready') || value.includes('pass')) return 'success'
  if (value.includes('critical') || value.includes('blocked')) return 'error'
  return 'warning'
}

onMounted(refresh)
</script>

<template>
  <div class="space-y-6">
    <div class="page-header">
      <div>
        <p class="page-kicker">{{ APP_STAGE }}</p>
        <h1 class="page-title">Production Final Acceptance</h1>
        <p class="page-subtitle">
          One release gate for Docker build, schema upgrade, core modules, attendance/payroll, Today&apos;s dashboard, security and recovery checks before moving ahead.
        </p>
      </div>
      <div class="flex flex-wrap gap-2">
        <UButton to="/production-readiness" icon="i-lucide-shield-check" variant="subtle" label="Readiness" />
        <UButton icon="i-lucide-refresh-cw" :loading="loading" @click="refresh">Refresh</UButton>
      </div>
    </div>

    <UAlert
      v-if="error"
      color="error"
      variant="soft"
      icon="i-lucide-triangle-alert"
      title="Acceptance status could not be loaded"
      :description="error"
    />

    <UAlert
      v-else
      :color="badgeColor(finalAcceptance?.overallStatus)"
      variant="soft"
      icon="i-lucide-clipboard-check"
      :title="finalAcceptance?.overallStatus || 'Loading final acceptance'"
      :description="`${APP_VERSION} / ${APP_BUILD_CODE}`"
    />

    <div class="grid gap-4 md:grid-cols-4">
      <UCard v-for="card in summaryCards" :key="card.label">
        <div class="flex items-start justify-between gap-3">
          <div>
            <p class="text-xs uppercase tracking-wide text-muted">{{ card.label }}</p>
            <p class="mt-1 text-2xl font-semibold">{{ card.value }}</p>
            <p class="text-xs text-muted">{{ card.detail }}</p>
          </div>
          <UIcon :name="card.icon" class="size-6 text-muted" />
        </div>
      </UCard>
    </div>

    <UCard>
      <template #header>
        <div class="flex items-center justify-between gap-3">
          <div>
            <h2 class="text-lg font-semibold">Final acceptance sections</h2>
            <p class="text-sm text-muted">Review each section on the production host or staging copy before starting the next module.</p>
          </div>
          <UBadge :color="missingManifest.length ? 'error' : 'success'" variant="soft">
            {{ missingManifest.length ? `${missingManifest.length} manifest gaps` : 'Manifest complete' }}
          </UBadge>
        </div>
      </template>

      <div class="grid gap-4 lg:grid-cols-2">
        <div v-for="section in sections" :key="section.title" class="rounded-xl border border-default p-4">
          <div class="mb-3 flex items-start justify-between gap-3">
            <div>
              <p class="font-semibold">{{ section.title }}</p>
              <p class="text-xs text-muted">Required: {{ section.required ? 'Yes' : 'No' }}</p>
            </div>
            <UBadge :color="badgeColor(section.status)" variant="soft">{{ section.status }}</UBadge>
          </div>
          <ul class="space-y-2 text-sm text-muted">
            <li v-for="item in section.items" :key="item" class="flex gap-2">
              <UIcon name="i-lucide-check-circle-2" class="mt-0.5 size-4 shrink-0" />
              <span>{{ item }}</span>
            </li>
          </ul>
        </div>
      </div>
    </UCard>

    <div class="grid gap-4 lg:grid-cols-2">
      <UCard>
        <template #header>
          <h2 class="text-lg font-semibold">Required automated checks</h2>
        </template>
        <div class="space-y-2">
          <div v-for="code in finalAcceptance?.requiredManifestCodes || []" :key="code" class="flex items-center justify-between gap-3 rounded-lg border border-default p-3">
            <code class="text-xs">{{ code }}</code>
            <UBadge :color="missingManifest.includes(code) ? 'error' : 'success'" variant="soft">
              {{ missingManifest.includes(code) ? 'Missing' : 'Present' }}
            </UBadge>
          </div>
        </div>
      </UCard>

      <UCard>
        <template #header>
          <h2 class="text-lg font-semibold">Manual checklist export</h2>
        </template>
        <div class="space-y-2">
          <div v-for="item in checklistItems" :key="`${item.section}-${item.item}`" class="rounded-lg border border-default p-3">
            <p class="text-xs uppercase tracking-wide text-muted">{{ item.section }}</p>
            <p class="text-sm">{{ item.item }}</p>
          </div>
        </div>
      </UCard>
    </div>
  </div>
</template>
