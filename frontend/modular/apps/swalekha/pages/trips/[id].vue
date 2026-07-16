<template>
  <section class="space-y-5">
    <div class="flex flex-wrap items-start justify-between gap-3">
      <div>
        <UButton icon="i-lucide-arrow-left" color="neutral" variant="ghost" size="sm" to="/trips">Trips</UButton>
        <h1 class="garmetix-dashboard-title mt-1">{{ trip?.name || 'Trip' }}</h1>
        <p v-if="trip" class="text-sm text-muted">
          {{ trip.destination || 'No destination set' }} - Spent {{ formatCurrency(trip.spentTotal) }}<span v-if="trip.budget != null"> of {{ formatCurrency(trip.budget) }} budget</span>
        </p>
      </div>
      <div class="flex flex-wrap items-center gap-2">
        <UBadge v-if="trip" :color="trip.isClosed ? 'neutral' : 'success'" variant="subtle">{{ trip.isClosed ? 'Closed' : 'Open' }}</UBadge>
        <UButton v-if="trip" :icon="trip.isClosed ? 'i-lucide-lock-open' : 'i-lucide-check-check'" color="neutral" variant="soft" @click="toggleClose">
          {{ trip.isClosed ? 'Reopen' : 'Close Trip' }}
        </UButton>
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refreshAll">Refresh</UButton>
      </div>
    </div>

    <UAlert v-if="error" icon="i-lucide-circle-alert" color="error" variant="subtle" title="Could not load trip" :description="error" />
    <UAlert
      v-if="trip?.isClosed"
      icon="i-lucide-info"
      color="neutral"
      variant="subtle"
      description="This trip is closed. Its spend already counts under the Travel category in Expense reporting - reopen it if you need to add more entries."
    />

    <UCard v-if="!trip?.isClosed">
      <template #header>
        <h2 class="text-sm font-semibold">Add Expense</h2>
      </template>
      <form class="grid gap-3 sm:grid-cols-5 sm:items-end" @submit.prevent="submitEntry">
        <UFormField label="Category" name="category" class="sm:col-span-1">
          <UInput v-model="entryForm.category" placeholder="Food, Transport, Stay..." class="w-full" />
        </UFormField>
        <UFormField label="Amount" name="amount" class="sm:col-span-1">
          <UInput v-model.number="entryForm.amount" type="number" step="0.01" icon="i-lucide-indian-rupee" class="w-full" />
        </UFormField>
        <UFormField label="Date" name="entryDate" class="sm:col-span-1">
          <UInput v-model="entryForm.entryDate" type="date" class="w-full" />
        </UFormField>
        <UFormField label="Narration" name="narration" class="sm:col-span-1">
          <UInput v-model="entryForm.narration" class="w-full" />
        </UFormField>
        <UButton type="submit" icon="i-lucide-plus" :loading="entrySaving" class="sm:col-span-1">Add</UButton>
      </form>
      <UAlert v-if="entryError" class="mt-3" icon="i-lucide-circle-alert" color="error" variant="subtle" :description="entryError" />
    </UCard>

    <UCard :ui="{ body: 'p-0' }">
      <UTable :data="entries" :columns="entryColumns" :loading="loading" class="w-full">
        <template #entryDate-cell="{ row }">
          {{ formatDate(row.original.entryDate) }}
        </template>
        <template #amount-cell="{ row }">
          <span class="font-mono text-sm">{{ formatCurrency(row.original.amount) }}</span>
        </template>
        <template #actions-cell="{ row }">
          <UTooltip text="Delete">
            <UButton icon="i-lucide-trash-2" color="error" variant="ghost" size="sm" @click="deleteEntry(row.original)" />
          </UTooltip>
        </template>
      </UTable>
      <div class="flex items-center justify-between gap-3 p-4">
        <p class="text-xs text-muted">{{ totalCount }} entries</p>
        <div class="flex items-center gap-2">
          <UButton icon="i-lucide-chevron-left" color="neutral" variant="soft" size="sm" :disabled="page <= 1" @click="changePage(page - 1)" />
          <span class="text-xs text-muted">Page {{ page }}</span>
          <UButton icon="i-lucide-chevron-right" color="neutral" variant="soft" size="sm" :disabled="page * pageSize >= totalCount" @click="changePage(page + 1)" />
        </div>
      </div>
    </UCard>
  </section>
