<script setup lang="ts">
const svc = useAttendanceShifts()
const workspace = useWorkspace()
const feedback = useUiFeedback()
const auth = useAuth()
const loading = ref(false)
const saving = ref(false)
const shifts = ref<any[]>([])
const editingId = ref('')
const canManage = computed(() => auth.canSeeAdmin.value || String(auth.user.value?.role || '').toLowerCase() === 'poweruser')
const emptyForm = () => ({
  name: 'Default Store Split Shift 09:00-21:00',
  startTimeMinutes: 540,
  endTimeMinutes: 1260,
  graceMinutes: 10,
  lateAfterMinutes: 10,
  halfDayAfterMinutes: 780,
  minimumFullDayMinutes: 0,
  minimumHalfDayMinutes: 0,
  overtimeAfterMinutes: 1260,
  weeklyOffDays: 'Sunday',
  active: true,
  attendanceMode: 'SessionBased',
  shiftCategory: 'StoreDefault',
  hasBreak: true,
  requiresBreakPunch: true,
  breakStartMinutes: 780,
  breakEndMinutes: 870,
  requiredSessionsForFullDay: 2,
  requiredSessionsForHalfDay: 1,
  countBreakAsWork: false
})
const form = reactive<any>(emptyForm())
const presets = [
  { label: 'Default Male / Store 9 AM - 9 PM with lunch', patch: { name: 'Default Store Split Shift 09:00-21:00', startTimeMinutes: 540, endTimeMinutes: 1260, hasBreak: true, requiresBreakPunch: true, breakStartMinutes: 780, breakEndMinutes: 870, requiredSessionsForFullDay: 2, requiredSessionsForHalfDay: 1, shiftCategory: 'StoreDefault', overtimeAfterMinutes: 1260 } },
  { label: 'Female 10 AM - 8 PM', patch: { name: 'Female Staff Shift 10:00-20:00', startTimeMinutes: 600, endTimeMinutes: 1200, hasBreak: false, requiresBreakPunch: false, breakStartMinutes: null, breakEndMinutes: null, requiredSessionsForFullDay: 1, requiredSessionsForHalfDay: 1, shiftCategory: 'Female', overtimeAfterMinutes: 1200 } },
  { label: 'Accounts 10 AM - 7 PM', patch: { name: 'Accounts Shift 10:00-19:00', startTimeMinutes: 600, endTimeMinutes: 1140, hasBreak: false, requiresBreakPunch: false, breakStartMinutes: null, breakEndMinutes: null, requiredSessionsForFullDay: 1, requiredSessionsForHalfDay: 1, shiftCategory: 'Accounts', overtimeAfterMinutes: 1140 } },
  { label: 'Housekeeping Double Shift', patch: { name: 'Housekeeping Double Shift', startTimeMinutes: 420, endTimeMinutes: 1320, hasBreak: true, requiresBreakPunch: true, breakStartMinutes: 900, breakEndMinutes: 1080, requiredSessionsForFullDay: 2, requiredSessionsForHalfDay: 1, shiftCategory: 'HouseKeeping', overtimeAfterMinutes: 1320 } },
  { label: 'Housekeeping Morning only', patch: { name: 'Housekeeping Morning Shift', startTimeMinutes: 420, endTimeMinutes: 900, hasBreak: false, requiresBreakPunch: false, breakStartMinutes: null, breakEndMinutes: null, requiredSessionsForFullDay: 1, requiredSessionsForHalfDay: 1, shiftCategory: 'HouseKeeping', overtimeAfterMinutes: 900 } },
  { label: 'Housekeeping Evening only', patch: { name: 'Housekeeping Evening Shift', startTimeMinutes: 840, endTimeMinutes: 1320, hasBreak: false, requiresBreakPunch: false, breakStartMinutes: null, breakEndMinutes: null, requiredSessionsForFullDay: 1, requiredSessionsForHalfDay: 1, shiftCategory: 'HouseKeeping', overtimeAfterMinutes: 1320 } }
]
function minutesLabel(value?: number | null) {
  if (value === null || value === undefined) return '-'
  const hours = Math.floor(value / 60)
  const minutes = value % 60
  return `${String(hours).padStart(2, '0')}:${String(minutes).padStart(2, '0')}`
}
function applyPreset(preset: any) { Object.assign(form, preset.patch) }
function resetForm() { Object.assign(form, emptyForm()); editingId.value = '' }
function edit(row: any) {
  Object.assign(form, { ...emptyForm(), ...row })
  editingId.value = row.id
}
async function refresh() {
  loading.value = true
  try { shifts.value = await svc.shifts() }
  catch (e: any) { feedback.fromError('Shifts refresh failed', e) }
  finally { loading.value = false }
}
async function save() {
  if (!canManage.value) return feedback.warning('Restricted', 'Only Admin, Owner or PowerUser can manage shifts.')
  saving.value = true
  try {
    const body = { ...form, companyId: workspace.companyId.value, storeGroupId: workspace.storeGroupId.value, storeId: workspace.storeId.value }
    if (editingId.value) {
      await svc.updateShift(editingId.value, body)
      feedback.success('Shift updated', 'Attendance shift was updated.')
    } else {
      await svc.createShift(body)
      feedback.success('Shift saved', 'Attendance shift was created.')
    }
    resetForm()
    await refresh()
  } catch (e: any) { feedback.fromError('Shift save failed', e) }
  finally { saving.value = false }
}
async function remove(row: any) {
  if (row.isProtectedDefault) return feedback.warning('Protected default shift', 'Default 9 AM - 9 PM shift can be edited but cannot be deleted.')
  if (!confirm(`Delete shift ${row.name}?`)) return
  try {
    await svc.deleteShift(row.id)
    feedback.success('Shift deleted', 'Attendance shift was removed.')
    await refresh()
  } catch (e: any) { feedback.fromError('Shift delete failed', e) }
}
onMounted(refresh)
</script>
<template>
  <AppShell title="Attendance Shifts" @refresh="refresh">
    <section class="space-y-5">
      <UiModulePageHeader title="Shifts" description="Create/edit split shifts, no-break shifts and housekeeping double shifts. One completed session is half-day; required full-day sessions define full day." icon="i-lucide-clock-3">
        <template #actions>
          <UButton to="/attendance/shift-rules" label="Employee Shift Rules" icon="i-lucide-user-cog" color="neutral" variant="subtle" />
          <UButton label="Refresh" :loading="loading" @click="refresh" />
        </template>
      </UiModulePageHeader>

      <UAlert v-if="!canManage" color="warning" variant="soft" title="Restricted" description="Only Admin, Owner and PowerUser can view/edit shift setup." />

      <UCard v-if="canManage">
        <template #header><strong>{{ editingId ? 'Edit Shift' : 'Create Shift' }}</strong></template>
        <div class="space-y-4">
          <div class="flex flex-wrap gap-2">
            <UButton v-for="preset in presets" :key="preset.label" size="xs" color="neutral" variant="soft" :label="preset.label" @click="applyPreset(preset)" />
          </div>
          <div class="grid gap-3 md:grid-cols-4">
            <UInput v-model="form.name" placeholder="Shift name" />
            <UInput v-model="form.shiftCategory" placeholder="Category e.g. Female / Accounts" />
            <UInput v-model.number="form.startTimeMinutes" type="number" placeholder="Start minutes" />
            <UInput v-model.number="form.endTimeMinutes" type="number" placeholder="End minutes" />
            <UInput v-model.number="form.graceMinutes" type="number" placeholder="Grace minutes" />
            <UInput v-model.number="form.breakStartMinutes" type="number" placeholder="Break/session 1 end" :disabled="!form.hasBreak" />
            <UInput v-model.number="form.breakEndMinutes" type="number" placeholder="Break/session 2 start" :disabled="!form.hasBreak" />
            <UInput v-model.number="form.overtimeAfterMinutes" type="number" placeholder="OT after minutes" />
            <UInput v-model.number="form.requiredSessionsForFullDay" type="number" placeholder="Full-day sessions" />
            <UInput v-model.number="form.requiredSessionsForHalfDay" type="number" placeholder="Half-day sessions" />
            <UInput v-model="form.weeklyOffDays" placeholder="Weekly off days" />
            <div class="flex gap-2">
              <UButton :label="editingId ? 'Update Shift' : 'Add Shift'" icon="i-lucide-save" :loading="saving" @click="save" />
              <UButton v-if="editingId" label="Cancel" color="neutral" variant="soft" @click="resetForm" />
            </div>
          </div>
          <div class="grid gap-3 md:grid-cols-4">
            <UCheckbox v-model="form.hasBreak" label="Has lunch / two-session break" />
            <UCheckbox v-model="form.requiresBreakPunch" label="Require Break Out / Break In punch" :disabled="!form.hasBreak" />
            <UCheckbox v-model="form.countBreakAsWork" label="Count break as work time" />
            <UCheckbox v-model="form.active" label="Active" />
          </div>
          <p class="text-xs text-muted">Minutes reference: 09:00 = 540, 10:00 = 600, 13:00 = 780, 14:30 = 870, 20:00 = 1200, 21:00 = 1260. For housekeeping double shift, Break Out/Break In represent first-shift exit and second-shift entry.</p>
        </div>
      </UCard>

      <div class="grid gap-3 md:grid-cols-2">
        <UCard v-for="row in shifts" :key="row.id">
          <div class="flex items-start justify-between gap-3">
            <div>
              <div class="flex flex-wrap items-center gap-2">
                <strong>{{ row.name }}</strong>
                <UBadge v-if="row.isProtectedDefault" color="warning" variant="subtle">Protected default</UBadge>
              </div>
              <p class="text-xs text-muted">{{ minutesLabel(row.startTimeMinutes) }} - {{ minutesLabel(row.endTimeMinutes) }} · Grace {{ row.graceMinutes }}m · OT after {{ minutesLabel(row.overtimeAfterMinutes) }}</p>
              <p class="text-xs text-muted" v-if="row.hasBreak">Break/two-session {{ minutesLabel(row.breakStartMinutes) }} - {{ minutesLabel(row.breakEndMinutes) }} · Full day needs {{ row.requiredSessionsForFullDay }} sessions · Half day {{ row.requiredSessionsForHalfDay }}</p>
              <p class="text-xs text-muted" v-else>No-break shift · Full day sessions {{ row.requiredSessionsForFullDay }}</p>
            </div>
            <div class="flex flex-col items-end gap-2">
              <UBadge :color="row.active ? 'success' : 'neutral'" variant="subtle">{{ row.active ? 'Active' : 'Inactive' }}</UBadge>
              <div class="flex gap-1">
                <UButton size="xs" icon="i-lucide-pencil" variant="ghost" @click="edit(row)" />
                <UButton size="xs" icon="i-lucide-trash-2" color="error" variant="ghost" :disabled="row.isProtectedDefault" @click="remove(row)" />
              </div>
            </div>
          </div>
        </UCard>
      </div>
    </section>
  </AppShell>
</template>
