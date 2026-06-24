<template>
  <section class="space-y-4" :aria-busy="loading">
    <div class="border border-default bg-muted/10 p-4">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="text-sm text-muted">Closing control</p>
          <h2 class="mt-1 text-2xl font-semibold">Day Close</h2>
          <p class="mt-2 max-w-2xl text-sm text-muted">
            Count physical cash, review petty cash book values, close the day, and print the petty cash sheet.
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton color="neutral" variant="soft" icon="i-lucide-refresh-cw" :loading="loading" @click="refresh">Refresh</UButton>
          <UButton icon="i-lucide-printer" color="neutral" variant="soft" :disabled="!pettyCashSheetId" @click="printPettyCash">Print Petty Cash</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="message" :color="messageTone" variant="subtle" :icon="messageIcon" :description="message" />
    <UAlert
      v-if="status?.bookSummary?.openingBalanceMismatch"
      color="warning"
      variant="subtle"
      icon="i-lucide-triangle-alert"
      :description="status.bookSummary.openingBalanceMismatchMessage"
    />

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
          <div class="grid gap-3 md:grid-cols-3">
            <UFormField label="Book closing cash">
              <UInput :model-value="money(status?.bookSummary?.cashInHand || 0)" readonly />
            </UFormField>
            <UFormField label="Physical cash from notes">
              <UInput :model-value="money(closingCashAmount)" readonly />
            </UFormField>
            <UFormField label="Difference">
              <UInput :model-value="money((closingCashAmount || pettyCashBookCash) - pettyCashBookCash)" readonly />
            </UFormField>
          </div>
          <div class="mt-4 grid gap-3 sm:grid-cols-2 lg:grid-cols-5">
            <UFormField v-for="note in cashDenominations" :key="note.key" :label="`Rs ${note.label}`">
              <UInput v-model="closingCash[note.key]" inputmode="numeric" />
            </UFormField>
          </div>
        </div>

        <div class="border border-default bg-muted/10 p-4">
          <div class="flex items-center justify-between gap-3">
            <div>
              <h3 class="font-semibold">Petty Cash Preview</h3>
              <p class="text-sm text-muted">Review calculated values before closing the day.</p>
            </div>
            <UBadge color="primary" variant="soft">{{ money(pettyCashBookCash) }}</UBadge>
          </div>
          <div class="mt-4 grid gap-3 md:grid-cols-3">
            <UFormField label="Opening"><UInput v-model="pettyCash.openingBalance" inputmode="decimal" /></UFormField>
            <UFormField label="Sales"><UInput v-model="pettyCash.sales" inputmode="decimal" /></UFormField>
            <UFormField label="Receipts"><UInput v-model="pettyCash.receipts" inputmode="decimal" /></UFormField>
            <UFormField label="Due receipts"><UInput v-model="pettyCash.dueReceipts" inputmode="decimal" /></UFormField>
            <UFormField label="Bank withdrawal"><UInput v-model="pettyCash.bankWithdrawal" inputmode="decimal" /></UFormField>
            <UFormField label="Expenses"><UInput v-model="pettyCash.expenses" inputmode="decimal" /></UFormField>
            <UFormField label="Payments"><UInput v-model="pettyCash.payments" inputmode="decimal" /></UFormField>
            <UFormField label="Customer due"><UInput v-model="pettyCash.customerDue" inputmode="decimal" /></UFormField>
            <UFormField label="Bank deposit"><UInput v-model="pettyCash.bankDeposit" inputmode="decimal" /></UFormField>
            <UFormField label="Non-cash sale"><UInput v-model="pettyCash.nonCashSale" inputmode="decimal" /></UFormField>
          </div>
        </div>
      </div>

      <aside class="space-y-4">
        <div class="border border-default bg-muted/10 p-4">
          <h3 class="text-base font-semibold">Close Store Day</h3>
          <p class="mt-2 text-sm text-muted">{{ status?.message || 'Load store day status before closing.' }}</p>
          <UCheckbox
            v-if="status?.bookSummary?.openingBalanceMismatch"
            v-model="confirmMismatch"
            class="mt-4"
            label="I checked and confirm opening balance mismatch"
          />
          <UFormField label="Closing remarks" class="mt-4">
            <UTextarea v-model="form.remarks" :rows="3" />
          </UFormField>
          <UButton class="mt-4" block icon="i-lucide-door-closed" :loading="loading" :disabled="!canClose" @click="closeDay">
            Confirm Close Day
          </UButton>
        </div>

        <div class="border border-default bg-muted/10 p-4">
          <h3 class="text-base font-semibold">Correction</h3>
          <div class="mt-4 grid gap-2">
            <UButton color="warning" variant="soft" icon="i-lucide-rotate-ccw" :loading="loading" :disabled="!status?.isClosed" @click="reopenDay">Reopen Day</UButton>
            <UButton color="error" variant="soft" icon="i-lucide-trash-2" :loading="loading" :disabled="!status?.isClosed" @click="deleteDayClose">Delete Close</UButton>
          </div>
        </div>

        <div class="border border-default bg-muted/10 p-4">
          <h3 class="text-base font-semibold">Book Notes</h3>
          <div class="mt-3 space-y-2 text-sm text-muted">
            <p v-for="note in status?.bookSummary?.notes || []" :key="note">{{ note }}</p>
            <p v-if="!status?.bookSummary?.notes?.length">No book notes loaded.</p>
          </div>
        </div>
      </aside>
    </section>
  </section>
