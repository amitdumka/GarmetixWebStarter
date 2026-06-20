<script setup lang="ts">
const api = useGarmetixApi()
const loading = ref(false)
const acceptance = ref<any>(null)
const readiness = ref<any>(null)
const error = ref('')

async function load() {
  loading.value = true
  error.value = ''
  const results = await Promise.allSettled([
    api.get<any>('google-drive-backup/final-acceptance'),
    api.get<any>('google-drive-backup/sync-readiness')
  ])
  if (results[0].status === 'fulfilled') acceptance.value = results[0].value
  if (results[1].status === 'fulfilled') readiness.value = results[1].value
  const rejected = results.find((item) => item.status === 'rejected') as PromiseRejectedResult | undefined
  if (rejected) error.value = rejected.reason?.data?.message || rejected.reason?.message || 'Google Drive backup refresh failed.'
  loading.value = false
}

onMounted(load)
</script>

<template>
  <UContainer class="space-y-6 py-6">
    <div class="flex items-center justify-between gap-3">
      <div>
        <p class="text-sm text-primary font-semibold">Stage 10E</p>
        <h1 class="text-2xl font-bold">Google Drive Backup Sync</h1>
        <p class="text-sm text-muted">Cloud backup configuration visibility and final acceptance for production backup safety.</p>
      </div>
      <UButton icon="i-lucide-refresh-cw" :loading="loading" @click="load">Refresh</UButton>
    </div>
    <UAlert v-if="error" color="warning" icon="i-lucide-triangle-alert" :title="error" />
    <div class="grid gap-4 md:grid-cols-4">
      <UCard><p class="text-sm text-muted">Cloud ready</p><UBadge :color="readiness?.ready ? 'success' : 'warning'">{{ readiness?.ready ? 'Ready' : 'Pending config' }}</UBadge></UCard>
      <UCard><p class="text-sm text-muted">Upload on backup</p><UBadge>{{ readiness?.uploadOnBackup ? 'Yes' : 'No' }}</UBadge></UCard>
      <UCard><p class="text-sm text-muted">Retention days</p><p class="text-2xl font-bold">{{ acceptance?.localRetentionDays ?? '-' }}</p></UCard>
      <UCard><p class="text-sm text-muted">Keep minimum</p><p class="text-2xl font-bold">{{ acceptance?.localKeepMinimum ?? '-' }}</p></UCard>
    </div>
    <UCard>
      <template #header><h2 class="font-semibold">Checks</h2></template>
      <div class="space-y-3">
        <div v-for="item in acceptance?.checks || []" :key="item.item" class="rounded-lg border p-3">
          <div class="flex items-center justify-between"><p class="font-medium">{{ item.item }}</p><UBadge>{{ item.status }}</UBadge></div>
          <p class="text-sm text-muted">{{ item.detail }}</p>
        </div>
      </div>
    </UCard>
  </UContainer>
</template>
