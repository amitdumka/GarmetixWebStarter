<script setup lang="ts">
const svc = useAttendanceShifts()
const api = useGarmetixApi()
const workspace = useWorkspace()
const feedback = useUiFeedback()
const auth = useAuth()
const loading = ref(false)
const saving = ref(false)
const loadError = ref('')
const rules = ref<any[]>([])
const shifts = ref<any[]>([])
const employees = ref<any[]>([])
const editingId = ref('')
const canManage = computed(() => auth.canSeeAdmin.value || String(auth.user.value?.role || '').toLowerCase() === 'poweruser')
const ruleTypes = ['Employee', 'Gender', 'Category', 'Department', 'Designation', 'StoreDefault']
const genders = ['Male', 'Female', 'TransGender']
const categories = ['Salesman', 'StoreManager', 'HouseKeeping', 'Owner', 'Accounts', 'TailorMaster', 'Tailors', 'TailoringAssistance', 'Others']
const search = ref('')
const ruleTypeFilter = ref('all')
const statusFilter = ref('active')

const emptyForm = () => ({
  ruleType: 'Employee',
  matchValue: '',
  employeeId: '',
  attendanceShiftId: '',
  effectiveFrom: new Date().toISOString().slice(0, 10),
  effectiveTo: '',
  priority: 100,
  active: true,
  notes: ''
})
const form = reactive<any>(emptyForm())

function asArray(value: any): any[] {
  if (Array.isArray(value)) return value
  if (Array.isArray(value?.items)) return value.items
  if (Array.isArray(value?.rows)) return value.rows
  if (Array.isArray(value?.data)) return value.data
  return []
}

const activeEmployees = computed(() => employees.value.filter((employee: any) => {
  const status = String(employee?.employeeStatus || '').trim().toLowerCase()
  return Boolean(employee?.working) && !['resigned', 'terminated', 'inactive'].includes(status)
}))
const employeeItems = computed(() => activeEmployees.value.map((e: any) => ({
  value: e.id,
  label: `${e.employeeCode || 'EMP'} - ${[e.firstName, e.lastName].filter(Boolean).join(' ') || e.name || 'Employee'}`
})))
const shiftItems = computed(() => shifts.value.map((s: any) => ({ value: s.id, label: s.name })))
const ruleTypeItems = computed(() => [{ value: 'all', label: 'All rule types' }, ...ruleTypes.map((type) => ({ value: type, label: type }))])
const statusItems = [
  { value: 'active', label: 'Active only' },
  { value: 'inactive', label: 'Inactive only' },
  { value: 'all', label: 'All statuses' }
]
const matchItems = computed(() => {
  if (form.ruleType === 'Gender') return genders
  if (form.ruleType === 'Category') return categories
  if (form.ruleType === 'Department') return [...new Set(activeEmployees.value.map((e: any) => e.department).filter(Boolean))]
  if (form.ruleType === 'Designation') return [...new Set(activeEmployees.value.map((e: any) => e.designation).filter(Boolean))]
  return []
})

const visibleRules = computed(() => {
  const query = search.value.trim().toLowerCase()
  return rules.value.filter((row: any) => {
    if (ruleTypeFilter.value !== 'all' && row.ruleType !== ruleTypeFilter.value) return false
    if (statusFilter.value === 'active' && !row.active) return false
    if (statusFilter.value === 'inactive' && row.active) return false
    if (!query) return true
    return [row.ruleType, row.matchValue, row.employeeCode, row.employeeName, row.shiftName, row.notes]
      .filter(Boolean)
      .some((value) => String(value).toLowerCase().includes(query))
  })
})

