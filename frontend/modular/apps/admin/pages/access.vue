<script setup lang="ts">
import { useAdminApiClient } from '../utils/admin-api'

useHead({ title: 'Users & Roles - Garmetix Admin' })

const toast = useToast()
const { get, post, put, remove } = useAdminApiClient()
const NO_SCOPE = '__none__'

const loading = ref(true)
const saving = ref(false)
const resetting = ref(false)
const deleting = ref(false)
const search = ref('')

const users = ref<any[]>([])
const matrixRows = ref<any[]>([])
const companies = ref<any[]>([])
const storeGroups = ref<any[]>([])
const stores = ref<any[]>([])

// Form state
const slideoverOpen = ref(false)
const isEditing = ref(false)
const editingUserId = ref('')
const form = reactive({
  name: '',
  userName: '',
  email: '',
  password: '',
  role: 5,
  userType: 6,
  appOperation: 2,
  companyId: NO_SCOPE as string | null,
  storeGroupId: NO_SCOPE as string | null,
  storeId: NO_SCOPE as string | null,
  isActive: true
})

// Modal states
const resetModalOpen = ref(false)
const resetTarget = ref<any>(null)
const resetPassword = ref('')

const deleteModalOpen = ref(false)
const deleteTarget = ref<any>(null)

const activeTab = ref('users')
const items = [
  { label: 'Users', icon: 'i-lucide-users', value: 'users' },
  { label: 'Role Matrix', icon: 'i-lucide-shield-check', value: 'matrix' }
]

const roleOptions = [
  { value: 0, label: 'Admin' },
  { value: 1, label: 'Store Manager' },
  { value: 2, label: 'Salesman' },
  { value: 3, label: 'Accountant' },
  { value: 4, label: 'Remote Accountant' },
  { value: 5, label: 'Member' },
  { value: 6, label: 'Power User' },
  { value: 7, label: 'HR' },
  { value: 8, label: 'Payroll' }
]

const userTypeOptions = [
  { value: 0, label: 'Admin' },
  { value: 1, label: 'Owner' },
  { value: 2, label: 'Store Manager' },
  { value: 3, label: 'Sales' },
  { value: 4, label: 'Accountant' },
  { value: 5, label: 'CA' },
  { value: 6, label: 'Guest' },
  { value: 7, label: 'Power User' },
  { value: 8, label: 'Employees' }
]

const appOperationOptions = [
  { value: 0, label: 'Company' },
  { value: 1, label: 'Store Group' },
  { value: 2, label: 'Store' },
  { value: 3, label: 'All' },
  { value: 4, label: 'None' }
]

const companyOptions = computed(() => [
  { value: NO_SCOPE, label: 'No company scope' },
  ...companies.value.map(c => ({ value: c.id, label: c.name || 'Company' }))
])

const groupOptions = computed(() => [
  { value: NO_SCOPE, label: 'No store group scope' },
  ...storeGroups.value
    .filter(g => form.companyId === NO_SCOPE || g.companyId === form.companyId)
    .map(g => ({ value: g.id, label: g.name || 'Store group' }))
])

const storeOptions = computed(() => [
  { value: NO_SCOPE, label: 'No store scope' },
  ...stores.value
    .filter(s => (form.companyId === NO_SCOPE || s.companyId === form.companyId) && (form.storeGroupId === NO_SCOPE || s.storeGroupId === form.storeGroupId))
    .map(s => ({ value: s.id, label: s.name || 'Store' }))
])

// Scope cascading resets
watch(() => form.companyId, () => {
  if (form.storeGroupId !== NO_SCOPE && !storeGroups.value.some(g => g.id === form.storeGroupId && g.companyId === form.companyId)) {
    form.storeGroupId = NO_SCOPE
  }
  if (form.storeId !== NO_SCOPE && !stores.value.some(s => s.id === form.storeId && (form.companyId === NO_SCOPE || s.companyId === form.companyId))) {
    form.storeId = NO_SCOPE
  }
})
watch(() => form.storeGroupId, () => {
  if (form.storeId !== NO_SCOPE && !stores.value.some(s => s.id === form.storeId && (form.storeGroupId === NO_SCOPE || s.storeGroupId === form.storeGroupId))) {
    form.storeId = NO_SCOPE
  }
})

function getScopeLabel(user: any) {
  if (String(user.appOperation).toLowerCase() === 'all') return 'All'
  const store = stores.value.find(s => s.id === user.storeId)
  if (store) return store.name
  const group = storeGroups.value.find(g => g.id === user.storeGroupId)
  if (group) return group.name
  const company = companies.value.find(c => c.id === user.companyId)
  return company?.name || user.appOperation || 'None'
}

