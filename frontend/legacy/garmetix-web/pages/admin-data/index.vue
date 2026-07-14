<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const feedback = useUiFeedback()
const config = useRuntimeConfig()

const isAuthenticated = auth.isAuthenticated
const loading = ref(false)
const actionLoading = ref(false)
const error = ref('')
const tables = ref<any[]>([])
const backups = ref<any[]>([])
const selectedTables = ref<string[]>([])
const importFile = ref<File | null>(null)
const importMode = ref('upsert')
const importConfirmation = ref('')
const restoreConfirmation = ref('')
const deleteConfirmation = ref('')
const clearConfirmation = ref('')
const selectedDeleteTable = ref('SalesInvoices')
const selectedIdsText = ref('')
const selectedClearTable = ref('SalesInvoices')
const result = ref<any>(null)
const preview = ref<any>(null)

const tableItems = computed(() => tables.value.map((item) => ({
  label: `${item.label || item.name} (${item.rows})`,
  value: item.name
})))

const backupItems = computed(() => backups.value.map((item) => ({
  label: `${item.operation} | ${item.createdAtUtc} | ${item.tables}`,
  value: item.id
})))
const selectedBackupId = ref('')

async function refresh() {
  if (!auth.isAuthenticated.value) return
  loading.value = true
  error.value = ''
  try {
    const response = await api.get<any>('admin-data/tables')
    tables.value = response.tables || []
    if (!selectedTables.value.length) selectedTables.value = tables.value.slice(0, 3).map((item) => item.name)
    if (!selectedDeleteTable.value && tables.value.length) selectedDeleteTable.value = tables.value[0].name
    if (!selectedClearTable.value && tables.value.length) selectedClearTable.value = tables.value[0].name
    await loadBackups()
  } catch (err) {
    error.value = feedback.errorMessage(err, 'Please check API and permissions.', 'Admin JSON maintenance load failed')
  } finally {
    loading.value = false
  }
}

async function loadBackups() {
  backups.value = await api.get<any[]>('admin-data/backups')
}

function adminApiUrl(path: string) {
  return `${config.public.apiBase}/${path}`
}

function selectedTablesCsv() {
  return selectedTables.value.join(',')
}

async function exportJson() {
  if (!selectedTables.value.length) {
    feedback.failed('Select at least one table')
    return
  }
  actionLoading.value = true
  try {
    const response = await fetch(adminApiUrl(`admin-data/export?tables=${encodeURIComponent(selectedTablesCsv())}`), {
      headers: api.authHeaders()
    })
    if (!response.ok) throw new Error(await response.text())
    const blob = await response.blob()
    downloadBlob(blob, `Garmetix-AdminJson-${new Date().toISOString().slice(0, 19).replace(/[:T]/g, '-')}.json`)
    feedback.notify('JSON export downloaded')
  } catch (err) {
    feedback.failed('JSON export failed', err)
  } finally {
    actionLoading.value = false
  }
}

function onImportFile(event: Event) {
  const input = event.target as HTMLInputElement
  importFile.value = input.files?.[0] || null
  result.value = null
}

async function validateJson() {
  if (!importFile.value) {
    feedback.failed('Select JSON file first')
    return
  }
  actionLoading.value = true
  try {
    const form = new FormData()
    form.append('file', importFile.value)
    result.value = await $fetch(adminApiUrl('admin-data/validate'), {
      method: 'POST',
      headers: api.authHeaders(),
      body: form
    })
    feedback.notify('JSON validation completed')
  } catch (err) {
    feedback.failed('JSON validation failed', err)
  } finally {
    actionLoading.value = false
  }
}

async function importJson() {
  if (!importFile.value) {
    feedback.failed('Select JSON file first')
    return
  }
  if (importConfirmation.value !== 'IMPORT JSON') {
    feedback.failed('Type IMPORT JSON before import')
    return
  }
  actionLoading.value = true
  try {
    const form = new FormData()
    form.append('file', importFile.value)
    result.value = await $fetch(adminApiUrl(`admin-data/import?mode=${encodeURIComponent(importMode.value)}&confirmation=${encodeURIComponent(importConfirmation.value)}`), {
      method: 'POST',
      headers: api.authHeaders(),
      body: form
    })
    await refresh()
    feedback.notify('JSON import completed')
  } catch (err) {
    feedback.failed('JSON import failed', err)
  } finally {
    actionLoading.value = false
  }
}

async function createBackup() {
  actionLoading.value = true
  try {
    result.value = await api.create<any>('admin-data/backup', {
      tables: selectedTables.value,
      operation: 'manual-ui-json-backup',
      remarks: 'Manual JSON backup from Admin Data Maintenance page'
    })
    await loadBackups()
    feedback.notify('Backup created')
  } catch (err) {
    feedback.failed('Backup failed', err)
  } finally {
    actionLoading.value = false
  }
}

