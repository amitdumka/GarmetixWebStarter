<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const feedback = useUiFeedback()
const isAuthenticated = auth.isAuthenticated

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const code = ref('')
const resolving = ref(false)
const result = ref<any | null>(null)
const cameraActive = ref(false)
const cameraSupported = ref(false)
const videoRef = ref<HTMLVideoElement | null>(null)
let cameraStream: MediaStream | null = null
let scanTimer: ReturnType<typeof setInterval> | undefined

async function refreshShell() {
  if (!auth.isAuthenticated.value) return
  const [companyRows, storeRows] = await Promise.all([
    api.list<any>('companies'),
    api.list<any>('stores')
  ])
  companies.value = companyRows
  stores.value = storeRows
}

async function resolveCode(scannedCode?: string) {
  const value = String(scannedCode || code.value).trim()
  if (!value) {
    feedback.notify('Scan code required', 'Scan a QR/barcode or enter the printed document code.', 'warning')
    return
  }

  resolving.value = true
  result.value = null
  try {
    result.value = await api.get<any>(`scan/${encodeURIComponent(value)}`)
    code.value = value
    stopCamera()
  } catch (error) {
    feedback.failed('Document was not found', error)
  } finally {
    resolving.value = false
  }
}

async function startCamera() {
  if (!cameraSupported.value || !navigator.mediaDevices?.getUserMedia) {
    feedback.notify('Camera scanner unavailable', 'Use a USB scanner or enter the printed code manually.', 'warning')
    return
  }

  try {
    cameraStream = await navigator.mediaDevices.getUserMedia({
      video: { facingMode: { ideal: 'environment' } },
      audio: false
    })
    cameraActive.value = true
    await nextTick()
    if (videoRef.value) {
      videoRef.value.srcObject = cameraStream
      await videoRef.value.play()
    }

    const Detector = (window as any).BarcodeDetector
    const detector = new Detector({ formats: ['qr_code', 'code_128', 'code_39', 'ean_13', 'ean_8'] })
    scanTimer = window.setInterval(async () => {
      if (!videoRef.value || videoRef.value.readyState < 2) return
      try {
        const values = await detector.detect(videoRef.value)
        const scanned = values?.[0]?.rawValue
        if (scanned) await resolveCode(scanned)
      } catch {
        // Some browsers briefly fail detection while the camera is focusing.
      }
    }, 650)
  } catch (error) {
    stopCamera()
    feedback.failed('Camera could not be started', error)
  }
}

function stopCamera() {
  if (scanTimer) {
    clearInterval(scanTimer)
    scanTimer = undefined
  }
  cameraStream?.getTracks().forEach(track => track.stop())
  cameraStream = null
  cameraActive.value = false
  if (videoRef.value) videoRef.value.srcObject = null
}

function openDocument() {
  if (result.value?.url) navigateTo(result.value.url)
}

function formatDate(value: string) {
  return value ? new Date(value).toLocaleDateString('en-IN') : '-'
}

function money(value: number) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR' }).format(Number(value || 0))
}

onMounted(async () => {
  auth.restore()
  cameraSupported.value = typeof window !== 'undefined' && 'BarcodeDetector' in window
  await refreshShell()
})

onBeforeUnmount(stopCamera)
</script>

<template>
  <AuthScreen v-if="!isAuthenticated" @authenticated="refreshShell" />

  <AppShell
    v-else
    title="Document Scanner"
    :companies="companies"
    :stores="stores"
    @refresh="refreshShell"
  >
    <section class="planner-dashboard">
      <UiModulePageHeader
        title="Document Scanner"
        description="Find a permitted invoice, voucher, note, petty-cash sheet or payroll document from its printed QR or barcode."
        icon="i-lucide-scan-qr-code"
        primary-label="Start Camera"
        primary-icon="i-lucide-camera"
        @primary="startCamera"
      >
        <template #actions>
          <UBadge :color="cameraActive ? 'success' : 'neutral'" variant="subtle">
            {{ cameraActive ? 'Camera active' : 'Scanner ready' }}
          </UBadge>
          <UButton
            v-if="cameraActive"
            color="neutral"
            variant="subtle"
            icon="i-lucide-camera-off"
            label="Stop"
            @click="stopCamera"
          />
        </template>
      </UiModulePageHeader>

      <div class="document-scan-layout">
        <UCard class="planner-card document-scan-input">
          <template #header>
            <div class="planner-card-header">
              <div>
                <h2>Scan or Enter Code</h2>
                <p>USB barcode scanners can type directly into this field and submit with Enter.</p>
              </div>
              <UIcon name="i-lucide-qr-code" class="size-6 text-primary" />
            </div>
          </template>

          <form class="document-code-form" @submit.prevent="resolveCode()">
            <UInput
              v-model="code"
              size="xl"
              icon="i-lucide-scan-line"
              autocomplete="off"
              autofocus
              placeholder="GMX:VOUCHER:... or document number"
            />
            <UButton type="submit" size="xl" icon="i-lucide-search" label="Find Document" :loading="resolving" />
          </form>

          <UAlert
            class="mt-4"
            color="info"
            variant="subtle"
            icon="i-lucide-shield-check"
            title="Permission-aware lookup"
            description="A scanned document opens only when it belongs to your permitted company or store workspace."
          />
        </UCard>

        <UCard v-if="cameraActive" class="planner-card scanner-camera-card">
          <video ref="videoRef" muted playsinline />
          <div class="scanner-guide" aria-hidden="true" />
        </UCard>

        <UCard v-if="result" class="planner-card document-result-card">
          <template #header>
            <div class="planner-card-header">
              <div>
                <UBadge color="success" variant="subtle">{{ result.entityType }}</UBadge>
                <h2>{{ result.number }}</h2>
              </div>
              <UButton icon="i-lucide-arrow-up-right" label="Open Entry" @click="openDocument" />
            </div>
          </template>

          <div class="document-result-grid">
            <div><span>Date</span><strong>{{ formatDate(result.onDate) }}</strong></div>
            <div><span>Party / Employee</span><strong>{{ result.partyName || '-' }}</strong></div>
            <div><span>Amount</span><strong>{{ money(result.amount) }}</strong></div>
            <div><span>Code</span><strong class="document-code">{{ result.code }}</strong></div>
          </div>
        </UCard>
      </div>
    </section>
  </AppShell>
</template>

<style scoped>
.document-scan-layout {
  display: grid;
  gap: 1rem;
}

.document-code-form {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto;
  gap: 0.75rem;
}

.scanner-camera-card {
  position: relative;
  overflow: hidden;
  min-height: 280px;
  padding: 0;
}

.scanner-camera-card video {
  display: block;
  width: 100%;
  max-height: 520px;
  object-fit: cover;
}

.scanner-guide {
  position: absolute;
  inset: 18% 24%;
  border: 3px solid rgb(45 212 191);
  box-shadow: 0 0 0 999px rgb(2 8 23 / 0.42);
}

.document-result-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 1rem;
}

.document-result-grid div {
  display: grid;
  gap: 0.25rem;
}

.document-result-grid span {
  color: var(--ui-text-muted);
  font-size: 0.75rem;
}

.document-code {
  overflow-wrap: anywhere;
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
}

@media (max-width: 720px) {
  .document-code-form,
  .document-result-grid {
    grid-template-columns: 1fr;
  }
}
</style>
