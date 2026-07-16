<template>
  <section class="space-y-5">
    <div class="flex flex-wrap items-start justify-between gap-3">
      <div>
        <p class="garmetix-kicker"><UIcon name="i-lucide-plane" class="size-4" /> Swalekha</p>
        <h1 class="garmetix-dashboard-title">Travel Expense Sheets</h1>
      </div>
      <div class="flex flex-wrap items-center gap-2">
        <USwitch v-model="includeClosed" label="Show closed" @change="refresh" />
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
        <UButton icon="i-lucide-plus" @click="openCreate">New Trip</UButton>
      </div>
    </div>

    <UAlert v-if="error" icon="i-lucide-circle-alert" color="error" variant="subtle" title="Could not load trips" :description="error" />

    <UCard :ui="{ body: 'p-0' }">
      <UTable :data="trips" :columns="tripColumns" :loading="loading" class="w-full">
        <template #destination-cell="{ row }">
          <span class="text-sm text-muted">{{ row.original.destination || '-' }}</span>
        </template>
        <template #dates-cell="{ row }">
          <span class="text-sm text-muted">{{ formatRange(row.original) }}</span>
        </template>
        <template #budget-cell="{ row }">
          <span class="text-sm text-muted">{{ row.original.budget != null ? formatCurrency(row.original.budget) : '-' }}</span>
        </template>
        <template #spentTotal-cell="{ row }">
          <span class="font-mono text-sm" :class="row.original.budget != null && row.original.spentTotal > row.original.budget ? 'text-error' : ''">
            {{ formatCurrency(row.original.spentTotal) }}
          </span>
        </template>
        <template #isClosed-cell="{ row }">
          <UBadge :color="row.original.isClosed ? 'neutral' : 'success'" variant="subtle">{{ row.original.isClosed ? 'Closed' : 'Open' }}</UBadge>
        </template>
        <template #actions-cell="{ row }">
          <div class="flex justify-end gap-1">
            <UTooltip text="Entries">
              <UButton icon="i-lucide-list" color="neutral" variant="ghost" size="sm" @click="navigateTo(`/trips/${row.original.id}`)" />
            </UTooltip>
            <UTooltip text="Edit">
              <UButton icon="i-lucide-pencil" color="primary" variant="ghost" size="sm" @click="openEdit(row.original)" />
            </UTooltip>
            <UTooltip :text="row.original.isClosed ? 'Reopen' : 'Close Trip'">
              <UButton :icon="row.original.isClosed ? 'i-lucide-lock-open' : 'i-lucide-check-check'" color="neutral" variant="ghost" size="sm" @click="toggleClose(row.original)" />
            </UTooltip>
            <UTooltip text="Delete">
              <UButton icon="i-lucide-trash-2" color="error" variant="ghost" size="sm" @click="deleteTrip(row.original)" />
            </UTooltip>
          </div>
        </template>
      </UTable>
    </UCard>

    <USlideover v-model:open="formOpen" :title="editingId ? 'Edit Trip' : 'New Trip'" :ui="{ content: 'sm:max-w-lg' }">
      <template #body>
        <form class="space-y-4" @submit.prevent="submitTrip">
          <UFormField label="Name" name="name">
            <UInput v-model="form.name" icon="i-lucide-tag" required class="w-full" />
          </UFormField>
          <UFormField label="Destination" name="destination">
            <UInput v-model="form.destination" class="w-full" />
          </UFormField>
          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Start date" name="startDate">
              <UInput v-model="form.startDate" type="date" class="w-full" />
            </UFormField>
            <UFormField label="End date" name="endDate">
              <UInput v-model="form.endDate" type="date" class="w-full" />
            </UFormField>
          </div>
          <UFormField label="Budget (optional)" name="budget">
            <UInput v-model.number="form.budget" type="number" step="0.01" icon="i-lucide-indian-rupee" class="w-full" />
          </UFormField>
          <UFormField label="Notes" name="notes">
            <UTextarea v-model="form.notes" :rows="3" class="w-full" />
          </UFormField>
          <UAlert v-if="formError" icon="i-lucide-circle-alert" color="error" variant="subtle" :description="formError" />
          <UButton type="submit" icon="i-lucide-save" :loading="saving" block>{{ editingId ? 'Save Trip' : 'Create Trip' }}</UButton>
        </form>
      </template>
    </USlideover>
  </section>
