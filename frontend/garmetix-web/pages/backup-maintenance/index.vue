<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const feedback = useUiFeedback()

const loading = ref(false)
const creatingBackup = ref(false)
const cleaning = ref(false)
const verifying = ref(false)
const status = ref<any | null>(null)
const backups = ref<any[]>([])
const verifyResult = ref<any | null>(null)
const cleanupResult = ref<any | null>(null)
const cloudLoading = ref(false)
const cloudUploading = ref<string | null>(null)
const cloudStatus = ref<any | null>(null)
const cloudBackups = ref<any[]>([])
const restoreDrillSteps = ref([
  { key: 'create', label: 'Create fresh backup', detail: 'Run Create backup before deployment or before risky repair.' },
  { key: 'verify', label: 'Verify all local backups', detail: 'Checks checksum/header/manifest for local backup files.' },
  { key: 'cloud', label: 'Upload latest backup off-site', detail: 'Upload to Google Drive or another external location.' },
  { key: 'preview', label: 'Run restore preview / dry run', detail: 'Use local restore preview on latest backup before real restore.' },
  { key: 'document', label: 'Record operator confirmation', detail: 'Confirm backup file name and drill result in your operation notes.' }
])
const restoreDrillState = reactive<Record<string, boolean>>({})
const restoreDrillNote = ref('')
const RESTORE_DRILL_KEY = 'garmetix:backup-restore-drill:v1'

const restoreDrillScore = computed(() => restoreDrillSteps.value.filter((step) => restoreDrillState[step.key]).length)
const restoreDrillReady = computed(() => restoreDrillScore.value === restoreDrillSteps.value.length)


const statusColor = computed(() => {
  const value = String(status.value?.status || '').toLowerCase()
  if (value.includes('ready')) return 'success'
  if (value.includes('critical')) return 'error'
  return 'warning'
})

const cards = computed(() => [
  {
    label: 'Backup status',
    value: status.value?.status || 'Unknown',
    meta: status.value?.enabled ? 'Automation enabled' : 'Automation disabled',
    icon: 'i-lucide-shield-check',
    color: statusColor.value
  },
  {
    label: 'Latest backup',
    value: status.value?.latestBackupFileName || 'None',
    meta: status.value?.latestBackupAgeHours == null ? 'No backup found' : `${status.value.latestBackupAgeHours} hours old`,
    icon: 'i-lucide-clock-3',
    color: status.value?.hasRecentBackup ? 'success' : 'warning'
  },
  {
    label: 'Local backups',
    value: status.value?.backupCount ?? 0,
    meta: `${status.value?.checksummedBackupCount ?? 0} checksummed, ${status.value?.manifestBackupCount ?? 0} manifests`,
    icon: 'i-lucide-database-backup',
    color: 'primary'
  },
  {
    label: 'Disk free',
    value: formatBytes(status.value?.freeSpaceBytes),
    meta: `Folder size ${formatBytes(status.value?.backupFolderSizeBytes)}`,
    icon: 'i-lucide-hard-drive',
    color: Number(status.value?.freeSpaceBytes || 0) < 1073741824 ? 'error' : 'success'
  },
  {
    label: 'Cleanup needed',
    value: Number(status.value?.orphanSidecarCount || 0) + Number(status.value?.temporaryRestoreFileCount || 0),
    meta: `${status.value?.orphanSidecarCount ?? 0} sidecars, ${status.value?.temporaryRestoreFileCount ?? 0} temp files`,
    icon: 'i-lucide-trash-2',
    color: Number(status.value?.orphanSidecarCount || 0) + Number(status.value?.temporaryRestoreFileCount || 0) > 0 ? 'warning' : 'success'
  }
])

const cloudSummaryColor = computed(() => {
  if (!cloudStatus.value?.enabled) return 'warning'
  if (!cloudStatus.value?.configured) return 'error'
  if (cloudStatus.value?.lastError) return 'warning'
  return 'success'
})

const cloudRows = computed(() => cloudBackups.value.map((backup) => ({
  ...backup,
  size: formatBytes(backup.sizeBytes),
  created: formatDateTime(backup.createdAtUtc)
})))

const backupRows = computed(() => backups.value.map((backup) => ({
  ...backup,
  size: formatBytes(backup.sizeBytes),
  created: formatDateTime(backup.createdAtUtc),
  integrity: backup.hasChecksum && backup.hasManifest
    ? 'Checksum + manifest'
    : backup.hasChecksum ? 'Checksum only' : 'Legacy / unchecked'
})))

