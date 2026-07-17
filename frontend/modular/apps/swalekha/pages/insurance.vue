<template>
  <section class="space-y-5">
    <div class="flex flex-wrap items-start justify-between gap-3">
      <div>
        <p class="garmetix-kicker"><UIcon name="i-lucide-shield-check" class="size-4" /> Swalekha</p>
        <h1 class="garmetix-dashboard-title">Insurance</h1>
      </div>
      <div class="flex flex-wrap items-center gap-2">
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
        <UButton icon="i-lucide-plus" @click="openCreate">New Policy</UButton>
      </div>
    </div>

    <UAlert v-if="error" icon="i-lucide-circle-alert" color="error" variant="subtle" title="Could not load policies" :description="error" />

    <div class="swalekha-grid">
      <UCard :ui="{ body: 'p-4' }">
        <p class="text-xs font-medium uppercase text-muted">Total Sum Assured</p>
        <p class="truncate text-lg font-semibold text-highlighted">{{ formatCurrency(totalSumAssured) }}</p>
      </UCard>
      <UCard :ui="{ body: 'p-4' }">
        <p class="text-xs font-medium uppercase text-muted">Active Policies</p>
        <p class="truncate text-lg font-semibold text-highlighted">{{ policies.filter(p => p.isActive && !p.isMatured).length }}</p>
      </UCard>
      <UCard :ui="{ body: 'p-4' }">
        <p class="text-xs font-medium uppercase text-muted">Premiums Due</p>
        <p class="truncate text-lg font-semibold text-warning">{{ policies.filter(p => p.premiumDueNow).length }}</p>
      </UCard>
    </div>

    <UCard :ui="{ body: 'p-0' }">
      <UTable :data="policies" :columns="policyColumns" :loading="loading" class="w-full">
        <template #policyType-cell="{ row }">
          <UBadge color="primary" variant="subtle">{{ row.original.policyType }}</UBadge>
        </template>
        <template #premiumAmount-cell="{ row }">
          <span class="font-mono text-sm">{{ formatCurrency(row.original.premiumAmount) }}</span>
          <span class="text-xs text-muted"> / {{ row.original.premiumFrequency }}</span>
        </template>
        <template #nextPremiumDueDate-cell="{ row }">
          <span class="text-sm">{{ formatDate(row.original.nextPremiumDueDate) }}</span>
          <UBadge v-if="row.original.premiumDueNow" color="warning" variant="subtle" size="xs" class="ml-1">Due</UBadge>
        </template>
        <template #isActive-cell="{ row }">
          <UBadge v-if="row.original.isMatured" color="neutral" variant="subtle">Matured</UBadge>
          <UBadge v-else :color="row.original.isActive ? 'success' : 'neutral'" variant="subtle">{{ row.original.isActive ? 'Active' : 'Inactive' }}</UBadge>
        </template>
        <template #actions-cell="{ row }">
          <div class="flex justify-end gap-1">
            <UButton v-if="!row.original.isMatured" icon="i-lucide-indian-rupee" color="primary" variant="ghost" size="sm" title="Pay Premium" @click="openPayPremium(row.original)" />
            <UButton v-if="!row.original.isMatured && row.original.maturityAmount != null" icon="i-lucide-check-check" color="success" variant="ghost" size="sm" title="Mark Matured" @click="openMarkMatured(row.original)" />
            <UButton icon="i-lucide-pencil" color="neutral" variant="ghost" size="sm" title="Edit" @click="openEdit(row.original)" />
            <UButton icon="i-lucide-trash-2" color="error" variant="ghost" size="sm" title="Delete" @click="deletePolicy(row.original)" />
          </div>
        </template>
      </UTable>
    </UCard>

    <USlideover v-model:open="formOpen" :title="editingId ? 'Edit Policy' : 'New Policy'" :ui="{ content: 'sm:max-w-lg' }">
      <template #body>
        <form class="space-y-4" @submit.prevent="submitPolicy">
          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Policy Type" name="policyType">
              <USelect v-model="form.policyType" :items="['Life', 'Health', 'Term', 'Vehicle', 'Property', 'ULIP', 'Other']" class="w-full" />
            </UFormField>
            <UFormField label="Insurer" name="insurer">
              <UInput v-model="form.insurer" required class="w-full" />
            </UFormField>
          </div>

          <UFormField label="Policy Number" name="policyNumber">
            <UInput v-model="form.policyNumber" class="w-full" />
          </UFormField>

          <UFormField label="Linked Account" name="accountId" description="Premiums debit this account; maturity credits it">
            <USelectMenu v-model="form.accountId" :items="accountOptions" value-key="value" placeholder="Not linked" class="w-full" />
          </UFormField>

          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Sum Assured" name="sumAssured">
              <UInput v-model.number="form.sumAssured" type="number" step="0.01" icon="i-lucide-indian-rupee" class="w-full" />
            </UFormField>
            <UFormField label="Premium Amount" name="premiumAmount">
              <UInput v-model.number="form.premiumAmount" type="number" step="0.01" icon="i-lucide-indian-rupee" class="w-full" />
            </UFormField>
          </div>

          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Premium Frequency" name="premiumFrequency">
              <USelect v-model="form.premiumFrequency" :items="['Monthly', 'Quarterly', 'HalfYearly', 'Yearly']" class="w-full" />
            </UFormField>
            <UFormField label="Start Date" name="startDate">
              <UInput v-model="form.startDate" type="date" class="w-full" />
            </UFormField>
          </div>

          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Nominee Name" name="nomineeName">
              <UInput v-model="form.nomineeName" class="w-full" />
            </UFormField>
            <UFormField label="Nominee Relationship" name="nomineeRelationship">
              <UInput v-model="form.nomineeRelationship" class="w-full" />
            </UFormField>
          </div>

          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Maturity Date" name="maturityDate" description="Endowment/ULIP only">
              <UInput v-model="form.maturityDate" type="date" class="w-full" />
            </UFormField>
            <UFormField label="Expected Maturity Amount" name="maturityAmount">
              <UInput v-model.number="form.maturityAmount" type="number" step="0.01" icon="i-lucide-indian-rupee" class="w-full" />
            </UFormField>
          </div>

          <USwitch v-model="form.isActive" label="Active" />

          <UFormField label="Notes" name="notes">
            <UTextarea v-model="form.notes" :rows="3" class="w-full" />
          </UFormField>

          <UAlert v-if="formError" icon="i-lucide-circle-alert" color="error" variant="subtle" :description="formError" />

          <UButton type="submit" icon="i-lucide-save" :loading="saving" block>{{ editingId ? 'Save' : 'Create Policy' }}</UButton>
        </form>
      </template>
    </USlideover>

    <USlideover v-model:open="premiumOpen" :title="`Pay Premium - ${premiumTarget?.insurer || ''}`" :ui="{ content: 'sm:max-w-md' }">
      <template #body>
        <form class="space-y-4" @submit.prevent="submitPremium">
          <UFormField label="Payment Date" name="paymentDate">
            <UInput v-model="premiumForm.paymentDate" type="date" class="w-full" />
          </UFormField>
          <UFormField label="Narration" name="narration">
            <UInput v-model="premiumForm.narration" class="w-full" />
          </UFormField>
          <UAlert v-if="premiumTarget" icon="i-lucide-info" color="neutral" variant="subtle" :description="`Amount: ${formatCurrency(premiumTarget.premiumAmount)}`" />
          <UAlert v-if="premiumError" icon="i-lucide-circle-alert" color="error" variant="subtle" :description="premiumError" />
          <UButton type="submit" icon="i-lucide-save" :loading="premiumSaving" block>Record Payment</UButton>
        </form>
      </template>
    </USlideover>

    <USlideover v-model:open="maturedOpen" :title="`Mark Matured - ${maturedTarget?.insurer || ''}`" :ui="{ content: 'sm:max-w-md' }">
      <template #body>
        <form class="space-y-4" @submit.prevent="submitMatured">
          <UFormField label="Maturity Amount" name="maturityAmount">
            <UInput v-model.number="maturedForm.maturityAmount" type="number" step="0.01" icon="i-lucide-indian-rupee" class="w-full" />
          </UFormField>
          <UFormField label="Credited Date" name="maturityCreditedDate">
            <UInput v-model="maturedForm.maturityCreditedDate" type="date" class="w-full" />
          </UFormField>
          <UFormField label="Narration" name="narration">
            <UInput v-model="maturedForm.narration" class="w-full" />
          </UFormField>
          <UAlert v-if="maturedError" icon="i-lucide-circle-alert" color="error" variant="subtle" :description="maturedError" />
          <UButton type="submit" icon="i-lucide-check-check" :loading="maturedSaving" block>Mark Matured</UButton>
        </form>
      </template>
    </USlideover>
  </section>
