<template>
  <section class="space-y-4">
    <div class="border border-default bg-muted/10 p-5">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="text-sm text-muted">Production readiness</p>
          <h2 class="mt-1 text-2xl font-semibold">GST Production Readiness</h2>
          <p class="mt-2 max-w-3xl text-sm text-muted">
            Review GST export readiness, e-invoice provider status, GSTIN provider configuration and portal schema mapping. Admin-only final acceptance remains outside this Books screen.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
          <UButton icon="i-lucide-file-spreadsheet" color="primary" variant="soft" :loading="downloadLoading" @click="downloadSchemaReview">Schema Excel</UButton>
        </div>
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

    <section class="grid gap-4 xl:grid-cols-2">
      <div class="border border-default bg-muted/10 p-4">
        <h3 class="mb-3 text-base font-semibold">Readiness</h3>
        <BooksMasterTable :columns="detailColumns" :rows="readinessRows" empty-text="No readiness rows found." />
      </div>
      <div class="border border-default bg-muted/10 p-4">
        <h3 class="mb-3 text-base font-semibold">Provider Status</h3>
        <BooksMasterTable :columns="detailColumns" :rows="providerRows" empty-text="No provider rows found." />
      </div>
    </section>

    <section class="border border-default bg-muted/10 p-4">
      <div class="mb-3 flex flex-col gap-2 lg:flex-row lg:items-center lg:justify-between">
        <div>
          <h3 class="text-base font-semibold">GST Schema Review</h3>
          <p class="text-xs text-muted">{{ readText(schemaReview, ['title']) }}</p>
        </div>
        <USelect v-model="schemaTab" :items="schemaItems" class="lg:w-44" />
      </div>
      <BooksMasterTable :columns="schemaColumns" :rows="schemaRows" empty-text="No schema review rows found." />
    </section>

    <section class="border border-default bg-muted/10 p-4">
      <h3 class="mb-3 text-base font-semibold">Warnings and Notes</h3>
      <ul class="space-y-2 text-sm text-muted">
        <li v-for="note in notes" :key="note" class="border-b border-default pb-2">{{ note }}</li>
        <li v-if="notes.length === 0">No readiness notes available.</li>
      </ul>
    </section>
  </section>
</template>

<script setup lang="ts">
import {
  readArray,
  readText,
  type ApiRecord,
  useBooksApiClient
} from '../utils/books-api'

useHead({ title: 'GST Production - Garmetix Books' })

const { download, get } = useBooksApiClient()
const loading = ref(true)
const downloadLoading = ref(false)
const error = ref('')
const readiness = ref<ApiRecord | null>(null)
const eInvoice = ref<ApiRecord | null>(null)
const gstinProvider = ref<ApiRecord | null>(null)
const schemaReview = ref<ApiRecord | null>(null)
const schemaTab = ref<'gstr1' | 'gstr3b'>('gstr1')