async function restoreBackup() {
  if (!selectedBackupId.value) {
    feedback.failed('Select backup first')
    return
  }
  if (restoreConfirmation.value !== 'RESTORE JSON BACKUP') {
    feedback.failed('Type RESTORE JSON BACKUP before restore')
    return
  }
  actionLoading.value = true
  try {
    result.value = await api.create<any>(`admin-data/restore/${selectedBackupId.value}?confirmation=${encodeURIComponent(restoreConfirmation.value)}`, {})
    await refresh()
    feedback.notify('Backup restored')
  } catch (err) {
    feedback.failed('Restore failed', err)
  } finally {
    actionLoading.value = false
  }
}

function selectedIds() {
  return selectedIdsText.value
    .split(/[\n, ]+/)
    .map((item) => item.trim())
    .filter(Boolean)
}

async function previewDelete() {
  preview.value = null
  actionLoading.value = true
  try {
    preview.value = await api.create<any>('admin-data/delete/preview', {
      table: selectedDeleteTable.value,
      ids: selectedIds(),
      confirmation: ''
    })
  } catch (err) {
    feedback.failed('Delete preview failed', err)
  } finally {
    actionLoading.value = false
  }
}

async function executeDelete() {
  if (deleteConfirmation.value !== 'DELETE WITH CASCADE') {
    feedback.failed('Type DELETE WITH CASCADE before delete')
    return
  }
  actionLoading.value = true
  try {
    result.value = await api.create<any>('admin-data/delete/execute', {
      table: selectedDeleteTable.value,
      ids: selectedIds(),
      confirmation: deleteConfirmation.value
    })
    await refresh()
    feedback.notify('Selected rows deleted with cascade')
  } catch (err) {
    feedback.failed('Cascade delete failed', err)
  } finally {
    actionLoading.value = false
  }
}

async function previewClear() {
  preview.value = null
  actionLoading.value = true
  try {
    preview.value = await api.create<any>('admin-data/clear/preview', {
      table: selectedClearTable.value,
      confirmation: ''
    })
  } catch (err) {
    feedback.failed('Clear preview failed', err)
  } finally {
    actionLoading.value = false
  }
}

async function executeClear() {
  if (clearConfirmation.value !== 'DELETE TABLE DATA') {
    feedback.failed('Type DELETE TABLE DATA before clear')
    return
  }
  actionLoading.value = true
  try {
    result.value = await api.create<any>('admin-data/clear/execute', {
      table: selectedClearTable.value,
      confirmation: clearConfirmation.value
    })
    await refresh()
    feedback.notify('Table data cleared')
  } catch (err) {
    feedback.failed('Clear table failed', err)
  } finally {
    actionLoading.value = false
  }
}

function downloadBlob(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  document.body.appendChild(link)
  link.click()
  link.remove()
  URL.revokeObjectURL(url)
}

onMounted(async () => {
  auth.restore()
  await refresh()
})
</script>

