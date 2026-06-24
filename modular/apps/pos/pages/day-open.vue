<template>
  <section class="space-y-4" :aria-busy="loading">
    <div class="border border-default bg-muted/10 p-4">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="text-sm text-muted">Opening control</p>
          <h2 class="mt-1 text-2xl font-semibold">Day Open</h2>
          <p class="mt-2 max-w-2xl text-sm text-muted">
            Open the working store day before billing. Opening cash is saved with denomination details.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton color="neutral" variant="soft" icon="i-lucide-refresh-cw" :loading="loading" @click="refresh">Refresh</UButton>
          <UButton to="/sale" icon="i-lucide-scan-barcode" :disabled="!status?.entryAllowed">Go to Sale</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="message" :color="messageTone" variant="subtle" :icon="messageIcon" :description="message" />

    <section class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
      <div v-for="card in statusCards" :key="card.label" class="border border-default bg-muted/10 p-4">
        <p class="text-sm text-muted">{{ card.label }}</p>
        <p class="mt-2 text-lg font-semibold">{{ card.value }}</p>
        <p class="mt-1 text-xs text-muted">{{ card.detail }}</p>
      </div>
    </section>

    <section class="grid gap-4 xl:grid-cols-[minmax(0,1fr)_360px]">
      <div class="space-y-4">
        <div class="grid gap-3 border border-default bg-muted/10 p-4 md:grid-cols-2">
          <UFormField label="Working store">
            <USelect v-model="form.storeId" :items="storeOptions" placeholder="Select store" @change="refresh" />
          </UFormField>
          <UFormField label="Date">
            <UInput v-model="form.onDate" type="date" @change="refresh" />
          </UFormField>
        </div>

        <div class="border border-default bg-muted/10 p-4">
          <div class="grid gap-3 md:grid-cols-2">
            <UFormField label="Opening balance">
              <UInput v-model="form.openingBalance" inputmode="decimal" />
            </UFormField>
            <UFormField label="Cash from notes">
              <UInput :model-value="money(openingCashAmount)" readonly />
            </UFormField>
          </div>
          <div class="mt-4 grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
            <UFormField v-for="note in cashDenominations" :key="note.key" :label="`Rs ${note.label}`">
              <UInput v-model="openingCash[note.key]" inputmode="numeric" />
            </UFormField>
          </div>
          <UFormField label="Opening remarks" class="mt-4">
            <UTextarea v-model="form.remarks" :rows="3" />
          </UFormField>
        </div>
      </div>

      <aside class="space-y-4">
        <div class="border border-default bg-muted/10 p-4">
          <h3 class="text-base font-semibold">Open Store Day</h3>
          <p class="mt-2 text-sm text-muted">{{ status?.message || 'Select store and date to load status.' }}</p>
          <dl class="mt-4 space-y-2 text-sm">
            <div class="flex justify-between gap-3"><dt class="text-muted">Previous closing</dt><dd>{{ money(status?.bookSummary?.previousPettyCashClosingBalance || 0) }}</dd></div>
            <div class="flex justify-between gap-3"><dt class="text-muted">Opening source</dt><dd class="text-right">{{ status?.bookSummary?.openingBalanceSource || '-' }}</dd></div>
            <div class="flex justify-between gap-3 text-base font-semibold"><dt>Opening</dt><dd>{{ money(Number(form.openingBalance || 0)) }}</dd></div>
          </dl>
          <UButton class="mt-4" block color="success" icon="i-lucide-door-open" :loading="loading" :disabled="!canOpen" @click="openDay">
            Open / Update Day
          </UButton>
        </div>

        <div class="border border-default bg-muted/10 p-4">
          <h3 class="text-base font-semibold">Holiday / Closed Day</h3>
          <UFormField label="Reason" class="mt-4">
            <UTextarea v-model="holidayReason" :rows="3" />
          </UFormField>
          <UButton class="mt-4" block color="warning" variant="soft" icon="i-lucide-calendar-x-2" :loading="loading" :disabled="!form.storeId" @click="markHoliday">
            Mark Holiday / Closed
          </UButton>
        </div>
      </aside>
    </section>
  </section>
</template>

<script setup lang="ts">
import { createGarmetixApiClient } from '@garmetix/shared-api'
import { getStoredToken, getStoredUser } from '@garmetix/shared-auth'
import { formatIndianMoney } from '@garmetix/shared-utils'
import { calculateCash, cashBlank, cashDenominations, localDateValue, toCashPayload } from '../utils/store-day'

useHead({ title: 'Day Open - Garmetix POS' })

