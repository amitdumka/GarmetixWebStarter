<script setup lang="ts">
const api = useGarmetixApi()
const reports = useAttendanceReports()
const feedback = useUiFeedback()

const loading = ref(false)
const saving = ref(false)
const revoking = ref('')
const bridgeEnrolling = ref('')
const rows = ref<any[]>([])
const employees = ref<any[]>([])
const bridgeBaseUrl = ref('http://127.0.0.1:8787/garmetix-fingerprint/')
const lastBridgeResult = ref<any | null>(null)
const revokeReasons = reactive<Record<string, string>>({})
const bridgeUrlKey = 'garmetix.attendance.enrollment.bridgeBaseUrl.v1'

const form = reactive({
  id: '',
  employeeId: '',
  consentGiven: true,
  consentMethod: 'Written',
  consentReference: '',
  fingerprintTemplateRef: '',
  faceTemplateRef: '',
  webAuthnCredentialId: '',
  templateProvider: '',
  deviceSerial: '',
  notes: ''
})

const consentMethods = [
  { label: 'Written', value: 'Written' },
  { label: 'Digital', value: 'Digital' },
  { label: 'Manager verified', value: 'ManagerVerified' }
]

const employeeOptions = computed(() => employees.value.map((employee) => ({
  label: `${employee.employeeCode || 'EMP'} - ${[employee.firstName, employee.lastName].filter(Boolean).join(' ') || employee.fullName || 'Employee'}`,
  value: employee.id
})))

const selectedEmployee = computed(() => employees.value.find((employee) => employee.id === form.employeeId))
const activeRows = computed(() => rows.value.filter((row) => !row.revokedAtUtc))
const revokedRows = computed(() => rows.value.filter((row) => row.revokedAtUtc))

const summaryCards = computed(() => [
  { label: 'Active', value: activeRows.value.length, detail: 'usable references', icon: 'i-lucide-fingerprint' },
  { label: 'Consent', value: rows.value.filter((row) => row.consentGiven).length, detail: 'captured records', icon: 'i-lucide-badge-check' },
  { label: 'Revoked', value: revokedRows.value.length, detail: 'blocked records', icon: 'i-lucide-shield-x' },
  { label: 'Raw Storage', value: 'Blocked', detail: 'references only', icon: 'i-lucide-shield-check' }
])

async function refresh() {
  loading.value = true
  try {
    const [enrollmentRows, employeeRows] = await Promise.all([
      api.list<any>('attendance/biometric-enrollments'),
      api.list<any>('employees')
    ])
    rows.value = enrollmentRows
    employees.value = employeeRows
  } catch (error: any) {
    feedback.fromError('Biometric enrollment refresh failed', error)
  } finally {
    loading.value = false
  }
}

function loadBridgeUrl() {
  if (!import.meta.client) return
  bridgeBaseUrl.value = localStorage.getItem(bridgeUrlKey) || bridgeBaseUrl.value
}

function resetForm() {
  Object.assign(form, {
    id: '',
    employeeId: '',
    consentGiven: true,
    consentMethod: 'Written',
    consentReference: '',
    fingerprintTemplateRef: '',
    faceTemplateRef: '',
    webAuthnCredentialId: '',
    templateProvider: '',
    deviceSerial: '',
    notes: ''
  })
}

function editRow(row: any) {
  Object.assign(form, {
    id: row.id,
    employeeId: row.employeeId,
    consentGiven: Boolean(row.consentGiven),
    consentMethod: extractNoteValue(row.notes, 'Consent') || 'Written',
    consentReference: extractNoteValue(row.notes, 'ConsentRef') || '',
    fingerprintTemplateRef: row.fingerprintTemplateRef || '',
    faceTemplateRef: row.faceTemplateRef || '',
    webAuthnCredentialId: row.webAuthnCredentialId || '',
    templateProvider: extractNoteValue(row.notes, 'Provider') || '',
    deviceSerial: extractNoteValue(row.notes, 'Device') || '',
    notes: extractNoteValue(row.notes, 'Note') || ''
  })
}

