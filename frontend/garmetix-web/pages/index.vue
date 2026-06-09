<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const feedback = useUiFeedback()
const isAuthenticated = auth.isAuthenticated

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const products = ref<any[]>([])
const customers = ref<any[]>([])
const vendors = ref<any[]>([])
const invoices = ref<any[]>([])
const vouchers = ref<any[]>([])
const employees = ref<any[]>([])
const loading = ref(false)
const setupStatus = ref<any | null>(null)
const setupMessage = ref('')
const canSeeAdmin = auth.canSeeAdmin

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

const needsSetup = computed(() => {
  return setupStatus.value && (!setupStatus.value.hasCompany || !setupStatus.value.hasStore || !setupStatus.value.hasProductCategory || !setupStatus.value.hasTax)
})

const setupProgress = computed(() => {
  if (!setupStatus.value) {
    return 0
  }

  const checks = [
    setupStatus.value.hasCompany,
    setupStatus.value.hasStoreGroup,
    setupStatus.value.hasStore,
    setupStatus.value.hasProductCategory,
    setupStatus.value.hasTax
  ]

  return Math.round((checks.filter(Boolean).length / checks.length) * 100)
})

const setupChecks = computed(() => [
  { label: 'Company', ready: Boolean(setupStatus.value?.hasCompany) },
  { label: 'Store group', ready: Boolean(setupStatus.value?.hasStoreGroup) },
  { label: 'Store', ready: Boolean(setupStatus.value?.hasStore) },
  { label: 'Product category', ready: Boolean(setupStatus.value?.hasProductCategory) },
  { label: 'Tax', ready: Boolean(setupStatus.value?.hasTax) }
])

const totalSales = computed(() => invoices.value.reduce((sum, invoice) => sum + moneyValue(invoice), 0))

const metrics = computed(() => [
  {
    label: 'Sales',
    value: money(totalSales.value),
    meta: `${invoices.value.length} invoices`,
    icon: 'i-lucide-receipt-indian-rupee',
    color: 'primary'
  },
  {
    label: 'Inventory',
    value: products.value.length,
    meta: `${stores.value.length} stores`,
    icon: 'i-lucide-boxes',
    color: 'success'
  },
  {
    label: 'Vouchers',
    value: vouchers.value.length,
    meta: 'payments, receipts, expenses',
    icon: 'i-lucide-banknote',
    color: 'warning'
  },
  {
    label: 'People',
    value: employees.value.length,
    meta: 'HR and payroll records',
    icon: 'i-lucide-users-round',
    color: 'neutral'
  }
])

const moduleCards = computed(() => {
  const items = [
    { to: '/billing', label: 'Billing', count: invoices.value.length, icon: 'i-lucide-receipt-indian-rupee', status: 'Live' },
    { to: '/sales-return', label: 'Sales Return', count: invoices.value.length, icon: 'i-lucide-rotate-ccw', status: 'Credit Note' },
    { to: '/inventory', label: 'Inventory', count: products.value.length, icon: 'i-lucide-boxes', status: 'Live' },
    { to: '/purchase', label: 'Purchase', count: products.value.length, icon: 'i-lucide-package-plus', status: 'Live' },
    { to: '/purchase-return', label: 'Purchase Return', count: products.value.length, icon: 'i-lucide-undo-2', status: 'Debit Note' },
    { to: '/parties', label: 'Parties', count: customers.value.length + vendors.value.length, icon: 'i-lucide-users-round', status: 'GSTIN' },
    { to: '/vouchers', label: 'Vouchers', count: vouchers.value.length, icon: 'i-lucide-banknote', status: 'Live' },
    { to: '/hr', label: 'HR', count: employees.value.length, icon: 'i-lucide-users-round', status: 'Attendance' }
  ]

  if (canSeeAdmin.value) {
    items.push({ to: '/setup', label: 'Company', count: companies.value.length + stores.value.length, icon: 'i-lucide-building-2', status: needsSetup.value ? 'Required' : 'Ready' })
  }

  return items
})

const currentWork = computed(() => [
  { title: 'Nuxt UI dashboard shell', status: 'Done', type: 'Frontend', owner: 'Codex', due: 'Stage 1' },
  { title: 'Reusable CRUD components', status: 'Done', type: 'Frontend', owner: 'Codex', due: 'Stage 2' },
  { title: 'Core store module conversion', status: 'In Progress', type: 'Frontend', owner: 'Codex', due: 'Stage 3' },
  { title: 'HR and payroll UI conversion', status: 'Pending', type: 'Frontend', owner: 'Codex', due: 'Stage 4' },
  { title: 'Reports and deployment polish', status: 'Pending', type: 'Release', owner: 'Codex', due: 'Stage 6' }
])

