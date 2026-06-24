<template>
  <section class="space-y-4">
    <div class="border border-default bg-muted/10 p-5">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="text-sm text-muted">SaaS license</p>
          <h2 class="mt-1 text-2xl font-semibold">License Activation</h2>
          <p class="mt-2 max-w-3xl text-sm text-muted">Read-only license status and enforcement review. Activation/generation actions are deferred to a later audited owner-only stage.</p>
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
      <h3 class="mb-3 text-base font-semibold">License Detail</h3>
      <dl class="grid gap-3 text-sm md:grid-cols-2">
        <div v-for="item in details" :key="item.label" class="border-b border-default pb-2">
          <dt class="text-xs text-muted">{{ item.label }}</dt>
          <dd class="mt-1 break-words font-medium">{{ item.value }}</dd>
        </div>
      </dl>
    </div>
  </section>
</template>

<script setup lang="ts">
import { formatDateTime, readText, type ApiRecord, useAdminApiClient } from '../utils/admin-api'

useHead({ title: 'License - Garmetix Admin' })

const { get } = useAdminApiClient()
const loading = ref(true)
const error = ref('')
const status = ref<ApiRecord | null>(null)
const cards = computed(() => [
  { label: 'State', value: readText(status.value, ['state', 'status'], 'Pending'), detail: 'License enforcement state' },
  { label: 'Tenant', value: readText(status.value, ['tenantCode', 'tenantId', 'clientCode']), detail: 'Configured client/tenant' },
  { label: 'Expires', value: formatDateTime(status.value?.expiresAtUtc ?? status.value?.expiresAt), detail: 'License expiry' },
  { label: 'Mode', value: readText(status.value, ['mode', 'enforcementMode']), detail: 'Activation mode' }
])
const details = computed(() => Object.entries(status.value ?? {}).slice(0, 24).map(([label, value]) => ({
  label,
  value: typeof value === 'object' ? JSON.stringify(value) : String(value ?? '-')
})))

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const data = await get<unknown>('license/status')
    if (data && typeof data === 'object') status.value = data as ApiRecord
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load license status.'
  } finally {
    loading.value = false
  }
}

onMounted(refresh)
</script>
