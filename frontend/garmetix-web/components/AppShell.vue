<script setup lang="ts">
import { APP_VERSION, APP_STAGE } from '~/utils/appVersion'

type MenuItem = {
  to: string
  label: string
  icon: string
  roles?: string[]
  adminOnly?: boolean
  stage?: string
}

type MenuGroup = {
  label: string
  items: MenuItem[]
}

const props = defineProps<{
  title: string
  companies?: any[]
  stores?: any[]
}>()

const emit = defineEmits<{
  refresh: []
  workspaceChange: []
}>()

const config = useRuntimeConfig()
const auth = useAuth()
const api = useGarmetixApi()
const workspace = useWorkspace()
const route = useRoute()
const colorMode = useColorMode()
const feedback = useUiFeedback()

const useLegacyShell = computed(() => config.public.dashboardShell === 'legacy')
const storeGroups = ref<any[]>([])
const workspaceOptions = ref<any | null>(null)
const workspaceCompanies = ref<any[]>([])
const workspaceStores = ref<any[]>([])
const commandOpen = ref(false)
const workspaceOpen = ref(false)
const searchTerm = ref('')
const now = ref(new Date())
const apiLive = ref<boolean | null>(null)
let clockTimer: ReturnType<typeof setInterval> | undefined
let healthTimer: ReturnType<typeof setInterval> | undefined

useHead(() => ({
  title: props.title || 'Dashboard'
}))

const themeOptions = [
  { label: 'System', value: 'system' },
  { label: 'Dark', value: 'dark' },
  { label: 'Light', value: 'light' }
]

const selectedTheme = computed({
  get: () => colorMode.preference,
  set: (value: string) => {
    colorMode.preference = value
  }
})

