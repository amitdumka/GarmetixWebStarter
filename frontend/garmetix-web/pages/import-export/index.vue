<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const feedback = useUiFeedback()
const config = useRuntimeConfig()
const isAuthenticated = auth.isAuthenticated

const companies = ref<any[]>([])
const stores = ref<any[]>([])
const modules = ref<any[]>([])
const loading = ref(false)
const downloading = ref('')
const importing = ref(false)
const selectedModule = ref('inventory')
const selectedFile = ref<File | null>(null)
const filePreview = ref<any[]>([])
const importResult = ref<any | null>(null)

type UiColor = 'primary' | 'success' | 'warning' | 'neutral' | 'error'

const moduleMeta: Record<string, { icon: string, color: UiColor, description: string }> = {
  setup: {
    icon: 'i-lucide-building-2',
    color: 'primary',
    description: 'Companies, store groups, and stores.'
  },
  inventory: {
    icon: 'i-lucide-boxes',
    color: 'success',
    description: 'Products, barcode, stock, MRP, and purchase/sold quantities.'
  },
  billing: {
    icon: 'i-lucide-receipt-indian-rupee',
    color: 'warning',
    description: 'Sales invoice register with paid and balance amounts.'
  },
  purchase: {
    icon: 'i-lucide-package-plus',
    color: 'primary',
    description: 'Purchase inward register and vendor bill amounts.'
  },
  vouchers: {
    icon: 'i-lucide-banknote',
    color: 'neutral',
    description: 'Payment, receipt, expense, and accounting vouchers.'
  },
  'petty-cash': {
    icon: 'i-lucide-circle-dollar-sign',
    color: 'warning',
    description: 'Daily cash sheets, expenses, receipts, and cash in hand.'
  },
  hr: {
    icon: 'i-lucide-users-round',
    color: 'success',
    description: 'Employee master data for HR migration.'
  },
  payroll: {
    icon: 'i-lucide-badge-indian-rupee',
    color: 'primary',
    description: 'Generated payslips with earnings and deductions.'
  },
  access: {
    icon: 'i-lucide-shield-check',
    color: 'error',
    description: 'Users, roles, access type, and operation scope.'
  }
}

const totalColumns = computed(() => modules.value.reduce((sum, item) => sum + Number(item.columns || 0), 0))
const selectedModuleName = computed(() => modules.value.find((item) => item.key === selectedModule.value)?.name || 'Module')
const selectedModuleInfo = computed(() => modules.value.find((item) => item.key === selectedModule.value))
const importRows = computed(() => {
  const result = importResult.value
  if (!result) {
    return []
  }

  if (!result.errors?.length) {
    return [{
      line: '-',
      field: 'Status',
      message: result.commit
        ? `${result.created} created and ${result.updated} updated.`
        : `${result.validRows} rows are ready to import.`
    }]
  }

  return result.errors.map((error: any) => ({
    line: error.line,
    field: error.field,
    message: error.message
  }))
})

async function refresh() {
  if (!auth.isAuthenticated.value) {
    return
  }

  loading.value = true
  try {
    const [companyRows, storeRows, moduleRows] = await Promise.all([
      api.list<any>('companies'),
      api.list<any>('stores'),
      api.get<any[]>('import-export/modules')
    ])

    companies.value = companyRows
    stores.value = storeRows
    modules.value = moduleRows
    selectedModule.value = moduleRows[0]?.key || 'inventory'
  } catch (error) {
    feedback.failed('Import/export refresh failed', error)
  } finally {
    loading.value = false
  }
}

async function downloadCsv(moduleKey: string, kind: 'export' | 'template') {
  downloading.value = `${kind}:${moduleKey}`
  try {
    const response = await fetch(`${config.public.apiBase}/import-export/${kind}/${moduleKey}`, {
      headers: api.authHeaders()
    })

    if (!response.ok) {
      throw new Error(await response.text())
    }

    const blob = await response.blob()
    const disposition = response.headers.get('content-disposition') || ''
    const fileName = disposition.match(/filename="?([^";]+)"?/)?.[1] || `Garmetix-${moduleKey}-${kind}.csv`
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = fileName
    document.body.appendChild(link)
    link.click()
    link.remove()
    URL.revokeObjectURL(url)
    feedback.notify(kind === 'export' ? 'Export downloaded' : 'Template downloaded', fileName)
  } catch (error) {
    feedback.failed(kind === 'export' ? 'Export failed' : 'Template failed', error)
  } finally {
    downloading.value = ''
  }
}

