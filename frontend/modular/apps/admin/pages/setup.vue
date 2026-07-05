<template>
  <div class="flex flex-col h-full p-6 gap-5">

    <!-- Header -->
    <div class="flex items-center justify-between">
      <div>
        <h1 class="text-2xl font-bold text-gray-900 dark:text-white">Company Setup</h1>
        <p class="text-sm text-gray-500 dark:text-gray-400 mt-0.5">Manage companies, store groups and stores</p>
      </div>
      <div class="flex gap-2">
        <UButton variant="soft" color="neutral" icon="i-lucide-refresh-cw" :loading="loading" @click="refresh">Refresh</UButton>
        <UButton v-if="activeTab === 'companies'" color="primary" icon="i-lucide-plus" @click="openCreate('company')">Add Company</UButton>
        <UButton v-if="activeTab === 'groups'" color="primary" icon="i-lucide-plus" @click="openCreate('group')">Add Group</UButton>
        <UButton v-if="activeTab === 'stores'" color="primary" icon="i-lucide-plus" @click="openCreate('store')">Add Store</UButton>
      </div>
    </div>

    <!-- Stats -->
    <div class="grid grid-cols-3 gap-4">
      <div class="rounded-xl border border-gray-200 dark:border-gray-800 bg-gray-50 dark:bg-gray-900 p-4">
        <p class="text-xs text-gray-500 dark:text-gray-400 uppercase tracking-wider">Companies</p>
        <p class="text-3xl font-bold text-gray-900 dark:text-white mt-1">{{ companies.length }}</p>
        <p class="text-xs text-gray-400 mt-1">{{ companies.filter(c => c.active).length }} active</p>
      </div>
      <div class="rounded-xl border border-gray-200 dark:border-gray-800 bg-gray-50 dark:bg-gray-900 p-4">
        <p class="text-xs text-gray-500 dark:text-gray-400 uppercase tracking-wider">Store Groups</p>
        <p class="text-3xl font-bold text-gray-900 dark:text-white mt-1">{{ groups.length }}</p>
        <p class="text-xs text-gray-400 mt-1">{{ groups.filter(g => g.active).length }} active</p>
      </div>
      <div class="rounded-xl border border-gray-200 dark:border-gray-800 bg-gray-50 dark:bg-gray-900 p-4">
        <p class="text-xs text-gray-500 dark:text-gray-400 uppercase tracking-wider">Stores</p>
        <p class="text-3xl font-bold text-gray-900 dark:text-white mt-1">{{ stores.length }}</p>
        <p class="text-xs text-gray-400 mt-1">{{ stores.filter(s => s.active).length }} active</p>
      </div>
    </div>

    <!-- Tabs -->
    <UTabs v-model="activeTab" :items="tabs" class="flex-1 flex flex-col min-h-0">
      
      <!-- COMPANIES -->
      <template #companies>
        <div class="mt-4 rounded-xl border border-gray-200 dark:border-gray-800 overflow-hidden">
          <div v-if="loading" class="flex justify-center items-center py-16">
            <UIcon name="i-lucide-loader-circle" class="animate-spin size-8 text-primary" />
          </div>
          <template v-else>
            <UTable v-if="companies.length" :data="companies" :columns="companyColumns">
              <template #active-cell="{ row }">
                <UBadge :color="row.original.active ? 'success' : 'neutral'" variant="subtle" size="sm">
                  {{ row.original.active ? 'Active' : 'Inactive' }}
                </UBadge>
              </template>
              <template #actions-cell="{ row }">
                <div class="flex gap-1 justify-end">
                  <UButton size="xs" variant="ghost" icon="i-lucide-pencil" @click="openEdit('company', row.original)" />
                  <UButton size="xs" variant="ghost" color="error" icon="i-lucide-trash-2" @click="confirmDelete('company', row.original)" />
                </div>
              </template>
            </UTable>
            <div v-else class="flex flex-col items-center py-16 gap-3 text-gray-400">
              <UIcon name="i-lucide-building-2" class="size-12 opacity-30" />
              <p>No companies yet. Use <strong>Client Onboarding</strong> or click <strong>Add Company</strong>.</p>
            </div>
          </template>
        </div>
      </template>

      <!-- STORE GROUPS -->
      <template #groups>
        <div class="mt-4 rounded-xl border border-gray-200 dark:border-gray-800 overflow-hidden">
          <div v-if="loading" class="flex justify-center items-center py-16">
            <UIcon name="i-lucide-loader-circle" class="animate-spin size-8 text-primary" />
          </div>
          <template v-else>
            <UTable v-if="groups.length" :data="groupRows" :columns="groupColumns">
              <template #active-cell="{ row }">
                <UBadge :color="row.original.active ? 'success' : 'neutral'" variant="subtle" size="sm">
                  {{ row.original.active ? 'Active' : 'Inactive' }}
                </UBadge>
              </template>
              <template #actions-cell="{ row }">
                <div class="flex gap-1 justify-end">
                  <UButton size="xs" variant="ghost" icon="i-lucide-pencil" @click="openEdit('group', findGroup(row.original.id))" />
                  <UButton size="xs" variant="ghost" color="error" icon="i-lucide-trash-2" @click="confirmDelete('group', findGroup(row.original.id))" />
                </div>
              </template>
            </UTable>
            <div v-else class="flex flex-col items-center py-16 gap-3 text-gray-400">
              <UIcon name="i-lucide-folder-tree" class="size-12 opacity-30" />
              <p>No store groups yet. Add a company first, then create a store group.</p>
            </div>
          </template>
        </div>
      </template>

      <!-- STORES -->
      <template #stores>
        <div class="mt-4 rounded-xl border border-gray-200 dark:border-gray-800 overflow-hidden">
          <div v-if="loading" class="flex justify-center items-center py-16">
            <UIcon name="i-lucide-loader-circle" class="animate-spin size-8 text-primary" />
          </div>
          <template v-else>
            <UTable v-if="stores.length" :data="storeRows" :columns="storeColumns">
              <template #active-cell="{ row }">
                <UBadge :color="row.original.active ? 'success' : 'neutral'" variant="subtle" size="sm">
                  {{ row.original.active ? 'Active' : 'Inactive' }}
                </UBadge>
              </template>
              <template #actions-cell="{ row }">
                <div class="flex gap-1 justify-end">
                  <UButton size="xs" variant="ghost" icon="i-lucide-pencil" @click="openEdit('store', findStore(row.original.id))" />
                  <UButton size="xs" variant="ghost" color="error" icon="i-lucide-trash-2" @click="confirmDelete('store', findStore(row.original.id))" />
                </div>
              </template>
            </UTable>
            <div v-else class="flex flex-col items-center py-16 gap-3 text-gray-400">
              <UIcon name="i-lucide-store" class="size-12 opacity-30" />
              <p>No stores yet. Add a store group first, then create stores.</p>
            </div>
          </template>
        </div>
      </template>
    </UTabs>

    <!-- ════════ MODALS ════════ -->

    <!-- Company Modal -->
    <UModal v-model:open="showCompanyModal" :title="editingId ? 'Edit Company' : 'Add Company'">
      <template #body>
        <div class="space-y-4">
          <div class="grid grid-cols-2 gap-3">
            <UFormField label="Company Name" required>
              <UInput v-model="companyForm.name" placeholder="ABC Enterprises" class="w-full" />
            </UFormField>
            <UFormField label="Company Code" required>
              <UInput v-model="companyForm.code" placeholder="ABC001" class="w-full" />
            </UFormField>
          </div>
          <div class="grid grid-cols-2 gap-3">
            <UFormField label="Company Type">
              <USelectMenu v-model="companyForm.companyType" :items="companyTypeOptions" value-key="value" class="w-full" />
            </UFormField>
            <UFormField label="Store Category">
              <USelectMenu v-model="companyForm.storeCategory" :items="storeCategoryOptions" value-key="value" class="w-full" />
            </UFormField>
          </div>
          <div class="grid grid-cols-2 gap-3">
            <UFormField label="GSTIN">
              <UInput v-model="companyForm.gstin" placeholder="27AAPFU0939F1ZV" class="w-full" />
            </UFormField>
            <UFormField label="PAN">
              <UInput v-model="companyForm.pan" placeholder="AAPFU0939F" class="w-full" />
            </UFormField>
          </div>
          <div class="grid grid-cols-2 gap-3">
            <UFormField label="Contact Number">
              <UInput v-model="companyForm.contactNumber" placeholder="+91 9876543210" class="w-full" />
            </UFormField>
            <UFormField label="Email">
              <UInput v-model="companyForm.email" type="email" placeholder="info@company.com" class="w-full" />
            </UFormField>
          </div>
          <div class="grid grid-cols-2 gap-3">
            <UFormField label="Contact Person">
              <UInput v-model="companyForm.contactPerson" placeholder="Owner name" class="w-full" />
            </UFormField>
            <UFormField label="Contact Mobile">
              <UInput v-model="companyForm.contactMobile" placeholder="+91 9876543210" class="w-full" />
            </UFormField>
          </div>
          <UFormField label="Address">
            <UTextarea v-model="companyForm.address" placeholder="Street address..." class="w-full" />
          </UFormField>
          <div class="grid grid-cols-3 gap-3">
            <UFormField label="City">
              <UInput v-model="companyForm.city" class="w-full" />
            </UFormField>
            <UFormField label="State">
              <UInput v-model="companyForm.state" class="w-full" />
            </UFormField>
            <UFormField label="Zip Code">
              <UInput v-model="companyForm.zipCode" class="w-full" />
            </UFormField>
          </div>
          <div class="grid grid-cols-2 gap-3">
            <UFormField label="Start Date">
              <UInput v-model="companyForm.startDate" type="date" class="w-full" />
            </UFormField>
            <UFormField label="CIN">
              <UInput v-model="companyForm.cin" placeholder="U74999DL2000PTC104430" class="w-full" />
            </UFormField>
          </div>
          <div class="flex items-center gap-3">
            <USwitch v-model="companyForm.active" />
            <span class="text-sm text-gray-600 dark:text-gray-400">Company is Active</span>
          </div>
        </div>
      </template>
      <template #footer>
        <div class="flex justify-end gap-3 w-full">
          <UButton variant="ghost" @click="showCompanyModal = false">Cancel</UButton>
          <UButton color="primary" :loading="saving" @click="saveCompany">{{ editingId ? 'Update' : 'Save' }}</UButton>
        </div>
      </template>
    </UModal>

    <!-- Store Group Modal -->
    <UModal v-model:open="showGroupModal" :title="editingId ? 'Edit Store Group' : 'Add Store Group'">
      <template #body>
        <div class="space-y-4">
          <UFormField label="Select Company" required>
            <USelectMenu v-model="groupForm.companyId" :items="companyOptions" value-key="value" placeholder="Choose company..." class="w-full" />
          </UFormField>
          <div class="grid grid-cols-2 gap-3">
            <UFormField label="Group Name" required>
              <UInput v-model="groupForm.name" placeholder="Main Branch Group" class="w-full" />
            </UFormField>
            <UFormField label="Group Code" required>
              <UInput v-model="groupForm.groupCode" placeholder="GRP001" class="w-full" />
            </UFormField>
          </div>
          <UFormField label="Store Category">
            <USelectMenu v-model="groupForm.storeCategory" :items="storeCategoryOptions" value-key="value" class="w-full" />
          </UFormField>
          <UFormField label="Start Date">
            <UInput v-model="groupForm.startDate" type="date" class="w-full" />
          </UFormField>
          <div class="flex items-center gap-3">
            <USwitch v-model="groupForm.active" />
            <span class="text-sm text-gray-600 dark:text-gray-400">Group is Active</span>
          </div>
        </div>
      </template>
      <template #footer>
        <div class="flex justify-end gap-3 w-full">
          <UButton variant="ghost" @click="showGroupModal = false">Cancel</UButton>
          <UButton color="primary" :loading="saving" @click="saveGroup">{{ editingId ? 'Update' : 'Save' }}</UButton>
        </div>
      </template>
    </UModal>

    <!-- Store Modal -->
    <UModal v-model:open="showStoreModal" :title="editingId ? 'Edit Store' : 'Add Store'">
      <template #body>
        <div class="space-y-4">
          <div class="grid grid-cols-2 gap-3">
            <UFormField label="Select Company" required>
              <USelectMenu v-model="storeForm.companyId" :items="companyOptions" value-key="value" placeholder="Company..." class="w-full" @update:model-value="storeForm.storeGroupId = null" />
            </UFormField>
            <UFormField label="Select Store Group" required>
              <USelectMenu v-model="storeForm.storeGroupId" :items="groupOptionsForCompany" value-key="value" placeholder="Group..." class="w-full" />
            </UFormField>
          </div>
          <div class="grid grid-cols-2 gap-3">
            <UFormField label="Store Name" required>
              <UInput v-model="storeForm.name" placeholder="Main Store" class="w-full" />
            </UFormField>
            <UFormField label="Store Code" required>
              <UInput v-model="storeForm.storeCode" placeholder="STR001" class="w-full" />
            </UFormField>
          </div>
          <UFormField label="Store Category">
            <USelectMenu v-model="storeForm.storeCategory" :items="storeCategoryOptions" value-key="value" class="w-full" />
          </UFormField>
          <div class="grid grid-cols-2 gap-3">
            <UFormField label="Contact Number">
              <UInput v-model="storeForm.contactNumber" placeholder="+91 9876543210" class="w-full" />
            </UFormField>
            <UFormField label="Email">
              <UInput v-model="storeForm.email" type="email" class="w-full" />
            </UFormField>
          </div>
          <UFormField label="Address">
            <UTextarea v-model="storeForm.address" class="w-full" />
          </UFormField>
          <div class="grid grid-cols-3 gap-3">
            <UFormField label="City">
              <UInput v-model="storeForm.city" class="w-full" />
            </UFormField>
            <UFormField label="State">
              <UInput v-model="storeForm.state" class="w-full" />
            </UFormField>
            <UFormField label="Zip Code">
              <UInput v-model="storeForm.zipCode" class="w-full" />
            </UFormField>
          </div>
          <UFormField label="Start Date">
            <UInput v-model="storeForm.startDate" type="date" class="w-full" />
          </UFormField>
          <div class="flex items-center gap-3">
            <USwitch v-model="storeForm.active" />
            <span class="text-sm text-gray-600 dark:text-gray-400">Store is Active</span>
          </div>
        </div>
      </template>
      <template #footer>
        <div class="flex justify-end gap-3 w-full">
          <UButton variant="ghost" @click="showStoreModal = false">Cancel</UButton>
          <UButton color="primary" :loading="saving" @click="saveStore">{{ editingId ? 'Update' : 'Save' }}</UButton>
        </div>
      </template>
    </UModal>

    <!-- Delete Confirm -->
    <UModal v-model:open="showDeleteModal" title="Confirm Delete">
      <template #body>
        <UAlert color="error" variant="subtle" icon="i-lucide-triangle-alert"
          title="This cannot be undone"
          :description="`Delete '${deleteTarget?.name || ''}'?`" />
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