const moduleGroups: MenuGroup[] = [
  {
    label: 'Dashboards',
    items: [
      { to: '/dashboard/store-manager', label: 'Store Manager', icon: 'i-lucide-store', roles: ['storemanager', 'manager'] },
      { to: '/dashboard/business', label: 'Owner / Admin', icon: 'i-lucide-chart-no-axes-combined', roles: ['owner', 'admin', 'accountant'] },
      { to: '/', label: 'Legacy Overview', icon: 'i-lucide-layout-dashboard', stage: 'revert-safe' },
      { to: '/reports', label: 'Reports', icon: 'i-lucide-file-text' },
      { to: '/gst-returns', label: 'GST Returns', icon: 'i-lucide-file-json-2' },
      { to: '/gst-reports', label: 'GST Reports', icon: 'i-lucide-table-properties' }
    ]
  },
  {
    label: 'Operations',
    items: [
      { to: '/billing', label: 'Billing', icon: 'i-lucide-receipt-indian-rupee' },
      { to: '/sales-return', label: 'Sales Return', icon: 'i-lucide-rotate-ccw' },
      { to: '/inventory', label: 'Inventory', icon: 'i-lucide-boxes' },
      { to: '/stock-operations', label: 'Stock Ops', icon: 'i-lucide-arrow-left-right' },
      { to: '/non-gst-goods', label: 'Non-GST Goods', icon: 'i-lucide-file-warning' },
      { to: '/purchase', label: 'Purchase', icon: 'i-lucide-package-plus' },
      { to: '/purchase-return', label: 'Purchase Return', icon: 'i-lucide-undo-2' },
      { to: '/customers', label: 'Customers', icon: 'i-lucide-user-round' },
      { to: '/parties', label: 'Parties / Vendors', icon: 'i-lucide-users-round' },
      { to: '/vouchers', label: 'Vouchers', icon: 'i-lucide-banknote' },
      { to: '/debit-notes', label: 'Debit Notes', icon: 'i-lucide-file-minus-2' },
      { to: '/credit-notes', label: 'Credit Notes', icon: 'i-lucide-file-plus-2' },
      { to: '/commercial-notes', label: 'Commercial Summary', icon: 'i-lucide-files' },
      { to: '/loyalty', label: 'Loyalty', icon: 'i-lucide-gift' },
      { to: '/accounting', label: 'Accounting', icon: 'i-lucide-landmark' },
      { to: '/petty-cash', label: 'Petty Cash', icon: 'i-lucide-circle-dollar-sign' }
    ]
  },
  {
    label: 'People',
    items: [
      { to: '/hr', label: 'HR', icon: 'i-lucide-users-round' },
      { to: '/payroll', label: 'Payroll', icon: 'i-lucide-badge-indian-rupee' }
    ]
  },
  {
    label: 'Off Book',
    items: [
      { to: '/cash-vouchers', label: 'Cash Vouchers', icon: 'i-lucide-wallet-cards' }
    ]
  },
  {
    label: 'Account',
    items: [
      { to: '/profile', label: 'My Profile', icon: 'i-lucide-user-cog' }
    ]
  },
  {
    label: 'Help',
    items: [
      { to: '/about-us', label: 'About Us', icon: 'i-lucide-info' },
      { to: '/contact-us', label: 'Contact Us', icon: 'i-lucide-message-circle' },
      { to: '/faq', label: 'FAQ', icon: 'i-lucide-circle-help' }
    ]
  },
  {
    label: 'Admin',
    items: [
      { to: '/setup', label: 'Company', icon: 'i-lucide-building-2', adminOnly: true },
      { to: '/client-onboarding', label: 'Onboarding', icon: 'i-lucide-route', adminOnly: true },
      { to: '/af-ss', label: 'AF/SS', icon: 'i-lucide-database-backup', adminOnly: true },
      { to: '/message-logs', label: 'Message Logs', icon: 'i-lucide-list-collapse', adminOnly: true },
      { to: '/access', label: 'Roles & Users', icon: 'i-lucide-shield-check', adminOnly: true },
      { to: '/import-export', label: 'Import Export', icon: 'i-lucide-file-down', adminOnly: true },
      { to: '/audit', label: 'Audit', icon: 'i-lucide-history', adminOnly: true },
      { to: '/system-health', label: 'System Health', icon: 'i-lucide-activity', adminOnly: true },
      { to: '/production-readiness', label: 'Production Readiness', icon: 'i-lucide-shield-check', adminOnly: true },
      { to: '/release-stabilization', label: 'Release Stabilization', icon: 'i-lucide-rocket', adminOnly: true },
      { to: '/data-consistency', label: 'Consistency & Repair', icon: 'i-lucide-shield-alert', adminOnly: true },
      { to: '/oracle-sync', label: 'Oracle Sync', icon: 'i-lucide-database-zap', adminOnly: true }
    ]
  }
]

const shellCompanies = computed(() => workspaceCompanies.value.length ? workspaceCompanies.value : (props.companies || []))
const shellStores = computed(() => workspaceStores.value.length ? workspaceStores.value : (props.stores || []))
const isCompanyLocked = computed(() => Boolean(workspaceOptions.value?.isCompanyLocked || auth.user.value?.companyId))
const isStoreGroupLocked = computed(() => Boolean(workspaceOptions.value?.isStoreGroupLocked || auth.user.value?.storeGroupId))
const isStoreLocked = computed(() => Boolean(workspaceOptions.value?.isStoreLocked || auth.user.value?.storeId))
const roleKey = computed(() => `${auth.user.value?.role || ''} ${auth.user.value?.userType || ''}`.toLowerCase())
const isBusinessUser = computed(() => auth.canSeeAdmin.value || roleKey.value.includes('accountant'))
const visibleGroups = computed(() => moduleGroups
  .map((group) => ({
    ...group,
    items: group.items.filter((item) => isVisibleItem(item))
  }))
  .filter((group) => group.items.length > 0))

const navigationItems = computed(() => visibleGroups.value.flatMap((group) => [
  { label: group.label, type: 'label' },
  ...group.items.map((item) => ({
    ...item,
    active: isActive(item.to)
  }))
]))

const allVisibleItems = computed(() => visibleGroups.value.flatMap((group) => group.items.map((item) => ({ ...item, group: group.label }))))
const commandItems = computed(() => {
  const term = searchTerm.value.trim().toLowerCase()
  return (term ? allVisibleItems.value.filter((item) => `${item.group} ${item.label}`.toLowerCase().includes(term)) : allVisibleItems.value).slice(0, 18)
})

