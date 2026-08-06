<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-dashboard-kicker">
            <UIcon name="i-lucide-building-2" class="size-4" />
            Company module
          </p>
          <h2 class="garmetix-dashboard-title">Company, Group And Store</h2>
          <p class="garmetix-dashboard-subtitle">Create, edit and delete company, store group and store masters.</p>
        </div>
        <div class="flex items-center gap-2">
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
          <UButton v-if="activeTab === 'companies'" icon="i-lucide-plus" color="primary" @click="openCreate('company')">Add Company</UButton>
          <UButton v-if="activeTab === 'groups'" icon="i-lucide-plus" color="primary" @click="openCreate('group')">Add Store Group</UButton>
          <UButton v-if="activeTab === 'stores'" icon="i-lucide-plus" color="primary" @click="openCreate('store')">Add Store</UButton>
        </div>
      </div>
    </div>

    <section class="garmetix-section-card">
      <div class="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h3 class="garmetix-panel-title">GST API Configuration</h3>
          <p class="text-xs text-muted">Configure GSTIN/HSN/e-invoice/e-way bill API providers and encrypted credentials from the GST & Taxes module - no environment variables needed.</p>
        </div>
        <UButton icon="i-lucide-plug-zap" color="primary" variant="soft" :to="gstApiSetupUrl" target="_blank">Configure GST API</UButton>
      </div>
    </section>

    <UAlert v-if="error" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />

    <section class="grid gap-3 md:grid-cols-3">
      <div v-for="card in cards" :key="card.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label">{{ card.label }}</p>
        <p class="garmetix-metric-value">{{ card.value }}</p>
        <p class="garmetix-metric-caption">{{ card.detail }}</p>
      </div>
    </section>

    <div class="flex flex-wrap items-center justify-between gap-3">
      <UTabs v-model="activeTab" :items="tabs" class="w-full max-w-md" />
      <UInput v-model="search" icon="i-lucide-search" placeholder="Search setup" class="w-full sm:w-64" />
    </div>

    <section class="garmetix-section-card overflow-hidden p-0">
      <UTable v-if="activeTab === 'companies'" :data="filteredCompanyRows" :columns="companyColumns" :loading="loading" class="w-full">
        <template #active-cell="{ row }">
          <UBadge variant="subtle" :color="row.original.active ? 'success' : 'neutral'">{{ row.original.active ? 'Active' : 'Inactive' }}</UBadge>
        </template>
        <template #actions-cell="{ row }">
          <div class="flex items-center justify-end gap-1">
            <UButton icon="i-lucide-pencil" color="primary" variant="ghost" size="sm" @click="openEdit('company', row.original)" />
            <UButton icon="i-lucide-trash-2" color="error" variant="ghost" size="sm" @click="askDelete('company', row.original)" />
          </div>
        </template>
      </UTable>

      <UTable v-else-if="activeTab === 'groups'" :data="filteredGroupRows" :columns="groupColumns" :loading="loading" class="w-full">
        <template #active-cell="{ row }">
          <UBadge variant="subtle" :color="row.original.active ? 'success' : 'neutral'">{{ row.original.active ? 'Active' : 'Inactive' }}</UBadge>
        </template>
        <template #actions-cell="{ row }">
          <div class="flex items-center justify-end gap-1">
            <UButton icon="i-lucide-pencil" color="primary" variant="ghost" size="sm" @click="openEdit('group', findGroup(row.original.id))" />
            <UButton icon="i-lucide-trash-2" color="error" variant="ghost" size="sm" @click="askDelete('group', findGroup(row.original.id))" />
          </div>
        </template>
      </UTable>

      <UTable v-else :data="filteredStoreRows" :columns="storeColumns" :loading="loading" class="w-full">
        <template #active-cell="{ row }">
          <UBadge variant="subtle" :color="row.original.active ? 'success' : 'neutral'">{{ row.original.active ? 'Active' : 'Inactive' }}</UBadge>
        </template>
        <template #actions-cell="{ row }">
          <div class="flex items-center justify-end gap-1">
            <UButton icon="i-lucide-pencil" color="primary" variant="ghost" size="sm" @click="openEdit('store', findStore(row.original.id))" />
            <UButton icon="i-lucide-trash-2" color="error" variant="ghost" size="sm" @click="askDelete('store', findStore(row.original.id))" />
          </div>
        </template>
      </UTable>
    </section>

    <!-- Slideover: Add/Edit Company -->
    <USlideover v-model:open="companyModalOpen" :title="editingId ? 'Edit Company' : 'Add Company'" :description="editingId ? 'Update company master details' : 'Create a new company master'">
      <template #body>
        <UForm :state="companyForm" class="space-y-4" @submit="saveCompany">
          <div class="grid grid-cols-2 gap-4">
            <UFormField label="Company Name" name="name" required>
              <UInput v-model="companyForm.name" placeholder="ABC Enterprises" class="w-full" />
            </UFormField>
            <UFormField label="Company Code" name="code" required>
              <UInput v-model="companyForm.code" placeholder="ABC001" class="w-full" />
            </UFormField>
          </div>
          <div class="grid grid-cols-2 gap-4">
            <UFormField label="Company Type" name="companyType">
              <USelectMenu v-model="companyForm.companyType" :items="companyTypeOptions" value-key="value" class="w-full" />
            </UFormField>
            <UFormField label="Store Category" name="storeCategory">
              <USelectMenu v-model="companyForm.storeCategory" :items="storeCategoryOptions" value-key="value" class="w-full" />
            </UFormField>
          </div>
          <div class="grid grid-cols-2 gap-4">
            <UFormField label="GSTIN" name="gstin">
              <UInput v-model="companyForm.gstin" placeholder="27AAPFU0939F1ZV" class="w-full" />
            </UFormField>
            <UFormField label="PAN" name="pan">
              <UInput v-model="companyForm.pan" placeholder="AAPFU0939F" class="w-full" />
            </UFormField>
          </div>
          <div class="grid grid-cols-2 gap-4">
            <UFormField label="Contact Number" name="contactNumber">
              <UInput v-model="companyForm.contactNumber" placeholder="+91 9876543210" class="w-full" />
            </UFormField>
            <UFormField label="Email" name="email">
              <UInput v-model="companyForm.email" type="email" placeholder="info@company.com" class="w-full" />
            </UFormField>
          </div>
          <div class="grid grid-cols-2 gap-4">
            <UFormField label="Contact Person" name="contactPerson">
              <UInput v-model="companyForm.contactPerson" placeholder="Owner name" class="w-full" />
            </UFormField>
            <UFormField label="Contact Mobile" name="contactMobile">
              <UInput v-model="companyForm.contactMobile" placeholder="+91 9876543210" class="w-full" />
            </UFormField>
          </div>
          <UFormField label="Address" name="address">
            <UTextarea v-model="companyForm.address" placeholder="Street address..." class="w-full" />
          </UFormField>
          <div class="grid grid-cols-3 gap-4">
            <UFormField label="City" name="city">
              <UInput v-model="companyForm.city" class="w-full" />
            </UFormField>
            <UFormField label="State" name="state">
              <UInput v-model="companyForm.state" class="w-full" />
            </UFormField>
            <UFormField label="Zip Code" name="zipCode">
              <UInput v-model="companyForm.zipCode" class="w-full" />
            </UFormField>
          </div>
          <div class="grid grid-cols-2 gap-4">
            <UFormField label="Start Date" name="startDate">
              <UInput v-model="companyForm.startDate" type="date" class="w-full" />
            </UFormField>
            <UFormField label="CIN" name="cin">
              <UInput v-model="companyForm.cin" placeholder="U74999DL2000PTC104430" class="w-full" />
            </UFormField>
          </div>
          <UCheckbox v-model="companyForm.active" label="Company is Active" />
          <div class="mt-6 flex justify-end gap-3">
            <UButton color="neutral" variant="ghost" label="Cancel" @click="companyModalOpen = false" />
            <UButton type="submit" color="primary" :loading="saving" :label="editingId ? 'Update' : 'Save Company'" />
          </div>
        </UForm>
      </template>
    </USlideover>

    <!-- Slideover: Add/Edit Store Group -->
    <USlideover v-model:open="groupModalOpen" :title="editingId ? 'Edit Store Group' : 'Add Store Group'" :description="editingId ? 'Update store group master details' : 'Create a new store group master'">
      <template #body>
        <UForm :state="groupForm" class="space-y-4" @submit="saveGroup">
          <UFormField label="Company" name="companyId" required>
            <USelectMenu v-model="groupForm.companyId" :items="companyOptions" value-key="value" placeholder="Choose company..." class="w-full" />
          </UFormField>
          <div class="grid grid-cols-2 gap-4">
            <UFormField label="Group Name" name="name" required>
              <UInput v-model="groupForm.name" placeholder="Main Branch Group" class="w-full" />
            </UFormField>
            <UFormField label="Group Code" name="groupCode" required>
              <UInput v-model="groupForm.groupCode" placeholder="GRP001" class="w-full" />
            </UFormField>
          </div>
          <UFormField label="Store Category" name="storeCategory">
            <USelectMenu v-model="groupForm.storeCategory" :items="storeCategoryOptions" value-key="value" class="w-full" />
          </UFormField>
          <UFormField label="Start Date" name="startDate">
            <UInput v-model="groupForm.startDate" type="date" class="w-full" />
          </UFormField>
          <UCheckbox v-model="groupForm.active" label="Store Group is Active" />
          <div class="mt-6 flex justify-end gap-3">
            <UButton color="neutral" variant="ghost" label="Cancel" @click="groupModalOpen = false" />
            <UButton type="submit" color="primary" :loading="saving" :label="editingId ? 'Update' : 'Save Store Group'" />
          </div>
        </UForm>
      </template>
    </USlideover>

    <!-- Slideover: Add/Edit Store -->
    <USlideover v-model:open="storeModalOpen" :title="editingId ? 'Edit Store' : 'Add Store'" :description="editingId ? 'Update store master details' : 'Create a new store master'">
      <template #body>
        <UForm :state="storeForm" class="space-y-4" @submit="saveStore">
          <div class="grid grid-cols-2 gap-4">
            <UFormField label="Company" name="companyId" required>
              <USelectMenu v-model="storeForm.companyId" :items="companyOptions" value-key="value" placeholder="Company..." class="w-full" @update:model-value="storeForm.storeGroupId = null" />
            </UFormField>
            <UFormField label="Store Group" name="storeGroupId" required>
              <USelectMenu v-model="storeForm.storeGroupId" :items="groupOptionsForCompany" value-key="value" placeholder="Group..." class="w-full" />
            </UFormField>
          </div>
          <div class="grid grid-cols-2 gap-4">
            <UFormField label="Store Name" name="name" required>
              <UInput v-model="storeForm.name" placeholder="Main Store" class="w-full" />
            </UFormField>
            <UFormField label="Store Code" name="storeCode" required>
              <UInput v-model="storeForm.storeCode" placeholder="STR001" class="w-full" />
            </UFormField>
          </div>
          <UFormField label="Store Category" name="storeCategory">
            <USelectMenu v-model="storeForm.storeCategory" :items="storeCategoryOptions" value-key="value" class="w-full" />
          </UFormField>
          <div class="grid grid-cols-2 gap-4">
            <UFormField label="Contact Number" name="contactNumber">
              <UInput v-model="storeForm.contactNumber" placeholder="+91 9876543210" class="w-full" />
            </UFormField>
            <UFormField label="Email" name="email">
              <UInput v-model="storeForm.email" type="email" class="w-full" />
            </UFormField>
          </div>
          <UFormField label="Address" name="address">
            <UTextarea v-model="storeForm.address" class="w-full" />
          </UFormField>
          <div class="grid grid-cols-3 gap-4">
            <UFormField label="City" name="city">
              <UInput v-model="storeForm.city" class="w-full" />
            </UFormField>
            <UFormField label="State" name="state">
              <UInput v-model="storeForm.state" class="w-full" />
            </UFormField>
            <UFormField label="Zip Code" name="zipCode">
              <UInput v-model="storeForm.zipCode" class="w-full" />
            </UFormField>
          </div>
          <UFormField label="Start Date" name="startDate">
            <UInput v-model="storeForm.startDate" type="date" class="w-full" />
          </UFormField>
          <UCheckbox v-model="storeForm.active" label="Store is Active" />
          <div class="mt-6 flex justify-end gap-3">
            <UButton color="neutral" variant="ghost" label="Cancel" @click="storeModalOpen = false" />
            <UButton type="submit" color="primary" :loading="saving" :label="editingId ? 'Update' : 'Save Store'" />
          </div>
        </UForm>
      </template>
    </USlideover>

    <!-- Modal: Confirm Delete -->
    <UModal v-model:open="deleteModalOpen" title="Confirm Delete" :description="`Are you sure you want to delete '${deleteTarget?.name ?? ''}'? This action cannot be undone.`">
      <template #body>
        <div class="flex justify-end gap-3">
          <UButton color="neutral" variant="ghost" label="Cancel" @click="deleteModalOpen = false" />
          <UButton color="error" :loading="saving" label="Delete" @click="executeDelete" />
        </div>
      </template>
    </UModal>
  </section>
