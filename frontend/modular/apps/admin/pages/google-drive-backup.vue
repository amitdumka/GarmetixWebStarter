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
          <p class="garmetix-dashboard-subtitle">Each company connects its own Google Drive account - backups go to that company's own storage, not one shared Drive for the whole platform.</p>
        </div>
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh" />
      </div>
    </div>

    <UAlert v-if="error" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />

    <section class="garmetix-section-card">
      <UFormField label="Company" help="Pick the company whose Google Drive backup you want to manage.">
        <USelectMenu v-model="selectedCompanyId" :items="companyOptions" value-key="value" placeholder="Select company..." class="w-full max-w-md" @update:model-value="loadStatus" />
      </UFormField>
    </section>

    <UAlert
      v-if="status && !status.oauthConfigured"
      color="warning"
      variant="subtle"
      icon="i-lucide-plug-zap"
      title="Google OAuth is not configured yet"
      description="Ask your platform administrator to create a Google Cloud OAuth 2.0 Web application client and set GoogleOAuth:ClientId/ClientSecret/RedirectUri/FrontendReturnUrl before any company can connect a Drive."
    />

    <section v-if="selectedCompanyId" class="grid gap-4 xl:grid-cols-2">
      <div class="garmetix-section-card">
        <div class="mb-3 flex items-center justify-between">
          <h3 class="garmetix-panel-title">Connection</h3>
          <UBadge v-if="status" variant="subtle" :color="status.connected ? 'success' : 'neutral'">{{ status.connected ? 'Connected' : 'Not Connected' }}</UBadge>
        </div>
        <div v-if="loadingStatus" class="space-y-2">
          <USkeleton class="h-4 w-2/3" />
          <USkeleton class="h-4 w-1/2" />
        </div>
        <div v-else-if="status?.connected" class="space-y-3 text-sm">
          <p><span class="text-muted">Google Account:</span> <span class="font-medium text-highlighted">{{ status.googleAccountEmail }}</span></p>
          <p><span class="text-muted">Folder:</span> <span class="font-medium text-highlighted">{{ status.folderName }}</span></p>
          <p><span class="text-muted">Connected:</span> {{ formatDateTime(status.connectedAtUtc) }}</p>
          <p v-if="status.lastError" class="text-error"><span class="text-muted">Last Error:</span> {{ status.lastError }}</p>
          <UButton color="error" variant="soft" icon="i-lucide-unlink" :loading="disconnecting" @click="disconnect">Disconnect</UButton>
        </div>
        <div v-else class="space-y-3">
          <p class="text-sm text-muted">This company hasn't connected a Google Drive account yet.</p>
          <UButton color="primary" icon="i-lucide-link" :loading="connecting" :disabled="!status?.oauthConfigured" @click="connect">Connect Google Drive</UButton>
        </div>
      </div>

      <div class="garmetix-section-card">
        <h3 class="garmetix-panel-title mb-3">Upload A Local Backup</h3>
        <UFormField label="Local Backup File">
          <USelectMenu v-model="uploadFileName" :items="localBackupOptions" value-key="value" placeholder="Select a local backup..." class="w-full" />
        </UFormField>
        <UButton class="mt-3" color="primary" variant="soft" icon="i-lucide-cloud-upload" :loading="uploading" :disabled="!status?.connected || !uploadFileName" @click="uploadLocalBackup">Upload To Drive</UButton>
      </div>
    </section>

    <section v-if="selectedCompanyId && status?.connected" class="garmetix-section-card overflow-hidden p-0">
      <div class="p-4"><h3 class="garmetix-panel-title">Cloud Backups</h3></div>
      <UTable :data="fileRows" :columns="columns" :loading="loadingFiles" class="w-full">
        <template #actions-cell="{ row }">
          <div class="flex items-center justify-end gap-1">
            <UButton icon="i-lucide-download" color="neutral" variant="ghost" size="sm" @click="download(row.original.id)" />
            <UButton icon="i-lucide-history" color="warning" variant="ghost" size="sm" @click="openRestore(row.original)" />
            <UButton icon="i-lucide-trash-2" color="error" variant="ghost" size="sm" @click="askDelete(row.original)" />
          </div>
        </template>
      </UTable>
    </section>

    <!-- Modal: Restore confirmation -->
    <UModal v-model:open="restoreModalOpen" title="Restore From Google Drive">
      <template #body>
        <div class="space-y-4">
          <UAlert color="error" variant="subtle" icon="i-lucide-triangle-alert" title="This replaces the entire live database." :description="`File: ${restoreTarget?.name ?? ''}`" />
          <UFormField label='Type "RESTORE" to confirm'>
            <UInput v-model="restoreConfirmation" placeholder="RESTORE" class="max-w-xs" />
          </UFormField>
          <div class="flex justify-end gap-3">
            <UButton color="neutral" variant="ghost" label="Cancel" @click="restoreModalOpen = false" />
            <UButton color="error" :loading="restoring" :disabled="restoreConfirmation !== 'RESTORE'" label="Restore Database" @click="executeRestore" />
          </div>
        </div>
      </template>
    </UModal>

    <!-- Modal: Confirm Delete -->
    <UModal v-model:open="deleteModalOpen" title="Delete Cloud Backup" :description="`Delete '${deleteTarget?.name ?? ''}' from Google Drive? This cannot be undone.`">
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

