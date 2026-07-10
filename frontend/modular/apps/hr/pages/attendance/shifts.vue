<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 xl:flex-row xl:items-end xl:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-clock-3" class="size-4" /> Attendance setup</p>
          <h2 class="garmetix-dashboard-title">Attendance Shifts</h2>
          <p class="garmetix-dashboard-subtitle">
            Create split shifts, no-break shifts and housekeeping schedules used by kiosk punches, monthly attendance and payroll review.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton to="/attendance/shift-rules" icon="i-lucide-route" color="neutral" variant="soft">Employee Shift Rules</UButton>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="load">Refresh</UButton>
          <UButton icon="i-lucide-plus" color="primary" @click="startCreate">New Shift</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="message" :color="messageTone" variant="subtle" :icon="messageIcon" :description="message" />
    <UAlert color="warning" variant="subtle" icon="i-lucide-shield-alert" title="Shift changes affect attendance and payroll" description="After changing shift timing or sessions, recalculate monthly attendance before final payroll." />

    <div class="grid gap-3 md:grid-cols-2 xl:grid-cols-5">
      <div v-for="card in cards" :key="card.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label">{{ card.label }}</p>
        <p class="garmetix-metric-value text-xl">{{ card.value }}</p>
      </div>
    </div>

    <UModal v-model:open="formOpen" :title="editingId ? 'Edit Shift' : 'Create Shift'">
      <template #body>
        <form class="space-y-3" @submit.prevent="save">
          <div class="garmetix-panel-header">
            <p class="garmetix-panel-subtitle">Minutes reference: 09:00 = 540, 13:00 = 780, 14:30 = 870, 21:00 = 1260.</p>
            <UBadge color="primary" variant="subtle">{{ form.attendanceMode }}</UBadge>
          </div>

          <div class="flex flex-wrap gap-2">
            <UButton v-for="preset in presets" :key="preset.label" type="button" size="xs" color="neutral" variant="soft" @click="applyPreset(preset.patch)">
              {{ preset.label }}
            </UButton>
          </div>

          <div class="grid grid-cols-3 gap-2 rounded-lg border border-default bg-muted/20 p-3 text-sm">
            <div><p class="text-muted">Working hours</p><p class="font-semibold">{{ durationLabel(formWorkingMinutes) }}</p></div>
            <div><p class="text-muted">Break hours</p><p class="font-semibold">{{ durationLabel(formBreakMinutes) }}</p></div>
            <div><p class="text-muted">Sessions for full day</p><p class="font-semibold">{{ form.requiredSessionsForFullDay }} session(s)</p></div>
          </div>

          <UFormField label="Shift name" required>
            <UInput v-model="form.name" placeholder="Shift name" />
          </UFormField>

          <div class="grid gap-3 md:grid-cols-2">
            <UFormField label="Category">
              <UInput v-model="form.shiftCategory" placeholder="StoreDefault / Female / Accounts" />
            </UFormField>
            <UFormField label="Attendance mode">
              <USelect v-model="form.attendanceMode" :items="attendanceModeOptions" />
            </UFormField>
            <UFormField label="Start minutes">
              <UInput v-model.number="form.startTimeMinutes" type="number" min="0" max="1439" />
            </UFormField>
            <UFormField label="End minutes">
              <UInput v-model.number="form.endTimeMinutes" type="number" min="0" max="1439" />
            </UFormField>
            <UFormField label="Grace minutes">
              <UInput v-model.number="form.graceMinutes" type="number" min="0" />
            </UFormField>
            <UFormField label="Late after minutes">
              <UInput v-model.number="form.lateAfterMinutes" type="number" min="0" />
            </UFormField>
            <UFormField label="Half day after minutes">
              <UInput v-model.number="form.halfDayAfterMinutes" type="number" min="0" max="1439" />
            </UFormField>
            <UFormField label="OT after minutes">
              <UInput v-model.number="form.overtimeAfterMinutes" type="number" min="0" max="1439" />
            </UFormField>
            <UFormField label="Full-day sessions">
              <UInput v-model.number="form.requiredSessionsForFullDay" type="number" min="1" />
            </UFormField>
            <UFormField label="Half-day sessions">
              <UInput v-model.number="form.requiredSessionsForHalfDay" type="number" min="1" />
            </UFormField>
            <UFormField label="Break/session 1 end">
              <UInput v-model.number="form.breakStartMinutes" type="number" min="0" max="1439" :disabled="!form.hasBreak" />
            </UFormField>
            <UFormField label="Break/session 2 start">
              <UInput v-model.number="form.breakEndMinutes" type="number" min="0" max="1439" :disabled="!form.hasBreak" />
            </UFormField>
            <UFormField label="Weekly off days">
              <UInput v-model="form.weeklyOffDays" placeholder="Sunday" />
            </UFormField>
            <UFormField label="Auto checkout minutes">
              <UInput v-model.number="form.autoCheckoutTimeMinutes" type="number" min="0" max="1439" :disabled="!form.autoCheckoutEnabled" />
            </UFormField>
          </div>

          <div class="grid gap-2 md:grid-cols-2">
            <UCheckbox v-model="form.hasBreak" label="Has lunch/two-session break" />
            <UCheckbox v-model="form.requiresBreakPunch" label="Require break punches" :disabled="!form.hasBreak" />
            <UCheckbox v-model="form.countBreakAsWork" label="Count break as work time" />
            <UCheckbox v-model="form.autoCheckoutEnabled" label="Auto checkout enabled" />
            <UCheckbox v-model="form.active" label="Active" />
          </div>

          <div class="flex flex-wrap justify-end gap-2">
            <UButton type="button" color="neutral" variant="soft" @click="resetForm">Clear</UButton>
            <UButton type="submit" color="primary" icon="i-lucide-save" :loading="saving" :disabled="!canSave">Save Shift</UButton>
          </div>
        </form>
      </template>
    </UModal>

    <div class="grid content-start gap-3 md:grid-cols-2">
        <div v-for="shift in shifts" :key="readText(shift, ['id'])" class="garmetix-row-card">
          <div class="flex items-start justify-between gap-3">
            <div class="min-w-0">
              <div class="flex flex-wrap items-center gap-2">
                <h3 class="font-semibold text-highlighted">{{ readText(shift, ['name']) }}</h3>
                <UBadge v-if="readBoolean(shift, ['isProtectedDefault'])" color="warning" variant="subtle">Protected default</UBadge>
              </div>
              <p class="mt-1 text-xs text-muted">
                {{ minutesLabel(readNumber(shift, ['startTimeMinutes'])) }} - {{ minutesLabel(readNumber(shift, ['endTimeMinutes'])) }}
                · Grace {{ readNumber(shift, ['graceMinutes']) }}m
                · OT after {{ minutesLabel(readNumber(shift, ['overtimeAfterMinutes'])) }}
              </p>
              <p class="mt-1 text-xs font-medium text-highlighted">
                Working {{ durationLabel(shiftWorkingMinutes(shift)) }}
                <span v-if="readBoolean(shift, ['hasBreak'])"> · Break {{ durationLabel(shiftBreakMinutes(shift)) }}</span>
              </p>
              <p v-if="readBoolean(shift, ['hasBreak'])" class="mt-1 text-xs text-muted">
                Break {{ minutesLabel(readNumber(shift, ['breakStartMinutes'])) }} - {{ minutesLabel(readNumber(shift, ['breakEndMinutes'])) }}
                · Full day {{ readNumber(shift, ['requiredSessionsForFullDay']) }} session(s)
                · Half day {{ readNumber(shift, ['requiredSessionsForHalfDay']) }}
              </p>
              <p v-else class="mt-1 text-xs text-muted">No-break shift · {{ readText(shift, ['shiftCategory'], 'Uncategorised') }} · {{ readNumber(shift, ['requiredSessionsForFullDay']) }} session(s) = full day</p>
            </div>
            <UBadge :color="readBoolean(shift, ['active'], true) ? 'success' : 'neutral'" variant="subtle">{{ readBoolean(shift, ['active'], true) ? 'Active' : 'Inactive' }}</UBadge>
          </div>
          <div class="mt-3 flex justify-end gap-2">
            <UButton size="xs" icon="i-lucide-pencil" color="primary" variant="soft" @click="edit(shift)">Edit</UButton>
            <UButton size="xs" icon="i-lucide-trash-2" color="error" variant="soft" :disabled="readBoolean(shift, ['isProtectedDefault'])" :loading="deletingId === readText(shift, ['id'])" @click="remove(shift)">Delete</UButton>
          </div>
        </div>
        <p v-if="!shifts.length" class="text-sm text-muted">No attendance shifts found.</p>
      </div>
  </section>