const recentActivity = computed(() => {
  const saleItems = invoices.value.slice(0, 5).map((invoice) => ({
    label: invoice.invoiceNumber || invoice.billNo || 'Sales invoice',
    meta: money(moneyValue(invoice)),
    icon: 'i-lucide-receipt-indian-rupee',
    to: '/billing'
  }))

  const setupItems = [
    ...(canSeeAdmin.value ? [{ label: `${companies.value.length} companies configured`, meta: 'Company', icon: 'i-lucide-building-2', to: '/setup' }] : []),
    { label: `${products.value.length} products available`, meta: 'Inventory', icon: 'i-lucide-boxes', to: '/inventory' },
    { label: `${employees.value.length} employees registered`, meta: 'HR', icon: 'i-lucide-users-round', to: '/hr' }
  ]

  return [...saleItems, ...setupItems].slice(0, 8)
})

async function refresh() {
  if (!auth.isAuthenticated.value) {
    return
  }

  loading.value = true
  try {
    setupStatus.value = await api.get<any>('setup/status')
    const [companyRows, storeRows, productRows, invoiceRows, voucherRows, employeeRows, customerRows, vendorRows] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores'),
      api.list<any>('products'),
      api.get<any[]>('billing/sales/recent'),
      api.list<any>('vouchers'),
      api.list<any>('employees'),
      api.list<any>('customers'),
      api.list<any>('vendors')
    ])

    companies.value = companyRows
    stores.value = storeRows
    products.value = productRows
    invoices.value = invoiceRows
    vouchers.value = voucherRows
    employees.value = employeeRows
    customers.value = customerRows
    vendors.value = vendorRows
  } catch (error) {
    feedback.failed('Dashboard refresh failed', error)
  } finally {
    loading.value = false
  }
}

async function quickSetup() {
  setupMessage.value = ''

  try {
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
    feedback.saved('Setup')
    await refresh()
  } catch (error) {
    feedback.failed('Setup save failed', error)
  }
}

function money(value: number) {
  return new Intl.NumberFormat('en-IN', {
    style: 'currency',
    currency: 'INR',
    maximumFractionDigits: 0
  }).format(value || 0)
}

function moneyValue(invoice: any) {
  return Number(invoice?.netAmount || invoice?.payableAmount || invoice?.grandTotal || invoice?.totalAmount || 0)
}