</template>

<script setup lang="ts">
import {
  useSwalekhaApiClient,
  type SwalekhaAccount,
  type SwalekhaInsurancePolicy,
  type SwalekhaInsurancePolicyPayload,
  type SwalekhaMarkMaturedPayload,
  type SwalekhaPayPremiumPayload
} from '../utils/swalekha-api'

useHead({ title: 'Insurance - Swalekha' })

const api = useSwalekhaApiClient()
const loading = ref(false)
const error = ref('')
const policies = ref<SwalekhaInsurancePolicy[]>([])
const accounts = ref<SwalekhaAccount[]>([])

const policyColumns = [
  { accessorKey: 'insurer', header: 'Insurer' },
  { accessorKey: 'policyType', header: 'Type' },
  { accessorKey: 'premiumAmount', header: 'Premium' },
  { accessorKey: 'nextPremiumDueDate', header: 'Next Due' },
  { accessorKey: 'isActive', header: 'Status' },
  { accessorKey: 'actions', header: '', enableSorting: false }
]

const accountOptions = computed(() => [
  { label: 'Not linked', value: null },
  ...accounts.value.filter(a => a.isActive).map(a => ({ label: `${a.name} (${a.accountType})`, value: a.id }))
])

const totalSumAssured = computed(() => policies.value.filter(p => p.isActive && !p.isMatured).reduce((sum, p) => sum + (p.sumAssured || 0), 0))

