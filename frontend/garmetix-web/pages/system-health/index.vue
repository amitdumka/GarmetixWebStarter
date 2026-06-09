<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const feedback = useUiFeedback()
const config = useRuntimeConfig()
const isAuthenticated = auth.isAuthenticated
const canSeeAdmin = auth.canSeeAdmin

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const health = ref<any | null>(null)
const bootstrap = ref<any | null>(null)
const loading = ref(false)
const publicHost = ref('')
const lastChecked = ref('')
const backups = ref<any[]>([])
const backupStatus = ref<any | null>(null)
const creatingBackup = ref(false)
const downloadingBackup = ref('')
const deletingBackup = ref('')
const restoreOpen = ref(false)
const restoring = ref(false)
const restoreFile = ref<File | null>(null)
const restoreConfirmation = ref('')
const restorePreview = ref<any | null>(null)
const previewingRestore = ref(false)
const verifyingBackup = ref('')
const previewingBackup = ref('')
const verificationResult = ref<any | null>(null)
const localRestorePreview = ref<any | null>(null)
const cloudBackups = ref<any[]>([])
const cloudStatus = ref<any | null>(null)
const uploadingCloudBackup = ref('')
const downloadingCloudBackup = ref('')
const deletingCloudBackup = ref('')
const restoringCloudBackup = ref('')
const cloudRestoreConfirmation = ref('')

const metrics = computed(() => [
  {
    label: 'API Service',
    value: health.value?.status || 'Unknown',
    meta: lastChecked.value ? `Checked ${lastChecked.value}` : 'Waiting for check',
    icon: health.value?.databaseReady ? 'i-lucide-wifi' : 'i-lucide-wifi-off',
    color: health.value?.databaseReady ? 'success' : 'error'
  },
  {
    label: 'Database',
    value: health.value?.databaseReady ? 'Ready' : 'Unavailable',
    meta: health.value?.environment || 'Environment unknown',
    icon: 'i-lucide-database',
    color: health.value?.databaseReady ? 'success' : 'error'
  },
  {
    label: 'API Route',
    value: String(config.public.apiBase || '/api'),
    meta: publicHost.value || 'Public host loading',
    icon: 'i-lucide-route',
    color: String(config.public.apiBase || '').startsWith('/api') ? 'primary' : 'warning'
  },
  {
    label: 'Admin Setup',
    value: bootstrap.value?.hasAdmin ? 'Configured' : 'Required',
    meta: bootstrap.value?.message || 'Bootstrap status',
    icon: 'i-lucide-shield-check',
    color: bootstrap.value?.hasAdmin ? 'success' : 'warning'
  },
  {
    label: 'Backups',
    value: backups.value.length,
    meta: backupStatus.value?.enabled
      ? `Daily at ${scheduleTime.value}`
      : 'Automation disabled',
    icon: 'i-lucide-hard-drive-download',
    color: backupStatus.value?.enabled ? 'success' : 'warning'
  },
  {
    label: 'Google Drive',
    value: cloudStatus.value?.configured ? 'Ready' : 'Not configured',
    meta: cloudStatus.value?.enabled
      ? `${cloudBackups.value.length} online files`
      : 'Cloud backup disabled',
    icon: 'i-lucide-cloud-upload',
    color: cloudStatus.value?.configured ? 'success' : cloudStatus.value?.enabled ? 'warning' : 'neutral'
  }
])

const scheduleTime = computed(() => {
  const hour = Number(backupStatus.value?.runHour ?? 0)
  const minute = Number(backupStatus.value?.runMinute ?? 0)
  return new Date(2000, 0, 1, hour, minute).toLocaleTimeString('en-IN', {
    hour: '2-digit',
    minute: '2-digit'
  })
})

const detailRows = computed(() => [
  ['Public host', publicHost.value || '-'],
  ['Public API base', String(config.public.apiBase || '/api')],
  ['Backend proxy route', '/api/health'],
  ['API application', health.value?.application || '-'],
  ['API environment', health.value?.environment || '-'],
  ['Database ready', health.value?.databaseReady ? 'Yes' : 'No'],
  ['Checked at UTC', health.value?.checkedAtUtc || '-'],
  ['Admin exists', bootstrap.value?.hasAdmin ? 'Yes' : 'No'],
  ['Users exist', bootstrap.value?.hasUsers ? 'Yes' : 'No']
])

