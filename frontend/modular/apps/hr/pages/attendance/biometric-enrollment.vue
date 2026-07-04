<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 xl:flex-row xl:items-start xl:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-fingerprint" class="size-4" /> Biometric enrollment</p>
          <h2 class="garmetix-dashboard-title">Biometric Enrollment</h2>
          <p class="garmetix-dashboard-subtitle">
            Save consent and safe reference IDs only. Raw fingerprint or face payloads are blocked by backend validation.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton icon="i-lucide-cable" color="primary" variant="soft" to="/attendance/device-bridge">Device Bridge</UButton>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="load">Refresh</UButton>
        </div>
      </div>
    </div>

    <UAlert color="warning" variant="subtle" icon="i-lucide-shield-alert" description="Mantra hardware capture remains a bridge action. This page stores only consent, template references and audit notes." />
    <UAlert v-if="message" :color="messageTone" variant="subtle" :icon="messageIcon" :description="message" />

    <div class="grid gap-3 md:grid-cols-4">
      <div v-for="card in cards" :key="card.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label">{{ card.label }}</p>
        <p class="garmetix-metric-value">{{ card.value }}</p>
        <p class="garmetix-metric-caption">{{ card.caption }}</p>
      </div>
    </div>

    <div class="grid gap-4 xl:grid-cols-[420px_minmax(0,1fr)]">
      <div class="garmetix-table-panel">
        <div class="garmetix-panel-header">
          <div>
            <h3 class="garmetix-panel-title">{{ form.id ? 'Update Enrollment' : 'New Enrollment' }}</h3>
            <p class="garmetix-panel-subtitle">Consent is mandatory before a biometric reference can be saved.</p>
          </div>
        </div>
        <div class="grid gap-3">
          <UFormField label="Employee" required>
            <USelect v-model="form.employeeId" :items="employeeOptions" placeholder="Select employee" />
          </UFormField>
          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Store">
              <USelect v-model="form.storeId" :items="storeOptions" placeholder="Select store" />
            </UFormField>
            <UFormField label="Provider">
              <UInput v-model="form.templateProvider" placeholder="Mantra / Simulator" />
            </UFormField>
          </div>
          <UCheckbox v-model="form.consentGiven" label="Employee consent captured" />
          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Consent method"><UInput v-model="form.consentMethod" placeholder="Written / OTP / Form" /></UFormField>
            <UFormField label="Consent reference"><UInput v-model="form.consentReference" placeholder="Consent form/audit ref" /></UFormField>
          </div>
          <UFormField label="Fingerprint template reference">
            <UInput v-model="form.fingerprintTemplateRef" placeholder="safe-template-ref-only" />
          </UFormField>
          <UFormField label="Face template reference">
            <UInput v-model="form.faceTemplateRef" placeholder="safe-face-ref-only" />
          </UFormField>
          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="WebAuthn credential"><UInput v-model="form.webAuthnCredentialId" /></UFormField>
            <UFormField label="Device serial"><UInput v-model="form.deviceSerial" placeholder="Mantra serial / kiosk id" /></UFormField>
          </div>
          <UFormField label="Notes"><UTextarea v-model="form.notes" autoresize /></UFormField>
          <UFormField label="Type ENROLL to enable save">
            <UInput v-model="confirmText" placeholder="ENROLL" />
          </UFormField>
          <div class="flex flex-wrap gap-2">
            <UButton icon="i-lucide-save" :disabled="!canSave" :loading="saving" @click="saveEnrollment">Save Enrollment</UButton>
            <UButton color="neutral" variant="soft" @click="resetForm">Clear</UButton>
          </div>
        </div>
      </div>

      <div class="garmetix-table-panel overflow-hidden">
        <div class="garmetix-panel-header">
          <div>
            <h3 class="garmetix-panel-title">Enrollment Register</h3>
            <p class="garmetix-panel-subtitle">{{ rows.length }} row(s)</p>
          </div>
        </div>
        <div class="overflow-auto">
          <table class="w-full min-w-[1080px] text-left text-sm">
            <thead class="bg-muted/30 text-xs uppercase text-muted">
              <tr>
                <th class="px-3 py-2">Employee</th>
                <th class="px-3 py-2">Consent</th>
                <th class="px-3 py-2">Status</th>
                <th class="px-3 py-2">Fingerprint</th>
                <th class="px-3 py-2">Face</th>
                <th class="px-3 py-2">Enrolled</th>
                <th class="px-3 py-2">Flags</th>
                <th class="px-3 py-2 text-right">Action</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(row, index) in rows" :key="readText(row, ['id'], String(index))" class="border-t border-default">
                <td class="px-3 py-2 font-medium">{{ readText(row, ['employeeName']) }}<br><span class="text-xs text-muted">{{ readText(row, ['employeeCode']) }}</span></td>
                <td class="px-3 py-2">{{ readBoolean(row, ['consentGiven']) ? 'Yes' : 'No' }}</td>
                <td class="px-3 py-2"><UBadge :color="statusTone(row)" variant="subtle">{{ readText(row, ['enrollmentStatus']) }}</UBadge></td>
                <td class="px-3 py-2">{{ shortRef(readText(row, ['fingerprintTemplateRef'], '')) }}</td>
                <td class="px-3 py-2">{{ shortRef(readText(row, ['faceTemplateRef'], '')) }}</td>
                <td class="px-3 py-2">{{ dateText(readText(row, ['enrolledAtUtc'], '')) }}</td>
                <td class="px-3 py-2">{{ flags(row).join(', ') }}</td>
                <td class="px-3 py-2">
                  <div class="flex justify-end gap-1">
                    <UButton size="xs" icon="i-lucide-pencil" color="neutral" variant="soft" @click="editRow(row)">Edit</UButton>
                    <UButton size="xs" icon="i-lucide-ban" color="error" variant="soft" :disabled="isRevoked(row)" @click="revokeRow(row)">Revoke</UButton>
                  </div>
                </td>
              </tr>
              <tr v-if="!rows.length">
                <td class="px-3 py-6 text-center text-muted" colspan="8">No biometric enrollment rows returned.</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { readArray, readBoolean, readText, type ApiRecord, useHrApiClient } from '../../utils/hr-api'

