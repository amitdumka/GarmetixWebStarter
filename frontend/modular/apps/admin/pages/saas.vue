<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-dashboard-kicker">
            <UIcon name="i-lucide-network" class="size-4" />
            SaaS operator console
          </p>
          <h2 class="garmetix-dashboard-title">SaaS Manager</h2>
          <p class="garmetix-dashboard-subtitle">Manage SaaS clients, license plans, issue offline activation tokens and review tenant subscriptions. SuperAdmin only.</p>
        </div>
        <div class="flex items-center gap-2">
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
          <UButton v-if="activeTab === 'clients'" icon="i-lucide-plus" color="primary" @click="openClientCreate">Add Client</UButton>
          <UButton v-if="activeTab === 'plans'" icon="i-lucide-plus" color="primary" @click="openPlanCreate">Add Plan</UButton>
          <UButton v-if="activeTab === 'tokens'" icon="i-lucide-key-round" color="primary" @click="openTokenGenerate">Generate Token</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />

    <section class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
      <div v-for="card in cards" :key="card.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label">{{ card.label }}</p>
        <p class="garmetix-metric-value">{{ card.value }}</p>
        <p class="garmetix-metric-caption">{{ card.detail }}</p>
      </div>
    </section>

    <UTabs v-model="activeTab" :items="tabs" class="w-full max-w-lg" />

    <section class="garmetix-section-card overflow-hidden p-0">
      <!-- Clients -->
      <UTable v-if="activeTab === 'clients'" :data="clients" :columns="clientColumns" :loading="loading" class="w-full">
        <template #active-cell="{ row }">
          <UBadge variant="subtle" :color="row.original.active ? 'success' : 'neutral'">{{ row.original.active ? 'Active' : 'Inactive' }}</UBadge>
        </template>
        <template #actions-cell="{ row }">
          <div class="flex items-center justify-end gap-1">
            <UButton icon="i-lucide-building-2" color="neutral" variant="ghost" size="sm" label="Companies" @click="openCompanyLinks(row.original)" />
            <UButton icon="i-lucide-pencil" color="primary" variant="ghost" size="sm" @click="openClientEdit(row.original)" />
            <UButton icon="i-lucide-trash-2" color="error" variant="ghost" size="sm" @click="askDelete('client', row.original)" />
          </div>
        </template>
      </UTable>

      <!-- Plans -->
      <UTable v-else-if="activeTab === 'plans'" :data="plans" :columns="planColumns" :loading="loading" class="w-full">
        <template #active-cell="{ row }">
          <UBadge variant="subtle" :color="row.original.active ? 'success' : 'neutral'">{{ row.original.active ? 'Active' : 'Inactive' }}</UBadge>
        </template>
        <template #actions-cell="{ row }">
          <div class="flex items-center justify-end gap-1">
            <UButton icon="i-lucide-pencil" color="primary" variant="ghost" size="sm" @click="openPlanEdit(row.original)" />
            <UButton icon="i-lucide-trash-2" color="error" variant="ghost" size="sm" @click="askDelete('plan', row.original)" />
          </div>
        </template>
      </UTable>

      <!-- Tokens -->
      <UTable v-else-if="activeTab === 'tokens'" :data="tokenRows" :columns="tokenColumns" :loading="loading" class="w-full">
        <template #status-cell="{ row }">
          <UBadge variant="subtle" :color="row.original.isActivated ? 'success' : 'warning'">{{ row.original.isActivated ? 'Activated' : 'Pending' }}</UBadge>
        </template>
        <template #expiresAt-cell="{ row }">
          <span :class="isExpired(row.original.expiresAtRaw) ? 'text-error' : ''">{{ formatDateTime(row.original.expiresAtRaw) }}</span>
        </template>
        <template #actions-cell="{ row }">
          <div class="flex items-center justify-end gap-1">
            <UButton icon="i-lucide-copy" color="neutral" variant="ghost" size="sm" @click="copyToken(row.original.tokenString)" />
            <UButton v-if="!row.original.isActivated" icon="i-lucide-trash-2" color="error" variant="ghost" size="sm" @click="askDelete('token', row.original)" />
          </div>
        </template>
      </UTable>

      <!-- Subscriptions -->
      <UTable v-else :data="subscriptionRows" :columns="subscriptionColumns" :loading="loading" class="w-full">
        <template #status-cell="{ row }">
          <UBadge variant="subtle" :color="row.original.isActive ? 'success' : 'error'">{{ row.original.isActive ? 'Active' : 'Expired' }}</UBadge>
        </template>
        <template #stores-cell="{ row }">{{ row.original.currentStores }} / {{ row.original.maxStores }}</template>
        <template #storeGroups-cell="{ row }">{{ row.original.currentStoreGroups }} / {{ row.original.maxStoreGroups }}</template>
        <template #users-cell="{ row }">{{ row.original.currentUsers }} / {{ row.original.maxUsers }}</template>
        <template #validTo-cell="{ row }">
          <span :class="isExpired(row.original.validToRaw) ? 'text-error' : ''">{{ formatDateTime(row.original.validToRaw) }}</span>
        </template>
      </UTable>
    </section>

    <!-- Slideover: Add/Edit Client -->
    <USlideover v-model:open="clientModalOpen" :title="editingId ? 'Edit Client' : 'Add Client'" :description="editingId ? 'Update SaaS client details' : 'Register a new SaaS client'">
      <template #body>
        <UForm :state="clientForm" class="space-y-4" @submit="saveClient">
          <div class="grid grid-cols-2 gap-4">
            <UFormField label="Client Code" name="clientCode" required>
              <UInput v-model="clientForm.clientCode" placeholder="CLI001" class="w-full" :disabled="!!editingId" />
            </UFormField>
            <UFormField label="Client Name" name="name" required>
              <UInput v-model="clientForm.name" placeholder="Full organization name" class="w-full" />
            </UFormField>
          </div>
          <div class="grid grid-cols-2 gap-4">
            <UFormField label="Email" name="email">
              <UInput v-model="clientForm.email" type="email" placeholder="contact@example.com" class="w-full" />
            </UFormField>
            <UFormField label="Mobile" name="mobile">
              <UInput v-model="clientForm.mobile" placeholder="+91 9876543210" class="w-full" />
            </UFormField>
          </div>
          <div class="grid grid-cols-2 gap-4">
            <UFormField label="City" name="city">
              <UInput v-model="clientForm.city" placeholder="Mumbai" class="w-full" />
            </UFormField>
            <UFormField label="State" name="state">
              <UInput v-model="clientForm.state" placeholder="Maharashtra" class="w-full" />
            </UFormField>
          </div>
          <UFormField label="GSTIN" name="gstin">
            <UInput v-model="clientForm.gstin" placeholder="27AAPFU0939F1ZV" class="w-full" />
          </UFormField>
          <UFormField label="Address" name="address">
            <UTextarea v-model="clientForm.address" placeholder="Full address..." class="w-full" />
          </UFormField>
          <UCheckbox v-model="clientForm.active" label="Client is Active" />
          <div class="mt-6 flex justify-end gap-3">
            <UButton color="neutral" variant="ghost" label="Cancel" @click="clientModalOpen = false" />
            <UButton type="submit" color="primary" :loading="saving" :label="editingId ? 'Update' : 'Save Client'" />
          </div>
        </UForm>
      </template>
    </USlideover>

    <!-- Slideover: Add/Edit Plan -->
    <USlideover v-model:open="planModalOpen" :title="editingId ? 'Edit Plan' : 'Add Plan'" :description="editingId ? 'Update license plan quotas' : 'Create a new license plan'">
      <template #body>
        <UForm :state="planForm" class="space-y-4" @submit="savePlan">
          <UFormField label="Plan Name" name="planName" required>
            <UInput v-model="planForm.planName" placeholder="e.g. Premium Plan" class="w-full" />
          </UFormField>
          <div class="grid grid-cols-2 gap-4">
            <UFormField label="Max Companies" name="maxCompanies">
              <UInput v-model.number="planForm.maxCompanies" type="number" min="1" class="w-full" />
            </UFormField>
            <UFormField label="Max Store Groups" name="maxStoreGroups">
              <UInput v-model.number="planForm.maxStoreGroups" type="number" min="1" class="w-full" />
            </UFormField>
          </div>
          <div class="grid grid-cols-2 gap-4">
            <UFormField label="Max Stores" name="maxStores">
              <UInput v-model.number="planForm.maxStores" type="number" min="1" class="w-full" />
            </UFormField>
            <UFormField label="Max Users" name="maxUsers">
              <UInput v-model.number="planForm.maxUsers" type="number" min="1" class="w-full" />
            </UFormField>
          </div>
          <UFormField label="Included Modules" name="includedModulesCsv" help="Comma-separated keys, e.g. main,pos,inventory,hr,books">
            <UInput v-model="planForm.includedModulesCsv" placeholder="main,pos,inventory,hr,books" class="w-full" />
          </UFormField>
          <UCheckbox v-model="planForm.active" label="Plan is Active" />
          <div class="mt-6 flex justify-end gap-3">
            <UButton color="neutral" variant="ghost" label="Cancel" @click="planModalOpen = false" />
            <UButton type="submit" color="primary" :loading="saving" :label="editingId ? 'Update' : 'Save Plan'" />
          </div>
        </UForm>
      </template>
    </USlideover>

    <!-- Modal: Generate Token -->
    <UModal v-model:open="tokenModalOpen" title="Generate License Token" description="Creates an offline activation token to send to the client via WhatsApp/email">
      <template #body>
        <UForm :state="tokenForm" class="space-y-4" @submit="generateToken">
          <UFormField label="Client" name="saaSClientId" required>
            <USelectMenu v-model="tokenForm.saaSClientId" :items="clientOptions" value-key="value" placeholder="Select client..." class="w-full" />
          </UFormField>
          <UFormField label="Plan" name="saaSPlanId" required>
            <USelectMenu v-model="tokenForm.saaSPlanId" :items="planOptions" value-key="value" placeholder="Select plan..." class="w-full" />
          </UFormField>
          <UFormField label="Validity (Days)" name="validityDays">
            <UInput v-model.number="tokenForm.validityDays" type="number" min="1" class="w-full" />
          </UFormField>
          <UFormField label="Notes" name="notes">
            <UInput v-model="tokenForm.notes" class="w-full" />
          </UFormField>
          <div v-if="generatedTokenString" class="rounded-md border border-success/40 bg-success/10 p-3">
            <p class="text-xs text-muted">Generated token - copy and send to the client:</p>
            <p class="mt-1 break-all font-mono text-sm text-highlighted">{{ generatedTokenString }}</p>
            <UButton size="xs" color="success" variant="soft" icon="i-lucide-copy" class="mt-2" @click="copyToken(generatedTokenString)">Copy</UButton>
          </div>
          <div class="mt-6 flex justify-end gap-3">
            <UButton color="neutral" variant="ghost" label="Close" @click="tokenModalOpen = false" />
            <UButton type="submit" color="primary" :loading="saving" label="Generate Token" />
          </div>
        </UForm>
      </template>
    </UModal>

    <!-- Modal: Confirm Delete -->
    <UModal v-model:open="deleteModalOpen" title="Confirm Delete" :description="`Are you sure you want to delete '${deleteTargetLabel}'? This action cannot be undone.`">
      <template #body>
        <div class="flex justify-end gap-3">
          <UButton color="neutral" variant="ghost" label="Cancel" @click="deleteModalOpen = false" />
          <UButton color="error" :loading="saving" label="Delete" @click="executeDelete" />
        </div>
      </template>
    </UModal>

    <!-- Modal: Manage Companies (link/unlink) -->
    <UModal v-model:open="companyLinksModalOpen" title="Manage Companies" :description="companyLinksClient ? `Companies linked to ${companyLinksClient.name}. Linking gates them under this client's plan/trial quotas and expiry enforcement.` : ''">
      <template #body>
        <div v-if="loadingCompanyLinks" class="space-y-2">
          <USkeleton class="h-4 w-2/3" />
          <USkeleton class="h-4 w-1/2" />
        </div>
        <div v-else class="max-h-96 space-y-2 overflow-y-auto">
          <div v-for="company in companyLinks" :key="company.companyId" class="flex items-center justify-between gap-3 rounded-md border border-default p-3">
            <div class="min-w-0">
              <p class="truncate text-sm font-medium text-highlighted">{{ company.companyName }}</p>
              <p class="text-xs text-muted">{{ company.companyCode }}</p>
            </div>
            <UBadge v-if="company.linkedToAnotherClient" variant="subtle" color="neutral" size="sm">Linked elsewhere</UBadge>
            <UButton
              v-else
              size="sm"
              :color="company.linked ? 'error' : 'primary'"
              :variant="company.linked ? 'soft' : 'solid'"
              :loading="linkingCompanyId === company.companyId"
              @click="toggleCompanyLink(company)"
            >
              {{ company.linked ? 'Unlink' : 'Link' }}
            </UButton>
          </div>
          <p v-if="!companyLinks.length" class="py-6 text-center text-sm text-muted">No companies found.</p>
        </div>
        <div class="mt-4 flex justify-end">
          <UButton color="neutral" variant="ghost" label="Close" @click="companyLinksModalOpen = false" />
        </div>
      </template>
    </UModal>
  </section>