</template>

<script setup lang="ts">
import { reactive, ref, computed, onMounted } from 'vue'
import { readText, toRows, type ApiRecord, useAdminApiClient } from '../utils/admin-api'

useHead({ title: 'Company - Garmetix Admin' })

type SetupTab = 'companies' | 'groups' | 'stores'

const toast = useToast()
const { get, post, put, remove } = useAdminApiClient()
const runtimeConfig = useRuntimeConfig()
const gstApiSetupUrl = computed(() => {
  const appUrls = runtimeConfig.public.appUrls as Record<string, string | undefined>
  const booksUrl = String(appUrls?.NUXT_PUBLIC_GARMETIX_BOOKS_URL || '').replace(/\/$/, '')
  return booksUrl ? `${booksUrl}/gst-tax-setup` : '/gst-tax-setup'
})

const loading = ref(true)
const saving = ref(false)
const error = ref('')
const search = ref('')
const activeTab = ref<SetupTab>('companies')

const companies = ref<ApiRecord[]>([])
const groups = ref<ApiRecord[]>([])
const stores = ref<ApiRecord[]>([])

const tabs = [
  { label: 'Companies', icon: 'i-lucide-building-2', value: 'companies' },
  { label: 'Store Groups', icon: 'i-lucide-folder-tree', value: 'groups' },
  { label: 'Stores', icon: 'i-lucide-store', value: 'stores' }
]

