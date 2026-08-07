<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-dashboard-kicker">
            <UIcon name="i-lucide-arrow-up-down" class="size-4" />
            Data movement
          </p>
          <h2 class="garmetix-dashboard-title">Import Export</h2>
          <p class="garmetix-dashboard-subtitle">Export/template downloads per module, plus a CSV import wizard (preview, then commit) for modules with import write support.</p>
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

    <section class="garmetix-section-card overflow-hidden p-0">
      <UTable :data="rows" :columns="columns" :loading="loading" class="w-full">
        <template #importSupported-cell="{ row }">
          <UBadge variant="subtle" :color="row.original.importSupportedRaw ? 'success' : 'neutral'">{{ row.original.importSupportedRaw ? 'Supported' : 'Export only' }}</UBadge>
        </template>
        <template #actions-cell="{ row }">
          <div class="flex items-center justify-end gap-1">
            <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-download" @click="download(row.original.key, 'export')">Export</UButton>
            <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-file-text" @click="download(row.original.key, 'template')">Template</UButton>
            <UButton v-if="row.original.importSupportedRaw" size="xs" color="primary" variant="soft" icon="i-lucide-upload" @click="openImport(row.original)">Import</UButton>
          </div>
        </template>
      </UTable>
    </section>

    <!-- Modal: Import wizard -->
    <UModal v-model:open="importModalOpen" :title="`Import - ${importTarget?.name ?? ''}`">
      <template #body>
        <div class="space-y-4">
          <UFormField label="CSV File">
            <input type="file" accept=".csv" class="block w-full text-sm" @change="onImportFileChange" />
          </UFormField>
          <div class="flex flex-wrap gap-2">
            <UButton color="neutral" variant="soft" icon="i-lucide-eye" :loading="previewing" :disabled="!importFile" @click="runImport(false)">Preview</UButton>
            <UButton
              v-if="importResult && importResult.invalidRows === 0"
              color="primary"
              icon="i-lucide-upload"
              :loading="committing"
              @click="runImport(true)"
            >
              Commit Import
            </UButton>
          </div>

          <div v-if="importResult" class="space-y-3">
            <div class="grid grid-cols-3 gap-3 text-sm sm:grid-cols-5">
              <div class="garmetix-row-card"><p class="text-xs text-muted">Rows Read</p><p class="font-semibold text-highlighted">{{ importResult.rowsRead }}</p></div>
              <div class="garmetix-row-card"><p class="text-xs text-muted">Valid</p><p class="font-semibold text-highlighted">{{ importResult.validRows }}</p></div>
              <div class="garmetix-row-card"><p class="text-xs text-muted">Invalid</p><p class="font-semibold" :class="importResult.invalidRows ? 'text-error' : 'text-highlighted'">{{ importResult.invalidRows }}</p></div>
              <div class="garmetix-row-card"><p class="text-xs text-muted">Created</p><p class="font-semibold text-highlighted">{{ importResult.created }}</p></div>
              <div class="garmetix-row-card"><p class="text-xs text-muted">Updated</p><p class="font-semibold text-highlighted">{{ importResult.updated }}</p></div>
            </div>
            <UBadge v-if="importResult.commit" color="success" variant="subtle">Committed</UBadge>
            <UBadge v-else color="neutral" variant="subtle">Preview only - not saved</UBadge>

            <div v-if="importResult.errors?.length" class="max-h-56 overflow-auto rounded-md border border-error/40">
              <table class="w-full text-left text-xs">
                <thead class="bg-error/10"><tr><th class="p-2">Line</th><th class="p-2">Field</th><th class="p-2">Message</th></tr></thead>
                <tbody>
                  <tr v-for="(err, idx) in importResult.errors" :key="idx" class="border-t border-default">
                    <td class="p-2">{{ err.line }}</td>
                    <td class="p-2">{{ err.field }}</td>
                    <td class="p-2">{{ err.message }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
            <ul v-if="importResult.warnings?.length" class="list-inside list-disc text-xs text-warning">
              <li v-for="(warning, idx) in importResult.warnings" :key="idx">{{ warning }}</li>
            </ul>
          </div>

          <div class="flex justify-end">
            <UButton color="neutral" variant="ghost" label="Close" @click="importModalOpen = false" />
          </div>
        </div>
      </template>
    </UModal>
  </section>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { getStoredToken } from '@garmetix/shared-auth'
import { readNumber, readText, type ApiRecord, useAdminApiClient } from '../utils/admin-api'

useHead({ title: 'Import Export - Garmetix Admin' })

const toast = useToast()
const { get, apiBaseUrl } = useAdminApiClient()

const loading = ref(true)
const error = ref('')
const modules = ref<ApiRecord[]>([])
const center = ref<ApiRecord | null>(null)
const health = ref<ApiRecord | null>(null)

const columns = [
  { accessorKey: 'name', header: 'Module' },
  { accessorKey: 'columns', header: 'Columns' },
  { accessorKey: 'importSupported', header: 'Import' },
  { accessorKey: 'actions', header: '', enableSorting: false }
]
const cards = computed(() => [
  { label: 'Modules', value: modules.value.length, detail: 'Registered import/export modules' },
  { label: 'Health', value: readText(health.value, ['status'], 'Pending'), detail: readText(health.value, ['message']) },
  { label: 'Importable', value: modules.value.filter(item => item.importSupported).length, detail: 'Modules with import write support' },
  { label: 'Warnings', value: readNumber(health.value, ['warningCount']), detail: 'Import/export warnings' }
])
const rows = computed(() => modules.value.map(item => ({
  key: readText(item, ['key']),
  name: readText(item, ['name', 'module']),
  columns: readText(item, ['columns'], '-'),
  importSupportedRaw: Boolean(item.importSupported)
})))

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const [moduleData, centerData, healthData] = await Promise.allSettled([
      get<unknown>('import-export/modules'),
      get<unknown>('import-export/center'),
      get<unknown>('import-export/health')
    ])
    if (moduleData.status === 'fulfilled') modules.value = Array.isArray(moduleData.value) ? moduleData.value as ApiRecord[] : []
    if (centerData.status === 'fulfilled' && centerData.value && typeof centerData.value === 'object') center.value = centerData.value as ApiRecord
    if (healthData.status === 'fulfilled' && healthData.value && typeof healthData.value === 'object') health.value = healthData.value as ApiRecord
    const failed = [moduleData, centerData, healthData].filter(item => item.status === 'rejected').length
    if (failed) error.value = `${failed} import/export request(s) could not be loaded.`
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load import/export.'
  } finally {
    loading.value = false
  }
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

