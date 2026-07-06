<template>
  <div class="flex flex-col h-full p-6 gap-5">

    <!-- ── Page Header ─────────────────────────────────────────────────── -->
    <div class="flex items-center justify-between">
      <div>
        <h1 class="text-2xl font-bold text-gray-900 dark:text-white">SaaS Manager</h1>
        <p class="text-sm text-gray-500 dark:text-gray-400 mt-0.5">Manage clients, plans, tokens and subscriptions</p>
      </div>
      <div class="flex gap-2">
        <UButton v-if="activeTab === 'clients'" color="primary" icon="i-lucide-plus" @click="openCreate('client')">Add Client</UButton>
        <UButton v-if="activeTab === 'plans'"   color="primary" icon="i-lucide-plus" @click="openCreate('plan')">Add Plan</UButton>
        <UButton v-if="activeTab === 'tokens'"  color="primary" icon="i-lucide-key-round" @click="openCreate('token')">Generate Token</UButton>
      </div>
    </div>

    <!-- ── Tabs ─────────────────────────────────────────────────────────── -->
    <UTabs v-model="activeTab" :items="tabs" class="flex-1 flex flex-col min-h-0">

      <!-- CLIENTS ─────────────────────────────────────────────────── -->
      <template #clients>
        <div class="mt-4 rounded-xl border border-gray-200 dark:border-gray-800 overflow-hidden">
          <div v-if="loading" class="flex justify-center items-center py-16">
            <UIcon name="i-lucide-loader-circle" class="animate-spin size-8 text-primary" />
          </div>
          <template v-else>
            <UTable v-if="clients.length" :data="clients" :columns="clientColumns">
              <template #actions-cell="{ row }">
                <div class="flex gap-1 justify-end">
                  <UButton size="xs" variant="ghost" icon="i-lucide-pencil" @click="openEdit('client', row.original)" />
                  <UButton size="xs" variant="ghost" color="error" icon="i-lucide-trash-2" @click="confirmDelete('client', row.original)" />
                </div>
              </template>
            </UTable>
            <div v-else class="flex flex-col items-center justify-center py-16 gap-3 text-gray-400">
              <UIcon name="i-lucide-users" class="size-12 opacity-30" />
              <p>No clients yet. Click <strong>Add Client</strong> to get started.</p>
            </div>
          </template>
        </div>
      </template>

      <!-- PLANS ───────────────────────────────────────────────────── -->
      <template #plans>
        <div class="mt-4 rounded-xl border border-gray-200 dark:border-gray-800 overflow-hidden">
          <div v-if="loading" class="flex justify-center items-center py-16">
            <UIcon name="i-lucide-loader-circle" class="animate-spin size-8 text-primary" />
          </div>
          <template v-else>
            <UTable v-if="plans.length" :data="plans" :columns="planColumns">
              <template #actions-cell="{ row }">
                <div class="flex gap-1 justify-end">
                  <UButton size="xs" variant="ghost" icon="i-lucide-pencil" @click="openEdit('plan', row.original)" />
                  <UButton size="xs" variant="ghost" color="error" icon="i-lucide-trash-2" @click="confirmDelete('plan', row.original)" />
                </div>
              </template>
            </UTable>
            <div v-else class="flex flex-col items-center justify-center py-16 gap-3 text-gray-400">
              <UIcon name="i-lucide-layers" class="size-12 opacity-30" />
              <p>No plans yet. Click <strong>Add Plan</strong> to get started.</p>
            </div>
          </template>
        </div>
      </template>

      <!-- TOKENS ──────────────────────────────────────────────────── -->
      <template #tokens>
        <div class="mt-4 rounded-xl border border-gray-200 dark:border-gray-800 overflow-hidden">
          <div v-if="loading" class="flex justify-center items-center py-16">
            <UIcon name="i-lucide-loader-circle" class="animate-spin size-8 text-primary" />
          </div>
          <template v-else>
            <UTable v-if="tokens.length" :data="tokens" :columns="tokenColumns">
              <template #isActivated-cell="{ row }">
                <UBadge :color="row.original.isActivated ? 'success' : 'warning'" variant="subtle" size="sm">
                  {{ row.original.isActivated ? 'Activated' : 'Pending' }}
                </UBadge>
              </template>
              <template #expiresAt-cell="{ row }">
                <span :class="isExpired(row.original.expiresAt) ? 'text-red-500' : 'text-gray-700 dark:text-gray-300'">
                  {{ formatDate(row.original.expiresAt) }}
                </span>
              </template>
              <template #actions-cell="{ row }">
                <div class="flex gap-1 justify-end">
                  <UButton v-if="!row.original.isActivated" size="xs" color="success" variant="subtle" icon="i-lucide-zap" @click="openActivateToken(row.original)">Activate</UButton>
                  <UButton size="xs" variant="ghost" color="error" icon="i-lucide-trash-2" @click="confirmDelete('token', row.original)" />
                </div>
              </template>
            </UTable>
            <div v-else class="flex flex-col items-center justify-center py-16 gap-3 text-gray-400">
              <UIcon name="i-lucide-key" class="size-12 opacity-30" />
              <p>No tokens yet. Click <strong>Generate Token</strong> to create one.</p>
            </div>
          </template>
        </div>
      </template>

      <!-- SUBSCRIPTIONS ────────────────────────────────────────────── -->
      <template #subscriptions>
        <div class="mt-4 rounded-xl border border-gray-200 dark:border-gray-800 overflow-hidden">
          <div v-if="loading" class="flex justify-center items-center py-16">
            <UIcon name="i-lucide-loader-circle" class="animate-spin size-8 text-primary" />
          </div>
          <template v-else>
            <UTable v-if="subscriptions.length" :data="subscriptions" :columns="subscriptionColumns">
              <template #isActive-cell="{ row }">
                <UBadge :color="row.original.isActive ? 'success' : 'error'" variant="subtle" size="sm">
                  {{ row.original.isActive ? 'Active' : 'Inactive' }}
                </UBadge>
              </template>
              <template #validFrom-cell="{ row }">{{ formatDate(row.original.validFrom) }}</template>
              <template #validTo-cell="{ row }">
                <span :class="isExpired(row.original.validTo) ? 'text-red-500' : ''">{{ formatDate(row.original.validTo) }}</span>
              </template>
            </UTable>
            <div v-else class="flex flex-col items-center justify-center py-16 gap-3 text-gray-400">
              <UIcon name="i-lucide-activity" class="size-12 opacity-30" />
              <p>No active subscriptions found.</p>
            </div>
          </template>
        </div>
      </template>
    </UTabs>

    <!-- ════════════════════════════════════════════════════════════════ -->
    <!-- MODALS                                                          -->
    <!-- ════════════════════════════════════════════════════════════════ -->

    <!-- Add / Edit Client -->
    <UModal v-model:open="showClientModal" :title="editingId ? 'Edit Client' : 'Add New Client'">
      <template #body>
        <div class="space-y-4">
          <div class="grid grid-cols-2 gap-4">
            <UFormField label="Client Code" required>
              <UInput v-model="clientForm.clientCode" placeholder="CLI001" class="w-full" :disabled="!!editingId" />
            </UFormField>
            <UFormField label="Client Name" required>
              <UInput v-model="clientForm.name" placeholder="Full organization name" class="w-full" />
            </UFormField>
          </div>
          <div class="grid grid-cols-2 gap-4">
            <UFormField label="Email">
              <UInput v-model="clientForm.email" type="email" placeholder="contact@example.com" class="w-full" />
            </UFormField>
            <UFormField label="Mobile">
              <UInput v-model="clientForm.mobile" placeholder="+91 9876543210" class="w-full" />
            </UFormField>
          </div>
          <div class="grid grid-cols-2 gap-4">
            <UFormField label="City">
              <UInput v-model="clientForm.city" placeholder="Mumbai" class="w-full" />
            </UFormField>
            <UFormField label="State">
              <UInput v-model="clientForm.state" placeholder="Maharashtra" class="w-full" />
            </UFormField>
          </div>
          <UFormField label="GSTIN">
            <UInput v-model="clientForm.gstin" placeholder="27AAPFU0939F1ZV" class="w-full" />
          </UFormField>
          <UFormField label="Address">
            <UTextarea v-model="clientForm.address" placeholder="Full address..." class="w-full" />
          </UFormField>
        </div>
      </template>
      <template #footer>
        <div class="flex justify-end gap-3 w-full">
          <UButton variant="ghost" @click="showClientModal = false">Cancel</UButton>
          <UButton color="primary" :loading="saving" @click="saveClient">{{ editingId ? 'Update' : 'Save Client' }}</UButton>
        </div>
      </template>
    </UModal>

    <!-- Add / Edit Plan -->
    <UModal v-model:open="showPlanModal" :title="editingId ? 'Edit Plan' : 'Add New Plan'">
      <template #body>
        <div class="space-y-4">
          <UFormField label="Plan Name" required>
            <UInput v-model="planForm.planName" placeholder="e.g. Premium Plan" class="w-full" />
          </UFormField>
          <div class="grid grid-cols-3 gap-3">
            <UFormField label="Max Companies">
              <UInput v-model.number="planForm.maxCompanies" type="number" min="1" class="w-full" />
            </UFormField>
            <UFormField label="Max Stores">
              <UInput v-model.number="planForm.maxStores" type="number" min="1" class="w-full" />
            </UFormField>
            <UFormField label="Max Users">
              <UInput v-model.number="planForm.maxUsers" type="number" min="1" class="w-full" />
            </UFormField>
          </div>
          <UFormField label="Included Modules" help="Comma-separated keys, e.g. main,pos,hr">
            <UInput v-model="planForm.includedModulesCsv" placeholder="main,pos,inventory,hr,books" class="w-full" />
          </UFormField>
          <div class="flex items-center gap-3">
            <USwitch v-model="planForm.isActive" />
            <span class="text-sm text-gray-600 dark:text-gray-400">Plan is Active</span>
          </div>
        </div>
      </template>
      <template #footer>
        <div class="flex justify-end gap-3 w-full">
          <UButton variant="ghost" @click="showPlanModal = false">Cancel</UButton>
          <UButton color="primary" :loading="saving" @click="savePlan">{{ editingId ? 'Update' : 'Save Plan' }}</UButton>
        </div>
      </template>
    </UModal>

    <!-- Generate Token -->
    <UModal v-model:open="showTokenModal" title="Generate License Token">
      <template #body>
        <div class="space-y-4">
          <UFormField label="Client" required>
            <USelectMenu v-model="tokenForm.selectedClient" :items="clientOptions" value-key="value" placeholder="Select client..." class="w-full" />
          </UFormField>
          <UFormField label="Plan" required>
            <USelectMenu v-model="tokenForm.selectedPlan" :items="planOptions" value-key="value" placeholder="Select plan..." class="w-full" />
          </UFormField>
          <UFormField label="Validity (Days)">
            <UInput v-model.number="tokenForm.validityDays" type="number" min="1" class="w-full" />
          </UFormField>
        </div>
      </template>
      <template #footer>
        <div class="flex justify-end gap-3 w-full">
          <UButton variant="ghost" @click="showTokenModal = false">Cancel</UButton>
          <UButton color="primary" :loading="saving" @click="generateToken">Generate Token</UButton>
        </div>
      </template>
    </UModal>

    <!-- Activate Token -->
    <UModal v-model:open="showActivateModal" title="Activate Token for a Tenant">
      <template #body>
        <div class="space-y-4">
          <UAlert color="info" variant="subtle" icon="i-lucide-info" title="Token Details"
            :description="`Token: ${activateForm.tokenString} · Plan: ${activateForm.planName}`" />
          <UFormField label="Select Tenant Company" required help="The company this token will be assigned to">
            <USelectMenu v-model="activateForm.selectedCompany" :items="companyOptions" value-key="value" placeholder="Select company..." class="w-full" />
          </UFormField>
        </div>
      </template>
      <template #footer>
        <div class="flex justify-end gap-3 w-full">
          <UButton variant="ghost" @click="showActivateModal = false">Cancel</UButton>
          <UButton color="success" :loading="saving" icon="i-lucide-zap" @click="activateToken">Activate</UButton>
        </div>
      </template>
    </UModal>

    <!-- Delete Confirmation -->
    <UModal v-model:open="showDeleteModal" title="Confirm Delete">
      <template #body>
        <UAlert color="error" variant="subtle" icon="i-lucide-triangle-alert"
          title="This action cannot be undone"
          :description="`Are you sure you want to delete '${deleteTarget?.name || deleteTarget?.planName || deleteTarget?.tokenString || ''}'?`" />
      </template>
      <template #footer>
        <div class="flex justify-end gap-3 w-full">
          <UButton variant="ghost" @click="showDeleteModal = false">Cancel</UButton>
          <UButton color="error" :loading="saving" icon="i-lucide-trash-2" @click="executeDelete">Delete</UButton>
        </div>
      </template>
    </UModal>

  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'