// Matches backend Garmetix.Core.Enums.CompanyType declaration order.
const companyTypeOptions = [
  { value: 0, label: 'Proprietorship' },
  { value: 1, label: 'Partnership' },
  { value: 2, label: 'Private Limited' },
  { value: 3, label: 'Public Limited' },
  { value: 4, label: 'LLP' },
  { value: 5, label: 'Others' }
]

// Matches backend Garmetix.Core.Enums.StoreCategory declaration order.
const storeCategoryOptions = [
  { value: 0, label: 'Cloths' },
  { value: 1, label: 'Garments' },
  { value: 2, label: 'Readymade' },
  { value: 3, label: 'Furniture' },
  { value: 4, label: 'Fuel Station' },
  { value: 5, label: 'General' },
  { value: 6, label: 'Retail' },
  { value: 7, label: 'Wholesale' },
  { value: 8, label: 'Distributor' },
  { value: 9, label: 'Others' }
]

function todayStr() {
  return new Date().toISOString().split('T')[0]
}

const companyModalOpen = ref(false)
const groupModalOpen = ref(false)
const storeModalOpen = ref(false)
const deleteModalOpen = ref(false)

const editingId = ref<string | null>(null)
const deleteTarget = ref<ApiRecord | null>(null)
const deleteType = ref<'company' | 'group' | 'store'>('company')