async function save() {
  if (!selectedEmployee.value) {
    feedback.notify('Select employee', 'Choose an employee before saving enrollment.', 'error')
    return
  }

  saving.value = true
  try {
    await api.create<any>('attendance/biometric-enrollments', {
      id: form.id || undefined,
      employeeId: selectedEmployee.value.id,
      companyId: selectedEmployee.value.companyId,
      storeGroupId: selectedEmployee.value.storeGroupId,
      storeId: selectedEmployee.value.storeId,
      consentGiven: form.consentGiven,
      consentMethod: form.consentMethod,
      consentReference: form.consentReference,
      fingerprintTemplateRef: form.fingerprintTemplateRef,
      faceTemplateRef: form.faceTemplateRef,
      webAuthnCredentialId: form.webAuthnCredentialId,
      templateProvider: form.templateProvider,
      deviceSerial: form.deviceSerial,
      notes: form.notes
    })
    feedback.success(form.id ? 'Enrollment updated' : 'Enrollment saved', 'Biometric consent/reference record saved.')
    await refresh()
    resetForm()
  } catch (error: any) {
    feedback.fromError('Biometric enrollment save failed', error)
  } finally {
    saving.value = false
  }
}

async function enrollFromBridge(mode: 'simulator' | 'external') {
  if (!selectedEmployee.value) {
    feedback.notify('Select employee', 'Choose an employee before bridge enrollment.', 'error')
    return
  }

  if (!form.consentGiven) {
    feedback.notify('Consent required', 'Capture employee consent before enrolling fingerprint reference.', 'error')
    return
  }

  bridgeEnrolling.value = mode
  try {
    if (import.meta.client) {
      localStorage.setItem(bridgeUrlKey, bridgeBaseUrl.value)
    }

    const employee = selectedEmployee.value
    const payload = {
      employeeId: employee.id,
      employeeCode: employee.employeeCode,
      employeeName: [employee.firstName, employee.lastName].filter(Boolean).join(' ') || employee.fullName || employee.employeeCode,
      companyId: employee.companyId,
      storeGroupId: employee.storeGroupId,
      storeId: employee.storeId
    }

    const result = mode === 'simulator'
      ? await reports.deviceBridgeSimulatorEnroll(payload)
      : await reports.deviceBridgeExternalEnroll({ bridgeBaseUrl: bridgeBaseUrl.value, ...payload })

    lastBridgeResult.value = result
    if (!result?.success) {
      feedback.notify('Bridge enrollment did not complete', result?.message || 'Check Message Logs for sanitized details.', 'warning')
      return
    }

    if (result.rawPayloadStored) {
      feedback.notify('Bridge response blocked', 'Bridge reported raw biometric payload storage. Do not save this enrollment.', 'error')
      return
    }

    if (!result.templateRef) {
      feedback.notify('Template reference missing', 'Bridge enroll completed but did not return a template reference.', 'warning')
      return
    }

    form.fingerprintTemplateRef = result.templateRef
    form.templateProvider = result.vendor || (mode === 'external' ? 'Mantra bridge' : 'Simulator bridge')
    form.deviceSerial = result.deviceSerial || form.deviceSerial
    form.consentReference = form.consentReference || `Bridge audit ${result.auditRef}`
    form.notes = compactNote(`Bridge ${mode} enroll ${result.matchStatus || 'Enrolled'} quality ${result.qualityScore || 0}; audit ${result.auditRef}`)
    feedback.success('Bridge enrollment captured', 'Template reference copied into the enrollment form. Review and save.')
  } catch (error: any) {
    feedback.fromError('Bridge enrollment failed', error)
  } finally {
    bridgeEnrolling.value = ''
  }
}

async function revoke(row: any) {
  revoking.value = row.id
  try {
    await api.create<any>(`attendance/biometric-enrollments/${row.id}/revoke`, {
      remarks: revokeReasons[row.id] || 'Revoked from biometric enrollment page'
    })
    feedback.success('Enrollment revoked', 'Biometric reference disabled for this employee.')
    revokeReasons[row.id] = ''
    await refresh()
    if (form.id === row.id) resetForm()
  } catch (error: any) {
    feedback.fromError('Biometric enrollment revoke failed', error)
  } finally {
    revoking.value = ''
  }
}

function extractNoteValue(notes: string | null | undefined, key: string) {
  if (!notes) return ''
  const match = notes.match(new RegExp(`${key}:\\s*([^;]+)`, 'i'))
  return match?.[1]?.trim() || ''
}