const userColumns = [
  { accessorKey: 'name', header: 'Name' },
  { accessorKey: 'userName', header: 'Username' },
  { accessorKey: 'email', header: 'Email' },
  { accessorKey: 'role', header: 'Role' },
  { accessorKey: 'scope', header: 'Scope' },
  { accessorKey: 'isActive', header: 'Status' },
  { accessorKey: 'actions', header: '', enableSorting: false }
]

const matrixColumns = [
  { accessorKey: 'role', header: 'Role' },
  { accessorKey: 'modules', header: 'Modules' },
  { accessorKey: 'edit', header: 'Edit' },
  { accessorKey: 'delete', header: 'Delete' }
]

const filteredUsers = computed(() => {
  let list = users.value.map(u => ({
    ...u,
    scope: getScopeLabel(u)
  }))
  if (search.value) {
    const term = search.value.toLowerCase()
    list = list.filter(u => 
      u.name?.toLowerCase().includes(term) || 
      u.userName?.toLowerCase().includes(term) ||
      u.email?.toLowerCase().includes(term) ||
      u.role?.toLowerCase().includes(term) ||
      u.scope?.toLowerCase().includes(term)
    )
  }
  return list
})

async function loadData() {
  loading.value = true
  try {
    const [uRes, cRes, gRes, sRes] = await Promise.allSettled([
      get<any>('access/users'),
      get<any>('companies'),
      get<any>('store-groups'),
      get<any>('stores')
    ])
    
    if (uRes.status === 'fulfilled') {
      const data = uRes.value
      users.value = data?.data || data?.$values || data || []
    }
    
    matrixRows.value = [
      { role: 'Owner', modules: 'All', edit: 'Allowed', delete: 'Allowed' },
      { role: 'Admin', modules: 'All', edit: 'Allowed', delete: 'Allowed' },
      { role: 'Store Manager', modules: 'POS, Inventory', edit: 'Allowed', delete: 'No' },
      { role: 'Salesman', modules: 'POS', edit: 'No', delete: 'No' },
      { role: 'Accountant', modules: 'Books, HR', edit: 'Allowed', delete: 'No' }
    ]

    if (cRes.status === 'fulfilled') {
      const data = cRes.value
      const list = data?.data || data?.$values || data || []
      companies.value = Array.isArray(list) ? list : (list.items || [])
    }
    if (gRes.status === 'fulfilled') {
      const data = gRes.value
      const list = data?.data || data?.$values || data || []
      storeGroups.value = Array.isArray(list) ? list : (list.items || [])
    }
    if (sRes.status === 'fulfilled') {
      const data = sRes.value
      const list = data?.data || data?.$values || data || []
      stores.value = Array.isArray(list) ? list : (list.items || [])
    }
  } catch (e: any) {
    console.error('loadData error:', e)
  } finally {
    loading.value = false
  }
}

function openCreateForm() {
  isEditing.value = false
  editingUserId.value = ''
  Object.assign(form, {
    name: '',
    userName: '',
    email: '',
    password: '',
    role: 5,
    userType: 6,
    appOperation: 2,
    companyId: NO_SCOPE,
    storeGroupId: NO_SCOPE,
    storeId: NO_SCOPE,
    isActive: true
  })
  slideoverOpen.value = true
}

function openEditForm(user: any) {
  isEditing.value = true
  editingUserId.value = user.id
  Object.assign(form, {
    name: user.name,
    userName: user.userName,
    email: user.email,
    password: '',
    role: roleOptions.find(r => String(r.label).toLowerCase() === String(user.role).toLowerCase())?.value ?? 5,
    userType: userTypeOptions.find(r => String(r.label).toLowerCase() === String(user.userType).toLowerCase())?.value ?? 6,
    appOperation: appOperationOptions.find(r => String(r.label).toLowerCase() === String(user.appOperation).toLowerCase())?.value ?? 2,
    companyId: user.companyId || NO_SCOPE,
    storeGroupId: user.storeGroupId || NO_SCOPE,
    storeId: user.storeId || NO_SCOPE,
    isActive: Boolean(user.isActive)
  })
  slideoverOpen.value = true
}