useHead({ title: 'Biometric Enrollment - Garmetix HR' })

const { get, post } = useHrApiClient()
const loading = ref(false)
const saving = ref(false)
const message = ref('')
const messageTone = ref<'info' | 'success' | 'warning' | 'error'>('info')
const rows = ref<ApiRecord[]>([])
const employees = ref<ApiRecord[]>([])
const stores = ref<ApiRecord[]>([])
const confirmText = ref('')
const form = reactive(emptyForm())

const messageIcon = computed(() => messageTone.value === 'success' ? 'i-lucide-circle-check' : messageTone.value === 'warning' ? 'i-lucide-triangle-alert' : messageTone.value === 'error' ? 'i-lucide-circle-alert' : 'i-lucide-info')
const employeeOptions = computed(() => employees.value.map(employee => ({ value: readText(employee, ['id'], ''), label: employeeName(employee) })))
const storeOptions = computed(() => stores.value.map(store => ({ value: readText(store, ['id'], ''), label: `${readText(store, ['storeName', 'name'])} | ${readText(store, ['storeCode', 'code'])}`.replace(/\s+\|\s+-$/, '') })))
const selectedStore = computed(() => stores.value.find(store => readText(store, ['id'], '') === form.storeId) ?? stores.value[0] ?? null)
const canSave = computed(() => Boolean(form.employeeId && form.storeId && form.consentGiven && confirmText.value.trim().toUpperCase() === 'ENROLL') && !saving.value)
const cards = computed(() => [
  { label: 'Enrollments', value: rows.value.length, caption: 'Loaded employee biometric rows' },
  { label: 'Active', value: rows.value.filter(row => !isRevoked(row)).length, caption: 'Not revoked' },
  { label: 'Fingerprint Ref', value: rows.value.filter(row => readText(row, ['fingerprintTemplateRef'], '') !== '-').length, caption: 'Safe references only' },
  { label: 'Raw Storage', value: 'Blocked', caption: 'Backend blocks raw payload markers' }
])

function emptyForm() {
  return {
    id: '',
    employeeId: '',
    storeId: '',
    consentGiven: false,
    consentMethod: 'Written',
    consentReference: '',
    fingerprintTemplateRef: '',
    faceTemplateRef: '',
    webAuthnCredentialId: '',
    templateProvider: 'Mantra',
    deviceSerial: '',
    notes: ''
  }
}

function employeeName(employee: ApiRecord) {
  const name = readText(employee, ['staffName', 'fullName', 'name'], '')
  if (name && name !== '-') return name
  return `${readText(employee, ['firstName'], '')} ${readText(employee, ['lastName'], '')}`.trim() || readText(employee, ['employeeCode'], '')
}

function showMessage(text: string, tone: typeof messageTone.value = 'success') {
  message.value = text
  messageTone.value = tone
}

