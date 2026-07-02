<template>
  <UApp>
    <main class="modular-shell">
      <aside class="modular-sidebar">
        <div class="modular-sidebar-header">
          <NuxtLink class="modular-brand" :to="homePath">
            <span class="modular-brand-mark">
              <img src="/garmetix-icon-512.png" alt="Garmetix" class="modular-brand-logo">
            </span>
            <span class="min-w-0">
              <span class="modular-brand-title">Garmetix</span>
              <span class="modular-brand-subtitle">{{ appCopy.badge }} | v{{ version.version }}</span>
            </span>
          </NuxtLink>
        </div>

        <div class="modular-sidebar-body">
          <UButton
            v-if="!authSnapshot.hasToken"
            to="/login"
            icon="i-lucide-log-in"
            color="primary"
            variant="soft"
            block
            class="justify-start"
          >
            Login
          </UButton>

          <div v-if="authSnapshot.hasToken" class="modular-stage-card">
            <div class="flex items-center justify-between gap-2">
              <span class="truncate text-sm font-semibold">{{ appCopy.badge }}</span>
              <UBadge color="primary" variant="subtle" size="xs">{{ ownedRouteCount }}</UBadge>
            </div>
            <p>{{ appCopy.subtitle }}</p>
          </div>

          <nav v-if="authSnapshot.hasToken" class="modular-nav" :aria-label="`${appCopy.badge} routes`">
            <section v-for="group in menuGroups" :key="group.key" class="modular-nav-group">
              <p class="modular-nav-label">{{ group.label }}</p>
              <UButton
                v-for="item in group.items"
                :key="item.id"
                :to="item.href"
                :icon="item.icon"
                color="neutral"
                :variant="isActive(item.href) ? 'soft' : 'ghost'"
                block
                class="modular-nav-link justify-start"
              >
                {{ item.label }}
              </UButton>
            </section>
          </nav>
        </div>

        <div class="modular-sidebar-footer">
          <UButton
            color="neutral"
            variant="ghost"
            icon="i-lucide-activity"
            block
            class="justify-start"
          >
            Status
            <UBadge :color="apiHealth.state === 'live' ? 'success' : apiHealth.state === 'checking' ? 'warning' : 'error'" variant="subtle" size="xs" class="ml-auto">
              {{ apiHealth.label }}
            </UBadge>
          </UButton>
          <UButton
            color="neutral"
            variant="ghost"
            icon="i-lucide-bell"
            block
            class="justify-start"
          >
            Notifications
            <UBadge color="neutral" variant="subtle" size="xs" class="ml-auto">0</UBadge>
          </UButton>
        </div>
      </aside>

      <section class="modular-main">
        <header class="modular-topbar">
          <div class="flex min-w-0 items-center gap-3">
            <UButton
              icon="i-lucide-panel-left"
              color="neutral"
              variant="ghost"
              class="lg:hidden"
              aria-label="Menu"
              @click="mobileNavOpen = true"
            />
            <div class="min-w-0">
              <div class="flex min-w-0 flex-wrap items-center gap-2">
                <h1 class="modular-page-title">{{ appCopy.title }}</h1>
                <UBadge color="primary" variant="subtle">{{ appCopy.badge }}</UBadge>
              </div>
              <p class="modular-page-subtitle">{{ activeUserLabel }}</p>
            </div>
          </div>

          <div class="modular-toolbar">
            <nav class="modular-app-switcher" aria-label="Garmetix apps">
              <UButton
                v-for="link in appLinks"
                :key="link.id"
                size="sm"
                :color="link.current ? 'primary' : 'neutral'"
                :variant="link.current ? 'solid' : 'subtle'"
                :to="link.href || undefined"
                :disabled="link.current || !link.configured"
                :external="!link.current"
              >
                {{ link.label }}
              </UButton>
            </nav>

            <UBadge color="neutral" variant="subtle" class="hidden xl:inline-flex">
              <UIcon name="i-lucide-store" class="size-3" />
              {{ workspaceLabel }}
            </UBadge>
            <UBadge color="neutral" variant="subtle" class="hidden xl:inline-flex">
              <UIcon name="i-lucide-clock-3" class="size-3" />
              {{ currentClock }}
            </UBadge>
            <UBadge :color="apiHealth.state === 'live' ? 'success' : apiHealth.state === 'checking' ? 'warning' : 'error'" variant="subtle" class="hidden 2xl:inline-flex">
              <UIcon :name="apiHealth.state === 'live' ? 'i-lucide-wifi' : 'i-lucide-wifi-off'" class="size-3" />
              {{ apiHealth.label }}
            </UBadge>
            <USelect v-model="selectedTheme" :items="themeOptions" class="hidden 2xl:flex w-28" aria-label="Theme" />
            <UColorModeButton color="neutral" variant="ghost" />
            <UButton v-if="authSnapshot.hasToken" color="neutral" variant="ghost" icon="i-lucide-log-out" aria-label="Logout" @click="logout" />
          </div>
        </header>

        <div class="modular-content">
          <slot />
        </div>
      </section>

      <USlideover v-model:open="mobileNavOpen" title="Garmetix">
        <template #body>
          <nav class="modular-nav">
            <section v-for="group in menuGroups" :key="group.key" class="modular-nav-group">
              <p class="modular-nav-label">{{ group.label }}</p>
              <UButton
                v-for="item in group.items"
                :key="item.id"
                :to="item.href"
                :icon="item.icon"
                color="neutral"
                :variant="isActive(item.href) ? 'soft' : 'ghost'"
                block
                class="modular-nav-link justify-start"
                @click="mobileNavOpen = false"
              >
                {{ item.label }}
              </UButton>
            </section>
          </nav>
        </template>
      </USlideover>
    </main>
  </UApp>
