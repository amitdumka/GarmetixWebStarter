<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const feedback = useUiFeedback()
const isAuthenticated = auth.isAuthenticated

const loading = ref(false)
const loadError = ref('')
const status = ref<any | null>(null)
const PERMISSION_ACCEPTANCE_KEY = 'garmetix:permission-final-acceptance:v1'
const acceptanceState = reactive<Record<string, boolean>>({})
const acceptanceNote = ref('')

const checks = ref([
  { key: 'admin', label: 'Admin / Owner login checked', detail: 'Can access setup, users, maintenance and data tools.' },
  { key: 'storeManager', label: 'Store Manager checked', detail: 'Can access store operations but not admin-only maintenance.' },
  { key: 'billing', label: 'Billing user checked', detail: 'Can create bills and returns according to role.' },
  { key: 'purchase', label: 'Purchase user checked', detail: 'Can manage purchase inward/returns but not unrelated admin areas.' },
  { key: 'accountant', label: 'Accountant checked', detail: 'Can access vouchers, petty cash, GST and accounting reports.' },
  { key: 'hrPayroll', label: 'HR / Payroll checked', detail: 'Can access employee/payroll flows according to assigned role.' },
  { key: 'scoping', label: 'Company/store scoping checked', detail: 'Scoped users cannot see other company/store records.' },
  { key: 'deleteRights', label: 'Delete/export/backup restrictions checked', detail: 'Only permitted users can delete, export sensitive data or restore backups.' },
  { key: 'blockedRoutes', label: 'Blocked route checks completed', detail: 'Each role sees Access Denied for routes it must not use.' },
  { key: 'landingRoutes', label: 'Landing routes confirmed', detail: 'Admin, Store Manager, HR, Payroll and Billing roles land on the expected dashboard/page.' }
])

const completedChecks = computed(() => checks.value.filter((check) => acceptanceState[check.key]).length)
const ready = computed(() => status.value?.hasAdmin && status.value?.hasScopedUsers && status.value?.hasRoleMatrixCoverage && completedChecks.value === checks.value.length)

function loadAcceptance() {
  if (typeof window === 'undefined') return
  try {
    const saved = JSON.parse(window.localStorage.getItem(PERMISSION_ACCEPTANCE_KEY) || '{}')
    if (saved.state) Object.assign(acceptanceState, saved.state)
    acceptanceNote.value = saved.note || ''
  } catch {
    // Ignore browser checklist cache.
  }
}

function saveAcceptance() {
  if (typeof window === 'undefined') return
  window.localStorage.setItem(PERMISSION_ACCEPTANCE_KEY, JSON.stringify({
    state: acceptanceState,
    note: acceptanceNote.value,
    savedAt: new Date().toISOString()
  }))
}

watch(acceptanceState, saveAcceptance, { deep: true })
watch(acceptanceNote, saveAcceptance)

async function refresh() {
  if (!auth.isAuthenticated.value) return
  loading.value = true
  loadError.value = ''
  try {
    status.value = await api.get<any>('permission-acceptance/status')
    feedback.notify('Permission acceptance refreshed', 'User role coverage was checked.', 'success')
  } catch (error) {
    loadError.value = feedback.errorMessage(error, 'Please check admin permission and API service.', 'Permission acceptance failed')
    feedback.failed('Permission acceptance failed', error)
  } finally {
    loading.value = false
  }
}

onMounted(async () => {
  auth.restore()
  loadAcceptance()
  await refresh()
})
</script>