async function refresh() {
  if (!auth.isAuthenticated.value || !auth.canSeeAdmin.value) return
  loading.value = true
  try {
    const [statusResponse, backupResponse] = await Promise.all([
      api.get<any>('backups/maintenance/status'),
      api.list<any>('backups')
    ])
    status.value = statusResponse
    backups.value = backupResponse
    await refreshCloud()
  } catch (error) {
    feedback.failed('Could not refresh backup maintenance', error)
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

async function cleanup() {
  cleaning.value = true
  try {
    cleanupResult.value = await api.create<any>('backups/maintenance/cleanup', {})
    feedback.saved('Backup cleanup')
    await refresh()
  } catch (error) {
    feedback.failed('Could not clean backup folder', error)
  } finally {
    cleaning.value = false
  }
}

async function verifyAll() {
  verifying.value = true
  try {
    verifyResult.value = await api.create<any>('backups/maintenance/verify-all', {})
    feedback.saved('Backup verification')
  } catch (error) {
    feedback.failed('Could not verify backups', error)
  } finally {
    verifying.value = false
  }
}

async function refreshCloud() {
  cloudLoading.value = true
  try {
    cloudStatus.value = await api.get<any>('backups/cloud/status')
    if (cloudStatus.value?.configured) {
      cloudBackups.value = await api.list<any>('backups/cloud')
    } else {
      cloudBackups.value = []
    }
  } catch (error) {
    cloudBackups.value = []
    feedback.failed('Could not refresh Google Drive backup status', error)
  } finally {
    cloudLoading.value = false
  }
}

async function uploadToCloud(fileName: string) {
  cloudUploading.value = fileName
  try {
    await api.create<any>(`backups/${encodeURIComponent(fileName)}/cloud`, {})
    feedback.saved('Google Drive upload')
    await refreshCloud()
  } catch (error) {
    feedback.failed('Could not upload backup to Google Drive', error)
  } finally {
    cloudUploading.value = null
  }
}

function loadRestoreDrill() {
  if (typeof window === 'undefined') return
  try {
    const saved = JSON.parse(window.localStorage.getItem(RESTORE_DRILL_KEY) || '{}')
    Object.assign(restoreDrillState, saved.state || {})
    restoreDrillNote.value = saved.note || ''
  } catch {
    // Ignore local checklist cache errors.
  }
}

function saveRestoreDrill() {
  if (typeof window === 'undefined') return
  window.localStorage.setItem(RESTORE_DRILL_KEY, JSON.stringify({
    state: restoreDrillState,
    note: restoreDrillNote.value,
    savedAt: new Date().toISOString()
  }))
}

watch(restoreDrillState, saveRestoreDrill, { deep: true })
watch(restoreDrillNote, saveRestoreDrill)

function formatBytes(value?: number | null) {
  const bytes = Number(value || 0)
  if (!bytes) return '0 B'
  const units = ['B', 'KB', 'MB', 'GB', 'TB']
  let size = bytes
  let unit = 0
  while (size >= 1024 && unit < units.length - 1) {
    size /= 1024
    unit += 1
  }
  return `${size.toFixed(size >= 10 || unit === 0 ? 0 : 1)} ${units[unit]}`
}

function formatDateTime(value?: string) {
  return value ? new Date(value).toLocaleString('en-IN') : '-'
}

onMounted(async () => {
  loadRestoreDrill()
  await refresh()
})
</script>

<template>
  <main class="page-shell space-y-6">
    <section class="page-hero rounded-3xl border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-800 dark:bg-slate-950">
      <div class="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="text-sm font-semibold uppercase tracking-wide text-primary">Stage 8G Package 4</p>
          <h1 class="mt-2 text-3xl font-bold text-slate-950 dark:text-white">Backup Maintenance</h1>
          <p class="mt-2 max-w-3xl text-sm text-slate-600 dark:text-slate-300">
            Monitor local PostgreSQL backups, verify checksums, clean stale restore files and confirm the Mac mini has enough disk space before production use.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton icon="i-lucide-refresh-cw" :loading="loading" @click="refresh">Refresh</UButton>
          <UButton icon="i-lucide-database-backup" color="primary" :loading="creatingBackup" @click="createBackup">Create backup</UButton>
          <UButton icon="i-lucide-shield-check" color="success" variant="soft" :loading="verifying" @click="verifyAll">Verify all</UButton>
          <UButton icon="i-lucide-trash-2" color="warning" variant="soft" :loading="cleaning" @click="cleanup">Cleanup</UButton>
        </div>
      </div>
    </section>

    <section class="grid gap-4 md:grid-cols-2 xl:grid-cols-5">
      <UCard v-for="card in cards" :key="card.label">
        <div class="flex items-start gap-3">
          <div class="rounded-2xl bg-slate-100 p-3 dark:bg-slate-900">
            <UIcon :name="card.icon" class="h-5 w-5" />
          </div>
          <div class="min-w-0">
            <p class="text-xs font-medium uppercase tracking-wide text-slate-500">{{ card.label }}</p>
            <p class="mt-1 truncate text-lg font-semibold text-slate-950 dark:text-white">{{ card.value }}</p>
            <p class="mt-1 text-xs text-slate-500">{{ card.meta }}</p>
          </div>
        </div>
      </UCard>
    </section>

    <UCard>
      <template #header>
        <div class="flex items-center justify-between gap-3">
          <div>
            <h2 class="text-lg font-semibold">Recommendations</h2>
            <p class="text-sm text-slate-500">Use these before production cutover and after every power-failure recovery.</p>
          </div>
          <UBadge :color="statusColor">{{ status?.status || 'Unknown' }}</UBadge>
        </div>
      </template>
      <ul class="space-y-2 text-sm text-slate-700 dark:text-slate-200">
        <li v-for="item in status?.recommendations || []" :key="item" class="flex gap-2">
          <UIcon name="i-lucide-check-circle-2" class="mt-0.5 h-4 w-4 text-primary" />
          <span>{{ item }}</span>
        </li>
      </ul>
    </UCard>

<UCard>
  <template #header>
    <div class="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
      <div>
        <h2 class="text-lg font-semibold">Production backup/restore drill</h2>
        <p class="text-sm text-slate-500">Mark these after a real Mac mini backup verification. This keeps a visible production acceptance record.</p>
      </div>
      <UBadge :color="restoreDrillReady ? 'success' : 'warning'" :label="`${restoreDrillScore}/${restoreDrillSteps.length} complete`" />
    </div>
  </template>
  <div class="grid gap-3 lg:grid-cols-2">
    <label
      v-for="step in restoreDrillSteps"
      :key="step.key"
      class="flex gap-3 rounded-2xl border border-slate-200 p-4 dark:border-slate-800"
    >
      <UCheckbox v-model="restoreDrillState[step.key]" />
      <span>
        <strong class="block text-sm text-slate-900 dark:text-white">{{ step.label }}</strong>
        <small class="text-slate-500">{{ step.detail }}</small>
      </span>
    </label>
  </div>
  <UFormField class="mt-4" label="Restore drill note">
    <UTextarea v-model="restoreDrillNote" :rows="3" placeholder="Example: Verified backup file name, cloud upload, restore preview result, operator name." />
  </UFormField>
  <template #footer>
    <div class="flex flex-wrap gap-2">
      <UButton icon="i-lucide-database-backup" color="primary" variant="soft" :loading="creatingBackup" @click="createBackup">Create backup</UButton>
      <UButton icon="i-lucide-shield-check" color="success" variant="soft" :loading="verifying" @click="verifyAll">Verify all</UButton>
      <UButton icon="i-lucide-cloud-upload" color="primary" variant="ghost" :disabled="!backupRows.length" @click="backupRows[0] && uploadToCloud(backupRows[0].fileName)">Upload latest</UButton>
    </div>
  </template>
</UCard>

    <UCard>
      <template #header>
        <div class="flex items-center justify-between gap-3">
          <h2 class="text-lg font-semibold">Local backup files</h2>
          <span class="text-sm text-slate-500">Retention: {{ status?.retentionCount ?? '-' }}</span>
        </div>
      </template>
      <div class="overflow-x-auto">
        <table class="min-w-full text-sm">
          <thead class="text-left text-xs uppercase text-slate-500">
            <tr>
              <th class="px-3 py-2">File</th>
              <th class="px-3 py-2">Created</th>
              <th class="px-3 py-2">Size</th>
              <th class="px-3 py-2">Integrity</th>
              <th class="px-3 py-2">Source</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="backup in backupRows" :key="backup.fileName" class="border-t border-slate-100 dark:border-slate-800">
              <td class="px-3 py-2 font-mono text-xs">{{ backup.fileName }}</td>
              <td class="px-3 py-2">{{ backup.created }}</td>
              <td class="px-3 py-2">{{ backup.size }}</td>
              <td class="px-3 py-2">{{ backup.integrity }}</td>
              <td class="px-3 py-2 capitalize">{{ backup.source }}</td>
            </tr>
            <tr v-if="!backupRows.length">
              <td colspan="5" class="px-3 py-6 text-center text-slate-500">No backup files found yet.</td>
            </tr>
          </tbody>
        </table>
      </div>
    </UCard>

    <UCard>
      <template #header>
        <div class="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
          <div>
            <h2 class="text-lg font-semibold">Google Drive off-site backup</h2>
            <p class="text-sm text-slate-500">Validate service-account backup storage before go-live and upload a verified local backup off-site.</p>
          </div>
          <div class="flex flex-wrap items-center gap-2">
            <UBadge :color="cloudSummaryColor">{{ cloudStatus?.configured ? 'Configured' : cloudStatus?.enabled ? 'Incomplete' : 'Disabled' }}</UBadge>
            <UButton icon="i-lucide-refresh-cw" variant="soft" :loading="cloudLoading" @click="refreshCloud">Refresh cloud</UButton>
          </div>
        </div>
      </template>

      <div class="grid gap-4 lg:grid-cols-3">
        <div class="rounded-2xl border border-slate-200 p-4 dark:border-slate-800">
          <p class="text-xs font-medium uppercase tracking-wide text-slate-500">Service account</p>
          <p class="mt-1 break-all text-sm font-semibold text-slate-900 dark:text-white">{{ cloudStatus?.serviceAccountEmail || 'Not configured' }}</p>
        </div>
        <div class="rounded-2xl border border-slate-200 p-4 dark:border-slate-800">
          <p class="text-xs font-medium uppercase tracking-wide text-slate-500">Drive folder</p>
          <p class="mt-1 break-all font-mono text-xs text-slate-900 dark:text-white">{{ cloudStatus?.folderId || 'Missing folder id' }}</p>
        </div>
        <div class="rounded-2xl border border-slate-200 p-4 dark:border-slate-800">
          <p class="text-xs font-medium uppercase tracking-wide text-slate-500">Cloud backups</p>
          <p class="mt-1 text-xl font-semibold text-slate-900 dark:text-white">{{ cloudStatus?.cloudBackupCount ?? cloudBackups.length }}</p>
          <p class="mt-1 text-xs text-slate-500">Retention {{ cloudStatus?.retentionCount ?? '-' }}, upload on backup {{ cloudStatus?.uploadOnBackup ? 'enabled' : 'manual' }}</p>
        </div>
      </div>

      <UAlert
        v-if="cloudStatus?.lastError"
        class="mt-4"
        color="warning"
        icon="i-lucide-triangle-alert"
        title="Last Google Drive error"
        :description="cloudStatus.lastError"
      />

      <div class="mt-4 overflow-x-auto">
        <table class="min-w-full text-sm">
          <thead class="text-left text-xs uppercase text-slate-500">
            <tr>
              <th class="px-3 py-2">Cloud file</th>
              <th class="px-3 py-2">Created</th>
              <th class="px-3 py-2">Size</th>
              <th class="px-3 py-2">Source</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="backup in cloudRows" :key="backup.id" class="border-t border-slate-100 dark:border-slate-800">
              <td class="px-3 py-2 font-mono text-xs">{{ backup.name }}</td>
              <td class="px-3 py-2">{{ backup.created }}</td>
              <td class="px-3 py-2">{{ backup.size }}</td>
              <td class="px-3 py-2 capitalize">{{ backup.source }}</td>
            </tr>
            <tr v-if="!cloudRows.length">
              <td colspan="4" class="px-3 py-6 text-center text-slate-500">No cloud backups listed yet, or Google Drive is not configured.</td>
            </tr>
          </tbody>
        </table>
      </div>

      <template #footer>
        <div class="flex flex-col gap-2 text-sm text-slate-500 lg:flex-row lg:items-center lg:justify-between">
          <span>Upload a local backup after checksum verification, then run the off-site restore drill on a disposable database.</span>
          <UDropdownMenu
            v-if="backupRows.length"
            :items="backupRows.map((backup) => ({ label: `Upload ${backup.fileName}`, icon: 'i-lucide-cloud-upload', onSelect: () => uploadToCloud(backup.fileName) }))"
          >
            <UButton icon="i-lucide-cloud-upload" color="primary" variant="soft" :loading="!!cloudUploading">Upload local backup</UButton>
          </UDropdownMenu>
        </div>
      </template>
    </UCard>

    <div class="grid gap-4 lg:grid-cols-2">
      <UCard v-if="verifyResult">
        <template #header>
          <h2 class="text-lg font-semibold">Last verification</h2>
        </template>
        <p class="text-sm text-slate-600 dark:text-slate-300">
          {{ verifyResult.passedBackups }} passed, {{ verifyResult.failedBackups }} failed from {{ verifyResult.totalBackups }} backups.
        </p>
      </UCard>
      <UCard v-if="cleanupResult">
        <template #header>
          <h2 class="text-lg font-semibold">Last cleanup</h2>
        </template>
        <p class="text-sm text-slate-600 dark:text-slate-300">
          Removed {{ cleanupResult.deletedFileCount }} files and recovered {{ formatBytes(cleanupResult.recoveredBytes) }}.
        </p>
      </UCard>
    </div>
  </main>
</template>