function ruleLabel(rule: any) {
  if (rule.ruleType === 'Employee') return [rule.employeeCode, rule.employeeName].filter(Boolean).join(' - ') || employeeItems.value.find((e: any) => e.value === rule.employeeId)?.label || 'Employee rule'
  if (rule.ruleType === 'StoreDefault') return 'Store default'
  return rule.matchValue || '-'
}
function shiftLabel(row: any) {
  return row.shiftName || shifts.value.find((s: any) => s.id === row.attendanceShiftId)?.name || 'Shift not found'
}
function formatDate(value?: string | null) {
  if (!value) return '-'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return String(value).slice(0, 10)
  return date.toLocaleDateString('en-IN', { day: '2-digit', month: '2-digit', year: 'numeric' })
}
function adjustPriority() {
  form.priority = form.ruleType === 'Employee' ? 100 : form.ruleType === 'Category' ? 200 : form.ruleType === 'Department' ? 250 : form.ruleType === 'Designation' ? 275 : form.ruleType === 'Gender' ? 300 : 900
  if (form.ruleType === 'StoreDefault') { form.employeeId = ''; form.matchValue = '' }
  if (form.ruleType !== 'Employee') form.employeeId = ''
}
function resetForm() {
  Object.assign(form, emptyForm())
  editingId.value = ''
  if (shifts.value[0]?.id) form.attendanceShiftId = shifts.value[0].id
}
function edit(row: any) {
  Object.assign(form, {
    ruleType: row.ruleType || 'Employee',
    matchValue: row.matchValue || '',
    employeeId: row.employeeId || '',
    attendanceShiftId: row.attendanceShiftId || '',
    effectiveFrom: row.effectiveFrom ? row.effectiveFrom.slice(0, 10) : new Date().toISOString().slice(0, 10),
    effectiveTo: row.effectiveTo ? row.effectiveTo.slice(0, 10) : '',
    priority: row.priority || 100,
    active: Boolean(row.active),
    notes: row.notes || ''
  })
  editingId.value = row.id
  window.scrollTo({ top: 0, behavior: 'smooth' })
}
async function refresh() {
  loading.value = true
  loadError.value = ''
  try {
    const [shiftRows, ruleRows, employeeRows] = await Promise.all([svc.shifts(), svc.shiftRules(), api.list<any>('employees')])
    shifts.value = asArray(shiftRows)
    rules.value = asArray(ruleRows)
    employees.value = asArray(employeeRows)
    if (!form.attendanceShiftId && shifts.value[0]?.id) form.attendanceShiftId = shifts.value[0].id
  } catch (error: any) {
    loadError.value = error?.data?.message || error?.message || 'Could not load shift rules.'
    feedback.fromError('Shift rules refresh failed', error)
  } finally { loading.value = false }
}
async function save() {
  if (!canManage.value) return feedback.warning('Restricted', 'Only Admin, Owner or PowerUser can manage employee shift rules.')
  if (!form.attendanceShiftId) return feedback.warning('Select shift', 'Please select the shift for this rule.')
  if (form.ruleType === 'Employee' && !form.employeeId) return feedback.warning('Select employee', 'Employee rule needs an active employee.')
  if (!['Employee', 'StoreDefault'].includes(form.ruleType) && !form.matchValue) return feedback.warning('Select match value', 'This rule type needs a match value.')
  saving.value = true
  try {
    const body = {
      ...form,
      employeeId: form.ruleType === 'Employee' ? form.employeeId : null,
      matchValue: form.ruleType === 'Employee' || form.ruleType === 'StoreDefault' ? null : form.matchValue,
      effectiveTo: form.effectiveTo || null,
      companyId: workspace.companyId.value || undefined,
      storeGroupId: workspace.storeGroupId.value || undefined,
      storeId: workspace.storeId.value || undefined
    }
    if (editingId.value) {
      await svc.updateShiftRule(editingId.value, body)
      feedback.success('Shift rule updated', 'Employee shift assignment rule was updated.')
    } else {
      await svc.createShiftRule(body)
      feedback.success('Shift rule saved', 'Employee shift assignment rule was created.')
    }
    resetForm()
    await refresh()
  } catch (error: any) { feedback.fromError('Shift rule save failed', error) }
  finally { saving.value = false }
}
async function remove(row: any) {
  if (!canManage.value) return feedback.warning('Restricted', 'Only Admin, Owner or PowerUser can delete employee shift rules.')
  if (!confirm(`Delete ${row.ruleType} rule: ${ruleLabel(row)}?`)) return
  try {
    await svc.deleteShiftRule(row.id)
    feedback.success('Shift rule deleted', 'Rule was removed.')
    await refresh()
  } catch (error: any) { feedback.fromError('Shift rule delete failed', error) }
}
onMounted(refresh)
watch(() => form.ruleType, adjustPriority)
</script>