<template>
  <AuthScreen v-if="!isAuthenticated" @authenticated="refresh" />

  <AppShell
    v-else
    title="Permission Final Acceptance"
    @refresh="refresh"
  >
    <section class="permission-acceptance-page">
      <UiModulePageHeader
        title="Permission Final Acceptance"
        description="Final role-wise user access acceptance for Admin, Store Manager, Billing, Purchase, Accountant and HR/Payroll users."
        icon="i-lucide-shield-check"
        primary-label="Run Checks"
        primary-icon="i-lucide-refresh-cw"
        @primary="refresh"
      >
        <template #actions>
          <UBadge :color="ready ? 'success' : 'warning'" :label="ready ? 'Permissions accepted' : `${completedChecks}/${checks.length} checked`" variant="subtle" />
          <UButton to="/access" icon="i-lucide-users" variant="subtle" label="Roles & Users" />
        </template>
      </UiModulePageHeader>

      <UAlert v-if="loadError" color="error" variant="subtle" icon="i-lucide-circle-alert" title="Permission acceptance unavailable" :description="loadError" />

      <div class="metric-grid">
        <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-users" color="primary" variant="subtle" /><div><p>Total Users</p><strong>{{ status?.totalUsers || 0 }}</strong><span>{{ status?.activeUsers || 0 }} active</span></div></div></UCard>
        <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-shield-check" :color="status?.hasAdmin ? 'success' : 'error'" variant="subtle" /><div><p>Admin Users</p><strong>{{ status?.adminUsers || 0 }}</strong><span>Admin/Owner coverage</span></div></div></UCard>
        <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-store" :color="status?.hasScopedUsers ? 'success' : 'warning'" variant="subtle" /><div><p>Scoped Users</p><strong>{{ status?.scopedUsers || 0 }}</strong><span>Store/company isolation test</span></div></div></UCard>
        <UCard class="planner-metric-card"><div class="planner-metric-body"><UAvatar icon="i-lucide-route" :color="status?.hasRoleMatrixCoverage ? 'success' : 'warning'" variant="subtle" /><div><p>Role Matrix</p><strong>{{ status?.readyRoleCount || 0 }}/{{ status?.requiredRoleCount || 0 }}</strong><span>required roles ready</span></div></div></UCard>
      </div>

      <UCard class="planner-card">
        <template #header><h2>Role Coverage</h2></template>
        <div class="table-wrap">
          <table>
            <thead><tr><th>Role</th><th>Total</th><th>Active</th><th>Admin</th><th>Status</th><th>Message</th></tr></thead>
            <tbody>
              <tr v-for="role in status?.roles || []" :key="role.role">
                <td>{{ role.role }}</td>
                <td>{{ role.userCount }}</td>
                <td>{{ role.activeUserCount }}</td>
                <td>{{ role.adminUserCount }}</td>
                <td><UBadge :color="role.status === 'Ready' ? 'success' : 'warning'" :label="role.status" variant="subtle" /></td>
                <td>{{ role.message }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </UCard>


      <UCard class="planner-card">
        <template #header><h2>Effective permission matrix</h2></template>
        <div class="table-wrap">
          <table>
            <thead><tr><th>Role</th><th>Entry</th><th>Edit</th><th>Delete</th><th>Admin</th><th>Modules</th><th>Status</th></tr></thead>
            <tbody>
              <tr v-for="row in status?.matrix || []" :key="row.role">
                <td><strong>{{ row.role }}</strong><br><small>{{ row.notes }}</small></td>
                <td>{{ row.entry ? 'Yes' : 'No' }}</td>
                <td>{{ row.edit ? 'Yes' : 'No' }}</td>
                <td>{{ row.delete ? 'Yes' : 'No' }}</td>
                <td>{{ row.adminWorkspace ? 'Yes' : 'No' }}</td>
                <td>{{ (row.modules || []).join(', ') || '-' }}</td>
                <td><UBadge :color="row.acceptanceStatus === 'Ready' ? 'success' : 'warning'" :label="row.acceptanceStatus" variant="subtle" /></td>
              </tr>
            </tbody>
          </table>
        </div>
      </UCard>

      <UCard class="planner-card">
        <template #header><h2>Route acceptance checklist</h2></template>
        <div class="route-grid">
          <div v-for="item in status?.routeExpectations || []" :key="item.role" class="route-card">
            <div class="route-card-head">
              <strong>{{ item.role }}</strong>
              <UBadge color="primary" variant="subtle">{{ item.landingRoute }}</UBadge>
            </div>
            <p>{{ item.testUserHint }}</p>
            <small><b>Allowed:</b> {{ (item.allowedRoutes || []).join(', ') || '-' }}</small>
            <small><b>Blocked:</b> {{ (item.blockedRoutes || []).join(', ') || '-' }}</small>
          </div>
        </div>
      </UCard>

      <UCard class="planner-card">
        <template #header><h2>Manual role-wise acceptance</h2></template>
        <div class="check-grid">
          <label v-for="check in checks" :key="check.key" class="check-card">
            <UCheckbox v-model="acceptanceState[check.key]" />
            <span>
              <strong>{{ check.label }}</strong>
              <small>{{ check.detail }}</small>
            </span>
          </label>
        </div>
        <UFormField class="mt-4" label="Permission acceptance note">
          <UTextarea v-model="acceptanceNote" :rows="3" placeholder="Example: Login tested as Admin, Billing user, Accountant. Store-scoped user cannot see other store." />
        </UFormField>
      </UCard>

      <UCard class="planner-card">
        <template #header><h2>Recommendations</h2></template>
        <ul class="recommendations">
          <li v-for="item in status?.recommendations || []" :key="item">
            <UIcon name="i-lucide-check-circle-2" />
            <span>{{ item }}</span>
          </li>
        </ul>
      </UCard>
    </section>
  </AppShell>
</template>

<style scoped>
.permission-acceptance-page { display: grid; gap: 1rem; }
.metric-grid, .check-grid, .route-grid { display: grid; gap: 1rem; }
.metric-grid { grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); }
.check-grid, .route-grid { grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); }
.check-card {
  display: flex;
  gap: .75rem;
  border: 1px solid rgb(var(--color-gray-200));
  border-radius: 1rem;
  padding: 1rem;
}
.check-card strong { display: block; }
.check-card small { color: rgb(var(--color-gray-500)); }
.table-wrap { overflow-x: auto; }
table { width: 100%; min-width: 860px; border-collapse: collapse; }
th, td { padding: .65rem .75rem; border-bottom: 1px solid rgb(var(--color-gray-200)); text-align: left; }
th { font-size: .75rem; text-transform: uppercase; color: rgb(var(--color-gray-500)); }
.route-card { border: 1px solid rgb(var(--color-gray-200)); border-radius: 1rem; padding: 1rem; display: grid; gap: .5rem; }
.route-card-head { display: flex; gap: .5rem; justify-content: space-between; align-items: center; }
.route-card p { margin: 0; color: rgb(var(--color-gray-600)); }
.route-card small { display: block; color: rgb(var(--color-gray-500)); }
.recommendations { display: grid; gap: .65rem; padding: 0; margin: 0; list-style: none; }
.recommendations li { display: flex; gap: .5rem; align-items: flex-start; }
.dark .check-card, .dark .route-card, .dark th, .dark td { border-color: rgb(var(--color-gray-800)); }
</style>
