<script setup lang="ts">
const attendance = useAttendance()
const feedback = useUiFeedback()
const loading = ref(false)
const rehearsal = ref<any | null>(null)

const summaryCards = computed(() => [
  { label: 'Version', value: rehearsal.value?.version || '-', detail: rehearsal.value?.buildCode || '-', icon: 'i-lucide-badge-check' },
  { label: 'Phases', value: String(rehearsal.value?.phases?.length || 0), detail: 'tablet checks', icon: 'i-lucide-list-checks' },
  { label: 'Pass Rules', value: String(rehearsal.value?.passCriteria?.length || 0), detail: 'go/no-go items', icon: 'i-lucide-shield-check' },
  { label: 'Blockers', value: String(rehearsal.value?.blockers?.length || 0), detail: 'must close', icon: 'i-lucide-triangle-alert' }
])

async function refresh() {
  loading.value = true
  try {
    rehearsal.value = await attendance.mobileKioskRehearsal()
  } catch (error: any) {
    feedback.fromError('Tablet rehearsal failed', error)
  } finally {
    loading.value = false
  }
}

onMounted(refresh)
</script>

<template>
  <AppShell title="Mobile Kiosk Rehearsal" @refresh="refresh">
    <section class="space-y-5">
      <UiModulePageHeader
        title="Mobile Kiosk Rehearsal"
        description="Physical Android tablet rehearsal checklist before fingerprint hardware work starts."
        icon="i-lucide-tablet-smartphone"
        :loading="loading"
      >
        <template #actions>
          <UButton to="/attendance/mobile-kiosk" icon="i-lucide-smartphone" label="Mobile Kiosk" color="neutral" variant="subtle" />
          <UButton to="/attendance/devices" icon="i-lucide-key-round" label="Kiosk Devices" color="neutral" variant="subtle" />
          <UButton to="/attendance/kiosk-monitor" icon="i-lucide-monitor-check" label="Kiosk Monitor" color="neutral" variant="subtle" />
          <UButton icon="i-lucide-refresh-cw" :loading="loading" label="Refresh" @click="refresh" />
        </template>
      </UiModulePageHeader>

      <UAlert
        color="primary"
        variant="soft"
        icon="i-lucide-tablet-smartphone"
        :title="rehearsal?.title || 'Physical Android tablet rehearsal'"
        :description="rehearsal?.goal || 'Confirm readiness, online punch, offline queue, sync and audit before device hardware work.'"
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

      <UCard>
        <template #header>
          <h2 class="text-lg font-semibold">Prerequisites</h2>
        </template>
        <div class="grid gap-2 md:grid-cols-2">
          <div v-for="item in rehearsal?.prerequisites || []" :key="item" class="flex gap-2 rounded-lg border border-default p-3 text-sm">
            <UIcon name="i-lucide-circle-check" class="mt-0.5 size-4 shrink-0 text-primary" />
            <span>{{ item }}</span>
          </div>
        </div>
      </UCard>

      <div class="space-y-4">
        <UCard v-for="phase in rehearsal?.phases || []" :key="phase.name">
          <template #header>
            <h2 class="text-lg font-semibold">{{ phase.name }}</h2>
          </template>
          <div class="grid gap-4 xl:grid-cols-2">
            <div>
              <p class="mb-2 text-xs uppercase text-muted">Checks</p>
              <div class="space-y-2">
                <div v-for="item in phase.checks || []" :key="item" class="flex gap-2 rounded-lg border border-default p-3 text-sm">
                  <UIcon name="i-lucide-check" class="mt-0.5 size-4 shrink-0 text-success" />
                  <span>{{ item }}</span>
                </div>
              </div>
            </div>
            <div>
              <p class="mb-2 text-xs uppercase text-muted">Evidence</p>
              <div class="space-y-2">
                <div v-for="item in phase.evidence || []" :key="item" class="flex gap-2 rounded-lg border border-default p-3 text-sm">
                  <UIcon name="i-lucide-camera" class="mt-0.5 size-4 shrink-0 text-info" />
                  <span>{{ item }}</span>
                </div>
              </div>
            </div>
          </div>
        </UCard>
      </div>

      <div class="grid gap-4 xl:grid-cols-3">
        <UCard>
          <template #header>
            <h2 class="text-lg font-semibold">Pass criteria</h2>
          </template>
          <div class="space-y-2">
            <div v-for="item in rehearsal?.passCriteria || []" :key="item" class="flex gap-2 rounded-lg border border-success/40 p-3 text-sm">
              <UIcon name="i-lucide-shield-check" class="mt-0.5 size-4 shrink-0 text-success" />
              <span>{{ item }}</span>
            </div>
          </div>
        </UCard>

        <UCard>
          <template #header>
            <h2 class="text-lg font-semibold">Blockers</h2>
          </template>
          <div class="space-y-2">
            <div v-for="item in rehearsal?.blockers || []" :key="item" class="flex gap-2 rounded-lg border border-error/40 p-3 text-sm">
              <UIcon name="i-lucide-triangle-alert" class="mt-0.5 size-4 shrink-0 text-error" />
              <span>{{ item }}</span>
            </div>
          </div>
        </UCard>

        <UCard>
          <template #header>
            <h2 class="text-lg font-semibold">Next after pass</h2>
          </template>
          <div class="space-y-2">
            <div v-for="item in rehearsal?.nextAfterPass || []" :key="item" class="flex gap-2 rounded-lg border border-default p-3 text-sm">
              <UIcon name="i-lucide-arrow-right" class="mt-0.5 size-4 shrink-0 text-primary" />
              <span>{{ item }}</span>
            </div>
          </div>
        </UCard>
      </div>
    </section>
  </AppShell>
</template>
