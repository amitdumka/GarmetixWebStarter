<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-dashboard-kicker">
            <UIcon name="i-lucide-shield-check" class="size-4" />
            Security
          </p>
          <h2 class="garmetix-dashboard-title">Users And Roles</h2>
          <p class="garmetix-dashboard-subtitle">Read-only user list and role matrix review. User create, edit, reset and delete stay in later explicit write stages.</p>
        </div>
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
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

    <section class="grid gap-4 xl:grid-cols-2">
      <div class="garmetix-section-card">
        <div class="mb-3 flex items-center justify-between gap-3">
          <div>
            <h3 class="garmetix-panel-title">Users</h3>
            <p class="garmetix-panel-subtitle">{{ users.length }} user(s)</p>
          </div>
          <UInput v-model="search" icon="i-lucide-search" placeholder="Search users" class="w-64" />
        </div>
        <AdminMasterTable :columns="userColumns" :rows="filteredUsers" empty-text="No users found." />
      </div>

      <div class="garmetix-section-card">
        <div class="mb-3">
          <h3 class="garmetix-panel-title">Role Matrix</h3>
          <p class="garmetix-panel-subtitle">{{ matrixRows.length }} role profile(s)</p>
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
