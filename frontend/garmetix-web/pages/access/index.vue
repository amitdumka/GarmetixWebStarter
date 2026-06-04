<script setup lang="ts">
import { Pencil, Plus, ShieldCheck, Trash2 } from 'lucide-vue-next'

const api = useGarmetixApi()
const auth = useAuth()
const isAuthenticated = auth.isAuthenticated

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const users = ref<any[]>([])
const loading = ref(false)
const viewMode = ref<'list' | 'create' | 'edit'>('list')
const accessMessage = ref('')
const searchText = ref('')

const roleOptions = [
  { value: 0, label: 'Admin' },
  { value: 1, label: 'Store Manager' },
  { value: 2, label: 'Salesman' },
  { value: 3, label: 'Accountant' },
  { value: 4, label: 'Remote Accountant' },
  { value: 5, label: 'Member' },
  { value: 6, label: 'Power User' }
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

const filteredUsers = computed(() => {
  const query = searchText.value.trim().toLowerCase()
  if (!query) {
    return users.value
  }

  return users.value.filter((user) => {
    return String(user.name || '').toLowerCase().includes(query) ||
      String(user.userName || '').toLowerCase().includes(query) ||
      String(user.email || '').toLowerCase().includes(query) ||
      String(user.role || '').toLowerCase().includes(query)
  })
})

function emptyUser() {
  return {
    id: '',
    name: '',
    userName: '',
    email: '',
    password: '',
    role: 5,
    userType: 6,
    companyId: '',
    storeGroupId: '',
    storeId: '',
    admin: false,
    appOperation: 2
  }
}

async function refresh() {
  if (!auth.isAuthenticated.value) {
    return
  }

  loading.value = true
  try {
    const [companyRows, storeRows, userRows] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores'),
      api.get<any[]>('access/users')
    ])

    companies.value = companyRows
    stores.value = storeRows
    users.value = userRows
  } finally {
    loading.value = false
  }
}

function resetForm() {
  Object.assign(form, emptyUser())
  const firstCompany = companies.value[0]
  const firstStore = stores.value[0]
  form.companyId = firstCompany?.id || ''
  form.storeGroupId = firstStore?.storeGroupId || ''
  form.storeId = firstStore?.id || ''
}

function startCreate() {
  resetForm()
  accessMessage.value = ''
  viewMode.value = 'create'
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
    companyId: user.companyId || '',
    storeGroupId: user.storeGroupId || '',
    storeId: user.storeId || '',
    admin: Boolean(user.admin),
    appOperation: appOperationValue(user.appOperation)
  })
  accessMessage.value = ''
  viewMode.value = 'edit'
}

function buildPayload() {
  const selectedStore = stores.value.find((item) => item.id === form.storeId)

  return {
    name: String(form.name || '').trim(),
    userName: String(form.userName || '').trim(),
    email: String(form.email || '').trim(),
    password: String(form.password || '').trim() || null,
    role: Number(form.role),
    userType: Number(form.userType),
    companyId: form.companyId || selectedStore?.companyId || null,
    storeGroupId: selectedStore?.storeGroupId || form.storeGroupId || null,
    storeId: form.storeId || null,
    admin: Boolean(form.admin) || Number(form.role) === 0,
    appOperation: Number(form.appOperation)
  }
}

async function saveUser() {
  accessMessage.value = ''

  try {
    const payload = buildPayload()
    if (viewMode.value === 'edit' && form.id) {
      await api.update<any>('access/users', form.id, payload)
      accessMessage.value = 'User updated.'
    } else {
      await api.create<any>('access/users', payload)
      accessMessage.value = 'User created.'
    }

    viewMode.value = 'list'
    await refresh()
  } catch (error: any) {
    accessMessage.value = error?.data?.message || error?.message || 'Could not save user.'
  }
}

