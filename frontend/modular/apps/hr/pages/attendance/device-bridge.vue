<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-cable" class="size-4" /> Fingerprint bridge</p>
          <h2 class="garmetix-dashboard-title">Device Bridge</h2>
          <p class="garmetix-dashboard-subtitle">
            Mantra bridge readiness, simulator checks and no-raw-biometric guard evidence.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="load">Refresh</UButton>
          <UButton icon="i-lucide-fingerprint" color="primary" variant="soft" to="/attendance/biometric-enrollment">Enrollment</UButton>
        </div>
      </div>
    </div>

    <UAlert
      color="warning"
      variant="subtle"
      icon="i-lucide-shield-alert"
      title="Hardware is not required for this stage"
      description="Real fingerprint capture remains disabled until the Mantra SDK/service is installed, consent is accepted and the bridge contract passes."
    />
    <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="error" />

    <div class="grid gap-3 md:grid-cols-4">
      <div v-for="card in cards" :key="card.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label">{{ card.label }}</p>
        <p class="garmetix-metric-value text-lg">{{ card.value }}</p>
        <p class="garmetix-metric-caption">{{ card.caption }}</p>
      </div>
    </div>

    <div class="grid gap-4 xl:grid-cols-[minmax(0,1.1fr)_minmax(340px,0.9fr)]">
      <div class="garmetix-table-panel">
        <div class="garmetix-panel-header">
          <div>
            <h3 class="garmetix-panel-title">Bridge Contract</h3>
            <p class="garmetix-panel-subtitle">Selected hardware, adapter and safe response boundary.</p>
          </div>
        </div>
        <div class="grid gap-3 md:grid-cols-2">
          <div class="rounded-lg border border-default p-3">
            <p class="text-xs uppercase text-muted">Selected hardware</p>
            <p class="mt-1 font-semibold">{{ readText(status, ['selectedFingerprintHardware']) }}</p>
            <p class="mt-1 text-xs text-muted">{{ readText(status, ['selectedBridgeAdapterStatus']) }}</p>
          </div>
          <div class="rounded-lg border border-default p-3">
            <p class="text-xs uppercase text-muted">Matching location</p>
            <p class="mt-1 text-sm">{{ readText(status, ['matchingLocation']) }}</p>
          </div>
        </div>
        <div class="mt-4 grid gap-4 lg:grid-cols-2">
          <div>
            <h4 class="mb-2 text-sm font-semibold">Supported Inputs</h4>
            <ul class="space-y-2 text-sm text-muted">
              <li v-for="item in textList(status, ['supportedBridgeInputs'])" :key="item" class="flex gap-2">
                <UIcon name="i-lucide-check-circle-2" class="mt-0.5 size-4 shrink-0 text-primary" />
                <span>{{ item }}</span>
              </li>
            </ul>
          </div>
          <div>
            <h4 class="mb-2 text-sm font-semibold">Privacy Rules</h4>
            <ul class="space-y-2 text-sm text-muted">
              <li v-for="item in textList(status, ['privacyRules'])" :key="item" class="flex gap-2">
                <UIcon name="i-lucide-lock-keyhole" class="mt-0.5 size-4 shrink-0 text-warning" />
                <span>{{ item }}</span>
              </li>
            </ul>
          </div>
        </div>
      </div>

      <div class="garmetix-table-panel">
        <div class="garmetix-panel-header">
          <div>
            <h3 class="garmetix-panel-title">Simulator Drill</h3>
            <p class="garmetix-panel-subtitle">Writes sanitized Message Logs only after confirmation.</p>
          </div>
        </div>
        <div class="grid gap-3">
          <UFormField label="Scenario">
            <USelect v-model="simulator.scenario" :items="scenarioOptions" />
          </UFormField>
          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Employee Code">
              <UInput v-model="simulator.employeeCode" placeholder="SIM-EMP-001" />
            </UFormField>
            <UFormField label="Employee Name">
              <UInput v-model="simulator.employeeName" placeholder="Simulator Employee" />
            </UFormField>
          </div>
          <UFormField label="Type SIMULATOR to enable drill buttons">
            <UInput v-model="confirmText" placeholder="SIMULATOR" />
          </UFormField>
          <div class="flex flex-wrap gap-2">
            <UButton size="sm" icon="i-lucide-radio-receiver" :disabled="!canRunSimulator" :loading="runningAction === 'capture'" @click="runSimulator('capture')">Capture</UButton>
            <UButton size="sm" icon="i-lucide-search-check" :disabled="!canRunSimulator" :loading="runningAction === 'identify'" @click="runSimulator('identify')">Identify</UButton>
            <UButton size="sm" icon="i-lucide-user-check" :disabled="!canRunSimulator" :loading="runningAction === 'enroll'" @click="runSimulator('enroll')">Enroll</UButton>
          </div>
          <UAlert v-if="simulatorMessage" :color="simulatorTone" variant="subtle" :icon="simulatorIcon" :description="simulatorMessage" />
          <div v-if="simulatorResult" class="rounded-lg border border-default bg-muted/20 p-3 text-sm">
            <div class="grid gap-2 sm:grid-cols-2">
              <p><span class="text-muted">Status:</span> {{ readText(simulatorResult, ['matchStatus']) }}</p>
              <p><span class="text-muted">Quality:</span> {{ readText(simulatorResult, ['qualityScore']) }}</p>
              <p><span class="text-muted">Audit Ref:</span> {{ readText(simulatorResult, ['auditRef']) }}</p>
              <p><span class="text-muted">Raw Stored:</span> {{ yesNo(readBoolean(simulatorResult, ['rawPayloadStored'])) }}</p>
            </div>
          </div>
        </div>
      </div>
    </div>

    <div class="grid gap-4 lg:grid-cols-2">
      <div class="garmetix-table-panel">
        <div class="garmetix-panel-header">
          <div>
            <h3 class="garmetix-panel-title">Implementation Checklist</h3>
            <p class="garmetix-panel-subtitle">Next safe steps before live Mantra use.</p>
          </div>
        </div>
        <ul class="space-y-2 text-sm text-muted">
          <li v-for="item in textList(status, ['implementationChecklist']).slice(0, 10)" :key="item" class="flex gap-2">
            <UIcon name="i-lucide-list-checks" class="mt-0.5 size-4 shrink-0 text-primary" />
            <span>{{ item }}</span>
          </li>
        </ul>
      </div>

      <div class="garmetix-table-panel">
        <div class="garmetix-panel-header">
          <div>
            <h3 class="garmetix-panel-title">Blockers</h3>
            <p class="garmetix-panel-subtitle">Do not enable live matching while these remain open.</p>
          </div>
        </div>
        <ul class="space-y-2 text-sm text-muted">
          <li v-for="item in textList(status, ['blockers'])" :key="item" class="flex gap-2">
            <UIcon name="i-lucide-ban" class="mt-0.5 size-4 shrink-0 text-error" />
            <span>{{ item }}</span>
          </li>
        </ul>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { readArray, readBoolean, readText, type ApiRecord, useHrApiClient } from '../../utils/hr-api'