</template>

<script setup lang="ts">
import type { FrontendAppId } from '@garmetix/shared-types'
import { checkApiHealth, type ApiHealthResult } from '@garmetix/shared-api'
import { clearStoredSession, getAuthSessionSnapshot, type AuthSessionSnapshot } from '@garmetix/shared-auth'
import { buildAppShellModel, normalizeFrontendAppId } from '../src'
import { buildAppTargetLinks, garmetixRoutes } from '../../../config/routes'
import { garmetixModularVersion } from '../../../config/version'

type MenuItem = {
  id: string
  label: string
  href: string
  icon: string
}

type MenuGroup = {
  key: string
  label: string
  items: MenuItem[]
}

const props = defineProps<{
  appId?: FrontendAppId
}>()

const route = useRoute()
const runtimeConfig = useRuntimeConfig()
const colorMode = useColorMode()
const apiBaseUrl = computed(() => String(runtimeConfig.public.apiBaseUrl || ''))
const appUrls = computed(() => (runtimeConfig.public.appUrls ?? {}) as Record<string, string | undefined>)
const effectiveAppId = computed<FrontendAppId>(() => normalizeFrontendAppId(props.appId || runtimeConfig.public.appId))
const version = garmetixModularVersion
const mobileNavOpen = ref(false)
const now = ref<Date | null>(null)
const apiHealth = ref<ApiHealthResult>({
  state: 'checking',
  label: 'Checking',
  message: 'Waiting for the health endpoint.'
})
const authSnapshot = ref<AuthSessionSnapshot>({
  state: 'anonymous',
  hasToken: false,
  label: 'Checking auth',
  message: 'Reading browser token storage.'
})
let clockTimer: ReturnType<typeof setInterval> | undefined

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

const shell = computed(() => buildAppShellModel({
  appId: effectiveAppId.value,
  routes: garmetixRoutes,
  env: appUrls.value,
  appLinks: buildAppTargetLinks(appUrls.value, effectiveAppId.value)
}))

const appCopy = computed(() => ({
  title: shell.value.title,
  subtitle: shell.value.subtitle,
  badge: shell.value.badge
}))

