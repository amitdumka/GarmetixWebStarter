<template>
  <section class="space-y-5">
    <div class="flex flex-wrap items-start justify-between gap-3">
      <div>
        <UButton icon="i-lucide-arrow-left" color="neutral" variant="ghost" size="sm" to="/contacts">Contacts</UButton>
        <h1 class="garmetix-dashboard-title mt-1">{{ contact?.name || 'Contact' }}</h1>
        <p v-if="contact" class="text-sm" :class="contact.balance > 0 ? 'text-success' : contact.balance < 0 ? 'text-error' : 'text-muted'">
          {{ contact.balance > 0 ? `Owes you ${formatCurrency(contact.balance)}` : contact.balance < 0 ? `You owe ${formatCurrency(Math.abs(contact.balance))}` : 'Settled - balance is zero' }}
        </p>
      </div>
      <div class="flex flex-wrap items-center gap-2">
        <UButton v-if="contact && contact.balance !== 0" icon="i-lucide-check-check" color="neutral" variant="soft" :loading="settling" @click="settle">Settle</UButton>
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refreshAll">Refresh</UButton>
      </div>
    </div>

    <UAlert v-if="error" icon="i-lucide-circle-alert" color="error" variant="subtle" title="Could not load contact" :description="error" />

    <UCard v-if="contact">
      <div class="swalekha-grid">
        <div>
          <p class="text-xs font-medium uppercase text-muted">Phone</p>
          <p class="text-sm text-highlighted">{{ contact.phone || '-' }}</p>
        </div>
        <div>
          <p class="text-xs font-medium uppercase text-muted">Email</p>
          <p class="text-sm text-highlighted">{{ contact.email || '-' }}</p>
        </div>
        <div>
          <p class="text-xs font-medium uppercase text-muted">Relationship</p>
          <p class="text-sm text-highlighted">{{ contact.relationship || '-' }}</p>
        </div>
      </div>
    </UCard>

    <UCard>
      <template #header>
        <h2 class="text-sm font-semibold">Add Ledger Entry</h2>
      </template>
      <form class="grid gap-3 sm:grid-cols-5 sm:items-end" @submit.prevent="submitEntry">
        <UFormField label="Type" name="entryType" class="sm:col-span-1">
          <USelect v-model="entryForm.entryType" :items="entryTypes" class="w-full" />
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
      <p class="mt-2 text-xs text-muted">Loan Given / Repayment Paid increase what's owed to you or reduce what you owe; Loan Taken / Repayment Received do the reverse.</p>
      <UAlert v-if="entryError" class="mt-3" icon="i-lucide-circle-alert" color="error" variant="subtle" :description="entryError" />
    </UCard>

    <UCard :ui="{ body: 'p-0' }">
      <UTable :data="entries" :columns="entryColumns" :loading="loading" class="w-full">
        <template #entryDate-cell="{ row }">
          {{ formatDate(row.original.entryDate) }}
        </template>
        <template #entryType-cell="{ row }">
          <UBadge :color="entryColor(row.original.entryType)" variant="subtle">{{ entryLabel(row.original.entryType) }}</UBadge>
        </template>
        <template #amount-cell="{ row }">
          <span class="font-mono text-sm">{{ formatCurrency(row.original.amount) }}</span>
        </template>
        <template #runningBalance-cell="{ row }">
          <span class="font-mono text-sm">{{ formatCurrency(row.original.runningBalance) }}</span>
        </template>
        <template #actions-cell="{ row }">
          <UButton icon="i-lucide-trash-2" color="error" variant="ghost" size="sm" title="Delete" @click="deleteEntry(row.original)" />
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
import { useSwalekhaApiClient, type SwalekhaContact, type SwalekhaPersonLedgerEntry, type SwalekhaPersonLedgerEntryPayload, type SwalekhaPersonLedgerEntryType, type SwalekhaPersonLedgerList } from '../../utils/swalekha-api'

const route = useRoute()
const contactId = computed(() => String(route.params.id))
const api = useSwalekhaApiClient()

useHead({ title: 'Contact Ledger - Swalekha' })

const loading = ref(false)
const error = ref('')
const contact = ref<SwalekhaContact | null>(null)
const entries = ref<SwalekhaPersonLedgerEntry[]>([])
const page = ref(1)
const pageSize = ref(25)
const totalCount = ref(0)

const entryTypes: SwalekhaPersonLedgerEntryType[] = ['LoanGiven', 'LoanTaken', 'RepaymentReceived', 'RepaymentPaid']

const entryColumns = [
  { accessorKey: 'entryDate', header: 'Date' },
  { accessorKey: 'entryType', header: 'Type' },
  { accessorKey: 'narration', header: 'Narration' },
  { accessorKey: 'amount', header: 'Amount' },
  { accessorKey: 'runningBalance', header: 'Balance' },
  { accessorKey: 'actions', header: '', enableSorting: false }
]

function entryLabel(type: string) {
  return { LoanGiven: 'Loan Given', LoanTaken: 'Loan Taken', RepaymentReceived: 'Repayment Received', RepaymentPaid: 'Repayment Paid' }[type] || type
}

function entryColor(type: string) {
  return type === 'LoanGiven' || type === 'RepaymentPaid' ? 'success' : 'error'
}

function formatCurrency(value: number) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(value || 0)
}

function formatDate(value: string) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  return new Intl.DateTimeFormat('en-IN', { dateStyle: 'medium' }).format(date)
}

async function loadContact() {
  contact.value = await api.get<SwalekhaContact>(`contacts/${contactId.value}`)
}

async function loadEntries() {
  const result = await api.get<SwalekhaPersonLedgerList>(`contacts/${contactId.value}/ledger?page=${page.value}&pageSize=${pageSize.value}`)
  entries.value = result.rows
  totalCount.value = result.totalCount
}

async function refreshAll() {
  loading.value = true
  error.value = ''
  try {
    await Promise.all([loadContact(), loadEntries()])
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not load this contact.'
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
const entryForm = reactive<SwalekhaPersonLedgerEntryPayload>({
  amount: 0,
  entryDate: new Date().toISOString().slice(0, 10),
  narration: '',
  entryType: 'LoanGiven'
})

async function submitEntry() {
  entrySaving.value = true
  entryError.value = ''
  try {
    await api.post(`contacts/${contactId.value}/ledger`, entryForm)
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

async function deleteEntry(entry: SwalekhaPersonLedgerEntry) {
  if (!confirm('Delete this ledger entry? The contact balance will be reversed accordingly.')) return
  try {
    await api.del(`contacts/${contactId.value}/ledger/${entry.id}`)
    await refreshAll()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not delete the entry.'
  }
}

const settling = ref(false)
async function settle() {
  if (!contact.value || contact.value.balance === 0) return
  if (!confirm('Post a repayment entry that brings this contact\'s balance to zero?')) return
  settling.value = true
  try {
    await api.post(`contacts/${contactId.value}/settle`, { entryDate: new Date().toISOString().slice(0, 10), narration: 'Settled' })
    await refreshAll()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not settle this contact.'
  } finally {
    settling.value = false
  }
}
</script>
