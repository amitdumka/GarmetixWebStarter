<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-dashboard-kicker">
            <UIcon name="i-lucide-database-backup" class="size-4" />
            Backup diagnostics
          </p>
          <h2 class="garmetix-dashboard-title">Backup Maintenance</h2>
          <p class="garmetix-dashboard-subtitle">Read-only backup status, local backup list and maintenance health. Create, cleanup, delete and restore actions are not exposed here.</p>
        </div>
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
      </div>
    </div>

    <UAlert v-if="error" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />

    <section class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
      <div v-for="card in cards" :key="card.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label">{{ card.label }}</p>
        <p class="garmetix-metric-value">{{ card.value }}</p>
        <p class="garmetix-metric-caption">{{ card.detail }}</p>
      </div>
    </section>

    <section class="grid gap-4 xl:grid-cols-2">
      <div class="garmetix-section-card">
        <h3 class="garmetix-panel-title mb-3">Local Backups</h3>
        <AdminMasterTable :columns="backupColumns" :rows="backupRows" empty-text="No local backups returned." />
      </div>
      <div class="garmetix-section-card">
        <h3 class="garmetix-panel-title mb-3">Maintenance Status</h3>
        <AdminMasterTable :columns="maintenanceColumns" :rows="maintenanceRows" empty-text="No maintenance status returned." />
      </div>
    </section>
  </section>
</template>

<script setup lang="ts">
import { formatDateTime, readText, toRows, type ApiRecord, useAdminApiClient } from '../utils/admin-api'

useHead({ title: 'Backup Maintenance - Garmetix Admin' })

const { get } = useAdminApiClient()
const loading = ref(true)
const error = ref('')
const status = ref<ApiRecord | null>(null)
const maintenance = ref<ApiRecord | null>(null)
const backups = ref<ApiRecord[]>([])
const backupColumns = [
  { key: 'file', label: 'File' },
  { key: 'created', label: 'Created' },
  { key: 'size', label: 'Size' },
  { key: 'checksum', label: 'Checksum' },
  { key: 'manifest', label: 'Manifest' }
]
const maintenanceColumns = [
  { key: 'item', label: 'Item' },
  { key: 'value', label: 'Value' }
]
const cards = computed(() => [
  { label: 'Enabled', value: readText(status.value, ['enabled'], '-'), detail: 'Scheduled backup setting' },
  { label: 'Backups', value: readText(status.value, ['backupCount'], '0'), detail: `Last ${formatDateTime(status.value?.lastBackupAtUtc)}` },
  { label: 'Restore', value: readText(status.value, ['restoreInProgress'], '-'), detail: 'Restore-in-progress flag' },
  { label: 'Directory', value: readText(maintenance.value, ['directoryWritable'], '-'), detail: readText(maintenance.value, ['directory']) }
])
const backupRows = computed(() => backups.value.map(item => ({
  file: readText(item, ['fileName', 'name']),
  created: formatDateTime(item.createdAtUtc),
  size: readText(item, ['sizeBytes', 'displaySize', 'size']),
  checksum: readText(item, ['hasChecksum']),
  manifest: readText(item, ['hasManifest'])
})))
const maintenanceRows = computed(() => Object.entries(maintenance.value ?? {}).slice(0, 24).map(([item, value]) => ({
  item,
  value: typeof value === 'object' ? JSON.stringify(value) : String(value ?? '-')
})))

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const [statusData, maintenanceData, backupData] = await Promise.allSettled([
      get<unknown>('backups/status'),
      get<unknown>('backups/maintenance/status'),
      get<unknown>('backups')
    ])
    if (statusData.status === 'fulfilled' && statusData.value && typeof statusData.value === 'object') status.value = statusData.value as ApiRecord
    if (maintenanceData.status === 'fulfilled' && maintenanceData.value && typeof maintenanceData.value === 'object') maintenance.value = maintenanceData.value as ApiRecord
    if (backupData.status === 'fulfilled') backups.value = toRows(backupData.value)
    const failed = [statusData, maintenanceData, backupData].filter(item => item.status === 'rejected').length
    if (failed) error.value = `${failed} backup request(s) could not be loaded.`
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load backup maintenance.'
  } finally {
    loading.value = false
  }
}

onMounted(refresh)
</script>
