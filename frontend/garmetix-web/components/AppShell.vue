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

const companyValue = ref('all')
const storeValue = ref('all')

const modules = [
  { to: '/', label: 'Dashboard', icon: 'i-lucide-layout-dashboard' },
  { to: '/setup', label: 'Setup', icon: 'i-lucide-building-2' },
  { to: '/billing', label: 'Billing', icon: 'i-lucide-receipt-indian-rupee' },
  { to: '/inventory', label: 'Inventory', icon: 'i-lucide-boxes' },
  { to: '/purchase', label: 'Purchase', icon: 'i-lucide-package-plus' },
  { to: '/vouchers', label: 'Vouchers', icon: 'i-lucide-banknote' },
  { to: '/petty-cash', label: 'Petty Cash', icon: 'i-lucide-circle-dollar-sign' },
  { to: '/hr', label: 'HR', icon: 'i-lucide-users-round' },
  { to: '/payroll', label: 'Payroll', icon: 'i-lucide-badge-indian-rupee' },
  { to: '/reports', label: 'Reports', icon: 'i-lucide-file-text' },
  { to: '/access', label: 'Access', icon: 'i-lucide-shield-check' },
  { to: '/import-export', label: 'Import Export', icon: 'i-lucide-file-down' }
]

const navigationItems = computed(() => modules.map((item) => ({
  ...item,
  active: item.to === '/' ? route.path === '/' : route.path.startsWith(item.to)
})))

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

function logout() {
  auth.logout()
  navigateTo('/')
}
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
            <UIcon name="i-lucide-shirt" class="size-5" />
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
              <UColorModeButton color="neutral" variant="ghost" />
            </div>
          </template>
        </UDashboardNavbar>
      </template>

      <div class="dashboard-content">
        <slot />
      </div>
    </UDashboardPanel>
  </UDashboardGroup>
</template>