</template>

<script setup lang="ts">
import { reactive, ref, computed, onMounted } from 'vue'
import { formatDateTime, readText, toRows, type ApiRecord, useAdminApiClient } from '../utils/admin-api'

useHead({ title: 'SaaS Manager - Garmetix Admin' })

const toast = useToast()
const { get, post, put, remove } = useAdminApiClient()

const loading = ref(true)
const saving = ref(false)
const error = ref('')

const clients = ref<ApiRecord[]>([])
const plans = ref<ApiRecord[]>([])
const tokens = ref<ApiRecord[]>([])
const subscriptions = ref<ApiRecord[]>([])

const activeTab = ref('clients')
const tabs = [
  { label: 'Clients', icon: 'i-lucide-users', value: 'clients' },
  { label: 'License Plans', icon: 'i-lucide-layers', value: 'plans' },
  { label: 'License Tokens', icon: 'i-lucide-key', value: 'tokens' },
  { label: 'Tenant Subscriptions', icon: 'i-lucide-activity', value: 'subscriptions' }
]

const clientModalOpen = ref(false)
const planModalOpen = ref(false)
const tokenModalOpen = ref(false)
const deleteModalOpen = ref(false)

const editingId = ref<string | null>(null)
const deleteTarget = ref<ApiRecord | null>(null)
const deleteType = ref<'client' | 'plan' | 'token'>('client')
const generatedTokenString = ref('')

