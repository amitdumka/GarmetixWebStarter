<template>
  <section class="space-y-4">
    <div class="border border-default bg-muted/10 p-5">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="text-sm text-muted">System diagnostics</p>
          <h2 class="mt-1 text-2xl font-semibold">System Health</h2>
          <p class="mt-2 max-w-3xl text-sm text-muted">Combined read-only view of API health, app version, runtime probes, database migrations and backup status.</p>
        </div>
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
      </div>
    </div>

    <UAlert v-if="error" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />

    <section class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
      <div v-for="card in cards" :key="card.label" class="border border-default bg-muted/20 p-4">
        <p class="text-sm text-muted">{{ card.label }}</p>
        <p class="mt-2 text-2xl font-semibold">{{ card.value }}</p>
        <p class="mt-1 text-xs text-muted">{{ card.detail }}</p>
      </div>
    </section>

    <section class="grid gap-4 xl:grid-cols-2">
      <div class="border border-default bg-muted/10 p-4">
        <h3 class="mb-3 text-base font-semibold">Runtime Probes</h3>
        <AdminMasterTable :columns="probeColumns" :rows="probeRows" empty-text="No runtime probes returned." />
      </div>
      <div class="border border-default bg-muted/10 p-4">
        <h3 class="mb-3 text-base font-semibold">Migration Status</h3>
        <AdminMasterTable :columns="migrationColumns" :rows="migrationRows" empty-text="No migration status returned." />
      </div>
    </section>
  </section>
</template>

<script setup lang="ts">
import { checkApiHealth, type ApiHealthResult } from '@garmetix/shared-api'
import { formatDateTime, readArray, readNumber, readText, type ApiRecord, useAdminApiClient } from '../utils/admin-api'

useHead({ title: 'System Health - Garmetix Admin' })

const { apiBaseUrl, get } = useAdminApiClient()
const loading = ref(true)
const error = ref('')
const apiHealth = ref<ApiHealthResult | null>(null)
const appInfo = ref<ApiRecord | null>(null)
const runtime = ref<ApiRecord | null>(null)
const backup = ref<ApiRecord | null>(null)
const migrations = ref<ApiRecord | null>(null)
const probeColumns = [
  { key: 'area', label: 'Area' },
  { key: 'title', label: 'Check' },
  { key: 'status', label: 'Status' },
  { key: 'detail', label: 'Detail' }
]
const migrationColumns = [
  { key: 'item', label: 'Item' },
  { key: 'value', label: 'Value' }
]
const probes = computed(() => readArray(runtime.value, ['probes']))
const cards = computed(() => [
  { label: 'API', value: apiHealth.value?.label ?? 'Pending', detail: apiHealth.value?.message ?? 'Health check pending' },
  { label: 'Version', value: readText(appInfo.value, ['version'], '-'), detail: readText(appInfo.value, ['stage']) },
  { label: 'Runtime Failures', value: readNumber(runtime.value, ['failedProbeCount']), detail: readText(runtime.value, ['status']) },
  { label: 'Backups', value: readNumber(backup.value, ['backupCount']), detail: `Last ${formatDateTime(backup.value?.lastBackupAtUtc)}` }
])
const probeRows = computed(() => probes.value.slice(0, 12).map(item => ({
  area: readText(item, ['area']),
  title: readText(item, ['title']),
  status: readText(item, ['status']),
  detail: readText(item, ['detail'])
})))
const migrationRows = computed(() => [
  { item: 'Applied migrations', value: readNumber(migrations.value, ['appliedCount']) },
  { item: 'Pending migrations', value: readNumber(migrations.value, ['pendingCount']) },
  { item: 'Auto migrate', value: readText(migrations.value, ['autoMigrateEnabled']) },
  { item: 'Checked at', value: formatDateTime(migrations.value?.checkedAtUtc) },
  { item: 'Last baseline', value: readText(migrations.value, ['lastKnownConsolidatedMigration']) }
])

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const [healthData, infoData, runtimeData, backupData, migrationData] = await Promise.allSettled([
      checkApiHealth(apiBaseUrl.value),
      get<unknown>('app-info/version'),
      get<unknown>('runtime-diagnostics'),
      get<unknown>('backups/status'),
      get<unknown>('database/migrations/status')
    ])
    if (healthData.status === 'fulfilled') apiHealth.value = healthData.value
    if (infoData.status === 'fulfilled' && infoData.value && typeof infoData.value === 'object') appInfo.value = infoData.value as ApiRecord
    if (runtimeData.status === 'fulfilled' && runtimeData.value && typeof runtimeData.value === 'object') runtime.value = runtimeData.value as ApiRecord
    if (backupData.status === 'fulfilled' && backupData.value && typeof backupData.value === 'object') backup.value = backupData.value as ApiRecord
    if (migrationData.status === 'fulfilled' && migrationData.value && typeof migrationData.value === 'object') migrations.value = migrationData.value as ApiRecord
    const failed = [infoData, runtimeData, backupData, migrationData].filter(item => item.status === 'rejected').length
    if (failed) error.value = `${failed} system health request(s) could not be loaded.`
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load system health.'
  } finally {
    loading.value = false
  }
}

onMounted(refresh)
</script>