function flags(row: ApiRecord) {
  return readArray(row, ['auditFlags']).map(item => typeof item === 'string' ? item : JSON.stringify(item))
}

function isRevoked(row: ApiRecord) {
  return readText(row, ['enrollmentStatus'], '').toLowerCase().includes('revoked') || readText(row, ['revokedAtUtc'], '') !== '-'
}

function statusTone(row: ApiRecord) {
  return isRevoked(row) ? 'error' : readBoolean(row, ['consentGiven']) ? 'success' : 'warning'
}

function dateText(value: string) {
  if (!value || value === '-') return '-'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString('en-IN')
}

function shortRef(value: string) {
  return !value || value === '-' ? '-' : value.length > 24 ? `${value.slice(0, 24)}...` : value
}

function resetForm() {
  Object.assign(form, emptyForm())
  if (stores.value.length) form.storeId = readText(stores.value[0], ['id'], '')
  confirmText.value = ''
}

function editRow(row: ApiRecord) {
  Object.assign(form, {
    id: readText(row, ['id'], ''),
    employeeId: readText(row, ['employeeId'], ''),
    storeId: readText(row, ['storeId'], ''),
    consentGiven: readBoolean(row, ['consentGiven']),
    consentMethod: 'Written',
    consentReference: '',
    fingerprintTemplateRef: readText(row, ['fingerprintTemplateRef'], '') === '-' ? '' : readText(row, ['fingerprintTemplateRef'], ''),
    faceTemplateRef: readText(row, ['faceTemplateRef'], '') === '-' ? '' : readText(row, ['faceTemplateRef'], ''),
    webAuthnCredentialId: readText(row, ['webAuthnCredentialId'], '') === '-' ? '' : readText(row, ['webAuthnCredentialId'], ''),
    templateProvider: 'Mantra',
    deviceSerial: '',
    notes: readText(row, ['notes'], '') === '-' ? '' : readText(row, ['notes'], '')
  })
}

async function load() {
  loading.value = true
  message.value = ''
  try {
    const [enrollmentRows, employeeRows, storeRows] = await Promise.all([
      get<ApiRecord[]>('api/attendance/biometric-enrollments'),
      get<ApiRecord[]>('api/employees'),
      get<ApiRecord[]>('api/stores')
    ])
    rows.value = Array.isArray(enrollmentRows) ? enrollmentRows : []
    employees.value = Array.isArray(employeeRows) ? employeeRows : []
    stores.value = Array.isArray(storeRows) ? storeRows : []
    if (!form.storeId && stores.value.length) form.storeId = readText(stores.value[0], ['id'], '')
  } catch (caught) {
    showMessage(caught instanceof Error ? caught.message : 'Unable to load biometric enrollments.', 'error')
  } finally {
    loading.value = false
  }
}

async function saveEnrollment() {
  if (!canSave.value) return
  saving.value = true
  try {
    const store = selectedStore.value
    await post<ApiRecord>('api/attendance/biometric-enrollments', {
      id: form.id || null,
      employeeId: form.employeeId,
      companyId: readText(store, ['companyId'], ''),
      storeGroupId: readText(store, ['storeGroupId'], ''),
      storeId: form.storeId,
      consentGiven: form.consentGiven,
      consentMethod: form.consentMethod || null,
      consentReference: form.consentReference || null,
      fingerprintTemplateRef: form.fingerprintTemplateRef || null,
      faceTemplateRef: form.faceTemplateRef || null,
      webAuthnCredentialId: form.webAuthnCredentialId || null,
      templateProvider: form.templateProvider || null,
      deviceSerial: form.deviceSerial || null,
      notes: form.notes || null
    })
    showMessage('Biometric enrollment reference saved.')
    resetForm()
    await load()
  } catch (caught) {
    showMessage(caught instanceof Error ? caught.message : 'Could not save biometric enrollment.', 'error')
  } finally {
    saving.value = false
  }
}

async function revokeRow(row: ApiRecord) {
  const id = readText(row, ['id'], '')
  if (!id || !window.confirm(`Revoke biometric enrollment for ${readText(row, ['employeeName'])}?`)) return
  try {
    await post<ApiRecord>(`api/attendance/biometric-enrollments/${id}/revoke`, { remarks: 'Revoked from modular HR biometric enrollment page.' })
    showMessage('Biometric enrollment revoked.')
    await load()
  } catch (caught) {
    showMessage(caught instanceof Error ? caught.message : 'Could not revoke biometric enrollment.', 'error')
  }
}

onMounted(load)
</script>
