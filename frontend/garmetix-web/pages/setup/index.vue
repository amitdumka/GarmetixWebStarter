<script setup lang="ts">
import { Building2, Pencil, Plus, Store, Trash2 } from 'lucide-vue-next'

const api = useGarmetixApi()
const auth = useAuth()
const isAuthenticated = auth.isAuthenticated

const companies = ref<any[]>([])
const storeGroups = ref<any[]>([])
const stores = ref<any[]>([])
const loading = ref(false)
const activeTab = ref<'companies' | 'groups' | 'stores'>('companies')
const viewMode = ref<'list' | 'form'>('list')
const setupMessage = ref('')
const editingId = ref('')

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

const companyForm = reactive<any>(emptyCompany())
const groupForm = reactive<any>(emptyStoreGroup())
const storeForm = reactive<any>(emptyStore())

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
  } finally {
    loading.value = false
  }
}

function showTab(tab: 'companies' | 'groups' | 'stores') {
  activeTab.value = tab
  viewMode.value = 'list'
  setupMessage.value = ''
  editingId.value = ''
}

function startCreate() {
  editingId.value = ''
  setupMessage.value = ''

  if (activeTab.value === 'companies') {
    Object.assign(companyForm, emptyCompany())
  } else if (activeTab.value === 'groups') {
    Object.assign(groupForm, emptyStoreGroup())
  } else {
    Object.assign(storeForm, emptyStore())
  }

  viewMode.value = 'form'
}

function startEdit(item: any) {
  editingId.value = item.id
  setupMessage.value = ''

  if (activeTab.value === 'companies') {
    Object.assign(companyForm, {
      ...item,
      startDate: String(item.startDate || new Date().toISOString()).slice(0, 10),
      endDate: item.endDate ? String(item.endDate).slice(0, 10) : ''
    })
  } else if (activeTab.value === 'groups') {
    Object.assign(groupForm, {
      ...item,
      company: null,
      startDate: String(item.startDate || new Date().toISOString()).slice(0, 10),
      endDate: item.endDate ? String(item.endDate).slice(0, 10) : ''
    })
  } else {
    Object.assign(storeForm, {
      ...item,
      company: null,
      storeGroup: null,
      startDate: String(item.startDate || new Date().toISOString()).slice(0, 10),
      endDate: item.endDate ? String(item.endDate).slice(0, 10) : ''
    })
  }

  viewMode.value = 'form'
}

async function saveCurrent() {
  setupMessage.value = ''

  try {
    if (activeTab.value === 'companies') {
      const payload = buildCompany()
      if (editingId.value) {
        await api.update<any>('companies', editingId.value, payload)
        setupMessage.value = 'Company updated.'
      } else {
        await api.create<any>('companies', payload)
        setupMessage.value = 'Company saved.'
      }
    } else if (activeTab.value === 'groups') {
      const payload = buildStoreGroup()
      if (editingId.value) {
        await api.update<any>('store-groups', editingId.value, payload)
        setupMessage.value = 'Store group updated.'
      } else {
        await api.create<any>('store-groups', payload)
        setupMessage.value = 'Store group saved.'
      }
    } else {
      const payload = buildStore()
      if (editingId.value) {
        await api.update<any>('stores', editingId.value, payload)
        setupMessage.value = 'Store updated.'
      } else {
        await api.create<any>('stores', payload)
        setupMessage.value = 'Store saved.'
      }
    }

    viewMode.value = 'list'
    await refresh()
  } catch (error: any) {
    setupMessage.value = error?.data?.message || error?.message || 'Could not save setup record.'
  }
}

function buildCompany() {
  return {
    ...companyForm,
    name: String(companyForm.name || '').trim(),
    code: String(companyForm.code || '').trim(),
    startDate: new Date(companyForm.startDate).toISOString(),
    endDate: companyForm.endDate ? new Date(companyForm.endDate).toISOString() : null,
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
    startDate: new Date(groupForm.startDate).toISOString(),
    endDate: groupForm.endDate ? new Date(groupForm.endDate).toISOString() : null,
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
    startDate: new Date(storeForm.startDate).toISOString(),
    endDate: storeForm.endDate ? new Date(storeForm.endDate).toISOString() : null,
    active: Boolean(storeForm.active),
    storeCategory: Number(storeForm.storeCategory),
    companyId: storeForm.companyId,
    storeGroupId: storeForm.storeGroupId,
    company: null,
    storeGroup: null
  }
}

