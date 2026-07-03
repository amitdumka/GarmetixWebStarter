<script setup lang="ts">
const devicesApi = useAttendanceDevices()
const feedback = useUiFeedback()
const loading = ref(false)
const savingId = ref<string | null>(null)
const status = ref('PendingReview')
const proofs = ref<any[]>([])
const summary = ref<any | null>(null)
const remarks = reactive<Record<string, string>>({})

const statusOptions = ['PendingReview', 'Approved', 'Rejected', 'Flagged', 'NeedsRegularization', 'All']

async function refresh() {
  loading.value = true
  try {
    const [proofRows, reviewSummary] = await Promise.all([
      devicesApi.photoProofs(status.value === 'All' ? undefined : status.value),
      devicesApi.photoProofReviewSummary()
    ])
    proofs.value = proofRows
    summary.value = reviewSummary
  } catch (error: any) {
    feedback.fromError('Photo proof review refresh failed', error)
  } finally {
    loading.value = false
  }
}

async function review(row: any, decision: string, createRegularizationRequest = false) {
  savingId.value = row.id
  try {
    await devicesApi.reviewPhotoProof(row.id, {
      decision,
      reason: decision,
      remarks: remarks[row.id] || `${decision} from Stage 9C photo review.`,
      createRegularizationRequest
    })
    feedback.success('Photo proof reviewed', `${decision} saved successfully.`)
    await refresh()
  } catch (error: any) {
    feedback.fromError('Could not review photo proof', error)
  } finally {
    savingId.value = null
  }
}

async function createRegularization(row: any) {
  savingId.value = row.id
  try {
    await devicesApi.createPhotoProofRegularization(row.id, remarks[row.id] || 'Created from Stage 9C photo proof review.')
    feedback.success('Regularization request created', 'Manager can now approve/correct it from Regularization Requests.')
    await refresh()
  } catch (error: any) {
    feedback.fromError('Could not create regularization request', error)
  } finally {
    savingId.value = null
  }
}

onMounted(refresh)
</script>

<template>
  <AppShell title="Face Photo Review" @refresh="refresh">
    <section class="space-y-5">
      <UiModulePageHeader title="Face Photo Review" description="Stage 9C manager review for kiosk photo proofs. This is manual proof review only, not AI face recognition." icon="i-lucide-user-check">
        <template #actions>
          <USelect v-model="status" :items="statusOptions" class="w-56" @change="refresh" />
          <UButton label="Refresh" :loading="loading" @click="refresh" />
          <UButton label="Regularization" to="/attendance/regularization" color="neutral" variant="subtle" />
        </template>
      </UiModulePageHeader>

      <div class="grid gap-3 md:grid-cols-6">
        <UCard><p class="text-xs text-muted">Pending</p><strong>{{ summary?.pending || 0 }}</strong></UCard>
        <UCard><p class="text-xs text-muted">Approved</p><strong>{{ summary?.approved || 0 }}</strong></UCard>
        <UCard><p class="text-xs text-muted">Rejected</p><strong>{{ summary?.rejected || 0 }}</strong></UCard>
        <UCard><p class="text-xs text-muted">Flagged</p><strong>{{ summary?.flagged || 0 }}</strong></UCard>
        <UCard><p class="text-xs text-muted">Needs Reg.</p><strong>{{ summary?.needsRegularization || 0 }}</strong></UCard>
        <UCard><p class="text-xs text-muted">Expiring 30d</p><strong>{{ summary?.expiringSoon || 0 }}</strong></UCard>
      </div>

      <UAlert color="warning" title="Privacy note" description="Photo proof is attendance evidence for manager review only. No face matching, liveness detection, or fingerprint matching is performed in Stage 9C." />

      <div class="grid gap-4 xl:grid-cols-2">
        <UCard v-for="row in proofs" :key="row.id">
          <template #header>
            <div class="flex items-center justify-between gap-3">
              <div>
                <p class="font-semibold">{{ row.employeeId }}</p>
                <p class="text-xs text-muted">{{ row.deviceCode || 'Device' }} · {{ row.clientPunchId || 'No client punch id' }}</p>
              </div>
              <UBadge :color="row.reviewStatus === 'Approved' ? 'success' : row.reviewStatus === 'Rejected' ? 'error' : row.reviewStatus === 'Flagged' ? 'warning' : 'neutral'">{{ row.reviewStatus || row.verificationStatus }}</UBadge>
            </div>
          </template>

          <div class="space-y-3">
            <div class="grid gap-2 text-sm md:grid-cols-2">
              <div><span class="text-muted">Captured:</span> {{ row.capturedAtUtc }}</div>
              <div><span class="text-muted">Uploaded:</span> {{ row.uploadedAtUtc }}</div>
              <div><span class="text-muted">Retention:</span> {{ row.retentionUntilUtc || 'Not set' }}</div>
              <div><span class="text-muted">Size:</span> {{ row.sizeBytes }} bytes</div>
              <div class="md:col-span-2"><span class="text-muted">Proof path:</span> {{ row.proofPath }}</div>
              <div v-if="row.reviewedBy"><span class="text-muted">Reviewed by:</span> {{ row.reviewedBy }}</div>
              <div v-if="row.reviewedAtUtc"><span class="text-muted">Reviewed at:</span> {{ row.reviewedAtUtc }}</div>
            </div>

            <UTextarea v-model="remarks[row.id]" placeholder="Manager review remarks / mismatch reason / correction note" />

            <div class="flex flex-wrap gap-2">
              <UButton size="sm" color="success" label="Approve" :loading="savingId === row.id" @click="review(row, 'Approved')" />
              <UButton size="sm" color="error" label="Reject" :loading="savingId === row.id" @click="review(row, 'Rejected')" />
              <UButton size="sm" color="warning" label="Flag" :loading="savingId === row.id" @click="review(row, 'Flagged')" />
              <UButton size="sm" color="primary" variant="subtle" label="Needs Regularization" :loading="savingId === row.id" @click="review(row, 'NeedsRegularization', true)" />
              <UButton size="sm" color="neutral" variant="ghost" label="Create Regularization" :loading="savingId === row.id" @click="createRegularization(row)" />
            </div>
          </div>
        </UCard>
      </div>

      <UAlert v-if="!loading && !proofs.length" color="neutral" title="No photo proofs found" description="Use the web kiosk to capture attendance photo proofs, then return here for manager review." />
    </section>
  </AppShell>
</template>