const schemaItems = [
  { label: 'GSTR-1', value: 'gstr1' },
  { label: 'GSTR-3B', value: 'gstr3b' }
]
const cards = computed(() => [
  { label: 'GST Export', value: yesNo(readiness.value?.gstExportReady), detail: 'Return export readiness' },
  { label: 'CA Review', value: yesNo(readiness.value?.caReviewWorkflowReady), detail: 'Review workflow' },
  { label: 'GSTIN Provider', value: yesNo(gstinProvider.value?.ready), detail: readText(gstinProvider.value, ['sourceName']) },
  { label: 'E-Invoice Live', value: yesNo(eInvoice.value?.livePostingEnabled), detail: readText(eInvoice.value, ['provider']) }
])
const readinessRows = computed(() => [
  { label: 'Version', value: readText(readiness.value, ['version']) },
  { label: 'GST Export Ready', value: yesNo(readiness.value?.gstExportReady) },
  { label: 'CA Review Workflow', value: yesNo(readiness.value?.caReviewWorkflowReady) },
  { label: 'E-Invoice Provider Configured', value: yesNo(readiness.value?.eInvoiceProviderConfigured) },
  { label: 'GSTIN Provider Configured', value: yesNo(readiness.value?.gstinProviderConfigured) },
  { label: 'Live Posting Enabled', value: yesNo(readiness.value?.livePostingEnabled) },
  { label: 'IRN Generation', value: readText(eInvoice.value, ['irnGeneration']) },
  { label: 'QR Handling', value: readText(eInvoice.value, ['qrHandling']) }
])
const providerRows = computed(() => [
  { label: 'GSTIN Lookup Enabled', value: yesNo(gstinProvider.value?.enabled) },
  { label: 'GSTIN Lookup Ready', value: yesNo(gstinProvider.value?.ready) },
  { label: 'Source', value: readText(gstinProvider.value, ['sourceName']) },
  { label: 'Timeout', value: readText(gstinProvider.value, ['timeoutSeconds']) },
  { label: 'API Key Header', value: readText(gstinProvider.value, ['apiKeyHeaderName']) },
  { label: 'E-Invoice Provider', value: readText(eInvoice.value, ['provider']) },
  { label: 'E-Invoice Cancel Flow', value: readText(eInvoice.value, ['cancelFlow']) }
])
const schemaRows = computed(() => readArray(schemaReview.value, [schemaTab.value]).map(item => ({
  section: readText(item, ['section']),
  exportKey: readText(item, ['exportKey']),
  status: readText(item, ['status']),
  notes: readText(item, ['notes'])
})))
const notes = computed(() => [
  ...readArray(readiness.value, ['warnings']).map(item => String(item)),
  ...readArray(gstinProvider.value, ['issues']).map(item => `GSTIN issue: ${String(item)}`),
  ...readArray(gstinProvider.value, ['recommendations']).map(item => `Recommendation: ${String(item)}`),
  ...readArray(schemaReview.value, ['warnings']).map(item => String(item))
])
const detailColumns = [
  { key: 'label', label: 'Check' },
  { key: 'value', label: 'Status' }
]
const schemaColumns = [
  { key: 'section', label: 'Section' },
  { key: 'exportKey', label: 'Export Key' },
  { key: 'status', label: 'Status' },
  { key: 'notes', label: 'Notes' }
]

function yesNo(value: unknown) {
  if (value === true) return 'Ready'
  if (value === false) return 'Pending'
  return '-'
}

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const [readinessData, eInvoiceData, gstinData, schemaData] = await Promise.allSettled([
      get<unknown>('gst-production/readiness'),
      get<unknown>('gst-production/e-invoice/status'),
      get<unknown>('gstin/provider/status'),
      get<unknown>('gst-returns/schema-review')
    ])
    if (readinessData.status === 'fulfilled' && readinessData.value && typeof readinessData.value === 'object') readiness.value = readinessData.value as ApiRecord
    if (eInvoiceData.status === 'fulfilled' && eInvoiceData.value && typeof eInvoiceData.value === 'object') eInvoice.value = eInvoiceData.value as ApiRecord
    if (gstinData.status === 'fulfilled' && gstinData.value && typeof gstinData.value === 'object') gstinProvider.value = gstinData.value as ApiRecord
    if (schemaData.status === 'fulfilled' && schemaData.value && typeof schemaData.value === 'object') schemaReview.value = schemaData.value as ApiRecord
    const failed = [readinessData, eInvoiceData, gstinData, schemaData].filter(item => item.status === 'rejected').length
    if (failed) error.value = `${failed} GST readiness request(s) could not be loaded.`
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load GST production readiness.'
  } finally {
    loading.value = false
  }
}

async function downloadSchemaReview() {
  downloadLoading.value = true
  error.value = ''
  try {
    await download('gst-returns/schema-review/excel', undefined, 'Garmetix-GST-Schema-Review.xlsx')
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to download GST schema review.'
  } finally {
    downloadLoading.value = false
  }
}

onMounted(refresh)
</script>
