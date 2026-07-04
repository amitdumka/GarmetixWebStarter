<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-clipboard-check" class="size-4" /> Attendance correction queue</p>
          <h2 class="garmetix-dashboard-title">Regularization</h2>
          <p class="garmetix-dashboard-subtitle">
            Review missed punch and correction requests. Approve or reject records with an audit remark.
          </p>
        </div>
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="load">Refresh</UButton>
      </div>
    </div>

    <UAlert
      color="warning"
      variant="subtle"
      icon="i-lucide-shield-alert"
      title="Controlled attendance correction"
      description="Creating a request is an attendance correction entry. Approve and reject actions remain manager decisions and require an audit remark."
    />
    <UAlert v-if="message" :color="messageTone" variant="subtle" :icon="messageIcon" :description="message" />

    <div class="grid gap-3 md:grid-cols-4">
      <div v-for="card in cards" :key="card.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label">{{ card.label }}</p>
        <p class="garmetix-metric-value">{{ card.value }}</p>
      </div>
    </div>

    <div class="grid gap-4 xl:grid-cols-[.9fr_1.1fr]">
      <form class="garmetix-section-card space-y-4" @submit.prevent="createRequest">
        <div>
          <h3 class="garmetix-panel-title">New Correction Request</h3>
          <p class="garmetix-panel-subtitle">Queue a missed punch or correction request for manager approval.</p>
        </div>
        <div class="grid gap-3 md:grid-cols-2">
          <UFormField label="Employee" name="employeeId" required>
            <USelect v-model="requestForm.employeeId" :items="employeeOptions" placeholder="Select employee" />
          </UFormField>
          <UFormField label="Request Type" name="requestType" required>
            <USelect v-model="requestForm.requestType" :items="requestTypeOptions" />
          </UFormField>
          <UFormField label="Punch Type" name="requestedPunchType" required>
            <USelect v-model="requestForm.requestedPunchType" :items="punchTypeOptions" />
          </UFormField>
          <UFormField label="Local Punch Time" name="requestedLocalPunchTime" required>
            <UInput v-model="requestForm.requestedLocalPunchTime" type="datetime-local" />
          </UFormField>
        </div>
        <UFormField label="Reason" name="reason" required>
          <UTextarea v-model="requestForm.reason" :rows="3" placeholder="Why this correction is needed" />
        </UFormField>
        <div class="flex flex-wrap justify-end gap-2">
          <UButton type="button" color="neutral" variant="soft" icon="i-lucide-rotate-ccw" @click="resetRequestForm">Reset</UButton>
          <UButton type="submit" icon="i-lucide-plus" :loading="creating" :disabled="!canCreate">Create Request</UButton>
        </div>
      </form>

      <div class="garmetix-section-card">
        <h3 class="garmetix-panel-title">Request Preview</h3>
        <pre class="mt-3 max-h-[330px] overflow-auto rounded-lg border border-default bg-default/40 p-3 text-xs">{{ formattedCreatePayload }}</pre>
      </div>
    </div>

    <div class="garmetix-table-panel">
      <div class="garmetix-panel-header">
        <div>
          <h3 class="garmetix-panel-title">Correction Requests</h3>
          <p class="garmetix-panel-subtitle">{{ rows.length }} request row(s)</p>
        </div>
      </div>
      <div class="overflow-auto">
        <table class="w-full min-w-[1040px] text-left text-sm">
          <thead class="bg-muted/30 text-xs uppercase text-muted">
            <tr>
              <th class="px-3 py-2">Employee</th>
              <th class="px-3 py-2">Request</th>
              <th class="px-3 py-2">Punch</th>
              <th class="px-3 py-2">Reason</th>
              <th class="px-3 py-2">Status</th>
              <th class="px-3 py-2">Action</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(row, index) in rows" :key="rowKey(row, index)" class="border-t border-default align-top">
              <td class="px-3 py-2">
                <p class="font-medium">{{ readText(row, ['employeeName', 'employee', 'employeeId']) }}</p>
                <p class="text-xs text-muted">{{ readText(row, ['requestedBy', 'createdBy']) }}</p>
              </td>
              <td class="px-3 py-2">{{ readText(row, ['requestType']) }}</td>
              <td class="px-3 py-2">
                <p>{{ readText(row, ['requestedPunchType']) }}</p>
                <p class="text-xs text-muted">{{ readText(row, ['requestedLocalPunchTime', 'requestedPunchTimeUtc']) }}</p>
              </td>
              <td class="px-3 py-2">{{ readText(row, ['reason']) }}</td>
              <td class="px-3 py-2">
                <UBadge :color="statusTone(row)" variant="subtle">{{ readText(row, ['status']) }}</UBadge>
              </td>
              <td class="px-3 py-2">
                <div class="flex min-w-64 flex-col gap-2">
                  <UInput v-model="remarks[rowKey(row, index)]" size="xs" placeholder="Manager remark" />
                  <div class="flex flex-wrap gap-2">
                    <UButton size="xs" color="success" variant="soft" :disabled="!isPending(row)" :loading="decidingId === readText(row, ['id'], '')" @click="decide(row, true)">Approve</UButton>
                    <UButton size="xs" color="error" variant="soft" :disabled="!isPending(row)" :loading="decidingId === readText(row, ['id'], '')" @click="decide(row, false)">Reject</UButton>
                  </div>
                </div>
              </td>
            </tr>
            <tr v-if="!rows.length">
              <td class="px-3 py-6 text-center text-muted" colspan="6">No regularization requests returned.</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { readText, toLocalDateInput, type ApiRecord, useHrApiClient } from '../../utils/hr-api'

