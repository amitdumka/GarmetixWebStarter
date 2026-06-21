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
const rehearsal = ref<any>(null)
const runSheet = ref<any>(null)

const phases = computed(() => rehearsal.value?.phases || [])
const issueBuckets = computed(() => rehearsal.value?.issueBuckets || [])
const runSheetItems = computed(() => runSheet.value?.items || [])

const quickLinks = [
  { to: '/production-readiness', label: 'Readiness', icon: 'i-lucide-shield-check' },
  { to: '/store-day', label: 'Store Day', icon: 'i-lucide-store' },
  { to: '/billing/new', label: 'Sale Invoice', icon: 'i-lucide-receipt-text' },
  { to: '/document-scan', label: 'Document Scanner', icon: 'i-lucide-scan-qr-code' },
  { to: '/import-export', label: 'Import / Export', icon: 'i-lucide-file-down' },
  { to: '/stock-reports', label: 'Stock Reports', icon: 'i-lucide-warehouse' },
  { to: '/vouchers', label: 'Vouchers', icon: 'i-lucide-file-check-2' },
  { to: '/attendance/today', label: 'Attendance', icon: 'i-lucide-calendar-check' },
  { to: '/payroll', label: 'Payroll', icon: 'i-lucide-badge-indian-rupee' },
  { to: '/production-support', label: 'Support', icon: 'i-lucide-life-buoy' },
  { to: '/message-logs', label: 'Message Logs', icon: 'i-lucide-list-collapse' },
  { to: '/runtime-diagnostics', label: 'Diagnostics', icon: 'i-lucide-stethoscope' }
]

const summaryCards = computed(() => [
  {
    label: 'Version',
    value: rehearsal.value?.version || APP_VERSION,
    detail: rehearsal.value?.buildCode || APP_BUILD_CODE,
    icon: 'i-lucide-badge-check'
  },
  {
    label: 'Status',
    value: rehearsal.value?.overallStatus || 'Loading',
    detail: APP_RELEASE_NAME,
    icon: 'i-lucide-clipboard-check'
  },
  {
    label: 'Phases',
    value: String(rehearsal.value?.phaseCount || phases.value.length || 0),
    detail: 'live-data rehearsal',
    icon: 'i-lucide-route'
  },
  {
    label: 'Blocking Checks',
    value: String(rehearsal.value?.blockingCount || 0),
    detail: 'must close before Stage 11',
    icon: 'i-lucide-octagon-alert'
  }
])

async function refresh() {
  loading.value = true
  error.value = ''

  try {
    const [summaryResult, runSheetResult] = await Promise.allSettled([
      api.get<any>('stage10m/production-rehearsal'),
      api.get<any>('stage10m/production-rehearsal/run-sheet')
    ])

    const failures: string[] = []
    if (summaryResult.status === 'fulfilled') rehearsal.value = summaryResult.value
    else failures.push('rehearsal summary')

    if (runSheetResult.status === 'fulfilled') runSheet.value = runSheetResult.value
    else failures.push('run sheet')

    if (failures.length) {
      error.value = `${failures.join(' and ')} could not be loaded. Open Message Logs for stored backend details.`
    }
  } catch (err: any) {
    error.value = err?.data?.message || err?.message || 'Could not load production rehearsal.'
  } finally {
    loading.value = false
  }
}

onMounted(refresh)
</script>

<template>
  <div class="space-y-6">
    <div class="page-header">
      <div>
        <p class="page-kicker">{{ APP_STAGE }}</p>
        <h1 class="page-title">Production Rehearsal</h1>
        <p class="page-subtitle">
          Live-data run sheet for store opening, billing, print/QR, purchase, import/export, accounting, attendance, payroll and go/no-go evidence before Stage 11.
        </p>
      </div>
      <div class="flex flex-wrap gap-2">
        <UButton to="/stage10k-operator-acceptance" icon="i-lucide-clipboard-check" variant="subtle" label="Operator Acceptance" />
        <UButton to="/production-support" icon="i-lucide-life-buoy" variant="subtle" label="Support" />
        <UButton icon="i-lucide-refresh-cw" :loading="loading" @click="refresh">Refresh</UButton>
      </div>
    </div>

    <UAlert
      v-if="error"
      color="error"
      variant="soft"
      icon="i-lucide-triangle-alert"
      title="Production rehearsal could not be loaded"
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
            <h2 class="text-lg font-semibold">Rehearsal shortcuts</h2>
            <p class="text-sm text-muted">Open each module from the same production URL used by the operator.</p>
          </div>
          <UBadge color="primary" variant="soft">{{ rehearsal?.rehearsalMode || 'Live-data rehearsal' }}</UBadge>
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
      <div v-for="phase in phases" :key="phase.title" class="rounded-lg border border-default bg-default p-4">
        <div class="mb-4 flex flex-wrap items-start justify-between gap-3">
          <div class="min-w-0">
            <p class="font-semibold">{{ phase.title }}</p>
            <p class="text-sm text-muted">{{ phase.expectedEvidence }}</p>
          </div>
          <UButton :to="phase.route" icon="i-lucide-arrow-up-right" size="xs" variant="subtle">Open</UButton>
        </div>

        <div class="space-y-2">
          <div v-for="action in phase.actions" :key="action" class="flex gap-2 rounded-md border border-muted p-2 text-sm">
            <UIcon name="i-lucide-circle-check" class="mt-0.5 size-4 shrink-0 text-primary" />
            <span>{{ action }}</span>
          </div>
        </div>

        <div class="mt-4 space-y-2">
          <p class="text-xs uppercase text-muted">Blocking checks</p>
          <div v-for="check in phase.blockingChecks" :key="check" class="flex gap-2 rounded-md border border-warning/30 p-2 text-sm">
            <UIcon name="i-lucide-octagon-alert" class="mt-0.5 size-4 shrink-0 text-warning" />
            <span>{{ check }}</span>
          </div>
        </div>
      </div>
    </div>

    <UCard>
      <template #header>
        <h2 class="text-lg font-semibold">Issue buckets</h2>
      </template>

      <div class="grid gap-3 lg:grid-cols-2">
        <div v-for="bucket in issueBuckets" :key="bucket.title" class="rounded-lg border border-default p-3">
          <div class="mb-2 flex flex-wrap items-center justify-between gap-2">
            <p class="font-medium">{{ bucket.title }}</p>
            <UButton :to="bucket.route" icon="i-lucide-arrow-up-right" size="xs" variant="ghost">Open</UButton>
          </div>
          <p class="text-sm text-muted">{{ bucket.evidence }}</p>
        </div>
      </div>
    </UCard>

    <UCard>
      <template #header>
        <h2 class="text-lg font-semibold">Printable run sheet</h2>
      </template>

      <div class="grid gap-3 lg:grid-cols-2">
        <div v-for="item in runSheetItems" :key="`${item.phase}-${item.action}`" class="rounded-lg border border-default p-3">
          <div class="mb-1 flex flex-wrap items-center gap-2">
            <UBadge color="primary" variant="soft">{{ item.phase }}</UBadge>
            <UBadge color="neutral" variant="soft">{{ item.route }}</UBadge>
          </div>
          <p class="text-sm">{{ item.action }}</p>
          <p class="mt-1 text-xs text-muted">{{ item.expectedEvidence }}</p>
        </div>
      </div>
    </UCard>
  </div>
</template>
