<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 xl:flex-row xl:items-start xl:justify-between">
        <div>
          <p class="garmetix-dashboard-kicker">
            <UIcon name="i-lucide-shield-off" class="size-4" />
            Communication & Mail
          </p>
          <h2 class="garmetix-dashboard-title">Suppression List</h2>
          <p class="garmetix-dashboard-subtitle">
            Addresses here are never sent to. Hard bounce, invalid address and complaint events from the Brevo webhook add
            entries automatically; removal always requires a reason for audit.
          </p>
        </div>
        <div class="flex flex-col gap-2 sm:flex-row">
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
          <UButton icon="i-lucide-plus" color="primary" @click="openAddModal">Add Manually</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />
    <UAlert v-if="message" color="success" variant="subtle" icon="i-lucide-circle-check" :description="message" />

    <div class="flex flex-wrap items-center gap-2">
      <UInput v-model="search" placeholder="Search email..." icon="i-lucide-search" @keyup.enter="refresh" />
      <label class="flex items-center gap-1 text-sm text-muted">
        <UCheckbox v-model="activeOnly" @update:model-value="refresh" /> Active only
      </label>
    </div>

    <section class="garmetix-section-card">
      <CommunicationMasterTable :columns="columns" :rows="rows" empty-text="No suppressed addresses.">
        <template #actions="{ row }">
          <UButton v-if="row.isActiveRaw" size="xs" color="error" variant="soft" icon="i-lucide-shield-check" @click="openRemoveModal(row)">
            Remove
          </UButton>
        </template>
      </CommunicationMasterTable>
    </section>

    <UModal v-model:open="addModalOpen" title="Add Suppression Entry">
      <template #body>
        <form class="space-y-3" @submit.prevent="submitAdd">
          <label class="space-y-1 text-sm">
            <span class="text-muted">Email Address</span>
            <UInput v-model="addForm.emailAddress" type="email" required />
          </label>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Company Id (optional - leave blank for a global suppression)</span>
            <UInput v-model="addForm.companyId" />
          </label>
          <div class="flex justify-end gap-2">
            <UButton type="button" color="neutral" variant="soft" @click="addModalOpen = false">Cancel</UButton>
            <UButton type="submit" color="primary" icon="i-lucide-save" :loading="saving">Add</UButton>
          </div>
        </form>
      </template>
    </UModal>

    <UModal v-model:open="removeModalOpen" title="Remove Suppression Entry" description="A reason is required.">
      <template #body>
        <div class="space-y-3">
          <p class="text-sm">Remove <strong>{{ entryPendingRemoval?.emailAddress }}</strong> from the suppression list?</p>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Reason</span>
            <UInput v-model="removeReason" placeholder="e.g. Customer confirmed address is valid again" />
          </label>
          <div class="flex justify-end gap-2">
            <UButton color="neutral" variant="soft" @click="removeModalOpen = false">Cancel</UButton>
            <UButton color="error" icon="i-lucide-shield-check" :loading="removing" @click="performRemove">Remove</UButton>
          </div>
        </div>
      </template>
    </UModal>
  </section>
</template>

<script setup lang="ts">
import CommunicationMasterTable from '../components/CommunicationMasterTable.vue'
import { formatDateTime, useCommunicationApiClient } from '../utils/communication-api'

useHead({ title: 'Suppression - Garmetix Communication & Mail' })

interface SuppressionEntry {
  id: string
  companyId: string | null
  emailAddress: string
  reason: string
  isActive: boolean
  createdAt: string
  removedAtUtc: string | null
  removalReason: string | null
}

const { get, post } = useCommunicationApiClient()

const loading = ref(true)
const error = ref('')
const message = ref('')
const search = ref('')
const activeOnly = ref(true)
const entries = ref<SuppressionEntry[]>([])

const columns = [
  { key: 'emailAddress', label: 'Email' },
  { key: 'reason', label: 'Reason' },
  { key: 'scope', label: 'Scope' },
  { key: 'status', label: 'Status' },
  { key: 'createdAt', label: 'Added' }
]
const rows = computed(() => entries.value.map(e => ({
  id: e.id,
  emailAddress: e.emailAddress,
  reason: e.reason,
  scope: e.companyId ? 'Company-scoped' : 'Global',
  status: e.isActive ? 'Active' : 'Removed',
  isActiveRaw: e.isActive,
  createdAt: formatDateTime(e.createdAt)
})))

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    entries.value = await get<SuppressionEntry[]>('/communication/suppression', {
      search: search.value || undefined,
      activeOnly: activeOnly.value
    }) ?? []
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to load the suppression list.'
  } finally {
    loading.value = false
  }
}

const addModalOpen = ref(false)
const saving = ref(false)
const addForm = reactive({ emailAddress: '', companyId: '' })

function openAddModal() {
  addForm.emailAddress = ''
  addForm.companyId = ''
  addModalOpen.value = true
}

async function submitAdd() {
  saving.value = true
  error.value = ''
  try {
    await post('/communication/suppression/manual', {
      emailAddress: addForm.emailAddress,
      companyId: addForm.companyId || null
    })
    addModalOpen.value = false
    message.value = 'Suppression entry added.'
    await refresh()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to add the suppression entry.'
  } finally {
    saving.value = false
  }
}

const removeModalOpen = ref(false)
const removing = ref(false)
const removeReason = ref('')
const entryPendingRemoval = ref<{ id: string, emailAddress: string } | null>(null)

function openRemoveModal(row: { id: string, emailAddress: string }) {
  entryPendingRemoval.value = row
  removeReason.value = ''
  removeModalOpen.value = true
}

async function performRemove() {
  if (!entryPendingRemoval.value) return
  if (!removeReason.value.trim()) {
    error.value = 'A reason is required to remove a suppression entry.'
    return
  }
  removing.value = true
  try {
    await post(`/communication/suppression/${entryPendingRemoval.value.id}/remove`, { reason: removeReason.value.trim() })
    removeModalOpen.value = false
    message.value = 'Suppression entry removed.'
    await refresh()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to remove the suppression entry.'
  } finally {
    removing.value = false
  }
}

onMounted(refresh)
</script>
