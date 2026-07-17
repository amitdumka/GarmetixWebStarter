<template>
  <section class="space-y-5">
    <div class="flex flex-wrap items-start justify-between gap-3">
      <div>
        <p class="garmetix-kicker"><UIcon name="i-lucide-heart-handshake" class="size-4" /> Swalekha</p>
        <h1 class="garmetix-dashboard-title">Family Members</h1>
      </div>
      <div class="flex flex-wrap items-center gap-2">
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
        <UButton icon="i-lucide-user-plus" @click="openCreate">Add Family Member</UButton>
      </div>
    </div>

    <UAlert v-if="error" icon="i-lucide-circle-alert" color="error" variant="subtle" title="Could not load family members" :description="error" />

    <UAlert
      icon="i-lucide-info"
      color="info"
      variant="subtle"
      title="How linked transfers work"
      description="Link a family member to their own Owner login, and ask them to add you back the same way. Once the link is confirmed both ways, a single transfer entry on your side updates their account automatically."
    />

    <UCard :ui="{ body: 'p-0' }">
      <UTable :data="members" :columns="memberColumns" :loading="loading" class="w-full">
        <template #relationship-cell="{ row }">
          <span class="text-sm text-muted">{{ row.original.relationship || '-' }}</span>
        </template>
        <template #linkedOwnerName-cell="{ row }">
          <div v-if="row.original.linkedOwnerName" class="flex items-center gap-1.5">
            <span class="text-sm">{{ row.original.linkedOwnerName }}</span>
            <UBadge v-if="row.original.linkConfirmed" color="success" variant="subtle" size="xs">Linked</UBadge>
            <UBadge v-else color="warning" variant="subtle" size="xs">Not confirmed yet</UBadge>
          </div>
          <span v-else class="text-sm text-muted">Not linked</span>
        </template>
        <template #isActive-cell="{ row }">
          <UBadge :color="row.original.isActive ? 'success' : 'neutral'" variant="subtle">{{ row.original.isActive ? 'Active' : 'Inactive' }}</UBadge>
        </template>
        <template #actions-cell="{ row }">
          <div class="flex justify-end gap-1">
            <UButton
              icon="i-lucide-send"
              color="primary"
              variant="ghost"
              size="sm"
              title="Send Money"
              :disabled="!row.original.linkConfirmed"
              @click="openTransfer(row.original)"
            />
            <UButton icon="i-lucide-pencil" color="neutral" variant="ghost" size="sm" title="Edit" @click="openEdit(row.original)" />
            <UButton icon="i-lucide-trash-2" color="error" variant="ghost" size="sm" title="Delete" @click="deleteMember(row.original)" />
          </div>
        </template>
      </UTable>
    </UCard>

    <USlideover v-model:open="formOpen" :title="editingId ? 'Edit Family Member' : 'Add Family Member'" :ui="{ content: 'sm:max-w-lg' }">
      <template #body>
        <form class="space-y-4" @submit.prevent="submitMember">
          <UFormField label="Name" name="name">
            <UInput v-model="form.name" icon="i-lucide-user" required class="w-full" />
          </UFormField>

          <UFormField label="Relationship" name="relationship">
            <UInput v-model="form.relationship" placeholder="Spouse, Son, Daughter, Father, Mother..." class="w-full" />
          </UFormField>

          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Mobile" name="mobile">
              <UInput v-model="form.mobile" class="w-full" />
            </UFormField>
            <UFormField label="Email" name="email">
              <UInput v-model="form.email" class="w-full" />
            </UFormField>
          </div>

          <UFormField label="Date Of Birth" name="dateOfBirth">
            <UInput v-model="form.dateOfBirth" type="date" class="w-full" />
          </UFormField>

          <UFormField
            label="Linked Owner Login"
            name="linkedOwnerId"
            description="Only Owner-type logins on this platform can be linked, e.g. another owner of the same company or family business"
          >
            <USelectMenu
              v-model="form.linkedOwnerId"
              :items="linkableOwnerOptions"
              value-key="value"
              placeholder="Not linked"
              class="w-full"
            />
          </UFormField>

          <USwitch v-model="form.isActive" label="Active" />

          <UFormField label="Notes" name="notes">
            <UTextarea v-model="form.notes" :rows="3" class="w-full" />
          </UFormField>

          <UAlert v-if="formError" icon="i-lucide-circle-alert" color="error" variant="subtle" :description="formError" />

          <UButton type="submit" icon="i-lucide-save" :loading="saving" block>{{ editingId ? 'Save' : 'Add Family Member' }}</UButton>
        </form>
      </template>
    </USlideover>

    <USlideover v-model:open="transferOpen" :title="`Send Money to ${transferTarget?.name || ''}`" :ui="{ content: 'sm:max-w-lg' }">
      <template #body>
        <form class="space-y-4" @submit.prevent="submitTransfer">
          <UFormField label="From Account" name="fromAccountId">
            <USelectMenu v-model="transferForm.fromAccountId" :items="accountSelectItems" value-key="value" class="w-full" />
          </UFormField>
          <UFormField label="Amount" name="amount">
            <UInput v-model.number="transferForm.amount" type="number" step="0.01" icon="i-lucide-indian-rupee" class="w-full" />
          </UFormField>
          <UFormField label="Date" name="transactionDate">
            <UInput v-model="transferForm.transactionDate" type="date" class="w-full" />
          </UFormField>
          <UFormField label="Narration" name="narration">
            <UInput v-model="transferForm.narration" class="w-full" />
          </UFormField>

          <UAlert v-if="transferError" icon="i-lucide-circle-alert" color="error" variant="subtle" :description="transferError" />
          <UAlert v-if="transferSuccess" icon="i-lucide-check-circle-2" color="success" variant="subtle" :description="transferSuccess" />

          <UButton type="submit" icon="i-lucide-send" :loading="transferring" block>Send</UButton>
        </form>
      </template>
    </USlideover>
  </section>