function formatDate(value: string | null | undefined) {
  if (!value) return '-'
  return new Date(value).toLocaleString()
}

function compactNote(value: string) {
  return value.length <= 260 ? value : value.slice(0, 260)
}

onMounted(() => {
  loadBridgeUrl()
  void refresh()
})
</script>

<template>
  <AppShell title="Biometric Enrollment" @refresh="refresh">
    <section class="space-y-5">
      <UiModulePageHeader
        title="Biometric Enrollment"
        description="Consent and safe biometric reference management for attendance devices."
        icon="i-lucide-fingerprint"
        :loading="loading"
      >
        <template #actions>
          <UButton to="/attendance/device-bridge" icon="i-lucide-usb" label="Bridge" color="neutral" variant="subtle" />
          <UButton icon="i-lucide-plus" label="New" color="neutral" variant="subtle" @click="resetForm" />
          <UButton icon="i-lucide-refresh-cw" label="Refresh" :loading="loading" @click="refresh" />
        </template>
      </UiModulePageHeader>

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

      <div class="grid gap-4 xl:grid-cols-[minmax(0,0.95fr)_minmax(0,1.35fr)]">
        <UCard>
          <template #header>
            <div>
              <h2 class="text-lg font-semibold">{{ form.id ? 'Edit Enrollment' : 'New Enrollment' }}</h2>
              <p class="text-sm text-muted">Template fields accept references only.</p>
            </div>
          </template>

          <div class="space-y-4">
            <UFormField label="Employee" required>
              <USelect v-model="form.employeeId" :items="employeeOptions" placeholder="Select employee" />
            </UFormField>

            <div class="rounded-lg border border-default p-3">
              <div class="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <p class="text-sm font-semibold">Mantra enrollment bridge</p>
                  <p class="text-xs text-muted">Use simulator for dry run or external bridge after Mantra service is installed.</p>
                </div>
                <UBadge color="primary" variant="soft">Mantra selected</UBadge>
              </div>
              <div class="mt-3 space-y-3">
                <UFormField label="Bridge base URL">
                  <UInput v-model="bridgeBaseUrl" placeholder="http://127.0.0.1:8787/garmetix-fingerprint/" />
                </UFormField>
                <div class="grid gap-2 sm:grid-cols-2">
                  <UButton icon="i-lucide-flask-conical" label="Simulator Enroll" color="neutral" variant="subtle" :loading="bridgeEnrolling === 'simulator'" @click="enrollFromBridge('simulator')" />
                  <UButton icon="i-lucide-fingerprint" label="Mantra Bridge Enroll" :loading="bridgeEnrolling === 'external'" @click="enrollFromBridge('external')" />
                </div>
                <UAlert
                  v-if="lastBridgeResult"
                  :color="lastBridgeResult.success ? 'success' : 'warning'"
                  variant="soft"
                  :title="lastBridgeResult.success ? 'Last bridge enroll ready' : 'Last bridge enroll blocked'"
                  :description="lastBridgeResult.message"
                />
              </div>
            </div>

            <div class="grid gap-3 md:grid-cols-2">
              <UFormField label="Consent">
                <UCheckbox v-model="form.consentGiven" label="Consent given" />
              </UFormField>
              <UFormField label="Consent method">
                <USelect v-model="form.consentMethod" :items="consentMethods" />
              </UFormField>
            </div>

            <UFormField label="Consent reference">
              <UInput v-model="form.consentReference" placeholder="Form no, approval ref, or note" />
            </UFormField>

            <div class="grid gap-3 md:grid-cols-2">
              <UFormField label="Template provider">
                <UInput v-model="form.templateProvider" placeholder="Vendor or bridge name" />
              </UFormField>
              <UFormField label="Device serial">
                <UInput v-model="form.deviceSerial" placeholder="Scanner/device serial" />
              </UFormField>
            </div>

            <UFormField label="Fingerprint template reference">
              <UInput v-model="form.fingerprintTemplateRef" placeholder="Vendor-approved reference only" />
            </UFormField>
            <UFormField label="Face template reference">
              <UInput v-model="form.faceTemplateRef" placeholder="Reference only" />
            </UFormField>
            <UFormField label="WebAuthn credential ID">
              <UInput v-model="form.webAuthnCredentialId" placeholder="Credential reference" />
            </UFormField>
            <UFormField label="Notes">
              <UTextarea v-model="form.notes" autoresize />
            </UFormField>

            <UAlert
              color="warning"
              variant="soft"
              icon="i-lucide-shield-alert"
              title="Raw biometric payloads are blocked"
              description="Do not paste fingerprint image, WSQ, minutiae, ISO template or base64 biometric payload into these fields."
            />

            <div class="flex flex-wrap justify-end gap-2">
              <UButton label="Reset" color="neutral" variant="subtle" @click="resetForm" />
              <UButton icon="i-lucide-save" label="Save Enrollment" :loading="saving" @click="save" />
            </div>
          </div>
        </UCard>

        <div class="space-y-4">
          <UCard>
            <template #header>
              <div class="flex flex-wrap items-center justify-between gap-3">
                <div>
                  <h2 class="text-lg font-semibold">Current Enrollments</h2>
                  <p class="text-sm text-muted">Latest 500 records in your workspace.</p>
                </div>
                <UBadge color="primary" variant="soft">{{ rows.length }}</UBadge>
              </div>
            </template>

            <div v-if="rows.length" class="grid gap-3">
              <div v-for="row in rows" :key="row.id" class="rounded-lg border border-default p-3">
                <div class="flex flex-wrap items-start justify-between gap-3">
                  <div class="min-w-0">
                    <div class="flex flex-wrap items-center gap-2">
                      <p class="font-semibold">{{ row.employeeName }}</p>
                      <UBadge color="neutral" variant="soft">{{ row.employeeCode }}</UBadge>
                      <UBadge :color="row.revokedAtUtc ? 'error' : row.enrollmentStatus === 'Enrolled' ? 'success' : 'warning'" variant="soft">
                        {{ row.enrollmentStatus }}
                      </UBadge>
                    </div>
                    <p class="mt-1 text-xs text-muted">Consent {{ row.consentGiven ? 'Yes' : 'No' }} - {{ formatDate(row.consentAtUtc) }}</p>
                  </div>
                  <div class="flex flex-wrap gap-2">
                    <UButton size="sm" icon="i-lucide-pencil" label="Edit" color="neutral" variant="subtle" @click="editRow(row)" />
                  </div>
                </div>

                <div class="mt-3 grid gap-2 md:grid-cols-3">
                  <div class="rounded-lg border border-default p-2">
                    <p class="text-xs text-muted">Fingerprint Ref</p>
                    <p class="truncate text-sm">{{ row.fingerprintTemplateRef || '-' }}</p>
                  </div>
                  <div class="rounded-lg border border-default p-2">
                    <p class="text-xs text-muted">Face Ref</p>
                    <p class="truncate text-sm">{{ row.faceTemplateRef || '-' }}</p>
                  </div>
                  <div class="rounded-lg border border-default p-2">
                    <p class="text-xs text-muted">WebAuthn</p>
                    <p class="truncate text-sm">{{ row.webAuthnCredentialId || '-' }}</p>
                  </div>
                </div>

                <div v-if="row.auditFlags?.length" class="mt-3 flex flex-wrap gap-2">
                  <UBadge v-for="flag in row.auditFlags" :key="flag" color="neutral" variant="soft">{{ flag }}</UBadge>
                </div>

                <div v-if="!row.revokedAtUtc" class="mt-3 grid gap-2 md:grid-cols-[minmax(0,1fr)_auto]">
                  <UInput v-model="revokeReasons[row.id]" placeholder="Revocation reason" />
                  <UButton size="sm" icon="i-lucide-ban" label="Revoke" color="error" variant="subtle" :loading="revoking === row.id" @click="revoke(row)" />
                </div>
                <UAlert v-else class="mt-3" color="error" variant="soft" title="Revoked" :description="row.revokedReason || 'No reason recorded.'" />
              </div>
            </div>
            <UAlert v-else color="neutral" variant="soft" title="No biometric enrollment records" description="Save an employee consent/reference record to begin." />
          </UCard>
        </div>
      </div>
    </section>
  </AppShell>
</template>
