<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 xl:flex-row xl:items-end xl:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-route" class="size-4" /> Attendance setup</p>
          <h2 class="garmetix-dashboard-title">Employee Shift Rules</h2>
          <p class="garmetix-dashboard-subtitle">
            Assign shifts by employee, category, department, designation, gender, or store default. These rules are used by kiosk attendance and monthly payroll generation.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="load">Refresh</UButton>
          <UButton icon="i-lucide-plus" color="primary" @click="startCreate">New Rule</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="message" :color="messageTone" variant="subtle" :icon="messageIcon" :description="message" />
    <UAlert
      color="warning"
      variant="subtle"
      icon="i-lucide-shield-alert"
      title="Shift rules affect payroll"
      description="Changing a shift rule can alter attendance status, payable days, overtime and late penalties after monthly recalculation."
    />

    <div class="grid gap-3 md:grid-cols-2 xl:grid-cols-5">
      <div v-for="card in cards" :key="card.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label">{{ card.label }}</p>
        <p class="garmetix-metric-value text-xl">{{ card.value }}</p>
      </div>
    </div>

    <UModal v-model:open="formOpen" :title="editingId ? 'Edit Rule' : 'New Rule'">
      <template #body>
        <form class="space-y-3" @submit.prevent="save">
          <div class="garmetix-panel-header">
            <p class="garmetix-panel-subtitle">Rule priority is lower-first. Employee rule has the highest priority by default.</p>
            <UBadge color="primary" variant="subtle">{{ form.ruleType }}</UBadge>
          </div>

          <div class="grid gap-3 md:grid-cols-2">
            <UFormField label="Rule type" required>
              <USelect v-model="form.ruleType" :items="ruleTypeOptions" @update:model-value="applyRuleDefaults" />
            </UFormField>
            <UFormField label="Shift" required>
              <USelect v-model="form.attendanceShiftId" :items="shiftOptions" placeholder="Select shift" />
            </UFormField>
          </div>

          <UFormField v-if="form.ruleType === 'Employee'" label="Employee" required>
            <USelectMenu v-model="form.employeeId" value-key="value" :items="employeeOptions" placeholder="Search employee..." @update:model-value="syncScopeFromEmployee" />
          </UFormField>

          <div v-else-if="form.ruleType !== 'StoreDefault'" class="grid gap-3 md:grid-cols-2">
            <UFormField :label="matchLabel" required>
              <UInput v-model="form.matchValue" :placeholder="matchPlaceholder" />
            </UFormField>
            <UFormField label="Priority">
              <UInput v-model.number="form.priority" type="number" min="1" max="999" />
            </UFormField>
          </div>

          <div v-else class="grid gap-3 md:grid-cols-2">
            <UFormField label="Priority">
              <UInput v-model.number="form.priority" type="number" min="1" max="999" />
            </UFormField>
            <div class="rounded-lg border border-default bg-default/40 p-3 text-sm text-muted">
              Store default applies when no employee/category/department/designation/gender rule matches.
            </div>
          </div>

          <div class="grid gap-3 md:grid-cols-2">
            <UFormField label="Effective from" required>
              <UInput v-model="form.effectiveFrom" type="date" />
            </UFormField>
            <UFormField label="Effective to">
              <UInput v-model="form.effectiveTo" type="date" />
            </UFormField>
          </div>

          <div class="grid gap-3 md:grid-cols-[1fr_auto] md:items-end">
            <UFormField label="Notes">
              <UInput v-model="form.notes" placeholder="Reason, approval or special condition" />
            </UFormField>
            <UCheckbox v-model="form.active" label="Active" />
          </div>

          <div class="rounded-lg border border-default bg-default/40 p-3 text-xs text-muted">
            <p>Scope is inherited from the signed-in workspace. Employee rules also copy company, group and store from the selected employee when available.</p>
          </div>

          <div class="flex flex-wrap justify-end gap-2">
            <UButton type="button" color="neutral" variant="soft" @click="resetForm">Clear</UButton>
            <UButton type="submit" color="primary" icon="i-lucide-save" :loading="saving" :disabled="!canSave">Save Rule</UButton>
          </div>
        </form>
      </template>
    </UModal>

    <div class="grid gap-4">
      <div class="garmetix-table-panel">
        <div class="garmetix-panel-header">
          <div>
            <h3 class="garmetix-panel-title">Shift Rule Register</h3>
            <p class="garmetix-panel-subtitle">{{ filteredRules.length }} rule(s) after filters.</p>
          </div>
          <div class="flex flex-wrap gap-2">
            <UInput v-model="search" icon="i-lucide-search" placeholder="Search rule" class="w-52" />
            <USelect v-model="filterType" :items="filterTypeOptions" class="w-44" />
          </div>
        </div>

        <div class="overflow-auto">
          <table class="w-full min-w-[980px] text-left text-sm">
            <thead class="bg-muted/30 text-xs uppercase text-muted">
              <tr>
                <th class="px-3 py-2">Priority</th>
                <th class="px-3 py-2">Rule</th>
                <th class="px-3 py-2">Match</th>
                <th class="px-3 py-2">Shift</th>
                <th class="px-3 py-2">Effective</th>
                <th class="px-3 py-2">Status</th>
                <th class="px-3 py-2 text-right">Action</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(rule, index) in pagedRules" :key="readText(rule, ['id'], String(index))" class="border-t border-default align-top">
                <td class="px-3 py-2 font-semibold">{{ readNumber(rule, ['priority']) }}</td>
                <td class="px-3 py-2">{{ readText(rule, ['ruleType']) }}</td>
                <td class="px-3 py-2">
                  <p class="font-medium">{{ ruleMatch(rule) }}</p>
                  <p class="text-xs text-muted">{{ readText(rule, ['notes'], '') }}</p>
                </td>
                <td class="px-3 py-2">{{ readText(rule, ['shiftName', 'attendanceShiftName', 'name']) }}</td>
                <td class="px-3 py-2">
                  <p>{{ formatDate(readText(rule, ['effectiveFrom'])) }}</p>
                  <p class="text-xs text-muted">To {{ formatDate(readText(rule, ['effectiveTo'], 'Open')) }}</p>
                </td>
                <td class="px-3 py-2">
                  <UBadge :color="readBoolean(rule, ['active'], true) ? 'success' : 'neutral'" variant="subtle">
                    {{ readBoolean(rule, ['active'], true) ? 'Active' : 'Inactive' }}
                  </UBadge>
                </td>
                <td class="px-3 py-2">
                  <div class="flex justify-end gap-2">
                    <UButton size="xs" color="primary" variant="soft" icon="i-lucide-pencil" @click="edit(rule)">Edit</UButton>
                    <UButton size="xs" color="error" variant="soft" icon="i-lucide-trash-2" :loading="deletingId === readText(rule, ['id'], '')" @click="remove(rule)">Delete</UButton>
                  </div>
                </td>
              </tr>
              <tr v-if="!pagedRules.length">
                <td class="px-3 py-6 text-center text-muted" colspan="7">No shift rules found.</td>
              </tr>
            </tbody>
          </table>
        </div>
        <div v-if="filteredRules.length" class="mt-3 flex flex-wrap items-center justify-between gap-2 text-sm text-muted">
          <p>Page {{ page }} of {{ totalPages }}</p>
          <div class="flex items-center gap-2">
            <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-left" :disabled="page <= 1" @click="page--">Prev</UButton>
            <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-chevron-right" :disabled="page >= totalPages" @click="page++">Next</UButton>
          </div>
        </div>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { isActiveEmployee } from '@garmetix/shared-utils'
