<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-smartphone" class="size-4" /> Android kiosk</p>
          <h2 class="garmetix-dashboard-title">Mobile Kiosk</h2>
          <p class="garmetix-dashboard-subtitle">
            MAUI Android kiosk build profile, API contract and offline queue rules.
          </p>
        </div>
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="load">Refresh</UButton>
      </div>
    </div>

    <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="error" />

    <div class="grid gap-3 md:grid-cols-4">
      <div v-for="card in cards" :key="card.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label">{{ card.label }}</p>
        <p class="garmetix-metric-value text-lg">{{ card.value }}</p>
        <p class="garmetix-metric-caption">{{ card.caption }}</p>
      </div>
    </div>

    <div class="grid gap-4 xl:grid-cols-[minmax(0,1fr)_minmax(360px,0.9fr)]">
      <div class="garmetix-table-panel">
        <div class="garmetix-panel-header">
          <div>
            <h3 class="garmetix-panel-title">Kiosk API Routes</h3>
            <p class="garmetix-panel-subtitle">The Android kiosk must use these shared API contracts.</p>
          </div>
          <UButton size="sm" variant="soft" icon="i-lucide-list-checks" to="/attendance/mobile-kiosk-rehearsal">Rehearsal</UButton>
        </div>
        <div class="grid gap-2 md:grid-cols-2">
          <div v-for="route in textList(status, ['routes'])" :key="route" class="rounded-lg border border-default p-3 text-sm">
            <p class="font-medium">{{ route }}</p>
            <p class="text-xs text-muted">Validated against the shared attendance API.</p>
          </div>
        </div>
      </div>

      <div class="garmetix-table-panel">
        <div class="garmetix-panel-header">
          <div>
            <h3 class="garmetix-panel-title">Offline Queue Contract</h3>
            <p class="garmetix-panel-subtitle">{{ readText(contract, ['sqliteTable'], 'pending_punches') }}</p>
          </div>
        </div>
        <ul class="space-y-2 text-sm text-muted">
          <li v-for="item in textList(contract, ['columns'])" :key="item" class="flex gap-2">
            <UIcon name="i-lucide-database" class="mt-0.5 size-4 shrink-0 text-primary" />
            <span>{{ item }}</span>
          </li>
        </ul>
      </div>
    </div>

    <div class="grid gap-4 lg:grid-cols-2">
      <div class="garmetix-table-panel">
        <div class="garmetix-panel-header">
          <div>
            <h3 class="garmetix-panel-title">Safety Rules</h3>
            <p class="garmetix-panel-subtitle">Rules for deployment and store use.</p>
          </div>
        </div>
        <ul class="space-y-2 text-sm text-muted">
          <li v-for="item in textList(status, ['safetyRules'])" :key="item" class="flex gap-2">
            <UIcon name="i-lucide-shield-check" class="mt-0.5 size-4 shrink-0 text-primary" />
            <span>{{ item }}</span>
          </li>
        </ul>
      </div>

      <div class="garmetix-table-panel">
        <div class="garmetix-panel-header">
          <div>
            <h3 class="garmetix-panel-title">Acceptance Checks</h3>
            <p class="garmetix-panel-subtitle">Run these before using a tablet in store.</p>
          </div>
        </div>
        <ul class="space-y-2 text-sm text-muted">
          <li v-for="item in textList(status, ['acceptanceChecks'])" :key="item" class="flex gap-2">
            <UIcon name="i-lucide-clipboard-check" class="mt-0.5 size-4 shrink-0 text-success" />
            <span>{{ item }}</span>
          </li>
        </ul>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { readArray, readText, type ApiRecord, useHrApiClient } from '../../utils/hr-api'

useHead({ title: 'Mobile Kiosk - Garmetix HR' })

const { get } = useHrApiClient()
const loading = ref(false)
const error = ref('')
const status = ref<ApiRecord | null>(null)
const contract = ref<ApiRecord | null>(null)

const cards = computed(() => [
  { label: 'Status', value: readText(status.value, ['status']), caption: readText(status.value, ['projectPath']) },
  { label: 'Target', value: readText(status.value, ['target']), caption: readText(status.value, ['apiBaseConfiguration']) },
  { label: 'Queue', value: readText(status.value, ['queueProvider']), caption: readText(contract.value, ['sqliteTable']) },
  { label: 'Sync Endpoint', value: readText(contract.value?.syncContract as ApiRecord, ['endpoint']), caption: readText(contract.value?.syncContract as ApiRecord, ['method']) }
])

function textList(source: ApiRecord | null | undefined, keys: string[]) {
  return readArray(source, keys).map(item => typeof item === 'string' ? item : JSON.stringify(item))
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    const [statusResponse, contractResponse] = await Promise.all([
      get<ApiRecord>('api/attendance/mobile-kiosk/status'),
      get<ApiRecord>('api/attendance/mobile-kiosk/offline-contract')
    ])
    status.value = statusResponse
    contract.value = contractResponse
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load mobile kiosk status.'
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>