const config = useRuntimeConfig()
const toast  = useToast()

// ── Tab state ──────────────────────────────────────────────────────────────
const activeTab = ref('clients')
const tabs = [
  { label: 'Clients',              icon: 'i-lucide-users',    value: 'clients',       slot: 'clients'       },
  { label: 'License Plans',        icon: 'i-lucide-layers',   value: 'plans',         slot: 'plans'         },
  { label: 'License Tokens',       icon: 'i-lucide-key',      value: 'tokens',        slot: 'tokens'        },
  { label: 'Tenant Subscriptions', icon: 'i-lucide-activity', value: 'subscriptions', slot: 'subscriptions' },
]

// ── Loading ────────────────────────────────────────────────────────────────
const loading = ref(false)
const saving  = ref(false)

// ── Data ───────────────────────────────────────────────────────────────────
const clients       = ref<any[]>([])
const plans         = ref<any[]>([])
const tokens        = ref<any[]>([])
const subscriptions = ref<any[]>([])
const companies     = ref<any[]>([])  // for activate token dropdown

// ── Modal visibility ───────────────────────────────────────────────────────
const showClientModal   = ref(false)
const showPlanModal     = ref(false)
const showTokenModal    = ref(false)
const showActivateModal = ref(false)
const showDeleteModal   = ref(false)

