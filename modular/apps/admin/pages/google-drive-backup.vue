<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-dashboard-kicker">
            <UIcon name="i-lucide-cloud-upload" class="size-4" />
            Off-site backup
          </p>
          <h2 class="garmetix-dashboard-title">Google Drive Backup</h2>
          <p class="garmetix-dashboard-subtitle">Read-only Google Drive backup status and cloud backup list. Upload, delete, download and restore actions remain disabled in modular foundation.</p>
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

    <div class="garmetix-section-card">
      <AdminMasterTable :columns="columns" :rows="rows" empty-text="No cloud backups returned." />
    </div>
  </section>
</template>

<script setup lang="ts">
import { formatDateTime, readText, toRows, type ApiRecord, useAdminApiClient } from '../utils/admin-api'

useHead({ title: 'Google Drive Backup - Garmetix Admin' })

const { get } = useAdminApiClient()
const loading = ref(true)
const error = ref('')
const status = ref<ApiRecord | null>(null)
const cloudRows = ref<ApiRecord[]>([])
const columns = [
  { key: 'name', label: 'File' },
  { key: 'created', label: 'Created' },
  { key: 'size', label: 'Size' },
  { key: 'id', label: 'Drive Id' }
]
const cards = computed(() => [
  { label: 'Enabled', value: readText(status.value, ['enabled'], '-'), detail: 'Google Drive setting' },
  { label: 'Configured', value: readText(status.value, ['configured'], '-'), detail: readText(status.value, ['message']) },
  { label: 'Files', value: cloudRows.value.length, detail: 'Cloud backup rows' },
  { label: 'Checked', value: formatDateTime(status.value?.checkedAtUtc), detail: readText(status.value, ['folderId']) }
])
const rows = computed(() => cloudRows.value.map(item => ({
  name: readText(item, ['name', 'fileName']),
  created: formatDateTime(item.createdAtUtc ?? item.createdTime),
  size: readText(item, ['sizeBytes', 'size']),
  id: readText(item, ['id', 'fileId'])
})))

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const [statusData, listData] = await Promise.allSettled([
      get<unknown>('backups/cloud/status'),
      get<unknown>('backups/cloud')
    ])
    if (statusData.status === 'fulfilled' && statusData.value && typeof statusData.value === 'object') status.value = statusData.value as ApiRecord
    if (listData.status === 'fulfilled') cloudRows.value = toRows(listData.value)
    const failed = [statusData, listData].filter(item => item.status === 'rejected').length
    if (failed) error.value = `${failed} Google Drive backup request(s) could not be loaded.`
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load Google Drive backup.'
  } finally {
    loading.value = false
  }
}

onMounted(refresh)
</script>
