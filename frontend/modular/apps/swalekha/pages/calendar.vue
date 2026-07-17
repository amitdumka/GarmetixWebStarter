<template>
  <section class="space-y-5">
    <div class="flex flex-wrap items-start justify-between gap-3">
      <div>
        <p class="garmetix-kicker"><UIcon name="i-lucide-calendar-days" class="size-4" /> Swalekha</p>
        <h1 class="garmetix-dashboard-title">Calendar</h1>
        <p class="text-sm text-muted">Appointments plus finance due-dates auto-populated from Investments, Loans, Insurance and Recurring Bills.</p>
      </div>
      <div class="flex flex-wrap items-center gap-2">
        <UButton icon="i-lucide-chevron-left" color="neutral" variant="soft" size="sm" @click="changeMonth(-1)" />
        <span class="min-w-32 text-center text-sm font-medium">{{ monthLabel }}</span>
        <UButton icon="i-lucide-chevron-right" color="neutral" variant="soft" size="sm" @click="changeMonth(1)" />
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
        <UButton icon="i-lucide-plus" @click="openCreate">New Appointment</UButton>
      </div>
    </div>

    <UAlert v-if="error" icon="i-lucide-circle-alert" color="error" variant="subtle" title="Could not load the calendar" :description="error" />

    <div v-if="!loading && events.length === 0" class="rounded-md border border-dashed border-default p-8 text-center text-sm text-muted">
      Nothing on the calendar this month.
    </div>

    <div class="space-y-2">
      <UCard v-for="event in events" :key="`${event.eventType}-${event.sourceId}-${event.date}`" :ui="{ body: 'p-3' }">
        <div class="flex items-center justify-between gap-3">
          <div class="flex items-center gap-3">
            <div class="flex size-11 shrink-0 flex-col items-center justify-center rounded-md bg-elevated/50 text-xs">
              <span class="font-semibold text-highlighted">{{ dayOfMonth(event.date) }}</span>
              <span class="text-muted">{{ weekday(event.date) }}</span>
            </div>
            <div class="min-w-0">
              <p class="truncate font-medium text-highlighted">{{ event.title }}</p>
              <p class="truncate text-xs text-muted">{{ event.description || eventTypeLabel(event.eventType) }}</p>
            </div>
          </div>
          <div class="flex shrink-0 items-center gap-2">
            <span v-if="event.amount != null" class="font-mono text-sm">{{ formatCurrency(event.amount) }}</span>
            <UBadge :color="eventTypeColor(event.eventType)" variant="subtle">{{ eventTypeLabel(event.eventType) }}</UBadge>
            <UButton
              v-if="event.eventType === 'Appointment'"
              icon="i-lucide-trash-2"
              color="error"
              variant="ghost"
              size="sm"
              title="Delete"
              @click="deleteAppointment(event)"
            />
          </div>
        </div>
      </UCard>
    </div>

    <USlideover v-model:open="formOpen" title="New Appointment" :ui="{ content: 'sm:max-w-lg' }">
      <template #body>
        <form class="space-y-4" @submit.prevent="submitAppointment">
          <UFormField label="Title" name="title">
            <UInput v-model="form.title" required class="w-full" />
          </UFormField>

          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Start" name="startAt">
              <UInput v-model="form.startAt" :type="form.isAllDay ? 'date' : 'datetime-local'" class="w-full" />
            </UFormField>
            <UFormField label="End" name="endAt">
              <UInput v-model="form.endAt" :type="form.isAllDay ? 'date' : 'datetime-local'" class="w-full" />
            </UFormField>
          </div>

          <USwitch v-model="form.isAllDay" label="All Day" />

          <UFormField label="Location" name="location">
            <UInput v-model="form.location" class="w-full" />
          </UFormField>

          <UFormField label="Description" name="description">
            <UTextarea v-model="form.description" :rows="3" class="w-full" />
          </UFormField>

          <UAlert v-if="formError" icon="i-lucide-circle-alert" color="error" variant="subtle" :description="formError" />

          <UButton type="submit" icon="i-lucide-save" :loading="saving" block>Add Appointment</UButton>
        </form>
      </template>
    </USlideover>
  </section>
</template>

<script setup lang="ts">
import { useSwalekhaApiClient, type SwalekhaAppointmentPayload, type SwalekhaCalendarEvent, type SwalekhaCalendarEventType } from '../utils/swalekha-api'

useHead({ title: 'Calendar - Swalekha' })

const api = useSwalekhaApiClient()
const loading = ref(false)
const error = ref('')
const events = ref<SwalekhaCalendarEvent[]>([])

const today = new Date()
const year = ref(today.getFullYear())
const month = ref(today.getMonth() + 1)

const monthLabel = computed(() => new Intl.DateTimeFormat('en-IN', { month: 'long', year: 'numeric' }).format(new Date(year.value, month.value - 1, 1)))

function formatCurrency(value: number) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(value || 0)
}

function dayOfMonth(value: string) {
  return new Date(value).getDate()
}

function weekday(value: string) {
  return new Intl.DateTimeFormat('en-IN', { weekday: 'short' }).format(new Date(value))
}

const eventTypeLabels: Record<SwalekhaCalendarEventType, string> = {
  Appointment: 'Appointment',
  RecurringBill: 'Bill Due',
  FixedDepositMaturity: 'FD Maturity',
  RecurringDepositMaturity: 'RD Maturity',
  InsurancePremium: 'Premium Due'
}

const eventTypeColors: Record<SwalekhaCalendarEventType, 'primary' | 'warning' | 'success' | 'secondary'> = {
  Appointment: 'primary',
  RecurringBill: 'warning',
  FixedDepositMaturity: 'success',
  RecurringDepositMaturity: 'success',
  InsurancePremium: 'warning'
}

function eventTypeLabel(type: SwalekhaCalendarEventType) {
  return eventTypeLabels[type] || type
}

function eventTypeColor(type: SwalekhaCalendarEventType) {
  return eventTypeColors[type] || 'neutral'
}

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    events.value = await api.get<SwalekhaCalendarEvent[]>(`calendar?year=${year.value}&month=${month.value}`)
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not load the calendar.'
  } finally {
    loading.value = false
  }
}

function changeMonth(delta: number) {
  let newMonth = month.value + delta
  let newYear = year.value
  if (newMonth < 1) { newMonth = 12; newYear -= 1 }
  if (newMonth > 12) { newMonth = 1; newYear += 1 }
  month.value = newMonth
  year.value = newYear
  refresh()
}

onMounted(refresh)

const formOpen = ref(false)
const saving = ref(false)
const formError = ref('')
const form = reactive<SwalekhaAppointmentPayload>(defaultForm())

function defaultForm(): SwalekhaAppointmentPayload {
  const now = new Date().toISOString()
  return { title: '', description: '', startAt: now.substring(0, 16), endAt: null, location: '', isAllDay: false, notes: '' }
}

function openCreate() {
  formError.value = ''
  Object.assign(form, defaultForm())
  formOpen.value = true
}

async function submitAppointment() {
  saving.value = true
  formError.value = ''
  try {
    await api.post('appointments', form)
    formOpen.value = false
    await refresh()
  } catch (err) {
    formError.value = err instanceof Error ? err.message : 'Could not save the appointment.'
  } finally {
    saving.value = false
  }
}

async function deleteAppointment(event: SwalekhaCalendarEvent) {
  if (!event.sourceId) return
  if (!confirm(`Delete "${event.title}"?`)) return
  try {
    await api.del(`appointments/${event.sourceId}`)
    await refresh()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not delete the appointment.'
  }
}
</script>
