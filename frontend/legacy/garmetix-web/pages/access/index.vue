<script setup lang="ts">
import { h, resolveComponent } from 'vue'
import type { TableColumn } from '@nuxt/ui'

const api = useGarmetixApi()
const auth = useAuth()
const feedback = useUiFeedback()
const isAuthenticated = auth.isAuthenticated
const canEdit = auth.canEdit
const canDelete = auth.canDelete

const UBadge = resolveComponent('UBadge')
const UButton = resolveComponent('UButton')
const NO_SCOPE = '__none__'

const companies = ref<any[]>([])
const storeGroups = ref<any[]>([])
const stores = ref<any[]>([])
const users = ref<any[]>([])
const permissionMatrix = ref<any[]>([])
const loading = ref(false)
const loadError = ref('')
const saving = ref(false)
const deleting = ref(false)
const resetting = ref(false)
const statusUpdatingId = ref('')
const search = ref('')
const formOpen = ref(false)
const deleteOpen = ref(false)
const resetOpen = ref(false)
const editingUserId = ref('')
const pendingDelete = ref<any | null>(null)
const pendingReset = ref<any | null>(null)
const resetPassword = ref('')

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

const form = reactive<any>(emptyUser())

const companyOptions = computed(() => [
  { value: NO_SCOPE, label: 'No company scope' },
  ...companies.value.map((company) => ({
    value: company.id,
    label: company.name || 'Company'
  }))
])

const groupOptions = computed(() => [
  { value: NO_SCOPE, label: 'No store group scope' },
  ...storeGroups.value
    .filter((group) => form.companyId === NO_SCOPE || group.companyId === form.companyId)
    .map((group) => ({
      value: group.id,
      label: group.name || 'Store group'
    }))
])

const storeOptions = computed(() => [
  { value: NO_SCOPE, label: 'No store scope' },
  ...stores.value
    .filter((store) => (form.companyId === NO_SCOPE || store.companyId === form.companyId) && (form.storeGroupId === NO_SCOPE || store.storeGroupId === form.storeGroupId))
    .map((store) => ({
      value: store.id,
      label: store.name || 'Store'
    }))
])


watch(() => form.companyId, () => {
  if (form.storeGroupId !== NO_SCOPE && !storeGroups.value.some((group) => group.id === form.storeGroupId && group.companyId === form.companyId)) {
    form.storeGroupId = NO_SCOPE
  }
  if (form.storeId !== NO_SCOPE && !stores.value.some((store) => store.id === form.storeId && (form.companyId === NO_SCOPE || store.companyId === form.companyId))) {
    form.storeId = NO_SCOPE
  }
})

watch(() => form.storeGroupId, () => {
  if (form.storeId !== NO_SCOPE && !stores.value.some((store) => store.id === form.storeId && (form.storeGroupId === NO_SCOPE || store.storeGroupId === form.storeGroupId))) {
    form.storeId = NO_SCOPE
  }
})

const metrics = computed(() => [
  {
    label: 'Users',
    value: users.value.length,
    meta: `${users.value.filter((user) => user.isActive).length} active`,
    icon: 'i-lucide-users-round',
    color: 'primary'
  },
  {
    label: 'Admins',
    value: users.value.filter((user) => user.admin || compact(user.role) === 'admin').length,
    meta: 'High privilege users',
    icon: 'i-lucide-shield-check',
    color: 'success'
  },
  {
    label: 'Store Scoped',
    value: users.value.filter((user) => Boolean(user.storeId)).length,
    meta: 'Bound to a store',
    icon: 'i-lucide-store',
    color: 'warning'
  },
  {
    label: 'Inactive',
    value: users.value.filter((user) => !user.isActive).length,
    meta: 'Login blocked',
    icon: 'i-lucide-user-x',
    color: 'error'
  }
])

const rows = computed(() => users.value.map((user) => ({
  id: user.id,
  name: user.name || '-',
  userName: user.userName || '-',
  email: user.email || '-',
  role: user.role || '-',
  userType: user.userType || '-',
  scope: scopeLabel(user),
  status: user.isActive ? 'Active' : 'Inactive',
  raw: user
})))