const clientForm = reactive({
  clientCode: '', name: '', email: '', mobile: '', address: '', city: '', state: '', gstin: '', active: true
})

const planForm = reactive({
  planName: '', maxCompanies: 1, maxStoreGroups: 1, maxStores: 2, maxUsers: 20, includedModulesCsv: '', active: true
})

const tokenForm = reactive({
  saaSClientId: null as string | { value: string } | null,
  saaSPlanId: null as string | { value: string } | null,
  validityDays: 365,
  notes: ''
})

function unwrapId(value: unknown) {
  return (value as { value?: string })?.value ?? (value as string | null)
}

const clientOptions = computed(() => clients.value.map(c => ({ value: String(c.id), label: `${readText(c, ['clientCode'])} - ${readText(c, ['name'])}` })))
const planOptions = computed(() => plans.value.map(p => ({ value: String(p.id), label: readText(p, ['planName']) })))

const clientColumns = [
  { accessorKey: 'clientCode', header: 'Code' },
  { accessorKey: 'name', header: 'Name' },
  { accessorKey: 'email', header: 'Email' },
  { accessorKey: 'mobile', header: 'Mobile' },
  { accessorKey: 'city', header: 'City' },
  { accessorKey: 'active', header: 'Status' },
  { accessorKey: 'actions', header: '', enableSorting: false }
]

