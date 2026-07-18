<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-dashboard-kicker">
            <UIcon name="i-lucide-shield-check" class="size-4" />
            Access control
          </p>
          <h2 class="garmetix-dashboard-title">Users And Roles</h2>
          <p class="garmetix-dashboard-subtitle">Create, edit, reset and deactivate user accounts, and review the role permission matrix.</p>
        </div>
        <div class="flex items-center gap-2">
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="loadData">Refresh</UButton>
          <UButton v-if="activeTab === 'users'" icon="i-lucide-plus" color="primary" @click="openCreateForm">New User</UButton>
        </div>
      </div>
    </div>

    <section class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
      <div v-for="card in cards" :key="card.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label">{{ card.label }}</p>
        <p class="garmetix-metric-value">{{ card.value }}</p>
        <p class="garmetix-metric-caption">{{ card.detail }}</p>
      </div>
    </section>

    <div class="flex flex-wrap items-center justify-between gap-3">
      <UTabs v-model="activeTab" :items="tabs" class="w-full max-w-xs" />
      <UInput v-if="activeTab === 'users'" v-model="search" icon="i-lucide-search" placeholder="Search users..." class="w-full sm:w-64" />
    </div>

    <section class="garmetix-section-card overflow-hidden p-0">
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
          <div class="flex items-center justify-end gap-1">
            <UTooltip text="Send Invitation Email">
              <UButton icon="i-lucide-mail" color="neutral" variant="ghost" size="sm" :loading="invitingUserId === row.original.id" @click="sendInvitation(row.original)" />
            </UTooltip>
            <UTooltip text="Reset Password">
              <UButton icon="i-lucide-key-round" color="neutral" variant="ghost" size="sm" @click="askReset(row.original)" />
            </UTooltip>
            <UTooltip text="Edit User">
              <UButton icon="i-lucide-pencil" color="primary" variant="ghost" size="sm" @click="openEditForm(row.original)" />
            </UTooltip>
            <UTooltip text="Toggle Status">
              <UButton :icon="row.original.isActive ? 'i-lucide-pause-circle' : 'i-lucide-play-circle'" color="warning" variant="ghost" size="sm" @click="toggleStatus(row.original)" />
            </UTooltip>
            <UTooltip text="Delete User">
              <UButton icon="i-lucide-trash-2" color="error" variant="ghost" size="sm" @click="askDelete(row.original)" />
            </UTooltip>
          </div>
        </template>
      </UTable>

      <UTable
        v-else
        :data="matrixRows"
        :columns="matrixColumns"
        class="w-full"
      >
        <template #edit-cell="{ row }">
          <UBadge variant="subtle" :color="row.original.edit === 'Allowed' ? 'success' : 'neutral'">{{ row.original.edit }}</UBadge>
        </template>
        <template #delete-cell="{ row }">
          <UBadge variant="subtle" :color="row.original.delete === 'Allowed' ? 'success' : 'neutral'">{{ row.original.delete }}</UBadge>
        </template>
      </UTable>
    </section>

    <!-- Slideover: Add/Edit User -->
    <USlideover v-model:open="slideoverOpen" :title="isEditing ? 'Edit User' : 'New User'" :description="isEditing ? 'Update user properties' : 'Create a new user account'">
      <template #body>
        <UForm :state="form" class="space-y-4" @submit="saveUser">
          <UFormField label="Name" name="name" required>
            <UInput v-model="form.name" class="w-full" required />
          </UFormField>

          <div class="grid grid-cols-2 gap-4">
            <UFormField label="Username" name="userName" required>
              <UInput v-model="form.userName" class="w-full" required />
            </UFormField>
            <UFormField label="Email" name="email" required>
              <UInput v-model="form.email" type="email" class="w-full" required />
            </UFormField>
          </div>

          <UFormField v-if="!isEditing" label="Password" name="password" required>
            <UInput v-model="form.password" type="password" class="w-full" required minlength="6" />
          </UFormField>
          <UAlert v-else color="info" variant="subtle" icon="i-lucide-info" description="Use Reset Password from the table to change this user's password." />

          <div class="grid grid-cols-2 gap-4">
            <UFormField label="Role" name="role">
              <USelectMenu v-model="form.role" :items="roleOptions" value-key="value" class="w-full" />
            </UFormField>
            <UFormField label="User Type" name="userType">
              <USelectMenu v-model="form.userType" :items="userTypeOptions" value-key="value" class="w-full" />
            </UFormField>
          </div>

          <UFormField label="Operation Scope" name="appOperation">
            <USelectMenu v-model="form.appOperation" :items="appOperationOptions" value-key="value" class="w-full" />
          </UFormField>

          <div class="space-y-4 rounded-md border border-default p-4 bg-elevated">
            <p class="text-sm font-medium text-highlighted">Access Restrictions</p>
            <UFormField label="Company" name="companyId">
              <USelectMenu v-model="form.companyId" :items="companyOptions" value-key="value" class="w-full" />
            </UFormField>
            <UFormField label="Store Group" name="storeGroupId">
              <USelectMenu v-model="form.storeGroupId" :items="groupOptions" value-key="value" class="w-full" />
            </UFormField>
            <UFormField label="Store" name="storeId">
              <USelectMenu v-model="form.storeId" :items="storeOptions" value-key="value" class="w-full" />
            </UFormField>
          </div>

          <UCheckbox v-model="form.isActive" label="Active Account" />

          <div class="mt-6 flex justify-end gap-3">
            <UButton color="neutral" variant="ghost" label="Cancel" @click="slideoverOpen = false" />
            <UButton type="submit" color="primary" :loading="saving" label="Save User" />
          </div>
        </UForm>
      </template>
    </USlideover>

    <!-- Modal: Reset Password -->
    <UModal v-model:open="resetModalOpen" title="Reset Password" :description="`Reset password for ${resetTarget?.userName ?? ''}`">
      <template #body>
        <UForm :state="{ resetPassword }" class="space-y-4" @submit="performReset">
          <UFormField label="New Password" name="resetPassword" required>
            <UInput v-model="resetPassword" type="password" class="w-full" required minlength="6" />
          </UFormField>
          <div class="mt-4 flex justify-end gap-3">
            <UButton color="neutral" variant="ghost" label="Cancel" @click="resetModalOpen = false" />
            <UButton type="submit" color="primary" :loading="resetting" label="Reset Password" />
          </div>
        </UForm>
      </template>
    </UModal>

    <!-- Modal: Confirm Delete -->
    <UModal v-model:open="deleteModalOpen" title="Delete User" :description="`Are you sure you want to delete ${deleteTarget?.userName ?? ''}? This action cannot be undone.`">
      <template #body>
        <div class="flex justify-end gap-3">
          <UButton color="neutral" variant="ghost" label="Cancel" @click="deleteModalOpen = false" />
          <UButton color="error" :loading="deleting" label="Delete" @click="performDelete" />
        </div>
      </template>
    </UModal>
  </section>
