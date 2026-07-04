<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-tablet-smartphone" class="size-4" /> Web kiosk</p>
          <h2 class="garmetix-dashboard-title">Attendance Kiosk</h2>
          <p class="garmetix-dashboard-subtitle">
            Device-token readiness and employee lookup console. Punch submission remains guarded until physical kiosk acceptance.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton icon="i-lucide-monitor-dot" color="neutral" variant="soft" to="/attendance/kiosk-monitor">Monitor</UButton>
          <UButton icon="i-lucide-smartphone" color="primary" variant="soft" to="/attendance/mobile-kiosk">Mobile Kiosk</UButton>
        </div>
      </div>
    </div>

    <UAlert
      color="warning"
      variant="subtle"
      icon="i-lucide-shield-alert"
      title="Punch action is intentionally not exposed here yet"
      description="Use this page to verify device readiness and employee lookup. Actual punch flows stay with approved kiosk apps or manual punch until the hardware rehearsal passes."
    />

    <div class="grid gap-4 xl:grid-cols-[380px_minmax(0,1fr)]">
      <div class="garmetix-table-panel">
        <div class="garmetix-panel-header">
          <div>
            <h3 class="garmetix-panel-title">Device Credentials</h3>
            <p class="garmetix-panel-subtitle">Credentials are stored only in this browser session.</p>
          </div>
        </div>
        <div class="grid gap-3">
          <UFormField label="Device ID">
            <UInput v-model="form.deviceId" placeholder="Registered kiosk device id" />
          </UFormField>
          <UFormField label="Device Token">
            <UInput v-model="form.deviceToken" type="password" placeholder="One-time token copied during registration" />
          </UFormField>
          <UFormField label="Optional Device Code">
            <UInput v-model="form.deviceCode" placeholder="KIOSK-..." />
          </UFormField>
          <div class="flex flex-wrap gap-2">
            <UButton size="sm" icon="i-lucide-shield-check" :loading="checkingReadiness" @click="checkReadiness">Check Readiness</UButton>
            <UButton size="sm" icon="i-lucide-radio" color="neutral" variant="soft" :loading="bootstrapping" @click="bootstrapDevice">Bootstrap</UButton>
          </div>
          <UAlert v-if="message" :color="messageTone" variant="subtle" :icon="messageIcon" :description="message" />
        </div>
      </div>

      <div class="grid gap-4">
        <div class="grid gap-3 md:grid-cols-4">
          <div v-for="card in cards" :key="card.label" class="garmetix-metric-card">
            <p class="garmetix-metric-label">{{ card.label }}</p>
            <p class="garmetix-metric-value text-lg">{{ card.value }}</p>
            <p class="garmetix-metric-caption">{{ card.caption }}</p>
          </div>
        </div>

        <div class="garmetix-table-panel">
          <div class="garmetix-panel-header">
            <div>
              <h3 class="garmetix-panel-title">Employee Lookup</h3>
              <p class="garmetix-panel-subtitle">Search by employee code, mobile number or name after device readiness.</p>
            </div>
            <UButton size="sm" icon="i-lucide-search" :disabled="!canLookup" :loading="lookupLoading" @click="lookupEmployee">Lookup</UButton>
          </div>
          <div class="grid gap-3 md:grid-cols-[minmax(0,1fr)_auto]">
            <UInput v-model="lookupTerm" placeholder="Employee code, mobile or name" @keyup.enter="lookupEmployee" />
          </div>
          <div class="mt-4 overflow-auto">
            <table class="w-full min-w-[700px] text-left text-sm">
              <thead class="bg-muted/30 text-xs uppercase text-muted">
                <tr>
                  <th class="px-3 py-2">Employee</th>
                  <th class="px-3 py-2">Code</th>
                  <th class="px-3 py-2">Mobile</th>
                  <th class="px-3 py-2">Department</th>
                  <th class="px-3 py-2">Designation</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="(employee, index) in employees" :key="readText(employee, ['id', 'employeeId'], String(index))" class="border-t border-default">
                  <td class="px-3 py-2 font-medium">{{ readText(employee, ['employeeName', 'staffName', 'name']) }}</td>
                  <td class="px-3 py-2">{{ readText(employee, ['employeeCode', 'code']) }}</td>
                  <td class="px-3 py-2">{{ readText(employee, ['mobile']) }}</td>
                  <td class="px-3 py-2">{{ readText(employee, ['department']) }}</td>
                  <td class="px-3 py-2">{{ readText(employee, ['designation']) }}</td>
                </tr>
                <tr v-if="!employees.length">
                  <td class="px-3 py-6 text-center text-muted" colspan="5">No employee lookup rows.</td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </div>

    <div class="grid gap-4 lg:grid-cols-2">
      <div class="garmetix-table-panel">
        <div class="garmetix-panel-header">
          <div>
            <h3 class="garmetix-panel-title">Fingerprint Rules</h3>
            <p class="garmetix-panel-subtitle">Rules returned by readiness check.</p>
          </div>
        </div>
        <ul class="space-y-2 text-sm text-muted">
          <li v-for="item in textList(readiness, ['fingerprintRules'])" :key="item" class="flex gap-2">
            <UIcon name="i-lucide-fingerprint" class="mt-0.5 size-4 shrink-0 text-primary" />
            <span>{{ item }}</span>
          </li>
        </ul>
      </div>
      <div class="garmetix-table-panel">
        <div class="garmetix-panel-header">
          <div>
            <h3 class="garmetix-panel-title">Next Stage Items</h3>
            <p class="garmetix-panel-subtitle">What remains before hardware go-live.</p>
          </div>
        </div>
        <ul class="space-y-2 text-sm text-muted">
          <li v-for="item in textList(readiness, ['nextStageItems']).concat(textList(bootstrap, ['supportedSources']))" :key="item" class="flex gap-2">
            <UIcon name="i-lucide-list-checks" class="mt-0.5 size-4 shrink-0 text-success" />
            <span>{{ item }}</span>
          </li>
        </ul>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { readArray, readBoolean, readNumber, readText, type ApiRecord, useHrApiClient } from '../../utils/hr-api'

