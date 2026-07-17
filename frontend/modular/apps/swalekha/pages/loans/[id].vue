<template>
  <section class="space-y-5">
    <div class="flex flex-wrap items-start justify-between gap-3">
      <div>
        <UButton icon="i-lucide-arrow-left" color="neutral" variant="ghost" size="sm" to="/loans">Loans</UButton>
        <h1 class="garmetix-dashboard-title mt-1">{{ loan?.lenderName || 'Loan' }}</h1>
        <p v-if="loan" class="text-sm text-muted">{{ loan.loanType }}<span v-if="loan.loanNumber"> - {{ loan.loanNumber }}</span></p>
      </div>
      <div class="flex flex-wrap items-center gap-2">
        <UBadge v-if="loan" :color="loan.isClosed ? 'neutral' : 'success'" variant="subtle">{{ loan.isClosed ? 'Closed' : 'Active' }}</UBadge>
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refreshAll">Refresh</UButton>
      </div>
    </div>

    <UAlert v-if="error" icon="i-lucide-circle-alert" color="error" variant="subtle" title="Could not load this loan" :description="error" />

    <div class="swalekha-grid" v-if="loan">
      <UCard :ui="{ body: 'p-4' }">
        <p class="text-xs font-medium uppercase text-muted">Principal</p>
        <p class="truncate text-lg font-semibold text-highlighted">{{ formatCurrency(loan.principalAmount) }}</p>
      </UCard>
      <UCard :ui="{ body: 'p-4' }">
        <p class="text-xs font-medium uppercase text-muted">Outstanding</p>
        <p class="truncate text-lg font-semibold text-error">{{ formatCurrency(loan.outstandingPrincipal) }}</p>
      </UCard>
      <UCard :ui="{ body: 'p-4' }">
        <p class="text-xs font-medium uppercase text-muted">EMI</p>
        <p class="truncate text-lg font-semibold text-highlighted">{{ formatCurrency(loan.emiAmount) }}</p>
      </UCard>
      <UCard :ui="{ body: 'p-4' }">
        <p class="text-xs font-medium uppercase text-muted">Interest Rate</p>
        <p class="truncate text-lg font-semibold text-highlighted">{{ loan.interestRatePercent }}%</p>
      </UCard>
    </div>

    <UCard v-if="!loan?.isClosed">
      <template #header>
        <h2 class="text-sm font-semibold">Record Payment</h2>
      </template>
      <form class="grid gap-3 sm:grid-cols-5 sm:items-end" @submit.prevent="submitPayment">
        <UFormField label="Type" name="paymentType" class="sm:col-span-1">
          <USelect v-model="paymentForm.paymentType" :items="['Emi', 'Prepayment']" class="w-full" />
        </UFormField>
        <UFormField label="Amount" name="amount" class="sm:col-span-1">
          <UInput v-model.number="paymentForm.amount" type="number" step="0.01" icon="i-lucide-indian-rupee" class="w-full" />
        </UFormField>
        <UFormField label="Date" name="paymentDate" class="sm:col-span-1">
          <UInput v-model="paymentForm.paymentDate" type="date" class="w-full" />
        </UFormField>
        <UFormField label="Narration" name="narration" class="sm:col-span-1">
          <UInput v-model="paymentForm.narration" class="w-full" />
        </UFormField>
        <UButton type="submit" icon="i-lucide-plus" :loading="paymentSaving" class="sm:col-span-1">Record</UButton>
      </form>
      <UAlert v-if="paymentError" class="mt-3" icon="i-lucide-circle-alert" color="error" variant="subtle" :description="paymentError" />
    </UCard>

    <UCard :ui="{ body: 'p-0' }">
      <template #header>
        <p class="font-semibold text-highlighted">Payment History</p>
      </template>
      <UTable :data="payments" :columns="paymentColumns" :loading="loading" class="w-full">
        <template #paymentDate-cell="{ row }">{{ formatDate(row.original.paymentDate) }}</template>
        <template #paymentType-cell="{ row }">
          <UBadge :color="row.original.paymentType === 'Prepayment' ? 'secondary' : 'primary'" variant="subtle">{{ row.original.paymentType }}</UBadge>
        </template>
        <template #amount-cell="{ row }"><span class="font-mono text-sm">{{ formatCurrency(row.original.amount) }}</span></template>
        <template #principalComponent-cell="{ row }"><span class="font-mono text-sm">{{ formatCurrency(row.original.principalComponent) }}</span></template>
        <template #interestComponent-cell="{ row }"><span class="font-mono text-sm">{{ formatCurrency(row.original.interestComponent) }}</span></template>
        <template #outstandingAfter-cell="{ row }"><span class="font-mono text-sm">{{ formatCurrency(row.original.outstandingAfter) }}</span></template>
        <template #actions-cell="{ row }">
          <UButton icon="i-lucide-trash-2" color="error" variant="ghost" size="sm" title="Delete" @click="deletePayment(row.original)" />
        </template>
      </UTable>
      <div class="flex items-center justify-between gap-3 p-4">
        <p class="text-xs text-muted">{{ totalCount }} payments</p>
        <div class="flex items-center gap-2">
          <UButton icon="i-lucide-chevron-left" color="neutral" variant="soft" size="sm" :disabled="page <= 1" @click="changePage(page - 1)" />
          <span class="text-xs text-muted">Page {{ page }}</span>
          <UButton icon="i-lucide-chevron-right" color="neutral" variant="soft" size="sm" :disabled="page * pageSize >= totalCount" @click="changePage(page + 1)" />
        </div>
      </div>
    </UCard>

    <UCard :ui="{ body: 'p-0' }">
      <template #header>
        <p class="font-semibold text-highlighted">Projected Amortization Schedule</p>
        <p class="text-xs text-muted">Projected forward from the current outstanding balance at the current EMI - actual payments may differ.</p>
      </template>
      <UTable :data="schedule" :columns="scheduleColumns" class="w-full">
        <template #openingBalance-cell="{ row }"><span class="font-mono text-sm">{{ formatCurrency(row.original.openingBalance) }}</span></template>
        <template #interestComponent-cell="{ row }"><span class="font-mono text-sm">{{ formatCurrency(row.original.interestComponent) }}</span></template>
        <template #principalComponent-cell="{ row }"><span class="font-mono text-sm">{{ formatCurrency(row.original.principalComponent) }}</span></template>
        <template #closingBalance-cell="{ row }"><span class="font-mono text-sm">{{ formatCurrency(row.original.closingBalance) }}</span></template>
      </UTable>
    </UCard>
  </section>