function statusColor(status: string) {
  if (status === 'Done' || status === 'Ready' || status === 'Live') {
    return 'success'
  }

  if (status === 'In Progress' || status === 'Attendance') {
    return 'primary'
  }

  if (status === 'Required') {
    return 'warning'
  }

  return 'neutral'
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
    title="Overview"
    :companies="companies"
    :stores="stores"
    @refresh="refresh"
  >
    <section class="planner-dashboard">
      <UiModulePageHeader
        title="All Stores"
        description="Planner-style overview for sales, stock, setup, HR, and the next UI migration stages."
        icon="i-lucide-store"
      >
        <template #actions>
          <UBadge :color="loading ? 'warning' : 'success'" variant="subtle">
            {{ loading ? 'Loading' : 'Synced' }}
          </UBadge>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="subtle" :loading="loading" label="Refresh" @click="refresh" />
        </template>
      </UiModulePageHeader>

      <UAlert
        v-if="needsSetup"
        class="dashboard-alert"
        color="warning"
        variant="subtle"
        icon="i-lucide-circle-alert"
        title="First-run setup is incomplete"
        description="Create the first company, store, product category, and GST tax before using live billing."
      />

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

      <div class="planner-workspace">
        <div class="planner-main-column">
          <UCard v-if="needsSetup && canSeeAdmin" class="planner-card">
            <template #header>
              <div class="planner-card-header">
                <div>
                  <h2>Quick Setup</h2>
                  <p>{{ setupProgress }}% complete</p>
                </div>
                <UBadge color="warning" variant="subtle">Required</UBadge>
              </div>
            </template>

            <form class="planner-setup-grid" @submit.prevent="quickSetup">
              <UFormField label="Company">
                <UInput v-model="setupForm.companyName" required />
              </UFormField>
              <UFormField label="Store group">
                <UInput v-model="setupForm.storeGroupName" required />
              </UFormField>
              <UFormField label="Store">
                <UInput v-model="setupForm.storeName" required />
              </UFormField>
              <UFormField label="Contact">
                <UInput v-model="setupForm.contactNumber" />
              </UFormField>
              <UFormField label="Email">
                <UInput v-model="setupForm.email" type="email" />
              </UFormField>
              <UFormField label="City">
                <UInput v-model="setupForm.city" required />
              </UFormField>
              <UFormField label="State">
                <UInput v-model="setupForm.state" required />
              </UFormField>
              <UFormField label="Zip">
                <UInput v-model="setupForm.zipCode" required />
              </UFormField>
              <div class="planner-form-actions">
                <UButton type="submit" icon="i-lucide-building-2" label="Create Setup" />
              </div>
            </form>

            <UAlert
              v-if="setupMessage"
              class="mt-4"
              color="success"
              variant="subtle"
              icon="i-lucide-check"
              :description="setupMessage"
            />
          </UCard>

          <UCard class="planner-card">
            <template #header>
              <div class="planner-card-header">
                <div>
                  <h2>Module Distribution</h2>
                  <p>Operational screens ready for focused list and form workflows</p>
                </div>
                <UBadge color="primary" variant="subtle">Stage 3</UBadge>
              </div>
            </template>

            <div class="planner-module-grid">
              <UButton
                v-for="item in moduleCards"
                :key="item.to"
                :to="item.to"
                color="neutral"
                variant="outline"
                class="planner-module-button"
              >
                <span class="planner-module-icon">
                  <UIcon :name="item.icon" class="size-5" />
                </span>
                <span>
                  <strong>{{ item.label }}</strong>
                  <small>{{ item.count }} records</small>
                </span>
                <UBadge :color="statusColor(item.status)" variant="subtle">{{ item.status }}</UBadge>
              </UButton>
            </div>
          </UCard>

          <UCard class="planner-card">
            <template #header>
              <div class="planner-card-header">
                <div>
                  <h2>Current Sprint</h2>
                  <p>UI implementation stages tracked like the planner template</p>
                </div>
                <UButton v-if="canSeeAdmin" to="/setup" color="neutral" variant="ghost" icon="i-lucide-arrow-right" label="Open Company" />
              </div>
            </template>

            <div class="planner-table-wrap">
              <table class="planner-table">
                <thead>
                  <tr>
                    <th>Title</th>
                    <th>Status</th>
                    <th>Type</th>
                    <th>Owner</th>
                    <th>Due</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="item in currentWork" :key="item.title">
                    <td>{{ item.title }}</td>
                    <td><UBadge :color="statusColor(item.status)" variant="subtle">{{ item.status }}</UBadge></td>
                    <td>{{ item.type }}</td>
                    <td>{{ item.owner }}</td>
                    <td>{{ item.due }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </UCard>
        </div>

        <aside class="planner-side-column">
          <UCard class="planner-card">
            <template #header>
              <div class="planner-card-header">
                <div>
                  <h2>Setup Health</h2>
                  <p>Required first-run data</p>
                </div>
                <strong>{{ setupProgress }}%</strong>
              </div>
            </template>

            <div class="planner-progress">
              <span :style="{ width: `${setupProgress}%` }" />
            </div>
            <div class="planner-check-list">
              <div v-for="item in setupChecks" :key="item.label">
                <UIcon :name="item.ready ? 'i-lucide-circle-check' : 'i-lucide-circle-alert'" class="size-4" />
                <span>{{ item.label }}</span>
              </div>
            </div>
          </UCard>

          <UCard class="planner-card">
            <template #header>
              <div class="planner-card-header">
                <div>
                  <h2>Recent Activity</h2>
                  <p>Latest sales and master data counts</p>
                </div>
              </div>
            </template>

            <div class="planner-activity-list">
              <NuxtLink v-for="item in recentActivity" :key="`${item.label}-${item.meta}`" :to="item.to">
                <UAvatar :icon="item.icon" size="sm" color="primary" variant="subtle" />
                <span>{{ item.label }}</span>
                <small>{{ item.meta }}</small>
              </NuxtLink>
            </div>
          </UCard>
        </aside>
      </div>
    </section>
  </AppShell>
</template>
