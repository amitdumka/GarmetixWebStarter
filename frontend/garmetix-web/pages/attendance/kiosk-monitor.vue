<script setup lang="ts">
const devicesApi = useAttendanceDevices()
const feedback = useUiFeedback()
const loading = ref(false)
const photoProofs = ref<any[]>([])
const syncBatches = ref<any[]>([])
async function refresh() {
  loading.value = true
  try {
    const [proofRows, batchRows] = await Promise.all([devicesApi.photoProofs(), devicesApi.syncBatches()])
    photoProofs.value = proofRows
    syncBatches.value = batchRows
  } catch (error: any) {
    feedback.fromError('Kiosk monitor refresh failed', error)
  } finally {
    loading.value = false
  }
}
onMounted(refresh)
</script>

<template>
  <AppShell title="Kiosk Monitor" @refresh="refresh">
    <section class="space-y-5">
      <UiModulePageHeader title="Kiosk Monitor" description="Review Stage 9C photo-proof uploads, manager review status, and offline sync batches." icon="i-lucide-monitor-check">
        <template #actions>
          <UButton label="Refresh" :loading="loading" @click="refresh" />
          <UButton label="Open Web Kiosk" to="/attendance/kiosk" color="neutral" variant="subtle" />
          <UButton label="Face Photo Review" to="/attendance/photo-review" color="neutral" variant="subtle" />
        </template>
      </UiModulePageHeader>

      <div class="grid gap-3 md:grid-cols-3">
        <UCard><p class="text-sm text-gray-500">Photo proofs</p><p class="text-2xl font-semibold">{{ photoProofs.length }}</p></UCard>
        <UCard><p class="text-sm text-gray-500">Sync batches</p><p class="text-2xl font-semibold">{{ syncBatches.length }}</p></UCard>
        <UCard><p class="text-sm text-gray-500">Failed batches</p><p class="text-2xl font-semibold">{{ syncBatches.filter(row => row.failedCount > 0).length }}</p></UCard>
      </div>

      <UCard>
        <template #header>Recent Photo Proofs</template>
        <UTable :rows="photoProofs" :columns="[
          { key: 'capturedAtUtc', label: 'Captured' },
          { key: 'employeeId', label: 'Employee' },
          { key: 'deviceCode', label: 'Device' },
          { key: 'verificationStatus', label: 'Status' },
          { key: 'proofPath', label: 'Proof Path' }
        ]" />
      </UCard>

      <UCard>
        <template #header>Offline Sync Batches</template>
        <UTable :rows="syncBatches" :columns="[
          { key: 'receivedAtUtc', label: 'Received' },
          { key: 'deviceCode', label: 'Device' },
          { key: 'status', label: 'Status' },
          { key: 'totalCount', label: 'Total' },
          { key: 'acceptedCount', label: 'Accepted' },
          { key: 'duplicateCount', label: 'Duplicate' },
          { key: 'failedCount', label: 'Failed' }
        ]" />
      </UCard>
    </section>
  </AppShell>
</template>