useHead({ title: 'Device Bridge - Garmetix HR' })

const { get, post } = useHrApiClient()
const loading = ref(false)
const error = ref('')
const status = ref<ApiRecord | null>(null)
const simulatorHealth = ref<ApiRecord | null>(null)
const confirmText = ref('')
const runningAction = ref('')
const simulatorMessage = ref('')
const simulatorResult = ref<ApiRecord | null>(null)
const simulator = reactive({
  scenario: 'Success',
  employeeCode: 'SIM-EMP-001',
  employeeName: 'Simulator Employee'
})

const scenarioOptions = ['Success', 'Fail', 'Timeout']
const canRunSimulator = computed(() => confirmText.value.trim().toUpperCase() === 'SIMULATOR' && !runningAction.value)
const simulatorTone = computed(() => readBoolean(simulatorResult.value, ['success']) ? 'success' : 'warning')
const simulatorIcon = computed(() => readBoolean(simulatorResult.value, ['success']) ? 'i-lucide-check-circle-2' : 'i-lucide-circle-alert')
const cards = computed(() => [
  {
    label: 'Contract Status',
    value: readText(status.value, ['status']),
    caption: readText(status.value, ['title'], 'Bridge readiness contract')
  },
  {
    label: 'Simulator',
    value: readBoolean(status.value, ['simulatorBridgeEnabled']) ? 'Enabled' : 'Off',
    caption: readText(simulatorHealth.value, ['message'], 'Simulator health not loaded')
  },
  {
    label: 'External Connector',
    value: readBoolean(status.value, ['externalBridgeConnectorEnabled']) ? 'Available' : 'Off',
    caption: 'Only localhost, loopback, host.docker.internal or private LAN bridge URLs'
  },
  {
    label: 'Raw Storage',
    value: readBoolean(status.value, ['rawFingerprintStorageAllowed']) ? 'Allowed' : 'Blocked',
    caption: 'Raw fingerprint payloads must remain blocked'
  }
])

function textList(source: ApiRecord | null | undefined, keys: string[]) {
  return readArray(source, keys).map(item => typeof item === 'string' ? item : JSON.stringify(item))
}

function yesNo(value: boolean) {
  return value ? 'Yes' : 'No'
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    const [statusResponse, healthResponse] = await Promise.all([
      get<ApiRecord>('api/attendance/device-bridge/status'),
      get<ApiRecord>('api/attendance/device-bridge/simulator/health')
    ])
    status.value = statusResponse
    simulatorHealth.value = healthResponse
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load device bridge status.'
  } finally {
    loading.value = false
  }
}

async function runSimulator(action: 'capture' | 'identify' | 'enroll') {
  if (!canRunSimulator.value) {
    simulatorMessage.value = 'Type SIMULATOR before running a bridge drill.'
    simulatorResult.value = null
    return
  }
  runningAction.value = action
  simulatorMessage.value = ''
  simulatorResult.value = null
  try {
    const response = await post<ApiRecord>(`api/attendance/device-bridge/simulator/${action}`, {
      scenario: simulator.scenario,
      employeeCode: simulator.employeeCode,
      employeeName: simulator.employeeName
    })
    simulatorResult.value = response
    simulatorMessage.value = readText(response, ['message'], 'Simulator drill completed.')
  } catch (caught) {
    simulatorMessage.value = caught instanceof Error ? caught.message : 'Unable to run simulator drill.'
  } finally {
    runningAction.value = ''
  }
}

onMounted(load)
</script>