async function onFileChange(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  selectedFile.value = file || null
  filePreview.value = []
  importResult.value = null

  if (!file) {
    return
  }

  const text = await file.text()
  const lines = text.split(/\r?\n/).filter(Boolean).slice(0, 6)
  filePreview.value = lines.map((line, index) => ({
    line: index + 1,
    text: line
  }))
}

async function uploadCsv(commit: boolean) {
  if (!selectedFile.value) {
    feedback.failed('Select a CSV file first')
    return
  }

  importing.value = true
  importResult.value = null
  try {
    const form = new FormData()
    form.append('file', selectedFile.value)

    const response = await fetch(`${config.public.apiBase}/import-export/import/${selectedModule.value}?commit=${commit}`, {
      method: 'POST',
      headers: api.authHeaders(),
      body: form
    })

    if (!response.ok) {
      throw new Error(await response.text())
    }

    const result = await response.json()
    importResult.value = result

    if (result.errors?.length) {
      feedback.failed(commit ? 'Import blocked' : 'Validation failed', { message: `${result.errors.length} issue(s) found.` })
    } else {
      feedback.notify(commit ? 'Import complete' : 'Validation complete', commit
        ? `${result.created} created, ${result.updated} updated.`
        : `${result.validRows} rows ready.`)
      if (commit) {
        await refresh()
      }
    }
  } catch (error) {
    feedback.failed(commit ? 'Import failed' : 'Validation failed', error)
  } finally {
    importing.value = false
  }
}

function moduleInfo(key: string) {
  return moduleMeta[key] || {
    icon: 'i-lucide-file-spreadsheet',
    color: 'neutral' as UiColor,
    description: 'Garmetix data export.'
  }
}