useHead({ title: 'Attendance Kiosk - Garmetix HR' })

const { post } = useHrApiClient()
const form = reactive({
  deviceId: '',
  deviceToken: '',
  deviceCode: ''
})
const lookupTerm = ref('')
const checkingReadiness = ref(false)
const bootstrapping = ref(false)
const lookupLoading = ref(false)
const readiness = ref<ApiRecord | null>(null)
const bootstrap = ref<ApiRecord | null>(null)
const employees = ref<ApiRecord[]>([])
const message = ref('')
const messageSuccess = ref(false)

const canLookup = computed(() => Boolean(form.deviceId && form.deviceToken && lookupTerm.value.trim()) && !lookupLoading.value)
const messageTone = computed(() => messageSuccess.value ? 'success' : 'warning')
const messageIcon = computed(() => messageSuccess.value ? 'i-lucide-check-circle-2' : 'i-lucide-circle-alert')
const cards = computed(() => [
  { label: 'Device', value: readBoolean(readiness.value, ['deviceValid']) ? 'Valid' : 'Not checked', caption: readText(readiness.value, ['deviceName', 'deviceCode'], 'Run readiness') },
  { label: 'Store Scope', value: readText(readiness.value, ['storeScope']), caption: 'Workspace returned by API' },
  { label: 'Photo Proof', value: readBoolean(readiness.value, ['photoProofEnabled']) ? 'Enabled' : 'Off', caption: `${readNumber(readiness.value, ['photoProofMaxBytes']) || 0} bytes max` },
  { label: 'Fingerprint', value: readBoolean(readiness.value, ['fingerprintPunchRequired']) ? 'Required' : 'Not required', caption: readText(readiness.value, ['fingerprintVerificationMode']) }
])

function textList(source: ApiRecord | null | undefined, keys: string[]) {
  return readArray(source, keys).map(item => typeof item === 'string' ? item : JSON.stringify(item))
}

function parseDeviceId() {
  const value = form.deviceId.trim()
  return value || null
}

async function checkReadiness() {
  if (!form.deviceId || !form.deviceToken) {
    message.value = 'Enter Device ID and Device Token before readiness check.'
    messageSuccess.value = false
    return
  }
  checkingReadiness.value = true
  message.value = ''
  try {
    const response = await post<ApiRecord>('api/attendance/kiosk/readiness', {
      deviceId: parseDeviceId(),
      deviceToken: form.deviceToken
    })
    readiness.value = response
    messageSuccess.value = readBoolean(response, ['deviceValid'])
    message.value = messageSuccess.value ? 'Kiosk device readiness passed.' : 'Kiosk device readiness failed.'
  } catch (caught) {
    messageSuccess.value = false
    message.value = caught instanceof Error ? caught.message : 'Unable to check kiosk readiness.'
  } finally {
    checkingReadiness.value = false
  }
}

async function bootstrapDevice() {
  bootstrapping.value = true
  message.value = ''
  try {
    const response = await post<ApiRecord>('api/attendance/kiosk/bootstrap', {
      deviceId: form.deviceId || null,
      deviceCode: form.deviceCode || null,
      deviceToken: form.deviceToken || null
    })
    bootstrap.value = response
    messageSuccess.value = readBoolean(response, ['deviceValid'])
    message.value = readText(response, ['message'], 'Kiosk bootstrap checked.')
    if (readText(response, ['deviceId'], '') !== '-') form.deviceId = readText(response, ['deviceId'], form.deviceId)
    if (readText(response, ['deviceCode'], '') !== '-') form.deviceCode = readText(response, ['deviceCode'], form.deviceCode)
  } catch (caught) {
    messageSuccess.value = false
    message.value = caught instanceof Error ? caught.message : 'Unable to bootstrap kiosk device.'
  } finally {
    bootstrapping.value = false
  }
}

async function lookupEmployee() {
  if (!canLookup.value) return
  lookupLoading.value = true
  message.value = ''
  try {
    const response = await post<ApiRecord[]>('api/attendance/kiosk/lookup-employee', {
      deviceId: parseDeviceId(),
      deviceToken: form.deviceToken,
      search: lookupTerm.value
    })
    employees.value = Array.isArray(response) ? response : []
    messageSuccess.value = true
    message.value = `${employees.value.length} employee row(s) returned.`
  } catch (caught) {
    employees.value = []
    messageSuccess.value = false
    message.value = caught instanceof Error ? caught.message : 'Unable to lookup employee.'
  } finally {
    lookupLoading.value = false
  }
}
</script>