// ── Editing state ──────────────────────────────────────────────────────────
const editingId    = ref<string | null>(null)
const deleteTarget = ref<any>(null)
const deleteType   = ref<'client' | 'plan' | 'token'>('client')

// ── Forms ──────────────────────────────────────────────────────────────────
const clientForm = reactive({
  clientCode: '', name: '', email: '', mobile: '',
  address: '', city: '', state: '', country: '', zipCode: '', gstin: '', isActive: true
})

const planForm = reactive({
  planName: '', maxCompanies: 1, maxStores: 2, maxUsers: 20, maxStoreGroups: 1,
  includedModulesCsv: '', isActive: true
})

const tokenForm = reactive({
  selectedClient: null as any,
  selectedPlan:   null as any,
  validityDays:   365
})

const activateForm = reactive({
  tokenString:     '',
  planName:        '',
  selectedCompany: null as any
})

// ── Dropdown options ───────────────────────────────────────────────────────
const clientOptions  = computed(() => clients.value.map(c => ({ label: `${c.clientCode} – ${c.name}`, value: c.id })))
const planOptions    = computed(() => plans.value.map(p => ({ label: p.planName, value: p.id })))
const companyOptions = computed(() => companies.value.map(c => ({ label: c.name, value: c.id })))

// ── Table columns ──────────────────────────────────────────────────────────
const clientColumns = [
  { accessorKey: 'clientCode', header: 'Code'    },
  { accessorKey: 'name',       header: 'Name'    },
  { accessorKey: 'email',      header: 'Email'   },
  { accessorKey: 'mobile',     header: 'Mobile'  },
  { accessorKey: 'city',       header: 'City'    },
  { accessorKey: 'actions',    header: '',        enableSorting: false },
]