useHead({ title: 'Company Setup - Garmetix Admin' })

const config = useRuntimeConfig()
const toast  = useToast()

// ── Tabs ───────────────────────────────────────────────────────────────────
const activeTab = ref('companies')
const tabs = [
  { label: 'Companies',   icon: 'i-lucide-building-2',  value: 'companies', slot: 'companies' },
  { label: 'Store Groups',icon: 'i-lucide-folder-tree', value: 'groups',    slot: 'groups'    },
  { label: 'Stores',      icon: 'i-lucide-store',       value: 'stores',    slot: 'stores'    },
]

// ── State ──────────────────────────────────────────────────────────────────
const loading = ref(false)
const saving  = ref(false)

const companies = ref<any[]>([])
const groups    = ref<any[]>([])
const stores    = ref<any[]>([])

const showCompanyModal = ref(false)
const showGroupModal   = ref(false)
const showStoreModal   = ref(false)
const showDeleteModal  = ref(false)

const editingId    = ref<string | null>(null)
const deleteTarget = ref<any>(null)
const deleteType   = ref<'company' | 'group' | 'store'>('company')

// ── Enums ──────────────────────────────────────────────────────────────────
const companyTypeOptions = [
  'Proprietorship','Partnership','PrivateLimited','PublicLimited','LLP','Others'
].map(v => ({ label: v.replace(/([A-Z])/g, ' $1').trim(), value: v }))