const backupRows = computed(() => backups.value.map((backup) => ({
  ...backup,
  size: formatBytes(backup.sizeBytes),
  created: formatDateTime(backup.createdAtUtc),
  sourceLabel: backup.source === 'pre-restore'
    ? 'Safety'
    : backup.source === 'scheduled' ? 'Scheduled' : 'Manual',
  integrityLabel: backup.hasChecksum && backup.hasManifest
    ? 'Checksum + manifest'
    : backup.hasChecksum ? 'Checksum only' : 'Legacy file',
  checksumShort: backup.sha256 ? `${String(backup.sha256).slice(0, 10)}...` : '-'
})))

const cloudBackupRows = computed(() => cloudBackups.value.map((backup) => ({
  ...backup,
  size: formatBytes(backup.sizeBytes),
  created: formatDateTime(backup.createdAtUtc),
  sourceLabel: backup.source === 'pre-restore'
    ? 'Safety'
    : backup.source === 'scheduled' ? 'Scheduled' : 'Manual'
})))

async function refresh() {
  if (!auth.isAuthenticated.value || !auth.canSeeAdmin.value) {
    return
  }

  loading.value = true
  try {
    const [healthResponse, bootstrapResponse, companyRows, storeRows, backupRowsResponse, backupStatusResponse, cloudStatusResponse] = await Promise.all([
      $fetch<any>('/api/health'),
      $fetch<any>('/api/auth/bootstrap-status'),
      api.list<any>('companies'),
      api.list<any>('stores'),
      api.list<any>('backups'),
      api.get<any>('backups/status'),
      api.get<any>('backups/cloud/status')
    ])

    health.value = healthResponse
    bootstrap.value = bootstrapResponse
    companies.value = companyRows
    stores.value = storeRows
    backups.value = backupRowsResponse
    backupStatus.value = backupStatusResponse
    cloudStatus.value = cloudStatusResponse
    cloudBackups.value = cloudStatusResponse?.configured
      ? await api.list<any>('backups/cloud')
      : []
    lastChecked.value = new Date().toLocaleTimeString('en-IN')
  } catch (error) {
    feedback.failed('System health refresh failed', error)
  } finally {
    loading.value = false
  }
}

async function createBackup() {
  creatingBackup.value = true
  try {
    await api.create<any>('backups', {})
    feedback.saved('Database backup')
    await refresh()
  } catch (error) {
    feedback.failed('Could not create database backup', error)
  } finally {
    creatingBackup.value = false
  }
}

async function downloadBackup(backup: any) {
  downloadingBackup.value = backup.fileName
  try {
    const response = await fetch(`${config.public.apiBase}/backups/${encodeURIComponent(backup.fileName)}`, {
      headers: api.authHeaders()
    })
    if (!response.ok) {
      throw new Error(await response.text())
    }

    const blob = await response.blob()
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = backup.fileName
    document.body.appendChild(link)
    link.click()
    link.remove()
    URL.revokeObjectURL(url)
    feedback.notify('Backup downloaded', backup.fileName)
  } catch (error) {
    feedback.failed('Could not download backup', error)
  } finally {
    downloadingBackup.value = ''
  }
}

async function deleteBackup(backup: any) {
  deletingBackup.value = backup.fileName
  try {
    await api.remove('backups', backup.fileName)
    feedback.deleted('Database backup')
    await refresh()
  } catch (error) {
    feedback.failed('Could not delete backup', error)
  } finally {
    deletingBackup.value = ''
  }
}


async function verifyBackup(backup: any) {
  verifyingBackup.value = backup.fileName
  verificationResult.value = null
  try {
    verificationResult.value = await api.get<any>(`backups/${encodeURIComponent(backup.fileName)}/verify`)
    feedback.notify('Backup verification completed', verificationResult.value.message, verificationResult.value.status === 'ok' ? 'success' : 'warning')
  } catch (error) {
    feedback.failed('Could not verify backup', error)
  } finally {
    verifyingBackup.value = ''
  }
}

async function previewLocalRestore(backup: any) {
  previewingBackup.value = backup.fileName
  localRestorePreview.value = null
  try {
    localRestorePreview.value = await api.create<any>(`backups/${encodeURIComponent(backup.fileName)}/restore/preview`, {})
    feedback.notify('Restore preflight completed', localRestorePreview.value.message, localRestorePreview.value.status === 'ok' ? 'success' : 'warning')
  } catch (error) {
    feedback.failed('Could not run restore preflight', error)
  } finally {
    previewingBackup.value = ''
  }
}

