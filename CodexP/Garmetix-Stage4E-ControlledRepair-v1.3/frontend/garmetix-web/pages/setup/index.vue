<script setup lang="ts">
import { h, resolveComponent } from 'vue'
import type { TableColumn } from '@nuxt/ui'

type SetupTab = 'companies' | 'groups' | 'stores'

const api = useGarmetixApi()
const auth = useAuth()
const feedback = useUiFeedback()
const isAuthenticated = auth.isAuthenticated
const canEdit = auth.canEdit
const canDelete = auth.canDelete

const UBadge = resolveComponent('UBadge')
const UButton = resolveComponent('UButton')

const companies = ref<any[]>([])
const storeGroups = ref<any[]>([])
const stores = ref<any[]>([])
const loading = ref(false)
const saving = ref(false)
const deleting = ref(false)
const activeTab = ref<SetupTab>('companies')
const search = ref('')
const formOpen = ref(false)
const editingId = ref('')
const deleteOpen = ref(false)
const pendingDelete = ref<any | null>(null)

const tabs = [
  { key: 'companies' as const, label: 'Companies', icon: 'i-lucide-building-2' },
  { key: 'groups' as const, label: 'Store Groups', icon: 'i-lucide-network' },
  { key: 'stores' as const, label: 'Stores', icon: 'i-lucide-store' }
]

const storeCategoryOptions = [
  { value: 0, label: 'Cloths' },
  { value: 1, label: 'Garments' },
  { value: 2, label: 'Readymade' },
  { value: 5, label: 'General' },
  { value: 6, label: 'Retail' },
  { value: 7, label: 'Wholesale' },
  { value: 8, label: 'Distributor' },
  { value: 9, label: 'Others' }
]

const companyTypeOptions = [
  { value: 0, label: 'Proprietorship' },
  { value: 1, label: 'Partnership' },
  { value: 2, label: 'Private Limited' },
  { value: 3, label: 'Public Limited' },
  { value: 4, label: 'LLP' },
  { value: 5, label: 'Others' }
]

const companyOptions = computed(() => companies.value.map((company) => ({
  value: company.id,
  label: company.name || 'Company'
})))

const groupOptions = computed(() => storeGroups.value.map((group) => ({
  value: group.id,
  label: group.name || 'Store group'
})))

const companyForm = reactive<any>(emptyCompany())
const groupForm = reactive<any>(emptyStoreGroup())
const storeForm = reactive<any>(emptyStore())

const activeLabel = computed(() => tabs.find((tab) => tab.key === activeTab.value)?.label || 'Company')
const activeCount = computed(() => activeRows.value.length)
const isEditing = computed(() => Boolean(editingId.value))
const formTitle = computed(() => `${isEditing.value ? 'Edit' : 'New'} ${singularLabel(activeTab.value)}`)

const activeRows = computed(() => {
  if (activeTab.value === 'companies') {
    return companies.value
  }

  if (activeTab.value === 'groups') {
    return storeGroups.value
  }

  return stores.value
})

const filteredRows = computed(() => {
  const term = search.value.trim().toLowerCase()
  if (!term) {
    return tableRows.value
  }

  return tableRows.value.filter((row) => JSON.stringify(row).toLowerCase().includes(term))
})

const tableRows = computed(() => activeRows.value.map((item) => {
  if (activeTab.value === 'companies') {
    return {
      id: item.id,
      name: item.name,
      code: item.code || '-',
      contact: item.contactNumber || item.email || '-',
      location: [item.city, item.state].filter(Boolean).join(', ') || '-',
      type: companyTypeLabel(item.companyType),
      status: item.active ? 'Active' : 'Inactive',
      raw: item
    }
  }

  if (activeTab.value === 'groups') {
    return {
      id: item.id,
      name: item.name,
      code: item.groupCode || '-',
      company: companyName(item.companyId),
      category: categoryLabel(item.storeCategory),
      status: item.active ? 'Active' : 'Inactive',
      raw: item
    }
  }

  return {
    id: item.id,
    name: item.name,
    code: item.storeCode || '-',
    company: companyName(item.companyId),
    group: groupName(item.storeGroupId),
    location: [item.city, item.state].filter(Boolean).join(', ') || '-',
    status: item.active ? 'Active' : 'Inactive',
    raw: item
  }
}))