useHead({ title: 'Google Drive Backup - Garmetix Admin' })

const toast = useToast()
const { get, post, remove, apiBaseUrl } = useAdminApiClient()

const loading = ref(true)
const loadingStatus = ref(false)
const loadingFiles = ref(false)
const error = ref('')

const companies = ref<ApiRecord[]>([])
const localBackups = ref<ApiRecord[]>([])
const selectedCompanyId = ref<string | { value: string } | null>(null)
const status = ref<ApiRecord | null>(null)
const files = ref<ApiRecord[]>([])

const connecting = ref(false)
const disconnecting = ref(false)
const uploading = ref(false)
const uploadFileName = ref<string | { value: string } | null>(null)

const columns = [
  { accessorKey: 'name', header: 'File' },
  { accessorKey: 'created', header: 'Created' },
  { accessorKey: 'size', header: 'Size' },
  { accessorKey: 'actions', header: '', enableSorting: false }
]

const companyOptions = computed(() => companies.value.map(c => ({ value: String(c.id), label: readText(c, ['name'], 'Company') })))
const localBackupOptions = computed(() => localBackups.value.map(b => ({ value: readText(b, ['fileName', 'name']), label: readText(b, ['fileName', 'name']) })))
const fileRows = computed(() => files.value.map(item => ({
  id: readText(item, ['id']),
  name: readText(item, ['name']),
  created: formatDateTime(item.createdAtUtc),
  size: readText(item, ['sizeBytes'], '0')
})))

function unwrapId(value: unknown) {
  return (value as { value?: string })?.value ?? (value as string | null)
}

function apiUrlFor(path: string) {
  const base = String(apiBaseUrl.value || '').replace(/\/+$/, '')
  return `${base}/${path.replace(/^\/+/, '')}`
}

function authHeaders() {
  if (typeof window === 'undefined') return {}
  const token = getStoredToken(window.localStorage)
  return token ? { Authorization: `Bearer ${token}` } : {}
}

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const [companyData, backupData] = await Promise.allSettled([
      get<unknown>('companies'),
      get<unknown>('backups')
    ])
    if (companyData.status === 'fulfilled') companies.value = toRows(companyData.value)
    if (backupData.status === 'fulfilled') localBackups.value = toRows(backupData.value)
    if (companies.value.length === 1) {
      selectedCompanyId.value = String(companies.value[0].id)
      await loadStatus()
    }
    const failed = [companyData, backupData].filter(item => item.status === 'rejected').length
    if (failed) error.value = `${failed} request(s) could not be loaded.`
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load Google Drive backup.'
  } finally {
    loading.value = false
  }
}

async function loadStatus() {
  const companyId = unwrapId(selectedCompanyId.value)
  status.value = null
  files.value = []
  if (!companyId) return
  loadingStatus.value = true
  try {
    status.value = await get<ApiRecord>(`backups/drive/${companyId}/status`)
    if (status.value?.connected) {
      await loadFiles()
    }
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load connection status.'
  } finally {
    loadingStatus.value = false
  }
}

async function loadFiles() {
  const companyId = unwrapId(selectedCompanyId.value)
  if (!companyId) return
  loadingFiles.value = true
  try {
    files.value = toRows(await get<unknown>(`backups/drive/${companyId}/files`))
  } catch (caught) {
    toast.add({ title: 'Error', description: caught instanceof Error ? caught.message : 'Could not list cloud backups.', color: 'error' })
  } finally {
    loadingFiles.value = false
  }
}

async function connect() {
  const companyId = unwrapId(selectedCompanyId.value)
  if (!companyId) return
  connecting.value = true
  try {
    const result = await get<ApiRecord>(`backups/drive/${companyId}/oauth/start`)
    const url = readText(result, ['authorizationUrl'], '')
    if (url) window.location.href = url
  } catch (caught) {
    toast.add({ title: 'Error', description: caught instanceof Error ? caught.message : 'Could not start Google Drive connection.', color: 'error' })
  } finally {
    connecting.value = false
  }
}

