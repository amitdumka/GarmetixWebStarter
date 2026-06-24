<template>
  <section class="space-y-4">
    <div class="border border-default bg-muted/10 p-5">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="text-sm text-muted">Data movement</p>
          <h2 class="mt-1 text-2xl font-semibold">Import Export</h2>
          <p class="mt-2 max-w-3xl text-sm text-muted">Read-only module catalog, import/export center status and engine health. Upload/commit actions remain in the legacy audited flow for now.</p>
        </div>
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
      </div>
    </div>

    <UAlert v-if="error" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />

    <section class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
      <div v-for="card in cards" :key="card.label" class="border border-default bg-muted/20 p-4">
        <p class="text-sm text-muted">{{ card.label }}</p>
        <p class="mt-2 text-2xl font-semibold">{{ card.value }}</p>
        <p class="mt-1 text-xs text-muted">{{ card.detail }}</p>
      </div>
    </section>

    <div class="border border-default bg-muted/10 p-4">
      <AdminMasterTable :columns="columns" :rows="rows" empty-text="No import/export modules returned." />
    </div>
  </section>
</template>

<script setup lang="ts">
import { readArray, readNumber, readText, toRows, type ApiRecord, useAdminApiClient } from '../utils/admin-api'

useHead({ title: 'Import Export - Garmetix Admin' })

const { get } = useAdminApiClient()
const loading = ref(true)
const error = ref('')
const modules = ref<ApiRecord[]>([])
const center = ref<ApiRecord | null>(null)
const health = ref<ApiRecord | null>(null)
const columns = [
  { key: 'name', label: 'Module' },
  { key: 'export', label: 'Export' },
  { key: 'template', label: 'Template' },
  { key: 'import', label: 'Import' }
]
const cards = computed(() => [
  { label: 'Modules', value: modules.value.length, detail: 'Registered import/export modules' },
  { label: 'Health', value: readText(health.value, ['status'], 'Pending'), detail: readText(health.value, ['message']) },
  { label: 'Center Rows', value: readArray(center.value, ['modules', 'items']).length, detail: 'Center module rows' },
  { label: 'Warnings', value: readNumber(health.value, ['warningCount']), detail: 'Import/export warnings' }
])
const rows = computed(() => modules.value.map(item => ({
  name: readText(item, ['name', 'module']),
  export: readText(item, ['exportPath', 'exportUrl'], 'Available'),
  template: readText(item, ['templatePath', 'templateUrl'], 'Available'),
  import: readText(item, ['importPath', 'uploadUrl'], 'Legacy flow')
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
    if (moduleData.status === 'fulfilled') modules.value = toRows(moduleData.value)
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

onMounted(refresh)
</script>
