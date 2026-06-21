<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { APP_BUILD_CODE, APP_RELEASE_NAME, APP_STAGE, APP_VERSION } from '~/utils/appVersion'

definePageMeta({
  layout: 'dashboard',
  middleware: ['auth']
})

const api = useGarmetixApi()
const loading = ref(false)
const error = ref('')
const support = ref<any>(null)
const drills = ref<any>(null)

const drillCards = computed(() => support.value?.drills || [])
const drillSteps = computed(() => drills.value?.items || [])

const quickLinks = [
  { to: '/message-logs', label: 'Message Logs', icon: 'i-lucide-list-collapse' },
  { to: '/runtime-diagnostics', label: 'Runtime Diagnostics', icon: 'i-lucide-stethoscope' },
  { to: '/system-health', label: 'System Health', icon: 'i-lucide-activity' },
  { to: '/production-readiness', label: 'Production Readiness', icon: 'i-lucide-shield-check' },
  { to: '/backup-maintenance', label: 'Backup Maintenance', icon: 'i-lucide-hard-drive-download' },
  { to: '/email-delivery', label: 'Email Delivery', icon: 'i-lucide-mail-check' },
  { to: '/print-final-acceptance', label: 'Print Acceptance', icon: 'i-lucide-printer-check' },
  { to: '/document-scan', label: 'Document Scanner', icon: 'i-lucide-scan-qr-code' }
]

const summaryCards = computed(() => [
  {
    label: 'Version',
    value: support.value?.version || APP_VERSION,
    detail: support.value?.buildCode || APP_BUILD_CODE,
    icon: 'i-lucide-badge-check'
  },
  {
    label: 'Support',
    value: support.value?.overallStatus || 'Loading',
    detail: APP_RELEASE_NAME,
    icon: 'i-lucide-life-buoy'
  },
  {
    label: 'Public Origin',
    value: support.value?.publicOrigin || '-',
    detail: support.value?.tunnelHint ? 'Tunnel detected' : 'Direct or proxy host',
    icon: 'i-lucide-globe'
  },
  {
    label: 'Drills',
    value: String(support.value?.drillCount || drillCards.value.length || 0),
    detail: 'production issue paths',
    icon: 'i-lucide-list-checks'
  }
])

async function refresh() {
  loading.value = true
  error.value = ''

  try {
    const [supportResult, drillResult] = await Promise.allSettled([
      api.get<any>('stage10l/production-support'),
      api.get<any>('stage10l/production-support/drills')
    ])

    const failures: string[] = []
    if (supportResult.status === 'fulfilled') support.value = supportResult.value
    else failures.push('support summary')

    if (drillResult.status === 'fulfilled') drills.value = drillResult.value
    else failures.push('support drills')

    if (failures.length) {
      error.value = `${failures.join(' and ')} could not be loaded. Open Message Logs for stored backend details.`
    }
  } catch (err: any) {
    error.value = err?.data?.message || err?.message || 'Could not load production support.'
  } finally {
    loading.value = false
  }
}

function severityColor(severity?: string) {
  const value = String(severity || '').toLowerCase()
  if (value.includes('critical')) return 'error'
  if (value.includes('high')) return 'warning'
  return 'primary'
}

onMounted(refresh)
</script>

<template>
  <div class="space-y-6">
    <div class="page-header">
      <div>
        <p class="page-kicker">{{ APP_STAGE }}</p>
        <h1 class="page-title">Production Support</h1>
        <p class="page-subtitle">
          Operator-friendly drills for failed save, failed print, backup warning, email/share failure and Cloudflare or hosted API mismatch.
        </p>
      </div>
      <div class="flex flex-wrap gap-2">
        <UButton to="/stage10k-operator-acceptance" icon="i-lucide-clipboard-check" variant="subtle" label="Operator Acceptance" />
        <UButton icon="i-lucide-refresh-cw" :loading="loading" @click="refresh">Refresh</UButton>
      </div>
    </div>

    <UAlert
      v-if="error"
      color="error"
      variant="soft"
      icon="i-lucide-triangle-alert"
      title="Production support could not be loaded"
      :description="error"
    />

    <div class="grid gap-4 md:grid-cols-4">
      <UCard v-for="card in summaryCards" :key="card.label">
        <div class="flex items-start justify-between gap-3">
          <div class="min-w-0">
            <p class="text-xs uppercase text-muted">{{ card.label }}</p>
            <p class="mt-1 truncate text-xl font-semibold">{{ card.value }}</p>
            <p class="truncate text-xs text-muted">{{ card.detail }}</p>
          </div>
          <UIcon :name="card.icon" class="size-6 shrink-0 text-muted" />
        </div>
      </UCard>
    </div>

    <UCard>
      <template #header>
        <div class="flex flex-wrap items-center justify-between gap-3">
          <div>
            <h2 class="text-lg font-semibold">Support shortcuts</h2>
            <p class="text-sm text-muted">Open these first when an operator reports a production issue.</p>
          </div>
          <UBadge :color="support?.tunnelHint ? 'warning' : 'success'" variant="soft">
            {{ support?.tunnelHint ? 'Tunnel/proxy host detected' : 'Host signal normal' }}
          </UBadge>
        </div>
      </template>

      <div class="grid gap-2 sm:grid-cols-2 lg:grid-cols-4">
        <UButton
          v-for="link in quickLinks"
          :key="link.to"
          :to="link.to"
          :icon="link.icon"
          color="neutral"
          variant="subtle"
          block
          class="justify-start"
        >
          {{ link.label }}
        </UButton>
      </div>
    </UCard>

    <div class="grid gap-4 xl:grid-cols-2">
      <div v-for="drill in drillCards" :key="drill.title" class="rounded-lg border border-default bg-default p-4">
        <div class="mb-4 flex flex-wrap items-start justify-between gap-3">
          <div class="min-w-0">
            <p class="font-semibold">{{ drill.title }}</p>
            <p class="text-sm text-muted">{{ drill.symptom }}</p>
          </div>
          <div class="flex gap-2">
            <UBadge :color="severityColor(drill.severity)" variant="soft">{{ drill.severity }}</UBadge>
            <UButton :to="drill.route" icon="i-lucide-arrow-up-right" size="xs" variant="subtle">Open</UButton>
          </div>
        </div>

        <div class="space-y-2">
          <div v-for="step in drill.steps" :key="step" class="flex gap-2 rounded-md border border-muted p-2 text-sm">
            <UIcon name="i-lucide-circle-check" class="mt-0.5 size-4 shrink-0 text-primary" />
            <span>{{ step }}</span>
          </div>
        </div>

        <div class="mt-4 flex flex-wrap gap-2">
          <UBadge v-for="evidence in drill.evidence" :key="evidence" color="neutral" variant="soft">
            {{ evidence }}
          </UBadge>
        </div>
      </div>
    </div>

    <UCard>
      <template #header>
        <h2 class="text-lg font-semibold">Printable step list</h2>
      </template>

      <div class="grid gap-3 lg:grid-cols-2">
        <div v-for="item in drillSteps" :key="`${item.drill}-${item.step}`" class="rounded-lg border border-default p-3">
          <div class="mb-1 flex flex-wrap items-center gap-2">
            <UBadge :color="severityColor(item.severity)" variant="soft">{{ item.severity }}</UBadge>
            <UBadge color="neutral" variant="soft">{{ item.drill }}</UBadge>
          </div>
          <p class="text-sm">{{ item.step }}</p>
        </div>
      </div>
    </UCard>
  </div>
</template>
