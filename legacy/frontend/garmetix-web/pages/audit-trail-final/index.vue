<script setup lang="ts">
const api = useGarmetixApi()
const loading = ref(false)
const acceptance = ref<any>(null)
const history = ref<any>(null)
const error = ref('')

async function load() {
  loading.value = true
  error.value = ''
  const results = await Promise.allSettled([
    api.get<any>('audit-trail/final-acceptance'),
    api.get<any>('audit-trail/change-history?take=50')
  ])
  if (results[0].status === 'fulfilled') acceptance.value = results[0].value
  if (results[1].status === 'fulfilled') history.value = results[1].value
  const rejected = results.find((item) => item.status === 'rejected') as PromiseRejectedResult | undefined
  if (rejected) error.value = rejected.reason?.data?.message || rejected.reason?.message || 'Audit final acceptance refresh failed.'
  loading.value = false
}

onMounted(load)
</script>

<template>
  <UContainer class="space-y-6 py-6">
    <div class="flex items-center justify-between gap-3">
      <div>
        <p class="text-sm text-primary font-semibold">Stage 10F</p>
        <h1 class="text-2xl font-bold">Audit Trail / Change History Final Acceptance</h1>
        <p class="text-sm text-muted">Review edit/delete history coverage for financial, HR, payroll and attendance records.</p>
      </div>
      <UButton icon="i-lucide-refresh-cw" :loading="loading" @click="load">Refresh</UButton>
    </div>
    <UAlert v-if="error" color="warning" icon="i-lucide-triangle-alert" :title="error" />
    <div class="grid gap-4 md:grid-cols-3">
      <UCard><p class="text-sm text-muted">Audit rows</p><p class="text-2xl font-bold">{{ acceptance?.totalAuditRows ?? 0 }}</p></UCard>
      <UCard><p class="text-sm text-muted">Modules logged</p><p class="text-2xl font-bold">{{ acceptance?.modules?.length ?? 0 }}</p></UCard>
      <UCard><p class="text-sm text-muted">Recent changes</p><p class="text-2xl font-bold">{{ history?.rows?.length ?? 0 }}</p></UCard>
    </div>
    <UCard>
      <template #header><h2 class="font-semibold">Acceptance checks</h2></template>
      <div class="space-y-3">
        <div v-for="item in acceptance?.checks || []" :key="item.item" class="rounded-lg border p-3">
          <div class="flex items-center justify-between"><p class="font-medium">{{ item.item }}</p><UBadge>{{ item.status }}</UBadge></div>
          <p class="text-sm text-muted">{{ item.detail }}</p>
        </div>
      </div>
    </UCard>
    <UCard>
      <template #header><h2 class="font-semibold">Recent change history</h2></template>
      <div class="space-y-2">
        <div v-for="row in history?.rows || []" :key="row.id" class="rounded-lg border p-3 text-sm">
          <div class="flex items-center justify-between gap-3"><p class="font-medium">{{ row.module }} · {{ row.entityDisplayName || row.entityName }}</p><UBadge>{{ row.action }}</UBadge></div>
          <p class="text-muted">{{ row.userName || 'System' }} · {{ row.requestPath || 'Background' }} · Fields: {{ row.changedFieldCount }}</p>
        </div>
      </div>
    </UCard>
  </UContainer>
</template>