const companyColumns: TableColumn<any>[] = [
  { accessorKey: 'name', header: 'Company' },
  { accessorKey: 'code', header: 'Code' },
  { accessorKey: 'contact', header: 'Contact' },
  { accessorKey: 'location', header: 'Location' },
  { accessorKey: 'type', header: 'Type' },
  statusColumn(),
  actionColumn()
]

const groupColumns: TableColumn<any>[] = [
  { accessorKey: 'name', header: 'Store Group' },
  { accessorKey: 'code', header: 'Code' },
  { accessorKey: 'company', header: 'Company' },
  { accessorKey: 'category', header: 'Category' },
  statusColumn(),
  actionColumn()
]

const storeColumns: TableColumn<any>[] = [
  { accessorKey: 'name', header: 'Store' },
  { accessorKey: 'code', header: 'Code' },
  { accessorKey: 'company', header: 'Company' },
  { accessorKey: 'group', header: 'Group' },
  { accessorKey: 'location', header: 'Location' },
  statusColumn(),
  actionColumn()
]

const activeColumns = computed(() => {
  if (activeTab.value === 'companies') {
    return companyColumns
  }

  if (activeTab.value === 'groups') {
    return groupColumns
  }

  return storeColumns
})

function statusColumn(): TableColumn<any> {
  return {
    accessorKey: 'status',
    header: 'Status',
    cell: ({ row }) => h(UBadge, {
      color: row.original.status === 'Active' ? 'success' : 'warning',
      variant: 'subtle'
    }, () => row.original.status)
  }
}

function actionColumn(): TableColumn<any> {
  return {
    id: 'actions',
    header: '',
    cell: ({ row }) => h('div', { class: 'table-action-buttons' }, [
      canEdit.value ? h(UButton, {
        color: 'neutral',
        variant: 'ghost',
        icon: 'i-lucide-pencil',
        label: 'Edit',
        onClick: () => startEdit(row.original.raw)
      }) : null,
      canDelete.value ? h(UButton, {
        color: 'error',
        variant: 'ghost',
        icon: 'i-lucide-trash-2',
        label: 'Delete',
        onClick: () => askDelete(row.original.raw)
      }) : null
    ].filter(Boolean))
  }
}

function emptyCompany() {
  return {
    name: '',
    code: '',
    startDate: new Date().toISOString().slice(0, 10),
    endDate: '',
    active: true,
    contactNumber: '',
    email: '',
    address: '',
    city: 'Dumka',
    state: 'Jharkhand',
    country: 'India',
    zipCode: '814101',
    gstin: '',
    pan: '',
    storeCategory: 6,
    contactPerson: '',
    contactMobile: '',
    cin: '',
    companyType: 0
  }
}

function emptyStoreGroup() {
  return {
    name: '',
    groupCode: '',
    storeCategory: 6,
    startDate: new Date().toISOString().slice(0, 10),
    endDate: '',
    active: true,
    companyId: companies.value[0]?.id || ''
  }
}

function emptyStore() {
  return {
    name: '',
    storeCode: '',
    startDate: new Date().toISOString().slice(0, 10),
    endDate: '',
    active: true,
    storeCategory: 6,
    contactNumber: '',
    email: '',
    address: '',
    city: 'Dumka',
    state: 'Jharkhand',
    country: 'India',
    zipCode: '814101',
    companyId: companies.value[0]?.id || '',
    storeGroupId: storeGroups.value[0]?.id || ''
  }
}

async function refresh() {
  if (!auth.isAuthenticated.value) {
    return
  }

  loading.value = true
  try {
    const [companyRows, groupRows, storeRows] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('store-groups'),
      api.list<any>('stores')
    ])

    companies.value = companyRows
    storeGroups.value = groupRows
    stores.value = storeRows
  } catch (error) {
    feedback.failed('Company refresh failed', error)
  } finally {
    loading.value = false
  }
}

function showTab(tab: SetupTab) {
  activeTab.value = tab
  search.value = ''
  editingId.value = ''
  pendingDelete.value = null
}

function startCreate() {
  editingId.value = ''

  if (activeTab.value === 'companies') {
    Object.assign(companyForm, emptyCompany())
  } else if (activeTab.value === 'groups') {
    Object.assign(groupForm, emptyStoreGroup())
  } else {
    Object.assign(storeForm, emptyStore())
  }

  formOpen.value = true
}

