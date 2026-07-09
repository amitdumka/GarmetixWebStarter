<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 xl:flex-row xl:items-end xl:justify-between">
        <div>
          <p class="garmetix-dashboard-kicker">
            <UIcon name="i-lucide-hand" class="size-4" />
            Manual attendance entry
          </p>
          <h2 class="garmetix-dashboard-title">Manual Punch</h2>
          <p class="garmetix-dashboard-subtitle">
            Create an audited CheckIn, BreakOut, BreakIn, CheckOut or Auto punch for a selected employee.
          </p>
        </div>
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loadingEmployees" @click="loadEmployees">Refresh Employees</UButton>
      </div>
    </div>

    <UAlert
      color="warning"
      variant="subtle"
      icon="i-lucide-shield-alert"
      title="Live attendance write"
      description="Saving this form records an attendance punch and updates the daily attendance row through the shared backend."
    />
    <UAlert v-if="message" :color="messageTone" variant="subtle" :icon="messageIcon" :description="message" />

    <div class="grid gap-4 xl:grid-cols-[1fr_.8fr]">
      <form class="garmetix-section-card space-y-4" @submit.prevent="savePunch">
        <div class="grid gap-3 md:grid-cols-2">
          <UFormField label="Employee" name="employeeId" required>
            <USelectMenu v-model="form.employeeId" value-key="value" :items="employeeOptions" placeholder="Search employee..." />
          </UFormField>
          <UFormField label="Punch Type" name="punchType" required>
            <USelect v-model="form.punchType" :items="punchTypeOptions" />
          </UFormField>
          <UFormField label="Local Punch Time" name="localPunchTime" required>
            <UInput v-model="form.localPunchTime" type="datetime-local" />
          </UFormField>
          <UFormField label="Source" name="source">
            <USelect v-model="form.source" :items="sourceOptions" />
          </UFormField>
        </div>
        <UFormField label="Reason" name="reason">
          <UInput v-model="form.reason" placeholder="Missed punch, manager correction, device offline..." />
        </UFormField>
        <UFormField label="Remarks" name="remarks">
          <UTextarea v-model="form.remarks" :rows="3" placeholder="Audit note" />
        </UFormField>
        <div class="flex flex-wrap justify-end gap-2">
          <UButton type="button" color="neutral" variant="soft" icon="i-lucide-rotate-ccw" @click="resetForm">Reset</UButton>
          <UButton type="submit" icon="i-lucide-save" :loading="saving" :disabled="!canSave">Save Punch</UButton>
        </div>
      </form>

      <div class="garmetix-section-card">
        <h3 class="garmetix-panel-title">Selected Employee</h3>
        <div class="mt-3 space-y-3 text-sm">
          <div v-for="item in selectedEmployeeDetails" :key="item.label" class="flex items-start justify-between gap-3 border-b border-default pb-2 last:border-b-0">
            <span class="text-muted">{{ item.label }}</span>
            <span class="text-right font-medium text-highlighted">{{ item.value }}</span>
          </div>
        </div>
        <div class="mt-4 rounded-lg border border-default bg-default/40 p-3">
          <p class="text-xs uppercase text-muted">Request Preview</p>
          <pre class="mt-2 max-h-[260px] overflow-auto text-xs">{{ formattedPayload }}</pre>
        </div>
      </div>
    </div>

    <div class="garmetix-table-panel">
      <div class="garmetix-panel-header">
        <div>
          <h3 class="garmetix-panel-title">Employee Lookup</h3>
          <p class="garmetix-panel-subtitle">{{ employees.length }} employee row(s)</p>
        </div>
      </div>
      <div class="overflow-auto">
        <table class="w-full min-w-[900px] text-left text-sm">
          <thead class="bg-muted/30 text-xs uppercase text-muted">
            <tr>
              <th class="px-3 py-2">Employee</th>
              <th class="px-3 py-2">Code</th>
              <th class="px-3 py-2">Department</th>
              <th class="px-3 py-2">Designation</th>
              <th class="px-3 py-2">Status</th>
              <th class="px-3 py-2">Action</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(employee, index) in employees" :key="readText(employee, ['id'], String(index))" class="border-t border-default">
              <td class="px-3 py-2 font-medium">{{ employeeName(employee) }}</td>
              <td class="px-3 py-2">{{ readText(employee, ['employeeCode', 'code']) }}</td>
              <td class="px-3 py-2">{{ readText(employee, ['department']) }}</td>
              <td class="px-3 py-2">{{ readText(employee, ['designation']) }}</td>
              <td class="px-3 py-2">
                <UBadge :color="employeeStatusTone(employee)" variant="subtle">{{ readText(employee, ['employeeStatus', 'status']) }}</UBadge>
              </td>
              <td class="px-3 py-2">
                <UButton size="xs" color="neutral" variant="soft" @click="selectEmployee(employee)">Use</UButton>
              </td>
            </tr>
            <tr v-if="!employees.length">
              <td class="px-3 py-6 text-center text-muted" colspan="6">No employees returned.</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { isActiveEmployee } from '@garmetix/shared-utils'
import { readBoolean, readText, toLocalDateInput, type ApiRecord, useHrApiClient } from '../../utils/hr-api'

useHead({ title: 'Manual Punch - Garmetix HR' })

const { get, post } = useHrApiClient()
const employees = ref<ApiRecord[]>([])
const loadingEmployees = ref(false)
const saving = ref(false)
const message = ref('')
const messageTone = ref<'success' | 'error' | 'warning' | 'neutral'>('neutral')