const allowedCompanies = computed(() => shellCompanies.value.filter((company) =>
  !auth.user.value?.companyId || company.id === auth.user.value.companyId))
const companyOptions = computed(() => allowedCompanies.value.map((company) => ({ label: company.name || company.companyName || 'Company', value: company.id })))
const allowedStoreGroups = computed(() => storeGroups.value.filter((group) =>
  (!workspace.companyId.value || group.companyId === workspace.companyId.value)
  && (!auth.user.value?.storeGroupId || group.id === auth.user.value.storeGroupId)))
const storeGroupOptions = computed(() => allowedStoreGroups.value.map((group) => ({ label: group.name || 'Store group', value: group.id })))
const allowedStores = computed(() => shellStores.value.filter((storeItem) =>
  (!workspace.companyId.value || storeItem.companyId === workspace.companyId.value)
  && (!workspace.storeGroupId.value || storeItem.storeGroupId === workspace.storeGroupId.value)
  && (!auth.user.value?.storeId || storeItem.id === auth.user.value.storeId)))
const storeOptions = computed(() => allowedStores.value.map((storeItem) => ({ label: storeItem.name || storeItem.storeName || 'Store', value: storeItem.id })))

const companyValue = computed({
  get: () => workspace.companyId.value,
  set: (value: string) => {
    workspace.selectCompany(value, auth.user.value, storeGroups.value, shellStores.value)
    emit('workspaceChange')
  }
})
const storeGroupValue = computed({
  get: () => workspace.storeGroupId.value,
  set: (value: string) => {
    workspace.selectStoreGroup(value, auth.user.value, shellStores.value)
    emit('workspaceChange')
  }
})
const storeValue = computed({
  get: () => workspace.storeId.value,
  set: (value: string) => {
    workspace.selectStore(value, auth.user.value)
    emit('workspaceChange')
  }
})

const workingCompanyName = computed(() => shellCompanies.value.find((item) => item.id === workspace.companyId.value)?.name || 'All companies')
const workingStoreName = computed(() => shellStores.value.find((item) => item.id === workspace.storeId.value)?.name || 'All permitted stores')
const currentClock = computed(() => now.value.toLocaleTimeString('en-IN', { hour: '2-digit', minute: '2-digit', hour12: true }))
const currentDate = computed(() => now.value.toLocaleDateString('en-IN', { day: '2-digit', month: 'short', year: 'numeric' }))
const apiBadge = computed(() => {
  if (apiLive.value === null) {
    return { label: 'Checking', color: 'warning' as const, icon: 'i-lucide-loader-circle' }
  }
  return apiLive.value
    ? { label: 'API Live', color: 'success' as const, icon: 'i-lucide-wifi' }
    : { label: 'API Offline', color: 'error' as const, icon: 'i-lucide-wifi-off' }
})

function isVisibleItem(item: MenuItem) {
  if (item.adminOnly && !auth.canSeeAdmin.value) {
    return false
  }
  if (item.to === '/dashboard/business') {
    return isBusinessUser.value
  }
  if (item.to === '/dashboard/store-manager') {
    return true
  }
  if (!item.roles?.length) {
    return true
  }
  return item.roles.some((role) => roleKey.value.includes(role)) || auth.canSeeAdmin.value
}

function isActive(to: string) {
  return to === '/' ? route.path === '/' : route.path.startsWith(to)
}

function logout() {
  auth.logout()
  navigateTo('/')
}

function openCommand() {
  searchTerm.value = ''
  commandOpen.value = true
}

function goTo(path: string) {
  commandOpen.value = false
  navigateTo(path)
}

async function checkApiHealth() {
  try {
    const response = await $fetch<{ databaseReady?: boolean }>('/api/health')
    apiLive.value = Boolean(response?.databaseReady)
  } catch {
    apiLive.value = false
  }
}

