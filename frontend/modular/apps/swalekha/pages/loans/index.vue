<template>
  <section class="space-y-5">
    <div class="flex flex-wrap items-start justify-between gap-3">
      <div>
        <p class="garmetix-kicker"><UIcon name="i-lucide-landmark" class="size-4" /> Swalekha</p>
        <h1 class="garmetix-dashboard-title">Loans</h1>
      </div>
      <div class="flex flex-wrap items-center gap-2">
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
        <UButton icon="i-lucide-plus" @click="openCreate">New Loan</UButton>
      </div>
    </div>

    <UAlert v-if="error" icon="i-lucide-circle-alert" color="error" variant="subtle" title="Could not load loans" :description="error" />

    <div class="swalekha-grid">
      <UCard :ui="{ body: 'p-4' }">
        <p class="text-xs font-medium uppercase text-muted">Total Outstanding</p>
        <p class="truncate text-lg font-semibold text-error">{{ formatCurrency(totalOutstanding) }}</p>
      </UCard>
      <UCard :ui="{ body: 'p-4' }">
        <p class="text-xs font-medium uppercase text-muted">Monthly EMI Commitment</p>
        <p class="truncate text-lg font-semibold text-highlighted">{{ formatCurrency(monthlyEmiCommitment) }}</p>
      </UCard>
      <UCard :ui="{ body: 'p-4' }">
        <p class="text-xs font-medium uppercase text-muted">Active Loans</p>
        <p class="truncate text-lg font-semibold text-highlighted">{{ loans.filter(l => !l.isClosed).length }}</p>
      </UCard>
    </div>

    <UCard :ui="{ body: 'p-0' }">
      <UTable :data="loans" :columns="loanColumns" :loading="loading" class="w-full">
        <template #loanType-cell="{ row }">
          <UBadge color="primary" variant="subtle">{{ row.original.loanType }}</UBadge>
        </template>
        <template #outstandingPrincipal-cell="{ row }"><span class="font-mono text-sm">{{ formatCurrency(row.original.outstandingPrincipal) }}</span></template>
        <template #emiAmount-cell="{ row }"><span class="font-mono text-sm">{{ formatCurrency(row.original.emiAmount) }}</span></template>
        <template #isClosed-cell="{ row }">
          <UBadge :color="row.original.isClosed ? 'neutral' : 'success'" variant="subtle">{{ row.original.isClosed ? 'Closed' : 'Active' }}</UBadge>
        </template>
        <template #actions-cell="{ row }">
          <div class="flex justify-end gap-1">
            <UButton icon="i-lucide-list" color="neutral" variant="ghost" size="sm" title="Schedule & Payments" @click="navigateTo(`/loans/${row.original.id}`)" />
            <UButton icon="i-lucide-pencil" color="neutral" variant="ghost" size="sm" title="Edit" @click="openEdit(row.original)" />
            <UButton icon="i-lucide-trash-2" color="error" variant="ghost" size="sm" title="Delete" @click="deleteLoan(row.original)" />
          </div>
        </template>
      </UTable>
    </UCard>

    <UCard>
      <template #header>
        <p class="font-semibold text-highlighted">Loans Given</p>
      </template>
      <p class="text-sm text-muted">
        Money you've lent to family or friends is tracked on the <ULink to="/contacts" class="text-primary">Contacts</ULink> page's Person Ledger, not
        duplicated here - use a "Loan Given" entry there and Settle when it's repaid.
      </p>
      <p v-if="receivableFromContacts > 0" class="mt-2 text-sm">
        Currently owed to you across all contacts: <span class="font-semibold text-success">{{ formatCurrency(receivableFromContacts) }}</span>
      </p>
    </UCard>

    <USlideover v-model:open="formOpen" :title="editingId ? 'Edit Loan' : 'New Loan'" :ui="{ content: 'sm:max-w-lg' }">
      <template #body>
        <form class="space-y-4" @submit.prevent="submitLoan">
          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Loan Type" name="loanType">
              <USelect v-model="form.loanType" :items="['Personal', 'Home', 'Car', 'Gold', 'Education', 'Other']" class="w-full" />
            </UFormField>
            <UFormField label="Lender Name" name="lenderName">
              <UInput v-model="form.lenderName" required class="w-full" />
            </UFormField>
          </div>

          <UFormField label="Loan Number" name="loanNumber">
            <UInput v-model="form.loanNumber" class="w-full" />
          </UFormField>

          <UFormField label="Linked Account" name="accountId" description="EMI/prepayments debit this account">
            <USelectMenu v-model="form.accountId" :items="accountOptions" value-key="value" placeholder="Not linked" class="w-full" />
          </UFormField>

          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Principal Amount" name="principalAmount">
              <UInput v-model.number="form.principalAmount" type="number" step="0.01" icon="i-lucide-indian-rupee" :disabled="!!editingId" class="w-full" />
            </UFormField>
            <UFormField label="Interest Rate % (annual)" name="interestRatePercent">
              <UInput v-model.number="form.interestRatePercent" type="number" step="0.01" class="w-full" />
            </UFormField>
          </div>

          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Tenure (months)" name="tenureMonths">
              <UInput v-model.number="form.tenureMonths" type="number" class="w-full" />
            </UFormField>
            <UFormField label="Start Date" name="startDate">
              <UInput v-model="form.startDate" type="date" class="w-full" />
            </UFormField>
          </div>

          <UFormField label="EMI Amount" name="emiAmount">
            <div class="flex gap-2">
              <UInput v-model.number="form.emiAmount" type="number" step="0.01" icon="i-lucide-indian-rupee" class="w-full" />
              <UButton icon="i-lucide-calculator" color="neutral" variant="soft" :loading="calculatingEmi" @click="calculateEmi">Suggest</UButton>
            </div>
          </UFormField>

          <UFormField label="Notes" name="notes">
            <UTextarea v-model="form.notes" :rows="3" class="w-full" />
          </UFormField>

          <UAlert v-if="formError" icon="i-lucide-circle-alert" color="error" variant="subtle" :description="formError" />

          <UButton type="submit" icon="i-lucide-save" :loading="saving" block>{{ editingId ? 'Save' : 'Create Loan' }}</UButton>
        </form>
      </template>
    </USlideover>
  </section>
