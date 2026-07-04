<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-fingerprint" class="size-4" /> Attendance devices</p>
          <h2 class="garmetix-dashboard-title">Kiosk Devices</h2>
          <p class="garmetix-dashboard-subtitle">
            Register kiosk devices, copy the one-time token, and revoke devices that should no longer punch attendance.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="load">Refresh</UButton>
          <UButton icon="i-lucide-tablet-smartphone" color="primary" variant="soft" to="/attendance/kiosk">Kiosk Check</UButton>
        </div>
      </div>
    </div>

    <UAlert
      v-if="registrationToken"
      color="success"
      variant="subtle"
      icon="i-lucide-key-round"
      title="Copy the device token now"
      :description="`Device ${readText(registrationResult, ['deviceCode'])} was registered. Token: ${registrationToken}`"
    />
    <UAlert v-if="message" :color="messageTone" variant="subtle" :icon="messageIcon" :description="message" />

    <div class="grid gap-3 md:grid-cols-4">
      <div v-for="card in cards" :key="card.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label">{{ card.label }}</p>
        <p class="garmetix-metric-value">{{ card.value }}</p>
      </div>
    </div>

    <div class="grid gap-4 xl:grid-cols-[380px_minmax(0,1fr)]">
      <div class="garmetix-table-panel">
        <div class="garmetix-panel-header">
          <div>
            <h3 class="garmetix-panel-title">Register Device</h3>
            <p class="garmetix-panel-subtitle">Requires store scope and typed confirmation.</p>
          </div>
        </div>
        <div class="grid gap-3">
          <UFormField label="Store">
            <USelect v-model="form.storeId" :items="storeOptions" placeholder="Select store" />
          </UFormField>
          <UFormField label="Device Name">
            <UInput v-model="form.deviceName" placeholder="Front counter tablet" />
          </UFormField>
          <div class="grid gap-3 sm:grid-cols-2">
            <UFormField label="Device Type">
              <USelect v-model="form.deviceType" :items="deviceTypeOptions" />
            </UFormField>
            <UFormField label="App Version">
              <UInput v-model="form.appVersion" placeholder="6.0.14" />
            </UFormField>
          </div>
          <UFormField label="Notes">
            <UInput v-model="form.notes" placeholder="Counter, tablet serial, operator note" />
          </UFormField>
          <UFormField label="Type REGISTER DEVICE to enable">
            <UInput v-model="registerConfirm" placeholder="REGISTER DEVICE" />
          </UFormField>
          <UButton icon="i-lucide-plus-circle" :disabled="!canRegister" :loading="registering" @click="registerDevice">Register Device</UButton>
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
          <table class="w-full min-w-[920px] text-left text-sm">
            <thead class="bg-muted/30 text-xs uppercase text-muted">
              <tr>
                <th class="px-3 py-2">Device</th>
                <th class="px-3 py-2">Code</th>
                <th class="px-3 py-2">Type</th>
                <th class="px-3 py-2">Status</th>
                <th class="px-3 py-2">Last Seen</th>
                <th class="px-3 py-2">Store</th>
                <th class="px-3 py-2 text-right">Action</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(device, index) in devices" :key="readText(device, ['id', 'deviceCode'], String(index))" class="border-t border-default">
                <td class="px-3 py-2 font-medium">{{ readText(device, ['deviceName', 'name']) }}</td>
                <td class="px-3 py-2">{{ readText(device, ['deviceCode', 'code']) }}</td>
                <td class="px-3 py-2">{{ readText(device, ['deviceType']) }}</td>
                <td class="px-3 py-2">
                  <UBadge :color="deviceStatusTone(device)" variant="subtle">{{ readText(device, ['status', 'isActive']) }}</UBadge>
                </td>
                <td class="px-3 py-2">{{ readText(device, ['lastSeenAtUtc']) }}</td>
                <td class="px-3 py-2">{{ storeNameFor(readText(device, ['storeId'], '')) }}</td>
                <td class="px-3 py-2 text-right">
                  <UButton
                    size="xs"
                    color="error"
                    variant="soft"
                    icon="i-lucide-ban"
                    :disabled="isRevoked(device)"
                    :loading="revokingId === readText(device, ['id'], '')"
                    @click="prepareRevoke(device)"
                  >
                    Revoke
                  </UButton>
                </td>
              </tr>
              <tr v-if="!devices.length">
                <td class="px-3 py-6 text-center text-muted" colspan="7">No devices returned.</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>

    <UModal v-model:open="revokeOpen">
      <template #content>
        <div class="grid gap-4 p-5">
          <div>
            <h3 class="text-base font-semibold">Revoke Device</h3>
            <p class="mt-1 text-sm text-muted">
              {{ readText(revokeTarget, ['deviceName']) }} / {{ readText(revokeTarget, ['deviceCode']) }}
            </p>
          </div>
          <UAlert color="warning" variant="subtle" icon="i-lucide-shield-alert" description="Revoked devices cannot punch attendance with the old token." />
          <UFormField label="Type REVOKE DEVICE to confirm">
            <UInput v-model="revokeConfirm" placeholder="REVOKE DEVICE" />
          </UFormField>
          <div class="flex justify-end gap-2">
            <UButton color="neutral" variant="soft" @click="revokeOpen = false">Cancel</UButton>
            <UButton color="error" icon="i-lucide-ban" :disabled="!canRevoke" :loading="Boolean(revokingId)" @click="revokeDevice">Revoke</UButton>
          </div>
        </div>
      </template>
    </UModal>
  </section>
</template>

<script setup lang="ts">
import { readBoolean, readText, type ApiRecord, useHrApiClient } from '../../utils/hr-api'

