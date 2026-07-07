<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 xl:flex-row xl:items-end xl:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-sliders-horizontal" class="size-4" /> Attendance setup</p>
          <h2 class="garmetix-dashboard-title">Attendance Policies</h2>
          <p class="garmetix-dashboard-subtitle">
            Configure grace, duplicate prevention, half-day, full-day, overtime and auto-checkout rules used during attendance recalculation.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="load">Refresh</UButton>
          <UButton icon="i-lucide-plus" color="primary" @click="startCreate">New Policy</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="message" :color="messageTone" variant="subtle" :icon="messageIcon" :description="message" />
    <UAlert color="warning" variant="subtle" icon="i-lucide-shield-alert" title="Policy changes require recalculation" description="After policy changes, recalculate the affected month before payroll finalization." />

    <div class="grid content-start gap-3 md:grid-cols-2">
      <div v-for="policy in policies" :key="readText(policy, ['id'])" class="garmetix-row-card">
        <div class="flex items-start justify-between gap-3">
          <div>
            <h3 class="font-semibold text-highlighted">{{ readText(policy, ['name']) }}</h3>
            <p class="mt-1 text-xs text-muted">
              Grace {{ readNumber(policy, ['graceMinutes']) }}m · Late {{ readNumber(policy, ['lateAfterMinutes']) }}m · Duplicate {{ readNumber(policy, ['duplicateWindowMinutes']) }}m
            </p>
            <p class="mt-1 text-xs text-muted">
              Half day after {{ minutesLabel(readNumber(policy, ['halfDayAfterMinutes'])) }} · Full min {{ readNumber(policy, ['minimumFullDayMinutes']) }}m · Half min {{ readNumber(policy, ['minimumHalfDayMinutes']) }}m · OT {{ readNumber(policy, ['overtimeAfterMinutes']) }}m
            </p>
            <p v-if="readBoolean(policy, ['autoCheckoutEnabled'])" class="mt-1 text-xs text-muted">Auto checkout after {{ readNumber(policy, ['autoCheckoutAfterMinutes']) }} minutes.</p>
          </div>
          <UBadge :color="readBoolean(policy, ['active'], true) ? 'success' : 'neutral'" variant="subtle">
            {{ readBoolean(policy, ['active'], true) ? 'Active' : 'Inactive' }}
          </UBadge>
        </div>
        <div class="mt-3 flex justify-end gap-2">
          <UButton size="xs" icon="i-lucide-pencil" color="neutral" variant="soft" @click="edit(policy)">Edit</UButton>
          <UButton size="xs" icon="i-lucide-trash-2" color="error" variant="soft" :loading="deletingId === readText(policy, ['id'])" @click="remove(policy)">Delete</UButton>
        </div>
      </div>
      <p v-if="!policies.length" class="text-sm text-muted">No attendance policies found.</p>
    </div>

    <UModal v-model:open="formOpen" :title="editingId ? 'Edit Policy' : 'New Policy'">
      <template #body>
        <form class="space-y-3" @submit.prevent="save">
          <div class="garmetix-panel-header">
            <p class="garmetix-panel-subtitle">This mirrors the legacy attendance policy setup and uses `/api/attendance/policies`.</p>
            <UBadge :color="form.active ? 'success' : 'neutral'" variant="subtle">{{ form.active ? 'Active' : 'Inactive' }}</UBadge>
          </div>

          <UFormField label="Policy name" required>
            <UInput v-model="form.name" placeholder="Default Attendance Policy" />
          </UFormField>

          <div class="grid gap-3 md:grid-cols-2">
            <UFormField label="Grace minutes">
              <UInput v-model.number="form.graceMinutes" type="number" min="0" />
            </UFormField>
            <UFormField label="Late after minutes">
              <UInput v-model.number="form.lateAfterMinutes" type="number" min="0" />
            </UFormField>
            <UFormField label="Half-day after minutes">
              <UInput v-model.number="form.halfDayAfterMinutes" type="number" min="0" max="1439" />
            </UFormField>
            <UFormField label="Minimum full-day minutes">
              <UInput v-model.number="form.minimumFullDayMinutes" type="number" min="0" />
            </UFormField>
            <UFormField label="Minimum half-day minutes">
              <UInput v-model.number="form.minimumHalfDayMinutes" type="number" min="0" />
            </UFormField>
            <UFormField label="Overtime after minutes">
              <UInput v-model.number="form.overtimeAfterMinutes" type="number" min="0" />
            </UFormField>
            <UFormField label="Duplicate window minutes">
              <UInput v-model.number="form.duplicateWindowMinutes" type="number" min="0" max="120" />
            </UFormField>
            <UFormField label="Auto checkout after minutes">
              <UInput v-model.number="form.autoCheckoutAfterMinutes" type="number" min="0" :disabled="!form.autoCheckoutEnabled" />
            </UFormField>
          </div>

          <div class="grid gap-2 md:grid-cols-2">
            <UCheckbox v-model="form.autoCheckoutEnabled" label="Auto checkout enabled" />
            <UCheckbox v-model="form.active" label="Active" />
          </div>

          <div class="flex flex-wrap justify-end gap-2">
            <UButton type="button" color="neutral" variant="soft" @click="resetForm">Clear</UButton>
            <UButton type="submit" color="primary" icon="i-lucide-save" :loading="saving" :disabled="!form.name.trim()">Save Policy</UButton>
          </div>
        </form>
      </template>
    </UModal>
  </section>