const storeCategoryOptions = [
  'Cloths','Garments','Readymade','Furniture','FuelStation','General','Retail','Wholesale','Distributor','Others'
].map(v => ({ label: v, value: v }))

// ── Forms ──────────────────────────────────────────────────────────────────
const companyForm = reactive({
  name: '', code: '', gstin: '', pan: '', cin: '',
  companyType: 'Proprietorship', storeCategory: 'Retail',
  contactNumber: '', email: '', address: '', city: '', state: 'Maharashtra', country: 'India', zipCode: '',
  contactPerson: '', contactMobile: '',
  startDate: new Date().toISOString().split('T')[0],
  active: true
})

const groupForm = reactive({
  name: '', groupCode: '', companyId: null as any,
  storeCategory: 'Retail', startDate: new Date().toISOString().split('T')[0], active: true
})

const storeForm = reactive({
  name: '', storeCode: '', companyId: null as any, storeGroupId: null as any,
  storeCategory: 'Retail', contactNumber: '', email: '',
  address: '', city: '', state: 'Maharashtra', country: 'India', zipCode: '',
  startDate: new Date().toISOString().split('T')[0], active: true
})

// ── Computed ───────────────────────────────────────────────────────────────
const companyOptions = computed(() => companies.value.map(c => ({ label: c.name, value: c.id })))

