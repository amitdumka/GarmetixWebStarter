<template>
  <section class="space-y-5">
    <div class="flex flex-wrap items-start justify-between gap-3">
      <div>
        <p class="garmetix-kicker"><UIcon name="i-lucide-landmark" class="size-4" /> Investments</p>
        <h1 class="garmetix-dashboard-title">Fixed Deposits</h1>
      </div>
      <div class="flex flex-wrap items-center gap-2">
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
        <UButton icon="i-lucide-plus" @click="openCreate">New Fixed Deposit</UButton>
      </div>
    </div>

    <UAlert v-if="error" icon="i-lucide-circle-alert" color="error" variant="subtle" title="Could not load fixed deposits" :description="error" />

    <USwitch v-model="includeClosed" label="Show matured deposits" @update:model-value="refresh" />

    <UCard :ui="{ body: 'p-0' }">
      <UTable :data="deposits" :columns="depositColumns" :loading="loading" class="w-full">
        <template #principalAmount-cell="{ row }">
          <span class="font-mono text-sm">{{ formatCurrency(row.original.principalAmount) }}</span>
        </template>
        <template #interestRatePercent-cell="{ row }">
          <span class="text-sm">{{ row.original.interestRatePercent }}%</span>
        </template>
        <template #maturityDate-cell="{ row }">
          <span class="text-sm">{{ formatDate(row.original.maturityDate) }}</span>
        </template>
        <template #isClosed-cell="{ row }">
          <UBadge :color="row.original.isClosed ? 'neutral' : 'success'" variant="subtle">{{ row.original.isClosed ? 'Matured' : 'Active' }}</UBadge>
        </template>
        <template #actions-cell="{ row }">
          <div class="flex justify-end gap-1">
            <UButton
              v-if="!row.original.isClosed"
              icon="i-lucide-badge-check"
              color="success"
              variant="ghost"
              size="sm"
              title="Mark Matured"
              @click="openMature(row.original)"
            />
            <UButton icon="i-lucide-pencil" color="neutral" variant="ghost" size="sm" title="Edit" :disabled="row.original.isClosed" @click="openEdit(row.original)" />
            <UButton icon="i-lucide-trash-2" color="error" variant="ghost" size="sm" title="Delete" @click="deleteDeposit(row.original)" />
          </div>
        </template>
      </UTable>
    </UCard>

    <USlideover v-model:open="formOpen" :title="editingId ? 'Edit Fixed Deposit' : 'New Fixed Deposit'" :ui="{ content: 'sm:max-w-lg' }">
      <template #body>
        <form class="space-y-4" @submit.prevent="submitDeposit">
          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Bank Name" name="bankName">
              <UInput v-model="form.bankName" required class="w-full" />
            </UFormField>
            <UFormField label="FD Number" name="fdNumber">
              <UInput v-model="form.fdNumber" class="w-full" />
            </UFormField>
          </div>

          <UFormField label="Linked Account" name="accountId" description="Debited on creation is manual - this is where the maturity payout lands">
            <USelectMenu v-model="form.accountId" :items="accountOptions" value-key="value" placeholder="Not linked" class="w-full" />
          </UFormField>

          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Principal Amount" name="principalAmount">
              <UInput v-model.number="form.principalAmount" type="number" step="0.01" icon="i-lucide-indian-rupee" class="w-full" />
            </UFormField>
            <UFormField label="Interest Rate %" name="interestRatePercent">
              <UInput v-model.number="form.interestRatePercent" type="number" step="0.01" class="w-full" />
            </UFormField>
          </div>

          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Tenure (Months)" name="tenureMonths">
              <UInput v-model.number="form.tenureMonths" type="number" class="w-full" />
            </UFormField>
            <UFormField label="TDS Deducted" name="tdsDeducted">
              <UInput v-model.number="form.tdsDeducted" type="number" step="0.01" class="w-full" />
            </UFormField>
          </div>

          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Start Date" name="startDate">
              <UInput v-model="form.startDate" type="date" class="w-full" />
            </UFormField>
            <UFormField label="Maturity Date" name="maturityDate">
              <UInput v-model="form.maturityDate" type="date" class="w-full" />
            </UFormField>
          </div>

          <UFormField label="Expected Maturity Amount" name="maturityAmount">
            <UInput v-model.number="form.maturityAmount" type="number" step="0.01" class="w-full" />
          </UFormField>

          <USwitch v-model="form.autoRenew" label="Auto Renew" />

          <UFormField label="Notes" name="notes">
            <UTextarea v-model="form.notes" :rows="3" class="w-full" />
          </UFormField>

          <UAlert v-if="formError" icon="i-lucide-circle-alert" color="error" variant="subtle" :description="formError" />

          <UButton type="submit" icon="i-lucide-save" :loading="saving" block>{{ editingId ? 'Save' : 'Create Fixed Deposit' }}</UButton>
        </form>
      </template>
    </USlideover>

    <USlideover v-model:open="matureOpen" :title="`Mark Matured - ${matureTarget?.bankName || ''}`" :ui="{ content: 'sm:max-w-lg' }">
      <template #body>
        <form class="space-y-4" @submit.prevent="submitMature">
          <UFormField label="Final Maturity Amount" name="maturityAmount">
            <UInput v-model.number="matureForm.maturityAmount" type="number" step="0.01" icon="i-lucide-indian-rupee" class="w-full" />
          </UFormField>
          <UFormField label="Credited Date" name="maturityCreditedDate">
            <UInput v-model="matureForm.maturityCreditedDate" type="date" class="w-full" />
          </UFormField>
          <UFormField label="Narration" name="narration">
            <UInput v-model="matureForm.narration" class="w-full" />
          </UFormField>

          <UAlert
            v-if="matureTarget?.accountId"
            icon="i-lucide-info"
            color="info"
            variant="subtle"
            description="This will credit the linked account with the maturity amount."
          />
          <UAlert v-else icon="i-lucide-info" color="neutral" variant="subtle" description="No account linked - this only records the maturity, no ledger entry is posted." />

          <UAlert v-if="matureError" icon="i-lucide-circle-alert" color="error" variant="subtle" :description="matureError" />

          <UButton type="submit" icon="i-lucide-badge-check" :loading="maturing" block>Mark Matured</UButton>
        </form>
      </template>
    </USlideover>
  </section>
