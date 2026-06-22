<script setup lang="ts">
definePageMeta({ layout: false })

const devicesApi = useAttendanceDevices()
const feedback = useUiFeedback()
const deviceId = ref('')
const deviceToken = ref('')
const search = ref('')
const loading = ref(false)
const employees = ref<any[]>([])
const selectedEmployee = ref<any | null>(null)
const readiness = ref<any | null>(null)
const photoDataUrl = ref('')
const photoProofPath = ref('')
const fingerprintBridgeUrl = ref('http://127.0.0.1:8787/garmetix-fingerprint/')
const fingerprintProof = ref<any | null>(null)
const verifyingFingerprint = ref(false)
const videoEl = ref<HTMLVideoElement | null>(null)
const cameraStream = ref<MediaStream | null>(null)
const pendingQueue = ref<any[]>([])

const storageKey = 'garmetix.attendance.kiosk.v1'
const pendingKey = 'garmetix.attendance.kiosk.pending.v1'
const fingerprintKey = 'garmetix.attendance.kiosk.fingerprintBridge.v1'

const fingerprintRequired = computed(() => Boolean(readiness.value?.fingerprintPunchRequired))

onMounted(() => {
  const saved = JSON.parse(localStorage.getItem(storageKey) || '{}')
  deviceId.value = saved.deviceId || ''
  deviceToken.value = saved.deviceToken || ''
  fingerprintBridgeUrl.value = localStorage.getItem(fingerprintKey) || fingerprintBridgeUrl.value
  pendingQueue.value = JSON.parse(localStorage.getItem(pendingKey) || '[]')
})

function saveDevice() {
  localStorage.setItem(storageKey, JSON.stringify({ deviceId: deviceId.value, deviceToken: deviceToken.value }))
  feedback.success('Kiosk device saved', 'This browser is now configured for the selected store kiosk.')
}

function saveQueue() {
  localStorage.setItem(pendingKey, JSON.stringify(pendingQueue.value))
}

async function checkReadiness() {
  if (!deviceId.value || !deviceToken.value) return feedback.error('Missing device', 'Enter Device ID and Device Token from Kiosk Devices.')
  loading.value = true
  try {
    readiness.value = await devicesApi.kioskReadiness({ deviceId: deviceId.value, deviceToken: deviceToken.value })
    if (readiness.value?.fingerprintBridgeBaseUrl) fingerprintBridgeUrl.value = readiness.value.fingerprintBridgeBaseUrl
    saveDevice()
    feedback.success('Kiosk ready', readiness.value?.deviceCode || 'Device accepted')
  } catch (error: any) {
    feedback.fromError('Kiosk readiness failed', error)
  } finally {
    loading.value = false
  }
}

async function lookupEmployee() {
  if (search.value.trim().length < 2) return
  loading.value = true
  try {
    employees.value = await devicesApi.kioskLookupEmployee({ deviceId: deviceId.value, deviceToken: deviceToken.value, search: search.value })
  } catch (error: any) {
    feedback.fromError('Employee lookup failed', error)
  } finally {
    loading.value = false
  }
}

async function startCamera() {
  if (!navigator.mediaDevices?.getUserMedia) return feedback.error('Camera not available', 'Use HTTPS or localhost to allow kiosk camera capture.')
  cameraStream.value = await navigator.mediaDevices.getUserMedia({ video: { facingMode: 'user' }, audio: false })
  if (videoEl.value) videoEl.value.srcObject = cameraStream.value
}

function capturePhoto() {
  if (!videoEl.value) return
  const canvas = document.createElement('canvas')
  canvas.width = videoEl.value.videoWidth || 640
  canvas.height = videoEl.value.videoHeight || 480
  const context = canvas.getContext('2d')
  if (!context) return
  context.drawImage(videoEl.value, 0, 0, canvas.width, canvas.height)
  photoDataUrl.value = canvas.toDataURL('image/jpeg', 0.82)
  feedback.success('Photo captured', 'Photo proof is ready to upload with punch.')
}