function startEdit(item: any) {
  editingId.value = item.id

  if (activeTab.value === 'companies') {
    Object.assign(companyForm, {
      ...item,
      startDate: toDateInput(item.startDate),
      endDate: item.endDate ? toDateInput(item.endDate) : ''
    })
  } else if (activeTab.value === 'groups') {
    Object.assign(groupForm, {
      ...item,
      company: null,
      startDate: toDateInput(item.startDate),
      endDate: item.endDate ? toDateInput(item.endDate) : ''
    })
  } else {
    Object.assign(storeForm, {
      ...item,
      company: null,
      storeGroup: null,
      startDate: toDateInput(item.startDate),
      endDate: item.endDate ? toDateInput(item.endDate) : ''
    })
  }

  formOpen.value = true
}

async function saveCurrent() {
  saving.value = true

  try {
    if (activeTab.value === 'companies') {
      const payload = buildCompany()
      if (editingId.value) {
        await api.update<any>('companies', editingId.value, payload)
        feedback.updated('Company')
      } else {
        await api.create<any>('companies', payload)
        feedback.saved('Company')
      }
    } else if (activeTab.value === 'groups') {
      const payload = buildStoreGroup()
      if (editingId.value) {
        await api.update<any>('store-groups', editingId.value, payload)
        feedback.updated('Store group')
      } else {
        await api.create<any>('store-groups', payload)
        feedback.saved('Store group')
      }
    } else {
      const payload = buildStore()
      if (editingId.value) {
        await api.update<any>('stores', editingId.value, payload)
        feedback.updated('Store')
      } else {
        await api.create<any>('stores', payload)
        feedback.saved('Store')
      }
    }

    formOpen.value = false
    await refresh()
  } catch (error) {
    feedback.failed('Could not save company record', error)
  } finally {
    saving.value = false
  }
}

function buildCompany() {
  return {
    ...companyForm,
    name: String(companyForm.name || '').trim(),
    code: String(companyForm.code || '').trim(),
    startDate: toApiDate(companyForm.startDate),
    endDate: companyForm.endDate ? toApiDate(companyForm.endDate) : null,
    active: Boolean(companyForm.active),
    storeCategory: Number(companyForm.storeCategory),
    companyType: Number(companyForm.companyType)
  }
}

function buildStoreGroup() {
  if (!groupForm.companyId) {
    throw new Error('Select company before saving store group.')
  }

  return {
    ...groupForm,
    name: String(groupForm.name || '').trim(),
    groupCode: String(groupForm.groupCode || '').trim(),
    startDate: toApiDate(groupForm.startDate),
    endDate: groupForm.endDate ? toApiDate(groupForm.endDate) : null,
    active: Boolean(groupForm.active),
    storeCategory: Number(groupForm.storeCategory),
    companyId: groupForm.companyId,
    company: null
  }
}

function buildStore() {
  if (!storeForm.companyId || !storeForm.storeGroupId) {
    throw new Error('Select company and store group before saving store.')
  }

  return {
    ...storeForm,
    name: String(storeForm.name || '').trim(),
    storeCode: String(storeForm.storeCode || '').trim(),
    startDate: toApiDate(storeForm.startDate),
    endDate: storeForm.endDate ? toApiDate(storeForm.endDate) : null,
    active: Boolean(storeForm.active),
    storeCategory: Number(storeForm.storeCategory),
    companyId: storeForm.companyId,
    storeGroupId: storeForm.storeGroupId,
    company: null,
    storeGroup: null
  }
}

function askDelete(item: any) {
  pendingDelete.value = item
  deleteOpen.value = true
}

async function confirmDelete() {
  if (!pendingDelete.value) {
    return
  }

  const resource = activeTab.value === 'companies'
    ? 'companies'
    : activeTab.value === 'groups' ? 'store-groups' : 'stores'

  deleting.value = true
  try {
    await api.remove(resource, pendingDelete.value.id)
    feedback.deleted(singularLabel(activeTab.value))
    deleteOpen.value = false
    pendingDelete.value = null
    await refresh()
  } catch (error) {
    feedback.failed('Could not delete company record', error)
  } finally {
    deleting.value = false
  }
}

function companyName(companyId: string) {
  return companies.value.find((item) => item.id === companyId)?.name || 'Company'
}