</template>

<script setup lang="ts">
import {
  useSwalekhaApiClient,
  type SwalekhaAccount,
  type SwalekhaFixedDeposit,
  type SwalekhaFixedDepositPayload,
  type SwalekhaMarkMaturedPayload
} from '../../utils/swalekha-api'

useHead({ title: 'Fixed Deposits - Swalekha' })

const api = useSwalekhaApiClient()
const loading = ref(false)
const error = ref('')
const deposits = ref<SwalekhaFixedDeposit[]>([])
const accounts = ref<SwalekhaAccount[]>([])
const includeClosed = ref(true)

const depositColumns = [
  { accessorKey: 'bankName', header: 'Bank' },
  { accessorKey: 'fdNumber', header: 'FD Number' },
  { accessorKey: 'principalAmount', header: 'Principal' },
  { accessorKey: 'interestRatePercent', header: 'Rate' },
  { accessorKey: 'maturityDate', header: 'Maturity Date' },
  { accessorKey: 'isClosed', header: 'Status' },
  { accessorKey: 'actions', header: '', enableSorting: false }
]

const accountOptions = computed(() => [
  { label: 'Not linked', value: null },
  ...accounts.value.filter(a => a.isActive).map(a => ({ label: `${a.name} (${a.accountType})`, value: a.id }))
])

function formatCurrency(value: number) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(value || 0)
}