const groupOptionsForCompany = computed(() => {
  const cId = storeForm.companyId?.value ?? storeForm.companyId
  return groups.value
    .filter(g => g.companyId === cId)
    .map(g => ({ label: g.name, value: g.id }))
})

const groupRows = computed(() => groups.value.map(g => ({
  id: g.id,
  name: g.name,
  groupCode: g.groupCode,
  company: companyName(g.companyId),
  storeCategory: g.storeCategory,
  active: g.active
})))

const storeRows = computed(() => stores.value.map(s => ({
  id: s.id,
  name: s.name,
  storeCode: s.storeCode,
  group: groupName(s.storeGroupId),
  company: companyName(s.companyId),
  city: s.city,
  contactNumber: s.contactNumber,
  active: s.active
})))

// ── Table columns ──────────────────────────────────────────────────────────
const companyColumns = [
  { accessorKey: 'name',          header: 'Name'         },
  { accessorKey: 'code',          header: 'Code'         },
  { accessorKey: 'gstin',         header: 'GSTIN'        },
  { accessorKey: 'city',          header: 'City'         },
  { accessorKey: 'contactNumber', header: 'Contact'      },
  { accessorKey: 'companyType',   header: 'Type'         },
  { accessorKey: 'active',        header: 'Status'       },
  { accessorKey: 'actions',       header: '', enableSorting: false },
]