function groupName(groupId: string) {
  return storeGroups.value.find((item) => item.id === groupId)?.name || 'Store group'
}

function categoryLabel(value: number) {
  return storeCategoryOptions.find((item) => item.value === Number(value))?.label || 'Retail'
}

function companyTypeLabel(value: number) {
  return companyTypeOptions.find((item) => item.value === Number(value))?.label || 'Company'
}

function singularLabel(tab: SetupTab) {
  if (tab === 'companies') {
    return 'Company'
  }

  if (tab === 'groups') {
    return 'Store group'
  }

  return 'Store'
}

function toApiDate(value: string) {
  return value ? new Date(`${value}T00:00:00`).toISOString() : null
}

function toDateInput(value: string) {
  return String(value || new Date().toISOString()).slice(0, 10)
}

onMounted(async () => {
  auth.restore()
  await refresh()
})
</script>

<template>
  <AuthScreen v-if="!isAuthenticated" @authenticated="refresh" />

  <AppShell
    v-else
    title="Company"
    :companies="companies"
    :stores="stores"
    @refresh="refresh"
  >
    <section class="planner-dashboard">
      <UiModulePageHeader
        title="Company & Stores"
        description="Manage companies, store groups, and stores before billing and inventory operations."
        icon="i-lucide-building-2"
        :primary-label="`New ${singularLabel(activeTab)}`"
        primary-icon="i-lucide-plus"
        @primary="startCreate"
      >
        <template #actions>
          <UBadge :color="loading ? 'warning' : 'success'" variant="subtle">
            {{ loading ? 'Loading' : 'Ready' }}
          </UBadge>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="subtle" :loading="loading" label="Refresh" @click="refresh" />
          <UButton icon="i-lucide-plus" :label="`New ${singularLabel(activeTab)}`" @click="startCreate" />
        </template>
      </UiModulePageHeader>

      <div class="setup-summary-grid">
        <UCard>
          <div class="planner-metric-body">
            <UAvatar icon="i-lucide-building-2" color="primary" variant="subtle" />
            <div>
              <p>Companies</p>
              <strong>{{ companies.length }}</strong>
              <span>Legal entities</span>
            </div>
          </div>
        </UCard>
        <UCard>
          <div class="planner-metric-body">
            <UAvatar icon="i-lucide-network" color="success" variant="subtle" />
            <div>
              <p>Store Groups</p>
              <strong>{{ storeGroups.length }}</strong>
              <span>Regional or business groups</span>
            </div>
          </div>
        </UCard>
        <UCard>
          <div class="planner-metric-body">
            <UAvatar icon="i-lucide-store" color="warning" variant="subtle" />
            <div>
              <p>Stores</p>
              <strong>{{ stores.length }}</strong>
              <span>Billing and stock locations</span>
            </div>
          </div>
        </UCard>
      </div>

      <UCard class="planner-card">
        <template #header>
          <div class="setup-list-header">
            <div class="setup-tabs">
              <UButton
                v-for="tab in tabs"
                :key="tab.key"
                :icon="tab.icon"
                :color="activeTab === tab.key ? 'primary' : 'neutral'"
                :variant="activeTab === tab.key ? 'solid' : 'subtle'"
                :label="tab.label"
                @click="showTab(tab.key)"
              />
            </div>

            <UBadge color="neutral" variant="subtle">{{ activeCount }} records</UBadge>
          </div>
        </template>

        <UiCrudToolbar
          v-model:search="search"
          :search-placeholder="`Search ${activeLabel.toLowerCase()}`"
          :loading="loading"
          refresh-label="Sync"
          :create-label="`New ${singularLabel(activeTab)}`"
          @refresh="refresh"
          @create="startCreate"
        />

        <UTable
          v-if="filteredRows.length"
          :data="filteredRows"
          :columns="activeColumns"
          :loading="loading"
        />

        <UiCrudEmptyState
          v-else
          :title="`No ${activeLabel.toLowerCase()} found`"
          description="Create the first company, group, or store record."
          icon="i-lucide-inbox"
          :action-label="`New ${singularLabel(activeTab)}`"
          @action="startCreate"
        />
      </UCard>

      <UiFormSlideover
        v-model:open="formOpen"
        :title="formTitle"
        :description="`Save ${singularLabel(activeTab).toLowerCase()} master data.`"
        :submit-label="isEditing ? 'Update' : 'Save'"
        :loading="saving"
        @submit="saveCurrent"
      >
        <template v-if="activeTab === 'companies'">
          <UFormField label="Name" required>
            <UInput v-model="companyForm.name" required />
          </UFormField>
          <UFormField label="Code">
            <UInput v-model="companyForm.code" />
          </UFormField>
          <UFormField label="Start date">
            <UInput v-model="companyForm.startDate" type="date" />
          </UFormField>
          <UFormField label="End date">
            <UInput v-model="companyForm.endDate" type="date" />
          </UFormField>
          <UFormField label="Company type">
            <USelect v-model="companyForm.companyType" :items="companyTypeOptions" />
          </UFormField>
          <UFormField label="Contact">
            <UInput v-model="companyForm.contactNumber" />
          </UFormField>
          <UFormField label="Email">
            <UInput v-model="companyForm.email" type="email" />
          </UFormField>
          <UFormField label="Address">
            <UTextarea v-model="companyForm.address" autoresize />
          </UFormField>
          <div class="form-two-column">
            <UFormField label="City">
              <UInput v-model="companyForm.city" />
            </UFormField>
            <UFormField label="State">
              <UInput v-model="companyForm.state" />
            </UFormField>
          </div>
          <div class="form-two-column">
            <UFormField label="GSTIN">
              <UInput v-model="companyForm.gstin" />
            </UFormField>
            <UFormField label="PAN">
              <UInput v-model="companyForm.pan" />
            </UFormField>
          </div>
          <UCheckbox v-model="companyForm.active" label="Active" />
        </template>

        <template v-else-if="activeTab === 'groups'">
          <UFormField label="Name" required>
            <UInput v-model="groupForm.name" required />
          </UFormField>
          <UFormField label="Code">
            <UInput v-model="groupForm.groupCode" />
          </UFormField>
          <UFormField label="Company" required>
            <USelect v-model="groupForm.companyId" :items="companyOptions" placeholder="Select company" />
          </UFormField>
          <UFormField label="Category">
            <USelect v-model="groupForm.storeCategory" :items="storeCategoryOptions" />
          </UFormField>
          <UFormField label="Start date">
            <UInput v-model="groupForm.startDate" type="date" />
          </UFormField>
          <UFormField label="End date">
            <UInput v-model="groupForm.endDate" type="date" />
          </UFormField>
          <UCheckbox v-model="groupForm.active" label="Active" />
        </template>

        <template v-else>
          <UFormField label="Name" required>
            <UInput v-model="storeForm.name" required />
          </UFormField>
          <UFormField label="Code">
            <UInput v-model="storeForm.storeCode" />
          </UFormField>
          <UFormField label="Company" required>
            <USelect v-model="storeForm.companyId" :items="companyOptions" placeholder="Select company" />
          </UFormField>
          <UFormField label="Store group" required>
            <USelect v-model="storeForm.storeGroupId" :items="groupOptions" placeholder="Select group" />
          </UFormField>
          <UFormField label="Contact">
            <UInput v-model="storeForm.contactNumber" />
          </UFormField>
          <UFormField label="Email">
            <UInput v-model="storeForm.email" type="email" />
          </UFormField>
          <UFormField label="Address">
            <UTextarea v-model="storeForm.address" autoresize />
          </UFormField>
          <div class="form-two-column">
            <UFormField label="City">
              <UInput v-model="storeForm.city" />
            </UFormField>
            <UFormField label="State">
              <UInput v-model="storeForm.state" />
            </UFormField>
          </div>
          <div class="form-two-column">
            <UFormField label="Country">
              <UInput v-model="storeForm.country" />
            </UFormField>
            <UFormField label="Zip">
              <UInput v-model="storeForm.zipCode" />
            </UFormField>
          </div>
          <UCheckbox v-model="storeForm.active" label="Active" />
        </template>
      </UiFormSlideover>

      <UiConfirmDeleteModal
        v-model:open="deleteOpen"
        :title="`Delete ${singularLabel(activeTab)}`"
        :description="`Delete ${pendingDelete?.name || 'this record'}?`"
        :loading="deleting"
        @confirm="confirmDelete"
      />
    </section>
  </AppShell>
</template>