</template>

<script setup lang="ts">
import { readBoolean, readNumber, readText, type ApiRecord, useHrApiClient } from '../../utils/hr-api'

useHead({ title: 'Attendance Shifts - Garmetix HR' })

const { get, post, put, del } = useHrApiClient()
const loading = ref(false)
const saving = ref(false)
const deletingId = ref('')
const editingId = ref('')
const message = ref('')
const messageTone = ref<'success' | 'error' | 'warning' | 'neutral'>('neutral')
const shifts = ref<ApiRecord[]>([])
const formOpen = ref(false)
const form = reactive(emptyForm())
const attendanceModeOptions = ['SessionBased', 'TimeBased']
const messageIcon = computed(() => messageTone.value === 'success' ? 'i-lucide-circle-check' : messageTone.value === 'warning' ? 'i-lucide-triangle-alert' : messageTone.value === 'error' ? 'i-lucide-circle-alert' : 'i-lucide-info')
const presets = [
  { label: 'Default Store 09-21', patch: { name: 'Default Store Split Shift 09:00-21:00', startTimeMinutes: 540, endTimeMinutes: 1260, hasBreak: true, requiresBreakPunch: true, breakStartMinutes: 780, breakEndMinutes: 870, requiredSessionsForFullDay: 2, requiredSessionsForHalfDay: 1, shiftCategory: 'StoreDefault', overtimeAfterMinutes: 1260 } },
  { label: 'Female 10-20', patch: { name: 'Female Staff Shift 10:00-20:00', startTimeMinutes: 600, endTimeMinutes: 1200, hasBreak: false, requiresBreakPunch: false, breakStartMinutes: null, breakEndMinutes: null, requiredSessionsForFullDay: 1, requiredSessionsForHalfDay: 1, shiftCategory: 'Female', overtimeAfterMinutes: 1200 } },
  { label: 'Accounts 10-19', patch: { name: 'Accounts Shift 10:00-19:00', startTimeMinutes: 600, endTimeMinutes: 1140, hasBreak: false, requiresBreakPunch: false, breakStartMinutes: null, breakEndMinutes: null, requiredSessionsForFullDay: 1, requiredSessionsForHalfDay: 1, shiftCategory: 'Accounts', overtimeAfterMinutes: 1140 } },
  { label: 'Housekeeping Double', patch: { name: 'Housekeeping Double Shift', startTimeMinutes: 420, endTimeMinutes: 1320, hasBreak: true, requiresBreakPunch: true, breakStartMinutes: 900, breakEndMinutes: 1080, requiredSessionsForFullDay: 2, requiredSessionsForHalfDay: 1, shiftCategory: 'HouseKeeping', overtimeAfterMinutes: 1320 } }
]
const cards = computed(() => [
  { label: 'Shifts', value: shifts.value.length },
  { label: 'Active', value: shifts.value.filter(row => readBoolean(row, ['active'], true)).length },
  { label: 'Split shifts', value: shifts.value.filter(row => readBoolean(row, ['hasBreak'])).length },
  { label: 'Auto checkout', value: shifts.value.filter(row => readBoolean(row, ['autoCheckoutEnabled'])).length },
  { label: 'Protected', value: shifts.value.filter(row => readBoolean(row, ['isProtectedDefault'])).length }
])
const canSave = computed(() => Boolean(form.name.trim() && Number(form.startTimeMinutes) >= 0 && Number(form.endTimeMinutes) >= 0))

