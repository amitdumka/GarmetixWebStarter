<script setup lang="ts">
import {
  Banknote,
  Boxes,
  Building2,
  PackagePlus,
  ReceiptIndianRupee,
  UsersRound
} from 'lucide-vue-next'

const api = useGarmetixApi()
const auth = useAuth()
const isAuthenticated = auth.isAuthenticated

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const products = ref<any[]>([])
const invoices = ref<any[]>([])
const vouchers = ref<any[]>([])
const employees = ref<any[]>([])
const loading = ref(false)
const setupStatus = ref<any | null>(null)
const setupMessage = ref('')

const setupForm = reactive({
  companyName: 'Garmetix Company',
  storeGroupName: 'Main Group',
  storeName: 'Main Store',
  contactNumber: '',
  email: 'admin@garmetix.local',
  city: 'Dumka',
  state: 'Jharkhand',
  zipCode: '814101'
})

const metrics = computed(() => [
  { label: 'Companies', value: companies.value.length },
  { label: 'Stores', value: stores.value.length },
  { label: 'Products', value: products.value.length },
  { label: 'Sales Invoices', value: invoices.value.length }
])

const moduleCards = computed(() => [
  { to: '/billing', label: 'Billing', count: invoices.value.length, icon: ReceiptIndianRupee },
  { to: '/inventory', label: 'Inventory', count: products.value.length, icon: Boxes },
  { to: '/purchase', label: 'Purchase', count: products.value.length, icon: PackagePlus },
  { to: '/vouchers', label: 'Vouchers', count: vouchers.value.length, icon: Banknote },
  { to: '/hr', label: 'HR', count: employees.value.length, icon: UsersRound },
  { to: '/setup', label: 'Company Setup', count: companies.value.length + stores.value.length, icon: Building2 }
])

const needsSetup = computed(() => {
  return setupStatus.value && (!setupStatus.value.hasCompany || !setupStatus.value.hasStore || !setupStatus.value.hasProductCategory || !setupStatus.value.hasTax)
})

async function refresh() {
  if (!auth.isAuthenticated.value) {
    return
  }

  loading.value = true
  try {
    setupStatus.value = await api.get<any>('setup/status')
    const [companyRows, storeRows, productRows, invoiceRows, voucherRows, employeeRows] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores'),
      api.list<any>('products'),
      api.get<any[]>('billing/sales/recent'),
      api.list<any>('vouchers'),
      api.list<any>('employees')
    ])

    companies.value = companyRows
    stores.value = storeRows
    products.value = productRows
    invoices.value = invoiceRows
    vouchers.value = voucherRows
    employees.value = employeeRows
  } finally {
    loading.value = false
  }
}

async function quickSetup() {
  setupMessage.value = ''
  const result = await api.create<any>('setup/quick-start', setupForm)
  setupStatus.value = {
    hasCompany: true,
    hasStoreGroup: true,
    hasStore: true,
    hasProductCategory: true,
    hasTax: true,
    companyId: result.companyId,
    storeGroupId: result.storeGroupId,
    storeId: result.storeId
  }
  setupMessage.value = 'Company, store, product category, and GST tax are ready.'
  await refresh()
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
    title="Dashboard"
    :companies="companies"
    :stores="stores"
    @refresh="refresh"
  >
    <section class="content">
      <div class="metric-grid">
        <article v-for="metric in metrics" :key="metric.label" class="metric">
          <p class="metric-label">{{ metric.label }}</p>
          <p class="metric-value">{{ metric.value }}</p>
        </article>
      </div>

      <section v-if="needsSetup" class="panel setup-panel">
        <div class="panel-header">
          <h2 class="panel-title">Quick Setup</h2>
          <span class="status warn">Required</span>
        </div>
        <form class="setup-grid" @submit.prevent="quickSetup">
          <div class="field">
            <label for="setupCompany">Company</label>
            <input id="setupCompany" v-model="setupForm.companyName" required />
          </div>
          <div class="field">
            <label for="setupGroup">Store group</label>
            <input id="setupGroup" v-model="setupForm.storeGroupName" required />
          </div>
          <div class="field">
            <label for="setupStore">Store</label>
            <input id="setupStore" v-model="setupForm.storeName" required />
          </div>
          <div class="field">
            <label for="setupMobile">Contact</label>
            <input id="setupMobile" v-model="setupForm.contactNumber" />
          </div>
          <div class="field">
            <label for="setupEmail">Email</label>
            <input id="setupEmail" v-model="setupForm.email" type="email" />
          </div>
          <div class="field">
            <label for="setupCity">City</label>
            <input id="setupCity" v-model="setupForm.city" required />
          </div>
          <div class="field">
            <label for="setupState">State</label>
            <input id="setupState" v-model="setupForm.state" required />
          </div>
          <div class="field">
            <label for="setupZip">Zip</label>
            <input id="setupZip" v-model="setupForm.zipCode" required />
          </div>
          <button class="button" type="submit">
            <Building2 :size="16" />
            Create Setup
          </button>
        </form>
        <p v-if="setupMessage" class="setup-message">{{ setupMessage }}</p>
      </section>

      <section class="panel">
        <div class="panel-header">
          <h2 class="panel-title">Modules</h2>
          <span class="status" :class="loading ? 'warn' : 'ok'">{{ loading ? 'Loading' : 'Ready' }}</span>
        </div>
        <div class="module-grid">
          <NuxtLink v-for="item in moduleCards" :key="item.to" class="module-card" :to="item.to">
            <component :is="item.icon" :size="22" />
            <span>{{ item.label }}</span>
            <strong>{{ item.count }}</strong>
          </NuxtLink>
        </div>
      </section>
    </section>
  </AppShell>
</template>
