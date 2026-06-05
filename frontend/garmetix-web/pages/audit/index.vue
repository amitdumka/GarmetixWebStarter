<script setup lang="ts">
import { h, resolveComponent } from 'vue'
import type { TableColumn } from '@nuxt/ui'

const api = useGarmetixApi()
const auth = useAuth()
const feedback = useUiFeedback()
const isAuthenticated = auth.isAuthenticated

const UBadge = resolveComponent('UBadge')

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const activities = ref<any[]>([])
const loading = ref(false)
const search = ref('')
const selectedModule = ref('all')

const moduleOptions = [
  { value: 'all', label: 'All Modules' },
  { value: 'Company', label: 'Company' },
  { value: 'Inventory', label: 'Inventory' },
  { value: 'Billing', label: 'Billing' },
  { value: 'Purchase', label: 'Purchase' },
  { value: 'Vouchers', label: 'Vouchers' },
  { value: 'Petty Cash', label: 'Petty Cash' },
  { value: 'HR', label: 'HR' },
  { value: 'Payroll', label: 'Payroll' }
]

const metrics = computed(() => [
  {
    label: 'Activities',
    value: activities.value.length,
    meta: 'Recent records',
    icon: 'i-lucide-history',
    color: 'primary'
  },
  {
    label: 'Created',
    value: activities.value.filter((item) => item.action === 'Created').length,
    meta: 'New records',
    icon: 'i-lucide-plus-circle',
    color: 'success'
  },
  {
    label: 'Updated',
    value: activities.value.filter((item) => item.action === 'Updated').length,
    meta: 'Changed records',
    icon: 'i-lucide-pencil',
    color: 'warning'
  },
  {
    label: 'Modules',
    value: new Set(activities.value.map((item) => item.module)).size,
    meta: 'Touched areas',
    icon: 'i-lucide-layout-grid',
    color: 'neutral'
  }
])

const rows = computed(() => activities.value.map((item) => ({
  onDate: formatDateTime(item.onDate),
  module: item.module,
  entity: item.entity,
  reference: item.reference,
  action: item.action,
  actor: item.actor || 'System',
  raw: item
})))

const filteredRows = computed(() => {
  const term = search.value.trim().toLowerCase()
  if (!term) {
    return rows.value
  }

  return rows.value.filter((row) => JSON.stringify(row).toLowerCase().includes(term))
})

const columns: TableColumn<any>[] = [
  { accessorKey: 'onDate', header: 'Date' },
  {
    accessorKey: 'module',
    header: 'Module',
    cell: ({ row }) => h(UBadge, {
      color: moduleColor(row.original.module),
      variant: 'subtle'
    }, () => row.original.module)
  },
  { accessorKey: 'entity', header: 'Entity' },
  { accessorKey: 'reference', header: 'Reference' },
  {
    accessorKey: 'action',
    header: 'Action',
    cell: ({ row }) => h(UBadge, {
      color: row.original.action === 'Created' ? 'success' : 'warning',
      variant: 'subtle'
    }, () => row.original.action)
  },
  { accessorKey: 'actor', header: 'Actor' }
]

async function refresh() {
  if (!auth.isAuthenticated.value) {
    return
  }

  loading.value = true
  try {
    const moduleQuery = selectedModule.value === 'all' ? '' : `&module=${encodeURIComponent(selectedModule.value)}`
    const [companyRows, storeRows, activityRows] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores'),
      api.get<any[]>(`audit/recent?take=250${moduleQuery}`)
    ])

    companies.value = companyRows
    stores.value = storeRows
    activities.value = activityRows
  } catch (error) {
    feedback.failed('Audit refresh failed', error)
  } finally {
    loading.value = false
  }
}

function moduleColor(moduleName: string) {
  const key = String(moduleName || '').toLowerCase()
  if (key === 'billing' || key === 'payroll') {
    return 'success'
  }

  if (key === 'purchase' || key === 'petty cash') {
    return 'warning'
  }

  if (key === 'hr') {
    return 'primary'
  }

  return 'neutral'
}

function formatDateTime(value: string) {
  return value ? new Date(value).toLocaleString() : '-'
}

watch(selectedModule, () => {
  refresh()
})

onMounted(async () => {
  auth.restore()
  await refresh()
})
</script>

<template>
  <AuthScreen v-if="!isAuthenticated" @authenticated="refresh" />

  <AppShell
    v-else
    title="Audit"
    :companies="companies"
    :stores="stores"
    @refresh="refresh"
  >
    <section class="planner-dashboard">
      <UiModulePageHeader
        title="Audit"
        description="Review recent created and updated records across Garmetix modules."
        icon="i-lucide-history"
        primary-label="Refresh"
        primary-icon="i-lucide-refresh-cw"
        @primary="refresh"
      >
        <template #actions>
          <UBadge :color="loading ? 'warning' : 'success'" variant="subtle">
            {{ loading ? 'Loading' : 'Ready' }}
          </UBadge>
          <USelect v-model="selectedModule" :items="moduleOptions" class="w-44" />
        </template>
      </UiModulePageHeader>

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

      <UCard class="planner-card">
        <template #header>
          <div class="setup-list-header">
            <div>
              <h3>Activity Register</h3>
              <p>Generated from existing record timestamps.</p>
            </div>
            <UBadge color="neutral" variant="subtle">{{ filteredRows.length }} shown</UBadge>
          </div>
        </template>

        <UiCrudToolbar
          v-model:search="search"
          search-placeholder="Search module, entity, reference, or actor"
          :loading="loading"
          refresh-label="Sync"
          create-label="Refresh"
          @refresh="refresh"
          @create="refresh"
        />

        <UTable
          v-if="filteredRows.length"
          :data="filteredRows"
          :columns="columns"
          :loading="loading"
        />

        <UiCrudEmptyState
          v-else
          title="No audit activity found"
          description="Create or update records in modules to populate the activity feed."
          icon="i-lucide-history"
          action-label="Refresh"
          @action="refresh"
        />
      </UCard>
    </section>
  </AppShell>
</template>
