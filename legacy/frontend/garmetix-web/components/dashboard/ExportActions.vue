<script setup lang="ts">
type ExportColumn = {
  key: string
  label: string
}

const props = withDefaults(defineProps<{
  title: string
  subtitle?: string
  fileName: string
  data?: Record<string, any> | null
  tables?: Array<{ name: string, rows?: any[], columns?: ExportColumn[] }>
  loading?: boolean
}>(), {
  subtitle: 'Export the current dashboard snapshot for review, sharing or offline checks.',
  data: null,
  tables: () => [],
  loading: false
})

const exportedAt = computed(() => new Date().toISOString())
const safeFileName = computed(() => props.fileName.replace(/[^a-z0-9-_]+/gi, '-').replace(/^-+|-+$/g, '').toLowerCase() || 'dashboard-snapshot')

function flattenValue(value: unknown) {
  if (value === null || value === undefined) return ''
  if (typeof value === 'object') return JSON.stringify(value)
  return String(value)
}

function toCsv(rows: any[], columns?: ExportColumn[]) {
  const sourceColumns = columns?.length
    ? columns
    : Object.keys(rows[0] || {}).map((key) => ({ key, label: key }))
  const header = sourceColumns.map((column) => csvCell(column.label)).join(',')
  const body = rows.map((row) => sourceColumns.map((column) => csvCell(flattenValue(row?.[column.key]))).join(',')).join('\n')
  return [header, body].filter(Boolean).join('\n')
}

function csvCell(value: string) {
  return `"${value.replace(/"/g, '""')}"`
}

function downloadBlob(content: string, mimeType: string, name: string) {
  if (!import.meta.client) return
  const blob = new Blob([content], { type: mimeType })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = name
  document.body.appendChild(link)
  link.click()
  link.remove()
  URL.revokeObjectURL(url)
}

function exportJson() {
  const payload = {
    title: props.title,
    exportedAt: exportedAt.value,
    dashboard: props.data || {},
    tables: props.tables || []
  }
  downloadBlob(JSON.stringify(payload, null, 2), 'application/json;charset=utf-8', `${safeFileName.value}-${Date.now()}.json`)
}

function exportCsv() {
  const tables = props.tables?.filter((table) => table.rows?.length) || []
  const csv = tables.length
    ? tables.map((table) => `# ${table.name}\n${toCsv(table.rows || [], table.columns)}`).join('\n\n')
    : toCsv([props.data || {}])
  downloadBlob(csv, 'text/csv;charset=utf-8', `${safeFileName.value}-${Date.now()}.csv`)
}

function printDashboard() {
  if (!import.meta.client) return
  window.print()
}
</script>

<template>
  <UCard class="dashboard-export-card print:hidden">
    <div class="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
      <div class="min-w-0">
        <div class="flex items-center gap-2">
          <UIcon name="i-lucide-download" class="size-4 text-primary" />
          <p class="text-sm font-semibold text-highlighted">Snapshot & export</p>
        </div>
        <p class="mt-1 text-xs text-muted">{{ subtitle }}</p>
      </div>
      <div class="flex flex-wrap gap-2">
        <UButton size="sm" variant="soft" color="neutral" icon="i-lucide-file-json" :disabled="loading" @click="exportJson">
          JSON
        </UButton>
        <UButton size="sm" variant="soft" color="neutral" icon="i-lucide-file-spreadsheet" :disabled="loading" @click="exportCsv">
          CSV
        </UButton>
        <UButton size="sm" color="primary" variant="soft" icon="i-lucide-printer" :disabled="loading" @click="printDashboard">
          Print / PDF
        </UButton>
      </div>
    </div>
  </UCard>
</template>