const filteredRows = computed(() => {
  const term = search.value.trim().toLowerCase()
  if (!term) {
    return rows.value
  }

  return rows.value.filter((row) => JSON.stringify(row).toLowerCase().includes(term))
})

const matrixRows = computed(() => permissionMatrix.value.map((profile) => ({
  role: profile.role,
  modules: Array.isArray(profile.modules) ? profile.modules.join(', ') : '-',
  entry: Boolean(profile.entry),
  edit: Boolean(profile.edit),
  delete: Boolean(profile.delete),
  adminWorkspace: Boolean(profile.adminWorkspace),
  notes: profile.notes || ''
})))

const columns: TableColumn<any>[] = [
  { accessorKey: 'name', header: 'Name' },
  { accessorKey: 'userName', header: 'Username' },
  { accessorKey: 'email', header: 'Email' },
  {
    accessorKey: 'role',
    header: 'Role',
    cell: ({ row }) => h(UBadge, {
      color: roleColor(row.original.role),
      variant: 'subtle'
    }, () => row.original.role)
  },
  { accessorKey: 'userType', header: 'User Type' },
  { accessorKey: 'scope', header: 'Scope' },
  {
    accessorKey: 'status',
    header: 'Status',
    cell: ({ row }) => h(UBadge, {
      color: row.original.status === 'Active' ? 'success' : 'error',
      variant: 'subtle'
    }, () => row.original.status)
  },
  {
    id: 'actions',
    header: '',
    cell: ({ row }) => h('div', { class: 'table-action-buttons' }, [
      canEdit.value ? h(UButton, {
        color: 'neutral',
        variant: 'ghost',
        icon: 'i-lucide-key-round',
        label: 'Reset',
        onClick: () => startReset(row.original.raw)
      }) : null,
      canEdit.value ? h(UButton, {
        color: 'neutral',
        variant: 'ghost',
        icon: 'i-lucide-pencil',
        label: 'Edit',
        onClick: () => startEdit(row.original.raw)
      }) : null,
      canEdit.value ? h(UButton, {
        color: row.original.raw.isActive ? 'warning' : 'success',
        variant: 'ghost',
        icon: row.original.raw.isActive ? 'i-lucide-user-x' : 'i-lucide-user-check',
        label: row.original.raw.isActive ? 'Deactivate' : 'Activate',
        loading: statusUpdatingId.value === row.original.raw.id,
        onClick: () => toggleUserStatus(row.original.raw)
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
]

const matrixColumns: TableColumn<any>[] = [
  { accessorKey: 'role', header: 'Role' },
  { accessorKey: 'modules', header: 'Module Access' },
  {
    accessorKey: 'entry',
    header: 'Entry',
    cell: ({ row }) => permissionBadge(row.original.entry)
  },
  {
    accessorKey: 'edit',
    header: 'Edit',
    cell: ({ row }) => permissionBadge(row.original.edit)
  },
  {
    accessorKey: 'delete',
    header: 'Delete',
    cell: ({ row }) => permissionBadge(row.original.delete)
  },
  {
    accessorKey: 'adminWorkspace',
    header: 'Admin',
    cell: ({ row }) => permissionBadge(row.original.adminWorkspace)
  },
  { accessorKey: 'notes', header: 'Scope Notes' }
]

function emptyUser() {
  return {
    id: '',
    name: '',
    userName: '',
    email: '',
    password: '',
    role: 5,
    userType: 6,
    companyId: NO_SCOPE,
    storeGroupId: NO_SCOPE,
    storeId: NO_SCOPE,
    isActive: true,
    appOperation: 2
  }
}

async function refresh() {
  if (!auth.isAuthenticated.value) {
    return
  }

  loading.value = true
  loadError.value = ''
  try {
    const [workspaceOptions, userRows, matrix] = await Promise.all([
      api.get<any>('workspace/options'),
      api.get<any[]>('access/users'),
      api.get<any[]>('access/matrix')
    ])

    companies.value = workspaceOptions?.companies || []
    storeGroups.value = workspaceOptions?.storeGroups || []
    stores.value = workspaceOptions?.stores || []
    users.value = userRows
    permissionMatrix.value = matrix
  } catch (error) {
    loadError.value = feedback.errorMessage(error, 'Users and access scopes could not be loaded. Try again.', 'Access refresh failed')
    feedback.failed('Access refresh failed', error)
  } finally {
    loading.value = false
  }
}

function resetForm() {
  Object.assign(form, emptyUser())
  const firstCompany = companies.value[0]
  const firstGroup = storeGroups.value.find((group) => group.companyId === firstCompany?.id) || storeGroups.value[0]
  const firstStore = stores.value.find((store) => store.storeGroupId === firstGroup?.id) || stores.value[0]
  form.companyId = firstCompany?.id || firstStore?.companyId || NO_SCOPE
  form.storeGroupId = firstGroup?.id || firstStore?.storeGroupId || NO_SCOPE
  form.storeId = firstStore?.id || NO_SCOPE
}

function startCreate() {
  resetForm()
  editingUserId.value = ''
  formOpen.value = true
}

function startEdit(user: any) {
  Object.assign(form, {
    id: user.id,
    name: user.name,
    userName: user.userName,
    email: user.email,
    password: '',
    role: roleValue(user.role),
    userType: userTypeValue(user.userType),
    companyId: user.companyId || NO_SCOPE,
    storeGroupId: user.storeGroupId || NO_SCOPE,
    storeId: user.storeId || NO_SCOPE,
    isActive: Boolean(user.isActive),
    appOperation: appOperationValue(user.appOperation)
  })
  editingUserId.value = user.id
  formOpen.value = true
}

function startReset(user: any) {
  pendingReset.value = user
  resetPassword.value = ''
  resetOpen.value = true
}

function askDelete(user: any) {
  pendingDelete.value = user
  deleteOpen.value = true
}

function buildPayload() {
  const selectedStore = stores.value.find((item) => item.id === form.storeId)
  const selectedGroup = storeGroups.value.find((item) => item.id === form.storeGroupId)

  return {
    name: String(form.name || '').trim(),
    userName: String(form.userName || '').trim(),
    email: String(form.email || '').trim(),
    password: editingUserId.value ? null : (String(form.password || '').trim() || null),
    role: Number(form.role),
    userType: Number(form.userType),
    companyId: form.companyId !== NO_SCOPE ? form.companyId : (selectedStore?.companyId || selectedGroup?.companyId || null),
    storeGroupId: form.storeGroupId !== NO_SCOPE ? form.storeGroupId : (selectedStore?.storeGroupId || null),
    storeId: form.storeId !== NO_SCOPE ? form.storeId : null,
    isActive: Boolean(form.isActive),
    appOperation: Number(form.appOperation)
  }
}

async function saveUser() {
  saving.value = true
  try {
    const payload = buildPayload()
    if (editingUserId.value) {
      await api.update<any>('access/users', editingUserId.value, payload)
      feedback.updated('User')
    } else {
      await api.create<any>('access/users', payload)
      feedback.saved('User')
    }

    formOpen.value = false
    await refresh()
  } catch (error) {
    feedback.failed('Could not save user', error)
  } finally {
    saving.value = false
  }
}

async function savePasswordReset() {
  if (!pendingReset.value || !resetPassword.value) {
    feedback.failed('Enter a new password')
    return
  }

  resetting.value = true
  try {
    await api.create<any>(`access/users/${pendingReset.value.id}/reset-password`, {
      newPassword: resetPassword.value
    })
    feedback.updated('Password')
    resetOpen.value = false
    pendingReset.value = null
    await refresh()
  } catch (error) {
    feedback.failed('Could not reset password', error)
  } finally {
    resetting.value = false
  }
}

async function toggleUserStatus(user: any) {
  statusUpdatingId.value = user.id
  try {
    await api.create<any>(`access/users/${user.id}/status`, {
      isActive: !user.isActive
    })
    feedback.updated(user.isActive ? 'User deactivated' : 'User activated')
    await refresh()
  } catch (error) {
    feedback.failed(`Could not ${user.isActive ? 'deactivate' : 'activate'} user`, error)
  } finally {
    statusUpdatingId.value = ''
  }
}

async function confirmDelete() {
  if (!pendingDelete.value) {
    return
  }

  deleting.value = true
  try {
    await api.remove('access/users', pendingDelete.value.id)
    feedback.deleted('User')
    deleteOpen.value = false
    pendingDelete.value = null
    await refresh()
  } catch (error) {
    feedback.failed('Could not delete user', error)
  } finally {
    deleting.value = false
  }
}

function roleValue(label: string) {
  return roleOptions.find((item) => compact(item.label) === compact(label))?.value ?? 5
}

function userTypeValue(label: string) {
  return userTypeOptions.find((item) => compact(item.label) === compact(label))?.value ?? 6
}

function appOperationValue(label: string) {
  return appOperationOptions.find((item) => compact(item.label) === compact(label))?.value ?? 2
}

function roleColor(role: string) {
  const key = compact(role)
  if (key === 'admin' || key === 'poweruser') {
    return 'success'
  }

  if (key.includes('accountant')) {
    return 'warning'
  }

  return key === 'member' ? 'neutral' : 'primary'
}

function permissionBadge(allowed: boolean) {
  return h(UBadge, {
    color: allowed ? 'success' : 'neutral',
    variant: 'subtle'
  }, () => allowed ? 'Allowed' : 'No')
}

function scopeLabel(user: any) {
  if (compact(user.appOperation) === 'all') {
    return 'All'
  }

  const store = stores.value.find((item) => item.id === user.storeId)
  if (store) {
    return store.name || 'Store'
  }

  const group = storeGroups.value.find((item) => item.id === user.storeGroupId)
  if (group) {
    return group.name || 'Store Group'
  }

  const company = companies.value.find((item) => item.id === user.companyId)
  return company?.name || user.appOperation || 'None'
}

function compact(value: string) {
  return String(value || '').replaceAll(' ', '').toLowerCase()
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
    title="Access"
    :companies="companies"
    :stores="stores"
    @refresh="refresh"
  >
    <section class="planner-dashboard">
      <UiModulePageHeader
        title="Access"
        description="Manage user identity, role, account status, password administration, and company/store access scope."
        icon="i-lucide-shield-check"
        primary-label="New User"
        primary-icon="i-lucide-user-plus"
        @primary="startCreate"
      >
        <template #actions>
          <UBadge :color="loading ? 'warning' : 'success'" variant="subtle">
            {{ loading ? 'Loading' : `${users.length} users` }}
          </UBadge>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="subtle" :loading="loading" label="Refresh" @click="refresh" />
        </template>
      </UiModulePageHeader>

      <div class="planner-metric-grid">
        <UCard v-for="metric in metrics" :key="metric.label" class="planner-metric-card">
          <div class="planner-metric-body">
            <UAvatar :icon="metric.icon" :color="metric.color" variant="subtle" />
            <div>
              <p>{{ metric.label }}</p>
              <strong>{{ metric.value }}</strong>
              <span>{{ metric.meta }}</span>
            </div>
          </div>
        </UCard>
      </div>

      <UiRegisterPanel
        title="Users & Roles"
        description="Role-wise access with scoped company and store permissions."
        :loading="loading"
        :error="loadError"
        :empty="!loading && !loadError && filteredRows.length === 0"
        empty-title="No users found"
        empty-description="Create users here after the first admin setup is complete."
        empty-icon="i-lucide-shield-check"
        @retry="refresh"
      >
        <template #actions>
          <UBadge color="neutral" variant="subtle">{{ filteredRows.length }} shown</UBadge>
        </template>

        <UiCrudToolbar
          v-model:search="search"
          search-placeholder="Search user, email, role, or scope"
          :loading="loading"
          refresh-label="Sync"
          create-label="New User"
          @refresh="refresh"
          @create="startCreate"
        />

        <div v-if="filteredRows.length" class="overflow-x-auto">
          <UTable
            :data="filteredRows"
            :columns="columns"
          />
        </div>
      </UiRegisterPanel>

      <UiRegisterPanel
        title="Role Permission Matrix"
        description="Server-enforced access rules for administration, module entry, edit, and delete operations."
        :loading="loading"
        :error="loadError"
        :empty="!loading && !loadError && matrixRows.length === 0"
        empty-title="Permission matrix unavailable"
        empty-description="Refresh to load the current server authorization rules."
        empty-icon="i-lucide-table-properties"
        @retry="refresh"
      >
        <template #actions>
          <UBadge color="primary" variant="subtle">{{ matrixRows.length }} roles</UBadge>
        </template>

        <div v-if="matrixRows.length" class="overflow-x-auto">
          <UTable
            :data="matrixRows"
            :columns="matrixColumns"
          />
        </div>
      </UiRegisterPanel>

      <UiFormSlideover
        v-model:open="formOpen"
        layout="modal"
        :title="editingUserId ? 'Edit User' : 'New User'"
        :description="editingUserId ? 'Update identity, role, status, and workspace scope.' : 'Create an active role-scoped application user.'"
        submit-label="Save User"
        :loading="saving"
        @submit="saveUser"
      >
        <UFormField label="Name" required>
          <UInput v-model="form.name" required />
        </UFormField>
        <div class="form-two-column">
          <UFormField label="Username" required>
            <UInput v-model="form.userName" required />
          </UFormField>
          <UFormField label="Email" required>
            <UInput v-model="form.email" required type="email" />
          </UFormField>
        </div>
        <UFormField v-if="!editingUserId" label="Initial password" required>
          <UInput v-model="form.password" required type="password" autocomplete="new-password" />
        </UFormField>
        <div class="form-two-column">
          <UFormField label="Role">
            <USelect v-model="form.role" :items="roleOptions" />
          </UFormField>
          <UFormField label="User type">
            <USelect v-model="form.userType" :items="userTypeOptions" />
          </UFormField>
        </div>
        <UFormField label="Operation scope">
          <USelect v-model="form.appOperation" :items="appOperationOptions" />
        </UFormField>
        <div class="form-two-column">
          <UFormField label="Company">
            <USelect v-model="form.companyId" :items="companyOptions" />
          </UFormField>
          <UFormField label="Store group">
            <USelect v-model="form.storeGroupId" :items="groupOptions" />
          </UFormField>
        </div>
        <UFormField label="Store">
          <USelect v-model="form.storeId" :items="storeOptions" />
        </UFormField>
        <UCheckbox v-model="form.isActive" label="Active account" />
        <UAlert
          color="neutral"
          variant="subtle"
          icon="i-lucide-shield-check"
          title="Admin access is role-controlled"
          description="Selecting the Admin role sets the internal admin flag automatically. It cannot be enabled separately."
        />
      </UiFormSlideover>

      <UModal v-model:open="resetOpen" title="Reset Password" :ui="{ content: 'max-w-md' }">
        <template #body>
          <div class="modal-stack">
            <UAlert
              color="warning"
              variant="subtle"
              icon="i-lucide-key-round"
              title="Password reset"
              :description="`Set a new password for ${pendingReset?.userName || 'this user'}.`"
            />
            <UFormField label="New password" required>
              <UInput v-model="resetPassword" required type="password" />
            </UFormField>
          </div>
        </template>

        <template #footer>
          <div class="modal-actions">
            <UButton color="neutral" variant="outline" label="Cancel" @click="resetOpen = false" />
            <UButton icon="i-lucide-key-round" label="Reset Password" :loading="resetting" @click="savePasswordReset" />
          </div>
        </template>
      </UModal>

      <UiConfirmDeleteModal
        v-model:open="deleteOpen"
        title="Delete User"
        :description="`Delete user ${pendingDelete?.userName || ''}?`"
        :loading="deleting"
        @confirm="confirmDelete"
      />
    </section>
  </AppShell>
</template>