async function deleteCurrent(item: any) {
  const label = activeTab.value === 'companies' ? item.name : activeTab.value === 'groups' ? item.name : item.name
  const confirmed = window.confirm(`Delete ${label}?`)
  if (!confirmed) {
    return
  }

  const resource = activeTab.value === 'companies'
    ? 'companies'
    : activeTab.value === 'groups' ? 'store-groups' : 'stores'

  await api.remove(resource, item.id)
  setupMessage.value = 'Record deleted.'
  await refresh()
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

onMounted(async () => {
  auth.restore()
  await refresh()
})
</script>

<template>
  <AuthScreen v-if="!isAuthenticated" @authenticated="refresh" />

  <AppShell
    v-else
    title="Setup"
    :companies="companies"
    :stores="stores"
    @refresh="refresh"
  >
    <section class="content">
      <section class="panel">
        <div class="panel-header">
          <h2 class="panel-title">Company & Stores</h2>
          <div class="panel-actions">
            <span class="status" :class="loading ? 'warn' : 'ok'">{{ loading ? 'Loading' : 'Ready' }}</span>
            <button class="button secondary" type="button" @click="showTab('companies')">Companies</button>
            <button class="button secondary" type="button" @click="showTab('groups')">Store Groups</button>
            <button class="button secondary" type="button" @click="showTab('stores')">Stores</button>
            <button class="button" type="button" @click="startCreate">
              <Plus :size="16" />
              New
            </button>
          </div>
        </div>

        <div v-if="viewMode === 'list'" class="panel-body">
          <div class="table-toolbar">
            <p v-if="setupMessage" class="inline-message">{{ setupMessage }}</p>
          </div>

          <table v-if="activeTab === 'companies'" class="table">
            <thead>
              <tr>
                <th>Company</th>
                <th>Code</th>
                <th>Contact</th>
                <th>City</th>
                <th>Type</th>
                <th>Status</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="company in companies" :key="company.id">
                <td>{{ company.name }}</td>
                <td>{{ company.code }}</td>
                <td>{{ company.contactNumber }}</td>
                <td>{{ company.city }}</td>
                <td>{{ companyTypeLabel(company.companyType) }}</td>
                <td><span class="status" :class="company.active ? 'ok' : 'warn'">{{ company.active ? 'Active' : 'Inactive' }}</span></td>
                <td>
                  <button class="button secondary" type="button" @click="startEdit(company)">
                    <Pencil :size="16" />
                    Edit
                  </button>
                  <button class="button danger-button" type="button" @click="deleteCurrent(company)">
                    <Trash2 :size="16" />
                    Delete
                  </button>
                </td>
              </tr>
              <tr v-if="companies.length === 0">
                <td colspan="7">No companies</td>
              </tr>
            </tbody>
          </table>

          <table v-else-if="activeTab === 'groups'" class="table">
            <thead>
              <tr>
                <th>Group</th>
                <th>Code</th>
                <th>Company</th>
                <th>Category</th>
                <th>Status</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="group in storeGroups" :key="group.id">
                <td>{{ group.name }}</td>
                <td>{{ group.groupCode }}</td>
                <td>{{ companyName(group.companyId) }}</td>
                <td>{{ categoryLabel(group.storeCategory) }}</td>
                <td><span class="status" :class="group.active ? 'ok' : 'warn'">{{ group.active ? 'Active' : 'Inactive' }}</span></td>
                <td>
                  <button class="button secondary" type="button" @click="startEdit(group)">
                    <Pencil :size="16" />
                    Edit
                  </button>
                  <button class="button danger-button" type="button" @click="deleteCurrent(group)">
                    <Trash2 :size="16" />
                    Delete
                  </button>
                </td>
              </tr>
              <tr v-if="storeGroups.length === 0">
                <td colspan="6">No store groups</td>
              </tr>
            </tbody>
          </table>

          <table v-else class="table">
            <thead>
              <tr>
                <th>Store</th>
                <th>Code</th>
                <th>Company</th>
                <th>Group</th>
                <th>City</th>
                <th>Status</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="store in stores" :key="store.id">
                <td>{{ store.name }}</td>
                <td>{{ store.storeCode }}</td>
                <td>{{ companyName(store.companyId) }}</td>
                <td>{{ groupName(store.storeGroupId) }}</td>
                <td>{{ store.city }}</td>
                <td><span class="status" :class="store.active ? 'ok' : 'warn'">{{ store.active ? 'Active' : 'Inactive' }}</span></td>
                <td>
                  <button class="button secondary" type="button" @click="startEdit(store)">
                    <Pencil :size="16" />
                    Edit
                  </button>
                  <button class="button danger-button" type="button" @click="deleteCurrent(store)">
                    <Trash2 :size="16" />
                    Delete
                  </button>
                </td>
              </tr>
              <tr v-if="stores.length === 0">
                <td colspan="7">No stores</td>
              </tr>
            </tbody>
          </table>
        </div>

        <form v-else-if="activeTab === 'companies'" class="form-grid wide-form" @submit.prevent="saveCurrent">
          <div class="field">
            <label for="companyName">Name</label>
            <input id="companyName" v-model="companyForm.name" required />
          </div>
          <div class="field">
            <label for="companyCode">Code</label>
            <input id="companyCode" v-model="companyForm.code" />
          </div>
          <div class="field">
            <label for="companyStart">Start date</label>
            <input id="companyStart" v-model="companyForm.startDate" type="date" />
          </div>
          <div class="field">
            <label for="companyType">Company type</label>
            <select id="companyType" v-model="companyForm.companyType">
              <option v-for="item in companyTypeOptions" :key="item.value" :value="item.value">{{ item.label }}</option>
            </select>
          </div>
          <div class="field">
            <label for="companyContact">Contact</label>
            <input id="companyContact" v-model="companyForm.contactNumber" />
          </div>
          <div class="field">
            <label for="companyEmail">Email</label>
            <input id="companyEmail" v-model="companyForm.email" type="email" />
          </div>
          <div class="field">
            <label for="companyCity">City</label>
            <input id="companyCity" v-model="companyForm.city" />
          </div>
          <div class="field">
            <label for="companyState">State</label>
            <input id="companyState" v-model="companyForm.state" />
          </div>
          <div class="field">
            <label for="companyGstin">GSTIN</label>
            <input id="companyGstin" v-model="companyForm.gstin" />
          </div>
          <div class="field">
            <label for="companyPan">PAN</label>
            <input id="companyPan" v-model="companyForm.pan" />
          </div>
          <label class="checkbox-field">
            <input v-model="companyForm.active" type="checkbox" />
            <span>Active</span>
          </label>
          <div class="form-actions">
            <button class="button secondary" type="button" @click="viewMode = 'list'">Cancel</button>
            <button class="button" type="submit">
              <Building2 :size="16" />
              Save Company
            </button>
          </div>
          <p v-if="setupMessage" class="setup-message">{{ setupMessage }}</p>
        </form>

        <form v-else-if="activeTab === 'groups'" class="form-grid wide-form" @submit.prevent="saveCurrent">
          <div class="field">
            <label for="groupName">Name</label>
            <input id="groupName" v-model="groupForm.name" required />
          </div>
          <div class="field">
            <label for="groupCode">Code</label>
            <input id="groupCode" v-model="groupForm.groupCode" />
          </div>
          <div class="field">
            <label for="groupCompany">Company</label>
            <select id="groupCompany" v-model="groupForm.companyId" required>
              <option value="">Select company</option>
              <option v-for="company in companies" :key="company.id" :value="company.id">{{ company.name }}</option>
            </select>
          </div>
          <div class="field">
            <label for="groupCategory">Category</label>
            <select id="groupCategory" v-model="groupForm.storeCategory">
              <option v-for="item in storeCategoryOptions" :key="item.value" :value="item.value">{{ item.label }}</option>
            </select>
          </div>
          <label class="checkbox-field">
            <input v-model="groupForm.active" type="checkbox" />
            <span>Active</span>
          </label>
          <div class="form-actions">
            <button class="button secondary" type="button" @click="viewMode = 'list'">Cancel</button>
            <button class="button" type="submit">
              <Building2 :size="16" />
              Save Group
            </button>
          </div>
          <p v-if="setupMessage" class="setup-message">{{ setupMessage }}</p>
        </form>

        <form v-else class="form-grid wide-form" @submit.prevent="saveCurrent">
          <div class="field">
            <label for="storeName">Name</label>
            <input id="storeName" v-model="storeForm.name" required />
          </div>
          <div class="field">
            <label for="storeCode">Code</label>
            <input id="storeCode" v-model="storeForm.storeCode" />
          </div>
          <div class="field">
            <label for="storeCompany">Company</label>
            <select id="storeCompany" v-model="storeForm.companyId" required>
              <option value="">Select company</option>
              <option v-for="company in companies" :key="company.id" :value="company.id">{{ company.name }}</option>
            </select>
          </div>
          <div class="field">
            <label for="storeGroup">Store group</label>
            <select id="storeGroup" v-model="storeForm.storeGroupId" required>
              <option value="">Select group</option>
              <option v-for="group in storeGroups" :key="group.id" :value="group.id">{{ group.name }}</option>
            </select>
          </div>
          <div class="field">
            <label for="storeContact">Contact</label>
            <input id="storeContact" v-model="storeForm.contactNumber" />
          </div>
          <div class="field">
            <label for="storeEmail">Email</label>
            <input id="storeEmail" v-model="storeForm.email" type="email" />
          </div>
          <div class="field">
            <label for="storeAddress">Address</label>
            <input id="storeAddress" v-model="storeForm.address" />
          </div>
          <div class="field">
            <label for="storeCity">City</label>
            <input id="storeCity" v-model="storeForm.city" />
          </div>
          <div class="field">
            <label for="storeState">State</label>
            <input id="storeState" v-model="storeForm.state" />
          </div>
          <div class="field">
            <label for="storeZip">Zip</label>
            <input id="storeZip" v-model="storeForm.zipCode" />
          </div>
          <label class="checkbox-field">
            <input v-model="storeForm.active" type="checkbox" />
            <span>Active</span>
          </label>
          <div class="form-actions">
            <button class="button secondary" type="button" @click="viewMode = 'list'">Cancel</button>
            <button class="button" type="submit">
              <Store :size="16" />
              Save Store
            </button>
          </div>
          <p v-if="setupMessage" class="setup-message">{{ setupMessage }}</p>
        </form>
      </section>
    </section>
  </AppShell>
</template>