</template>

<script setup lang="ts">
import {
  useSwalekhaApiClient,
  type SwalekhaAccount,
  type SwalekhaFamilyMember,
  type SwalekhaFamilyMemberPayload,
  type SwalekhaFamilyTransferPayload,
  type SwalekhaFamilyTransferResult,
  type SwalekhaLinkableOwner
} from '../../utils/swalekha-api'

useHead({ title: 'Family Members - Swalekha' })

const api = useSwalekhaApiClient()
const loading = ref(false)
const error = ref('')
const members = ref<SwalekhaFamilyMember[]>([])
const linkableOwners = ref<SwalekhaLinkableOwner[]>([])
const accounts = ref<SwalekhaAccount[]>([])

const memberColumns = [
  { accessorKey: 'name', header: 'Name' },
  { accessorKey: 'relationship', header: 'Relationship' },
  { accessorKey: 'mobile', header: 'Mobile' },
  { accessorKey: 'linkedOwnerName', header: 'Linked Login' },
  { accessorKey: 'isActive', header: 'Status' },
  { accessorKey: 'actions', header: '', enableSorting: false }
]

const linkableOwnerOptions = computed(() => [
  { label: 'Not linked', value: null },
  ...linkableOwners.value.map(o => ({ label: o.name, value: o.id }))
])

const accountSelectItems = computed(() => accounts.value
  .filter(a => a.isActive)
  .map(a => ({ label: `${a.name} (${formatCurrency(a.currentBalance)})`, value: a.id })))

function formatCurrency(value: number) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(value || 0)
}

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const [membersResult, ownersResult, accountsResult] = await Promise.all([
      api.get<SwalekhaFamilyMember[]>('family?includeInactive=true'),
      api.get<SwalekhaLinkableOwner[]>('family/linkable-owners'),
      api.get<SwalekhaAccount[]>('accounts')
    ])
    members.value = membersResult
    linkableOwners.value = ownersResult
    accounts.value = accountsResult
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not load family members.'
  } finally {
    loading.value = false
  }
}

onMounted(refresh)

const formOpen = ref(false)
const saving = ref(false)
const formError = ref('')
const editingId = ref<string | null>(null)
const form = reactive<SwalekhaFamilyMemberPayload>(defaultForm())

function defaultForm(): SwalekhaFamilyMemberPayload {
  return { name: '', relationship: '', mobile: '', email: '', dateOfBirth: null, linkedOwnerId: null, isActive: true, notes: '' }
}

function openCreate() {
  editingId.value = null
  formError.value = ''
  Object.assign(form, defaultForm())
  formOpen.value = true
}

function openEdit(member: SwalekhaFamilyMember) {
  editingId.value = member.id
  formError.value = ''
  Object.assign(form, {
    name: member.name,
    relationship: member.relationship || '',
    mobile: member.mobile || '',
    email: member.email || '',
    dateOfBirth: member.dateOfBirth ? member.dateOfBirth.substring(0, 10) : null,
    linkedOwnerId: member.linkedOwnerId || null,
    isActive: member.isActive,
    notes: member.notes || ''
  })
  formOpen.value = true
}

async function submitMember() {
  saving.value = true
  formError.value = ''
  try {
    if (editingId.value) {
      await api.put(`family/${editingId.value}`, form)
    } else {
      await api.post('family', form)
    }
    formOpen.value = false
    await refresh()
  } catch (err) {
    formError.value = err instanceof Error ? err.message : 'Could not save the family member.'
  } finally {
    saving.value = false
  }
}

async function deleteMember(member: SwalekhaFamilyMember) {
  if (!confirm(`Remove family member "${member.name}"?`)) return
  try {
    await api.del(`family/${member.id}`)
    await refresh()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not remove the family member.'
  }
}

const transferOpen = ref(false)
const transferring = ref(false)
const transferError = ref('')
const transferSuccess = ref('')
const transferTarget = ref<SwalekhaFamilyMember | null>(null)
const transferForm = reactive<SwalekhaFamilyTransferPayload>(defaultTransferForm())

function defaultTransferForm(): SwalekhaFamilyTransferPayload {
  return { fromAccountId: '', amount: 0, transactionDate: new Date().toISOString().substring(0, 10), narration: '' }
}

function openTransfer(member: SwalekhaFamilyMember) {
  transferTarget.value = member
  transferError.value = ''
  transferSuccess.value = ''
  Object.assign(transferForm, defaultTransferForm())
  transferOpen.value = true
}

async function submitTransfer() {
  if (!transferTarget.value) return
  transferring.value = true
  transferError.value = ''
  transferSuccess.value = ''
  try {
    const result = await api.post<SwalekhaFamilyTransferResult>(`family/${transferTarget.value.id}/transfer`, transferForm)
    transferSuccess.value = result.message
    await refresh()
  } catch (err) {
    transferError.value = err instanceof Error ? err.message : 'Could not send the transfer.'
  } finally {
    transferring.value = false
  }
}
</script>