async function uploadBackupToDrive(backup: any) {
  uploadingCloudBackup.value = backup.fileName
  try {
    await api.create<any>(`backups/${encodeURIComponent(backup.fileName)}/cloud`, {})
    feedback.notify('Uploaded to Google Drive', backup.fileName)
    await refresh()
  } catch (error) {
    feedback.failed('Could not upload backup to Google Drive', error)
  } finally {
    uploadingCloudBackup.value = ''
  }
}

async function downloadCloudBackup(backup: any) {
  downloadingCloudBackup.value = backup.id
  try {
    const response = await fetch(`${config.public.apiBase}/backups/cloud/${encodeURIComponent(backup.id)}/download`, {
      headers: api.authHeaders()
    })
    if (!response.ok) {
      throw new Error(await response.text())
    }

    const blob = await response.blob()
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = backup.name
    document.body.appendChild(link)
    link.click()
    link.remove()
    URL.revokeObjectURL(url)
    feedback.notify('Google Drive backup downloaded', backup.name)
  } catch (error) {
    feedback.failed('Could not download Google Drive backup', error)
  } finally {
    downloadingCloudBackup.value = ''
  }
}

async function deleteCloudBackup(backup: any) {
  deletingCloudBackup.value = backup.id
  try {
    await api.remove('backups/cloud', backup.id)
    feedback.deleted('Google Drive backup')
    await refresh()
  } catch (error) {
    feedback.failed('Could not delete Google Drive backup', error)
  } finally {
    deletingCloudBackup.value = ''
  }
}

async function restoreCloudBackup(backup: any) {
  if (cloudRestoreConfirmation.value !== 'RESTORE') {
    feedback.failed('Type RESTORE before restoring from Google Drive')
    return
  }

  restoringCloudBackup.value = backup.id
  try {
    const response = await fetch(`${config.public.apiBase}/backups/cloud/${encodeURIComponent(backup.id)}/restore?confirmation=RESTORE`, {
      method: 'POST',
      headers: api.authHeaders()
    })
    if (!response.ok) {
      throw new Error(await response.text())
    }

    const result = await response.json()
    cloudRestoreConfirmation.value = ''
    feedback.notify('Database restored from Google Drive', `Safety backup: ${result.safetyBackup?.fileName || 'created'}`)
    await refresh()
  } catch (error) {
    feedback.failed('Google Drive restore failed', error)
  } finally {
    restoringCloudBackup.value = ''
  }
}

function onRestoreFileChange(event: Event) {
  const input = event.target as HTMLInputElement
  restoreFile.value = input.files?.[0] || null
  restorePreview.value = null
}

function beginRestore() {
  restoreFile.value = null
  restoreConfirmation.value = ''
  restorePreview.value = null
  restoreOpen.value = true
}

async function previewUploadedRestore() {
  if (!restoreFile.value) {
    feedback.failed('Select a backup before preflight')
    return
  }

  previewingRestore.value = true
  restorePreview.value = null
  try {
    const form = new FormData()
    form.append('file', restoreFile.value)
    const response = await fetch(`${config.public.apiBase}/backups/restore/preview`, {
      method: 'POST',
      headers: api.authHeaders(),
      body: form
    })
    if (!response.ok) {
      throw new Error(await response.text())
    }

    restorePreview.value = await response.json()
    feedback.notify('Restore preflight completed', restorePreview.value.message, restorePreview.value.status === 'ok' ? 'success' : 'warning')
  } catch (error) {
    feedback.failed('Restore preflight failed', error)
  } finally {
    previewingRestore.value = false
  }
}

async function restoreDatabase() {
  if (!restoreFile.value || restoreConfirmation.value !== 'RESTORE') {
    feedback.failed('Select a backup and type RESTORE')
    return
  }

  restoring.value = true
  try {
    const form = new FormData()
    form.append('file', restoreFile.value)
    form.append('confirmation', restoreConfirmation.value)
    const response = await fetch(`${config.public.apiBase}/backups/restore`, {
      method: 'POST',
      headers: api.authHeaders(),
      body: form
    })
    if (!response.ok) {
      throw new Error(await response.text())
    }

    const result = await response.json()
    restoreOpen.value = false
    feedback.notify('Database restored', `Safety backup: ${result.safetyBackup?.fileName || 'created'}`)
    await refresh()
  } catch (error) {
    feedback.failed('Database restore failed', error)
  } finally {
    restoring.value = false
  }
}