async function disconnect() {
  const companyId = unwrapId(selectedCompanyId.value)
  if (!companyId) return
  disconnecting.value = true
  try {
    await post(`backups/drive/${companyId}/disconnect`, undefined)
    toast.add({ title: 'Disconnected', description: 'Google Drive connection removed.', color: 'success' })
    await loadStatus()
  } catch (caught) {
    toast.add({ title: 'Error', description: caught instanceof Error ? caught.message : 'Disconnect failed.', color: 'error' })
  } finally {
    disconnecting.value = false
  }
}

async function uploadLocalBackup() {
  const companyId = unwrapId(selectedCompanyId.value)
  const fileName = unwrapId(uploadFileName.value)
  if (!companyId || !fileName) return
  uploading.value = true
  try {
    await post(`backups/drive/${companyId}/upload/${encodeURIComponent(fileName)}`, undefined)
    toast.add({ title: 'Uploaded', description: `${fileName} uploaded to Google Drive.`, color: 'success' })
    await loadFiles()
  } catch (caught) {
    toast.add({ title: 'Error', description: caught instanceof Error ? caught.message : 'Upload failed.', color: 'error' })
  } finally {
    uploading.value = false
  }
}

function download(fileId: string) {
  const companyId = unwrapId(selectedCompanyId.value)
  if (!companyId) return
  const url = apiUrlFor(`backups/drive/${companyId}/files/${encodeURIComponent(fileId)}/download`)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.target = '_blank'
  anchor.rel = 'noopener'
  document.body.appendChild(anchor)
  anchor.click()
  anchor.remove()
}

const deleteModalOpen = ref(false)
const deleteTarget = ref<ApiRecord | null>(null)
const deleting = ref(false)

function askDelete(file: ApiRecord) {
  deleteTarget.value = file
  deleteModalOpen.value = true
}

async function executeDelete() {
  const companyId = unwrapId(selectedCompanyId.value)
  if (!companyId || !deleteTarget.value) return
  deleting.value = true
  try {
    await remove(`backups/drive/${companyId}/files/${encodeURIComponent(String(deleteTarget.value.id))}`)
    toast.add({ title: 'Deleted', description: `${deleteTarget.value.name} deleted.`, color: 'success' })
    deleteModalOpen.value = false
    await loadFiles()
  } catch (caught) {
    toast.add({ title: 'Error', description: caught instanceof Error ? caught.message : 'Delete failed.', color: 'error' })
  } finally {
    deleting.value = false
  }
}

const restoreModalOpen = ref(false)
const restoreTarget = ref<ApiRecord | null>(null)
const restoreConfirmation = ref('')
const restoring = ref(false)

function openRestore(file: ApiRecord) {
  restoreTarget.value = file
  restoreConfirmation.value = ''
  restoreModalOpen.value = true
}

async function executeRestore() {
  const companyId = unwrapId(selectedCompanyId.value)
  if (!companyId || !restoreTarget.value || restoreConfirmation.value !== 'RESTORE') return
  restoring.value = true
  try {
    const url = apiUrlFor(`backups/drive/${companyId}/files/${encodeURIComponent(String(restoreTarget.value.id))}/restore?confirmation=RESTORE`)
    const response = await fetch(url, { method: 'POST', headers: authHeaders() })
    const body = await response.json()
    if (!response.ok) throw new Error(readText(body, ['message'], 'Restore failed.'))
    toast.add({ title: 'Restore Completed', description: readText(body, ['message'], 'Database restore completed.'), color: 'success' })
    restoreModalOpen.value = false
  } catch (caught) {
    toast.add({ title: 'Error', description: caught instanceof Error ? caught.message : 'Restore failed.', color: 'error' })
  } finally {
    restoring.value = false
  }
}

onMounted(async () => {
  await refresh()
  if (typeof window !== 'undefined') {
    const params = new URLSearchParams(window.location.search)
    if (params.get('driveConnected') === 'true') {
      toast.add({ title: 'Google Drive Connected', description: 'This company can now back up to its own Drive.', color: 'success' })
      window.history.replaceState({}, '', window.location.pathname)
    } else if (params.get('driveError')) {
      toast.add({ title: 'Connection Failed', description: params.get('driveError') ?? 'Google Drive connection failed.', color: 'error' })
      window.history.replaceState({}, '', window.location.pathname)
    }
  }
})
</script>