useHead({ title: 'Regularization - Garmetix HR' })

const { get, post } = useHrApiClient()
const loading = ref(false)
const loadingEmployees = ref(false)
const creating = ref(false)
const decidingId = ref('')
const rows = ref<ApiRecord[]>([])
const employees = ref<ApiRecord[]>([])
const message = ref('')
const messageTone = ref<'success' | 'error' | 'warning' | 'neutral'>('neutral')
const remarks = reactive<Record<string, string>>({})
const requestForm = reactive({
  employeeId: null as string | null,
  requestType: 'MissedPunch',
  requestedPunchType: 'CheckIn',
  requestedLocalPunchTime: defaultLocalPunchTime(),
  reason: ''
})

const requestTypeOptions = [
  { value: 'MissedPunch', label: 'Missed Punch' },
  { value: 'WrongPunch', label: 'Wrong Punch' },
  { value: 'TimeCorrection', label: 'Time Correction' }
]
const punchTypeOptions = [
  { value: 'CheckIn', label: 'Check In' },
  { value: 'BreakOut', label: 'Break Out' },
  { value: 'BreakIn', label: 'Break In' },
  { value: 'CheckOut', label: 'Check Out' }
]

const messageIcon = computed(() => messageTone.value === 'success' ? 'i-lucide-circle-check' : messageTone.value === 'warning' ? 'i-lucide-triangle-alert' : messageTone.value === 'error' ? 'i-lucide-circle-alert' : 'i-lucide-info')
const selectedEmployee = computed(() => employees.value.find(employee => readText(employee, ['id'], '') === requestForm.employeeId) ?? null)
const employeeOptions = computed(() => employees.value.map(employee => ({
  value: readText(employee, ['id'], ''),
  label: `${employeeName(employee)} | ${readText(employee, ['employeeCode', 'code'])}`.replace(/\s+\|\s+-$/, '')
})))
const canCreate = computed(() => Boolean(requestForm.employeeId && requestForm.requestType && requestForm.requestedPunchType && requestForm.requestedLocalPunchTime && requestForm.reason.trim() && !creating.value))
const createPayload = computed(() => {
  const employee = selectedEmployee.value
  const localPunchTime = requestForm.requestedLocalPunchTime || defaultLocalPunchTime()
  return {
    employeeId: requestForm.employeeId,
    attendancePunchId: null,
    requestType: requestForm.requestType,
    requestedPunchType: requestForm.requestedPunchType,
    requestedPunchTimeUtc: new Date(localPunchTime).toISOString(),
    requestedLocalPunchTime: localPunchTime,
    reason: requestForm.reason,
    companyId: employee ? readText(employee, ['companyId'], '') : '',
    storeGroupId: employee ? readText(employee, ['storeGroupId'], '') : '',
    storeId: employee ? readText(employee, ['storeId'], '') : ''
  }
})
const formattedCreatePayload = computed(() => JSON.stringify(createPayload.value, null, 2))
const cards = computed(() => [
  { label: 'Requests', value: rows.value.length },
  { label: 'Pending', value: rows.value.filter(isPending).length },
  { label: 'Approved', value: rows.value.filter(row => readText(row, ['status'], '').toLowerCase() === 'approved').length },
  { label: 'Rejected', value: rows.value.filter(row => readText(row, ['status'], '').toLowerCase() === 'rejected').length }
])

