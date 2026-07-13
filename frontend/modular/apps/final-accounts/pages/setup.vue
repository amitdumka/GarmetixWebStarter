<template>
  <section class="space-y-5">
    <div class="flex flex-wrap items-start justify-between gap-3">
      <div>
        <p class="garmetix-kicker"><UIcon name="i-lucide-sliders-horizontal" class="size-4" /> Final Accounts</p>
        <h1 class="garmetix-dashboard-title">Setup</h1>
      </div>
      <UButton to="/" icon="i-lucide-arrow-left" color="neutral" variant="soft">Status</UButton>
    </div>

    <UAlert v-if="message" :icon="messageIcon" :color="messageTone" variant="subtle" :title="messageTitle" :description="message" />

    <UCard :ui="{ body: 'p-5' }">
      <form class="grid gap-4 lg:grid-cols-2" @submit.prevent="save">
        <UFormField label="Module status" class="lg:col-span-2">
          <USwitch v-model="form.enabled" label="Enabled" />
        </UFormField>

        <UFormField label="Company ID" name="companyId">
          <UInput v-model="form.companyId" icon="i-lucide-building-2" placeholder="Global" />
        </UFormField>

        <UFormField label="Store group ID" name="storeGroupId">
          <UInput v-model="form.storeGroupId" icon="i-lucide-network" placeholder="Global" />
        </UFormField>

        <UFormField label="Store ID" name="storeId">
          <UInput v-model="form.storeId" icon="i-lucide-store" placeholder="Global" />
        </UFormField>

        <UFormField label="Posting mode" name="postingMode">
          <USelect v-model="form.postingMode" :items="postingModes" icon="i-lucide-git-branch" />
        </UFormField>

        <UFormField label="Statement template" name="statementTemplate">
          <UInput v-model="form.statementTemplate" icon="i-lucide-file-spreadsheet" />
        </UFormField>

        <UFormField label="Inventory valuation" name="inventoryValuationMethod">
          <USelect v-model="form.inventoryValuationMethod" :items="valuationMethods" icon="i-lucide-scale" />
        </UFormField>

        <UFormField label="Rounding scale" name="roundingScale">
          <UInput v-model.number="form.roundingScale" type="number" min="0" max="6" icon="i-lucide-decimals-arrow-right" />
        </UFormField>

        <div class="grid gap-3 lg:col-span-2 sm:grid-cols-2">
          <USwitch v-model="form.allowHistoricalBackfill" label="Historical backfill" />
          <USwitch v-model="form.allowTallyExport" label="Tally export" />
          <USwitch v-model="form.allowProjections" label="Projections" />
          <USwitch v-model="form.allowPeriodReopen" label="Period reopen" />
        </div>

        <div class="flex flex-wrap gap-2 lg:col-span-2">
          <UButton type="submit" icon="i-lucide-save" :loading="saving">Save</UButton>
          <UButton type="button" icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="load">Reload</UButton>
        </div>
      </form>
    </UCard>
  </section>
</template>

<script setup lang="ts">
import { type FinalAccountsSaveSettings, type FinalAccountsSettings, useFinalAccountsApiClient } from '../utils/final-accounts-api'

useHead({ title: 'Final Accounts Setup' })

const api = useFinalAccountsApiClient()
const loading = ref(false)
const saving = ref(false)
const message = ref('')
const messageTone = ref<'success' | 'error' | 'neutral'>('neutral')
const messageTitle = computed(() => messageTone.value === 'success' ? 'Saved' : messageTone.value === 'error' ? 'Setup unavailable' : 'Setup')
const messageIcon = computed(() => messageTone.value === 'success' ? 'i-lucide-circle-check' : messageTone.value === 'error' ? 'i-lucide-circle-alert' : 'i-lucide-info')
const postingModes = ['ManualSync', 'DraftOnly', 'AutoPost']
const valuationMethods = ['WeightedAverage', 'FIFO', 'ManualClosing']
const form = reactive<FinalAccountsSaveSettings>({
  enabled: false,
  companyId: '',
  storeGroupId: '',
  storeId: '',
  postingMode: 'ManualSync',
  statementTemplate: 'GarmentRetail.v1',
  inventoryValuationMethod: 'WeightedAverage',
  roundingScale: 2,
  allowHistoricalBackfill: false,
  allowTallyExport: false,
  allowProjections: false,
  allowPeriodReopen: false
})

async function load() {
  loading.value = true
  message.value = ''
  try {
    applySettings(await api.get<FinalAccountsSettings>('settings'))
  } catch (err) {
    messageTone.value = 'error'
    message.value = err instanceof Error ? err.message : 'Settings could not be loaded.'
  } finally {
    loading.value = false
  }
}

async function save() {
  saving.value = true
  message.value = ''
  try {
    applySettings(await api.put<FinalAccountsSettings>('settings', normalizePayload()))
    messageTone.value = 'success'
    message.value = 'Final Accounts setup saved.'
  } catch (err) {
    messageTone.value = 'error'
    message.value = err instanceof Error ? err.message : 'Settings could not be saved.'
  } finally {
    saving.value = false
  }
}

function applySettings(settings: FinalAccountsSettings) {
  form.enabled = settings.enabled
  form.companyId = settings.companyId || ''
  form.storeGroupId = settings.storeGroupId || ''
  form.storeId = settings.storeId || ''
  form.postingMode = settings.postingMode || 'ManualSync'
  form.statementTemplate = settings.statementTemplate || 'GarmentRetail.v1'
  form.inventoryValuationMethod = settings.inventoryValuationMethod || 'WeightedAverage'
  form.roundingScale = settings.roundingScale ?? 2
  form.allowHistoricalBackfill = settings.allowHistoricalBackfill
  form.allowTallyExport = settings.allowTallyExport
  form.allowProjections = settings.allowProjections
  form.allowPeriodReopen = settings.allowPeriodReopen
}

function normalizePayload(): FinalAccountsSaveSettings {
  return {
    ...form,
    companyId: blankToNull(form.companyId),
    storeGroupId: blankToNull(form.storeGroupId),
    storeId: blankToNull(form.storeId),
    roundingScale: Math.min(Math.max(Number(form.roundingScale) || 0, 0), 6)
  }
}

function blankToNull(value: string | null | undefined) {
  const trimmed = String(value || '').trim()
  return trimmed ? trimmed : null
}

onMounted(load)
</script>
