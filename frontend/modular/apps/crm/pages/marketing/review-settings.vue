<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-star" class="size-4" /> Digital CRM setup</p>
          <h2 class="garmetix-dashboard-title">Review Settings</h2>
          <p class="garmetix-dashboard-subtitle">Store-level Google review, social links, WhatsApp support and private feedback settings for public invoices.</p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton color="neutral" variant="soft" icon="i-lucide-refresh-cw" :loading="loading" @click="refresh">Refresh</UButton>
          <UButton icon="i-lucide-save" :loading="saving" :disabled="!selectedStoreId" @click="save">Save Settings</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="error" variant="subtle" icon="i-lucide-circle-alert" :description="error" />

    <div class="garmetix-section-card grid gap-3 lg:grid-cols-3">
      <UFormField label="Company">
        <USelect v-model="selectedCompanyId" :items="companyOptions" @update:model-value="onCompanyChanged" />
      </UFormField>
      <UFormField label="Store">
        <USelect v-model="selectedStoreId" :items="storeOptions" placeholder="Select store" @update:model-value="loadSelectedSetting" />
      </UFormField>
      <div class="flex items-end">
        <UBadge :color="selectedSetting ? 'success' : 'neutral'" variant="subtle">{{ selectedSetting ? 'Configured' : 'New setting' }}</UBadge>
      </div>
    </div>

    <section class="grid gap-4 xl:grid-cols-[minmax(0,1fr)_minmax(320px,0.7fr)]">
      <div class="garmetix-section-card">
        <div class="mb-4">
          <p class="garmetix-kicker"><UIcon name="i-lucide-link" class="size-4" /> Public links</p>
          <h3 class="text-xl font-semibold text-highlighted">{{ selectedStoreName }}</h3>
        </div>

        <div class="grid gap-4 lg:grid-cols-2">
          <UFormField label="Google review URL">
            <UInput v-model="form.googleReviewUrl" placeholder="https://g.page/r/..." />
          </UFormField>
          <UFormField label="Instagram URL">
            <UInput v-model="form.instagramUrl" placeholder="https://instagram.com/..." />
          </UFormField>
          <UFormField label="Facebook URL">
            <UInput v-model="form.facebookUrl" placeholder="https://facebook.com/..." />
          </UFormField>
          <UFormField label="WhatsApp support number">
            <UInput v-model="form.whatsAppSupportNumber" placeholder="919999999999" />
          </UFormField>
          <UFormField label="Review button text">
            <UInput v-model="form.reviewButtonText" />
          </UFormField>
          <UFormField label="Feedback button text">
            <UInput v-model="form.feedbackButtonText" />
          </UFormField>
        </div>

        <div class="mt-5 grid gap-3 md:grid-cols-2">
          <div class="rounded-md border border-default p-3"><UCheckbox v-model="form.enableGoogleReview" label="Show Google review button" /></div>
          <div class="rounded-md border border-default p-3"><UCheckbox v-model="form.enablePrivateFeedback" label="Allow private feedback" /></div>
          <div class="rounded-md border border-default p-3"><UCheckbox v-model="form.enableInstagram" label="Show Instagram link" /></div>
          <div class="rounded-md border border-default p-3"><UCheckbox v-model="form.enableFacebook" label="Show Facebook link" /></div>
          <div class="rounded-md border border-default p-3 md:col-span-2"><UCheckbox v-model="form.enableWhatsappSupport" label="Show WhatsApp support button" /></div>
        </div>

        <div class="mt-5 flex justify-end">
          <UButton icon="i-lucide-save" :loading="saving" :disabled="!selectedStoreId" @click="save">Save Settings</UButton>
        </div>
      </div>

      <div class="garmetix-section-card">
        <p class="garmetix-kicker"><UIcon name="i-lucide-store" class="size-4" /> Configured stores</p>
        <h3 class="text-xl font-semibold text-highlighted">Review Coverage</h3>
        <div class="mt-4 grid gap-3">
          <button
            v-for="setting in settings"
            :key="String(setting.id || setting.storeId)"
            type="button"
            class="rounded-md border border-default p-3 text-left hover:bg-muted/30"
            @click="selectSetting(setting)"
          >
            <div class="flex items-start justify-between gap-3">
              <div>
                <p class="font-semibold text-highlighted">{{ storeName(readText(setting, ['storeId'], '')) }}</p>
                <p class="text-xs text-muted">{{ enabledSummary(setting) }}</p>
              </div>
              <UBadge :color="readText(setting, ['googleReviewUrl'], '') ? 'success' : 'neutral'" variant="subtle">Google</UBadge>
            </div>
          </button>
          <p v-if="!settings.length" class="text-sm text-muted">No review settings configured yet.</p>
        </div>
      </div>
    </section>
  </section>
</template>

<script setup lang="ts">
import { stripServerUrl } from '@garmetix/shared-utils'
import { readId, readText, toRows, type ApiRecord, useCrmApiClient } from '../../utils/crm-api'

const toast = useToast()
const { get, put } = useCrmApiClient()

const companies = ref<ApiRecord[]>([])
const stores = ref<ApiRecord[]>([])
const settings = ref<ApiRecord[]>([])
const selectedCompanyId = ref('')
const selectedStoreId = ref('')
const loading = ref(false)
const saving = ref(false)
const error = ref('')