<template>
  <AppShell title="Employee Shift Rules" @refresh="refresh">
    <section class="space-y-5">
      <UiModulePageHeader title="Employee Shift Rules" description="Assign shifts by employee, gender, category, department, designation, or store default. Employee rule has highest priority." icon="i-lucide-user-cog">
        <template #actions>
          <UBadge color="neutral" variant="subtle">{{ rules.length }} rules</UBadge>
          <UButton to="/attendance/shifts" label="Shifts" icon="i-lucide-clock-3" color="neutral" variant="subtle" />
          <UButton label="Refresh" :loading="loading" @click="refresh" />
        </template>
      </UiModulePageHeader>

      <UAlert v-if="!canManage" color="warning" variant="soft" title="Restricted" description="Only Admin, Owner and PowerUser can view/edit/delete employee shift rules." />

      <template v-else>
        <UCard>
          <template #header>
            <div class="flex flex-wrap items-center justify-between gap-2">
              <strong>{{ editingId ? 'Edit Assignment Rule' : 'Add Assignment Rule' }}</strong>
              <UButton v-if="editingId" label="New Rule" color="neutral" variant="soft" icon="i-lucide-plus" @click="resetForm" />
            </div>
          </template>
          <div class="grid gap-3 md:grid-cols-4">
            <USelect v-model="form.ruleType" :items="ruleTypes" placeholder="Rule type" />
            <USelect v-if="form.ruleType === 'Employee'" v-model="form.employeeId" :items="employeeItems" placeholder="Active employee" />
            <USelect v-else-if="matchItems.length" v-model="form.matchValue" :items="matchItems" placeholder="Match value" />
            <UInput v-else v-model="form.matchValue" :disabled="form.ruleType === 'StoreDefault'" placeholder="Match value" />
            <USelect v-model="form.attendanceShiftId" :items="shiftItems" placeholder="Shift" />
            <UInput v-model.number="form.priority" type="number" placeholder="Priority" />
            <UInput v-model="form.effectiveFrom" type="date" />
            <UInput v-model="form.effectiveTo" type="date" placeholder="Effective to" />
            <UInput v-model="form.notes" placeholder="Notes" />
            <USwitch v-model="form.active" label="Active" />
            <div class="flex gap-2">
              <UButton :label="editingId ? 'Update Rule' : 'Save Rule'" icon="i-lucide-save" :loading="saving" @click="save" />
              <UButton v-if="editingId" label="Cancel" color="neutral" variant="soft" @click="resetForm" />
            </div>
          </div>
          <p class="mt-3 text-xs text-muted">Priority: Employee 100, Category 200, Department 250, Designation 275, Gender 300, Store default 900. Lower number wins.</p>
        </UCard>

        <UiRegisterPanel
          :title="`Shift rules (${visibleRules.length})`"
          description="Saved employee/category/gender/department/designation shift assignments. Use Edit or Delete from the action column."
          :loading="loading"
          :error="loadError"
          :empty="!loading && visibleRules.length === 0"
          empty-title="No shift rules visible"
          empty-description="Create a rule above, or change filters to show active/inactive rules."
          empty-icon="i-lucide-user-cog"
          @retry="refresh"
        >
          <template #actions>
            <UBadge color="primary" variant="subtle">{{ rules.length }} total</UBadge>
          </template>

          <UiCrudToolbar
            v-model:search="search"
            search-placeholder="Search employee, rule, shift, department, category"
            :loading="loading"
            refresh-label="Sync"
            create-label="Clear Form"
            @refresh="refresh"
            @create="resetForm"
          >
            <template #filters>
              <USelect v-model="ruleTypeFilter" :items="ruleTypeItems" class="min-w-40" aria-label="Rule type filter" />
              <USelect v-model="statusFilter" :items="statusItems" class="min-w-36" aria-label="Status filter" />
            </template>
          </UiCrudToolbar>

          <div v-if="visibleRules.length" class="overflow-x-auto">
            <table class="min-w-full text-sm">
              <thead>
                <tr class="text-left text-xs uppercase text-muted">
                  <th class="p-2">Priority</th>
                  <th class="p-2">Rule Type</th>
                  <th class="p-2">Employee / Match</th>
                  <th class="p-2">Shift Rule</th>
                  <th class="p-2">Effective Date</th>
                  <th class="p-2">Status</th>
                  <th class="p-2 text-right">Edit / Delete</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="row in visibleRules" :key="row.id" class="border-t border-default">
                  <td class="p-2">{{ row.priority }}</td>
                  <td class="p-2"><UBadge color="neutral" variant="subtle">{{ row.ruleType }}</UBadge></td>
                  <td class="p-2 font-medium">{{ ruleLabel(row) }}</td>
                  <td class="p-2">{{ shiftLabel(row) }}</td>
                  <td class="p-2">
                    {{ formatDate(row.effectiveFrom) }}
                    <span v-if="row.effectiveTo"> to {{ formatDate(row.effectiveTo) }}</span>
                  </td>
                  <td class="p-2"><UBadge :color="row.active ? 'success' : 'neutral'" variant="subtle">{{ row.active ? 'Active' : 'Inactive' }}</UBadge></td>
                  <td class="p-2 text-right">
                    <div class="flex justify-end gap-1">
                      <UButton size="xs" label="Edit" variant="soft" icon="i-lucide-pencil" @click="edit(row)" />
                      <UButton size="xs" label="Delete" color="error" variant="soft" icon="i-lucide-trash-2" @click="remove(row)" />
                    </div>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </UiRegisterPanel>
      </template>
    </section>
  </AppShell>
</template>