<template>
  <AuthScreen v-if="!isAuthenticated" @authenticated="refresh" />

  <AppShell
    v-else
    title="Admin JSON Data Maintenance"
    @refresh="refresh"
  >
    <section class="planner-dashboard">
      <UiModulePageHeader
        title="Admin JSON Data Maintenance"
        description="Admin-only JSON export/import, snapshot backup, restore, table clear and cascading row delete for initial data correction. Use carefully."
        icon="i-lucide-database-backup"
        primary-label="Refresh"
        primary-icon="i-lucide-refresh-cw"
        @primary="refresh"
      >
        <template #actions>
          <UBadge color="error" variant="subtle">Admin destructive tools</UBadge>
        </template>
      </UiModulePageHeader>

      <UAlert
        color="error"
        variant="subtle"
        icon="i-lucide-triangle-alert"
        title="Danger zone"
        description="Every import, restore, clear and cascade delete creates a JSON backup snapshot first. For full disaster recovery, still keep the scheduled PostgreSQL backup enabled."
      />

      <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-alert-triangle" title="Load failed" :description="error" />

      <UCard class="planner-card">
        <template #header>
          <div class="setup-list-header">
            <div>
              <h3>Tables</h3>
              <p>Select one or more tables for JSON export, backup or import restore scope.</p>
            </div>
            <UBadge color="neutral" variant="subtle">{{ tables.length }} tables</UBadge>
          </div>
        </template>

        <div class="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
          <label v-for="table in tables" :key="table.name" class="rounded-xl border border-slate-200 p-3 text-sm dark:border-slate-800">
            <div class="flex items-start gap-3">
              <UCheckbox v-model="selectedTables" :value="table.name" />
              <div>
                <strong>{{ table.label || table.name }}</strong>
                <p class="text-xs text-muted">{{ table.name }} · {{ table.rows }} rows · {{ table.columns }} columns</p>
                <UBadge v-if="table.destructive" color="warning" variant="subtle" size="xs">linked data</UBadge>
              </div>
            </div>
          </label>
        </div>

        <div class="mt-4 flex flex-wrap gap-2">
          <UButton icon="i-lucide-download" label="Export JSON" :loading="actionLoading" @click="exportJson" />
          <UButton icon="i-lucide-shield-plus" color="neutral" variant="subtle" label="Create Backup Snapshot" :loading="actionLoading" @click="createBackup" />
        </div>
      </UCard>

      <UCard class="planner-card">
        <template #header>
          <div class="setup-list-header">
            <div>
              <h3>Validate and Import JSON</h3>
              <p>Validate first. Import automatically creates a backup snapshot before writing data.</p>
            </div>
            <UBadge color="warning" variant="subtle">Confirmation: IMPORT JSON</UBadge>
          </div>
        </template>

        <div class="grid gap-3 lg:grid-cols-3">
          <UFormField label="JSON file">
            <UInput type="file" accept="application/json,.json" @change="onImportFile" />
          </UFormField>
          <UFormField label="Mode">
            <USelect v-model="importMode" :items="[
              { label: 'Upsert by Id', value: 'upsert' },
              { label: 'Insert only', value: 'insert-only' },
              { label: 'Replace selected tables', value: 'replace-table' }
            ]" />
          </UFormField>
          <UFormField label="Confirmation">
            <UInput v-model="importConfirmation" placeholder="IMPORT JSON" />
          </UFormField>
        </div>
        <div class="mt-4 flex flex-wrap gap-2">
          <UButton icon="i-lucide-check-check" color="neutral" variant="subtle" label="Validate JSON" :loading="actionLoading" @click="validateJson" />
          <UButton icon="i-lucide-upload" label="Import JSON" :loading="actionLoading" @click="importJson" />
        </div>
      </UCard>

      <UCard class="planner-card">
        <template #header>
          <div class="setup-list-header">
            <div>
              <h3>Restore JSON Backup</h3>
              <p>Restores a snapshot created by this page. A safety backup is created before restore.</p>
            </div>
            <UBadge color="error" variant="subtle">Confirmation: RESTORE JSON BACKUP</UBadge>
          </div>
        </template>
        <div class="grid gap-3 lg:grid-cols-2">
          <UFormField label="Backup">
            <USelect v-model="selectedBackupId" :items="backupItems" placeholder="Select backup" />
          </UFormField>
          <UFormField label="Confirmation">
            <UInput v-model="restoreConfirmation" placeholder="RESTORE JSON BACKUP" />
          </UFormField>
        </div>
        <div class="mt-4 flex flex-wrap gap-2">
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="subtle" label="Refresh Backups" @click="loadBackups" />
          <UButton icon="i-lucide-history" color="warning" label="Restore Backup" :loading="actionLoading" @click="restoreBackup" />
        </div>
      </UCard>

      <UCard class="planner-card">
        <template #header>
          <div class="setup-list-header">
            <div>
              <h3>Cascade Delete Selected Rows</h3>
              <p>Paste row IDs. The system previews linked rows and creates backup before delete.</p>
            </div>
            <UBadge color="error" variant="subtle">Confirmation: DELETE WITH CASCADE</UBadge>
          </div>
        </template>
        <div class="grid gap-3 lg:grid-cols-3">
          <UFormField label="Table">
            <USelect v-model="selectedDeleteTable" :items="tableItems" />
          </UFormField>
          <UFormField label="Row IDs">
            <UTextarea v-model="selectedIdsText" placeholder="Paste one or more UUIDs, comma/newline separated" autoresize />
          </UFormField>
          <UFormField label="Confirmation">
            <UInput v-model="deleteConfirmation" placeholder="DELETE WITH CASCADE" />
          </UFormField>
        </div>
        <div class="mt-4 flex flex-wrap gap-2">
          <UButton icon="i-lucide-search" color="neutral" variant="subtle" label="Preview Delete" :loading="actionLoading" @click="previewDelete" />
          <UButton icon="i-lucide-trash-2" color="error" label="Delete Selected" :loading="actionLoading" @click="executeDelete" />
        </div>
      </UCard>

      <UCard class="planner-card">
        <template #header>
          <div class="setup-list-header">
            <div>
              <h3>Clear Full Table</h3>
              <p>Deletes all data from one table with database cascade. Use only during initial setup correction.</p>
            </div>
            <UBadge color="error" variant="subtle">Confirmation: DELETE TABLE DATA</UBadge>
          </div>
        </template>
        <div class="grid gap-3 lg:grid-cols-3">
          <UFormField label="Table">
            <USelect v-model="selectedClearTable" :items="tableItems" />
          </UFormField>
          <UFormField label="Confirmation">
            <UInput v-model="clearConfirmation" placeholder="DELETE TABLE DATA" />
          </UFormField>
        </div>
        <div class="mt-4 flex flex-wrap gap-2">
          <UButton icon="i-lucide-search" color="neutral" variant="subtle" label="Preview Table Clear" :loading="actionLoading" @click="previewClear" />
          <UButton icon="i-lucide-ban" color="error" label="Clear Table Data" :loading="actionLoading" @click="executeClear" />
        </div>
      </UCard>

      <UCard v-if="preview || result" class="planner-card">
        <template #header>
          <div class="setup-list-header">
            <div>
              <h3>Last Result</h3>
              <p>Preview or operation response.</p>
            </div>
          </div>
        </template>
        <pre class="overflow-auto rounded-xl bg-slate-950 p-4 text-xs text-slate-100">{{ JSON.stringify(preview || result, null, 2) }}</pre>
      </UCard>
    </section>
  </AppShell>
</template>