const form = reactive({
  googleReviewUrl: '',
  instagramUrl: '',
  facebookUrl: '',
  whatsAppSupportNumber: '',
  enableGoogleReview: true,
  enableInstagram: false,
  enableFacebook: false,
  enableWhatsappSupport: false,
  enablePrivateFeedback: true,
  reviewButtonText: 'Share your honest Google review',
  feedbackButtonText: 'Share private feedback'
})

const companyOptions = computed(() => companies.value.map(row => ({ label: readText(row, ['name', 'companyName'], 'Company'), value: readId(row) })))
const visibleStores = computed(() => stores.value.filter(row => !selectedCompanyId.value || readText(row, ['companyId'], '') === selectedCompanyId.value))
const storeOptions = computed(() => visibleStores.value.map(row => ({ label: readText(row, ['name', 'storeName'], 'Store'), value: readId(row) })))
const selectedStore = computed(() => stores.value.find(row => readId(row) === selectedStoreId.value) ?? null)
const selectedSetting = computed(() => settings.value.find(row => readText(row, ['storeId'], '') === selectedStoreId.value) ?? null)
const selectedStoreName = computed(() => selectedStore.value ? readText(selectedStore.value, ['name', 'storeName'], 'Store') : 'Select a store')

function storeName(storeId: string) {
  const store = stores.value.find(row => readId(row) === storeId)
  return store ? readText(store, ['name', 'storeName'], 'Store') : storeId || 'Store'
}

function enabledSummary(row: ApiRecord) {
  const labels = [
    row.enableGoogleReview ? 'Google' : '',
    row.enablePrivateFeedback ? 'Feedback' : '',
    row.enableWhatsappSupport ? 'WhatsApp' : '',
    row.enableInstagram ? 'Instagram' : '',
    row.enableFacebook ? 'Facebook' : ''
  ].filter(Boolean)
  return labels.length ? labels.join(', ') : 'All public buttons disabled'
}

function resetForm(row?: ApiRecord | null) {
  Object.assign(form, {
    googleReviewUrl: readText(row, ['googleReviewUrl'], ''),
    instagramUrl: readText(row, ['instagramUrl'], ''),
    facebookUrl: readText(row, ['facebookUrl'], ''),
    whatsAppSupportNumber: readText(row, ['whatsAppSupportNumber'], ''),
    enableGoogleReview: row?.enableGoogleReview === undefined ? true : Boolean(row.enableGoogleReview),
    enableInstagram: Boolean(row?.enableInstagram),
    enableFacebook: Boolean(row?.enableFacebook),
    enableWhatsappSupport: Boolean(row?.enableWhatsappSupport),
    enablePrivateFeedback: row?.enablePrivateFeedback === undefined ? true : Boolean(row.enablePrivateFeedback),
    reviewButtonText: readText(row, ['reviewButtonText'], 'Share your honest Google review'),
    feedbackButtonText: readText(row, ['feedbackButtonText'], 'Share private feedback')
  })
}

function selectSetting(row: ApiRecord) {
  selectedStoreId.value = readText(row, ['storeId'], '')
  resetForm(row)
}

function loadSelectedSetting() {
  resetForm(selectedSetting.value)
}

function onCompanyChanged() {
  const store = visibleStores.value[0]
  selectedStoreId.value = store ? readId(store) : ''
  loadSelectedSetting()
}

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const [companyRows, storeRows, settingRows] = await Promise.all([
      get<unknown>('companies'),
      get<unknown>('stores'),
      get<unknown>('digital-bill-review-settings')
    ])
    companies.value = toRows(companyRows)
    stores.value = toRows(storeRows)
    settings.value = toRows(settingRows)
    selectedCompanyId.value ||= readId(companies.value[0])
    selectedStoreId.value ||= readId(visibleStores.value[0])
    loadSelectedSetting()
  } catch (caught) {
    error.value = stripServerUrl(caught instanceof Error ? caught.message : 'Could not load review settings.')
    toast.add({ title: 'Could not load review settings', description: error.value, color: 'error' })
  } finally {
    loading.value = false
  }
}

async function save() {
  const store = selectedStore.value
  if (!store) return
  saving.value = true
  error.value = ''
  try {
    const saved = await put<ApiRecord>(`digital-bill-review-settings/store/${selectedStoreId.value}`, {
      id: selectedSetting.value?.id || null,
      companyId: readText(store, ['companyId'], ''),
      storeGroupId: readText(store, ['storeGroupId'], ''),
      storeId: selectedStoreId.value,
      ...form
    })
    const index = settings.value.findIndex(row => readText(row, ['storeId'], '') === selectedStoreId.value)
    if (index >= 0) settings.value[index] = saved
    else settings.value.unshift(saved)
    resetForm(saved)
    toast.add({ title: 'Review settings saved', color: 'success' })
  } catch (caught) {
    error.value = stripServerUrl(caught instanceof Error ? caught.message : 'Could not save review settings.')
    toast.add({ title: 'Could not save review settings', description: error.value, color: 'error' })
  } finally {
    saving.value = false
  }
}

onMounted(refresh)
useHead({ title: 'Review Settings - Garmetix CRM' })
</script>