async function loadWorkspaceOptions() {
  try {
    const options = await api.get<any>('workspace/options')
    workspaceOptions.value = options
    workspaceCompanies.value = options?.companies || []
    storeGroups.value = options?.storeGroups || []
    workspaceStores.value = options?.stores || []
  } catch {
    workspaceOptions.value = null
    workspaceCompanies.value = []
    workspaceStores.value = []
    try {
      storeGroups.value = await api.list<any>('store-groups')
    } catch {
      storeGroups.value = []
    }
  }
  workspace.initialize(auth.user.value, shellCompanies.value, storeGroups.value, shellStores.value)
  if (workspaceOptions.value?.defaultCompanyId && !workspace.companyId.value) workspace.companyId.value = workspaceOptions.value.defaultCompanyId
  if (workspaceOptions.value?.defaultStoreGroupId && !workspace.storeGroupId.value) workspace.storeGroupId.value = workspaceOptions.value.defaultStoreGroupId
  if (workspaceOptions.value?.defaultStoreId && !workspace.storeId.value) workspace.storeId.value = workspaceOptions.value.defaultStoreId
}

watch(
  () => [auth.user.value?.id, props.companies, props.stores],
  () => workspace.initialize(auth.user.value, shellCompanies.value, storeGroups.value, shellStores.value),
  { deep: true }
)

onMounted(async () => {
  await loadWorkspaceOptions()
  clockTimer = setInterval(() => { now.value = new Date() }, 1000)
  checkApiHealth()
  healthTimer = setInterval(checkApiHealth, 60000)
})

onBeforeUnmount(() => {
  if (clockTimer) clearInterval(clockTimer)
  if (healthTimer) clearInterval(healthTimer)
})
</script>