function formatBytes(value: number) {
  const bytes = Number(value || 0)
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  if (bytes < 1024 * 1024 * 1024) return `${(bytes / 1024 / 1024).toFixed(1)} MB`
  return `${(bytes / 1024 / 1024 / 1024).toFixed(1)} GB`
}

function formatDateTime(value: string) {
  return value ? new Date(value).toLocaleString('en-IN') : '-'
}

onMounted(async () => {
  auth.restore()
  publicHost.value = window.location.origin
  await refresh()
})
</script>

<template>
  <AuthScreen v-if="!isAuthenticated" @authenticated="refresh" />

  <AppShell
    v-else
    title="System Health"
    :companies="companies"
    :stores="stores"
    @refresh="refresh"
  >
    <section class="planner-dashboard">
      <UiModulePageHeader
        title="System Health"
        description="Check deployment, API proxy, database, and first-admin readiness."
        icon="i-lucide-activity"
        primary-label="Refresh"
        primary-icon="i-lucide-refresh-cw"
        @primary="refresh"
      >
        <template #actions>
          <UBadge :color="health?.databaseReady ? 'success' : loading ? 'warning' : 'error'" variant="subtle">
            {{ loading ? 'Checking' : health?.databaseReady ? 'Live' : 'Needs Attention' }}
          </UBadge>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="subtle" :loading="loading" label="Refresh" @click="refresh" />
        </template>
      </UiModulePageHeader>

      <UAlert
        v-if="!canSeeAdmin"
        class="dashboard-alert"
        color="error"
        variant="subtle"
        icon="i-lucide-shield-alert"
        title="Admin access required"
        description="Only Owner and Admin users can view deployment health."
      />

      <template v-else>
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
            <div class="planner-card-header">
              <div>
                <h2>Deployment Details</h2>
                <p>Use this page after Docker restart or Cloudflare tunnel changes.</p>
              </div>
              <UBadge :color="health?.databaseReady ? 'success' : 'error'" variant="subtle">
                {{ health?.databaseReady ? 'Healthy' : 'Not Ready' }}
              </UBadge>
            </div>
          </template>

          <div class="system-health-grid">
            <div v-for="[label, value] in detailRows" :key="label" class="system-health-row">
              <span>{{ label }}</span>
              <strong>{{ value }}</strong>
            </div>
          </div>
        </UCard>

        <UCard class="planner-card">
          <template #header>
            <div class="planner-card-header">
              <div>
                <h2>Backup and Restore</h2>
                <p>Full PostgreSQL database recovery points, including application settings and records.</p>
              </div>
              <div class="table-action-buttons">
                <UBadge :color="backupStatus?.enabled ? 'success' : 'warning'" variant="subtle">
                  {{ backupStatus?.enabled ? `Daily ${scheduleTime}` : 'Manual only' }}
                </UBadge>
                <UButton
                  icon="i-lucide-rotate-ccw"
                  color="error"
                  variant="subtle"
                  label="Restore"
                  @click="beginRestore"
                />
                <UButton
                  icon="i-lucide-database-backup"
                  label="Create Backup"
                  :loading="creatingBackup"
                  @click="createBackup"
                />
              </div>
            </div>
          </template>

          <UAlert
            color="info"
            variant="subtle"
            icon="i-lucide-shield-check"
            title="Protected recovery"
            :description="`Scheduled backups keep the latest ${backupStatus?.retentionCount || 14} files. Restore automatically creates a safety backup first.`"
          />

          <UTable
            v-if="backupRows.length"
            class="mt-4"
            :data="backupRows"
            :columns="[
              { accessorKey: 'fileName', header: 'Backup' },
              { accessorKey: 'sourceLabel', header: 'Source' },
              { accessorKey: 'created', header: 'Created' },
              { accessorKey: 'size', header: 'Size' },
              { accessorKey: 'integrityLabel', header: 'Integrity' },
              { id: 'actions', header: '' }
            ]"
          >
            <template #sourceLabel-cell="{ row }">
              <UBadge
                :color="row.original.source === 'pre-restore' ? 'warning' : row.original.source === 'scheduled' ? 'success' : 'primary'"
                variant="subtle"
              >
                {{ row.original.sourceLabel }}
              </UBadge>
            </template>
            <template #integrityLabel-cell="{ row }">
              <div class="backup-integrity-cell">
                <UBadge :color="row.original.hasChecksum ? 'success' : 'warning'" variant="subtle">
                  {{ row.original.integrityLabel }}
                </UBadge>
                <small>{{ row.original.checksumShort }}</small>
              </div>
            </template>
            <template #actions-cell="{ row }">
              <div class="table-action-buttons">
                <UButton
                  color="success"
                  variant="ghost"
                  icon="i-lucide-shield-check"
                  label="Verify"
                  :loading="verifyingBackup === row.original.fileName"
                  @click="verifyBackup(row.original)"
                />
                <UButton
                  color="warning"
                  variant="ghost"
                  icon="i-lucide-list-checks"
                  label="Preflight"
                  :loading="previewingBackup === row.original.fileName"
                  @click="previewLocalRestore(row.original)"
                />
                <UButton
                  color="neutral"
                  variant="ghost"
                  icon="i-lucide-download"
                  label="Download"
                  :loading="downloadingBackup === row.original.fileName"
                  @click="downloadBackup(row.original)"
                />
                <UButton
                  v-if="cloudStatus?.configured"
                  color="primary"
                  variant="ghost"
                  icon="i-lucide-cloud-upload"
                  label="Drive"
                  :loading="uploadingCloudBackup === row.original.fileName"
                  @click="uploadBackupToDrive(row.original)"
                />
                <UButton
                  color="error"
                  variant="ghost"
                  icon="i-lucide-trash-2"
                  label="Delete"
                  :loading="deletingBackup === row.original.fileName"
                  @click="deleteBackup(row.original)"
                />
              </div>
            </template>
          </UTable>

          <UiCrudEmptyState
            v-else
            title="No database backups"
            description="Create the first full-system recovery point."
            icon="i-lucide-hard-drive-download"
            action-label="Create Backup"
            @action="createBackup"
          />

          <div v-if="verificationResult" class="mt-4">
            <UAlert
              :color="verificationResult.status === 'ok' ? 'success' : 'error'"
              variant="subtle"
              icon="i-lucide-shield-check"
              :title="`Verification: ${verificationResult.fileName}`"
              :description="`${verificationResult.message} SHA256: ${verificationResult.sha256 || 'missing'}`"
            />
          </div>

          <div v-if="localRestorePreview" class="mt-4 restore-preview-box">
            <UAlert
              :color="localRestorePreview.status === 'ok' ? 'success' : 'error'"
              variant="subtle"
              icon="i-lucide-list-checks"
              :title="`Restore preflight: ${localRestorePreview.fileName}`"
              :description="localRestorePreview.message"
            />
            <pre>{{ localRestorePreview.previewLines?.join('\\n') }}</pre>
          </div>
        </UCard>

        <UCard class="planner-card">
          <template #header>
            <div class="planner-card-header">
              <div>
                <h2>Google Drive Online Backup</h2>
                <p>Upload local PostgreSQL backup files to your configured Google Drive folder and restore them when needed.</p>
              </div>
              <UBadge :color="cloudStatus?.configured ? 'success' : cloudStatus?.enabled ? 'warning' : 'neutral'" variant="subtle">
                {{ cloudStatus?.configured ? 'Connected' : cloudStatus?.enabled ? 'Needs Setup' : 'Disabled' }}
              </UBadge>
            </div>
          </template>

          <UAlert
            :color="cloudStatus?.configured ? 'success' : 'warning'"
            variant="subtle"
            icon="i-lucide-cloud"
            :title="cloudStatus?.configured ? 'Google Drive backup is configured' : 'Google Drive backup is not configured'"
            :description="cloudStatus?.configured
              ? `Folder ${cloudStatus.folderId}. Upload on backup: ${cloudStatus.uploadOnBackup ? 'enabled' : 'manual only'}. Retention: ${cloudStatus.retentionCount}.`
              : 'Add a service account JSON file, share the target Drive folder with that service account email, and set GoogleDriveBackup__Enabled=true.'"
          />

          <div v-if="cloudStatus?.lastError" class="mt-4">
            <UAlert
              color="error"
              variant="subtle"
              icon="i-lucide-triangle-alert"
              title="Last Google Drive backup error"
              :description="cloudStatus.lastError"
            />
          </div>

          <div v-if="cloudStatus?.configured" class="mt-4 form-grid">
            <UFormField label="Type RESTORE to enable cloud restore buttons">
              <UInput v-model="cloudRestoreConfirmation" placeholder="RESTORE" autocomplete="off" />
            </UFormField>
          </div>

          <UTable
            v-if="cloudBackupRows.length"
            class="mt-4"
            :data="cloudBackupRows"
            :columns="[
              { accessorKey: 'name', header: 'Drive Backup' },
              { accessorKey: 'sourceLabel', header: 'Source' },
              { accessorKey: 'created', header: 'Created' },
              { accessorKey: 'size', header: 'Size' },
              { id: 'actions', header: '' }
            ]"
          >
            <template #sourceLabel-cell="{ row }">
              <UBadge
                :color="row.original.source === 'pre-restore' ? 'warning' : row.original.source === 'scheduled' ? 'success' : 'primary'"
                variant="subtle"
              >
                {{ row.original.sourceLabel }}
              </UBadge>
            </template>
            <template #actions-cell="{ row }">
              <div class="table-action-buttons">
                <UButton
                  color="neutral"
                  variant="ghost"
                  icon="i-lucide-download"
                  label="Download"
                  :loading="downloadingCloudBackup === row.original.id"
                  @click="downloadCloudBackup(row.original)"
                />
                <UButton
                  color="warning"
                  variant="ghost"
                  icon="i-lucide-rotate-ccw"
                  label="Restore"
                  :disabled="cloudRestoreConfirmation !== 'RESTORE'"
                  :loading="restoringCloudBackup === row.original.id"
                  @click="restoreCloudBackup(row.original)"
                />
                <UButton
                  color="error"
                  variant="ghost"
                  icon="i-lucide-trash-2"
                  label="Delete"
                  :loading="deletingCloudBackup === row.original.id"
                  @click="deleteCloudBackup(row.original)"
                />
              </div>
            </template>
          </UTable>

          <UiCrudEmptyState
            v-else
            title="No Google Drive backups"
            description="Create a local backup first. If Google Drive is configured with upload-on-backup, it will upload automatically; otherwise use the Drive button from the local backup list."
            icon="i-lucide-cloud-upload"
          />
        </UCard>
      </template>
    </section>
  </AppShell>

  <UModal v-model:open="restoreOpen" title="Restore Database" :ui="{ content: 'max-w-2xl' }">
    <template #body>
      <div class="form-grid">
        <UAlert
          color="error"
          variant="subtle"
          icon="i-lucide-triangle-alert"
          title="This replaces the current database"
          description="Stop users from entering data before restore. A safety backup is created automatically, but current records will be replaced by the selected recovery point."
        />
        <UFormField label="PostgreSQL backup file" required>
          <UInput type="file" accept=".dump,application/octet-stream" @change="onRestoreFileChange" />
        </UFormField>
        <UFormField label="Type RESTORE to confirm" required>
          <UInput v-model="restoreConfirmation" autocomplete="off" placeholder="RESTORE" />
        </UFormField>
        <div v-if="restorePreview" class="restore-preview-box">
          <UAlert
            :color="restorePreview.status === 'ok' ? 'success' : 'error'"
            variant="subtle"
            icon="i-lucide-list-checks"
            :title="`Preflight: ${restorePreview.fileName}`"
            :description="restorePreview.message"
          />
          <pre>{{ restorePreview.previewLines?.join('\\n') }}</pre>
        </div>
      </div>
    </template>
    <template #footer>
      <div class="modal-actions">
        <UButton color="neutral" variant="outline" label="Cancel" :disabled="restoring" @click="restoreOpen = false" />
        <UButton
          color="warning"
          variant="subtle"
          icon="i-lucide-list-checks"
          label="Preflight"
          :loading="previewingRestore"
          :disabled="!restoreFile"
          @click="previewUploadedRestore"
        />
        <UButton
          color="error"
          icon="i-lucide-rotate-ccw"
          label="Restore Database"
          :loading="restoring"
          :disabled="!restoreFile || restoreConfirmation !== 'RESTORE' || restorePreview?.status !== 'ok'"
          @click="restoreDatabase"
        />
      </div>
    </template>
  </UModal>
</template>

<style scoped>
.backup-integrity-cell {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.backup-integrity-cell small {
  color: var(--ui-text-muted);
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, 'Liberation Mono', 'Courier New', monospace;
}

.restore-preview-box {
  display: grid;
  gap: 0.75rem;
}

.restore-preview-box pre {
  max-height: 14rem;
  overflow: auto;
  border-radius: 0.75rem;
  padding: 0.75rem;
  background: var(--ui-bg-muted);
  color: var(--ui-text);
  font-size: 0.75rem;
  white-space: pre-wrap;
}
</style>