const companyForm = reactive({
  name: '', code: '', gstin: '', pan: '', cin: '',
  companyType: 0, storeCategory: 6,
  contactNumber: '', email: '', address: '', city: 'Dumka', state: 'Jharkhand', country: 'India', zipCode: '814101',
  contactPerson: '', contactMobile: '',
  startDate: todayStr(), active: true
})

const groupForm = reactive({
  name: '', groupCode: '', companyId: null as string | { value: string } | null,
  storeCategory: 6, startDate: todayStr(), active: true
})

const storeForm = reactive({
  name: '', storeCode: '', companyId: null as string | { value: string } | null, storeGroupId: null as string | { value: string } | null,
  storeCategory: 6, contactNumber: '', email: '',
  address: '', city: 'Dumka', state: 'Jharkhand', country: 'India', zipCode: '814101',
  startDate: todayStr(), active: true
})

const companyOptions = computed(() => companies.value.map(c => ({ value: String(c.id), label: readText(c, ['name'], 'Company') })))

function unwrapId(value: unknown) {
  return (value as { value?: string })?.value ?? (value as string | null)
}

const groupOptionsForCompany = computed(() => {
  const companyId = unwrapId(storeForm.companyId)
  return groups.value
    .filter(g => !companyId || g.companyId === companyId)
    .map(g => ({ value: String(g.id), label: readText(g, ['name'], 'Store group') }))
})