const planColumns = [
  { accessorKey: 'planName',           header: 'Plan Name'     },
  { accessorKey: 'maxCompanies',       header: 'Companies'     },
  { accessorKey: 'maxStores',          header: 'Stores'        },
  { accessorKey: 'maxUsers',           header: 'Users'         },
  { accessorKey: 'includedModulesCsv', header: 'Modules'       },
  { accessorKey: 'actions',            header: '', enableSorting: false },
]

const tokenColumns = [
  { accessorKey: 'tokenString',       header: 'Token'       },
  { accessorKey: 'saaSClient.name',   header: 'Client'      },
  { accessorKey: 'saaSPlan.planName', header: 'Plan'        },
  { accessorKey: 'isActivated',       header: 'Status'      },
  { accessorKey: 'expiresAt',         header: 'Expires'     },
  { accessorKey: 'actions',           header: '', enableSorting: false },
]

const subscriptionColumns = [
  { accessorKey: 'company.name', header: 'Company'    },
  { accessorKey: 'planName',     header: 'Plan'       },
  { accessorKey: 'maxUsers',     header: 'Users'      },
  { accessorKey: 'maxStores',    header: 'Stores'     },
  { accessorKey: 'validFrom',    header: 'Valid From' },
  { accessorKey: 'validTo',      header: 'Valid To'   },
  { accessorKey: 'isActive',     header: 'Status'     },
]

