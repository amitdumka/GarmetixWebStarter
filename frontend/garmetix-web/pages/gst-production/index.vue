<script setup lang="ts">
const api = useGarmetixApi()
const loading = ref(false)
const readiness = ref<any>(null)
const status = ref<any>(null)
const acceptance = ref<any>(null)
const error = ref('')

async function load() {
  loading.value = true
  error.value = ''
  const results = await Promise.allSettled([
    api.get<any>('gst-production/readiness'),
    api.get<any>('gst-production/e-invoice/status'),
    api.get<any>('gst-production/final-acceptance')
  ])
  if (results[0].status === 'fulfilled') readiness.value = results[0].value
  if (results[1].status === 'fulfilled') status.value = results[1].value
  if (results[2].status === 'fulfilled') acceptance.value = results[2].value
  const rejected = results.find((item) => item.status === 'rejected') as PromiseRejectedResult | undefined
  if (rejected) error.value = rejected.reason?.data?.message || rejected.reason?.message || 'Some GST production checks failed.'
  loading.value = false
}

onMounted(load)
</script>

<template>
  <UContainer class="space-y-6 py-6">
    <div class="flex items-center justify-between gap-3">
      <div>
        <p class="text-sm text-primary font-semibold">Stage 10D</p>
        <h1 class="text-2xl font-bold">GST / e-Invoice Production Readiness</h1>
        <p class="text-sm text-muted">Review GST export, CA workflow and e-Invoice provider readiness before live IRP posting.</p>
      </div>
      <UButton icon="i-lucide-refresh-cw" :loading="loading" @click="load">Refresh</UButton>
    </div>
    <UAlert v-if="error" color="warning" icon="i-lucide-triangle-alert" :title="error" />
    <div class="grid gap-4 md:grid-cols-3">
      <UCard><p class="text-sm text-muted">GST export</p><UBadge :color="readiness?.gstExportReady ? 'success' : 'warning'">{{ readiness?.gstExportReady ? 'Ready' : 'Review' }}</UBadge></UCard>
      <UCard><p class="text-sm text-muted">e-Invoice provider</p><UBadge :color="readiness?.eInvoiceProviderConfigured ? 'success' : 'warning'">{{ status?.provider || 'Not configured' }}</UBadge></UCard>
      <UCard><p class="text-sm text-muted">Live posting</p><UBadge :color="readiness?.livePostingEnabled ? 'success' : 'neutral'">{{ readiness?.livePostingEnabled ? 'Enabled' : 'Disabled' }}</UBadge></UCard>
    </div>
    <UCard>
      <template #header><h2 class="font-semibold">Final acceptance</h2></template>
      <div class="space-y-3">
        <div v-for="item in acceptance?.checks || []" :key="item.item" class="rounded-lg border p-3">
          <div class="flex items-center justify-between"><p class="font-medium">{{ item.item }}</p><UBadge>{{ item.status }}</UBadge></div>
          <p class="text-sm text-muted">{{ item.detail }}</p>
        </div>
      </div>
    </UCard>
    <UAlert color="info" icon="i-lucide-info" title="Safety guard" description="Live e-Invoice posting stays off until provider credentials and production mode are explicitly configured." />
  </UContainer>
</template>