const groupColumns = [
  { accessorKey: 'name',          header: 'Group Name'   },
  { accessorKey: 'groupCode',     header: 'Code'         },
  { accessorKey: 'company',       header: 'Company'      },
  { accessorKey: 'storeCategory', header: 'Category'     },
  { accessorKey: 'active',        header: 'Status'       },
  { accessorKey: 'actions',       header: '', enableSorting: false },
]

const storeColumns = [
  { accessorKey: 'name',          header: 'Store Name'   },
  { accessorKey: 'storeCode',     header: 'Code'         },
  { accessorKey: 'company',       header: 'Company'      },
  { accessorKey: 'group',         header: 'Group'        },
  { accessorKey: 'city',          header: 'City'         },
  { accessorKey: 'contactNumber', header: 'Contact'      },
  { accessorKey: 'active',        header: 'Status'       },
  { accessorKey: 'actions',       header: '', enableSorting: false },
]

// ── Helpers ────────────────────────────────────────────────────────────────
function getHeaders() {
  if (!import.meta.client) return {}
  const token = localStorage.getItem('garmetix.token')
  return token ? { Authorization: `Bearer ${token}` } : {}
}

function unwrap(data: any): any[] {
  if (!data) return []
  return Array.isArray(data) ? data : (data.$values ?? data.items ?? data.data ?? [])
}

