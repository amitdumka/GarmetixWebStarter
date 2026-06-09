<script setup lang="ts">
import { h, resolveComponent } from 'vue'
import type { TableColumn } from '@nuxt/ui'

const api = useGarmetixApi()
const auth = useAuth()
const feedback = useUiFeedback()
const isAuthenticated = auth.isAuthenticated

const UBadge = resolveComponent('UBadge')
const UButton = resolveComponent('UButton')

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const activities = ref<any[]>([])
const loading = ref(false)
const search = ref('')
const selectedModule = ref('all')
const selectedAction = ref('all')
const actorFilter = ref('')
const entityFilter = ref('')
const fromDate = ref('')
const toDate = ref('')
const selectedDetail = ref<any | null>(null)
const detailLoading = ref(false)
const detailOpen = computed({
  get: () => Boolean(selectedDetail.value),
  set: (value: boolean) => { if (!value) selectedDetail.value = null }
})

const moduleOptions = [
  { value: 'all', label: 'All Modules' },
  { value: 'Company', label: 'Company' },
  { value: 'Inventory', label: 'Inventory' },
  { value: 'Billing', label: 'Billing' },
  { value: 'Purchase', label: 'Purchase' },
  { value: 'Vouchers', label: 'Vouchers' },
  { value: 'Accounting', label: 'Accounting' },
  { value: 'Petty Cash', label: 'Petty Cash' },
  { value: 'HR', label: 'HR' },
  { value: 'Payroll', label: 'Payroll' },
  { value: 'GST Returns', label: 'GST Returns' }
]

const actionOptions = [
  { value: 'all', label: 'All Actions' },
  { value: 'Created', label: 'Created' },
  { value: 'Updated', label: 'Updated' },
  { value: 'Deleted', label: 'Deleted' }
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
    label: 'Deleted',
    value: activities.value.filter((item) => item.action === 'Deleted').length,
    meta: 'Soft-deleted records',
    icon: 'i-lucide-trash-2',
    color: 'error'
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
  entityId: item.entityId,
  raw: item
})))

const filteredRows = computed(() => {
  const term = search.value.trim().toLowerCase()
  return rows.value.filter((row) => {
    if (term && !JSON.stringify(row).toLowerCase().includes(term)) {
      return false
    }

    if (selectedAction.value !== 'all' && row.action !== selectedAction.value) {
      return false
    }

    if (actorFilter.value.trim() && !row.actor.toLowerCase().includes(actorFilter.value.trim().toLowerCase())) {
      return false
    }

    if (entityFilter.value.trim() && !row.entity.toLowerCase().includes(entityFilter.value.trim().toLowerCase())) {
      return false
    }

    const rowTime = row.raw?.onDate ? new Date(row.raw.onDate).getTime() : 0
    if (fromDate.value && rowTime < new Date(fromDate.value).getTime()) {
      return false
    }

    if (toDate.value && rowTime > new Date(`${toDate.value}T23:59:59`).getTime()) {
      return false
    }

    return true
  })
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
      color: actionColor(row.original.action),
      variant: 'subtle'
    }, () => row.original.action)
  },
  { accessorKey: 'actor', header: 'Actor' },
  {
    id: 'actions',
    header: '',
    cell: ({ row }) => h(UButton, {
      color: 'neutral',
      variant: 'ghost',
      icon: 'i-lucide-list-tree',
      label: 'Fields',
      onClick: () => viewDetail(row.original.raw.entityId)
    })
  }
]

