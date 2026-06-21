<script setup lang="ts">
const reports = useAttendanceReports()
const feedback = useUiFeedback()
const loading = ref(false)
const status = ref<any | null>(null)

const summaryCards = computed(() => [
  { label: 'Status', value: status.value?.status || 'Loading', detail: status.value?.buildCode || '-', icon: 'i-lucide-fingerprint' },
  { label: 'Bridge Enabled', value: status.value?.fingerprintBridgeEnabled ? 'Yes' : 'No', detail: 'requires hardware approval', icon: 'i-lucide-power' },
  { label: 'Raw Storage', value: status.value?.rawFingerprintStorageAllowed ? 'Allowed' : 'Blocked', detail: 'privacy guard', icon: 'i-lucide-shield-alert' },
  { label: 'Adapters', value: String(status.value?.adapterCandidates?.length || 0), detail: 'candidate devices', icon: 'i-lucide-usb' }
])

async function refresh() {
  loading.value = true
  try {
    status.value = await reports.deviceBridgeStatus()
  } catch (error: any) {
    feedback.fromError('Fingerprint bridge status failed', error)
  } finally {
    loading.value = false
  }
}

onMounted(refresh)
</script>

<template>
  <AppShell title="Fingerprint Bridge" @refresh="refresh">
    <section class="space-y-5">
      <UiModulePageHeader
        title="Fingerprint Bridge"
        description="Stage 11B defines the vendor-neutral fingerprint bridge contract before any scanner SDK is connected."
        icon="i-lucide-fingerprint"
        :loading="loading"
      >
        <template #actions>
          <UButton to="/attendance/biometric-enrollment" icon="i-lucide-user-check" label="Enrollment" color="neutral" variant="subtle" />
          <UButton to="/attendance/mobile-kiosk" icon="i-lucide-smartphone" label="Mobile Kiosk" color="neutral" variant="subtle" />
          <UButton icon="i-lucide-refresh-cw" :loading="loading" label="Refresh" @click="refresh" />
        </template>
      </UiModulePageHeader>

      <UAlert
        color="warning"
        variant="soft"
        icon="i-lucide-shield-alert"
        :title="status?.title || 'Vendor-neutral fingerprint bridge contract'"
        :description="status?.matchingLocation || 'Garmetix stores consent and template references only. Raw fingerprint payloads remain blocked.'"
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
            <h2 class="text-lg font-semibold">Bridge foundation</h2>
          </template>
          <div class="space-y-2">
            <div v-for="item in status?.supportedBridgeInputs || []" :key="item" class="flex gap-2 rounded-lg border border-default p-3 text-sm">
              <UIcon name="i-lucide-check" class="mt-0.5 size-4 shrink-0 text-success" />
              <span>{{ item }}</span>
            </div>
          </div>
        </UCard>

        <UCard>
          <template #header>
            <h2 class="text-lg font-semibold">Local bridge contract</h2>
          </template>
          <div class="space-y-3">
            <div class="rounded-lg border border-default p-3">
              <p class="text-xs uppercase text-muted">Base URL</p>
              <code class="mt-1 block break-all text-xs">{{ status?.bridgeContract?.localBridgeBaseUrl }}</code>
            </div>
            <div class="grid gap-2 sm:grid-cols-2">
              <div v-for="key in ['health', 'capture', 'identify', 'enroll']" :key="key" class="rounded-lg border border-default p-3">
                <p class="text-xs uppercase text-muted">{{ key }}</p>
                <code class="mt-1 block text-xs">{{ status?.bridgeContract?.[key] }}</code>
              </div>
            </div>
          </div>
        </UCard>
      </div>

      <UCard>
        <template #header>
          <h2 class="text-lg font-semibold">Adapter candidates</h2>
        </template>
        <div class="grid gap-3 xl:grid-cols-4">
          <div v-for="item in status?.adapterCandidates || []" :key="item.name" class="rounded-lg border border-default p-3">
            <div class="flex items-start justify-between gap-3">
              <div class="min-w-0">
                <p class="font-medium">{{ item.name }}</p>
                <p class="mt-1 text-xs text-muted">{{ item.platform }}</p>
              </div>
              <UBadge color="primary" variant="soft">{{ item.decisionStatus }}</UBadge>
            </div>
            <p class="mt-3 text-sm text-muted">{{ item.fit }}</p>
          </div>
        </div>
      </UCard>

      <div class="grid gap-4 xl:grid-cols-2">
        <UCard>
          <template #header>
            <h2 class="text-lg font-semibold">Privacy rules</h2>
          </template>
          <div class="space-y-2">
            <div v-for="item in status?.privacyRules || []" :key="item" class="flex gap-2 rounded-lg border border-warning/40 p-3 text-sm">
              <UIcon name="i-lucide-shield-check" class="mt-0.5 size-4 shrink-0 text-warning" />
              <span>{{ item }}</span>
            </div>
          </div>
        </UCard>

        <UCard>
          <template #header>
            <h2 class="text-lg font-semibold">Implementation checklist</h2>
          </template>
          <div class="space-y-2">
            <div v-for="item in status?.implementationChecklist || []" :key="item" class="flex gap-2 rounded-lg border border-default p-3 text-sm">
              <UIcon name="i-lucide-list-checks" class="mt-0.5 size-4 shrink-0 text-primary" />
              <span>{{ item }}</span>
            </div>
          </div>
        </UCard>
      </div>

      <div class="grid gap-4 xl:grid-cols-3">
        <UCard>
          <template #header>
            <h2 class="text-lg font-semibold">Rehearsal steps</h2>
          </template>
          <div class="space-y-2">
            <div v-for="item in status?.rehearsalSteps || []" :key="item" class="flex gap-2 rounded-lg border border-default p-3 text-sm">
              <UIcon name="i-lucide-play-circle" class="mt-0.5 size-4 shrink-0 text-info" />
              <span>{{ item }}</span>
            </div>
          </div>
        </UCard>

        <UCard>
          <template #header>
            <h2 class="text-lg font-semibold">Blockers</h2>
          </template>
          <div class="space-y-2">
            <div v-for="item in status?.blockers || []" :key="item" class="flex gap-2 rounded-lg border border-error/40 p-3 text-sm">
              <UIcon name="i-lucide-triangle-alert" class="mt-0.5 size-4 shrink-0 text-error" />
              <span>{{ item }}</span>
            </div>
          </div>
        </UCard>

        <UCard>
          <template #header>
            <h2 class="text-lg font-semibold">Next after this part</h2>
          </template>
          <div class="space-y-2">
            <div v-for="item in status?.nextAfterThisPart || []" :key="item" class="flex gap-2 rounded-lg border border-default p-3 text-sm">
              <UIcon name="i-lucide-arrow-right" class="mt-0.5 size-4 shrink-0 text-primary" />
              <span>{{ item }}</span>
            </div>
          </div>
        </UCard>
      </div>
    </section>
  </AppShell>
</template>