function companyName(id: string) {
  return companies.value.find(c => c.id === id)?.name || '—'
}

function groupName(id: string) {
  return groups.value.find(g => g.id === id)?.name || '—'
}

function findGroup(id: string) {
  return groups.value.find(g => g.id === id)
}

function findStore(id: string) {
  return stores.value.find(s => s.id === id)
}

function todayStr() {
  return new Date().toISOString().split('T')[0]
}

// ── Fetch ──────────────────────────────────────────────────────────────────
async function fetchCompanies() {
  try { companies.value = unwrap(await $fetch(config.public.apiBaseUrl + '/companies', { headers: getHeaders() })) }
  catch (e) { console.error('fetchCompanies:', e) }
}

async function fetchGroups() {
  try { groups.value = unwrap(await $fetch(config.public.apiBaseUrl + '/store-groups', { headers: getHeaders() })) }
  catch (e) { console.error('fetchGroups:', e) }
}

async function fetchStores() {
  try { stores.value = unwrap(await $fetch(config.public.apiBaseUrl + '/stores', { headers: getHeaders() })) }
  catch (e) { console.error('fetchStores:', e) }
}

async function refresh() {
  loading.value = true
  await Promise.all([fetchCompanies(), fetchGroups(), fetchStores()])
  loading.value = false
}

// ── Modal open ─────────────────────────────────────────────────────────────
function openCreate(type: 'company' | 'group' | 'store') {
  editingId.value = null
  if (type === 'company') {
    Object.assign(companyForm, { name: '', code: '', gstin: '', pan: '', cin: '', companyType: 'Proprietorship', storeCategory: 'Retail', contactNumber: '', email: '', address: '', city: '', state: 'Maharashtra', country: 'India', zipCode: '', contactPerson: '', contactMobile: '', startDate: todayStr(), active: true })
    showCompanyModal.value = true
  } else if (type === 'group') {
    Object.assign(groupForm, { name: '', groupCode: '', companyId: null, storeCategory: 'Retail', startDate: todayStr(), active: true })
    showGroupModal.value = true
  } else {
    Object.assign(storeForm, { name: '', storeCode: '', companyId: null, storeGroupId: null, storeCategory: 'Retail', contactNumber: '', email: '', address: '', city: '', state: 'Maharashtra', country: 'India', zipCode: '', startDate: todayStr(), active: true })
    showStoreModal.value = true
  }
}

function openEdit(type: 'company' | 'group' | 'store', row: any) {
  if (!row) return
  editingId.value = row.id
  if (type === 'company') {
    Object.assign(companyForm, { name: row.name, code: row.code, gstin: row.gSTIN || row.gstin || '', pan: row.pan || '', cin: row.cIN || row.cin || '', companyType: row.companyType || 'Proprietorship', storeCategory: row.storeCategory || 'Retail', contactNumber: row.contactNumber || '', email: row.email || '', address: row.address || '', city: row.city || '', state: row.state || '', country: row.country || 'India', zipCode: row.zipCode || '', contactPerson: row.contactPerson || '', contactMobile: row.contactMobile || '', startDate: row.startDate ? row.startDate.split('T')[0] : todayStr(), active: row.active ?? true })
    showCompanyModal.value = true
  } else if (type === 'group') {
    Object.assign(groupForm, { name: row.name, groupCode: row.groupCode, companyId: row.companyId, storeCategory: row.storeCategory || 'Retail', startDate: row.startDate ? row.startDate.split('T')[0] : todayStr(), active: row.active ?? true })
    showGroupModal.value = true
  } else {
    Object.assign(storeForm, { name: row.name, storeCode: row.storeCode, companyId: row.companyId, storeGroupId: row.storeGroupId, storeCategory: row.storeCategory || 'Retail', contactNumber: row.contactNumber || '', email: row.email || '', address: row.address || '', city: row.city || '', state: row.state || '', country: row.country || 'India', zipCode: row.zipCode || '', startDate: row.startDate ? row.startDate.split('T')[0] : todayStr(), active: row.active ?? true })
    showStoreModal.value = true
  }
}

