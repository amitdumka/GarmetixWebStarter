<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const feedback = useUiFeedback()

const loading = ref(false)
const running = ref(false)
const repairing = ref(false)
const testing = ref(false)
const status = ref<any | null>(null)
const testResult = ref<any | null>(null)
const runResult = ref<any | null>(null)
const selectedEntity = ref('')

const entityOptions = computed(() => [
  { label: 'All configured entities', value: '' },
  ...(status.value?.entityNames || []).map((name: string) => ({ label: name, value: name }))
])

const metrics = computed(() => [
  {
    label: 'Oracle Sync',
    value: status.value?.enabled ? 'Enabled' : 'Disabled',
    meta: status.value?.configured ? 'Ready to connect' : 'Configure connection string first',
    icon: status.value?.enabled ? 'i-lucide-cloud' : 'i-lucide-cloud-off',
    color: status.value?.configured ? 'success' : 'warning'
  },
  {
    label: 'Direction',
    value: status.value?.direction || '-',
    meta: 'PostgreSQL primary → Oracle shared hub',
    icon: 'i-lucide-arrow-right-left',
    color: 'primary'
  },
  {
    label: 'Last Run',
    value: status.value?.lastRunUtc ? formatDate(status.value.lastRunUtc) : 'Never',
    meta: status.value?.lastError || 'No error recorded',
    icon: status.value?.lastError ? 'i-lucide-triangle-alert' : 'i-lucide-check-circle-2',
    color: status.value?.lastError ? 'error' : 'success'
  },
  {
    label: 'Entities',
    value: status.value?.entityNames?.length || 0,
    meta: `${status.value?.batchSize || 0} rows per run`,
    icon: 'i-lucide-database-zap',
    color: 'neutral'
  }
])

async function refresh() {
  if (!auth.isAuthenticated.value || !auth.canSeeAdmin.value) return
  loading.value = true
  try {
    status.value = await api.get<any>('oracle-sync/status')
  } catch (error) {
    feedback.failed('Could not load Oracle sync status', error)
  } finally {
    loading.value = false
  }
}

async function testConnection() {
  testing.value = true
  try {
    testResult.value = await api.create<any>('oracle-sync/test', {})
    feedback.notify('Oracle connection succeeded', testResult.value?.serverTimeUtc || '', 'success')
  } catch (error) {
    testResult.value = null
    feedback.failed('Oracle connection test failed', error)
  } finally {
    testing.value = false
  }
}

async function repair() {
  repairing.value = true
  try {
    await api.create<any>('oracle-sync/repair', {})
    feedback.saved('Oracle sync storage repaired')
    await refresh()
  } catch (error) {
    feedback.failed('Could not repair Oracle sync storage', error)
  } finally {
    repairing.value = false
  }
}

async function runNow() {
  running.value = true
  try {
    runResult.value = await api.create<any>('oracle-sync/run', {
      entityName: selectedEntity.value || null,
      repairFirst: true
    })
    feedback.notify('Oracle sync completed', `${runResult.value?.totalPushed || 0} event(s) pushed`, runResult.value?.success ? 'success' : 'warning')
    await refresh()
  } catch (error) {
    feedback.failed('Oracle sync failed', error)
  } finally {
    running.value = false
  }
}

function formatDate(value: string) {
  return value ? new Date(value).toLocaleString('en-IN') : '-'
}

onMounted(async () => { auth.restore(); await refresh() })
</script>

<template>
  <AuthScreen v-if="!auth.isAuthenticated.value" />
  <AppShell v-else title="Oracle Secondary Sync" @refresh="refresh">
    <UiModulePageHeader
      title="Oracle Secondary Sync"
      description="Sync PostgreSQL primary data to an Oracle Cloud secondary database that can act as a shared common ground for connected apps."
      icon="i-lucide-database-zap"
    >
      <template #actions>
        <UButton label="Run Sync" icon="i-lucide-play" :loading="running" @click="runNow" />
      </template>
    </UiModulePageHeader>

    <UAlert
      class="mt-4"
      color="primary"
      variant="subtle"
      title="Safe first version"
      description="This module pushes common entity snapshots from local PostgreSQL to Oracle. Inbound bi-directional merge/conflict rules are intentionally reserved for a guided next step."
    />

    <div class="planner-metric-grid mt-4">
      <UCard v-for="metric in metrics" :key="metric.label" class="planner-card">
        <div class="metric-card-content">
          <UIcon :name="metric.icon" class="metric-icon" />
          <div>
            <p>{{ metric.label }}</p>
            <strong>{{ metric.value }}</strong>
            <small>{{ metric.meta }}</small>
          </div>
        </div>
      </UCard>
    </div>

    <div class="page-grid two-column-layout mt-4">
      <UCard class="planner-card">
        <template #header><strong>Manual Control</strong></template>
        <UFormField label="Entity">
          <USelect v-model="selectedEntity" :items="entityOptions" />
        </UFormField>
        <div class="form-actions mt-4">
          <UButton color="neutral" variant="subtle" label="Refresh" icon="i-lucide-refresh-cw" :loading="loading" @click="refresh" />
          <UButton color="neutral" variant="subtle" label="Test Oracle" icon="i-lucide-plug-zap" :loading="testing" @click="testConnection" />
          <UButton color="neutral" variant="subtle" label="Repair Storage" icon="i-lucide-wrench" :loading="repairing" @click="repair" />
          <UButton label="Run Now" icon="i-lucide-play" :loading="running" @click="runNow" />
        </div>
        <UAlert v-if="testResult" class="mt-4" color="success" variant="subtle" title="Oracle connection test passed" :description="testResult.serverTimeUtc || testResult.message" />
      </UCard>

      <UCard class="planner-card">
        <template #header><strong>Configuration Summary</strong></template>
        <dl class="planner-detail-list">
          <div><dt>Enabled</dt><dd>{{ status?.enabled ? 'Yes' : 'No' }}</dd></div>
          <div><dt>Configured</dt><dd>{{ status?.configured ? 'Yes' : 'No' }}</dd></div>
          <div><dt>Tenant</dt><dd>{{ status?.tenantId || '-' }}</dd></div>
          <div><dt>Source app</dt><dd>{{ status?.sourceApplication || '-' }}</dd></div>
          <div><dt>Schema</dt><dd>{{ status?.schema || 'Current Oracle user' }}</dd></div>
          <div><dt>Interval</dt><dd>{{ status?.intervalSeconds || 0 }} seconds</dd></div>
        </dl>
      </UCard>
    </div>

    <UCard v-if="runResult" class="planner-card mt-4">
      <template #header><strong>Last Manual Sync Result</strong></template>
      <UAlert :color="runResult.success ? 'success' : 'error'" variant="subtle" :title="runResult.message" :description="runResult.error || `${runResult.totalPushed} event(s) pushed`" />
      <div class="planner-table-wrap mt-4">
        <table class="planner-table">
          <thead><tr><th>Entity</th><th>Scanned</th><th>Pushed</th><th>Checkpoint</th><th>Error</th></tr></thead>
          <tbody>
            <tr v-for="row in runResult.entities" :key="row.entityName">
              <td>{{ row.entityName }}</td>
              <td>{{ row.scanned }}</td>
              <td>{{ row.pushed }}</td>
              <td>{{ formatDate(row.checkpointUtc) }}</td>
              <td>{{ row.error || '-' }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </UCard>
  </AppShell>
</template>