import { readBoolean, readNumber, readText, toLocalDateInput, type ApiRecord, useHrApiClient } from '../../utils/hr-api'

useHead({ title: 'Employee Shift Rules - Garmetix HR' })

const { get, post, put, del } = useHrApiClient()
const loading = ref(false)
const saving = ref(false)
const deletingId = ref('')
const message = ref('')
const messageTone = ref<'success' | 'error' | 'warning' | 'neutral'>('neutral')
const rules = ref<ApiRecord[]>([])
const shifts = ref<ApiRecord[]>([])
const employees = ref<ApiRecord[]>([])
const search = ref('')
const filterType = ref('All')
const editingId = ref('')
const formOpen = ref(false)
const page = ref(1)
const pageSize = ref(20)
const form = reactive({
  ruleType: 'Employee',
  matchValue: '',
  employeeId: '',
  attendanceShiftId: '',
  effectiveFrom: toLocalDateInput(),
  effectiveTo: '',
  priority: 100,
  active: true,
  notes: '',
  companyId: '',
  storeGroupId: '',
  storeId: ''
})

const ruleTypeOptions = ['Employee', 'Category', 'Department', 'Designation', 'Gender', 'StoreDefault']
const filterTypeOptions = ['All', ...ruleTypeOptions]
const messageIcon = computed(() => messageTone.value === 'success' ? 'i-lucide-circle-check' : messageTone.value === 'warning' ? 'i-lucide-triangle-alert' : messageTone.value === 'error' ? 'i-lucide-circle-alert' : 'i-lucide-info')
const shiftOptions = computed(() => shifts.value.map(shift => ({
  label: `${readText(shift, ['name'])} (${readText(shift, ['startTime'], '')}-${readText(shift, ['endTime'], '')})`,
  value: readText(shift, ['id'], '')
})).filter(item => item.value))
const employeeOptions = computed(() => employees.value.filter(isActiveEmployee).map(employee => ({
  label: `${readText(employee, ['employeeCode'], 'EMP')} - ${readText(employee, ['fullName', 'staffName', 'firstName'], 'Employee')}`,
  value: readText(employee, ['id'], '')
})).filter(item => item.value))
const filteredRules = computed(() => {
  const term = search.value.trim().toLowerCase()
  return rules.value.filter(rule => {
    const typeMatch = filterType.value === 'All' || readText(rule, ['ruleType']) === filterType.value
    if (!typeMatch) return false
    if (!term) return true
    return [readText(rule, ['ruleType']), ruleMatch(rule), readText(rule, ['shiftName']), readText(rule, ['notes'])]
      .join(' ')
      .toLowerCase()
      .includes(term)
  })
})
const totalPages = computed(() => Math.max(1, Math.ceil(filteredRules.value.length / pageSize.value)))
const pagedRules = computed(() => {
  const start = (page.value - 1) * pageSize.value
  return filteredRules.value.slice(start, start + pageSize.value)
})