function confirmDelete(type: 'company' | 'group' | 'store', row: any) {
  deleteType.value   = type
  deleteTarget.value = row
  showDeleteModal.value = true
}

// ── CRUD ───────────────────────────────────────────────────────────────────
async function saveCompany() {
  if (!companyForm.name || !companyForm.code) {
    toast.add({ title: 'Validation', description: 'Name and Code are required.', color: 'warning' })
    return
  }
  saving.value = true
  try {
    const body = { ...companyForm, gSTIN: companyForm.gstin, cIN: companyForm.cin }
    const url = editingId.value
      ? `${config.public.apiBaseUrl}/companies/${editingId.value}`
      : `${config.public.apiBaseUrl}/companies`
    await $fetch(url, { method: editingId.value ? 'PUT' : 'POST', headers: getHeaders(), body })
    toast.add({ title: editingId.value ? 'Updated' : 'Created', description: `Company ${companyForm.name} saved.`, color: 'success' })
    showCompanyModal.value = false
    await fetchCompanies()
  } catch (e: any) {
    toast.add({ title: 'Error', description: e.data?.message || 'Failed to save.', color: 'error' })
  } finally { saving.value = false }
}

async function saveGroup() {
  const companyId = groupForm.companyId?.value ?? groupForm.companyId
  if (!groupForm.name || !groupForm.groupCode || !companyId) {
    toast.add({ title: 'Validation', description: 'Name, Code and Company are required.', color: 'warning' })
    return
  }
  saving.value = true
  try {
    const body = { ...groupForm, companyId }
    const url = editingId.value
      ? `${config.public.apiBaseUrl}/store-groups/${editingId.value}`
      : `${config.public.apiBaseUrl}/store-groups`
    await $fetch(url, { method: editingId.value ? 'PUT' : 'POST', headers: getHeaders(), body })
    toast.add({ title: 'Saved', description: `Store Group ${groupForm.name} saved.`, color: 'success' })
    showGroupModal.value = false
    await fetchGroups()
  } catch (e: any) {
    toast.add({ title: 'Error', description: e.data?.message || 'Failed to save.', color: 'error' })
  } finally { saving.value = false }
}

async function saveStore() {
  const companyId    = storeForm.companyId?.value    ?? storeForm.companyId
  const storeGroupId = storeForm.storeGroupId?.value ?? storeForm.storeGroupId
  if (!storeForm.name || !storeForm.storeCode || !companyId || !storeGroupId) {
    toast.add({ title: 'Validation', description: 'Name, Code, Company and Group are required.', color: 'warning' })
    return
  }
  saving.value = true
  try {
    const body = { ...storeForm, companyId, storeGroupId }
    const url = editingId.value
      ? `${config.public.apiBaseUrl}/stores/${editingId.value}`
      : `${config.public.apiBaseUrl}/stores`
    await $fetch(url, { method: editingId.value ? 'PUT' : 'POST', headers: getHeaders(), body })
    toast.add({ title: 'Saved', description: `Store ${storeForm.name} saved.`, color: 'success' })
    showStoreModal.value = false
    await fetchStores()
  } catch (e: any) {
    toast.add({ title: 'Error', description: e.data?.message || 'Failed to save.', color: 'error' })
  } finally { saving.value = false }
}

async function executeDelete() {
  saving.value = true
  const urlMap = { company: 'companies', group: 'store-groups', store: 'stores' }
  try {
    await $fetch(`${config.public.apiBaseUrl}/${urlMap[deleteType.value]}/${deleteTarget.value.id}`, {
      method: 'DELETE', headers: getHeaders()
    })
    toast.add({ title: 'Deleted', description: `${deleteTarget.value.name} deleted.`, color: 'success' })
    showDeleteModal.value = false
    if (deleteType.value === 'company')     await fetchCompanies()
    else if (deleteType.value === 'group')  await fetchGroups()
    else                                    await fetchStores()
  } catch (e: any) {
    toast.add({ title: 'Error', description: e.data?.message || 'Delete failed.', color: 'error' })
  } finally { saving.value = false }
}

onMounted(refresh)
</script>