async function saveUser() {
  saving.value = true
  try {
    const payload = {
      ...form,
      companyId: form.companyId === NO_SCOPE ? null : form.companyId,
      storeGroupId: form.storeGroupId === NO_SCOPE ? null : form.storeGroupId,
      storeId: form.storeId === NO_SCOPE ? null : form.storeId,
      password: isEditing.value ? null : form.password
    }

    if (isEditing.value) {
      await put(`access/users/${editingUserId.value}`, payload)
      toast.add({ title: 'Success', description: 'User updated successfully.', color: 'success' })
    } else {
      await post('access/users', payload)
      toast.add({ title: 'Success', description: 'User created successfully.', color: 'success' })
    }
    slideoverOpen.value = false
    await loadData()
  } catch (err: any) {
    toast.add({ title: 'Error', description: err.message || 'Failed to save user.', color: 'error' })
  } finally {
    saving.value = false
  }
}

async function toggleStatus(user: any) {
  try {
    await post(`access/users/${user.id}/status`, { isActive: !user.isActive })
    toast.add({ title: 'Success', description: `User ${user.isActive ? 'deactivated' : 'activated'}.`, color: 'success' })
    await loadData()
  } catch (err: any) {
    toast.add({ title: 'Error', description: err.message, color: 'error' })
  }
}

function askReset(user: any) {
  resetTarget.value = user
  resetPassword.value = ''
  resetModalOpen.value = true
}

async function performReset() {
  if (!resetPassword.value) {
    toast.add({ title: 'Validation', description: 'Password is required.', color: 'warning' })
    return
  }
  resetting.value = true
  try {
    await post(`access/users/${resetTarget.value.id}/reset-password`, { newPassword: resetPassword.value })
    toast.add({ title: 'Success', description: 'Password reset successfully.', color: 'success' })
    resetModalOpen.value = false
  } catch (err: any) {
    toast.add({ title: 'Error', description: err.message, color: 'error' })
  } finally {
    resetting.value = false
  }
}

function askDelete(user: any) {
  deleteTarget.value = user
  deleteModalOpen.value = true
}

async function performDelete() {
  deleting.value = true
  try {
    await remove(`access/users/${deleteTarget.value.id}`)
    toast.add({ title: 'Success', description: 'User deleted.', color: 'success' })
    deleteModalOpen.value = false
    await loadData()
  } catch (err: any) {
    toast.add({ title: 'Error', description: err.message, color: 'error' })
  } finally {
    deleting.value = false
  }
}

onMounted(loadData)
</script>