const form = reactive({
  employeeId: null as string | null,
  punchType: 'Auto',
  localPunchTime: defaultLocalPunchTime(),
  source: 'Manual',
  reason: '',
  remarks: ''
})

const punchTypeOptions = [
  { value: 'Auto', label: 'Auto' },
  { value: 'CheckIn', label: 'Check In' },
  { value: 'BreakOut', label: 'Break Out' },
  { value: 'BreakIn', label: 'Break In' },
  { value: 'CheckOut', label: 'Check Out' }
]
const sourceOptions = [
  { value: 'Manual', label: 'Manual' },
  { value: 'ManagerCorrection', label: 'Manager Correction' },
  { value: 'DeviceOffline', label: 'Device Offline' }
]

const messageIcon = computed(() => messageTone.value === 'success' ? 'i-lucide-circle-check' : messageTone.value === 'warning' ? 'i-lucide-triangle-alert' : messageTone.value === 'error' ? 'i-lucide-circle-alert' : 'i-lucide-info')
const selectedEmployee = computed(() => employees.value.find(employee => readText(employee, ['id'], '') === form.employeeId) ?? null)
const employeeOptions = computed(() => employees.value.filter(isActiveEmployee).map(employee => ({
  value: readText(employee, ['id'], ''),
  label: `${employeeName(employee)} | ${readText(employee, ['employeeCode', 'code'])}`.replace(/\s+\|\s+-$/, '')
})))
const canSave = computed(() => Boolean(form.employeeId && form.localPunchTime && form.punchType && !saving.value))
const selectedEmployeeDetails = computed(() => [
  { label: 'Name', value: selectedEmployee.value ? employeeName(selectedEmployee.value) : '-' },
  { label: 'Code', value: selectedEmployee.value ? readText(selectedEmployee.value, ['employeeCode', 'code']) : '-' },
  { label: 'Department', value: selectedEmployee.value ? readText(selectedEmployee.value, ['department']) : '-' },
  { label: 'Company', value: selectedEmployee.value ? readText(selectedEmployee.value, ['companyId']) : '-' },
  { label: 'Store Group', value: selectedEmployee.value ? readText(selectedEmployee.value, ['storeGroupId']) : '-' },
  { label: 'Store', value: selectedEmployee.value ? readText(selectedEmployee.value, ['storeId']) : '-' }
])
const payload = computed(() => {
  const employee = selectedEmployee.value
  const localPunchTime = form.localPunchTime || defaultLocalPunchTime()
  return {
    employeeId: form.employeeId,
    punchType: form.punchType,
    localPunchTime,
    punchTimeUtc: new Date(localPunchTime).toISOString(),
    source: form.source,
    reason: form.reason || null,
    remarks: form.remarks || null,
    companyId: employee ? readText(employee, ['companyId'], '') : '',
    storeGroupId: employee ? readText(employee, ['storeGroupId'], '') : '',
    storeId: employee ? readText(employee, ['storeId'], '') : ''
  }
})
const formattedPayload = computed(() => JSON.stringify(payload.value, null, 2))

function defaultLocalPunchTime() {
  const now = new Date()
  return `${toLocalDateInput(now)}T${String(now.getHours()).padStart(2, '0')}:${String(now.getMinutes()).padStart(2, '0')}`
}

function employeeName(employee: ApiRecord) {
  const fullName = readText(employee, ['staffName', 'fullName'], '')
  if (fullName) return fullName
  return `${readText(employee, ['firstName'], '')} ${readText(employee, ['lastName'], '')}`.trim() || 'Employee'
}

function employeeStatusTone(employee: ApiRecord) {
  const status = readText(employee, ['employeeStatus', 'status'], '').toLowerCase()
  if (status.includes('active') || readBoolean(employee, ['working'])) return 'success'
  if (status.includes('resign') || status.includes('terminate')) return 'warning'
  return 'neutral'
}

function selectEmployee(employee: ApiRecord) {
  form.employeeId = readText(employee, ['id'], '')
}

function resetForm() {
  form.punchType = 'Auto'
  form.localPunchTime = defaultLocalPunchTime()
  form.source = 'Manual'
  form.reason = ''
  form.remarks = ''
  message.value = ''
}

async function loadEmployees() {
  loadingEmployees.value = true
  message.value = ''
  try {
    const response = await get<ApiRecord[] | { rows?: ApiRecord[], items?: ApiRecord[], data?: ApiRecord[] }>('api/employees')
    employees.value = Array.isArray(response) ? response : (response.rows ?? response.items ?? response.data ?? [])
    if (!form.employeeId && employees.value.length) form.employeeId = readText(employees.value[0], ['id'], '')
  } catch (caught) {
    messageTone.value = 'error'
    message.value = caught instanceof Error ? caught.message : 'Unable to load employees.'
  } finally {
    loadingEmployees.value = false
  }
}

async function savePunch() {
  if (!canSave.value) {
    messageTone.value = 'warning'
    message.value = 'Select employee, punch type and local punch time before saving.'
    return
  }

  saving.value = true
  message.value = ''
  try {
    const response = await post<ApiRecord>('api/attendance/manual-punch', payload.value)
    const duplicate = readBoolean(response, ['duplicate'])
    messageTone.value = duplicate ? 'warning' : 'success'
    message.value = readText(response, ['message'], duplicate ? 'Duplicate punch ignored.' : 'Manual punch saved.')
  } catch (caught) {
    messageTone.value = 'error'
    message.value = caught instanceof Error ? caught.message : 'Unable to save manual punch.'
  } finally {
    saving.value = false
  }
}

onMounted(loadEmployees)
</script>
