<template>
  <section class="space-y-4">
    <div class="border border-default bg-muted/10 p-5">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="text-sm text-muted">SaaS onboarding</p>
          <h2 class="mt-1 text-2xl font-semibold">Client Onboarding</h2>
          <p class="mt-2 max-w-3xl text-sm text-muted">Read-only onboarding summary and setup options for new Garmetix clients.</p>
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
      <AdminMasterTable :columns="columns" :rows="rows" empty-text="No onboarding rows returned." />
    </div>
  </section>
</template>

<script setup lang="ts">
import { readArray, readText, type ApiRecord, useAdminApiClient } from '../utils/admin-api'

useHead({ title: 'Client Onboarding - Garmetix Admin' })

const { get } = useAdminApiClient()
const loading = ref(true)
const error = ref('')
const summary = ref<ApiRecord | null>(null)
const options = ref<ApiRecord | null>(null)
const columns = [
  { key: 'name', label: 'Item' },
  { key: 'status', label: 'Status' },
  { key: 'detail', label: 'Detail' }
]
const optionRows = computed(() => readArray(options.value, ['items', 'options', 'steps']))
const summaryRows = computed(() => readArray(summary.value, ['items', 'steps', 'checks']))
const rows = computed(() => [...summaryRows.value, ...optionRows.value].map((item, index) => ({
  id: readText(item, ['id'], String(index)),
  name: readText(item, ['name', 'title', 'label'], `Item ${index + 1}`),
  status: readText(item, ['status', 'state']),
  detail: readText(item, ['detail', 'description', 'message'])
})))
const cards = computed(() => [
  { label: 'Summary', value: readText(summary.value, ['status', 'state'], 'Pending'), detail: readText(summary.value, ['message']) },
  { label: 'Options', value: optionRows.value.length, detail: 'Available setup options' },
  { label: 'Steps', value: summaryRows.value.length, detail: 'Onboarding checklist rows' },
  { label: 'Mode', value: readText(options.value, ['mode'], 'Read only'), detail: 'Foundation stage' }
])

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const [summaryData, optionData] = await Promise.allSettled([
      get<unknown>('client-onboarding/summary'),
      get<unknown>('client-onboarding/options')
    ])
    if (summaryData.status === 'fulfilled' && summaryData.value && typeof summaryData.value === 'object') summary.value = summaryData.value as ApiRecord
    if (optionData.status === 'fulfilled' && optionData.value && typeof optionData.value === 'object') options.value = optionData.value as ApiRecord
    const failed = [summaryData, optionData].filter(item => item.status === 'rejected').length
    if (failed) error.value = `${failed} onboarding request(s) could not be loaded.`
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load client onboarding.'
  } finally {
    loading.value = false
  }
}

onMounted(refresh)
</script>