const planColumns = [
  { accessorKey: 'planName', header: 'Plan Name' },
  { accessorKey: 'maxCompanies', header: 'Companies' },
  { accessorKey: 'maxStoreGroups', header: 'Store Groups' },
  { accessorKey: 'maxStores', header: 'Stores' },
  { accessorKey: 'maxUsers', header: 'Users' },
  { accessorKey: 'includedModulesCsv', header: 'Modules' },
  { accessorKey: 'active', header: 'Status' },
  { accessorKey: 'actions', header: '', enableSorting: false }
]

const tokenColumns = [
  { accessorKey: 'tokenString', header: 'Token' },
  { accessorKey: 'clientName', header: 'Client' },
  { accessorKey: 'planName', header: 'Plan' },
  { accessorKey: 'status', header: 'Status' },
  { accessorKey: 'activatedCompanyName', header: 'Activated Company' },
  { accessorKey: 'expiresAt', header: 'Expires' },
  { accessorKey: 'actions', header: '', enableSorting: false }
]

const subscriptionColumns = [
  { accessorKey: 'companyName', header: 'Company' },
  { accessorKey: 'clientName', header: 'Client' },
  { accessorKey: 'planName', header: 'Plan' },
  { accessorKey: 'storeGroups', header: 'Store Groups' },
  { accessorKey: 'stores', header: 'Stores' },
  { accessorKey: 'users', header: 'Users' },
  { accessorKey: 'validTo', header: 'Valid To' },
  { accessorKey: 'status', header: 'Status' }
]

