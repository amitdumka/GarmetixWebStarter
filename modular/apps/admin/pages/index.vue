<template>
  <section class="space-y-4">
    <div class="border border-default bg-muted/10 p-5">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="text-sm text-muted">Admin command center</p>
          <h2 class="mt-1 text-2xl font-semibold">SaaS, Setup And System Control</h2>
          <p class="mt-2 max-w-3xl text-sm text-muted">
            Owner/admin workspace for company setup, users, license, logs, runtime health and deployment readiness. This foundation stage keeps destructive operations out of the modular UI.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton to="/system-health" icon="i-lucide-heart-pulse">System Health</UButton>
          <UButton to="/message-logs" color="neutral" variant="soft" icon="i-lucide-message-square-warning">Message Logs</UButton>
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
        <div class="mb-3 flex items-center justify-between gap-3">
          <h3 class="text-base font-semibold">Admin Coverage</h3>
          <UBadge :color="loading ? 'warning' : 'primary'" variant="subtle">{{ loading ? 'Loading' : 'Ready' }}</UBadge>
        </div>
        <div class="grid gap-2 sm:grid-cols-2">
          <UButton v-for="item in quickLinks" :key="item.href" :to="item.href" :icon="item.icon" color="neutral" variant="soft" class="justify-start">
            {{ item.label }}
          </UButton>
        </div>
      </div>

      <div class="border border-default bg-muted/10 p-4">
        <div class="mb-3 flex items-center justify-between gap-3">
          <h3 class="text-base font-semibold">Runtime Snapshot</h3>
          <UButton to="/runtime-diagnostics" size="sm" color="neutral" variant="ghost" icon="i-lucide-arrow-right">Open</UButton>
        </div>
        <div v-if="probes.length" class="space-y-2">
          <div v-for="probe in probes.slice(0, 6)" :key="readText(probe, ['title'])" class="flex items-center justify-between gap-3 border border-default bg-default/40 p-3">
            <div class="min-w-0">
              <p class="truncate text-sm font-medium">{{ readText(probe, ['title']) }}</p>
              <p class="text-xs text-muted">{{ readText(probe, ['detail']) }}</p>
            </div>
            <UBadge :color="probe.passed === false ? 'warning' : 'success'" variant="subtle">{{ readText(probe, ['status'], 'Ok') }}</UBadge>
          </div>
        </div>
        <p v-else class="text-sm text-muted">Runtime probes will appear after diagnostics load.</p>
      </div>
    </section>
  </section>
</template>

<script setup lang="ts">
import { readArray, readNumber, readText, toRows, type ApiRecord, useAdminApiClient } from '../utils/admin-api'

useHead({ title: 'Admin - Garmetix Admin' })

const { get } = useAdminApiClient()
const loading = ref(true)
const error = ref('')
const companies = ref<ApiRecord[]>([])
const stores = ref<ApiRecord[]>([])
const users = ref<ApiRecord[]>([])
const runtime = ref<ApiRecord | null>(null)
const license = ref<ApiRecord | null>(null)

const probes = computed(() => readArray(runtime.value, ['probes']))
const cards = computed(() => [
  { label: 'Companies', value: companies.value.length, detail: 'Company master rows' },
  { label: 'Stores', value: stores.value.length, detail: 'Store master rows' },
  { label: 'Users', value: users.value.length, detail: 'Access users visible to this admin' },
  { label: 'Runtime Issues', value: readNumber(runtime.value, ['failedProbeCount']), detail: readText(runtime.value, ['status'], 'Diagnostics pending') }
])
const quickLinks = [
  { label: 'Company', href: '/setup', icon: 'i-lucide-building-2' },
  { label: 'Users & Roles', href: '/access', icon: 'i-lucide-shield-check' },
  { label: 'License', href: '/license-activation', icon: 'i-lucide-key-round' },
  { label: 'Message Logs', href: '/message-logs', icon: 'i-lucide-message-square-warning' },
  { label: 'Import Export', href: '/import-export', icon: 'i-lucide-arrow-up-down' },
  { label: 'Data Consistency', href: '/data-consistency', icon: 'i-lucide-database-zap' },
  { label: 'System Health', href: '/system-health', icon: 'i-lucide-heart-pulse' },
  { label: 'Runtime', href: '/runtime-diagnostics', icon: 'i-lucide-bug' }
]

onMounted(async () => {
  loading.value = true
  error.value = ''
  try {
    const [companyData, storeData, userData, runtimeData, licenseData] = await Promise.allSettled([
      get<unknown>('companies'),
      get<unknown>('stores'),
      get<unknown>('access/users'),
      get<unknown>('runtime-diagnostics'),
      get<unknown>('license/status')
    ])
    if (companyData.status === 'fulfilled') companies.value = toRows(companyData.value)
    if (storeData.status === 'fulfilled') stores.value = toRows(storeData.value)
    if (userData.status === 'fulfilled') users.value = toRows(userData.value)
    if (runtimeData.status === 'fulfilled' && runtimeData.value && typeof runtimeData.value === 'object') runtime.value = runtimeData.value as ApiRecord
    if (licenseData.status === 'fulfilled' && licenseData.value && typeof licenseData.value === 'object') license.value = licenseData.value as ApiRecord
    const failed = [companyData, storeData, userData, runtimeData, licenseData].filter(item => item.status === 'rejected').length
    if (failed) error.value = `${failed} admin summary request(s) could not be loaded yet.`
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load admin dashboard.'
  } finally {
    loading.value = false
  }
})
</script>
