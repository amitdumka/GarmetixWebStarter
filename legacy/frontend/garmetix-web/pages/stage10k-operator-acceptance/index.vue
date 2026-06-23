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
const summary = ref<any>(null)
const checklist = ref<any>(null)

const sections = computed(() => summary.value?.sections || [])
const checklistItems = computed(() => checklist.value?.items || [])
const dailyRoutes = computed(() => summary.value?.dailyRoutes || [])

const summaryCards = computed(() => [
  {
    label: 'Version',
    value: summary.value?.version || APP_VERSION,
    detail: summary.value?.buildCode || APP_BUILD_CODE,
    icon: 'i-lucide-badge-check'
  },
  {
    label: 'Status',
    value: summary.value?.overallStatus || 'Loading',
    detail: APP_RELEASE_NAME,
    icon: 'i-lucide-clipboard-check'
  },
  {
    label: 'Sections',
    value: String(summary.value?.sectionCount || sections.value.length || 0),
    detail: 'operator flows',
    icon: 'i-lucide-list-checks'
  },
  {
    label: 'Checklist',
    value: String(summary.value?.requiredItemCount || checklistItems.value.length || 0),
    detail: 'required review items',
    icon: 'i-lucide-check-check'
  }
])

const quickLinks = [
  { to: '/store-day', label: 'Store Operations', icon: 'i-lucide-store' },
  { to: '/billing/new', label: 'Sale Invoice', icon: 'i-lucide-receipt-text' },
  { to: '/petty-cash', label: 'Petty Cash', icon: 'i-lucide-wallet' },
  { to: '/vouchers', label: 'Vouchers', icon: 'i-lucide-file-check-2' },
  { to: '/purchase/new', label: 'Purchase', icon: 'i-lucide-package-plus' },
  { to: '/stock-reports', label: 'Stock Reports', icon: 'i-lucide-warehouse' },
  { to: '/attendance/today', label: 'Attendance', icon: 'i-lucide-calendar-check' },
  { to: '/payroll', label: 'Payroll', icon: 'i-lucide-badge-indian-rupee' },
  { to: '/backup-maintenance', label: 'Backup', icon: 'i-lucide-hard-drive-download' },
  { to: '/message-logs', label: 'Message Logs', icon: 'i-lucide-list-collapse' },
  { to: '/document-scan', label: 'Document Scanner', icon: 'i-lucide-scan-qr-code' },
  { to: '/system-health', label: 'System Health', icon: 'i-lucide-activity' }
]

async function refresh() {
  loading.value = true
  error.value = ''

  try {
    const [summaryResult, checklistResult] = await Promise.allSettled([
      api.get<any>('stage10k/operator-acceptance'),
      api.get<any>('stage10k/operator-acceptance/checklist')
    ])

    const failures: string[] = []
    if (summaryResult.status === 'fulfilled') summary.value = summaryResult.value
    else failures.push('summary')

    if (checklistResult.status === 'fulfilled') checklist.value = checklistResult.value
    else failures.push('checklist')

    if (failures.length) {
      error.value = `Operator acceptance ${failures.join(' and ')} could not be loaded. Check Message Logs for the stored error details.`
    }
  } catch (err: any) {
    error.value = err?.data?.message || err?.message || 'Could not load operator acceptance.'
  } finally {
    loading.value = false
  }
}

function badgeColor(status?: string) {
  const value = String(status || '').toLowerCase()
  if (value.includes('ready') || value.includes('healthy')) return 'success'
  if (value.includes('degraded') || value.includes('attention')) return 'warning'
  if (value.includes('blocked') || value.includes('error')) return 'error'
  return 'neutral'
}

onMounted(refresh)
</script>

<template>
  <div class="space-y-6">
    <div class="page-header">
      <div>
        <p class="page-kicker">{{ APP_STAGE }}</p>
        <h1 class="page-title">Production Operator Acceptance</h1>
        <p class="page-subtitle">
          Daily rehearsal checklist for store opening, billing, cash closing, purchase, accounting, attendance, payroll, backup and support evidence.
        </p>
      </div>
      <div class="flex flex-wrap gap-2">
        <UButton to="/production-final-acceptance" icon="i-lucide-shield-check" variant="subtle" label="Final Acceptance" />
        <UButton icon="i-lucide-refresh-cw" :loading="loading" @click="refresh">Refresh</UButton>
      </div>
    </div>

    <UAlert
      v-if="error"
      color="error"
      variant="soft"
      icon="i-lucide-triangle-alert"
      title="Operator acceptance could not be loaded"
      :description="error"
    />

    <UAlert
      v-else
      :color="badgeColor(summary?.overallStatus)"
      variant="soft"
      icon="i-lucide-clipboard-check"
      :title="summary?.overallStatus || 'Loading operator acceptance'"
      :description="`${APP_VERSION} / ${APP_BUILD_CODE}`"
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
            <h2 class="text-lg font-semibold">Operator flow links</h2>
            <p class="text-sm text-muted">Use these while rehearsing a full store day on the production host.</p>
          </div>
          <UBadge color="primary" variant="soft">{{ dailyRoutes.length }} API routes tracked</UBadge>
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
      <div v-for="section in sections" :key="section.title" class="rounded-lg border border-default bg-default p-4">
        <div class="mb-4 flex flex-wrap items-start justify-between gap-3">
          <div class="min-w-0">
            <p class="font-semibold">{{ section.title }}</p>
            <p class="text-xs text-muted">{{ section.cadence }} · {{ section.route }}</p>
          </div>
          <UButton :to="section.route" icon="i-lucide-arrow-up-right" size="xs" variant="subtle">Open</UButton>
        </div>

        <div class="space-y-2">
          <div v-for="item in section.items" :key="item" class="flex gap-2 rounded-md border border-muted p-2 text-sm">
            <UIcon name="i-lucide-check-circle-2" class="mt-0.5 size-4 shrink-0 text-primary" />
            <span>{{ item }}</span>
          </div>
        </div>

        <div class="mt-4 flex flex-wrap gap-2">
          <UBadge v-for="evidence in section.evidence" :key="evidence" color="neutral" variant="soft">
            {{ evidence }}
          </UBadge>
        </div>
      </div>
    </div>

    <UCard>
      <template #header>
        <h2 class="text-lg font-semibold">Manual checklist</h2>
      </template>

      <div class="grid gap-3 lg:grid-cols-2">
        <div v-for="item in checklistItems" :key="`${item.section}-${item.item}`" class="rounded-lg border border-default p-3">
          <div class="mb-1 flex flex-wrap items-center gap-2">
            <UBadge color="primary" variant="soft">{{ item.section }}</UBadge>
            <UBadge color="neutral" variant="soft">{{ item.cadence }}</UBadge>
          </div>
          <p class="text-sm">{{ item.item }}</p>
        </div>
      </div>
    </UCard>
  </div>
</template>