function formatDate(value: string) {
  return new Date(value).toLocaleDateString('en-IN')
}

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const [depositsResult, accountsResult] = await Promise.all([
      api.get<SwalekhaFixedDeposit[]>(`fixed-deposits?includeClosed=${includeClosed.value}`),
      api.get<SwalekhaAccount[]>('accounts')
    ])
    deposits.value = depositsResult
    accounts.value = accountsResult
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not load fixed deposits.'
  } finally {
    loading.value = false
  }
}

onMounted(refresh)

const formOpen = ref(false)
const saving = ref(false)
const formError = ref('')
const editingId = ref<string | null>(null)
const form = reactive<SwalekhaFixedDepositPayload>(defaultForm())

function defaultForm(): SwalekhaFixedDepositPayload {
  return {
    bankName: '', fdNumber: '', accountId: null, principalAmount: 0, interestRatePercent: 0,
    tenureMonths: 12, startDate: new Date().toISOString().substring(0, 10), maturityDate: '',
    maturityAmount: null, autoRenew: false, tdsDeducted: null, notes: ''
  }
}

function openCreate() {
  editingId.value = null
  formError.value = ''
  Object.assign(form, defaultForm())
  formOpen.value = true
}

function openEdit(deposit: SwalekhaFixedDeposit) {
  editingId.value = deposit.id
  formError.value = ''
  Object.assign(form, {
    bankName: deposit.bankName,
    fdNumber: deposit.fdNumber || '',
    accountId: deposit.accountId || null,
    principalAmount: deposit.principalAmount,
    interestRatePercent: deposit.interestRatePercent,
    tenureMonths: deposit.tenureMonths,
    startDate: deposit.startDate.substring(0, 10),
    maturityDate: deposit.maturityDate.substring(0, 10),
    maturityAmount: deposit.maturityAmount ?? null,
    autoRenew: deposit.autoRenew,
    tdsDeducted: deposit.tdsDeducted ?? null,
    notes: deposit.notes || ''
  })
  formOpen.value = true
}

async function submitDeposit() {
  saving.value = true
  formError.value = ''
  try {
    if (editingId.value) {
      await api.put(`fixed-deposits/${editingId.value}`, form)
    } else {
      await api.post('fixed-deposits', form)
    }
    formOpen.value = false
    await refresh()
  } catch (err) {
    formError.value = err instanceof Error ? err.message : 'Could not save the fixed deposit.'
  } finally {
    saving.value = false
  }
}

async function deleteDeposit(deposit: SwalekhaFixedDeposit) {
  if (!confirm(`Delete FD "${deposit.bankName} ${deposit.fdNumber || ''}"?`)) return
  try {
    await api.del(`fixed-deposits/${deposit.id}`)
    await refresh()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not delete the fixed deposit.'
  }
}

const matureOpen = ref(false)
const maturing = ref(false)
const matureError = ref('')
const matureTarget = ref<SwalekhaFixedDeposit | null>(null)
const matureForm = reactive<SwalekhaMarkMaturedPayload>(defaultMatureForm())

function defaultMatureForm(): SwalekhaMarkMaturedPayload {
  return { maturityAmount: 0, maturityCreditedDate: new Date().toISOString().substring(0, 10), narration: '' }
}

function openMature(deposit: SwalekhaFixedDeposit) {
  matureTarget.value = deposit
  matureError.value = ''
  Object.assign(matureForm, defaultMatureForm())
  matureForm.maturityAmount = deposit.maturityAmount || deposit.principalAmount
  matureOpen.value = true
}

async function submitMature() {
  if (!matureTarget.value) return
  maturing.value = true
  matureError.value = ''
  try {
    await api.post(`fixed-deposits/${matureTarget.value.id}/mark-matured`, matureForm)
    matureOpen.value = false
    await refresh()
  } catch (err) {
    matureError.value = err instanceof Error ? err.message : 'Could not mark this fixed deposit matured.'
  } finally {
    maturing.value = false
  }
}
</script>