</template>

<script setup lang="ts">
import { readBoolean, readNumber, readText, type ApiRecord, useHrApiClient } from '../../utils/hr-api'

useHead({ title: 'Attendance Policies - Garmetix HR' })

const { get, post, put, del } = useHrApiClient()
const loading = ref(false)
const saving = ref(false)
const editingId = ref('')
const deletingId = ref('')
const message = ref('')
const messageTone = ref<'success' | 'error' | 'warning' | 'neutral'>('neutral')
const policies = ref<ApiRecord[]>([])
const formOpen = ref(false)
const form = reactive(emptyForm())
const messageIcon = computed(() => messageTone.value === 'success' ? 'i-lucide-circle-check' : messageTone.value === 'warning' ? 'i-lucide-triangle-alert' : messageTone.value === 'error' ? 'i-lucide-circle-alert' : 'i-lucide-info')

function emptyForm() {
  return {
    name: 'Default Attendance Policy',
    graceMinutes: 10,
    lateAfterMinutes: 10,
    halfDayAfterMinutes: 750,
    minimumFullDayMinutes: 480,
    minimumHalfDayMinutes: 240,
    overtimeAfterMinutes: 540,
    autoCheckoutEnabled: false,
    autoCheckoutAfterMinutes: null as number | null,
    duplicateWindowMinutes: 5,
    active: true
  }
}

function minutesLabel(value: number) {
  if (!value) return '0m'
  const hours = Math.floor(value / 60)
  const minutes = value % 60
  return hours ? `${hours}h ${minutes}m` : `${minutes}m`
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

function edit(policy: ApiRecord) {
  editingId.value = readText(policy, ['id'], '')
  Object.assign(form, emptyForm(), policy)
  formOpen.value = true
}

async function remove(policy: ApiRecord) {
  const id = readText(policy, ['id'], '')
  if (!id || !window.confirm(`Delete policy "${readText(policy, ['name'])}"?`)) return
  deletingId.value = id
  message.value = ''
  try {
    await del<void>(`api/attendance/policies/${id}`)
    messageTone.value = 'success'
    message.value = 'Attendance policy deleted.'
    await load()
  } catch (caught) {
    messageTone.value = 'error'
    message.value = caught instanceof Error ? caught.message : 'Unable to delete attendance policy.'
  } finally {
    deletingId.value = ''
  }
}

function buildPayload() {
  return {
    ...form,
    name: form.name.trim(),
    autoCheckoutAfterMinutes: form.autoCheckoutEnabled ? form.autoCheckoutAfterMinutes : null
  }
}

async function load() {
  loading.value = true
  message.value = ''
  try {
    const rows = await get<ApiRecord[]>('api/attendance/policies')
    policies.value = Array.isArray(rows) ? rows : []
  } catch (caught) {
    messageTone.value = 'error'
    message.value = caught instanceof Error ? caught.message : 'Unable to load attendance policies.'
  } finally {
    loading.value = false
  }
}

async function save() {
  if (!form.name.trim()) return
  saving.value = true
  message.value = ''
  try {
    if (editingId.value) {
      await put<ApiRecord>(`api/attendance/policies/${editingId.value}`, buildPayload())
      message.value = 'Attendance policy updated.'
    } else {
      await post<ApiRecord>('api/attendance/policies', buildPayload())
      message.value = 'Attendance policy created.'
    }
    messageTone.value = 'success'
    resetForm()
    formOpen.value = false
    await load()
  } catch (caught) {
    messageTone.value = 'error'
    message.value = caught instanceof Error ? caught.message : 'Unable to save attendance policy.'
  } finally {
    saving.value = false
  }
}

onMounted(load)
</script>