function rowKey(row: ApiRecord, index: number) {
  return readText(row, ['id'], String(index))
}

function isPending(row: ApiRecord) {
  return readText(row, ['status'], '').toLowerCase() === 'pending'
}

function statusTone(row: ApiRecord) {
  const status = readText(row, ['status'], '').toLowerCase()
  if (status === 'approved') return 'success'
  if (status === 'rejected') return 'error'
  if (status === 'pending') return 'warning'
  return 'neutral'
}

function defaultLocalPunchTime() {
  const now = new Date()
  return `${toLocalDateInput(now)}T${String(now.getHours()).padStart(2, '0')}:${String(now.getMinutes()).padStart(2, '0')}`
}

function employeeName(employee: ApiRecord) {
  const fullName = readText(employee, ['staffName', 'fullName'], '')
  if (fullName) return fullName
  return `${readText(employee, ['firstName'], '')} ${readText(employee, ['lastName'], '')}`.trim() || 'Employee'
}

function resetRequestForm() {
  requestForm.requestType = 'MissedPunch'
  requestForm.requestedPunchType = 'CheckIn'
  requestForm.requestedLocalPunchTime = defaultLocalPunchTime()
  requestForm.reason = ''
}

async function loadEmployees() {
  loadingEmployees.value = true
  try {
    const response = await get<ApiRecord[] | { rows?: ApiRecord[], items?: ApiRecord[], data?: ApiRecord[] }>('api/employees')
    employees.value = Array.isArray(response) ? response : (response.rows ?? response.items ?? response.data ?? [])
    if (!requestForm.employeeId && employees.value.length) requestForm.employeeId = readText(employees.value[0], ['id'], '')
  } catch (caught) {
    messageTone.value = 'error'
    message.value = caught instanceof Error ? caught.message : 'Unable to load employees.'
  } finally {
    loadingEmployees.value = false
  }
}

async function load() {
  loading.value = true
  message.value = ''
  try {
    const response = await get<ApiRecord[]>('api/attendance/regularization')
    rows.value = Array.isArray(response) ? response : []
  } catch (caught) {
    messageTone.value = 'error'
    message.value = caught instanceof Error ? caught.message : 'Unable to load regularization requests.'
  } finally {
    loading.value = false
  }
}

async function createRequest() {
  if (!canCreate.value) {
    messageTone.value = 'warning'
    message.value = 'Select employee, punch details and reason before creating a correction request.'
    return
  }

  creating.value = true
  message.value = ''
  try {
    await post<ApiRecord>('api/attendance/regularization', createPayload.value)
    messageTone.value = 'success'
    message.value = 'Regularization request created.'
    resetRequestForm()
    await load()
  } catch (caught) {
    messageTone.value = 'error'
    message.value = caught instanceof Error ? caught.message : 'Unable to create regularization request.'
  } finally {
    creating.value = false
  }
}

async function decide(row: ApiRecord, approved: boolean) {
  const id = readText(row, ['id'], '')
  if (!id) {
    messageTone.value = 'warning'
    message.value = 'This request has no saved id.'
    return
  }

  decidingId.value = id
  message.value = ''
  try {
    await post<ApiRecord>(`api/attendance/regularization/${id}/${approved ? 'approve' : 'reject'}`, {
      remarks: remarks[id] || `${approved ? 'Approved' : 'Rejected'} from modular HR app.`
    })
    messageTone.value = 'success'
    message.value = `Regularization request ${approved ? 'approved' : 'rejected'}.`
    await load()
  } catch (caught) {
    messageTone.value = 'error'
    message.value = caught instanceof Error ? caught.message : 'Unable to update regularization request.'
  } finally {
    decidingId.value = ''
  }
}

onMounted(async () => {
  await Promise.all([loadEmployees(), load()])
})
</script>
