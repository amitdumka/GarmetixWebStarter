<script setup lang="ts">
const attendance = useAttendance()
const feedback = useUiFeedback()
const loading = ref(false)
const status = ref<any | null>(null)
const contract = ref<any | null>(null)

const summaryCards = computed(() => [
  { label: 'Status', value: status.value?.status || 'Loading', detail: status.value?.target || 'net10.0-android', icon: 'i-lucide-smartphone' },
  { label: 'Queue', value: status.value?.queueProvider || 'SQLite', detail: contract.value?.sqliteTable || 'pending_punches', icon: 'i-lucide-database' },
  { label: 'API Routes', value: String(status.value?.routes?.length || 0), detail: 'kiosk endpoints', icon: 'i-lucide-route' },
  { label: 'Build', value: status.value?.version || '-', detail: status.value?.buildCode || '-', icon: 'i-lucide-badge-check' }
])

async function refresh() {
  loading.value = true
  try {
    const [statusResult, contractResult] = await Promise.allSettled([
      attendance.mobileKioskStatus(),
      attendance.mobileKioskOfflineContract()
    ])
    const failures: string[] = []
    if (statusResult.status === 'fulfilled') status.value = statusResult.value
    else failures.push('mobile kiosk status')
    if (contractResult.status === 'fulfilled') contract.value = contractResult.value
    else failures.push('offline contract')
    if (failures.length) feedback.error('Mobile kiosk status incomplete', `${failures.join(' and ')} could not load. Check Message Logs for details.`)
  } catch (error: any) {
    feedback.fromError('Mobile kiosk status failed', error)
  } finally {
    loading.value = false
  }
}

onMounted(refresh)
</script>

<template>
  <AppShell title="Mobile Attendance Kiosk" @refresh="refresh">
    <section class="space-y-5">
      <UiModulePageHeader
        title="Mobile Attendance Kiosk"
        description="Stage 11A starts the native MAUI Android kiosk shell with local SQLite offline queue and the existing attendance kiosk API contract."
        icon="i-lucide-smartphone"
        :loading="loading"
      >
        <template #actions>
          <UButton to="/attendance/devices" icon="i-lucide-tablet-smartphone" label="Kiosk Devices" color="neutral" variant="subtle" />
          <UButton to="/attendance/kiosk-monitor" icon="i-lucide-monitor-check" label="Kiosk Monitor" color="neutral" variant="subtle" />
          <UButton icon="i-lucide-refresh-cw" :loading="loading" label="Refresh" @click="refresh" />
        </template>
      </UiModulePageHeader>

      <UAlert
        color="primary"
        variant="soft"
        icon="i-lucide-smartphone"
        title="Stage 11A shell ready"
        :description="`${status?.projectPath || 'apps/Garmetix.AttendanceKiosk'} uses ${status?.queueProvider || 'SQLite local queue'} and the existing kiosk sync API.`"
      />

      <div class="grid gap-3 md:grid-cols-4">
        <UCard v-for="card in summaryCards" :key="card.label">
          <div class="flex items-start justify-between gap-3">
            <div class="min-w-0">
              <p class="text-xs uppercase text-muted">{{ card.label }}</p>
              <p class="mt-1 truncate text-lg font-semibold">{{ card.value }}</p>
              <p class="truncate text-xs text-muted">{{ card.detail }}</p>
            </div>
            <UIcon :name="card.icon" class="size-5 shrink-0 text-muted" />
          </div>
        </UCard>
      </div>

      <div class="grid gap-4 xl:grid-cols-2">
        <UCard>
          <template #header>
            <h2 class="text-lg font-semibold">Native shell files</h2>
          </template>
          <div class="space-y-2">
            <div v-for="file in status?.shellFiles || []" :key="file" class="rounded-lg border border-default p-3">
              <code class="text-xs">{{ file }}</code>
            </div>
          </div>
        </UCard>

        <UCard>
          <template #header>
            <h2 class="text-lg font-semibold">Kiosk API routes</h2>
          </template>
          <div class="space-y-2">
            <div v-for="route in status?.routes || []" :key="route" class="rounded-lg border border-default p-3">
              <code class="text-xs">{{ route }}</code>
            </div>
          </div>
        </UCard>
      </div>

      <div class="grid gap-4 xl:grid-cols-2">
        <UCard>
          <template #header>
            <h2 class="text-lg font-semibold">SQLite offline queue</h2>
          </template>
          <div class="space-y-2">
            <div v-for="column in contract?.columns || []" :key="column" class="rounded-lg border border-default p-3">
              <code class="text-xs">{{ column }}</code>
            </div>
          </div>
        </UCard>

        <UCard>
          <template #header>
            <h2 class="text-lg font-semibold">Acceptance checks</h2>
          </template>
          <div class="space-y-2">
            <div v-for="item in status?.acceptanceChecks || []" :key="item" class="flex gap-2 rounded-lg border border-default p-3 text-sm">
              <UIcon name="i-lucide-circle-check" class="mt-0.5 size-4 shrink-0 text-primary" />
              <span>{{ item }}</span>
            </div>
          </div>
        </UCard>
      </div>

      <UCard>
        <template #header>
          <h2 class="text-lg font-semibold">Safety rules</h2>
        </template>
        <div class="grid gap-2 md:grid-cols-2">
          <div v-for="item in status?.safetyRules || []" :key="item" class="flex gap-2 rounded-lg border border-default p-3 text-sm">
            <UIcon name="i-lucide-shield-check" class="mt-0.5 size-4 shrink-0 text-success" />
            <span>{{ item }}</span>
          </div>
        </div>
      </UCard>
    </section>
  </AppShell>
</template>
