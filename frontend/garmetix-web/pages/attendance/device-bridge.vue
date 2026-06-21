<script setup lang="ts">
const reports = useAttendanceReports()
const feedback = useUiFeedback()
const loading = ref(false)
const runningSimulator = ref('')
const status = ref<any | null>(null)
const simulatorHealth = ref<any | null>(null)
const simulatorResult = ref<any | null>(null)

const summaryCards = computed(() => [
  { label: 'Status', value: status.value?.status || 'Loading', detail: status.value?.buildCode || '-', icon: 'i-lucide-fingerprint' },
  { label: 'Bridge Enabled', value: status.value?.fingerprintBridgeEnabled ? 'Yes' : 'No', detail: 'requires hardware approval', icon: 'i-lucide-power' },
  { label: 'Raw Storage', value: status.value?.rawFingerprintStorageAllowed ? 'Allowed' : 'Blocked', detail: 'privacy guard', icon: 'i-lucide-shield-alert' },
  { label: 'Adapters', value: String(status.value?.adapterCandidates?.length || 0), detail: 'candidate devices', icon: 'i-lucide-usb' }
])

async function refresh() {
  loading.value = true
  try {
    const [statusResult, healthResult] = await Promise.allSettled([
      reports.deviceBridgeStatus(),
      reports.deviceBridgeSimulatorHealth()
    ])
    if (statusResult.status === 'fulfilled') status.value = statusResult.value
    else throw statusResult.reason
    if (healthResult.status === 'fulfilled') simulatorHealth.value = healthResult.value
  } catch (error: any) {
    feedback.fromError('Fingerprint bridge status failed', error)
  } finally {
    loading.value = false
  }
}

async function runSimulator(action: 'capture' | 'identify' | 'enroll', scenario = 'Success') {
  runningSimulator.value = `${action}-${scenario}`
  try {
    const body = {
      scenario,
      employeeCode: 'SIM-EMP-001',
      employeeName: 'Simulator Employee'
    }
    const runners: Record<string, (payload: any) => Promise<any>> = {
      capture: reports.deviceBridgeSimulatorCapture,
      identify: reports.deviceBridgeSimulatorIdentify,
      enroll: reports.deviceBridgeSimulatorEnroll
    }
    simulatorResult.value = await runners[action](body)
    if (simulatorResult.value?.success) {
      feedback.success('Simulator handshake completed', simulatorResult.value.message)
    } else {
      feedback.notify('Simulator returned controlled failure', simulatorResult.value?.message || 'Check Message Logs for sanitized details.', 'warning')
    }
  } catch (error: any) {
    feedback.fromError('Fingerprint simulator failed', error)
  } finally {
    runningSimulator.value = ''
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
          <div class="flex flex-wrap items-center justify-between gap-3">
            <div>
              <h2 class="text-lg font-semibold">Simulator handshake</h2>
              <p class="text-sm text-muted">Run safe bridge responses before a real fingerprint reader is selected.</p>
            </div>
            <UBadge color="success" variant="soft">{{ simulatorHealth?.bridgeMode || 'Simulator' }}</UBadge>
          </div>
        </template>
        <div class="grid gap-4 xl:grid-cols-[minmax(0,1fr)_minmax(0,1.4fr)]">
          <div class="space-y-3">
            <div class="rounded-lg border border-default p-3">
              <p class="text-xs uppercase text-muted">Health</p>
              <p class="mt-1 text-sm font-medium">{{ simulatorHealth?.message || 'Health not loaded yet.' }}</p>
              <p class="mt-1 text-xs text-muted">{{ simulatorHealth?.deviceSerial || 'SIM-FP-0001' }}</p>
            </div>
            <div class="grid gap-2 sm:grid-cols-2">
              <UButton icon="i-lucide-scan-line" label="Capture" :loading="runningSimulator === 'capture-Success'" @click="runSimulator('capture')" />
              <UButton icon="i-lucide-search-check" label="Identify" :loading="runningSimulator === 'identify-Success'" @click="runSimulator('identify')" />
              <UButton icon="i-lucide-user-plus" label="Enroll" :loading="runningSimulator === 'enroll-Success'" @click="runSimulator('enroll')" />
              <UButton icon="i-lucide-triangle-alert" label="Test Failure" color="warning" variant="subtle" :loading="runningSimulator === 'identify-Fail'" @click="runSimulator('identify', 'Fail')" />
            </div>
          </div>
          <div class="rounded-lg border border-default p-3">
            <p class="text-xs uppercase text-muted">Last simulator result</p>
            <div v-if="simulatorResult" class="mt-3 grid gap-2 sm:grid-cols-2">
              <div class="rounded-lg border border-default p-3">
                <p class="text-xs text-muted">Status</p>
                <p class="font-medium">{{ simulatorResult.success ? 'Success' : 'Controlled Failure' }}</p>
              </div>
              <div class="rounded-lg border border-default p-3">
                <p class="text-xs text-muted">Match</p>
                <p class="font-medium">{{ simulatorResult.matchStatus }}</p>
              </div>
              <div class="rounded-lg border border-default p-3">
                <p class="text-xs text-muted">Quality</p>
                <p class="font-medium">{{ simulatorResult.qualityScore }}</p>
              </div>
              <div class="rounded-lg border border-default p-3">
                <p class="text-xs text-muted">Raw Payload Stored</p>
                <p class="font-medium">{{ simulatorResult.rawPayloadStored ? 'Yes' : 'No' }}</p>
              </div>
              <div class="rounded-lg border border-default p-3 sm:col-span-2">
                <p class="text-xs text-muted">Audit Ref</p>
                <code class="mt-1 block break-all text-xs">{{ simulatorResult.auditRef }}</code>
              </div>
              <div class="rounded-lg border border-default p-3 sm:col-span-2">
                <p class="text-xs text-muted">Message</p>
                <p class="mt-1 text-sm">{{ simulatorResult.message }}</p>
              </div>
            </div>
            <p v-else class="mt-3 text-sm text-muted">Run a simulator action to view the bridge response and Message Logs audit reference.</p>
          </div>
        </div>
      </UCard>

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