function emptyForm() {
  return {
    name: 'Default Store Split Shift 09:00-21:00',
    startTimeMinutes: 540,
    endTimeMinutes: 1260,
    graceMinutes: 10,
    lateAfterMinutes: 10,
    halfDayAfterMinutes: 780,
    minimumFullDayMinutes: 0,
    minimumHalfDayMinutes: 0,
    overtimeAfterMinutes: 1260,
    autoCheckoutEnabled: false,
    autoCheckoutTimeMinutes: null as number | null,
    weeklyOffDays: 'Sunday',
    active: true,
    attendanceMode: 'SessionBased',
    shiftCategory: 'StoreDefault',
    hasBreak: true,
    requiresBreakPunch: true,
    breakStartMinutes: 780 as number | null,
    breakEndMinutes: 870 as number | null,
    requiredSessionsForFullDay: 2,
    requiredSessionsForHalfDay: 1,
    countBreakAsWork: false
  }
}

function minutesLabel(value?: number | null) {
  if (value === null || value === undefined || Number.isNaN(value)) return '-'
  const hours = Math.floor(value / 60)
  const minutes = value % 60
  return `${String(hours).padStart(2, '0')}:${String(minutes).padStart(2, '0')}`
}

function durationLabel(minutes: number) {
  const total = Math.max(0, Math.round(minutes))
  const hours = Math.floor(total / 60)
  const remaining = total % 60
  if (!hours) return `${remaining}m`
  return remaining ? `${hours}h ${remaining}m` : `${hours}h`
}