useHead({ title: 'Kiosk Devices - Garmetix HR' })

const { get, post } = useHrApiClient()
const loading = ref(false)
const registering = ref(false)
const revokingId = ref('')
const message = ref('')
const messageTone = ref<'info' | 'success' | 'warning' | 'error'>('info')
const devices = ref<ApiRecord[]>([])
const stores = ref<ApiRecord[]>([])
const registrationResult = ref<ApiRecord | null>(null)
const registerConfirm = ref('')
const revokeOpen = ref(false)
const revokeTarget = ref<ApiRecord | null>(null)
const revokeConfirm = ref('')
const form = reactive({
  storeId: '',
  deviceName: '',
  deviceType: 'WebKiosk',
  appVersion: '6.0.14',
  notes: ''
})

const deviceTypeOptions = [
  { value: 'WebKiosk', label: 'Web Kiosk' },
  { value: 'MauiAndroidKiosk', label: 'MAUI Android Kiosk' },
  { value: 'FingerprintBridge', label: 'Fingerprint Bridge' }
]
const storeOptions = computed(() => stores.value.map(store => ({
  value: readText(store, ['id'], ''),
  label: `${readText(store, ['storeName', 'name'])} | ${readText(store, ['storeCode', 'code'])}`.replace(/\s+\|\s+-$/, '')
})))
const selectedStore = computed(() => stores.value.find(store => readText(store, ['id'], '') === form.storeId) ?? null)
const registrationToken = computed(() => readText(registrationResult.value, ['deviceToken'], ''))
const canRegister = computed(() => Boolean(form.storeId && form.deviceName.trim() && registerConfirm.value.trim().toUpperCase() === 'REGISTER DEVICE') && !registering.value)
const canRevoke = computed(() => Boolean(revokeTarget.value && revokeConfirm.value.trim().toUpperCase() === 'REVOKE DEVICE' && !revokingId.value))
const messageIcon = computed(() => messageTone.value === 'success' ? 'i-lucide-circle-check' : messageTone.value === 'warning' ? 'i-lucide-triangle-alert' : messageTone.value === 'error' ? 'i-lucide-circle-alert' : 'i-lucide-info')
const cards = computed(() => [
  { label: 'Devices', value: devices.value.length },
  { label: 'Active', value: devices.value.filter(item => ['true', 'active', 'enabled'].includes(readText(item, ['isActive', 'status'], '').toLowerCase())).length },
  { label: 'Revoked', value: devices.value.filter(isRevoked).length },
  { label: 'Stores', value: stores.value.length }
])

function storeNameFor(storeId: string) {
  const store = stores.value.find(item => readText(item, ['id'], '') === storeId)
  return store ? readText(store, ['storeName', 'name']) : storeId || '-'
}

function isRevoked(device: ApiRecord) {
  const status = readText(device, ['status'], '').toLowerCase()
  return status.includes('revoked') || readBoolean(device, ['revoked']) || readText(device, ['revokedAtUtc'], '') !== '-'
}

function deviceStatusTone(device: ApiRecord) {
  if (isRevoked(device)) return 'error'
  const status = readText(device, ['status'], '').toLowerCase()
  if (status.includes('active')) return 'success'
  return 'neutral'
}

function prepareRevoke(device: ApiRecord) {
  revokeTarget.value = device
  revokeConfirm.value = ''
  revokeOpen.value = true
}

async function load() {
  loading.value = true
  message.value = ''
  try {
    const [deviceResponse, storeResponse] = await Promise.all([
      get<ApiRecord[]>('api/attendance/devices'),
      get<ApiRecord[]>('api/stores')
    ])
    devices.value = Array.isArray(deviceResponse) ? deviceResponse : []
    stores.value = Array.isArray(storeResponse) ? storeResponse : []
    if (!form.storeId && stores.value.length) form.storeId = readText(stores.value[0], ['id'], '')
  } catch (caught) {
    messageTone.value = 'error'
    message.value = caught instanceof Error ? caught.message : 'Unable to load attendance devices.'
  } finally {
    loading.value = false
  }
}

async function registerDevice() {
  if (!canRegister.value || !selectedStore.value) return
  registering.value = true
  message.value = ''
  registrationResult.value = null
  try {
    const response = await post<ApiRecord>('api/attendance/devices/register', {
      deviceName: form.deviceName,
      deviceType: form.deviceType,
      appVersion: form.appVersion || null,
      notes: form.notes || null,
      companyId: readText(selectedStore.value, ['companyId'], ''),
      storeGroupId: readText(selectedStore.value, ['storeGroupId'], ''),
      storeId: form.storeId
    })
    registrationResult.value = response
    messageTone.value = 'success'
    message.value = `Device ${readText(response, ['deviceCode'])} registered. Copy the token now.`
    registerConfirm.value = ''
    form.deviceName = ''
    form.notes = ''
    await load()
  } catch (caught) {
    messageTone.value = 'error'
    message.value = caught instanceof Error ? caught.message : 'Unable to register device.'
  } finally {
    registering.value = false
  }
}

async function revokeDevice() {
  const id = readText(revokeTarget.value, ['id'], '')
  if (!id || !canRevoke.value) return
  revokingId.value = id
  message.value = ''
  try {
    await post<ApiRecord>(`api/attendance/devices/${id}/revoke`, {})
    messageTone.value = 'success'
    message.value = 'Device revoked.'
    revokeOpen.value = false
    await load()
  } catch (caught) {
    messageTone.value = 'error'
    message.value = caught instanceof Error ? caught.message : 'Unable to revoke device.'
  } finally {
    revokingId.value = ''
  }
}

onMounted(load)
</script>