async function uploadPhotoProof(clientPunchId: string) {
  if (!selectedEmployee.value || !photoDataUrl.value) return ''
  const result = await devicesApi.uploadPhotoProof({
    deviceId: deviceId.value,
    deviceToken: deviceToken.value,
    employeeId: selectedEmployee.value.id,
    dataUrl: photoDataUrl.value,
    clientPunchId,
    capturedAtUtc: new Date().toISOString(),
    remarks: 'Stage 9B kiosk photo proof only, no face recognition.'
  })
  photoProofPath.value = result.photoProofPath
  return result.photoProofPath
}

function bridgeEndpoint(path: string) {
  const base = fingerprintBridgeUrl.value.trim().replace(/\/+$/, '')
  return `${base}/${path.replace(/^\/+/, '')}`
}

function fingerprintProofIsFresh() {
  if (!fingerprintProof.value?.capturedAtUtc) return false
  const maxMinutes = Number(readiness.value?.fingerprintProofMaxAgeMinutes || 10)
  const capturedAt = new Date(fingerprintProof.value.capturedAtUtc).getTime()
  return Number.isFinite(capturedAt) && Date.now() - capturedAt <= maxMinutes * 60 * 1000
}

async function verifyFingerprint() {
  if (!selectedEmployee.value) {
    feedback.error('Select employee', 'Lookup and select employee before fingerprint verification.')
    return null
  }
  localStorage.setItem(fingerprintKey, fingerprintBridgeUrl.value)
  verifyingFingerprint.value = true
  fingerprintProof.value = null
  try {
    const response = await fetch(bridgeEndpoint('identify'), {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({
        employeeId: selectedEmployee.value.id,
        employeeCode: selectedEmployee.value.employeeCode,
        employeeName: selectedEmployee.value.fullName,
        companyId: selectedEmployee.value.companyId,
        storeGroupId: selectedEmployee.value.storeGroupId,
        storeId: selectedEmployee.value.storeId,
        rawPayloadAllowed: false
      })
    })
    const result = await response.json()
    const quality = Number(result.qualityScore || 0)
    const minQuality = Number(readiness.value?.fingerprintMinQualityScore || 60)
    const matched = ['matched', 'identified', 'accepted'].includes(String(result.matchStatus || '').toLowerCase())
    if (!response.ok || !result.success || result.rawPayloadStored || !matched || quality < minQuality) {
      feedback.error('Fingerprint not accepted', result.message || `Match and quality ${minQuality}+ are required.`)
      return null
    }
    fingerprintProof.value = {
      success: Boolean(result.success),
      matchStatus: result.matchStatus,
      employeeId: result.employeeId || selectedEmployee.value.id,
      employeeCode: result.employeeCode || selectedEmployee.value.employeeCode,
      templateRef: result.templateRef,
      qualityScore: quality,
      capturedAtUtc: result.capturedAtUtc || new Date().toISOString(),
      auditRef: result.auditRef,
      rawPayloadStored: Boolean(result.rawPayloadStored),
      warnings: result.warnings || [],
      vendor: result.vendor,
      deviceSerial: result.deviceSerial
    }
    feedback.success('Fingerprint verified', `${result.matchStatus || 'Matched'} with quality ${quality}.`)
    return fingerprintProof.value
  } catch (error: any) {
    feedback.fromError('Fingerprint bridge failed', error)
    return null
  } finally {
    verifyingFingerprint.value = false
  }
}