async function refresh() {
  if (!auth.isAuthenticated.value) {
    return
  }

  loading.value = true
  try {
    const query = new URLSearchParams({ take: '500' })
    if (selectedModule.value !== 'all') query.set('module', selectedModule.value)
    if (selectedAction.value !== 'all') query.set('action', selectedAction.value)
    if (actorFilter.value.trim()) query.set('actor', actorFilter.value.trim())
    if (entityFilter.value.trim()) query.set('entity', entityFilter.value.trim())
    if (fromDate.value) query.set('from', fromDate.value)
    if (toDate.value) query.set('to', toDate.value)
    if (search.value.trim()) query.set('search', search.value.trim())
    const [companyRows, storeRows, activityRows] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores'),
      api.get<any[]>(`audit/recent?${query.toString()}`)
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

async function viewDetail(entityId: string) {
  detailLoading.value = true
  try {
    selectedDetail.value = await api.get<any>(`audit/${entityId}`)
  } catch (error) {
    feedback.failed('Could not load audit fields', error)
  } finally {
    detailLoading.value = false
  }
}

function exportAuditCsv() {
  const headers = ['Date', 'Module', 'Entity', 'Reference', 'Action', 'Actor']
  const csvRows = filteredRows.value.map((row) => [row.onDate, row.module, row.entity, row.reference, row.action, row.actor])
  const csv = [headers, ...csvRows]
    .map((row) => row.map((cell) => `"${String(cell ?? '').replace(/"/g, '""')}"`).join(','))
    .join('\n')
  const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = `Garmetix-Audit-${new Date().toISOString().slice(0, 10)}.csv`
  document.body.appendChild(link)
  link.click()
  link.remove()
  URL.revokeObjectURL(url)
  feedback.notify('Audit export ready', `${filteredRows.value.length} rows exported.`)
}

function clearFilters() {
  search.value = ''
  selectedModule.value = 'all'
  selectedAction.value = 'all'
  actorFilter.value = ''
  entityFilter.value = ''
  fromDate.value = ''
  toDate.value = ''
  refresh()
}

function moduleColor(moduleName: string) {
  const key = String(moduleName || '').toLowerCase()
  if (key === 'billing' || key === 'payroll') {
    return 'success'
  }

  if (key === 'purchase' || key === 'petty cash') {
    return 'warning'
  }

  if (key === 'hr' || key === 'accounting') {
    return 'primary'
  }

  return 'neutral'
}

function actionColor(action: string) {
  const key = String(action || '').toLowerCase()
  if (key === 'created') {
    return 'success'
  }

  if (key === 'deleted') {
    return 'error'
  }

  return 'warning'
}

function formatDateTime(value: string) {
  return value ? new Date(value).toLocaleString() : '-'
}

watch(selectedModule, () => { refresh() })
watch(selectedAction, () => { refresh() })

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
          <USelect v-model="selectedAction" :items="actionOptions" class="w-36" />
          <UButton color="neutral" variant="subtle" icon="i-lucide-download" label="Export CSV" @click="exportAuditCsv" />
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
              <h3>Audit Filters</h3>
              <p>Filter by module, user, date, action, entity, or keyword.</p>
            </div>
            <UButton color="neutral" variant="ghost" icon="i-lucide-filter-x" label="Clear" @click="clearFilters" />
          </div>
        </template>
        <div class="form-grid four-column">
          <UFormField label="Module">
            <USelect v-model="selectedModule" :items="moduleOptions" />
          </UFormField>
          <UFormField label="Action">
            <USelect v-model="selectedAction" :items="actionOptions" />
          </UFormField>
          <UFormField label="Actor / user">
            <UInput v-model="actorFilter" placeholder="User name" @keyup.enter="refresh" />
          </UFormField>
          <UFormField label="Entity">
            <UInput v-model="entityFilter" placeholder="Invoice, Product..." @keyup.enter="refresh" />
          </UFormField>
          <UFormField label="From date">
            <UInput v-model="fromDate" type="date" @change="refresh" />
          </UFormField>
          <UFormField label="To date">
            <UInput v-model="toDate" type="date" @change="refresh" />
          </UFormField>
          <UFormField label="Keyword">
            <UInput v-model="search" placeholder="Reference or text" @keyup.enter="refresh" />
          </UFormField>
          <div class="form-action-field">
            <UButton icon="i-lucide-search" label="Apply" :loading="loading" @click="refresh" />
          </div>
        </div>
      </UCard>

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
      <UModal v-model:open="detailOpen" title="Audit Field Details" :ui="{ content: 'max-w-3xl' }">
        <template #body>
          <div v-if="detailLoading" class="empty-state-card">Loading field details...</div>
          <div v-else-if="selectedDetail" class="audit-detail-grid">
            <UAlert
              color="neutral"
              variant="subtle"
              :title="`${selectedDetail.entity} / ${selectedDetail.entityId}`"
              :description="`Created: ${formatDateTime(selectedDetail.createdAt)} | Updated: ${formatDateTime(selectedDetail.updatedAt)} | Deleted: ${selectedDetail.deleted}`"
            />
            <div class="planner-table-wrap">
              <table class="planner-table">
                <thead><tr><th>Changed field</th><th>Before</th><th>After</th></tr></thead>
                <tbody>
                  <tr v-for="field in selectedDetail.changedFields" :key="field.field">
                    <td>{{ field.field }}</td>
                    <td>{{ field.before || '-' }}</td>
                    <td>{{ field.after || '-' }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
            <USeparator label="Current field values" />
            <div class="planner-table-wrap">
              <table class="planner-table">
                <thead><tr><th>Field</th><th>Value</th></tr></thead>
                <tbody>
                  <tr v-for="field in selectedDetail.fields" :key="field.name">
                    <td>{{ field.name }}</td>
                    <td>{{ field.value || '-' }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </template>
        <template #footer>
          <UButton color="neutral" variant="outline" label="Close" @click="selectedDetail = null" />
        </template>
      </UModal>

    </section>
  </AppShell>
</template>