function download(moduleKey: string, kind: 'export' | 'template') {
  const url = apiUrlFor(`import-export/${kind}/${encodeURIComponent(moduleKey)}`)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.target = '_blank'
  anchor.rel = 'noopener'
  document.body.appendChild(anchor)
  anchor.click()
  anchor.remove()
}

const importModalOpen = ref(false)
const importTarget = ref<ApiRecord | null>(null)
const importFile = ref<File | null>(null)
const previewing = ref(false)
const committing = ref(false)
const importResult = ref<{
  rowsRead: number
  validRows: number
  invalidRows: number
  created: number
  updated: number
  commit: boolean
  errors: Array<{ line: number, field: string, message: string }>
  warnings: string[]
} | null>(null)

function openImport(row: ApiRecord) {
  importTarget.value = row
  importFile.value = null
  importResult.value = null
  importModalOpen.value = true
}

function onImportFileChange(event: Event) {
  const input = event.target as HTMLInputElement
  importFile.value = input.files?.[0] ?? null
  importResult.value = null
}

async function runImport(commit: boolean) {
  if (!importFile.value || !importTarget.value) return
  if (commit) committing.value = true
  else previewing.value = true
  try {
    const form = new FormData()
    form.append('file', importFile.value)
    const url = apiUrlFor(`import-export/import/${encodeURIComponent(String(importTarget.value.key))}?commit=${commit}`)
    const response = await fetch(url, { method: 'POST', headers: authHeaders(), body: form })
    const body = await response.json()
    if (!response.ok) throw new Error(readText(body, ['message'], 'Import failed.'))
    importResult.value = {
      rowsRead: readNumber(body, ['rowsRead']),
      validRows: readNumber(body, ['validRows']),
      invalidRows: readNumber(body, ['invalidRows']),
      created: readNumber(body, ['created']),
      updated: readNumber(body, ['updated']),
      commit: Boolean(body.commit),
      errors: Array.isArray(body.errors) ? body.errors : [],
      warnings: Array.isArray(body.warnings) ? body.warnings : []
    }
    if (commit) {
      toast.add({ title: 'Import Committed', description: `${importResult.value.created} created, ${importResult.value.updated} updated.`, color: 'success' })
    }
  } catch (caught) {
    toast.add({ title: 'Error', description: caught instanceof Error ? caught.message : 'Import failed.', color: 'error' })
  } finally {
    previewing.value = false
    committing.value = false
  }
}

onMounted(refresh)
</script>