async function punch(punchType = 'Auto') {
  if (!selectedEmployee.value) return feedback.error('Select employee', 'Lookup and select employee first.')
  const clientPunchId = `KIOSK-${Date.now()}-${Math.random().toString(36).slice(2)}`
  let proofPath = photoProofPath.value
  let proof = fingerprintProof.value
  try {
    if (fingerprintRequired.value && !fingerprintProofIsFresh()) {
      proof = await verifyFingerprint()
      if (!proof) return
    }
    if (photoDataUrl.value && !proofPath) proofPath = await uploadPhotoProof(clientPunchId)
    const body = {
      employeeId: selectedEmployee.value.id,
      punchType,
      punchTimeUtc: new Date().toISOString(),
      localPunchTime: new Date().toISOString(),
      source: photoDataUrl.value ? 'FacePhotoProof' : 'Kiosk',
      deviceId: deviceId.value,
      deviceToken: deviceToken.value,
      photoProofPath: proofPath,
      clientPunchId,
      companyId: selectedEmployee.value.companyId,
      storeGroupId: selectedEmployee.value.storeGroupId,
      storeId: selectedEmployee.value.storeId,
      fingerprintProof: proof,
      remarks: 'Stage 9B kiosk punch.'
    }
    const result = await devicesApi.kioskPunch(body)
    feedback.success('Attendance saved', result.message || 'Punch recorded')
    photoDataUrl.value = ''
    photoProofPath.value = ''
    fingerprintProof.value = null
  } catch (error: any) {
    if (fingerprintRequired.value && !readiness.value?.fingerprintOfflineQueueAllowed) {
      feedback.fromError('Punch not saved', error)
      return
    }
    const queued = {
      employeeId: selectedEmployee.value.id,
      punchType,
      punchTimeUtc: new Date().toISOString(),
      localPunchTime: new Date().toISOString(),
      source: photoDataUrl.value ? 'FacePhotoProof' : 'Kiosk',
      deviceId: deviceId.value,
      deviceToken: deviceToken.value,
      photoProofPath: proofPath,
      clientPunchId,
      companyId: selectedEmployee.value.companyId,
      storeGroupId: selectedEmployee.value.storeGroupId,
      storeId: selectedEmployee.value.storeId,
      fingerprintProof: proof,
      remarks: 'Queued by Stage 9B kiosk page after failed live punch.'
    }
    pendingQueue.value.push(queued)
    saveQueue()
    feedback.error('Punch queued offline', 'Live save failed, so this punch was kept in local browser queue.')
  }
}