function breakDurationMinutes(hasBreak: boolean, breakStart: number | null | undefined, breakEnd: number | null | undefined) {
  if (!hasBreak || breakStart === null || breakStart === undefined || breakEnd === null || breakEnd === undefined) return 0
  return Math.max(0, breakEnd - breakStart)
}

function workingDurationMinutes(startMinutes: number, endMinutes: number, breakMinutes: number, countBreakAsWork: boolean) {
  const span = Math.max(0, endMinutes - startMinutes)
  return countBreakAsWork ? span : Math.max(0, span - breakMinutes)
}

const formBreakMinutes = computed(() => breakDurationMinutes(form.hasBreak, form.breakStartMinutes, form.breakEndMinutes))
const formWorkingMinutes = computed(() => workingDurationMinutes(form.startTimeMinutes, form.endTimeMinutes, formBreakMinutes.value, form.countBreakAsWork))

function shiftBreakMinutes(shift: ApiRecord) {
  return breakDurationMinutes(readBoolean(shift, ['hasBreak']), readNumber(shift, ['breakStartMinutes']), readNumber(shift, ['breakEndMinutes']))
}

function shiftWorkingMinutes(shift: ApiRecord) {
  return workingDurationMinutes(
    readNumber(shift, ['startTimeMinutes']),
    readNumber(shift, ['endTimeMinutes']),
    shiftBreakMinutes(shift),
    readBoolean(shift, ['countBreakAsWork'])
  )
}

function applyPreset(patch: Record<string, unknown>) {
  Object.assign(form, patch)
}

function resetForm() {
  editingId.value = ''
  Object.assign(form, emptyForm())
  message.value = ''
}

function startCreate() {
  resetForm()
  formOpen.value = true
}

function edit(row: ApiRecord) {
  editingId.value = readText(row, ['id'], '')
  Object.assign(form, emptyForm(), row)
  formOpen.value = true
}

function buildPayload() {
  return {
    ...form,
    name: form.name.trim(),
    shiftCategory: form.shiftCategory?.trim() || null,
    weeklyOffDays: form.weeklyOffDays?.trim() || 'Sunday',
    breakStartMinutes: form.hasBreak ? form.breakStartMinutes : null,
    breakEndMinutes: form.hasBreak ? form.breakEndMinutes : null,
    requiresBreakPunch: form.hasBreak ? form.requiresBreakPunch : false,
    autoCheckoutTimeMinutes: form.autoCheckoutEnabled ? form.autoCheckoutTimeMinutes : null
  }
}

async function load() {
  loading.value = true
  message.value = ''
  try {
    const rows = await get<ApiRecord[]>('api/attendance/shifts')
    shifts.value = Array.isArray(rows) ? rows : []
  } catch (caught) {
    messageTone.value = 'error'
    message.value = caught instanceof Error ? caught.message : 'Unable to load attendance shifts.'
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
      await put<ApiRecord>(`api/attendance/shifts/${editingId.value}`, buildPayload())
      message.value = 'Attendance shift updated.'
    } else {
      await post<ApiRecord>('api/attendance/shifts', buildPayload())
      message.value = 'Attendance shift created.'
    }
    messageTone.value = 'success'
    resetForm()
    formOpen.value = false
    await load()
  } catch (caught) {
    messageTone.value = 'error'
    message.value = caught instanceof Error ? caught.message : 'Unable to save attendance shift.'
  } finally {
    saving.value = false
  }
}

async function remove(row: ApiRecord) {
  const id = readText(row, ['id'], '')
  if (!id || readBoolean(row, ['isProtectedDefault']) || !window.confirm(`Delete shift ${readText(row, ['name'])}?`)) return
  deletingId.value = id
  message.value = ''
  try {
    await del<void>(`api/attendance/shifts/${id}`)
    messageTone.value = 'success'
    message.value = 'Attendance shift deleted.'
    await load()
  } catch (caught) {
    messageTone.value = 'error'
    message.value = caught instanceof Error ? caught.message : 'Unable to delete attendance shift.'
  } finally {
    deletingId.value = ''
  }
}

onMounted(load)
</script>