const runtimeConfig = useRuntimeConfig()
const apiBaseUrl = computed(() => String(runtimeConfig.public.apiBaseUrl || ''))
const loading = ref(false)
const stores = ref<any[]>([])
const status = ref<any | null>(null)
const openingCash = reactive(cashBlank())
const holidayReason = ref('Store closed / holiday')
const message = ref('')
const messageTone = ref<'success' | 'error' | 'warning' | 'neutral'>('neutral')
const messageIcon = computed(() => messageTone.value === 'success' ? 'i-lucide-circle-check' : messageTone.value === 'error' ? 'i-lucide-circle-alert' : messageTone.value === 'warning' ? 'i-lucide-triangle-alert' : 'i-lucide-info')
const form = reactive({
  storeId: '',
  onDate: localDateValue(),
  openingBalance: 0,
  remarks: 'Day opened from POS app'
})

const api = computed(() => createGarmetixApiClient({
  baseUrl: apiBaseUrl.value,
  getToken: () => import.meta.client ? getStoredToken(window.localStorage) : null
}))
const storeOptions = computed(() => stores.value.map(store => ({
  value: store.id,
  label: store.name || store.storeName || 'Store'
})))
const openingCashAmount = computed(() => calculateCash(openingCash))
const canOpen = computed(() => Boolean(form.storeId && !status.value?.isClosed && !loading.value))
const statusCards = computed(() => [
  {
    label: 'Opening',
    value: status.value?.isOpened ? 'Done' : 'Pending',
    detail: money(status.value?.openingBalance || form.openingBalance || 0)
  },
  {
    label: 'Closing',
    value: status.value?.isClosed ? 'Done' : 'Pending',
    detail: money(status.value?.physicalClosingBalance || 0)
  },
  {
    label: 'Entry Status',
    value: status.value?.entryAllowed ? 'Allowed' : 'Blocked',
    detail: status.value?.message || 'Status pending'
  },
  {
    label: 'Book Cash',
    value: money(status.value?.bookSummary?.cashInHand || 0),
    detail: 'Calculated cash in hand'
  }
])

function money(value: number | string | null | undefined) {
  return formatIndianMoney(value)
}

function showMessage(tone: typeof messageTone.value, text: string) {
  messageTone.value = tone
  message.value = text
}

async function loadStores() {
  const storedUser = getStoredUser(window.localStorage)
  stores.value = await api.value.get<any[]>('stores')
  form.storeId = storedUser?.storeId || form.storeId || stores.value[0]?.id || ''
}

async function refresh() {
  if (!import.meta.client) return
  const token = getStoredToken(window.localStorage)
  if (!token) {
    showMessage('warning', 'Login is required before opening a store day.')
    return
  }
  if (!stores.value.length) await loadStores()
  if (!form.storeId) return

  loading.value = true
  try {
    status.value = await api.value.get<any>(`store-day/status?storeId=${form.storeId}&onDate=${form.onDate}`)
    form.openingBalance = Number(status.value?.openingBalance ?? status.value?.bookSummary?.openingBalance ?? form.openingBalance ?? 0)
    message.value = ''
  } catch (error) {
    showMessage('error', error instanceof Error ? error.message : 'Could not load store day status.')
  } finally {
    loading.value = false
  }
}

async function openDay() {
  if (loading.value) return
  if (!form.storeId) {
    showMessage('warning', 'Select store before opening day.')
    return
  }
  loading.value = true
  try {
    status.value = await api.value.post<any>('store-day/open', {
      storeId: form.storeId,
      onDate: form.onDate,
      openingBalance: Number(form.openingBalance || 0),
      cashDetail: toCashPayload(openingCash, Number(form.openingBalance || 0)),
      remarks: form.remarks
    })
    form.openingBalance = Number(status.value?.openingBalance || form.openingBalance || 0)
    showMessage('success', 'Store day opened.')
  } catch (error) {
    showMessage('error', error instanceof Error ? error.message : 'Store day opening failed.')
  } finally {
    loading.value = false
  }
}

async function markHoliday() {
  if (loading.value) return
  if (!form.storeId) {
    showMessage('warning', 'Select store before marking holiday.')
    return
  }
  loading.value = true
  try {
    status.value = await api.value.post<any>('store-day/holiday', {
      storeId: form.storeId,
      onDate: form.onDate,
      reason: holidayReason.value
    })
    showMessage('success', 'Store holiday/closed day marked.')
  } catch (error) {
    showMessage('error', error instanceof Error ? error.message : 'Holiday marking failed.')
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  void refresh()
})
</script>