// ── Helpers ────────────────────────────────────────────────────────────────
function getHeaders() {
  if (!import.meta.client) return {}
  const token = localStorage.getItem('garmetix.token')
  return token ? { Authorization: `Bearer ${token}` } : {}
}

function unwrap(data: any): any[] {
  if (!data) return []
  return Array.isArray(data) ? data : (data.$values ?? [])
}

function formatDate(d: string | null | undefined) {
  if (!d) return '—'
  return new Date(d).toLocaleDateString('en-IN', { day: '2-digit', month: 'short', year: 'numeric' })
}

function isExpired(d: string | null | undefined) {
  if (!d) return false
  return new Date(d) < new Date()
}

// ── Fetch ──────────────────────────────────────────────────────────────────
async function fetchClients() {
  try { clients.value = unwrap(await $fetch(config.public.apiBaseUrl + '/saas/clients', { headers: getHeaders() })) }
  catch (e) { console.error('fetchClients:', e) }
}

async function fetchPlans() {
  try { plans.value = unwrap(await $fetch(config.public.apiBaseUrl + '/saas/plans', { headers: getHeaders() })) }
  catch (e) { console.error('fetchPlans:', e) }
}

async function fetchTokens() {
  try { tokens.value = unwrap(await $fetch(config.public.apiBaseUrl + '/saas/tokens', { headers: getHeaders() })) }
  catch (e) { console.error('fetchTokens:', e) }
}

async function fetchSubscriptions() {
  try { subscriptions.value = unwrap(await $fetch(config.public.apiBaseUrl + '/saas/subscriptions', { headers: getHeaders() })) }
  catch (e) { console.error('fetchSubscriptions:', e) }
}

async function fetchCompanies() {
  try {
    const data = unwrap(await $fetch(config.public.apiBaseUrl + '/companies', { headers: getHeaders() }))
    companies.value = data
  } catch (e) { console.error('fetchCompanies:', e) }
}

// ── Modal open helpers ─────────────────────────────────────────────────────
function openCreate(type: 'client' | 'plan' | 'token') {
  editingId.value = null
  if (type === 'client') {
    Object.assign(clientForm, { clientCode: '', name: '', email: '', mobile: '', address: '', city: '', state: '', country: '', zipCode: '', gstin: '', isActive: true })
    showClientModal.value = true
  } else if (type === 'plan') {
    Object.assign(planForm, { planName: '', maxCompanies: 1, maxStores: 2, maxUsers: 20, maxStoreGroups: 1, includedModulesCsv: '', isActive: true })
    showPlanModal.value = true
  } else {
    tokenForm.selectedClient = null
    tokenForm.selectedPlan   = null
    tokenForm.validityDays   = 365
    showTokenModal.value = true
  }
}

function openEdit(type: 'client' | 'plan', row: any) {
  editingId.value = row.id
  if (type === 'client') {
    Object.assign(clientForm, {
      clientCode: row.clientCode, name: row.name, email: row.email, mobile: row.mobile,
      address: row.address || '', city: row.city || '', state: row.state || '',
      country: row.country || '', zipCode: row.zipCode || '', gstin: row.gstin || row.gSTIN || '', isActive: row.isActive
    })
    showClientModal.value = true
  } else {
    Object.assign(planForm, {
      planName: row.planName, maxCompanies: row.maxCompanies, maxStores: row.maxStores,
      maxUsers: row.maxUsers, maxStoreGroups: row.maxStoreGroups || 1,
      includedModulesCsv: row.includedModulesCsv || '', isActive: row.isActive
    })
    showPlanModal.value = true
  }
}

function confirmDelete(type: 'client' | 'plan' | 'token', row: any) {
  deleteType.value   = type
  deleteTarget.value = row
  showDeleteModal.value = true
}

function openActivateToken(token: any) {
  activateForm.tokenString     = token.tokenString
  activateForm.planName        = token.saaSPlan?.planName || ''
  activateForm.selectedCompany = null
  fetchCompanies()
  showActivateModal.value = true
}

