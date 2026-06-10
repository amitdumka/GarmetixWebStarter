<script setup lang="ts">
import { APP_VERSION, APP_STAGE, APP_BUILD_CODE } from '~/utils/appVersion'

const api = useGarmetixApi()
const feedback = useUiFeedback()

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const loading = ref(false)

const dashboardLinks = [
  { label: 'Smart Dashboard', to: '/dashboard', icon: 'i-lucide-gauge', description: 'Role-aware landing page that routes users to the correct dashboard.' },
  { label: 'Store Manager Dashboard', to: '/dashboard/store-manager', icon: 'i-lucide-store', description: 'Current-store sales, stock, alerts and daily action view.' },
  { label: 'Owner / Admin Dashboard', to: '/dashboard/business', icon: 'i-lucide-chart-no-axes-combined', description: 'Company, store-group and store-level executive analytics.' },
  { label: 'Legacy Overview', to: '/', icon: 'i-lucide-layout-dashboard', description: 'Old dashboard page kept safely for rollback and comparison.' }
]

const mainMenuGroups = [
  { label: 'Dashboards', count: 8, description: 'Smart landing, store manager, business, reports and GST dashboards.' },
  { label: 'Operations', count: 17, description: 'Billing, inventory, purchase, returns, customers, vouchers and accounting.' },
  { label: 'People', count: 2, description: 'HR and payroll operations.' },
  { label: 'Account', count: 1, description: 'Logged-in user profile and password management.' },
  { label: 'Help', count: 3, description: 'About Us, Contact Us and FAQ pages with live version identity.' },
  { label: 'Admin', count: 12, description: 'Setup, onboarding, logs, access, health, repair and integration tools.' }
]

const templateChecklist = [
  'Nuxt UI dashboard-style shell is active by default.',
  'Legacy shell remains available with NUXT_PUBLIC_DASHBOARD_SHELL=legacy.',
  'Sidebar is controlled, collapsible, resizable and supports Ctrl/Cmd+B.',
  'Topbar includes workspace, refresh, message logs, theme, dashboard actions and user menu.',
  'Command menu opens with Ctrl/Cmd + K.',
  'Favorites and recent pages are stored locally per browser.',
  'Bottom utility navigation and footer account dropdown mirror Nuxt UI Dashboard template patterns.',
  'No major page was removed during Stage 7.'
]

async function refresh() {
  loading.value = true
  try {
    const [companyRows, storeRows] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores')
    ])
    companies.value = companyRows
    stores.value = storeRows
  } catch (error) {
    feedback.failed('Dashboard map load failed', error)
  } finally {
    loading.value = false
  }
}

onMounted(refresh)
</script>

<template>
  <AppShell title="Dashboard Map" :companies="companies" :stores="stores" @refresh="refresh" @workspace-change="refresh">
    <section class="dashboard-v3-page">
      <div class="dashboard-v3-hero business">
        <div>
          <UBadge color="primary" variant="subtle" icon="i-lucide-map">Stage 7E</UBadge>
          <h1>Dashboard implementation map</h1>
          <p>Template alignment, dashboard routing, menu preservation and rollback notes for v{{ APP_VERSION }}.</p>
        </div>
        <div class="dashboard-v3-hero-actions">
          <UBadge :color="loading ? 'warning' : 'success'" variant="subtle">{{ loading ? 'Loading' : 'Ready' }}</UBadge>
          <UButton icon="i-lucide-refresh-cw" label="Refresh" :loading="loading" @click="refresh" />
        </div>
      </div>

      <div class="dashboard-map-grid">
        <div class="dashboard-map-section">
          <UCard>
            <template #header>
              <div class="dashboard-v3-card-header">
                <div>
                  <h2>Dashboard pages</h2>
                  <p>Current role dashboard structure and safe legacy route.</p>
                </div>
              </div>
            </template>
            <div class="dashboard-map-menu-grid">
              <NuxtLink v-for="item in dashboardLinks" :key="item.to" :to="item.to" class="dashboard-map-menu-card">
                <UIcon :name="item.icon" class="h-5 w-5" />
                <span>
                  <strong>{{ item.label }}</strong>
                  <small>{{ item.description }}</small>
                </span>
              </NuxtLink>
            </div>
          </UCard>

          <UCard>
            <template #header>
              <div class="dashboard-v3-card-header">
                <div>
                  <h2>Preserved menu groups</h2>
                  <p>All current links remain available in the dashboard shell.</p>
                </div>
              </div>
            </template>
            <div class="dashboard-v3-health-grid">
              <div v-for="group in mainMenuGroups" :key="group.label" class="dashboard-v3-health-card">
                <UBadge color="neutral" variant="subtle" icon="i-lucide-panels-top-left">{{ group.count }} links</UBadge>
                <strong>{{ group.label }}</strong>
                <small>{{ group.description }}</small>
              </div>
            </div>
          </UCard>
        </div>

        <div class="dashboard-map-section">
          <UCard>
            <template #header>
              <div class="dashboard-v3-card-header">
                <div>
                  <h2>Version identity</h2>
                  <p>Use About Us or this page to identify the running code.</p>
                </div>
              </div>
            </template>
            <div class="profile-detail-grid">
              <div class="profile-detail-card"><span>Version</span><strong>{{ APP_VERSION }}</strong></div>
              <div class="profile-detail-card"><span>Stage</span><strong>{{ APP_STAGE }}</strong></div>
              <div class="profile-detail-card"><span>Build Code</span><strong>{{ APP_BUILD_CODE }}</strong></div>
            </div>
          </UCard>

          <UCard>
            <template #header>
              <div class="dashboard-v3-card-header">
                <div>
                  <h2>Stage 7 safeguards</h2>
                  <p>Revert and review options before removing any old page.</p>
                </div>
              </div>
            </template>
            <ul class="release-check-list">
              <li v-for="item in templateChecklist" :key="item">
                <UIcon name="i-lucide-check-circle-2" class="h-4 w-4" />
                <span>{{ item }}</span>
              </li>
            </ul>
          </UCard>

          <UAlert
            color="warning"
            variant="subtle"
            icon="i-lucide-shield-alert"
            title="Removal policy"
            description="No major existing page should be deleted until you confirm it after comparing the legacy and new dashboard routes."
          />
        </div>
      </div>
    </section>
  </AppShell>
</template>