const tokenRows = computed(() => tokens.value.map(t => ({
  id: t.id,
  tokenString: readText(t, ['tokenString']),
  clientName: readText(t, ['clientName']),
  planName: readText(t, ['planName']),
  isActivated: Boolean(t.isActivated),
  activatedCompanyName: readText(t, ['activatedCompanyName'], '-'),
  expiresAtRaw: t.expiresAt
})))

const subscriptionRows = computed(() => subscriptions.value.map(s => ({
  companyName: readText(s, ['companyName']),
  clientName: readText(s, ['clientName']),
  planName: readText(s, ['planName']),
  currentStoreGroups: s.currentStoreGroups ?? 0,
  maxStoreGroups: s.maxStoreGroups ?? 0,
  currentStores: s.currentStores ?? 0,
  maxStores: s.maxStores ?? 0,
  currentUsers: s.currentUsers ?? 0,
  maxUsers: s.maxUsers ?? 0,
  validToRaw: s.validTo,
  isActive: Boolean(s.isActive)
})))

const cards = computed(() => [
  { label: 'Clients', value: clients.value.length, detail: 'Registered SaaS clients' },
  { label: 'Plans', value: plans.value.length, detail: 'License plans defined' },
  { label: 'Tokens Pending', value: tokens.value.filter(t => !t.isActivated).length, detail: 'Not yet activated' },
  { label: 'Active Subscriptions', value: subscriptions.value.filter(s => s.isActive).length, detail: 'Companies currently subscribed' }
])

const deleteTargetLabel = computed(() => readText(deleteTarget.value, ['name', 'planName', 'tokenString'], ''))

function isExpired(value: unknown) {
  if (!value) return false
  return new Date(String(value)) < new Date()
}