</template>

<script setup lang="ts">
import { createApiUrl, createGarmetixApiClient } from '@garmetix/shared-api'
import { getStoredToken, getStoredUser } from '@garmetix/shared-auth'
import { formatIndianMoney } from '@garmetix/shared-utils'
import {
  calculateCash,
  calculatePettyCashBookCash,
  cashBlank,
  cashDenominations,
  copyBookSummaryToPettyCashDraft,
  localDateValue,
  pettyCashBlank,
  toCashPayload
} from '../utils/store-day'

useHead({ title: 'Day Close - Garmetix POS' })

const runtimeConfig = useRuntimeConfig()
const apiBaseUrl = computed(() => String(runtimeConfig.public.apiBaseUrl || ''))
const loading = ref(false)
const stores = ref<any[]>([])
const status = ref<any | null>(null)
const closingResult = ref<any | null>(null)
const closingCash = reactive(cashBlank())
const pettyCash = reactive(pettyCashBlank())
const confirmMismatch = ref(false)
const message = ref('')
const messageTone = ref<'success' | 'error' | 'warning' | 'neutral'>('neutral')
const messageIcon = computed(() => messageTone.value === 'success' ? 'i-lucide-circle-check' : messageTone.value === 'error' ? 'i-lucide-circle-alert' : messageTone.value === 'warning' ? 'i-lucide-triangle-alert' : 'i-lucide-info')
const form = reactive({
  storeId: '',
  onDate: localDateValue(),
  remarks: 'Day closed from POS app'
})

