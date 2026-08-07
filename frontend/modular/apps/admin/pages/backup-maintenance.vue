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
          <p class="garmetix-dashboard-subtitle">Create, download, verify, delete and restore PostgreSQL backups.</p>
        </div>
        <div class="flex items-center gap-2">
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
          <UButton icon="i-lucide-database-zap" color="primary" :loading="creating" @click="createBackup">Create Backup</UButton>
        </div>
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

    <section class="garmetix-section-card">
      <div class="mb-3 flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
        <h3 class="garmetix-panel-title">Local Backups</h3>
        <div class="flex flex-wrap gap-2">
          <UButton size="sm" color="neutral" variant="soft" icon="i-lucide-badge-check" :loading="verifyingAll" @click="verifyAll">Verify All</UButton>
          <UButton size="sm" color="neutral" variant="soft" icon="i-lucide-trash" :loading="cleaning" @click="cleanup">Run Cleanup</UButton>
        </div>
      </div>
      <UTable :data="backupRows" :columns="backupColumns" :loading="loading" class="w-full">
        <template #checksum-cell="{ row }">
          <UBadge variant="subtle" :color="row.original.checksumRaw ? 'success' : 'neutral'">{{ row.original.checksumRaw ? 'Yes' : 'No' }}</UBadge>
        </template>
        <template #actions-cell="{ row }">
          <div class="flex items-center justify-end gap-1">
            <UButton icon="i-lucide-badge-check" color="neutral" variant="ghost" size="sm" :loading="verifyingFile === row.original.file" @click="verifyOne(row.original.file)" />
            <UButton icon="i-lucide-download" color="neutral" variant="ghost" size="sm" @click="download(row.original.file)" />
            <UButton icon="i-lucide-eye" color="primary" variant="ghost" size="sm" :loading="previewingFile === row.original.file" @click="previewLocalRestore(row.original.file)" />
            <UButton icon="i-lucide-trash-2" color="error" variant="ghost" size="sm" @click="askDelete(row.original.file)" />
          </div>
        </template>
      </UTable>
    </section>

    <section class="garmetix-section-card">
      <h3 class="garmetix-panel-title mb-3">Restore From Upload</h3>
      <UAlert color="error" variant="subtle" icon="i-lucide-triangle-alert" title="This replaces the entire live database." description="A safety backup of the current database is taken automatically before any restore, but this is still a destructive, hard-to-reverse action. Only proceed if you're certain." class="mb-4" />
      <div class="space-y-4">
        <UFormField label="Backup File (.dump)">
          <input type="file" accept=".dump" class="block w-full text-sm" @change="onRestoreFileChange" />
        </UFormField>
        <div class="flex flex-wrap gap-2">
          <UButton color="neutral" variant="soft" icon="i-lucide-eye" :loading="previewingUpload" :disabled="!restoreFile" @click="previewUploadRestore">Preview</UButton>
        </div>
        <div v-if="restorePreview" class="garmetix-row-card space-y-1 text-sm">
          <p v-for="(item, key) in restorePreview" :key="key"><span class="text-muted">{{ key }}:</span> {{ item }}</p>
        </div>
        <UFormField label='Type "RESTORE" to confirm'>
          <UInput v-model="restoreConfirmation" placeholder="RESTORE" class="max-w-xs" />
        </UFormField>
        <UButton color="error" icon="i-lucide-triangle-alert" :loading="restoring" :disabled="!restoreFile || restoreConfirmation !== 'RESTORE'" @click="executeRestore">Restore Database</UButton>
      </div>
    </section>

    <div class="garmetix-section-card">
      <h3 class="garmetix-panel-title mb-3">Maintenance Status</h3>
      <AdminMasterTable :columns="maintenanceColumns" :rows="maintenanceRows" empty-text="No maintenance status returned." />
    </div>

    <!-- Modal: Verify/Preview Result -->
    <UModal v-model:open="resultModalOpen" :title="resultModalTitle">
      <template #body>
        <pre class="max-h-96 overflow-auto whitespace-pre-wrap rounded-md bg-muted/30 p-3 text-xs text-muted">{{ resultModalContent }}</pre>
        <div class="mt-4 flex justify-end">
          <UButton color="neutral" variant="ghost" label="Close" @click="resultModalOpen = false" />
        </div>
      </template>
    </UModal>

    <!-- Modal: Confirm Delete -->
    <UModal v-model:open="deleteModalOpen" title="Delete Backup" :description="`Delete '${deleteTarget}'? This cannot be undone.`">
      <template #body>
        <div class="flex justify-end gap-3">
          <UButton color="neutral" variant="ghost" label="Cancel" @click="deleteModalOpen = false" />
          <UButton color="error" :loading="deleting" label="Delete" @click="executeDelete" />
        </div>
      </template>
    </UModal>
  </section>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { getStoredToken } from '@garmetix/shared-auth'