async function deleteUser(user: any) {
  const confirmed = window.confirm(`Delete user ${user.userName}?`)
  if (!confirmed) {
    return
  }

  try {
    await api.remove('access/users', user.id)
    accessMessage.value = 'User deleted.'
    await refresh()
  } catch (error: any) {
    accessMessage.value = error?.data?.message || error?.message || 'Could not delete user.'
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
    <section class="content">
      <section class="panel">
        <div class="panel-header">
          <h2 class="panel-title">Users & Roles</h2>
          <div class="panel-actions">
            <span class="status" :class="loading ? 'warn' : 'ok'">{{ loading ? 'Loading' : `${users.length} users` }}</span>
            <button class="button secondary" type="button" @click="viewMode = 'list'">
              <ShieldCheck :size="16" />
              List
            </button>
            <button class="button" type="button" @click="startCreate">
              <Plus :size="16" />
              New User
            </button>
          </div>
        </div>

        <div v-if="viewMode === 'list'" class="panel-body">
          <div class="table-toolbar">
            <input v-model="searchText" class="search" aria-label="Search users" placeholder="Search user, email, role" />
            <p v-if="accessMessage" class="inline-message">{{ accessMessage }}</p>
          </div>
          <table class="table">
            <thead>
              <tr>
                <th>Name</th>
                <th>Username</th>
                <th>Email</th>
                <th>Role</th>
                <th>User Type</th>
                <th>Scope</th>
                <th>Admin</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="user in filteredUsers" :key="user.id">
                <td>{{ user.name }}</td>
                <td>{{ user.userName }}</td>
                <td>{{ user.email }}</td>
                <td><span class="status ok">{{ user.role }}</span></td>
                <td>{{ user.userType }}</td>
                <td>{{ user.appOperation }}</td>
                <td><span class="status" :class="user.admin ? 'ok' : 'warn'">{{ user.admin ? 'Yes' : 'No' }}</span></td>
                <td>
                  <button class="button secondary" type="button" @click="startEdit(user)">
                    <Pencil :size="16" />
                    Edit
                  </button>
                  <button class="button danger-button" type="button" @click="deleteUser(user)">
                    <Trash2 :size="16" />
                    Delete
                  </button>
                </td>
              </tr>
              <tr v-if="filteredUsers.length === 0">
                <td colspan="8">No users</td>
              </tr>
            </tbody>
          </table>
        </div>

        <form v-else class="form-grid wide-form" @submit.prevent="saveUser">
          <div class="field">
            <label for="name">Name</label>
            <input id="name" v-model="form.name" required />
          </div>
          <div class="field">
            <label for="userName">Username</label>
            <input id="userName" v-model="form.userName" required />
          </div>
          <div class="field">
            <label for="email">Email</label>
            <input id="email" v-model="form.email" required type="email" />
          </div>
          <div class="field">
            <label for="password">{{ viewMode === 'edit' ? 'New password' : 'Password' }}</label>
            <input id="password" v-model="form.password" :required="viewMode === 'create'" type="password" />
          </div>
          <div class="field">
            <label for="role">Role</label>
            <select id="role" v-model="form.role">
              <option v-for="item in roleOptions" :key="item.value" :value="item.value">{{ item.label }}</option>
            </select>
          </div>
          <div class="field">
            <label for="userType">User type</label>
            <select id="userType" v-model="form.userType">
              <option v-for="item in userTypeOptions" :key="item.value" :value="item.value">{{ item.label }}</option>
            </select>
          </div>
          <div class="field">
            <label for="appOperation">Scope</label>
            <select id="appOperation" v-model="form.appOperation">
              <option v-for="item in appOperationOptions" :key="item.value" :value="item.value">{{ item.label }}</option>
            </select>
          </div>
          <div class="field">
            <label for="company">Company</label>
            <select id="company" v-model="form.companyId">
              <option value="">No company scope</option>
              <option v-for="company in companies" :key="company.id" :value="company.id">{{ company.name }}</option>
            </select>
          </div>
          <div class="field">
            <label for="store">Store</label>
            <select id="store" v-model="form.storeId">
              <option value="">No store scope</option>
              <option v-for="store in stores" :key="store.id" :value="store.id">{{ store.name }}</option>
            </select>
          </div>
          <label class="checkbox-field">
            <input v-model="form.admin" type="checkbox" />
            <span>Admin access</span>
          </label>
          <div class="form-actions">
            <button class="button secondary" type="button" @click="viewMode = 'list'">Cancel</button>
            <button class="button" type="submit">
              <ShieldCheck :size="16" />
              Save User
            </button>
          </div>
          <p v-if="accessMessage" class="setup-message">{{ accessMessage }}</p>
        </form>
      </section>
    </section>
  </AppShell>
</template>
