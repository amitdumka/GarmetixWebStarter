<script setup lang="ts">
import { Pencil, Plus, Trash2, UsersRound } from 'lucide-vue-next'

const api = useGarmetixApi()
const auth = useAuth()
const isAuthenticated = auth.isAuthenticated

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const employees = ref<any[]>([])
const setupStatus = ref<any | null>(null)
const loading = ref(false)
const viewMode = ref<'list' | 'create' | 'edit'>('list')
const hrMessage = ref('')
const searchText = ref('')

const genderOptions = [
  { value: 0, label: 'Male' },
  { value: 1, label: 'Female' },
  { value: 2, label: 'TransGender' }
]

const categoryOptions = [
  { value: 0, label: 'Salesman' },
  { value: 1, label: 'Store Manager' },
  { value: 2, label: 'House Keeping' },
  { value: 3, label: 'Owner' },
  { value: 4, label: 'Accounts' },
  { value: 8, label: 'Others' }
]

const form = reactive<any>(emptyEmployee())

const filteredEmployees = computed(() => {
  const query = searchText.value.trim().toLowerCase()
  if (!query) {
    return employees.value
  }

  return employees.value.filter((employee) => {
    return String(employee.firstName || '').toLowerCase().includes(query) ||
      String(employee.lastName || '').toLowerCase().includes(query) ||
      String(employee.mobile || '').toLowerCase().includes(query) ||
      String(employee.email || '').toLowerCase().includes(query)
  })
})

function emptyEmployee() {
  return {
    id: '',
    title: 'Mr.',
    firstName: '',
    lastName: '',
    gender: 0,
    dateOfBirth: '1990-01-01',
    empId: 0,
    joiningDate: new Date().toISOString().slice(0, 10),
    leavingDate: null,
    working: true,
    category: 0,
    pan: '',
    aadhar: '',
    email: '',
    mobile: ''
  }
}

async function refresh() {
  if (!auth.isAuthenticated.value) {
    return
  }

  loading.value = true
  try {
    setupStatus.value = await api.get<any>('setup/status')
    const [companyRows, storeRows, employeeRows] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores'),
      api.list<any>('employees')
    ])

    companies.value = companyRows
    stores.value = storeRows
    employees.value = employeeRows
  } finally {
    loading.value = false
  }
}

function resetForm() {
  Object.assign(form, emptyEmployee())
}

function startCreate() {
  resetForm()
  hrMessage.value = ''
  viewMode.value = 'create'
}

function startEdit(employee: any) {
  Object.assign(form, {
    ...employee,
    title: employee.title || 'Mr.',
    dateOfBirth: String(employee.dateOfBirth || '1990-01-01').slice(0, 10),
    joiningDate: String(employee.joiningDate || new Date().toISOString()).slice(0, 10),
    leavingDate: employee.leavingDate ? String(employee.leavingDate).slice(0, 10) : null,
    salaryStructures: null,
    attendances: null,
    salaryPayments: null,
    employeeDetails: null
  })
  hrMessage.value = ''
  viewMode.value = 'edit'
}

function buildPayload() {
  const companyId = setupStatus.value?.companyId || companies.value[0]?.id
  const storeGroupId = setupStatus.value?.storeGroupId || stores.value[0]?.storeGroupId
  const storeId = setupStatus.value?.storeId || stores.value[0]?.id

  if (!companyId || !storeGroupId || !storeId) {
    throw new Error('Run quick setup before saving employees.')
  }

  return {
    ...form,
    title: String(form.title || '').trim(),
    firstName: String(form.firstName || '').trim(),
    lastName: String(form.lastName || '').trim(),
    gender: Number(form.gender),
    dateOfBirth: new Date(form.dateOfBirth).toISOString(),
    empId: Number(form.empId || 0),
    joiningDate: new Date(form.joiningDate).toISOString(),
    leavingDate: form.leavingDate ? new Date(form.leavingDate).toISOString() : null,
    working: Boolean(form.working),
    category: Number(form.category),
    pan: String(form.pan || '').trim() || null,
    aadhar: String(form.aadhar || '').trim(),
    email: String(form.email || '').trim() || null,
    mobile: String(form.mobile || '').trim(),
    companyId,
    storeGroupId,
    storeId
  }
}

async function saveEmployee() {
  hrMessage.value = ''

  try {
    const payload = buildPayload()
    if (viewMode.value === 'edit' && form.id) {
      await api.update<any>('employees', form.id, payload)
      hrMessage.value = 'Employee updated.'
    } else {
      await api.create<any>('employees', payload)
      hrMessage.value = 'Employee saved.'
    }

    viewMode.value = 'list'
    await refresh()
  } catch (error: any) {
    hrMessage.value = error?.data?.message || error?.message || 'Could not save employee.'
  }
}

async function deleteEmployee(employee: any) {
  const confirmed = window.confirm(`Delete employee ${employee.firstName} ${employee.lastName}?`)
  if (!confirmed) {
    return
  }

  await api.remove('employees', employee.id)
  hrMessage.value = 'Employee deleted.'
  await refresh()
}