function formatCurrency(value: number) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(value || 0)
}

function formatDate(value: string) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  return new Intl.DateTimeFormat('en-IN', { dateStyle: 'medium' }).format(date)
}

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const [policiesResult, accountsResult] = await Promise.all([
      api.get<SwalekhaInsurancePolicy[]>('insurance-policies?includeInactive=true'),
      api.get<SwalekhaAccount[]>('accounts')
    ])
    policies.value = policiesResult
    accounts.value = accountsResult
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not load policies.'
  } finally {
    loading.value = false
  }
}

onMounted(refresh)

const formOpen = ref(false)
const saving = ref(false)
const formError = ref('')
const editingId = ref<string | null>(null)
const form = reactive<SwalekhaInsurancePolicyPayload>(defaultForm())

function defaultForm(): SwalekhaInsurancePolicyPayload {
  return {
    policyType: 'Life', insurer: '', policyNumber: '', accountId: null,
    sumAssured: null, premiumAmount: 0, premiumFrequency: 'Yearly',
    startDate: new Date().toISOString().substring(0, 10),
    nomineeName: '', nomineeRelationship: '', maturityDate: null, maturityAmount: null,
    isActive: true, notes: ''
  }
}

function openCreate() {
  editingId.value = null
  formError.value = ''
  Object.assign(form, defaultForm())
  formOpen.value = true
}

function openEdit(policy: SwalekhaInsurancePolicy) {
  editingId.value = policy.id
  formError.value = ''
  Object.assign(form, {
    policyType: policy.policyType,
    insurer: policy.insurer,
    policyNumber: policy.policyNumber || '',
    accountId: policy.accountId || null,
    sumAssured: policy.sumAssured ?? null,
    premiumAmount: policy.premiumAmount,
    premiumFrequency: policy.premiumFrequency,
    startDate: policy.startDate.substring(0, 10),
    nomineeName: policy.nomineeName || '',
    nomineeRelationship: policy.nomineeRelationship || '',
    maturityDate: policy.maturityDate ? policy.maturityDate.substring(0, 10) : null,
    maturityAmount: policy.maturityAmount ?? null,
    isActive: policy.isActive,
    notes: policy.notes || ''
  })
  formOpen.value = true
}

async function submitPolicy() {
  saving.value = true
  formError.value = ''
  try {
    if (editingId.value) {
      await api.put(`insurance-policies/${editingId.value}`, form)
    } else {
      await api.post('insurance-policies', form)
    }
    formOpen.value = false
    await refresh()
  } catch (err) {
    formError.value = err instanceof Error ? err.message : 'Could not save the policy.'
  } finally {
    saving.value = false
  }
}

async function deletePolicy(policy: SwalekhaInsurancePolicy) {
  if (!confirm(`Delete the policy with "${policy.insurer}"?`)) return
  try {
    await api.del(`insurance-policies/${policy.id}`)
    await refresh()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not delete the policy.'
  }
}

const premiumOpen = ref(false)
const premiumSaving = ref(false)
const premiumError = ref('')
const premiumTarget = ref<SwalekhaInsurancePolicy | null>(null)
const premiumForm = reactive<SwalekhaPayPremiumPayload>({ paymentDate: new Date().toISOString().substring(0, 10), narration: '' })

function openPayPremium(policy: SwalekhaInsurancePolicy) {
  premiumTarget.value = policy
  premiumError.value = ''
  premiumForm.paymentDate = new Date().toISOString().substring(0, 10)
  premiumForm.narration = ''
  premiumOpen.value = true
}

async function submitPremium() {
  if (!premiumTarget.value) return
  premiumSaving.value = true
  premiumError.value = ''
  try {
    await api.post(`insurance-policies/${premiumTarget.value.id}/pay-premium`, premiumForm)
    premiumOpen.value = false
    await refresh()
  } catch (err) {
    premiumError.value = err instanceof Error ? err.message : 'Could not record the premium payment.'
  } finally {
    premiumSaving.value = false
  }
}

const maturedOpen = ref(false)
const maturedSaving = ref(false)
const maturedError = ref('')
const maturedTarget = ref<SwalekhaInsurancePolicy | null>(null)
const maturedForm = reactive<SwalekhaMarkMaturedPayload>({ maturityAmount: 0, maturityCreditedDate: new Date().toISOString().substring(0, 10), narration: '' })

function openMarkMatured(policy: SwalekhaInsurancePolicy) {
  maturedTarget.value = policy
  maturedError.value = ''
  maturedForm.maturityAmount = policy.maturityAmount || 0
  maturedForm.maturityCreditedDate = new Date().toISOString().substring(0, 10)
  maturedForm.narration = ''
  maturedOpen.value = true
}

async function submitMatured() {
  if (!maturedTarget.value) return
  maturedSaving.value = true
  maturedError.value = ''
  try {
    await api.post(`insurance-policies/${maturedTarget.value.id}/mark-matured`, maturedForm)
    maturedOpen.value = false
    await refresh()
  } catch (err) {
    maturedError.value = err instanceof Error ? err.message : 'Could not mark the policy matured.'
  } finally {
    maturedSaving.value = false
  }
}
</script>