function companyName(id: unknown) {
  return readText(companies.value.find(c => c.id === id), ['name'])
}

function groupName(id: unknown) {
  return readText(groups.value.find(g => g.id === id), ['name'])
}

function findGroup(id: string) {
  return groups.value.find(g => g.id === id) ?? null
}

function findStore(id: string) {
  return stores.value.find(s => s.id === id) ?? null
}

const companyRows = computed(() => companies.value.map(c => ({
  id: c.id,
  name: readText(c, ['name']),
  code: readText(c, ['code']),
  gstin: readText(c, ['gSTIN', 'gstin']),
  city: readText(c, ['city']),
  contactNumber: readText(c, ['contactNumber']),
  active: Boolean(c.active)
})))

const groupRows = computed(() => groups.value.map(g => ({
  id: g.id,
  name: readText(g, ['name']),
  groupCode: readText(g, ['groupCode']),
  company: companyName(g.companyId),
  city: readText(g, ['city']),
  active: Boolean(g.active)
})))

const storeRows = computed(() => stores.value.map(s => ({
  id: s.id,
  name: readText(s, ['name']),
  storeCode: readText(s, ['storeCode']),
  group: groupName(s.storeGroupId),
  company: companyName(s.companyId),
  city: readText(s, ['city']),
  contactNumber: readText(s, ['contactNumber']),
  active: Boolean(s.active)
})))

function matchesSearch(row: ApiRecord) {
  const term = search.value.trim().toLowerCase()
  if (!term) return true
  return JSON.stringify(row).toLowerCase().includes(term)
}

const filteredCompanyRows = computed(() => companyRows.value.filter(matchesSearch))
const filteredGroupRows = computed(() => groupRows.value.filter(matchesSearch))
const filteredStoreRows = computed(() => storeRows.value.filter(matchesSearch))

const cards = computed(() => [
  { label: 'Companies', value: companies.value.length, detail: 'Registered company masters' },
  { label: 'Store Groups', value: groups.value.length, detail: 'Group masters' },
  { label: 'Stores', value: stores.value.length, detail: 'Store masters' }
])

const companyColumns = [
  { accessorKey: 'name', header: 'Company' },
  { accessorKey: 'code', header: 'Code' },
  { accessorKey: 'gstin', header: 'GSTIN' },
  { accessorKey: 'city', header: 'City' },
  { accessorKey: 'contactNumber', header: 'Contact' },
  { accessorKey: 'active', header: 'Status' },
  { accessorKey: 'actions', header: '', enableSorting: false }
]

const groupColumns = [
  { accessorKey: 'name', header: 'Group' },
  { accessorKey: 'groupCode', header: 'Code' },
  { accessorKey: 'company', header: 'Company' },
  { accessorKey: 'city', header: 'City' },
  { accessorKey: 'active', header: 'Status' },
  { accessorKey: 'actions', header: '', enableSorting: false }
]

const storeColumns = [
  { accessorKey: 'name', header: 'Store' },
  { accessorKey: 'storeCode', header: 'Code' },
  { accessorKey: 'group', header: 'Group' },
  { accessorKey: 'city', header: 'City' },
  { accessorKey: 'contactNumber', header: 'Phone' },
  { accessorKey: 'active', header: 'Status' },
  { accessorKey: 'actions', header: '', enableSorting: false }
]

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const [companyData, groupData, storeData] = await Promise.allSettled([
      get<unknown>('companies'),
      get<unknown>('store-groups'),
      get<unknown>('stores')
    ])
    if (companyData.status === 'fulfilled') companies.value = toRows(companyData.value)
    if (groupData.status === 'fulfilled') groups.value = toRows(groupData.value)
    if (storeData.status === 'fulfilled') stores.value = toRows(storeData.value)
    const failed = [companyData, groupData, storeData].filter(item => item.status === 'rejected').length
    if (failed) error.value = `${failed} setup request(s) could not be loaded.`
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load setup.'
  } finally {
    loading.value = false
  }
}