function genderLabel(value: number) {
  return genderOptions.find((item) => item.value === Number(value))?.label || 'Gender'
}

function categoryLabel(value: number) {
  return categoryOptions.find((item) => item.value === Number(value))?.label || 'Employee'
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
    title="HR"
    :companies="companies"
    :stores="stores"
    @refresh="refresh"
  >
    <section class="content">
      <section class="panel">
        <div class="panel-header">
          <h2 class="panel-title">Employees</h2>
          <div class="panel-actions">
            <span class="status" :class="loading ? 'warn' : 'ok'">{{ loading ? 'Loading' : `${employees.length} employees` }}</span>
            <button class="button secondary" type="button" @click="viewMode = 'list'">
              <UsersRound :size="16" />
              List
            </button>
            <button class="button" type="button" @click="startCreate">
              <Plus :size="16" />
              New Employee
            </button>
          </div>
        </div>

        <div v-if="viewMode === 'list'" class="panel-body">
          <div class="table-toolbar">
            <input v-model="searchText" class="search" aria-label="Search employees" placeholder="Search employee, mobile, email" />
            <p v-if="hrMessage" class="inline-message">{{ hrMessage }}</p>
          </div>
          <table class="table">
            <thead>
              <tr>
                <th>Employee</th>
                <th>Mobile</th>
                <th>Email</th>
                <th>Category</th>
                <th>Joining</th>
                <th>Status</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="employee in filteredEmployees" :key="employee.id">
                <td>{{ employee.title }} {{ employee.firstName }} {{ employee.lastName }}</td>
                <td>{{ employee.mobile }}</td>
                <td>{{ employee.email }}</td>
                <td>{{ categoryLabel(employee.category) }}</td>
                <td>{{ new Date(employee.joiningDate).toLocaleDateString() }}</td>
                <td><span class="status" :class="employee.working ? 'ok' : 'warn'">{{ employee.working ? 'Working' : 'Inactive' }}</span></td>
                <td>
                  <button class="button secondary" type="button" @click="startEdit(employee)">
                    <Pencil :size="16" />
                    Edit
                  </button>
                  <button class="button danger-button" type="button" @click="deleteEmployee(employee)">
                    <Trash2 :size="16" />
                    Delete
                  </button>
                </td>
              </tr>
              <tr v-if="filteredEmployees.length === 0">
                <td colspan="7">No employees</td>
              </tr>
            </tbody>
          </table>
        </div>

        <form v-else class="form-grid wide-form" @submit.prevent="saveEmployee">
          <div class="field">
            <label for="title">Title</label>
            <input id="title" v-model="form.title" />
          </div>
          <div class="field">
            <label for="firstName">First name</label>
            <input id="firstName" v-model="form.firstName" required />
          </div>
          <div class="field">
            <label for="lastName">Last name</label>
            <input id="lastName" v-model="form.lastName" required />
          </div>
          <div class="field">
            <label for="gender">Gender</label>
            <select id="gender" v-model="form.gender">
              <option v-for="item in genderOptions" :key="item.value" :value="item.value">{{ item.label }}</option>
            </select>
          </div>
          <div class="field">
            <label for="dateOfBirth">Date of birth</label>
            <input id="dateOfBirth" v-model="form.dateOfBirth" required type="date" />
          </div>
          <div class="field">
            <label for="joiningDate">Joining date</label>
            <input id="joiningDate" v-model="form.joiningDate" required type="date" />
          </div>
          <div class="field">
            <label for="category">Category</label>
            <select id="category" v-model="form.category">
              <option v-for="item in categoryOptions" :key="item.value" :value="item.value">{{ item.label }}</option>
            </select>
          </div>
          <div class="field">
            <label for="empId">Employee ID</label>
            <input id="empId" v-model="form.empId" min="0" type="number" />
          </div>
          <div class="field">
            <label for="mobile">Mobile</label>
            <input id="mobile" v-model="form.mobile" required />
          </div>
          <div class="field">
            <label for="email">Email</label>
            <input id="email" v-model="form.email" type="email" />
          </div>
          <div class="field">
            <label for="aadhar">Aadhar</label>
            <input id="aadhar" v-model="form.aadhar" required />
          </div>
          <div class="field">
            <label for="pan">PAN</label>
            <input id="pan" v-model="form.pan" />
          </div>
          <label class="checkbox-field">
            <input v-model="form.working" type="checkbox" />
            <span>Working</span>
          </label>
          <div class="form-actions">
            <button class="button secondary" type="button" @click="viewMode = 'list'">Cancel</button>
            <button class="button" type="submit">
              <UsersRound :size="16" />
              Save Employee
            </button>
          </div>
          <p v-if="hrMessage" class="setup-message">{{ hrMessage }}</p>
        </form>
      </section>
    </section>
  </AppShell>
</template>