import { formatDateTime, readText, toRows, type ApiRecord, useAdminApiClient } from '../utils/admin-api'

useHead({ title: 'Backup Maintenance - Garmetix Admin' })

const toast = useToast()
const { get, post, remove, apiUrlFor, authHeaders } = useAdminBackupClient()

const loading = ref(true)
const error = ref('')
const status = ref<ApiRecord | null>(null)
const maintenance = ref<ApiRecord | null>(null)
const backups = ref<ApiRecord[]>([])

const creating = ref(false)
const verifyingAll = ref(false)
const cleaning = ref(false)
const verifyingFile = ref('')
const previewingFile = ref('')

const backupColumns = [
  { accessorKey: 'file', header: 'File' },
  { accessorKey: 'created', header: 'Created' },
  { accessorKey: 'size', header: 'Size' },
  { accessorKey: 'checksum', header: 'Checksum' },
  { accessorKey: 'actions', header: '', enableSorting: false }
]
const maintenanceColumns = [
  { key: 'item', label: 'Item' },
  { key: 'value', label: 'Value' }
]

const cards = computed(() => [
  { label: 'Enabled', value: readText(status.value, ['enabled'], '-'), detail: 'Scheduled backup setting' },
  { label: 'Backups', value: readText(status.value, ['backupCount'], '0'), detail: `Last ${formatDateTime(status.value?.lastBackupAtUtc)}` },
  { label: 'Restore In Progress', value: readText(status.value, ['restoreInProgress'], '-'), detail: 'Restore-in-progress flag' },
  { label: 'Directory Writable', value: readText(maintenance.value, ['directoryWritable'], '-'), detail: readText(maintenance.value, ['directory']) }
])
const backupRows = computed(() => backups.value.map(item => ({
  file: readText(item, ['fileName', 'name']),
  created: formatDateTime(item.createdAtUtc),
  size: readText(item, ['sizeBytes', 'displaySize', 'size']),
  checksumRaw: Boolean(item.hasChecksum)
})))
const maintenanceRows = computed(() => Object.entries(maintenance.value ?? {}).slice(0, 24).map(([item, value]) => ({
  item,
  value: typeof value === 'object' ? JSON.stringify(value) : String(value ?? '-')
})))

const resultModalOpen = ref(false)
const resultModalTitle = ref('')
const resultModalContent = ref('')

function showResult(title: string, content: unknown) {
  resultModalTitle.value = title
  resultModalContent.value = typeof content === 'string' ? content : JSON.stringify(content, null, 2)
  resultModalOpen.value = true
}

const deleteModalOpen = ref(false)
const deleteTarget = ref('')
const deleting = ref(false)

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

async function createBackup() {
  creating.value = true
  try {
    const result = await post<ApiRecord>('backups', undefined)
    toast.add({ title: 'Backup Created', description: readText(result, ['fileName', 'name'], 'Backup created.'), color: 'success' })
    await refresh()
  } catch (caught) {
    toast.add({ title: 'Error', description: caught instanceof Error ? caught.message : 'Failed to create backup.', color: 'error' })
  } finally {
    creating.value = false
  }
}

async function verifyAll() {
  verifyingAll.value = true
  try {
    const result = await post<ApiRecord>('backups/maintenance/verify-all', undefined)
    showResult('Verify All Result', result)
    await refresh()
  } catch (caught) {
    toast.add({ title: 'Error', description: caught instanceof Error ? caught.message : 'Verify all failed.', color: 'error' })
  } finally {
    verifyingAll.value = false
  }
}

async function cleanup() {
  cleaning.value = true
  try {
    const result = await post<ApiRecord>('backups/maintenance/cleanup', undefined)
    showResult('Cleanup Result', result)
    await refresh()
  } catch (caught) {
    toast.add({ title: 'Error', description: caught instanceof Error ? caught.message : 'Cleanup failed.', color: 'error' })
  } finally {
    cleaning.value = false
  }
}

