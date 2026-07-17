<template>
  <section class="space-y-5">
    <div class="flex flex-wrap items-start justify-between gap-3">
      <div>
        <p class="garmetix-kicker"><UIcon name="i-lucide-users" class="size-4" /> Swalekha</p>
        <h1 class="garmetix-dashboard-title">Contacts &amp; Person Ledger</h1>
      </div>
      <div class="flex flex-wrap items-center gap-2">
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
        <UButton icon="i-lucide-user-plus" @click="openCreate">New Contact</UButton>
      </div>
    </div>

    <UAlert v-if="error" icon="i-lucide-circle-alert" color="error" variant="subtle" title="Could not load contacts" :description="error" />

    <div class="swalekha-grid">
      <UCard :ui="{ body: 'p-4' }">
        <div class="flex items-center justify-between gap-3">
          <div class="min-w-0">
            <p class="text-xs font-medium uppercase text-muted">Owed to you</p>
            <p class="truncate text-lg font-semibold text-success">{{ formatCurrency(totalReceivable) }}</p>
          </div>
          <UIcon name="i-lucide-arrow-down-left" class="size-5 shrink-0 text-success" />
        </div>
      </UCard>
      <UCard :ui="{ body: 'p-4' }">
        <div class="flex items-center justify-between gap-3">
          <div class="min-w-0">
            <p class="text-xs font-medium uppercase text-muted">You owe</p>
            <p class="truncate text-lg font-semibold text-error">{{ formatCurrency(totalPayable) }}</p>
          </div>
          <UIcon name="i-lucide-arrow-up-right" class="size-5 shrink-0 text-error" />
        </div>
      </UCard>
      <UCard :ui="{ body: 'p-4' }">
        <div class="flex items-center justify-between gap-3">
          <div class="min-w-0">
            <p class="text-xs font-medium uppercase text-muted">Net</p>
            <p class="truncate text-lg font-semibold text-highlighted">{{ formatCurrency(totalReceivable - totalPayable) }}</p>
          </div>
          <UIcon name="i-lucide-scale" class="size-5 shrink-0 text-primary" />
        </div>
      </UCard>
    </div>

    <UCard :ui="{ body: 'p-0' }">
      <UTable :data="contacts" :columns="contactColumns" :loading="loading" class="w-full">
        <template #relationship-cell="{ row }">
          <span class="text-sm text-muted">{{ row.original.relationship || '-' }}</span>
        </template>
        <template #balance-cell="{ row }">
          <span class="font-mono text-sm" :class="row.original.balance > 0 ? 'text-success' : row.original.balance < 0 ? 'text-error' : ''">
            {{ formatCurrency(Math.abs(row.original.balance)) }}
            <span class="text-xs text-muted">{{ row.original.balance > 0 ? 'owes you' : row.original.balance < 0 ? 'you owe' : '' }}</span>
          </span>
        </template>
        <template #isActive-cell="{ row }">
          <UBadge :color="row.original.isActive ? 'success' : 'neutral'" variant="subtle">{{ row.original.isActive ? 'Active' : 'Inactive' }}</UBadge>
        </template>
        <template #actions-cell="{ row }">
          <div class="flex justify-end gap-1">
            <UButton icon="i-lucide-list" color="neutral" variant="ghost" size="sm" title="Ledger" @click="navigateTo(`/contacts/${row.original.id}`)" />
            <UButton icon="i-lucide-pencil" color="primary" variant="ghost" size="sm" title="Edit" @click="openEdit(row.original)" />
            <UButton icon="i-lucide-trash-2" color="error" variant="ghost" size="sm" title="Delete" @click="deleteContact(row.original)" />
          </div>
        </template>
      </UTable>
    </UCard>

    <USlideover v-model:open="formOpen" :title="editingId ? 'Edit Contact' : 'New Contact'" :ui="{ content: 'sm:max-w-lg' }">
      <template #body>
        <form class="space-y-4" @submit.prevent="submitContact">
          <UFormField label="Name" name="name">
            <UInput v-model="form.name" icon="i-lucide-user" required class="w-full" />
          </UFormField>

          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Phone" name="phone">
              <UInput v-model="form.phone" class="w-full" />
            </UFormField>
            <UFormField label="Email" name="email">
              <UInput v-model="form.email" class="w-full" />
            </UFormField>
          </div>

          <UFormField label="Relationship" name="relationship">
            <UInput v-model="form.relationship" placeholder="Friend, Family, Colleague..." class="w-full" />
          </UFormField>

          <USwitch v-model="form.isActive" label="Active" />

          <UFormField label="Notes" name="notes">
            <UTextarea v-model="form.notes" :rows="3" class="w-full" />
          </UFormField>

          <UAlert v-if="formError" icon="i-lucide-circle-alert" color="error" variant="subtle" :description="formError" />

          <UButton type="submit" icon="i-lucide-save" :loading="saving" block>{{ editingId ? 'Save Contact' : 'Create Contact' }}</UButton>
        </form>
      </template>
    </USlideover>
  </section>
