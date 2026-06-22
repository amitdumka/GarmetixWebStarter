<script setup lang="ts">
const reports = useAttendanceReports()
const feedback = useUiFeedback()
const loading = ref(false)
const status = ref<any | null>(null)

const summaryCards = computed(() => [
  { label: 'Stage', value: status.value?.status || 'Loading', detail: status.value?.buildCode || '-', icon: 'i-lucide-scan-face' },
  { label: 'Face Match', value: status.value?.realFaceRecognitionEnabled ? 'Enabled' : 'Blocked', detail: 'requires approval', icon: 'i-lucide-shield-alert' },
  { label: 'Raw Storage', value: status.value?.rawFaceTemplateStorageAllowed ? 'Allowed' : 'Blocked', detail: 'privacy guard', icon: 'i-lucide-lock' },
  { label: 'Providers', value: String(status.value?.providerCandidates?.length || 0), detail: 'candidate paths', icon: 'i-lucide-plug' }
])

async function refresh() {
  loading.value = true
  try {
    status.value = await reports.faceLivenessStatus()
  } catch (error: any) {
    feedback.fromError('Face liveness readiness failed', error)
  } finally {
    loading.value = false
  }
}

onMounted(refresh)
</script>

<template>
  <AppShell title="Face Liveness Readiness" @refresh="refresh">
    <section class="space-y-5">
      <UiModulePageHeader
        title="Face Liveness Readiness"
        description="Stage 11C privacy-safe readiness contract before any automated face matching or liveness SDK is connected."
        icon="i-lucide-scan-face"
        :loading="loading"
      >
        <template #actions>
          <UButton to="/attendance/photo-review" icon="i-lucide-user-check" label="Photo Review" color="neutral" variant="subtle" />
          <UButton to="/attendance/biometric-enrollment" icon="i-lucide-fingerprint" label="Enrollment" color="neutral" variant="subtle" />
          <UButton icon="i-lucide-refresh-cw" :loading="loading" label="Refresh" @click="refresh" />
        </template>
      </UiModulePageHeader>

      <UAlert
        color="warning"
        variant="soft"
        icon="i-lucide-shield-alert"
        :title="status?.title || 'Face liveness readiness contract'"
        :description="status?.message || 'Automated face matching is not enabled. This page defines the contract and blockers before provider work.'"
      />

      <div class="grid gap-3 md:grid-cols-4">
        <UCard v-for="card in summaryCards" :key="card.label">
          <div class="flex items-start justify-between gap-3">
            <div class="min-w-0">
              <p class="text-xs font-semibold uppercase tracking-wide text-muted">{{ card.label }}</p>
              <p class="mt-1 truncate text-lg font-semibold text-highlighted">{{ card.value }}</p>
              <p class="mt-1 truncate text-xs text-muted">{{ card.detail }}</p>
            </div>
            <UIcon :name="card.icon" class="size-5 shrink-0 text-primary" />
          </div>
        </UCard>
      </div>

      <div class="grid gap-4 xl:grid-cols-[1.05fr_0.95fr]">
        <UCard>
          <template #header>
            <div class="flex items-center gap-2">
              <UIcon name="i-lucide-badge-check" class="size-5 text-primary" />
              <h2 class="text-base font-semibold text-highlighted">Current Safe Base</h2>
            </div>
          </template>
          <div class="grid gap-2 sm:grid-cols-2">
            <div v-for="item in status?.currentSafeBase || []" :key="item" class="rounded-md border border-default bg-muted/30 p-3 text-sm text-toned">
              {{ item }}
            </div>
          </div>
        </UCard>

        <UCard>
          <template #header>
            <div class="flex items-center gap-2">
              <UIcon name="i-lucide-file-json" class="size-5 text-primary" />
              <h2 class="text-base font-semibold text-highlighted">Response Contract</h2>
            </div>
          </template>
          <div class="flex flex-wrap gap-2">
            <UBadge v-for="item in status?.expectedResponseContract || []" :key="item" color="primary" variant="soft">
              {{ item }}
            </UBadge>
          </div>
        </UCard>
      </div>

      <div class="grid gap-4 lg:grid-cols-3">
        <UCard>
          <template #header>
            <div class="flex items-center gap-2">
              <UIcon name="i-lucide-ban" class="size-5 text-error" />
              <h2 class="text-base font-semibold text-highlighted">Blocked Fields</h2>
            </div>
          </template>
          <div class="flex flex-wrap gap-2">
            <UBadge v-for="item in status?.blockedResponseFields || []" :key="item" color="error" variant="soft">
              {{ item }}
            </UBadge>
          </div>
        </UCard>

        <UCard>
          <template #header>
            <div class="flex items-center gap-2">
              <UIcon name="i-lucide-plug" class="size-5 text-primary" />
              <h2 class="text-base font-semibold text-highlighted">Provider Candidates</h2>
            </div>
          </template>
          <ul class="space-y-2 text-sm text-toned">
            <li v-for="item in status?.providerCandidates || []" :key="item" class="flex gap-2">
              <UIcon name="i-lucide-dot" class="mt-0.5 size-4 shrink-0 text-primary" />
              <span>{{ item }}</span>
            </li>
          </ul>
        </UCard>

        <UCard>
          <template #header>
            <div class="flex items-center gap-2">
              <UIcon name="i-lucide-clipboard-check" class="size-5 text-primary" />
              <h2 class="text-base font-semibold text-highlighted">Approved Inputs</h2>
            </div>
          </template>
          <div class="flex flex-wrap gap-2">
            <UBadge v-for="item in status?.approvedInputs || []" :key="item" color="neutral" variant="subtle">
              {{ item }}
            </UBadge>
          </div>
        </UCard>
      </div>

      <div class="grid gap-4 xl:grid-cols-2">
        <UCard>
          <template #header>
            <div class="flex items-center gap-2">
              <UIcon name="i-lucide-list-checks" class="size-5 text-primary" />
              <h2 class="text-base font-semibold text-highlighted">Readiness Checklist</h2>
            </div>
          </template>
          <ul class="space-y-3 text-sm text-toned">
            <li v-for="item in status?.readinessChecklist || []" :key="item" class="flex gap-2">
              <UIcon name="i-lucide-check-circle-2" class="mt-0.5 size-4 shrink-0 text-success" />
              <span>{{ item }}</span>
            </li>
          </ul>
        </UCard>

        <UCard>
          <template #header>
            <div class="flex items-center gap-2">
              <UIcon name="i-lucide-octagon-alert" class="size-5 text-warning" />
              <h2 class="text-base font-semibold text-highlighted">Blockers And Next</h2>
            </div>
          </template>
          <div class="space-y-4">
            <div>
              <p class="mb-2 text-xs font-semibold uppercase tracking-wide text-muted">Blockers</p>
              <ul class="space-y-2 text-sm text-toned">
                <li v-for="item in status?.blockers || []" :key="item" class="flex gap-2">
                  <UIcon name="i-lucide-alert-triangle" class="mt-0.5 size-4 shrink-0 text-warning" />
                  <span>{{ item }}</span>
                </li>
              </ul>
            </div>
            <div>
              <p class="mb-2 text-xs font-semibold uppercase tracking-wide text-muted">Next After This Part</p>
              <ul class="space-y-2 text-sm text-toned">
                <li v-for="item in status?.nextAfterThisPart || []" :key="item" class="flex gap-2">
                  <UIcon name="i-lucide-arrow-right" class="mt-0.5 size-4 shrink-0 text-primary" />
                  <span>{{ item }}</span>
                </li>
              </ul>
            </div>
          </div>
        </UCard>
      </div>
    </section>
  </AppShell>
</template>
