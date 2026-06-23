<template>
  <section class="space-y-4">
    <div class="border border-default bg-muted/10 p-5">
      <div class="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <p class="text-sm text-muted">Attendance devices</p>
          <h2 class="mt-1 text-2xl font-semibold">Kiosk Devices</h2>
          <p class="mt-2 max-w-3xl text-sm text-muted">
            Device registration and revocation are intentionally left for the next HR stage. This page checks the current device list.
          </p>
        </div>
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="load">Refresh</UButton>
      </div>
    </div>

    <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="error" />

    <div class="grid gap-3 md:grid-cols-3">
      <div v-for="card in cards" :key="card.label" class="border border-default bg-muted/20 p-4">
        <p class="text-xs text-muted">{{ card.label }}</p>
        <p class="mt-1 text-xl font-semibold">{{ card.value }}</p>
      </div>
    </div>

    <div class="overflow-hidden border border-default bg-muted/10">
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
