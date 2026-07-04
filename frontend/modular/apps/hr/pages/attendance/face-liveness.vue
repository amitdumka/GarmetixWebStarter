<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 xl:flex-row xl:items-start xl:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-scan-face" class="size-4" /> Face liveness</p>
          <h2 class="garmetix-dashboard-title">Face Liveness</h2>
          <p class="garmetix-dashboard-subtitle">
            Readiness contract, simulator proof and raw face payload blocking before any real provider is enabled.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton icon="i-lucide-image-check" color="primary" variant="soft" to="/attendance/photo-review">Photo Review</UButton>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="load">Refresh</UButton>
        </div>
      </div>
    </div>

    <UAlert color="warning" variant="subtle" icon="i-lucide-shield-alert" description="Real face recognition is not enabled. This page verifies safe contracts and logs simulator proof only." />
    <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="error" />

    <div class="grid gap-3 md:grid-cols-4">
      <div v-for="card in cards" :key="card.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label">{{ card.label }}</p>
        <p class="garmetix-metric-value text-lg">{{ card.value }}</p>
        <p class="garmetix-metric-caption">{{ card.caption }}</p>
      </div>
    </div>

    <div class="grid gap-4 xl:grid-cols-[minmax(0,1fr)_400px]">
      <div class="garmetix-table-panel">
        <div class="garmetix-panel-header">
          <div>
            <h3 class="garmetix-panel-title">Readiness Contract</h3>
            <p class="garmetix-panel-subtitle">{{ readText(status, ['message']) }}</p>
          </div>
        </div>
        <div class="grid gap-4 lg:grid-cols-2">
          <div>
            <h4 class="mb-2 text-sm font-semibold">Current Safe Base</h4>
            <ul class="space-y-2 text-sm text-muted">
              <li v-for="item in textList(status, ['currentSafeBase'])" :key="item" class="flex gap-2">
                <UIcon name="i-lucide-check-circle-2" class="mt-0.5 size-4 shrink-0 text-primary" />
                <span>{{ item }}</span>
              </li>
            </ul>
          </div>
          <div>
            <h4 class="mb-2 text-sm font-semibold">Blocked Fields</h4>
            <ul class="space-y-2 text-sm text-muted">
              <li v-for="item in textList(status, ['blockedResponseFields'])" :key="item" class="flex gap-2">
                <UIcon name="i-lucide-ban" class="mt-0.5 size-4 shrink-0 text-error" />
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
            <p class="garmetix-panel-subtitle">Proof/verify returns references and scores only.</p>
          </div>
        </div>
        <div class="grid gap-3">
          <UFormField label="Scenario">
            <USelect v-model="simulator.scenario" :items="scenarioOptions" />
          </UFormField>
          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Employee Code"><UInput v-model="simulator.employeeCode" /></UFormField>
            <UFormField label="Employee Name"><UInput v-model="simulator.employeeName" /></UFormField>
          </div>
          <UFormField label="Photo proof id">
            <UInput v-model="simulator.photoProofId" placeholder="Optional proof id" />
          </UFormField>
          <UFormField label="Type FACE to enable drill">
            <UInput v-model="confirmText" placeholder="FACE" />
          </UFormField>
          <div class="flex flex-wrap gap-2">
            <UButton size="sm" icon="i-lucide-camera" :disabled="!canRun" :loading="runningAction === 'proof'" @click="runSimulator('proof')">Proof</UButton>
            <UButton size="sm" icon="i-lucide-shield-check" :disabled="!canRun" :loading="runningAction === 'verify'" @click="runSimulator('verify')">Verify</UButton>
          </div>
          <UAlert v-if="simulatorMessage" :color="simulatorTone" variant="subtle" :icon="simulatorIcon" :description="simulatorMessage" />
          <div v-if="result" class="rounded-lg border border-default bg-muted/20 p-3 text-sm">
            <p><span class="text-muted">Status:</span> {{ readText(result, ['matchStatus']) }}</p>
            <p><span class="text-muted">Quality:</span> {{ readText(result, ['qualityScore']) }}</p>
            <p><span class="text-muted">Liveness:</span> {{ readText(result, ['livenessScore']) }}</p>
            <p><span class="text-muted">Raw Stored:</span> {{ readBoolean(result, ['rawPayloadStored']) ? 'Yes' : 'No' }}</p>
            <p><span class="text-muted">Audit:</span> {{ readText(result, ['auditRef']) }}</p>
          </div>
        </div>
      </div>
    </div>

    <div class="grid gap-4 lg:grid-cols-2">
      <div class="garmetix-table-panel">
        <div class="garmetix-panel-header">
          <div>
            <h3 class="garmetix-panel-title">Readiness Checklist</h3>
            <p class="garmetix-panel-subtitle">Must pass before production provider wiring.</p>
          </div>
        </div>
        <ul class="space-y-2 text-sm text-muted">
          <li v-for="item in textList(status, ['readinessChecklist']).slice(0, 10)" :key="item" class="flex gap-2">
            <UIcon name="i-lucide-list-checks" class="mt-0.5 size-4 shrink-0 text-primary" />
            <span>{{ item }}</span>
          </li>
        </ul>
      </div>
      <div class="garmetix-table-panel">
        <div class="garmetix-panel-header">
          <div>
            <h3 class="garmetix-panel-title">Blockers</h3>
            <p class="garmetix-panel-subtitle">Keep real matching disabled until resolved.</p>
          </div>
        </div>
        <ul class="space-y-2 text-sm text-muted">
          <li v-for="item in textList(status, ['blockers'])" :key="item" class="flex gap-2">
            <UIcon name="i-lucide-alert-triangle" class="mt-0.5 size-4 shrink-0 text-warning" />
            <span>{{ item }}</span>
          </li>
        </ul>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { readArray, readBoolean, readText, type ApiRecord, useHrApiClient } from '../../utils/hr-api'