watch([search, filterType], () => { page.value = 1 })

const cards = computed(() => [
  { label: 'Rules', value: rules.value.length },
  { label: 'Active', value: rules.value.filter(rule => readBoolean(rule, ['active'], true)).length },
  { label: 'Employee Rules', value: rules.value.filter(rule => readText(rule, ['ruleType']) === 'Employee').length },
  { label: 'Store Defaults', value: rules.value.filter(rule => readText(rule, ['ruleType']) === 'StoreDefault').length },
  { label: 'Shifts', value: shifts.value.length }
])
const matchLabel = computed(() => form.ruleType === 'Gender' ? 'Gender' : form.ruleType)
const matchPlaceholder = computed(() => form.ruleType === 'Gender' ? 'Female / Male' : `Enter ${form.ruleType.toLowerCase()} value`)
const canSave = computed(() => Boolean(form.attendanceShiftId && form.effectiveFrom && (
  form.ruleType === 'StoreDefault' ||
  (form.ruleType === 'Employee' ? form.employeeId : form.matchValue.trim())
)))

function applyRuleDefaults() {
  form.employeeId = form.ruleType === 'Employee' ? form.employeeId : ''
  form.matchValue = form.ruleType === 'StoreDefault' || form.ruleType === 'Employee' ? '' : form.matchValue
  form.priority = form.ruleType === 'Employee' ? 100 :
    form.ruleType === 'Category' ? 200 :
      form.ruleType === 'Department' ? 250 :
        form.ruleType === 'Designation' ? 275 :
          form.ruleType === 'Gender' ? 300 : 900
}

function resetForm() {
  editingId.value = ''
  Object.assign(form, {
    ruleType: 'Employee',
    matchValue: '',
    employeeId: '',
    attendanceShiftId: shifts.value.length ? readText(shifts.value[0], ['id'], '') : '',
    effectiveFrom: toLocalDateInput(),
    effectiveTo: '',
    priority: 100,
    active: true,
    notes: '',
    companyId: '',
    storeGroupId: '',
    storeId: ''
  })
  message.value = ''
}

function syncScopeFromEmployee() {
  const employee = employees.value.find(item => readText(item, ['id'], '') === form.employeeId)
  form.companyId = employee ? readText(employee, ['companyId'], '') : ''
  form.storeGroupId = employee ? readText(employee, ['storeGroupId'], '') : ''
  form.storeId = employee ? readText(employee, ['storeId'], '') : ''
}

function ruleMatch(rule: ApiRecord) {
  const type = readText(rule, ['ruleType'])
  if (type === 'Employee') {
    return `${readText(rule, ['employeeCode'], 'EMP')} - ${readText(rule, ['employeeName'], 'Employee')}`
  }
  if (type === 'StoreDefault') return 'Current store default'
  return readText(rule, ['matchValue'], '-')
}