</template>

<script setup lang="ts">
import { useSwalekhaApiClient, type SwalekhaExpenseEntry, type SwalekhaExpenseEntryList, type SwalekhaExpenseEntryPayload, type SwalekhaTrip } from '../../utils/swalekha-api'

const route = useRoute()
const tripId = computed(() => String(route.params.id))
const api = useSwalekhaApiClient()

useHead({ title: 'Trip - Swalekha' })

const loading = ref(false)
const error = ref('')
const trip = ref<SwalekhaTrip | null>(null)
const entries = ref<SwalekhaExpenseEntry[]>([])
const page = ref(1)
const pageSize = ref(25)
const totalCount = ref(0)

const entryColumns = [
  { accessorKey: 'entryDate', header: 'Date' },
  { accessorKey: 'category', header: 'Category' },
  { accessorKey: 'narration', header: 'Narration' },
  { accessorKey: 'amount', header: 'Amount' },
  { accessorKey: 'actions', header: '', enableSorting: false }
]

function formatCurrency(value: number) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(value || 0)
}

function formatDate(value: string) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  return new Intl.DateTimeFormat('en-IN', { dateStyle: 'medium' }).format(date)
}

async function loadTrip() {
  trip.value = await api.get<SwalekhaTrip>(`trips/${tripId.value}`)
}

async function loadEntries() {
  if (!trip.value) return
  const result = await api.get<SwalekhaExpenseEntryList>(`expense-sheets/${trip.value.sheetId}/entries?page=${page.value}&pageSize=${pageSize.value}`)
  entries.value = result.rows
  totalCount.value = result.totalCount
}

async function refreshAll() {
  loading.value = true
  error.value = ''
  try {
    await loadTrip()
    await loadEntries()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not load this trip.'
  } finally {
    loading.value = false
  }
}

function changePage(next: number) {
  if (next < 1) return
  page.value = next
  loadEntries()
}

onMounted(refreshAll)

const entrySaving = ref(false)
const entryError = ref('')
const entryForm = reactive<SwalekhaExpenseEntryPayload>({
  category: '',
  amount: 0,
  entryDate: new Date().toISOString().slice(0, 10),
  narration: '',
  isHidden: false
})

async function submitEntry() {
  if (!trip.value) return
  entrySaving.value = true
  entryError.value = ''
  try {
    await api.post(`expense-sheets/${trip.value.sheetId}/entries`, entryForm)
    entryForm.category = ''
    entryForm.amount = 0
    entryForm.narration = ''
    page.value = 1
    await refreshAll()
  } catch (err) {
    entryError.value = err instanceof Error ? err.message : 'Could not add the entry.'
  } finally {
    entrySaving.value = false
  }
}

async function deleteEntry(entry: SwalekhaExpenseEntry) {
  if (!trip.value) return
  if (!confirm('Delete this expense entry?')) return
  try {
    await api.del(`expense-sheets/${trip.value.sheetId}/entries/${entry.id}`)
    await refreshAll()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not delete the entry.'
  }
}

async function toggleClose() {
  if (!trip.value) return
  const action = trip.value.isClosed ? 'reopen' : 'close'
  if (!confirm(trip.value.isClosed ? 'Reopen this trip for more expenses?' : 'Close this trip? Its spend already counts under Travel - closing just marks the trip finished.')) return
  try {
    await api.post(`trips/${tripId.value}/${action}`)
    await refreshAll()
  } catch (err) {
    error.value = err instanceof Error ? err.message : `Could not ${action} the trip.`
  }
}
</script>