useHead({ title: 'Face Liveness - Garmetix HR' })

const { get, post } = useHrApiClient()
const loading = ref(false)
const error = ref('')
const status = ref<ApiRecord | null>(null)
const health = ref<ApiRecord | null>(null)
const result = ref<ApiRecord | null>(null)
const simulatorMessage = ref('')
const runningAction = ref('')
const confirmText = ref('')
const simulator = reactive({
  scenario: 'Success',
  employeeCode: 'SIM-FACE-001',
  employeeName: 'Face Simulator Employee',
  photoProofId: ''
})

const scenarioOptions = ['Success', 'Fail', 'Timeout', 'RawPayload'].map(value => ({ label: value, value }))
const canRun = computed(() => confirmText.value.trim().toUpperCase() === 'FACE' && !runningAction.value)
const simulatorTone = computed(() => readBoolean(result.value, ['success']) ? 'success' : 'warning')
const simulatorIcon = computed(() => readBoolean(result.value, ['success']) ? 'i-lucide-check-circle-2' : 'i-lucide-circle-alert')
const cards = computed(() => [
  { label: 'Status', value: readText(status.value, ['status']), caption: readText(status.value, ['title'], 'Readiness contract') },
  { label: 'Real Matching', value: readBoolean(status.value, ['realFaceRecognitionEnabled']) ? 'Enabled' : 'Off', caption: 'Must remain off until approved' },
  { label: 'Photo Proof', value: readBoolean(status.value, ['photoProofEvidenceEnabled']) ? 'Available' : 'Off', caption: 'Manual review workflow' },
  { label: 'Simulator', value: readBoolean(health.value, ['success']) ? 'Ready' : 'Not loaded', caption: readText(health.value, ['message'], 'Health not loaded') }
])

function textList(source: ApiRecord | null | undefined, keys: string[]) {
  return readArray(source, keys).map(item => typeof item === 'string' ? item : JSON.stringify(item))
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    const [statusResponse, healthResponse] = await Promise.all([
      get<ApiRecord>('api/attendance/face-liveness/status'),
      get<ApiRecord>('api/attendance/face-liveness/simulator/health')
    ])
    status.value = statusResponse
    health.value = healthResponse
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load face liveness status.'
  } finally {
    loading.value = false
  }
}

async function runSimulator(action: 'proof' | 'verify') {
  if (!canRun.value) return
  runningAction.value = action
  simulatorMessage.value = ''
  result.value = null
  try {
    const response = await post<ApiRecord>(`api/attendance/face-liveness/simulator/${action}`, {
      scenario: simulator.scenario,
      employeeCode: simulator.employeeCode,
      employeeName: simulator.employeeName,
      photoProofId: simulator.photoProofId || null
    })
    result.value = response
    simulatorMessage.value = readText(response, ['message'], 'Face liveness simulator completed.')
  } catch (caught) {
    simulatorMessage.value = caught instanceof Error ? caught.message : 'Unable to run face liveness simulator.'
  } finally {
    runningAction.value = ''
  }
}

onMounted(load)
</script>