async function verifyOne(fileName: string) {
  verifyingFile.value = fileName
  try {
    const result = await get<ApiRecord>(`backups/${encodeURIComponent(fileName)}/verify`)
    showResult(`Verify - ${fileName}`, result)
  } catch (caught) {
    toast.add({ title: 'Error', description: caught instanceof Error ? caught.message : 'Verify failed.', color: 'error' })
  } finally {
    verifyingFile.value = ''
  }
}

async function previewLocalRestore(fileName: string) {
  previewingFile.value = fileName
  try {
    const result = await post<ApiRecord>(`backups/${encodeURIComponent(fileName)}/restore/preview`, undefined)
    showResult(`Restore Preview - ${fileName}`, result)
  } catch (caught) {
    toast.add({ title: 'Error', description: caught instanceof Error ? caught.message : 'Preview failed.', color: 'error' })
  } finally {
    previewingFile.value = ''
  }
}

function download(fileName: string) {
  const url = apiUrlFor(`backups/${encodeURIComponent(fileName)}`)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.target = '_blank'
  anchor.rel = 'noopener'
  document.body.appendChild(anchor)
  anchor.click()
  anchor.remove()
}

function askDelete(fileName: string) {
  deleteTarget.value = fileName
  deleteModalOpen.value = true
}

async function executeDelete() {
  deleting.value = true
  try {
    await remove(`backups/${encodeURIComponent(deleteTarget.value)}`)
    toast.add({ title: 'Deleted', description: `${deleteTarget.value} deleted.`, color: 'success' })
    deleteModalOpen.value = false
    await refresh()
  } catch (caught) {
    toast.add({ title: 'Error', description: caught instanceof Error ? caught.message : 'Delete failed.', color: 'error' })
  } finally {
    deleting.value = false
  }
}

const restoreFile = ref<File | null>(null)
const restorePreview = ref<Record<string, unknown> | null>(null)
const restoreConfirmation = ref('')
const previewingUpload = ref(false)
const restoring = ref(false)

function onRestoreFileChange(event: Event) {
  const input = event.target as HTMLInputElement
  restoreFile.value = input.files?.[0] ?? null
  restorePreview.value = null
}

async function previewUploadRestore() {
  if (!restoreFile.value) return
  previewingUpload.value = true
  try {
    const form = new FormData()
    form.append('file', restoreFile.value)
    const response = await fetch(apiUrlFor('backups/restore/preview'), { method: 'POST', headers: authHeaders(), body: form })
    const body = await response.json()
    if (!response.ok) throw new Error(readText(body, ['message'], 'Preview failed.'))
    restorePreview.value = body
    toast.add({ title: 'Preview Ready', description: 'Review the preview below before restoring.', color: 'info' })
  } catch (caught) {
    toast.add({ title: 'Error', description: caught instanceof Error ? caught.message : 'Preview failed.', color: 'error' })
  } finally {
    previewingUpload.value = false
  }
}

async function executeRestore() {
  if (!restoreFile.value || restoreConfirmation.value !== 'RESTORE') return
  restoring.value = true
  try {
    const form = new FormData()
    form.append('file', restoreFile.value)
    form.append('confirmation', 'RESTORE')
    const response = await fetch(apiUrlFor('backups/restore'), { method: 'POST', headers: authHeaders(), body: form })
    const body = await response.json()
    if (!response.ok) throw new Error(readText(body, ['message'], 'Restore failed.'))
    toast.add({ title: 'Restore Completed', description: readText(body, ['message'], 'Database restore completed.'), color: 'success' })
    restoreFile.value = null
    restorePreview.value = null
    restoreConfirmation.value = ''
    await refresh()
  } catch (caught) {
    toast.add({ title: 'Error', description: caught instanceof Error ? caught.message : 'Restore failed.', color: 'error' })
  } finally {
    restoring.value = false
  }
}

function useAdminBackupClient() {
  const { get, post, remove, apiBaseUrl } = useAdminApiClient()
  function apiUrlFor(path: string) {
    const base = String(apiBaseUrl.value || '').replace(/\/+$/, '')
    return `${base}/${path.replace(/^\/+/, '')}`
  }
  function authHeaders() {
    if (typeof window === 'undefined') return {}
    const token = getStoredToken(window.localStorage)
    return token ? { Authorization: `Bearer ${token}` } : {}
  }
  return { get, post, remove, apiUrlFor, authHeaders }
}

onMounted(refresh)
</script>
