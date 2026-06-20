<script setup lang="ts">
const reports = useAttendanceReports()
const feedback = useUiFeedback()
const loading = ref(false)
const status = ref<any | null>(null)

async function refresh() {
  loading.value = true
  try {
    status.value = await reports.deviceBridgeStatus()
  } catch (error: any) {
    feedback.fromError('Device bridge status failed', error)
  } finally {
    loading.value = false
  }
}

onMounted(refresh)
</script>

<template>
  <AppShell title="Attendance Device Bridge" @refresh="refresh">
    <section class="space-y-5">
      <UiModulePageHeader
        title="Fingerprint Device Bridge Planning"
        description="Stage 9I keeps the biometric bridge integration-ready without storing raw fingerprint images or binding to any vendor SDK yet."
        icon="i-lucide-cable"
        :loading="loading"
      />

      <UAlert
        color="info"
        icon="i-lucide-fingerprint"
        title="Bridge placeholder only"
        description="Device registration, token validation, sync APIs and biometric enrollment references are ready. Real fingerprint matching must wait until hardware/vendor SDK is selected."
      />

      <div class="grid gap-3 md:grid-cols-3">
        <UCard><p class="text-xs text-muted">Bridge Status</p><strong>{{ status?.status || 'Loading' }}</strong></UCard>
        <UCard><p class="text-xs text-muted">Fingerprint Bridge Enabled</p><strong>{{ status?.fingerprintBridgeEnabled ? 'Yes' : 'No' }}</strong></UCard>
        <UCard><p class="text-xs text-muted">Raw Fingerprint Storage</p><strong>{{ status?.rawFingerprintStorageAllowed ? 'Allowed' : 'Blocked' }}</strong></UCard>
      </div>

      <div class="grid gap-4 md:grid-cols-2">
        <UCard>
          <h3 class="mb-3 font-semibold">Available bridge foundation</h3>
          <ul class="space-y-2 text-sm">
            <li v-for="item in status?.supportedBridgeInputs || []" :key="item" class="flex gap-2"><UIcon name="i-lucide-check" class="mt-0.5" />{{ item }}</li>
          </ul>
        </UCard>
        <UCard>
          <h3 class="mb-3 font-semibold">Later implementation list</h3>
          <ul class="space-y-2 text-sm">
            <li v-for="item in status?.laterImplementationItems || []" :key="item" class="flex gap-2"><UIcon name="i-lucide-clock" class="mt-0.5" />{{ item }}</li>
          </ul>
        </UCard>
      </div>
    </section>
  </AppShell>
</template>