<template>
  <UDashboardPage>
    <UDashboardPanel grow>
      <UDashboardNavbar title="Users & Roles" badge="Access Control">
        <template #right>
          <UButton
            v-if="activeTab === 'users'"
            color="primary"
            icon="i-lucide-plus"
            label="New User"
            @click="openCreateForm"
          />
        </template>
      </UDashboardNavbar>

      <UDashboardToolbar>
        <template #left>
          <UTabs v-model="activeTab" :items="items" class="w-full" />
        </template>
        <template #right>
          <UInput
            v-if="activeTab === 'users'"
            v-model="search"
            icon="i-lucide-search"
            placeholder="Search users..."
            class="w-64"
          />
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="ghost" :loading="loading" @click="loadData" />
        </template>
      </UDashboardToolbar>

      <UDashboardPanelContent class="p-0">
        <!-- Users Tab -->
        <UTable
          v-if="activeTab === 'users'"
          :data="filteredUsers"
          :columns="userColumns"
          :loading="loading"
          class="w-full"
        >
          <template #role-cell="{ row }">
            <UBadge variant="subtle" :color="String(row.original.role).toLowerCase().includes('admin') ? 'success' : 'neutral'">
              {{ row.original.role }}
            </UBadge>
          </template>
          <template #isActive-cell="{ row }">
            <UBadge variant="subtle" :color="row.original.isActive ? 'success' : 'error'">
              {{ row.original.isActive ? 'Active' : 'Inactive' }}
            </UBadge>
          </template>
          <template #actions-cell="{ row }">
            <div class="flex items-center gap-1">
              <UTooltip text="Reset Password">
                <UButton icon="i-lucide-key-round" color="neutral" variant="ghost" @click="askReset(row.original)" />
              </UTooltip>
              <UTooltip text="Edit User">
                <UButton icon="i-lucide-pencil" color="primary" variant="ghost" @click="openEditForm(row.original)" />
              </UTooltip>
              <UTooltip text="Toggle Status">
                <UButton :icon="row.original.isActive ? 'i-lucide-pause-circle' : 'i-lucide-play-circle'" color="warning" variant="ghost" @click="toggleStatus(row.original)" />
              </UTooltip>
              <UTooltip text="Delete User">
                <UButton icon="i-lucide-trash-2" color="error" variant="ghost" @click="askDelete(row.original)" />
              </UTooltip>
            </div>
          </template>
        </UTable>

        <!-- Role Matrix Tab -->
        <UTable
          v-else-if="activeTab === 'matrix'"
          :data="matrixRows"
          :columns="matrixColumns"
          :loading="loading"
          class="w-full"
        >
          <template #edit-cell="{ row }">
            <UBadge variant="subtle" :color="row.original.edit === 'Allowed' ? 'success' : 'neutral'">{{ row.original.edit }}</UBadge>
          </template>
          <template #delete-cell="{ row }">
            <UBadge variant="subtle" :color="row.original.delete === 'Allowed' ? 'success' : 'neutral'">{{ row.original.delete }}</UBadge>
          </template>
        </UTable>
      </UDashboardPanelContent>
    </UDashboardPanel>

    <!-- Slideover: Add/Edit User -->
    <USlideover v-model:open="slideoverOpen" :title="isEditing ? 'User Details' : 'New User'" :description="isEditing ? 'Update user properties' : 'Create a new user'">
      <template #body>
        <UForm :state="form" @submit="saveUser" class="space-y-4">
          <UFormField label="Name" name="name" required>
            <UInput v-model="form.name" required />
          </UFormField>
          
          <div class="grid grid-cols-2 gap-4">
            <UFormField label="Username" name="userName" required>
              <UInput v-model="form.userName" required />
            </UFormField>
            <UFormField label="Email" name="email" required>
              <UInput v-model="form.email" type="email" required />
            </UFormField>
          </div>

          <UFormField v-if="!isEditing" label="Password" name="password" required>
            <UInput v-model="form.password" type="password" required minlength="6" />
          </UFormField>

          <div class="grid grid-cols-2 gap-4">
            <UFormField label="Role" name="role">
              <USelect v-model="form.role" :items="roleOptions" />
            </UFormField>
            <UFormField label="User Type" name="userType">
              <USelect v-model="form.userType" :items="userTypeOptions" />
            </UFormField>
          </div>

          <UFormField label="Operation Scope" name="appOperation">
            <USelect v-model="form.appOperation" :items="appOperationOptions" />
          </UFormField>

          <div class="space-y-4 rounded-md border border-(--ui-border) p-4 bg-(--ui-bg-elevated)">
            <p class="text-sm font-medium text-(--ui-text-highlighted)">Access Restrictions</p>
            <UFormField label="Company" name="companyId">
              <USelect v-model="form.companyId" :items="companyOptions" />
            </UFormField>
            <UFormField label="Store Group" name="storeGroupId">
              <USelect v-model="form.storeGroupId" :items="groupOptions" />
            </UFormField>
            <UFormField label="Store" name="storeId">
              <USelect v-model="form.storeId" :items="storeOptions" />
            </UFormField>
          </div>

          <UFormField name="isActive">
            <UCheckbox v-model="form.isActive" label="Active Account" />
          </UFormField>

          <div class="mt-6 flex justify-end gap-3">
            <UButton color="neutral" variant="ghost" label="Cancel" @click="slideoverOpen = false" />
            <UButton type="submit" color="primary" :loading="saving" label="Save User" />
          </div>
        </UForm>
      </template>
    </USlideover>

    <!-- Modal: Reset Password -->
    <UModal v-model:open="resetModalOpen" title="Reset Password" :description="`Reset password for ${resetTarget?.userName}`">
      <template #body>
        <UForm :state="{ resetPassword }" @submit="performReset" class="space-y-4">
          <UFormField label="New Password" name="resetPassword" required>
            <UInput v-model="resetPassword" type="password" required minlength="6" />
          </UFormField>
          <div class="flex justify-end gap-3 mt-4">
            <UButton color="neutral" variant="ghost" label="Cancel" @click="resetModalOpen = false" />
            <UButton type="submit" color="primary" :loading="resetting" label="Reset Password" />
          </div>
        </UForm>
      </template>
    </UModal>

    <!-- Modal: Confirm Delete -->
    <UModal v-model:open="deleteModalOpen" title="Delete User" :description="`Are you sure you want to delete ${deleteTarget?.userName}? This action cannot be undone.`">
      <template #body>
        <div class="flex justify-end gap-3 mt-4">
          <UButton color="neutral" variant="ghost" label="Cancel" @click="deleteModalOpen = false" />
          <UButton color="error" :loading="deleting" label="Delete" @click="performDelete" />
        </div>
      </template>
    </UModal>

  </UDashboardPage>
</template>