function startCreate() {
  resetForm()
  formOpen.value = true
}

function edit(rule: ApiRecord) {
  editingId.value = readText(rule, ['id'], '')
  Object.assign(form, {
    ruleType: readText(rule, ['ruleType'], 'Employee'),
    matchValue: readText(rule, ['matchValue'], ''),
    employeeId: readText(rule, ['employeeId'], ''),
    attendanceShiftId: readText(rule, ['attendanceShiftId'], ''),
    effectiveFrom: dateInput(readText(rule, ['effectiveFrom'])),
    effectiveTo: dateInput(readText(rule, ['effectiveTo'], '')),
    priority: readNumber(rule, ['priority']) || 100,
    active: readBoolean(rule, ['active'], true),
    notes: readText(rule, ['notes'], ''),
    companyId: readText(rule, ['companyId'], ''),
    storeGroupId: readText(rule, ['storeGroupId'], ''),
    storeId: readText(rule, ['storeId'], '')
  })
  formOpen.value = true
}

function dateInput(value: string) {
  if (!value || value === '-') return ''
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? value.slice(0, 10) : toLocalDateInput(date)
}

function formatDate(value: string) {
  if (!value || value === '-') return '-'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? value : date.toLocaleDateString()
}

function buildPayload() {
  if (form.ruleType === 'Employee') syncScopeFromEmployee()
  return {
    ruleType: form.ruleType,
    matchValue: form.ruleType === 'Employee' || form.ruleType === 'StoreDefault' ? null : form.matchValue.trim(),
    employeeId: form.ruleType === 'Employee' ? form.employeeId : null,
    attendanceShiftId: form.attendanceShiftId,
    effectiveFrom: form.effectiveFrom,
    effectiveTo: form.effectiveTo || null,
    priority: form.priority,
    active: form.active,
    notes: form.notes.trim() || null,
    companyId: form.companyId || undefined,
    storeGroupId: form.storeGroupId || undefined,
    storeId: form.storeId || undefined
  }
}

async function load() {
  loading.value = true
  message.value = ''
  try {
    const [shiftRows, ruleRows, employeeRows] = await Promise.all([
      get<ApiRecord[]>('api/attendance/shifts'),
      get<ApiRecord[]>('api/attendance/shift-rules'),
      get<ApiRecord[]>('api/employees')
    ])
    shifts.value = Array.isArray(shiftRows) ? shiftRows : []
    rules.value = Array.isArray(ruleRows) ? ruleRows : []
    employees.value = Array.isArray(employeeRows) ? employeeRows : []
    if (!form.attendanceShiftId && shifts.value.length) form.attendanceShiftId = readText(shifts.value[0], ['id'], '')
  } catch (caught) {
    messageTone.value = 'error'
    message.value = caught instanceof Error ? caught.message : 'Unable to load shift rules.'
  } finally {
    loading.value = false
  }
}

async function save() {
  if (!canSave.value) return
  saving.value = true
  message.value = ''
  try {
    if (editingId.value) {
      await put<ApiRecord>(`api/attendance/shift-rules/${editingId.value}`, buildPayload())
      message.value = 'Shift rule updated.'
    } else {
      await post<ApiRecord>('api/attendance/shift-rules', buildPayload())
      message.value = 'Shift rule created.'
    }
    messageTone.value = 'success'
    await load()
    resetForm()
    formOpen.value = false
  } catch (caught) {
    messageTone.value = 'error'
    message.value = caught instanceof Error ? caught.message : 'Unable to save shift rule.'
  } finally {
    saving.value = false
  }
}

async function remove(rule: ApiRecord) {
  const id = readText(rule, ['id'], '')
  if (!id) return
  const confirmed = window.confirm(`Delete shift rule for ${ruleMatch(rule)}?`)
  if (!confirmed) return
  deletingId.value = id
  message.value = ''
  try {
    await del<void>(`api/attendance/shift-rules/${id}`)
    messageTone.value = 'success'
    message.value = 'Shift rule deleted.'
    await load()
  } catch (caught) {
    messageTone.value = 'error'
    message.value = caught instanceof Error ? caught.message : 'Unable to delete shift rule.'
  } finally {
    deletingId.value = ''
  }
}

onMounted(load)
</script>