</template>

<script setup lang="ts">
import { useSwalekhaApiClient, type SwalekhaTrip, type SwalekhaTripPayload } from '../../utils/swalekha-api'

useHead({ title: 'Travel Expense Sheets - Swalekha' })

const api = useSwalekhaApiClient()
const loading = ref(false)
const error = ref('')
const trips = ref<SwalekhaTrip[]>([])
const includeClosed = ref(false)

const tripColumns = [
  { accessorKey: 'name', header: 'Name' },
  { accessorKey: 'destination', header: 'Destination' },
  { accessorKey: 'dates', header: 'Dates' },
  { accessorKey: 'budget', header: 'Budget' },
  { accessorKey: 'spentTotal', header: 'Spent' },
  { accessorKey: 'isClosed', header: 'Status' },
  { accessorKey: 'actions', header: '', enableSorting: false }
]

function formatCurrency(value: number) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(value || 0)
}

function formatDate(value?: string | null) {
  if (!value) return null
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  return new Intl.DateTimeFormat('en-IN', { dateStyle: 'medium' }).format(date)
}

function formatRange(trip: SwalekhaTrip) {
  const start = formatDate(trip.startDate)
  const end = formatDate(trip.endDate)
  if (start && end) return `${start} - ${end}`
  return start || end || '-'
}

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    trips.value = await api.get<SwalekhaTrip[]>(`trips?includeClosed=${includeClosed.value}`)
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not load trips.'
  } finally {
    loading.value = false
  }
}

onMounted(refresh)

const formOpen = ref(false)
const saving = ref(false)
const formError = ref('')
const editingId = ref<string | null>(null)
const form = reactive<SwalekhaTripPayload>(defaultForm())

function defaultForm(): SwalekhaTripPayload {
  return { name: '', destination: '', startDate: '', endDate: '', budget: null, notes: '' }
}

function openCreate() {
  editingId.value = null
  formError.value = ''
  Object.assign(form, defaultForm())
  formOpen.value = true
}

function openEdit(trip: SwalekhaTrip) {
  editingId.value = trip.id
  formError.value = ''
  Object.assign(form, {
    name: trip.name,
    destination: trip.destination || '',
    startDate: trip.startDate ? trip.startDate.slice(0, 10) : '',
    endDate: trip.endDate ? trip.endDate.slice(0, 10) : '',
    budget: trip.budget ?? null,
    notes: trip.notes || ''
  })
  formOpen.value = true
}

async function submitTrip() {
  saving.value = true
  formError.value = ''
  try {
    if (editingId.value) {
      await api.put(`trips/${editingId.value}`, form)
    } else {
      await api.post('trips', form)
    }
    formOpen.value = false
    await refresh()
  } catch (err) {
    formError.value = err instanceof Error ? err.message : 'Could not save the trip.'
  } finally {
    saving.value = false
  }
}

async function toggleClose(trip: SwalekhaTrip) {
  const action = trip.isClosed ? 'reopen' : 'close'
  if (!confirm(trip.isClosed ? `Reopen "${trip.name}" for more expenses?` : `Close "${trip.name}"? Its spend already counts under Travel - closing just marks the trip finished.`)) return
  try {
    await api.post(`trips/${trip.id}/${action}`)
    await refresh()
  } catch (err) {
    error.value = err instanceof Error ? err.message : `Could not ${action} the trip.`
  }
}

async function deleteTrip(trip: SwalekhaTrip) {
  if (!confirm(`Delete trip "${trip.name}"? Its expense entries are kept for the record.`)) return
  try {
    await api.del(`trips/${trip.id}`)
    await refresh()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not delete the trip.'
  }
}
</script>