// ── CRUD ───────────────────────────────────────────────────────────────────
async function saveClient() {
  if (!clientForm.clientCode || !clientForm.name) {
    toast.add({ title: 'Validation', description: 'Client Code and Name are required.', color: 'warning' })
    return
  }
  saving.value = true
  try {
    const body = {
      clientCode: clientForm.clientCode, name: clientForm.name, email: clientForm.email,
      mobile: clientForm.mobile, address: clientForm.address, city: clientForm.city,
      state: clientForm.state, country: clientForm.country, zipCode: clientForm.zipCode,
      gSTIN: clientForm.gstin, isActive: clientForm.isActive
    }
    if (editingId.value) {
      await $fetch(config.public.apiBaseUrl + `/saas/clients/${editingId.value}`, { method: 'PUT', headers: getHeaders(), body })
      toast.add({ title: 'Updated', description: 'Client updated successfully.', color: 'success' })
    } else {
      await $fetch(config.public.apiBaseUrl + '/saas/clients', { method: 'POST', headers: getHeaders(), body })
      toast.add({ title: 'Created', description: 'Client added successfully.', color: 'success' })
    }
    showClientModal.value = false
    await fetchClients()
  } catch (e: any) {
    toast.add({ title: 'Error', description: e.data?.message || 'Failed to save client.', color: 'error' })
  } finally { saving.value = false }
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
      await $fetch(config.public.apiBaseUrl + `/saas/plans/${editingId.value}`, { method: 'PUT', headers: getHeaders(), body })
      toast.add({ title: 'Updated', description: 'Plan updated successfully.', color: 'success' })
    } else {
      await $fetch(config.public.apiBaseUrl + '/saas/plans', { method: 'POST', headers: getHeaders(), body })
      toast.add({ title: 'Created', description: 'Plan added successfully.', color: 'success' })
    }
    showPlanModal.value = false
    await fetchPlans()
  } catch (e: any) {
    toast.add({ title: 'Error', description: e.data?.message || 'Failed to save plan.', color: 'error' })
  } finally { saving.value = false }
}

async function generateToken() {
  const clientId = tokenForm.selectedClient?.value ?? tokenForm.selectedClient
  const planId   = tokenForm.selectedPlan?.value   ?? tokenForm.selectedPlan
  if (!clientId || !planId) {
    toast.add({ title: 'Validation', description: 'Please select a Client and a Plan.', color: 'warning' })
    return
  }
  saving.value = true
  try {
    const result = await $fetch(config.public.apiBaseUrl + '/saas/tokens/generate', {
      method: 'POST', headers: getHeaders(),
      body: { clientId, planId, validityDays: tokenForm.validityDays || 365 }
    }) as any
    toast.add({ title: 'Token Generated', description: `Token ready: ${result.tokenString ?? result.TokenString ?? ''}`, color: 'success' })
    showTokenModal.value = false
    await fetchTokens()
  } catch (e: any) {
    toast.add({ title: 'Error', description: e.data?.message || 'Failed to generate token.', color: 'error' })
  } finally { saving.value = false }
}

async function activateToken() {
  const companyId = activateForm.selectedCompany?.value ?? activateForm.selectedCompany
  if (!companyId) {
    toast.add({ title: 'Validation', description: 'Please select a company.', color: 'warning' })
    return
  }
  saving.value = true
  try {
    await $fetch(config.public.apiBaseUrl + '/saas/tokens/activate', {
      method: 'POST', headers: getHeaders(),
      body: { tokenString: activateForm.tokenString, companyId }
    })
    toast.add({ title: 'Activated!', description: 'Token activated and subscription created.', color: 'success' })
    showActivateModal.value = false
    await Promise.all([fetchTokens(), fetchSubscriptions()])
  } catch (e: any) {
    toast.add({ title: 'Error', description: e.data?.message || 'Activation failed.', color: 'error' })
  } finally { saving.value = false }
}

async function executeDelete() {
  saving.value = true
  try {
    const urlMap = { client: 'clients', plan: 'plans', token: 'tokens' }
    await $fetch(`${config.public.apiBaseUrl}/saas/${urlMap[deleteType.value]}/${deleteTarget.value.id}`, {
      method: 'DELETE', headers: getHeaders()
    })
    toast.add({ title: 'Deleted', description: 'Record deleted successfully.', color: 'success' })
    showDeleteModal.value = false
    if (deleteType.value === 'client')      await fetchClients()
    else if (deleteType.value === 'plan')   await fetchPlans()
    else if (deleteType.value === 'token')  await fetchTokens()
  } catch (e: any) {
    toast.add({ title: 'Error', description: e.data?.message || 'Delete failed.', color: 'error' })
  } finally { saving.value = false }
}

// ── Init ───────────────────────────────────────────────────────────────────
async function initData() {
  loading.value = true
  await Promise.all([fetchClients(), fetchPlans(), fetchTokens(), fetchSubscriptions()])
  loading.value = false
}

onMounted(() => initData())
</script>
