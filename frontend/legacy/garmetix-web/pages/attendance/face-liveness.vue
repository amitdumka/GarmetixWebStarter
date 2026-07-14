<script setup lang="ts">
const reports = useAttendanceReports()
const feedback = useUiFeedback()
const loading = ref(false)
const runningSimulator = ref('')
const runningExternal = ref('')
const status = ref<any | null>(null)
const simulatorHealth = ref<any | null>(null)
const simulatorResult = ref<any | null>(null)
const externalBridgeUrl = ref('http://127.0.0.1:8791/garmetix-face/')
const externalResult = ref<any | null>(null)

const summaryCards = computed(() => [
  { label: 'Stage', value: status.value?.status || 'Loading', detail: status.value?.buildCode || '-', icon: 'i-lucide-scan-face' },
  { label: 'Face Match', value: status.value?.realFaceRecognitionEnabled ? 'Enabled' : 'Blocked', detail: 'requires approval', icon: 'i-lucide-shield-alert' },
  { label: 'Raw Storage', value: status.value?.rawFaceTemplateStorageAllowed ? 'Allowed' : 'Blocked', detail: 'privacy guard', icon: 'i-lucide-lock' },
  { label: 'Providers', value: String(status.value?.providerCandidates?.length || 0), detail: 'candidate paths', icon: 'i-lucide-plug' }
])

async function refresh() {
  loading.value = true
  try {
    const [statusResult, healthResult] = await Promise.allSettled([
      reports.faceLivenessStatus(),
      reports.faceLivenessSimulatorHealth()
    ])
    if (statusResult.status === 'fulfilled') status.value = statusResult.value
    else throw statusResult.reason
    if (healthResult.status === 'fulfilled') simulatorHealth.value = healthResult.value
  } catch (error: any) {
    feedback.fromError('Face liveness readiness failed', error)
  } finally {
    loading.value = false
  }
}

async function runSimulator(action: 'proof' | 'verify', scenario = 'Success') {
  runningSimulator.value = `${action}-${scenario}`
  try {
    const body = {
      scenario,
      employeeCode: 'SIM-FACE-001',
      employeeName: 'Face Simulator Employee',
      faceTemplateRef: 'sim-face-ref-sim-face-001',
      consentAuditRef: 'consent-audit-simulator'
    }
    const runners: Record<string, (payload: any) => Promise<any>> = {
      proof: reports.faceLivenessSimulatorProof,
      verify: reports.faceLivenessSimulatorVerify
    }
    simulatorResult.value = await runners[action](body)
    if (simulatorResult.value?.success) {
      feedback.success('Face/liveness simulator completed', simulatorResult.value.message)
    } else {
      feedback.notify('Face/liveness simulator returned controlled result', simulatorResult.value?.message || 'Check Message Logs for sanitized details.', 'warning')
    }
  } catch (error: any) {
    feedback.fromError('Face/liveness simulator failed', error)
  } finally {
    runningSimulator.value = ''
  }
}