</template>

<script setup lang="ts">
import { reactive, ref, computed, onMounted } from 'vue'
import { toRows, useAdminApiClient } from '../utils/admin-api'

useHead({ title: 'Users And Roles - Garmetix Admin' })

const toast = useToast()
const { get, post, put, remove } = useAdminApiClient()
const NO_SCOPE = '__none__'

const loading = ref(true)
const saving = ref(false)
const resetting = ref(false)
const deleting = ref(false)
const search = ref('')

const users = ref<any[]>([])
const companies = ref<any[]>([])
const storeGroups = ref<any[]>([])
const stores = ref<any[]>([])

const slideoverOpen = ref(false)
const isEditing = ref(false)
const editingUserId = ref('')
const form = reactive({
  name: '', userName: '', email: '', password: '',
  role: 5, userType: 6, appOperation: 2,
  companyId: NO_SCOPE as string | null,
  storeGroupId: NO_SCOPE as string | null,
  storeId: NO_SCOPE as string | null,
  isActive: true
})

const resetModalOpen = ref(false)
const resetTarget = ref<any>(null)
const resetPassword = ref('')

const deleteModalOpen = ref(false)
const deleteTarget = ref<any>(null)

const activeTab = ref('users')
const tabs = [
  { label: 'Users', icon: 'i-lucide-users', value: 'users' },
  { label: 'Role Matrix', icon: 'i-lucide-shield-check', value: 'matrix' }
]

// Matches backend Garmetix.Core.Enums.LoginRole declaration order.
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

// Matches backend Garmetix.Core.Enums.UserType declaration order.
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

// Matches backend Garmetix.Core.Enums.AppOperation declaration order.
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

const matrixRows = [
  { role: 'Owner', modules: 'All', edit: 'Allowed', delete: 'Allowed' },
  { role: 'Admin', modules: 'All', edit: 'Allowed', delete: 'Allowed' },
  { role: 'Store Manager', modules: 'POS, Inventory', edit: 'Allowed', delete: 'No' },
  { role: 'Salesman', modules: 'POS', edit: 'No', delete: 'No' },
  { role: 'Accountant', modules: 'Books, HR', edit: 'Allowed', delete: 'No' }
]