</template>

<script setup lang="ts">
import { useSwalekhaApiClient, type SwalekhaContact, type SwalekhaContactPayload } from '../../utils/swalekha-api'

useHead({ title: 'Contacts - Swalekha' })

const api = useSwalekhaApiClient()
const loading = ref(false)
const error = ref('')
const contacts = ref<SwalekhaContact[]>([])

const contactColumns = [
  { accessorKey: 'name', header: 'Name' },
  { accessorKey: 'phone', header: 'Phone' },
  { accessorKey: 'relationship', header: 'Relationship' },
  { accessorKey: 'balance', header: 'Balance' },
  { accessorKey: 'isActive', header: 'Status' },
  { accessorKey: 'actions', header: '', enableSorting: false }
]

const totalReceivable = computed(() => contacts.value.filter(c => c.balance > 0).reduce((sum, c) => sum + c.balance, 0))
const totalPayable = computed(() => contacts.value.filter(c => c.balance < 0).reduce((sum, c) => sum + Math.abs(c.balance), 0))

function formatCurrency(value: number) {
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(value || 0)
}

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    contacts.value = await api.get<SwalekhaContact[]>('contacts?includeInactive=true')
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not load contacts.'
  } finally {
    loading.value = false
  }
}

onMounted(refresh)

const formOpen = ref(false)
const saving = ref(false)
const formError = ref('')
const editingId = ref<string | null>(null)
const form = reactive<SwalekhaContactPayload>(defaultForm())

function defaultForm(): SwalekhaContactPayload {
  return { name: '', phone: '', email: '', relationship: '', isActive: true, notes: '' }
}

function openCreate() {
  editingId.value = null
  formError.value = ''
  Object.assign(form, defaultForm())
  formOpen.value = true
}

function openEdit(contact: SwalekhaContact) {
  editingId.value = contact.id
  formError.value = ''
  Object.assign(form, {
    name: contact.name,
    phone: contact.phone || '',
    email: contact.email || '',
    relationship: contact.relationship || '',
    isActive: contact.isActive,
    notes: contact.notes || ''
  })
  formOpen.value = true
}

async function submitContact() {
  saving.value = true
  formError.value = ''
  try {
    if (editingId.value) {
      await api.put(`contacts/${editingId.value}`, form)
    } else {
      await api.post('contacts', form)
    }
    formOpen.value = false
    await refresh()
  } catch (err) {
    formError.value = err instanceof Error ? err.message : 'Could not save the contact.'
  } finally {
    saving.value = false
  }
}

async function deleteContact(contact: SwalekhaContact) {
  if (!confirm(`Delete contact "${contact.name}"? Its ledger history is kept for the record.`)) return
  try {
    await api.del(`contacts/${contact.id}`)
    await refresh()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Could not delete the contact.'
  }
}
</script>
