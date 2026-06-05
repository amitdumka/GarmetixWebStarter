<script setup lang="ts">
const props = defineProps<{
  title: string
  companies?: any[]
  stores?: any[]
}>()

const emit = defineEmits<{
  refresh: []
}>()

const auth = useAuth()
const route = useRoute()
const feedback = useUiFeedback()
const messageLogs = feedback.logs

useHead(() => ({
  title: props.title || 'Dashboard'
}))

const companyValue = ref('all')
const storeValue = ref('all')
const colorMode = useColorMode()
const logOpen = ref(false)
const now = ref(new Date())
const apiLive = ref<boolean | null>(null)
let clockTimer: ReturnType<typeof setInterval> | undefined
let healthTimer: ReturnType<typeof setInterval> | undefined

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

const moduleGroups = [
  {
    label: 'Dashboards',
    items: [
      { to: '/', label: 'Overview', icon: 'i-lucide-layout-dashboard' },
      { to: '/reports', label: 'Reports', icon: 'i-lucide-file-text' }
    ]
  },
  {
    label: 'Operations',
    items: [
      { to: '/billing', label: 'Billing', icon: 'i-lucide-receipt-indian-rupee' },
      { to: '/inventory', label: 'Inventory', icon: 'i-lucide-boxes' },
      { to: '/purchase', label: 'Purchase', icon: 'i-lucide-package-plus' },
      { to: '/vouchers', label: 'Vouchers', icon: 'i-lucide-banknote' },
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
    label: 'Admin',
    items: [
      { to: '/setup', label: 'Company', icon: 'i-lucide-building-2' },
      { to: '/access', label: 'Roles & Users', icon: 'i-lucide-shield-check' },
      { to: '/import-export', label: 'Import Export', icon: 'i-lucide-file-down' },
      { to: '/audit', label: 'Audit', icon: 'i-lucide-history' },
      { to: '/system-health', label: 'System Health', icon: 'i-lucide-activity' }
    ]
  }
]

function isActive(to: string) {
  return to === '/' ? route.path === '/' : route.path.startsWith(to)
}

const visibleModuleGroups = computed(() => moduleGroups.filter((group) => group.label !== 'Admin' || auth.canSeeAdmin.value))

const navigationItems = computed(() => visibleModuleGroups.value.flatMap((group) => [
  { label: group.label, type: 'label' },
  ...group.items.map((item) => ({
    ...item,
    active: isActive(item.to)
  }))
]))

const companyOptions = computed(() => [
  { label: 'All Companies', value: 'all' },
  ...((props.companies || []).map((company) => ({
    label: company.name || company.companyName || 'Company',
    value: company.id
  })))
])

const storeOptions = computed(() => [
  { label: 'All Stores', value: 'all' },
  ...((props.stores || []).map((storeItem) => ({
    label: storeItem.name || storeItem.storeName || 'Store',
    value: storeItem.id
  })))
])

const workingStoreName = computed(() => {
  const userStoreId = auth.user.value?.storeId
  const selectedStoreId = storeValue.value !== 'all' ? storeValue.value : userStoreId
  const store = (props.stores || []).find((item) => item.id === selectedStoreId) || (props.stores || [])[0]

  return store?.name || store?.storeName || 'No store selected'
})

const currentClock = computed(() => now.value.toLocaleTimeString('en-IN', {
  hour: '2-digit',
  minute: '2-digit',
  second: '2-digit',
  hour12: true
}))

const currentDate = computed(() => now.value.toLocaleDateString('en-IN', {
  day: '2-digit',
  month: 'short',
  year: 'numeric'
}))

const apiBadge = computed(() => {
  if (apiLive.value === null) {
    return { label: 'Checking', color: 'warning' as const, icon: 'i-lucide-loader-circle' }
  }

  return apiLive.value
    ? { label: 'API Live', color: 'success' as const, icon: 'i-lucide-wifi' }
    : { label: 'API Offline', color: 'error' as const, icon: 'i-lucide-wifi-off' }
})

function logout() {
  auth.logout()
  navigateTo('/')
}

function formatLogDate(value: string) {
  return value ? new Date(value).toLocaleString() : '-'
}

async function checkApiHealth() {
  try {
    const response = await $fetch<{ databaseReady?: boolean }>('/api/health')
    apiLive.value = Boolean(response?.databaseReady)
  } catch {
    apiLive.value = false
  }
}

onMounted(() => {
  clockTimer = setInterval(() => {
    now.value = new Date()
  }, 1000)

  checkApiHealth()
  healthTimer = setInterval(checkApiHealth, 60000)
})

onBeforeUnmount(() => {
  if (clockTimer) {
    clearInterval(clockTimer)
  }

  if (healthTimer) {
    clearInterval(healthTimer)
  }
})
</script>

<template>
  <UDashboardGroup storage-key="garmetix-dashboard">
    <UDashboardSidebar
      id="garmetix-sidebar"
      collapsible
      resizable
      :min-size="12"
      :default-size="16"
      :max-size="20"
      :ui="{ footer: 'border-t border-default' }"
    >
      <template #header="{ collapsed }">
        <NuxtLink class="ui-brand" to="/">
          <div class="ui-brand-mark">
            <img class="ui-brand-logo" src="/garmetix-icon-512.png" alt="Garmetix" />
          </div>
          <div v-if="!collapsed" class="min-w-0">
            <p class="ui-brand-title">Garmetix</p>
            <p class="ui-brand-subtitle">Store management</p>
          </div>
        </NuxtLink>
      </template>

      <template #default="{ collapsed }">
        <UNavigationMenu
          :collapsed="collapsed"
          :items="navigationItems"
          orientation="vertical"
        />
      </template>

      <template #footer="{ collapsed }">
        <div v-if="!collapsed" class="sidebar-stage-card">
          <div class="sidebar-stage-card-header">
            <span>{{ workingStoreName }}</span>
            <UBadge size="xs" :color="apiBadge.color" variant="subtle" :icon="apiBadge.icon">
              {{ apiBadge.label }}
            </UBadge>
          </div>
          <div class="sidebar-status-clock">
            <strong>{{ currentClock }}</strong>
            <span>{{ currentDate }}</span>
          </div>
          <p>Current working store and service status.</p>
        </div>
        <UButton
          color="neutral"
          variant="ghost"
          icon="i-lucide-log-out"
          :label="collapsed ? undefined : 'Logout'"
          :square="collapsed"
          block
          @click="logout"
        />
      </template>
    </UDashboardSidebar>

    <UDashboardPanel id="garmetix-main">
      <template #header>
        <UDashboardNavbar :title="title">
          <template #leading>
            <UDashboardSidebarCollapse />
          </template>
          <template #right>
            <div class="dashboard-toolbar">
              <USelect
                v-model="companyValue"
                :items="companyOptions"
                class="hidden md:flex w-44"
                aria-label="Company"
              />
              <USelect
                v-model="storeValue"
                :items="storeOptions"
                class="hidden lg:flex w-40"
                aria-label="Store"
              />
              <UTooltip text="Refresh data">
                <UButton
                  color="neutral"
                  variant="subtle"
                  icon="i-lucide-refresh-cw"
                  @click="emit('refresh')"
                />
              </UTooltip>
              <UTooltip text="Message log">
                <UButton
                  color="neutral"
                  variant="subtle"
                  icon="i-lucide-list-collapse"
                  :label="messageLogs.length ? String(messageLogs.length) : undefined"
                  @click="logOpen = true"
                />
              </UTooltip>
              <USelect
                v-model="selectedTheme"
                :items="themeOptions"
                class="hidden sm:flex w-28"
                aria-label="Theme"
              />
              <UTooltip text="Toggle theme">
                <UColorModeButton color="neutral" variant="ghost" />
              </UTooltip>
            </div>
          </template>
        </UDashboardNavbar>
      </template>

      <div class="dashboard-content">
        <slot />
      </div>
    </UDashboardPanel>
  </UDashboardGroup>

  <UModal v-model:open="logOpen" title="Message Log" :ui="{ content: 'max-w-3xl' }">
    <template #body>
      <div v-if="messageLogs.length" class="message-log-list">
        <div v-for="entry in messageLogs" :key="entry.id" class="message-log-entry">
          <div class="message-log-header">
            <UBadge :color="entry.color" variant="subtle">{{ entry.title }}</UBadge>
            <span>{{ formatLogDate(entry.at) }}</span>
          </div>
          <p>{{ entry.message }}</p>
          <pre v-if="entry.details">{{ entry.details }}</pre>
        </div>
      </div>
      <UiCrudEmptyState
        v-else
        title="No messages"
        description="Application messages and technical details will appear here."
        icon="i-lucide-list-collapse"
      />
    </template>

    <template #footer>
      <div class="modal-actions">
        <UButton color="neutral" variant="outline" label="Close" @click="logOpen = false" />
        <UButton color="warning" variant="subtle" icon="i-lucide-trash-2" label="Clear Log" @click="feedback.clearLogs" />
      </div>
    </template>
  </UModal>
</template>