<template>
  <AppShellLegacy
    v-if="useLegacyShell"
    :title="title"
    :companies="companies"
    :stores="stores"
    @refresh="emit('refresh')"
    @workspace-change="emit('workspaceChange')"
  >
    <slot />
  </AppShellLegacy>

  <UDashboardGroup v-else storage-key="garmetix-dashboard-v3">
    <UDashboardSidebar
      id="garmetix-dashboard-sidebar"
      collapsible
      resizable
      :min-size="12"
      :default-size="18"
      :max-size="24"
      :ui="{ footer: 'border-t border-default' }"
    >
      <template #header="{ collapsed }">
        <NuxtLink class="dashboard-brand" to="/dashboard/store-manager">
          <div class="dashboard-brand-mark">
            <img class="ui-brand-logo" src="/garmetix-icon-512.png" alt="Garmetix" />
          </div>
          <div v-if="!collapsed" class="min-w-0">
            <p class="dashboard-brand-title">Garmetix</p>
            <p class="dashboard-brand-subtitle">Dashboard shell · v3</p>
          </div>
        </NuxtLink>
      </template>

      <template #default="{ collapsed }">
        <div class="dashboard-sidebar-stack">
          <UButton
            color="neutral"
            variant="subtle"
            icon="i-lucide-search"
            :label="collapsed ? undefined : 'Search menu'"
            :square="collapsed"
            block
            @click="openCommand"
          />
          <UNavigationMenu :collapsed="collapsed" :items="navigationItems" orientation="vertical" />
        </div>
      </template>

      <template #footer="{ collapsed }">
        <div class="dashboard-sidebar-footer">
          <div v-if="!collapsed" class="dashboard-scope-card">
            <div class="dashboard-scope-row">
              <span>{{ workingStoreName }}</span>
              <UBadge size="xs" :color="apiBadge.color" variant="subtle" :icon="apiBadge.icon">{{ apiBadge.label }}</UBadge>
            </div>
            <p>{{ workingCompanyName }}</p>
            <div class="dashboard-clock">
              <strong>{{ currentClock }}</strong>
              <span>{{ currentDate }}</span>
            </div>
            <p>{{ APP_STAGE }} · v{{ APP_VERSION }}</p>
            <p class="dashboard-revert-hint">Revert: set NUXT_PUBLIC_DASHBOARD_SHELL=legacy</p>
          </div>
          <UButton color="neutral" variant="ghost" icon="i-lucide-user-cog" :label="collapsed ? undefined : 'My Profile'" :square="collapsed" block @click="navigateTo('/profile')" />
          <UButton color="neutral" variant="ghost" icon="i-lucide-log-out" :label="collapsed ? undefined : 'Logout'" :square="collapsed" block @click="logout" />
        </div>
      </template>
    </UDashboardSidebar>

    <UDashboardPanel id="garmetix-dashboard-main">
      <template #header>
        <UDashboardNavbar :title="title">
          <template #leading>
            <UDashboardSidebarCollapse />
          </template>

          <template #right>
            <div class="dashboard-toolbar">
              <UButton color="neutral" variant="subtle" icon="i-lucide-search" class="hidden md:inline-flex" label="Search" @click="openCommand" />
              <UButton color="neutral" variant="subtle" icon="i-lucide-building-2" class="xl:hidden" aria-label="Workspace" @click="workspaceOpen = true" />
              <USelect v-model="companyValue" :items="companyOptions" :disabled="isCompanyLocked || companyOptions.length < 2" class="hidden lg:flex w-44" aria-label="Company" />
              <USelect v-model="storeGroupValue" :items="storeGroupOptions" :disabled="isStoreGroupLocked || storeGroupOptions.length < 2" class="hidden xl:flex w-40" aria-label="Store group" />
              <USelect v-model="storeValue" :items="storeOptions" :disabled="isStoreLocked || storeOptions.length < 2" class="hidden 2xl:flex w-44" aria-label="Store" />
              <UButton color="neutral" variant="subtle" icon="i-lucide-refresh-cw" aria-label="Refresh" @click="emit('refresh')" />
              <UButton color="neutral" variant="subtle" icon="i-lucide-list-collapse" aria-label="Message logs" @click="navigateTo('/message-logs')" />
              <USelect v-model="selectedTheme" :items="themeOptions" class="hidden sm:flex w-28" aria-label="Theme" />
              <UColorModeButton color="neutral" variant="ghost" />
            </div>
          </template>
        </UDashboardNavbar>
      </template>

      <div class="dashboard-template-content">
        <slot />
      </div>
    </UDashboardPanel>
  </UDashboardGroup>

  <UModal v-model:open="commandOpen" title="Search Garmetix" :ui="{ content: 'max-w-2xl' }">
    <template #body>
      <div class="dashboard-command">
        <UInput v-model="searchTerm" icon="i-lucide-search" placeholder="Search pages, reports, setup and operations..." autofocus />
        <div class="dashboard-command-list">
          <button v-for="item in commandItems" :key="item.to" class="dashboard-command-item" type="button" @click="goTo(item.to)">
            <UIcon :name="item.icon" class="h-5 w-5" />
            <span>
              <strong>{{ item.label }}</strong>
              <small>{{ item.group }}</small>
            </span>
            <UIcon name="i-lucide-arrow-right" class="h-4 w-4" />
          </button>
        </div>
      </div>
    </template>
  </UModal>

  <UModal v-model:open="workspaceOpen" title="Workspace" :ui="{ content: 'max-w-lg' }">
    <template #body>
      <div class="form-grid">
        <UFormField label="Company">
          <USelect v-model="companyValue" :items="companyOptions" :disabled="isCompanyLocked || companyOptions.length < 2" placeholder="Select company" />
        </UFormField>
        <UFormField label="Store group">
          <USelect v-model="storeGroupValue" :items="storeGroupOptions" :disabled="isStoreGroupLocked || storeGroupOptions.length < 2" placeholder="Select store group" />
        </UFormField>
        <UFormField label="Store">
          <USelect v-model="storeValue" :items="storeOptions" :disabled="isStoreLocked || storeOptions.length < 2" placeholder="Select store" />
        </UFormField>
      </div>
    </template>
    <template #footer>
      <div class="modal-actions">
        <UButton color="neutral" variant="outline" label="Close" @click="workspaceOpen = false" />
        <UButton icon="i-lucide-check" label="Use Workspace" @click="workspaceOpen = false" />
      </div>
    </template>
  </UModal>
</template>
