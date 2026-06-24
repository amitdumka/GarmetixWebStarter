<template>
  <section class="space-y-4">
    <div class="border border-default bg-muted/10 p-5">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="text-sm text-muted">Security</p>
          <h2 class="mt-1 text-2xl font-semibold">Users And Roles</h2>
          <p class="mt-2 max-w-3xl text-sm text-muted">Read-only user list and role matrix review. User create, edit, reset and delete stay in later explicit write stages.</p>
        </div>
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
      </div>
    </div>

    <UAlert v-if="error" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />

    <section class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
      <div v-for="card in cards" :key="card.label" class="border border-default bg-muted/20 p-4">
        <p class="text-sm text-muted">{{ card.label }}</p>
        <p class="mt-2 text-2xl font-semibold">{{ card.value }}</p>
        <p class="mt-1 text-xs text-muted">{{ card.detail }}</p>
      </div>
    </section>

    <section class="grid gap-4 xl:grid-cols-2">
      <div class="border border-default bg-muted/10 p-4">
        <div class="mb-3 flex items-center justify-between gap-3">
          <div>
            <h3 class="text-base font-semibold">Users</h3>
            <p class="text-xs text-muted">{{ users.length }} user(s)</p>
          </div>
          <UInput v-model="search" icon="i-lucide-search" placeholder="Search users" class="w-64" />
        </div>
        <AdminMasterTable :columns="userColumns" :rows="filteredUsers" empty-text="No users found." />
      </div>

      <div class="border border-default bg-muted/10 p-4">
        <div class="mb-3">
          <h3 class="text-base font-semibold">Role Matrix</h3>
          <p class="text-xs text-muted">{{ matrixRows.length }} role profile(s)</p>
        </div>
        <AdminMasterTable :columns="matrixColumns" :rows="matrixRows" empty-text="No role matrix returned." />
      </div>
    </section>
  </section>
</template>

<script setup lang="ts">
import { readArray, readText, toRows, type ApiRecord, useAdminApiClient } from '../utils/admin-api'

useHead({ title: 'Users And Roles - Garmetix Admin' })

const { get } = useAdminApiClient()
const loading = ref(true)
const error = ref('')
const search = ref('')
const users = ref<ApiRecord[]>([])
const matrix = ref<ApiRecord[]>([])
const userColumns = [
  { key: 'name', label: 'Name' },
  { key: 'userName', label: 'Username' },
  { key: 'role', label: 'Role' },
  { key: 'scope', label: 'Scope' },
  { key: 'status', label: 'Status' }
]
const matrixColumns = [
  { key: 'role', label: 'Role' },
  { key: 'modules', label: 'Modules' },
  { key: 'edit', label: 'Edit' },
  { key: 'delete', label: 'Delete' }
]
const cards = computed(() => [
  { label: 'Users', value: users.value.length, detail: 'Visible users' },
  { label: 'Active Users', value: users.value.filter(item => item.isActive !== false).length, detail: 'Can login' },
  { label: 'Admins', value: users.value.filter(item => item.admin || readText(item, ['role']).toLowerCase().includes('admin')).length, detail: 'Privileged accounts' },
  { label: 'Roles', value: matrix.value.length, detail: 'Access profiles' }
])
const userRows = computed(() => users.value.map(item => ({
  name: readText(item, ['name']),
  userName: readText(item, ['userName']),
  role: readText(item, ['role', 'userType']),
  scope: item.storeId ? 'Store' : item.storeGroupId ? 'Store Group' : item.companyId ? 'Company' : 'Global',
  status: item.isActive === false ? 'Inactive' : 'Active'
})))
const filteredUsers = computed(() => {
  const term = search.value.trim().toLowerCase()
  if (!term) return userRows.value
  return userRows.value.filter(row => JSON.stringify(row).toLowerCase().includes(term))
})
const matrixRows = computed(() => matrix.value.map(item => ({
  role: readText(item, ['role', 'name', 'key']),
  modules: readArray(item, ['modules', 'policies', 'routes']).slice(0, 6).map(row => readText(row, ['name', 'policy', 'label'], String(row))).join(', '),
  edit: readText(item, ['canEdit', 'edit'], '-'),
  delete: readText(item, ['canDelete', 'delete'], '-')
})))

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const [userData, matrixData] = await Promise.allSettled([
      get<unknown>('access/users'),
      get<unknown>('access/matrix')
    ])
    if (userData.status === 'fulfilled') users.value = toRows(userData.value)
    if (matrixData.status === 'fulfilled') matrix.value = toRows(matrixData.value)
    const failed = [userData, matrixData].filter(item => item.status === 'rejected').length
    if (failed) error.value = `${failed} access request(s) could not be loaded.`
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load access data.'
  } finally {
    loading.value = false
  }
}

onMounted(refresh)
</script>