const api = computed(() => createGarmetixApiClient({
  baseUrl: apiBaseUrl.value,
  getToken: () => import.meta.client ? getStoredToken(window.localStorage) : null
}))
const storeOptions = computed(() => stores.value.map(store => ({
  value: store.id,
  label: store.name || store.storeName || 'Store'
})))
const closingCashAmount = computed(() => calculateCash(closingCash))
const pettyCashBookCash = computed(() => calculatePettyCashBookCash(pettyCash))
const pettyCashSheetId = computed(() => closingResult.value?.pettyCashSheetId || status.value?.pettyCashSheetId || '')
const canClose = computed(() => Boolean(status.value?.isOpened && !status.value?.isClosed && (!status.value?.bookSummary?.openingBalanceMismatch || confirmMismatch.value) && !loading.value))
const statusCards = computed(() => [
  {
    label: 'Opening',
    value: status.value?.isOpened ? 'Done' : 'Pending',
    detail: money(status.value?.openingBalance || 0)
  },
  {
    label: 'Closing',
    value: status.value?.isClosed ? 'Done' : 'Pending',
    detail: money(status.value?.physicalClosingBalance || 0)
  },
  {
    label: 'Book Cash',
    value: money(status.value?.bookSummary?.cashInHand || 0),
    detail: 'Calculated from books'
  },
  {
    label: 'Difference',
    value: money(status.value?.difference || 0),
    detail: 'Physical minus book'
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
    showMessage('warning', 'Login is required before closing a store day.')
    return
  }
  if (!stores.value.length) await loadStores()
  if (!form.storeId) return

  loading.value = true
  closingResult.value = null
  try {
    status.value = await api.value.get<any>(`store-day/status?storeId=${form.storeId}&onDate=${form.onDate}`)
    copyBookSummaryToPettyCashDraft(pettyCash, status.value?.bookSummary || {})
    confirmMismatch.value = false
    message.value = ''
  } catch (error) {
    showMessage('error', error instanceof Error ? error.message : 'Could not load store day status.')
  } finally {
    loading.value = false
  }
}

async function closeDay() {
  if (loading.value) return
  if (!form.storeId) {
    showMessage('warning', 'Select store before closing day.')
    return
  }
  loading.value = true
  try {
    closingResult.value = await api.value.post<any>('store-day/close', {
      storeId: form.storeId,
      onDate: form.onDate,
      cashDetail: toCashPayload(closingCash, closingCashAmount.value || pettyCashBookCash.value || status.value?.bookSummary?.cashInHand),
      useBookCashIfNoCashDetail: true,
      confirmOpeningBalanceMismatch: confirmMismatch.value,
      pettyCashSheet: { ...pettyCash },
      remarks: form.remarks
    })
    status.value = closingResult.value?.status || status.value
    showMessage('success', 'Store day closed and petty cash sheet updated.')
  } catch (error: any) {
    const payload = parseErrorPayload(error)
    if (payload?.requiresConfirmation || payload?.summary?.openingBalanceMismatch) {
      if (payload.summary) copyBookSummaryToPettyCashDraft(pettyCash, payload.summary)
      confirmMismatch.value = true
      showMessage('warning', payload.message || 'Opening balance differs from previous petty cash closing. Confirm and close again.')
      return
    }
    showMessage('error', error instanceof Error ? error.message : 'Store day closing failed.')
  } finally {
    loading.value = false
  }
}

async function reopenDay() {
  await correction('store-day/reopen', 'Store day reopened.')
}

async function deleteDayClose() {
  await correction('store-day/delete-close', 'Store day close deleted.')
}

async function correction(path: string, successMessage: string) {
  if (loading.value) return
  if (!form.storeId) return
  loading.value = true
  try {
    status.value = await api.value.post<any>(path, {
      storeId: form.storeId,
      onDate: form.onDate,
      reason: 'Correction from POS app'
    })
    closingResult.value = null
    showMessage('success', successMessage)
  } catch (error) {
    showMessage('error', error instanceof Error ? error.message : 'Correction failed.')
  } finally {
    loading.value = false
  }
}

async function printPettyCash() {
  if (loading.value) return
  if (!pettyCashSheetId.value) {
    showMessage('warning', 'Close the day before printing petty cash.')
    return
  }
  const token = getStoredToken(window.localStorage)
  const response = await fetch(createApiUrl(apiBaseUrl.value, `petty-cash-sheets/${pettyCashSheetId.value}/pdf`), {
    headers: token ? { Authorization: `Bearer ${token}` } : undefined
  })
  if (!response.ok) {
    showMessage('error', 'Could not open petty cash PDF.')
    return
  }
  const blob = await response.blob()
  window.open(URL.createObjectURL(blob), '_blank', 'noopener,noreferrer')
}

function parseErrorPayload(error: any) {
  try {
    const message = error instanceof Error ? error.message : ''
    const start = message.indexOf('{')
    if (start >= 0) return JSON.parse(message.slice(start))
  } catch {
    return null
  }
  return null
}

onMounted(() => {
  void refresh()
})
</script>