async function syncPending() {
  if (!pendingQueue.value.length) return feedback.success('No pending punches', 'Offline queue is empty.')
  loading.value = true
  try {
    const result = await devicesApi.syncPending({ deviceId: deviceId.value, deviceToken: deviceToken.value, punches: pendingQueue.value })
    pendingQueue.value = pendingQueue.value.filter((_: any, index: number) => index >= result.accepted + result.duplicate)
    saveQueue()
    feedback.success('Sync completed', `Accepted ${result.accepted}, duplicate ${result.duplicate}, failed ${result.failed}.`)
  } catch (error: any) {
    feedback.fromError('Pending sync failed', error)
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="min-h-screen bg-gray-950 p-4 text-gray-50">
    <div class="mx-auto max-w-5xl space-y-4">
      <div class="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 class="text-2xl font-bold">Garmetix Attendance Kiosk</h1>
          <p class="text-sm text-gray-300">Stage 9B web kiosk with photo proof and offline queue foundation.</p>
        </div>
        <NuxtLink to="/attendance" class="text-sm text-primary-300">Back to Attendance</NuxtLink>
      </div>

      <UCard>
        <div class="grid gap-3 md:grid-cols-4">
          <UInput v-model="deviceId" placeholder="Device ID" />
          <UInput v-model="deviceToken" placeholder="Device Token" type="password" />
          <UButton label="Save Device" color="neutral" variant="subtle" @click="saveDevice" />
          <UButton label="Check Readiness" :loading="loading" @click="checkReadiness" />
        </div>
        <UAlert v-if="readiness" class="mt-3" color="success" title="Kiosk ready" :description="`${readiness.deviceCode} / ${readiness.deviceName}. Duplicate window ${readiness.duplicateWindowMinutes} minutes.`" />
      </UCard>

      <div class="grid gap-4 lg:grid-cols-2">
        <UCard>
          <template #header>Employee Lookup</template>
          <div class="flex gap-2">
            <UInput v-model="search" class="flex-1" placeholder="Employee code, mobile, or name" @keyup.enter="lookupEmployee" />
            <UButton label="Search" :loading="loading" @click="lookupEmployee" />
          </div>
          <div class="mt-3 space-y-2">
            <button v-for="employee in employees" :key="employee.id" class="w-full rounded-lg border border-gray-700 p-3 text-left hover:bg-gray-900" @click="selectedEmployee = employee">
              <div class="font-semibold">{{ employee.fullName }}</div>
              <div class="text-xs text-gray-400">{{ employee.employeeCode }} · {{ employee.mobile }} · {{ employee.designation }}</div>
            </button>
          </div>
          <UAlert v-if="selectedEmployee" class="mt-3" color="primary" title="Selected employee" :description="`${selectedEmployee.fullName} (${selectedEmployee.employeeCode})`" />
        </UCard>

        <UCard>
          <template #header>Photo Proof</template>
          <div class="space-y-3">
            <video ref="videoEl" autoplay playsinline muted class="h-56 w-full rounded-lg bg-black object-cover" />
            <div class="flex flex-wrap gap-2">
              <UButton label="Start Camera" color="neutral" variant="subtle" @click="startCamera" />
              <UButton label="Capture Photo" @click="capturePhoto" />
            </div>
            <img v-if="photoDataUrl" :src="photoDataUrl" class="h-32 rounded border border-gray-700 object-cover" alt="Captured photo proof" />
            <p class="text-xs text-gray-400">Photo proof is saved only as attendance evidence. No face matching is performed in Stage 9B.</p>
          </div>
        </UCard>
      </div>

      <UCard>
        <template #header>Fingerprint Guard</template>
        <div class="grid gap-3 lg:grid-cols-[minmax(0,1fr)_auto]">
          <UInput v-model="fingerprintBridgeUrl" placeholder="http://127.0.0.1:8787/garmetix-fingerprint/" />
          <UButton label="Verify Fingerprint" icon="i-lucide-fingerprint" :loading="verifyingFingerprint" @click="verifyFingerprint" />
        </div>
        <div class="mt-3 grid gap-3 md:grid-cols-3">
          <UAlert
            :color="fingerprintRequired ? 'warning' : 'neutral'"
            :title="fingerprintRequired ? 'Required before punch' : 'Optional'"
            :description="`Mode ${readiness?.fingerprintVerificationMode || 'Off'}, min quality ${readiness?.fingerprintMinQualityScore || 60}.`"
          />
          <UAlert
            v-if="fingerprintProof"
            color="success"
            title="Fingerprint verified"
            :description="`${fingerprintProof.matchStatus} quality ${fingerprintProof.qualityScore}. Audit ${fingerprintProof.auditRef}`"
          />
          <UAlert
            v-if="readiness?.fingerprintRules?.length"
            color="info"
            title="Rules"
            :description="readiness.fingerprintRules.join(' ')"
          />
        </div>
      </UCard>

      <UCard>
        <div class="flex flex-wrap gap-2">
          <UButton label="Auto Punch" size="xl" @click="punch('Auto')" />
          <UButton label="Check In" size="xl" color="success" @click="punch('CheckIn')" />
          <UButton label="Check Out" size="xl" color="warning" @click="punch('CheckOut')" />
          <UButton label="Sync Pending" size="xl" color="neutral" variant="subtle" :loading="loading" @click="syncPending" />
        </div>
        <p class="mt-2 text-sm text-gray-400">Pending offline queue: {{ pendingQueue.length }}</p>
      </UCard>
    </div>
  </div>
</template>