function openCreate(type: 'company' | 'group' | 'store') {
  editingId.value = null
  if (type === 'company') {
    Object.assign(companyForm, { name: '', code: '', gstin: '', pan: '', cin: '', companyType: 0, storeCategory: 6, contactNumber: '', email: '', address: '', city: 'Dumka', state: 'Jharkhand', country: 'India', zipCode: '814101', contactPerson: '', contactMobile: '', startDate: todayStr(), active: true })
    companyModalOpen.value = true
  } else if (type === 'group') {
    Object.assign(groupForm, { name: '', groupCode: '', companyId: null, storeCategory: 6, startDate: todayStr(), active: true })
    groupModalOpen.value = true
  } else {
    Object.assign(storeForm, { name: '', storeCode: '', companyId: null, storeGroupId: null, storeCategory: 6, contactNumber: '', email: '', address: '', city: 'Dumka', state: 'Jharkhand', country: 'India', zipCode: '814101', startDate: todayStr(), active: true })
    storeModalOpen.value = true
  }
}

function toDateInput(value: unknown) {
  const raw = String(value ?? '')
  return raw ? raw.split('T')[0] : todayStr()
}

function openEdit(type: 'company' | 'group' | 'store', row: ApiRecord | null) {
  if (!row) return
  editingId.value = String(row.id)
  if (type === 'company') {
    Object.assign(companyForm, {
      name: row.name ?? '', code: row.code ?? '', gstin: row.gSTIN ?? '', pan: row.pan ?? '', cin: row.cIN ?? '',
      companyType: Number(row.companyType ?? 0), storeCategory: Number(row.storeCategory ?? 6),
      contactNumber: row.contactNumber ?? '', email: row.email ?? '', address: row.address ?? '',
      city: row.city ?? '', state: row.state ?? '', country: row.country ?? 'India', zipCode: row.zipCode ?? '',
      contactPerson: row.contactPerson ?? '', contactMobile: row.contactMobile ?? '',
      startDate: toDateInput(row.startDate), active: Boolean(row.active)
    })
    companyModalOpen.value = true
  } else if (type === 'group') {
    Object.assign(groupForm, {
      name: row.name ?? '', groupCode: row.groupCode ?? '', companyId: row.companyId ? String(row.companyId) : null,
      storeCategory: Number(row.storeCategory ?? 6), startDate: toDateInput(row.startDate), active: Boolean(row.active)
    })
    groupModalOpen.value = true
  } else {
    Object.assign(storeForm, {
      name: row.name ?? '', storeCode: row.storeCode ?? '',
      companyId: row.companyId ? String(row.companyId) : null, storeGroupId: row.storeGroupId ? String(row.storeGroupId) : null,
      storeCategory: Number(row.storeCategory ?? 6), contactNumber: row.contactNumber ?? '', email: row.email ?? '',
      address: row.address ?? '', city: row.city ?? '', state: row.state ?? '', country: row.country ?? 'India', zipCode: row.zipCode ?? '',
      startDate: toDateInput(row.startDate), active: Boolean(row.active)
    })
    storeModalOpen.value = true
  }
}

function askDelete(type: 'company' | 'group' | 'store', row: ApiRecord | null) {
  if (!row) return
  deleteType.value = type
  deleteTarget.value = row
  deleteModalOpen.value = true
}