const appLinks = computed(() => shell.value.appLinks)
const ownedRouteCount = computed(() => shell.value.routeCount)
const homePath = computed(() => menuGroups.value[0]?.items[0]?.href || '/')
const currentClock = computed(() => now.value?.toLocaleTimeString('en-IN', { hour: '2-digit', minute: '2-digit', hour12: true }) || '--:--')
const workspaceLabel = computed(() => {
  const user = authSnapshot.value.user
  return user?.storeId ? 'Store assigned' : 'All permitted stores'
})
const activeUserLabel = computed(() => {
  if (!authSnapshot.value.hasToken) return `${appCopy.value.badge} access - sign in to continue.`
  const user = authSnapshot.value.user
  const role = user?.role || user?.userType || (user?.isSuperAdmin ? 'SuperAdmin' : user?.admin ? 'Admin' : 'User')
  return `${user?.name || user?.userName || 'Signed in'} | ${role}`
})

const localMenus: Record<FrontendAppId, MenuGroup[]> = {
  main: [
    { key: 'dashboard', label: 'Dashboards', items: [
      { id: 'dashboard', label: 'Dashboard', href: '/', icon: 'i-lucide-gauge' },
      { id: 'today', label: "Today's", href: '/dashboard/todays', icon: 'i-lucide-sun' },
      { id: 'store', label: 'Store', href: '/dashboard/store-manager', icon: 'i-lucide-store' },
      { id: 'store-day', label: 'Store Operations', href: '/store-day', icon: 'i-lucide-sun-medium' }
    ] },
    { key: 'sales', label: 'Sales', items: [
      { id: 'billing', label: 'Sale Invoices', href: '/billing', icon: 'i-lucide-receipt-indian-rupee' },
      { id: 'tailoring', label: 'Tailoring', href: '/tailoring', icon: 'i-lucide-scissors' }
    ] },
    { key: 'purchase', label: 'Purchase', items: [
      { id: 'purchase', label: 'Purchase', href: '/purchase', icon: 'i-lucide-package-plus' },
      { id: 'purchase-new', label: 'New Inward', href: '/purchase/new', icon: 'i-lucide-file-plus-2' },
      { id: 'purchase-return', label: 'Purchase Return', href: '/purchase-return', icon: 'i-lucide-undo-2' }
    ] },
    { key: 'inventory', label: 'Inventory', items: [
      { id: 'inventory', label: 'Product Master', href: '/inventory', icon: 'i-lucide-boxes' },
      { id: 'stock-operations', label: 'Stock Operations', href: '/stock-operations', icon: 'i-lucide-arrow-left-right' }
    ] },
    { key: 'crm', label: 'Customers', items: [
      { id: 'customers', label: 'Customers', href: '/customers', icon: 'i-lucide-user-round' },
      { id: 'customer-new', label: 'New Customer', href: '/customers/new', icon: 'i-lucide-user-plus' }
    ] },
    { key: 'reports', label: 'Reports', items: [
      { id: 'reports', label: 'Reports Center', href: '/reports', icon: 'i-lucide-file-text' },
      { id: 'document-scan', label: 'Document Scanner', href: '/document-scan', icon: 'i-lucide-scan-qr-code' },
      { id: 'profile', label: 'Profile', href: '/profile', icon: 'i-lucide-circle-user-round' }
    ] }
  ],
  pos: [
    { key: 'counter', label: 'Counter', items: [
      { id: 'counter', label: 'Counter Home', href: '/', icon: 'i-lucide-layout-dashboard' },
      { id: 'day-open', label: 'Day Open', href: '/day-open', icon: 'i-lucide-sunrise' },
      { id: 'sale', label: 'Sale', href: '/sale', icon: 'i-lucide-scan-barcode' },
      { id: 'hold-bills', label: 'Hold Bills', href: '/hold-bills', icon: 'i-lucide-pause-circle' },
      { id: 'returns', label: 'Returns', href: '/returns', icon: 'i-lucide-rotate-ccw' },
      { id: 'exchange', label: 'Exchange', href: '/exchange', icon: 'i-lucide-repeat-2' },
      { id: 'print', label: 'Print Queue', href: '/print', icon: 'i-lucide-printer' },
      { id: 'day-close', label: 'Day Close', href: '/day-close', icon: 'i-lucide-sunset' }
    ] }
  ],
  hr: [
    { key: 'people', label: 'People', items: [
      { id: 'home', label: 'HR Home', href: '/', icon: 'i-lucide-layout-dashboard' },
      { id: 'employees', label: 'Employees', href: '/hr', icon: 'i-lucide-users-round' },
      { id: 'benefits', label: 'Benefits', href: '/hr-benefits', icon: 'i-lucide-heart-handshake' }
    ] },
    { key: 'attendance', label: 'Attendance', items: [
      { id: 'attendance', label: 'Attendance', href: '/attendance', icon: 'i-lucide-calendar-check' },
      { id: 'today', label: 'Today', href: '/attendance/today', icon: 'i-lucide-calendar-check-2' },
      { id: 'monthly', label: 'Monthly', href: '/attendance/monthly', icon: 'i-lucide-calendar-range' },
      { id: 'regularization', label: 'Regularization', href: '/attendance/regularization', icon: 'i-lucide-calendar-clock' },
      { id: 'devices', label: 'Devices', href: '/attendance/devices', icon: 'i-lucide-fingerprint' }
    ] },
    { key: 'payroll', label: 'Payroll', items: [
      { id: 'summary', label: 'Payroll Summary', href: '/attendance/payroll-summary', icon: 'i-lucide-file-spreadsheet' },
      { id: 'review', label: 'Payroll Review', href: '/attendance/payroll-review', icon: 'i-lucide-hand-coins' },
      { id: 'draft', label: 'Salary Draft', href: '/attendance/salary-draft', icon: 'i-lucide-receipt-indian-rupee' },
      { id: 'payment', label: 'Salary Payment', href: '/attendance/salary-payment', icon: 'i-lucide-wallet-cards' },
      { id: 'payroll', label: 'Payroll', href: '/payroll', icon: 'i-lucide-badge-indian-rupee' }
    ] }
  ],
  'ai-sense': [
    { key: 'analytics', label: 'Analytics', items: [
      { id: 'home', label: 'AI Home', href: '/', icon: 'i-lucide-brain-circuit' },
      { id: 'business', label: 'Business Dashboard', href: '/dashboard/business', icon: 'i-lucide-chart-no-axes-combined' },
      { id: 'sales', label: 'Sales Analysis', href: '/ai-sense/sales-analysis', icon: 'i-lucide-trending-up' },
      { id: 'purchase', label: 'Purchase Analysis', href: '/ai-sense/purchase-analysis', icon: 'i-lucide-chart-column-increasing' },
      { id: 'profit', label: 'Profit Analysis', href: '/ai-sense/profit-analysis', icon: 'i-lucide-chart-pie' },
      { id: 'stock-risk', label: 'Stock Risk', href: '/ai-sense/stock-risk', icon: 'i-lucide-package-search' },
      { id: 'stock-reports', label: 'Stock Reports', href: '/stock-reports', icon: 'i-lucide-chart-column-stacked' },
      { id: 'daily', label: 'Daily Summary', href: '/ai-sense/daily-summary', icon: 'i-lucide-calendar-days' }
    ] }
  ],
  books: [
    { key: 'accounting', label: 'Accounting', items: [
      { id: 'home', label: 'Books Home', href: '/', icon: 'i-lucide-layout-dashboard' },
      { id: 'accounting', label: 'Accounting', href: '/accounting', icon: 'i-lucide-landmark' },
      { id: 'parties', label: 'Parties', href: '/parties', icon: 'i-lucide-users-round' },
      { id: 'vouchers', label: 'Vouchers', href: '/vouchers', icon: 'i-lucide-banknote' },
      { id: 'petty-cash', label: 'Petty Cash', href: '/petty-cash', icon: 'i-lucide-circle-dollar-sign' },
      { id: 'cash-details', label: 'Cash Details', href: '/cash-details', icon: 'i-lucide-coins' }
    ] },
    { key: 'notes', label: 'Notes And GST', items: [
      { id: 'debit-notes', label: 'Debit Notes', href: '/debit-notes', icon: 'i-lucide-file-minus-2' },
      { id: 'credit-notes', label: 'Credit Notes', href: '/credit-notes', icon: 'i-lucide-file-plus-2' },
      { id: 'commercial-notes', label: 'Commercial Summary', href: '/commercial-notes', icon: 'i-lucide-files' },
      { id: 'gst-returns', label: 'GST Returns', href: '/gst-returns', icon: 'i-lucide-file-json-2' },
      { id: 'gst-reports', label: 'GST Reports', href: '/gst-reports', icon: 'i-lucide-table-properties' }
    ] },
    { key: 'audit', label: 'Audit', items: [
      { id: 'fy-locks', label: 'FY Locks', href: '/financial-year-locks', icon: 'i-lucide-lock-keyhole' },
      { id: 'audit', label: 'Audit', href: '/audit', icon: 'i-lucide-search-check' },
      { id: 'message-logs', label: 'Message Logs', href: '/message-logs', icon: 'i-lucide-message-square-warning' }
    ] }
  ],
  admin: [
    { key: 'setup', label: 'Company', items: [
      { id: 'home', label: 'Admin Home', href: '/', icon: 'i-lucide-layout-dashboard' },
      { id: 'setup', label: 'Company Setup', href: '/setup', icon: 'i-lucide-building-2' },
      { id: 'onboarding', label: 'Onboarding', href: '/client-onboarding', icon: 'i-lucide-route' },
      { id: 'access', label: 'Roles And Users', href: '/access', icon: 'i-lucide-shield-check' },
      { id: 'license', label: 'License', href: '/license-activation', icon: 'i-lucide-key-round' }
    ] },
    { key: 'data', label: 'Data And Audit', items: [
      { id: 'import-export', label: 'Import Export', href: '/import-export', icon: 'i-lucide-file-down' },
      { id: 'data-consistency', label: 'Data Consistency', href: '/data-consistency', icon: 'i-lucide-shield-alert' },
      { id: 'message-logs', label: 'Message Logs', href: '/message-logs', icon: 'i-lucide-list-collapse' }
    ] },
    { key: 'maintenance', label: 'Maintenance', items: [
      { id: 'system-health', label: 'System Health', href: '/system-health', icon: 'i-lucide-activity' },
      { id: 'runtime', label: 'Runtime Diagnostics', href: '/runtime-diagnostics', icon: 'i-lucide-stethoscope' },
      { id: 'backup', label: 'Backup Maintenance', href: '/backup-maintenance', icon: 'i-lucide-hard-drive-download' },
      { id: 'drive', label: 'Google Drive Backup', href: '/google-drive-backup', icon: 'i-lucide-cloud-upload' },
      { id: 'production', label: 'Production Readiness', href: '/production-readiness', icon: 'i-lucide-shield-check' },
      { id: 'support', label: 'Production Support', href: '/production-support', icon: 'i-lucide-life-buoy' }
    ] }
  ]
}

const menuGroups = computed(() => localMenus[effectiveAppId.value] ?? localMenus.main)

function isActive(href: string) {
  return route.path === href || (href !== '/' && route.path.startsWith(`${href}/`))
}

function refreshAuthSnapshot() {
  authSnapshot.value = getAuthSessionSnapshot(window.localStorage)
}

function logout() {
  clearStoredSession(window.localStorage)
  refreshAuthSnapshot()
  navigateTo('/login')
}

onMounted(async () => {
  now.value = new Date()
  clockTimer = setInterval(() => { now.value = new Date() }, 1000)
  refreshAuthSnapshot()
  apiHealth.value = await checkApiHealth(apiBaseUrl.value)
})

onBeforeUnmount(() => {
  if (clockTimer) clearInterval(clockTimer)
})

watch(() => route.fullPath, () => {
  if (import.meta.client) refreshAuthSnapshot()
})

useHead(() => ({
  title: appCopy.value.title,
  bodyAttrs: {
    class: 'garmetix-dashboard-shell garmetix-modern-dashboard-shell'
  }
}))
</script>