async function runExternal(action: 'health' | 'proof' | 'verify') {
  runningExternal.value = action
  try {
    const body = {
      bridgeBaseUrl: externalBridgeUrl.value,
      employeeCode: 'SIM-FACE-001',
      employeeName: 'Face Simulator Employee',
      faceTemplateRef: 'external-face-ref-sim-face-001',
      consentAuditRef: 'consent-audit-external'
    }
    const runners: Record<string, (payload: any) => Promise<any>> = {
      health: reports.faceLivenessExternalHealth,
      proof: reports.faceLivenessExternalProof,
      verify: reports.faceLivenessExternalVerify
    }
    externalResult.value = await runners[action](body)
    if (externalResult.value?.success) {
      feedback.success('External face/liveness bridge completed', externalResult.value.message)
    } else {
      feedback.notify('External face/liveness bridge returned blocked or failed result', externalResult.value?.message || 'Check Message Logs for sanitized details.', 'warning')
    }
  } catch (error: any) {
    feedback.fromError('External face/liveness bridge failed', error)
  } finally {
    runningExternal.value = ''
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

      <div class="grid gap-4 xl:grid-cols-2">
        <UCard>
          <template #header>
            <div class="flex flex-wrap items-center justify-between gap-3">
              <div class="flex items-center gap-2">
                <UIcon name="i-lucide-flask-conical" class="size-5 text-primary" />
                <h2 class="text-base font-semibold text-highlighted">Simulator Proof</h2>
              </div>
              <UBadge color="success" variant="soft">{{ simulatorHealth?.bridgeMode || 'Simulator' }}</UBadge>
            </div>
          </template>
          <div class="space-y-4">
            <div class="rounded-md border border-default bg-muted/30 p-3">
              <p class="text-xs font-semibold uppercase tracking-wide text-muted">Health</p>
              <p class="mt-1 text-sm text-toned">{{ simulatorHealth?.message || 'Health not loaded yet.' }}</p>
            </div>
            <div class="grid gap-2 sm:grid-cols-3">
              <UButton icon="i-lucide-badge-check" label="Proof" :loading="runningSimulator === 'proof-Success'" @click="runSimulator('proof')" />
              <UButton icon="i-lucide-search-check" label="Verify" color="neutral" variant="subtle" :loading="runningSimulator === 'verify-Success'" @click="runSimulator('verify')" />
              <UButton icon="i-lucide-shield-alert" label="Block Raw" color="warning" variant="subtle" :loading="runningSimulator === 'verify-RawPayload'" @click="runSimulator('verify', 'RawPayload')" />
            </div>
            <div class="rounded-md border border-default p-3">
              <p class="text-xs font-semibold uppercase tracking-wide text-muted">Last simulator result</p>
              <div v-if="simulatorResult" class="mt-3 grid gap-2 sm:grid-cols-2">
                <div class="rounded-md border border-default p-3">
                  <p class="text-xs text-muted">Status</p>
                  <p class="font-medium">{{ simulatorResult.success ? 'Success' : 'Blocked / Failed' }}</p>
                </div>
                <div class="rounded-md border border-default p-3">
                  <p class="text-xs text-muted">Match</p>
                  <p class="font-medium">{{ simulatorResult.matchStatus }}</p>
                </div>
                <div class="rounded-md border border-default p-3">
                  <p class="text-xs text-muted">Quality / Liveness</p>
                  <p class="font-medium">{{ simulatorResult.qualityScore }} / {{ simulatorResult.livenessScore }}</p>
                </div>
                <div class="rounded-md border border-default p-3">
                  <p class="text-xs text-muted">Raw Payload Stored</p>
                  <p class="font-medium">{{ simulatorResult.rawPayloadStored ? 'Yes' : 'No' }}</p>
                </div>
                <div class="rounded-md border border-default p-3 sm:col-span-2">
                  <p class="text-xs text-muted">Audit Ref</p>
                  <code class="mt-1 block break-all text-xs">{{ simulatorResult.auditRef }}</code>
                </div>
                <div class="rounded-md border border-default p-3 sm:col-span-2">
                  <p class="text-xs text-muted">Message</p>
                  <p class="mt-1 text-sm">{{ simulatorResult.message }}</p>
                </div>
                <div v-if="simulatorResult.warnings?.length" class="rounded-md border border-warning/40 p-3 sm:col-span-2">
                  <p class="text-xs text-muted">Warnings</p>
                  <p v-for="item in simulatorResult.warnings" :key="item" class="mt-1 text-sm">{{ item }}</p>
                </div>
              </div>
              <p v-else class="mt-3 text-sm text-muted">Run a simulator action to create a sanitized Message Logs audit reference.</p>
            </div>
          </div>
        </UCard>

        <UCard>
          <template #header>
            <div class="flex flex-wrap items-center justify-between gap-3">
              <div class="flex items-center gap-2">
                <UIcon name="i-lucide-plug" class="size-5 text-primary" />
                <h2 class="text-base font-semibold text-highlighted">External Bridge</h2>
              </div>
              <UBadge color="info" variant="soft">Local/private only</UBadge>
            </div>
          </template>
          <div class="space-y-4">
            <UFormField label="Bridge base URL">
              <UInput v-model="externalBridgeUrl" placeholder="http://127.0.0.1:8791/garmetix-face/" />
            </UFormField>
            <div class="grid gap-2 sm:grid-cols-3">
              <UButton icon="i-lucide-heart-pulse" label="Health" color="neutral" variant="subtle" :loading="runningExternal === 'health'" @click="runExternal('health')" />
              <UButton icon="i-lucide-badge-check" label="Proof" color="neutral" variant="subtle" :loading="runningExternal === 'proof'" @click="runExternal('proof')" />
              <UButton icon="i-lucide-search-check" label="Verify" color="neutral" variant="subtle" :loading="runningExternal === 'verify'" @click="runExternal('verify')" />
            </div>
            <div class="rounded-md border border-default bg-muted/30 p-3 text-sm text-muted">
              Allowed hosts are localhost, loopback, host.docker.internal and private LAN addresses. Raw face images, embeddings, landmarks and template payloads are blocked.
            </div>
            <div class="rounded-md border border-default p-3">
              <p class="text-xs font-semibold uppercase tracking-wide text-muted">Last external result</p>
              <div v-if="externalResult" class="mt-3 grid gap-2 sm:grid-cols-2">
                <div class="rounded-md border border-default p-3">
                  <p class="text-xs text-muted">Status</p>
                  <p class="font-medium">{{ externalResult.success ? 'Success' : 'Blocked / Failed' }}</p>
                </div>
                <div class="rounded-md border border-default p-3">
                  <p class="text-xs text-muted">Vendor</p>
                  <p class="font-medium">{{ externalResult.vendor || '-' }}</p>
                </div>
                <div class="rounded-md border border-default p-3">
                  <p class="text-xs text-muted">Match</p>
                  <p class="font-medium">{{ externalResult.matchStatus }}</p>
                </div>
                <div class="rounded-md border border-default p-3">
                  <p class="text-xs text-muted">Quality / Liveness</p>
                  <p class="font-medium">{{ externalResult.qualityScore }} / {{ externalResult.livenessScore }}</p>
                </div>
                <div class="rounded-md border border-default p-3">
                  <p class="text-xs text-muted">Raw Payload Stored</p>
                  <p class="font-medium">{{ externalResult.rawPayloadStored ? 'Yes' : 'No' }}</p>
                </div>
                <div class="rounded-md border border-default p-3">
                  <p class="text-xs text-muted">Template Ref</p>
                  <p class="truncate font-medium">{{ externalResult.faceTemplateRef || '-' }}</p>
                </div>
                <div class="rounded-md border border-default p-3 sm:col-span-2">
                  <p class="text-xs text-muted">Audit Ref</p>
                  <code class="mt-1 block break-all text-xs">{{ externalResult.auditRef }}</code>
                </div>
                <div class="rounded-md border border-default p-3 sm:col-span-2">
                  <p class="text-xs text-muted">Message</p>
                  <p class="mt-1 text-sm">{{ externalResult.message }}</p>
                </div>
                <div v-if="externalResult.warnings?.length" class="rounded-md border border-warning/40 p-3 sm:col-span-2">
                  <p class="text-xs text-muted">Warnings</p>
                  <p v-for="item in externalResult.warnings" :key="item" class="mt-1 text-sm">{{ item }}</p>
                </div>
              </div>
              <p v-else class="mt-3 text-sm text-muted">Run a connector action after starting a compatible local face/liveness bridge.</p>
            </div>
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
