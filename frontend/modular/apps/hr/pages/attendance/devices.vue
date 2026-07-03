<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-fingerprint" class="size-4" /> Attendance devices</p>
          <h2 class="garmetix-dashboard-title">Kiosk Devices</h2>
          <p class="garmetix-dashboard-subtitle">
            Device registration and revocation are intentionally left for the next HR stage. This page checks the current device list.
          </p>
        </div>
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="load">Refresh</UButton>
      </div>
    </div>

    <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="error" />

    <div class="grid gap-3 md:grid-cols-3">
      <div v-for="card in cards" :key="card.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label">{{ card.label }}</p>
        <p class="garmetix-metric-value">{{ card.value }}</p>
      </div>
    </div>

    <div class="garmetix-table-panel">
      <div class="garmetix-panel-header">
        <div>
          <h3 class="garmetix-panel-title">Registered Devices</h3>
          <p class="garmetix-panel-subtitle">{{ devices.length }} device row(s)</p>
        </div>
      </div>
      <div class="overflow-auto">
        <table class="w-full min-w-[700px] text-left text-sm">
          <thead class="bg-muted/30 text-xs uppercase text-muted">
            <tr>
              <th class="px-3 py-2">Device</th>
              <th class="px-3 py-2">Code</th>
              <th class="px-3 py-2">Store</th>
              <th class="px-3 py-2">Status</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(device, index) in devices" :key="readText(device, ['id', 'deviceCode'], String(index))" class="border-t border-default">
              <td class="px-3 py-2 font-medium">{{ readText(device, ['deviceName', 'name']) }}</td>
              <td class="px-3 py-2">{{ readText(device, ['deviceCode', 'code']) }}</td>
              <td class="px-3 py-2">{{ readText(device, ['storeName', 'store']) }}</td>
              <td class="px-3 py-2">{{ readText(device, ['status', 'isActive']) }}</td>
            </tr>
            <tr v-if="!devices.length">
              <td class="px-3 py-6 text-center text-muted" colspan="4">No devices returned.</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { readText, type ApiRecord, useHrApiClient } from '../../utils/hr-api'

useHead({ title: 'Kiosk Devices - Garmetix HR' })

const { get } = useHrApiClient()
const loading = ref(false)
const error = ref('')
const devices = ref<ApiRecord[]>([])
const cards = computed(() => [
  { label: 'Devices', value: devices.value.length },
  { label: 'Active', value: devices.value.filter(item => ['true', 'active', 'enabled'].includes(readText(item, ['isActive', 'status'], '').toLowerCase())).length },
  { label: 'Bridge Ready', value: devices.value.filter(item => readText(item, ['bridgeStatus', 'status'], '').toLowerCase().includes('ready')).length }
])

async function load() {
  loading.value = true
  error.value = ''
  try {
    const response = await get<ApiRecord[]>('api/attendance/devices')
    devices.value = Array.isArray(response) ? response : []
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load attendance devices.'
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>