const filteredUsers = computed(() => {
  let list = users.value.map(u => ({ ...u, scope: getScopeLabel(u) }))
  if (search.value.trim()) {
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

const cards = computed(() => [
  { label: 'Users', value: users.value.length, detail: 'Total user accounts' },
  { label: 'Active Users', value: users.value.filter(u => u.isActive !== false).length, detail: 'Currently enabled' },
  { label: 'Admins', value: users.value.filter(u => u.admin || String(u.role ?? '').toLowerCase().includes('admin')).length, detail: 'Admin-role accounts' },
  { label: 'Roles', value: matrixRows.length, detail: 'Roles in permission matrix' }
])

async function loadData() {
  loading.value = true
  try {
    const [uRes, cRes, gRes, sRes] = await Promise.allSettled([
      get<unknown>('access/users'),
      get<unknown>('companies'),
      get<unknown>('store-groups'),
      get<unknown>('stores')
    ])
    if (uRes.status === 'fulfilled') users.value = toRows(uRes.value)
    if (cRes.status === 'fulfilled') companies.value = toRows(cRes.value)
    if (gRes.status === 'fulfilled') storeGroups.value = toRows(gRes.value)
    if (sRes.status === 'fulfilled') stores.value = toRows(sRes.value)
  } catch (caught) {
    console.error('loadData error:', caught)
  } finally {
    loading.value = false
  }
}

function openCreateForm() {
  isEditing.value = false
  editingUserId.value = ''
  Object.assign(form, {
    name: '', userName: '', email: '', password: '',
    role: 5, userType: 6, appOperation: 2,
    companyId: NO_SCOPE, storeGroupId: NO_SCOPE, storeId: NO_SCOPE,
    isActive: true
  })
  slideoverOpen.value = true
}

function openEditForm(user: any) {
  isEditing.value = true
  editingUserId.value = user.id
  Object.assign(form, {
    name: user.name, userName: user.userName, email: user.email, password: '',
    role: roleOptions.find(r => r.label.toLowerCase() === String(user.role).toLowerCase())?.value ?? 5,
    userType: userTypeOptions.find(r => r.label.toLowerCase() === String(user.userType).toLowerCase())?.value ?? 6,
    appOperation: appOperationOptions.find(r => r.label.toLowerCase() === String(user.appOperation).toLowerCase())?.value ?? 2,
    companyId: user.companyId || NO_SCOPE,
    storeGroupId: user.storeGroupId || NO_SCOPE,
    storeId: user.storeId || NO_SCOPE,
    isActive: Boolean(user.isActive)
  })
  slideoverOpen.value = true
}

function unwrapScope(value: string | { value: string } | null) {
  const raw = (value as { value?: string })?.value ?? value
  return raw === NO_SCOPE ? null : raw
}

async function saveUser() {
  saving.value = true
  try {
    const payload = {
      name: form.name,
      userName: form.userName,
      email: form.email,
      password: isEditing.value ? null : form.password,
      role: (form.role as any)?.value ?? form.role,
      userType: (form.userType as any)?.value ?? form.userType,
      appOperation: (form.appOperation as any)?.value ?? form.appOperation,
      companyId: unwrapScope(form.companyId),
      storeGroupId: unwrapScope(form.storeGroupId),
      storeId: unwrapScope(form.storeId),
      isActive: form.isActive
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
  } catch (caught) {
    toast.add({ title: 'Error', description: caught instanceof Error ? caught.message : 'Failed to save user.', color: 'error' })
  } finally {
    saving.value = false
  }
}

const invitingUserId = ref('')

async function sendInvitation(user: any) {
  invitingUserId.value = user.id
  try {
    const result = await post<any>(`access/users/${user.id}/send-invitation-email`)
    if (result?.enqueued) {
      toast.add({ title: 'Success', description: `Invitation email queued for ${user.name || user.userName}.`, color: 'success' })
    } else {
      toast.add({ title: 'Not sent', description: String(result?.skipReason ?? 'Could not queue the invitation email.'), color: 'warning' })
    }
  } catch (caught) {
    toast.add({ title: 'Error', description: caught instanceof Error ? caught.message : 'Failed to queue the invitation email.', color: 'error' })
  } finally {
    invitingUserId.value = ''
  }
}

async function toggleStatus(user: any) {
  try {
    await post(`access/users/${user.id}/status`, { isActive: !user.isActive })
    toast.add({ title: 'Success', description: `User ${user.isActive ? 'deactivated' : 'activated'}.`, color: 'success' })
    await loadData()
  } catch (caught) {
    toast.add({ title: 'Error', description: caught instanceof Error ? caught.message : 'Failed to update status.', color: 'error' })
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
  } catch (caught) {
    toast.add({ title: 'Error', description: caught instanceof Error ? caught.message : 'Failed to reset password.', color: 'error' })
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
  } catch (caught) {
    toast.add({ title: 'Error', description: caught instanceof Error ? caught.message : 'Failed to delete user.', color: 'error' })
  } finally {
    deleting.value = false
  }
}

onMounted(loadData)
</script>