async function copyToken(value: string) {
  try {
    await navigator.clipboard.writeText(value)
    toast.add({ title: 'Copied', description: 'Token copied to clipboard.', color: 'success' })
  } catch {
    toast.add({ title: 'Copy failed', description: 'Could not access the clipboard.', color: 'warning' })
  }
}

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const [clientData, planData, tokenData, subscriptionData] = await Promise.allSettled([
      get<unknown>('saas/clients'),
      get<unknown>('saas/plans'),
      get<unknown>('saas/tokens'),
      get<unknown>('saas/subscriptions')
    ])
    if (clientData.status === 'fulfilled') clients.value = toRows(clientData.value)
    if (planData.status === 'fulfilled') plans.value = toRows(planData.value)
    if (tokenData.status === 'fulfilled') tokens.value = toRows(tokenData.value)
    if (subscriptionData.status === 'fulfilled') subscriptions.value = toRows(subscriptionData.value)
    const failed = [clientData, planData, tokenData, subscriptionData].filter(item => item.status === 'rejected').length
    if (failed) error.value = `${failed} SaaS Manager request(s) could not be loaded. This page is SuperAdmin-only.`
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load SaaS Manager data.'
  } finally {
    loading.value = false
  }
}

function openClientCreate() {
  editingId.value = null
  Object.assign(clientForm, { clientCode: '', name: '', email: '', mobile: '', address: '', city: '', state: '', gstin: '', active: true })
  clientModalOpen.value = true
}

function openClientEdit(row: ApiRecord) {
  editingId.value = String(row.id)
  Object.assign(clientForm, {
    clientCode: row.clientCode ?? '', name: row.name ?? '', email: row.email ?? '', mobile: row.mobile ?? '',
    address: row.address ?? '', city: row.city ?? '', state: row.state ?? '', gstin: row.gstin ?? '', active: Boolean(row.active)
  })
  clientModalOpen.value = true
}

function openPlanCreate() {
  editingId.value = null
  Object.assign(planForm, { planName: '', maxCompanies: 1, maxStoreGroups: 1, maxStores: 2, maxUsers: 20, includedModulesCsv: '', active: true })
  planModalOpen.value = true
}

function openPlanEdit(row: ApiRecord) {
  editingId.value = String(row.id)
  Object.assign(planForm, {
    planName: row.planName ?? '', maxCompanies: Number(row.maxCompanies ?? 1), maxStoreGroups: Number(row.maxStoreGroups ?? 1),
    maxStores: Number(row.maxStores ?? 2), maxUsers: Number(row.maxUsers ?? 20), includedModulesCsv: row.includedModulesCsv ?? '', active: Boolean(row.active)
  })
  planModalOpen.value = true
}

function openTokenGenerate() {
  generatedTokenString.value = ''
  Object.assign(tokenForm, { saaSClientId: null, saaSPlanId: null, validityDays: 365, notes: '' })
  tokenModalOpen.value = true
}

function askDelete(type: 'client' | 'plan' | 'token', row: ApiRecord) {
  deleteType.value = type
  deleteTarget.value = row
  deleteModalOpen.value = true
}

const companyLinksModalOpen = ref(false)
const companyLinksClient = ref<ApiRecord | null>(null)
const companyLinks = ref<ApiRecord[]>([])
const loadingCompanyLinks = ref(false)
const linkingCompanyId = ref('')

async function openCompanyLinks(client: ApiRecord) {
  companyLinksClient.value = client
  companyLinksModalOpen.value = true
  await loadCompanyLinks()
}

async function loadCompanyLinks() {
  if (!companyLinksClient.value) return
  loadingCompanyLinks.value = true
  try {
    companyLinks.value = toRows(await get<unknown>(`saas/clients/${companyLinksClient.value.id}/companies`))
  } catch (caught) {
    toast.add({ title: 'Error', description: caught instanceof Error ? caught.message : 'Failed to load companies.', color: 'error' })
  } finally {
    loadingCompanyLinks.value = false
  }
}