async function exportAll() {
  for (const item of modules.value) {
    await downloadCsv(item.key, 'export')
  }
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
    title="Import Export"
    :companies="companies"
    :stores="stores"
    @refresh="refresh"
  >
    <section class="planner-dashboard">
      <UiModulePageHeader
        title="Import Export"
        description="Download CSV exports and matching import templates for Garmetix modules."
        icon="i-lucide-file-down"
        primary-label="Export All"
        primary-icon="i-lucide-download"
        @primary="exportAll"
      >
        <template #actions>
          <UBadge :color="loading ? 'warning' : 'success'" variant="subtle">
            {{ loading ? 'Loading' : 'Ready' }}
          </UBadge>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="subtle" :loading="loading" label="Refresh" @click="refresh" />
        </template>
      </UiModulePageHeader>

      <div class="planner-metric-grid">
        <UCard class="planner-metric-card">
          <div class="planner-metric-body">
            <UAvatar icon="i-lucide-database" color="primary" variant="subtle" />
            <div>
              <p>Modules</p>
              <strong>{{ modules.length }}</strong>
              <span>Export-ready datasets</span>
            </div>
          </div>
        </UCard>
        <UCard class="planner-metric-card">
          <div class="planner-metric-body">
            <UAvatar icon="i-lucide-columns-3" color="success" variant="subtle" />
            <div>
              <p>Columns</p>
              <strong>{{ totalColumns }}</strong>
              <span>Across templates</span>
            </div>
          </div>
        </UCard>
        <UCard class="planner-metric-card">
          <div class="planner-metric-body">
            <UAvatar icon="i-lucide-file-check-2" color="warning" variant="subtle" />
            <div>
              <p>Templates</p>
              <strong>{{ modules.length }}</strong>
              <span>Download before import</span>
            </div>
          </div>
        </UCard>
        <UCard class="planner-metric-card">
          <div class="planner-metric-body">
            <UAvatar icon="i-lucide-shield-check" color="neutral" variant="subtle" />
            <div>
              <p>Access</p>
              <strong>Admin</strong>
              <span>Protected exports</span>
            </div>
          </div>
        </UCard>
      </div>

      <div class="import-export-grid">
        <UCard v-for="item in modules" :key="item.key" class="planner-card">
          <template #header>
            <div class="setup-list-header">
              <div class="setup-tabs-title">
                <UAvatar :icon="moduleInfo(item.key).icon" :color="moduleInfo(item.key).color" variant="subtle" />
                <div>
                  <h3>{{ item.name }}</h3>
                  <p>{{ item.columns }} columns</p>
                </div>
              </div>
              <UBadge color="neutral" variant="subtle">CSV</UBadge>
            </div>
          </template>

          <p class="card-description">{{ moduleInfo(item.key).description }}</p>

          <div class="import-export-actions">
            <UButton
              icon="i-lucide-download"
              label="Export"
              :loading="downloading === `export:${item.key}`"
              @click="downloadCsv(item.key, 'export')"
            />
            <UButton
              icon="i-lucide-file-spreadsheet"
              color="neutral"
              variant="subtle"
              label="Template"
              :loading="downloading === `template:${item.key}`"
              @click="downloadCsv(item.key, 'template')"
            />
          </div>
        </UCard>
      </div>

      <UCard class="planner-card">
        <template #header>
          <div class="setup-list-header">
            <div>
              <h3>Import Preparation</h3>
              <p>Validate CSV shape before the write step.</p>
            </div>
            <UBadge color="warning" variant="subtle">Validation stage</UBadge>
          </div>
        </template>

        <div class="import-panel">
          <UFormField label="Module">
            <USelect
              v-model="selectedModule"
              :items="modules.map((item) => ({ label: item.name, value: item.key }))"
            />
          </UFormField>
          <UFormField label="CSV file">
            <UInput type="file" accept=".csv,text/csv" @change="onFileChange" />
          </UFormField>
          <UButton
            icon="i-lucide-file-spreadsheet"
            color="neutral"
            variant="subtle"
            label="Download Template"
            :loading="downloading === `template:${selectedModule}`"
            @click="downloadCsv(selectedModule, 'template')"
          />
        </div>

        <UAlert
          :color="selectedModuleInfo?.importSupported ? 'info' : 'warning'"
          variant="subtle"
          icon="i-lucide-info"
          :title="selectedModuleInfo?.importSupported ? 'Validated import is enabled' : 'Export only for now'"
          :description="selectedModuleInfo?.importSupported
            ? `Selected module: ${selectedModuleName}. Validate first, then import only when there are no row errors.`
            : `Selected module: ${selectedModuleName}. Download/export is available; write import will be enabled after module-specific rules are added.`"
        />

        <div class="import-export-actions">
          <UButton
            icon="i-lucide-check-check"
            color="neutral"
            variant="subtle"
            label="Validate CSV"
            :loading="importing"
            :disabled="!selectedFile || !selectedModuleInfo?.importSupported"
            @click="uploadCsv(false)"
          />
          <UButton
            icon="i-lucide-upload"
            label="Import CSV"
            :loading="importing"
            :disabled="!selectedFile || !selectedModuleInfo?.importSupported || Boolean(importResult?.errors?.length)"
            @click="uploadCsv(true)"
          />
        </div>

        <div v-if="importResult" class="planner-metric-grid mt-4">
          <UCard class="planner-metric-card">
            <div class="planner-metric-body">
              <UAvatar icon="i-lucide-list-checks" color="primary" variant="subtle" />
              <div>
                <p>Rows Read</p>
                <strong>{{ importResult.rowsRead }}</strong>
                <span>{{ importResult.commit ? 'Imported file' : 'Validated file' }}</span>
              </div>
            </div>
          </UCard>
          <UCard class="planner-metric-card">
            <div class="planner-metric-body">
              <UAvatar icon="i-lucide-check-circle" color="success" variant="subtle" />
              <div>
                <p>Valid</p>
                <strong>{{ importResult.validRows }}</strong>
                <span>Rows without errors</span>
              </div>
            </div>
          </UCard>
          <UCard class="planner-metric-card">
            <div class="planner-metric-body">
              <UAvatar icon="i-lucide-alert-circle" color="error" variant="subtle" />
              <div>
                <p>Invalid</p>
                <strong>{{ importResult.invalidRows }}</strong>
                <span>Rows needing fixes</span>
              </div>
            </div>
          </UCard>
          <UCard class="planner-metric-card">
            <div class="planner-metric-body">
              <UAvatar icon="i-lucide-database" color="neutral" variant="subtle" />
              <div>
                <p>Write Result</p>
                <strong>{{ importResult.created }} / {{ importResult.updated }}</strong>
                <span>Created / updated</span>
              </div>
            </div>
          </UCard>
        </div>

        <UAlert
          v-if="importResult?.warnings?.length"
          class="mt-4"
          color="warning"
          variant="subtle"
          icon="i-lucide-triangle-alert"
          title="Warnings"
          :description="importResult.warnings.join(' ')"
        />

        <UTable
          v-if="importRows.length"
          class="mt-4"
          :data="importRows"
          :columns="[
            { accessorKey: 'line', header: 'Line' },
            { accessorKey: 'field', header: 'Field' },
            { accessorKey: 'message', header: 'Message' }
          ]"
        />

        <UTable
          v-if="filePreview.length"
          class="mt-4"
          :data="filePreview"
          :columns="[
            { accessorKey: 'line', header: 'Line' },
            { accessorKey: 'text', header: 'Preview' }
          ]"
        />
      </UCard>
    </section>
  </AppShell>
</template>
