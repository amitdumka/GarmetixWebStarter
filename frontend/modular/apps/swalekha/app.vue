<template>
  <UApp>
    <UDashboardGroup unit="rem" class="garmetix-dashboard-group">
      <UDashboardSidebar
        id="swalekha-sidebar"
        v-model:open="sidebarOpen"
        collapsible
        resizable
        :default-size="16"
        :min-size="13"
        :max-size="22"
        :collapsed-size="4"
        class="garmetix-dashboard-sidebar bg-elevated/25"
        :ui="{ footer: 'lg:border-t lg:border-default' }"
      >
        <template #header="{ collapsed }">
          <UButton
            to="/"
            color="neutral"
            variant="ghost"
            block
            :square="collapsed"
            class="garmetix-shell-brand"
            :icon="collapsed ? 'i-lucide-book-heart' : undefined"
          >
            <template v-if="!collapsed" #default>
              <span class="flex min-w-0 items-center gap-2 text-left">
                <UIcon name="i-lucide-book-heart" class="size-5 shrink-0 text-primary" />
                <span class="min-w-0">
                  <span class="block truncate text-sm font-bold text-highlighted">Swalekha</span>
                  <span class="block truncate text-xs text-muted">Personal &amp; Personal Finance</span>
                </span>
              </span>
            </template>
          </UButton>
        </template>

        <template #default="{ collapsed }">
          <div v-if="!collapsed" class="garmetix-shell-context">
            <div class="flex items-center justify-between gap-2">
              <span class="truncate text-xs font-semibold text-highlighted">Owner-only</span>
              <UBadge :color="health?.ok ? 'success' : 'neutral'" variant="subtle" size="xs">
                {{ health?.ok ? 'DB connected' : 'DB status unknown' }}
              </UBadge>
            </div>
            <p>Fully isolated from the Garmetix business apps - separate database, separate login gate.</p>
          </div>

          <UButton
            v-if="!authSnapshot.hasToken"
            to="/login"
            icon="i-lucide-log-in"
            color="primary"
            variant="soft"
            block
            :square="collapsed"
            class="justify-start"
          >
            <span v-if="!collapsed">Login</span>
          </UButton>

          <UNavigationMenu
            v-if="authSnapshot.hasToken"
            :collapsed="collapsed"
            :items="navigationItems"
            orientation="vertical"
            tooltip
            popover
            class="garmetix-shell-navigation"
          />
        </template>

        <template #footer="{ collapsed }">
          <div class="garmetix-shell-footer">
            <UButton
              v-if="authSnapshot.hasToken"
              color="neutral"
              variant="ghost"
              block
              :square="collapsed"
              class="justify-start"
              :avatar="{ icon: 'i-lucide-user-round-check' }"
              :label="collapsed ? undefined : userLabel"
            />
            <UButton
              v-if="authSnapshot.hasToken"
              icon="i-lucide-log-out"
              color="neutral"
              variant="ghost"
              block
              :square="collapsed"
              class="justify-start"
              @click="logout"
            >
              <span v-if="!collapsed">Log out</span>
            </UButton>
          </div>
        </template>
      </UDashboardSidebar>

      <UDashboardPanel id="swalekha-workspace" class="garmetix-dashboard-panel">
        <template #header>
          <UDashboardNavbar :title="pageTitle" :ui="{ right: 'gap-2' }">
            <template #leading>
              <UDashboardSidebarCollapse title="Collapse sidebar" />
            </template>

            <template #trailing>
              <UBadge color="primary" variant="subtle">Personal Finance</UBadge>
            </template>

            <template #right>
              <UColorModeButton color="neutral" variant="ghost" />
            </template>
          </UDashboardNavbar>
        </template>

        <template #body>
          <div class="garmetix-dashboard-content">
            <NuxtPage />
          </div>
        </template>
      </UDashboardPanel>
    </UDashboardGroup>
  </UApp>
</template>

<script setup lang="ts">
import type { NavigationMenuItem } from '@nuxt/ui'
import { clearStoredSession, getAuthSessionSnapshot, type AuthSessionSnapshot } from '@garmetix/shared-auth'
import { useSwalekhaApiClient, type SwalekhaHealth } from './utils/swalekha-api'

// Deliberately not using packages/shared-ui/components/ModularAppShell.vue - that component's
// header is an app-switcher dropdown linking to every business app (main/pos/hr/books/...).
// Swalekha is a fully isolated module (Owner-only, own database, not part of the app switcher),
// so this is its own shell instead - same Dashboard Layout structure (top bar + collapsible
// sidebar nav), reusing the shared garmetix-dashboard-* CSS classes for visual consistency,
// but with only Swalekha's own routes in the nav and no cross-app link anywhere.

const route = useRoute()
const sidebarOpen = ref(false)
const authSnapshot = ref<AuthSessionSnapshot>({ state: 'anonymous', hasToken: false, label: '', message: '' })
const health = ref<SwalekhaHealth | null>(null)

const navItems = [
  { label: 'Dashboard', icon: 'i-lucide-layout-dashboard', to: '/' },
  { label: 'My Profile', icon: 'i-lucide-id-card', to: '/profile' },
  { label: 'Family Members', icon: 'i-lucide-heart-handshake', to: '/family' },
  { label: 'Accounts Hub', icon: 'i-lucide-landmark', to: '/accounts' },
  { label: 'Contacts', icon: 'i-lucide-users', to: '/contacts' },
  { label: 'Expenses', icon: 'i-lucide-receipt', to: '/expenses' },
  { label: 'Income', icon: 'i-lucide-wallet', to: '/income' },
  { label: 'Recurring Bills', icon: 'i-lucide-calendar-clock', to: '/recurring-bills' },
  { label: 'Trips', icon: 'i-lucide-plane', to: '/trips' }
]

function isActive(to: string) {
  if (to === '/') return route.path === '/'
  return route.path === to || route.path.startsWith(`${to}/`)
}

const navigationItems = computed<NavigationMenuItem[]>(() => navItems.map(item => ({
  label: item.label,
  icon: item.icon,
  to: item.to,
  active: isActive(item.to),
  onSelect: () => {
    sidebarOpen.value = false
  }
})))

const pageTitle = computed(() => navItems.find(item => isActive(item.to))?.label || 'Swalekha')
const userLabel = computed(() => authSnapshot.value.user?.name || authSnapshot.value.user?.userName || 'Owner')

onMounted(async () => {
  authSnapshot.value = getAuthSessionSnapshot(window.localStorage)
  if (authSnapshot.value.hasToken) {
    try {
      const api = useSwalekhaApiClient()
      health.value = await api.get<SwalekhaHealth>('health')
    } catch {
      health.value = null
    }
  }
})

function logout() {
  clearStoredSession(window.localStorage)
  authSnapshot.value = getAuthSessionSnapshot(window.localStorage)
  navigateTo('/login')
}

// The garmetix-dashboard-* CSS (shared-ui/assets/modular-shell.css) sizes the sidebar/panel to
// fill the viewport via this body class - without it the dashboard layout can look collapsed.
useHead({ bodyAttrs: { class: 'garmetix-dashboard-shell' } })
</script>