</template>

<script setup lang="ts">
import {
  useSwalekhaApiClient,
  type SwalekhaAccount,
  type SwalekhaCalculateEmiResult,
  type SwalekhaContact,
  type SwalekhaLoan,
  type SwalekhaLoanPayload
} from '../../utils/swalekha-api'

useHead({ title: 'Loans - Swalekha' })

const api = useSwalekhaApiClient()
const loading = ref(false)
const error = ref('')
const loans = ref<SwalekhaLoan[]>([])
const accounts = ref<SwalekhaAccount[]>([])
const contacts = ref<SwalekhaContact[]>([])

const loanColumns = [
  { accessorKey: 'lenderName', header: 'Lender' },
  { accessorKey: 'loanType', header: 'Type' },
  { accessorKey: 'outstandingPrincipal', header: 'Outstanding' },
  { accessorKey: 'emiAmount', header: 'EMI' },
  { accessorKey: 'isClosed', header: 'Status' },
  { accessorKey: 'actions', header: '', enableSorting: false }
]

const accountOptions = computed(() => [
  { label: 'Not linked', value: null },
  ...accounts.value.filter(a => a.isActive).map(a => ({ label: `${a.name} (${a.accountType})`, value: a.id }))
])

const totalOutstanding = computed(() => loans.value.filter(l => !l.isClosed).reduce((sum, l) => sum + l.outstandingPrincipal, 0))
const monthlyEmiCommitment = computed(() => loans.value.filter(l => !l.isClosed).reduce((sum, l) => sum + l.emiAmount, 0))
const receivableFromContacts = computed(() => contacts.value.filter(c => c.balance > 0).reduce((sum, c) => sum + c.balance, 0))

function formatCurrency(value: number) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(value || 0)
}

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const [loansResult, accountsResult, contactsResult] = await Promise.all([
      api.get<SwalekhaLoan[]>('loans?includeClosed=true'),
      api.get<SwalekhaAccount[]>('accounts'),
      api.get<SwalekhaContact[]>('contacts?includeInactive=true')
    ])
    loans.value = loansResult
    accounts.value = accountsResult
    contacts.value = contactsResult
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not load loans.'
  } finally {
    loading.value = false
  }
}

onMounted(refresh)

const formOpen = ref(false)
const saving = ref(false)
const formError = ref('')
const calculatingEmi = ref(false)
const editingId = ref<string | null>(null)
const form = reactive<SwalekhaLoanPayload>(defaultForm())

function defaultForm(): SwalekhaLoanPayload {
  return {
    loanType: 'Personal', lenderName: '', loanNumber: '', accountId: null,
    principalAmount: 0, interestRatePercent: 0, tenureMonths: 12, emiAmount: 0,
    startDate: new Date().toISOString().substring(0, 10), notes: ''
  }
}

function openCreate() {
  editingId.value = null
  formError.value = ''
  Object.assign(form, defaultForm())
  formOpen.value = true
}

function openEdit(loan: SwalekhaLoan) {
  editingId.value = loan.id
  formError.value = ''
  Object.assign(form, {
    loanType: loan.loanType,
    lenderName: loan.lenderName,
    loanNumber: loan.loanNumber || '',
    accountId: loan.accountId || null,
    principalAmount: loan.principalAmount,
    interestRatePercent: loan.interestRatePercent,
    tenureMonths: loan.tenureMonths,
    emiAmount: loan.emiAmount,
    startDate: loan.startDate.substring(0, 10),
    notes: loan.notes || ''
  })
  formOpen.value = true
}

async function calculateEmi() {
  calculatingEmi.value = true
  try {
    const result = await api.post<SwalekhaCalculateEmiResult>('loans/calculate-emi', {
      principalAmount: form.principalAmount,
      interestRatePercent: form.interestRatePercent,
      tenureMonths: form.tenureMonths
    })
    form.emiAmount = result.emiAmount
  } catch (err) {
    formError.value = err instanceof Error ? err.message : 'Could not calculate the EMI.'
  } finally {
    calculatingEmi.value = false
  }
}

async function submitLoan() {
  saving.value = true
  formError.value = ''
  try {
    if (editingId.value) {
      await api.put(`loans/${editingId.value}`, form)
    } else {
      await api.post('loans', form)
    }
    formOpen.value = false
    await refresh()
  } catch (err) {
    formError.value = err instanceof Error ? err.message : 'Could not save the loan.'
  } finally {
    saving.value = false
  }
}

async function deleteLoan(loan: SwalekhaLoan) {
  if (!confirm(`Delete the loan from "${loan.lenderName}"? Its payment history is kept for the record.`)) return
  try {
    await api.del(`loans/${loan.id}`)
    await refresh()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not delete the loan.'
  }
}
</script>