</template>

<script setup lang="ts">
import {
  useSwalekhaApiClient,
  type SwalekhaAmortizationRow,
  type SwalekhaLoan,
  type SwalekhaLoanPayment,
  type SwalekhaLoanPaymentList,
  type SwalekhaLoanPaymentPayload
} from '../../utils/swalekha-api'

const route = useRoute()
const loanId = computed(() => String(route.params.id))
const api = useSwalekhaApiClient()

useHead({ title: 'Loan - Swalekha' })

const loading = ref(false)
const error = ref('')
const loan = ref<SwalekhaLoan | null>(null)
const payments = ref<SwalekhaLoanPayment[]>([])
const schedule = ref<SwalekhaAmortizationRow[]>([])
const page = ref(1)
const pageSize = ref(25)
const totalCount = ref(0)

const paymentColumns = [
  { accessorKey: 'paymentDate', header: 'Date' },
  { accessorKey: 'paymentType', header: 'Type' },
  { accessorKey: 'amount', header: 'Amount' },
  { accessorKey: 'principalComponent', header: 'Principal' },
  { accessorKey: 'interestComponent', header: 'Interest' },
  { accessorKey: 'outstandingAfter', header: 'Outstanding After' },
  { accessorKey: 'actions', header: '', enableSorting: false }
]

const scheduleColumns = [
  { accessorKey: 'monthNumber', header: 'Month' },
  { accessorKey: 'openingBalance', header: 'Opening Balance' },
  { accessorKey: 'interestComponent', header: 'Interest' },
  { accessorKey: 'principalComponent', header: 'Principal' },
  { accessorKey: 'closingBalance', header: 'Closing Balance' }
]

function formatCurrency(value: number) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(value || 0)
}

function formatDate(value: string) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  return new Intl.DateTimeFormat('en-IN', { dateStyle: 'medium' }).format(date)
}

async function loadLoan() {
  loan.value = await api.get<SwalekhaLoan>(`loans/${loanId.value}`)
}

async function loadPayments() {
  const result = await api.get<SwalekhaLoanPaymentList>(`loans/${loanId.value}/payments?page=${page.value}&pageSize=${pageSize.value}`)
  payments.value = result.rows
  totalCount.value = result.totalCount
}

async function loadSchedule() {
  schedule.value = await api.get<SwalekhaAmortizationRow[]>(`loans/${loanId.value}/amortization-schedule`)
}

async function refreshAll() {
  loading.value = true
  error.value = ''
  try {
    await loadLoan()
    await Promise.all([loadPayments(), loadSchedule()])
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not load this loan.'
  } finally {
    loading.value = false
  }
}

function changePage(next: number) {
  if (next < 1) return
  page.value = next
  loadPayments()
}

onMounted(refreshAll)

const paymentSaving = ref(false)
const paymentError = ref('')
const paymentForm = reactive<SwalekhaLoanPaymentPayload>(defaultPaymentForm())

function defaultPaymentForm(): SwalekhaLoanPaymentPayload {
  return { paymentType: 'Emi', paymentDate: new Date().toISOString().substring(0, 10), amount: 0, narration: '' }
}

async function submitPayment() {
  paymentSaving.value = true
  paymentError.value = ''
  try {
    await api.post(`loans/${loanId.value}/payments`, paymentForm)
    Object.assign(paymentForm, defaultPaymentForm())
    if (loan.value) paymentForm.amount = loan.value.emiAmount
    page.value = 1
    await refreshAll()
  } catch (err) {
    paymentError.value = err instanceof Error ? err.message : 'Could not record the payment.'
  } finally {
    paymentSaving.value = false
  }
}

async function deletePayment(payment: SwalekhaLoanPayment) {
  if (!confirm('Delete this payment? This reverses its effect on the outstanding balance and the linked account.')) return
  try {
    await api.del(`loans/${loanId.value}/payments/${payment.id}`)
    await refreshAll()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not delete the payment.'
  }
}
</script>