async function saveCompany() {
  if (!companyForm.name || !companyForm.code) {
    toast.add({ title: 'Validation', description: 'Company Name and Code are required.', color: 'warning' })
    return
  }
  saving.value = true
  try {
    const body = {
      id: editingId.value ?? undefined,
      name: companyForm.name,
      code: companyForm.code,
      gSTIN: companyForm.gstin,
      pan: companyForm.pan,
      cIN: companyForm.cin,
      companyType: Number(unwrapId(companyForm.companyType) ?? companyForm.companyType),
      storeCategory: Number(unwrapId(companyForm.storeCategory) ?? companyForm.storeCategory),
      contactNumber: companyForm.contactNumber,
      email: companyForm.email,
      address: companyForm.address,
      city: companyForm.city,
      state: companyForm.state,
      country: companyForm.country,
      zipCode: companyForm.zipCode,
      contactPerson: companyForm.contactPerson,
      contactMobile: companyForm.contactMobile,
      startDate: companyForm.startDate,
      active: companyForm.active
    }
    if (editingId.value) {
      await put(`companies/${editingId.value}`, body)
      toast.add({ title: 'Updated', description: `Company ${companyForm.name} updated.`, color: 'success' })
    } else {
      await post('companies', body)
      toast.add({ title: 'Created', description: `Company ${companyForm.name} created.`, color: 'success' })
    }
    companyModalOpen.value = false
    await refresh()
  } catch (caught) {
    toast.add({ title: 'Error', description: caught instanceof Error ? caught.message : 'Failed to save company.', color: 'error' })
  } finally {
    saving.value = false
  }
}

async function saveGroup() {
  const companyId = unwrapId(groupForm.companyId)
  if (!groupForm.name || !groupForm.groupCode || !companyId) {
    toast.add({ title: 'Validation', description: 'Name, Code and Company are required.', color: 'warning' })
    return
  }
  saving.value = true
  try {
    const body = {
      id: editingId.value ?? undefined,
      name: groupForm.name,
      groupCode: groupForm.groupCode,
      companyId,
      storeCategory: Number(unwrapId(groupForm.storeCategory) ?? groupForm.storeCategory),
      startDate: groupForm.startDate,
      active: groupForm.active
    }
    if (editingId.value) {
      await put(`store-groups/${editingId.value}`, body)
      toast.add({ title: 'Updated', description: `Store Group ${groupForm.name} updated.`, color: 'success' })
    } else {
      await post('store-groups', body)
      toast.add({ title: 'Created', description: `Store Group ${groupForm.name} created.`, color: 'success' })
    }
    groupModalOpen.value = false
    await refresh()
  } catch (caught) {
    toast.add({ title: 'Error', description: caught instanceof Error ? caught.message : 'Failed to save store group.', color: 'error' })
  } finally {
    saving.value = false
  }
}

async function saveStore() {
  const companyId = unwrapId(storeForm.companyId)
  const storeGroupId = unwrapId(storeForm.storeGroupId)
  if (!storeForm.name || !storeForm.storeCode || !companyId || !storeGroupId) {
    toast.add({ title: 'Validation', description: 'Name, Code, Company and Store Group are required.', color: 'warning' })
    return
  }
  saving.value = true
  try {
    const body = {
      id: editingId.value ?? undefined,
      name: storeForm.name,
      storeCode: storeForm.storeCode,
      companyId,
      storeGroupId,
      storeCategory: Number(unwrapId(storeForm.storeCategory) ?? storeForm.storeCategory),
      contactNumber: storeForm.contactNumber,
      email: storeForm.email,
      address: storeForm.address,
      city: storeForm.city,
      state: storeForm.state,
      country: storeForm.country,
      zipCode: storeForm.zipCode,
      startDate: storeForm.startDate,
      active: storeForm.active
    }
    if (editingId.value) {
      await put(`stores/${editingId.value}`, body)
      toast.add({ title: 'Updated', description: `Store ${storeForm.name} updated.`, color: 'success' })
    } else {
      await post('stores', body)
      toast.add({ title: 'Created', description: `Store ${storeForm.name} created.`, color: 'success' })
    }
    storeModalOpen.value = false
    await refresh()
  } catch (caught) {
    toast.add({ title: 'Error', description: caught instanceof Error ? caught.message : 'Failed to save store.', color: 'error' })
  } finally {
    saving.value = false
  }
}

async function executeDelete() {
  if (!deleteTarget.value) return
  saving.value = true
  try {
    const urlMap = { company: 'companies', group: 'store-groups', store: 'stores' } as const
    await remove(`${urlMap[deleteType.value]}/${deleteTarget.value.id}`)
    toast.add({ title: 'Deleted', description: `${readText(deleteTarget.value, ['name'])} deleted.`, color: 'success' })
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
