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
    : backup.source === 'scheduled' ? 'Scheduled' : 'Manual'
})))

async function refresh() {
  if (!auth.isAuthenticated.value || !auth.canSeeAdmin.value) {
    return
  }

  loading.value = true
  try {
    const [healthResponse, bootstrapResponse, companyRows, storeRows, backupRowsResponse, backupStatusResponse] = await Promise.all([
      $fetch<any>('/api/health'),
      $fetch<any>('/api/auth/bootstrap-status'),
      api.list<any>('companies'),
      api.list<any>('stores'),
      api.list<any>('backups'),
      api.get<any>('backups/status')
    ])

    health.value = healthResponse
    bootstrap.value = bootstrapResponse
    companies.value = companyRows
    stores.value = storeRows
    backups.value = backupRowsResponse
    backupStatus.value = backupStatusResponse
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

function onRestoreFileChange(event: Event) {
  const input = event.target as HTMLInputElement
  restoreFile.value = input.files?.[0] || null
}

function beginRestore() {
  restoreFile.value = null
  restoreConfirmation.value = ''
  restoreOpen.value = true
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
                  :loading="downloadingBackup === row.original.fileName"
                  @click="downloadBackup(row.original)"
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
      </div>
    </template>
    <template #footer>
      <div class="modal-actions">
        <UButton color="neutral" variant="outline" label="Cancel" :disabled="restoring" @click="restoreOpen = false" />
        <UButton
          color="error"
          icon="i-lucide-rotate-ccw"
          label="Restore Database"
          :loading="restoring"
          :disabled="!restoreFile || restoreConfirmation !== 'RESTORE'"
          @click="restoreDatabase"
        />
      </div>
    </template>
  </UModal>
</template>