async function toggleCompanyLink(company: ApiRecord) {
  if (!companyLinksClient.value) return
  linkingCompanyId.value = String(company.companyId)
  try {
    const action = company.linked ? 'unlink' : 'link'
    const response = await post<ApiRecord>(`saas/clients/${companyLinksClient.value.id}/companies/${company.companyId}/${action}`)
    toast.add({ title: 'Success', description: readText(response, ['message'], 'Updated.'), color: 'success' })
    await loadCompanyLinks()
    await refresh()
  } catch (caught) {
    toast.add({ title: 'Error', description: caught instanceof Error ? caught.message : 'Failed to update link.', color: 'error' })
  } finally {
    linkingCompanyId.value = ''
  }
}

async function saveClient() {
  if (!clientForm.clientCode || !clientForm.name) {
    toast.add({ title: 'Validation', description: 'Client Code and Name are required.', color: 'warning' })
    return
  }
  saving.value = true
  try {
    const body = { ...clientForm }
    if (editingId.value) {
      await put(`saas/clients/${editingId.value}`, body)
      toast.add({ title: 'Updated', description: 'Client updated successfully.', color: 'success' })
    } else {
      await post('saas/clients', body)
      toast.add({ title: 'Created', description: 'Client added successfully.', color: 'success' })
    }
    clientModalOpen.value = false
    await refresh()
  } catch (caught) {
    toast.add({ title: 'Error', description: caught instanceof Error ? caught.message : 'Failed to save client.', color: 'error' })
  } finally {
    saving.value = false
  }
}

async function savePlan() {
  if (!planForm.planName) {
    toast.add({ title: 'Validation', description: 'Plan Name is required.', color: 'warning' })
    return
  }
  saving.value = true
  try {
    const body = { ...planForm }
    if (editingId.value) {
      await put(`saas/plans/${editingId.value}`, body)
      toast.add({ title: 'Updated', description: 'Plan updated successfully.', color: 'success' })
    } else {
      await post('saas/plans', body)
      toast.add({ title: 'Created', description: 'Plan added successfully.', color: 'success' })
    }
    planModalOpen.value = false
    await refresh()
  } catch (caught) {
    toast.add({ title: 'Error', description: caught instanceof Error ? caught.message : 'Failed to save plan.', color: 'error' })
  } finally {
    saving.value = false
  }
}

async function generateToken() {
  const clientId = unwrapId(tokenForm.saaSClientId)
  const planId = unwrapId(tokenForm.saaSPlanId)
  if (!clientId || !planId) {
    toast.add({ title: 'Validation', description: 'Please select a Client and a Plan.', color: 'warning' })
    return
  }
  saving.value = true
  try {
    const result = await post<ApiRecord>('saas/tokens/generate', {
      saaSClientId: clientId,
      saaSPlanId: planId,
      validityDays: tokenForm.validityDays || 365,
      notes: tokenForm.notes || null
    })
    generatedTokenString.value = readText(result, ['tokenString'], '')
    toast.add({ title: 'Token Generated', description: 'Copy the token below and send it to the client.', color: 'success' })
    await refresh()
  } catch (caught) {
    toast.add({ title: 'Error', description: caught instanceof Error ? caught.message : 'Failed to generate token.', color: 'error' })
  } finally {
    saving.value = false
  }
}

async function executeDelete() {
  if (!deleteTarget.value) return
  saving.value = true
  try {
    const urlMap = { client: 'saas/clients', plan: 'saas/plans', token: 'saas/tokens' } as const
    await remove(`${urlMap[deleteType.value]}/${deleteTarget.value.id}`)
    toast.add({ title: 'Deleted', description: 'Record deleted successfully.', color: 'success' })
    deleteModalOpen.value = false
    await refresh()
  } catch (caught) {
    toast.add({ title: 'Error', description: caught instanceof Error ? caught.message : 'Delete failed.', color: 'error' })
  } finally {
    saving.value = false
  }
}

onMounted(refresh)
</script>
