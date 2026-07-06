<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-kicker"><UIcon name="i-lucide-message-circle" class="size-4" /> Digital CRM setup</p>
          <h2 class="garmetix-dashboard-title">WhatsApp Settings</h2>
          <p class="garmetix-dashboard-subtitle">Provider, auto-send and template settings for invoice links and campaign messaging.</p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton color="neutral" variant="soft" icon="i-lucide-refresh-cw" :loading="loading" @click="refresh">Refresh</UButton>
          <UButton icon="i-lucide-save" :loading="saving" :disabled="!selectedStoreId" @click="save">Save Provider</UButton>
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
      <div class="flex flex-wrap items-end gap-2">
        <UBadge :color="form.isEnabled ? 'success' : 'neutral'" variant="subtle">{{ form.isEnabled ? 'Enabled' : 'Disabled' }}</UBadge>
        <UBadge :color="selectedSetting?.hasApiToken ? 'success' : 'warning'" variant="subtle">{{ selectedSetting?.hasApiToken ? 'Token saved' : 'Token not saved' }}</UBadge>
      </div>
    </div>

    <section class="grid gap-4 xl:grid-cols-[minmax(0,1fr)_minmax(340px,0.75fr)]">
      <div class="garmetix-section-card">
        <div class="mb-4">
          <p class="garmetix-kicker"><UIcon name="i-lucide-send" class="size-4" /> Provider setup</p>
          <h3 class="text-xl font-semibold text-highlighted">{{ selectedStoreName }}</h3>
        </div>

        <div class="grid gap-4 lg:grid-cols-2">
          <UFormField label="Provider">
            <USelect v-model="form.provider" :items="providerItems" />
          </UFormField>
          <UFormField label="Template name">
            <UInput v-model="form.templateName" placeholder="invoice_link" />
          </UFormField>
          <UFormField label="API base URL">
            <UInput v-model="form.apiBaseUrl" placeholder="Optional provider URL" />
          </UFormField>
          <UFormField label="API token">
            <UInput v-model="form.apiToken" type="password" placeholder="Leave blank to keep saved token" />
          </UFormField>
          <UFormField label="Phone number ID">
            <UInput v-model="form.phoneNumberId" />
          </UFormField>
          <UFormField label="Sender ID">
            <UInput v-model="form.senderId" />
          </UFormField>
          <UFormField label="Language">
            <UInput v-model="form.languageCode" placeholder="en" />
          </UFormField>
          <UFormField label="Retry limit">
            <UInput v-model.number="form.retryLimit" type="number" min="0" max="10" />
          </UFormField>
          <UFormField label="Message template" class="lg:col-span-2">
            <UTextarea v-model="form.messageTemplateText" :rows="4" />
          </UFormField>
        </div>

        <div class="mt-5 grid gap-3 md:grid-cols-2">
          <div class="rounded-md border border-default p-3"><UCheckbox v-model="form.isEnabled" label="Enable provider for this store" /></div>
          <div class="rounded-md border border-default p-3"><UCheckbox v-model="form.autoSendDigitalBills" label="Auto-send digital bills" /></div>
          <div class="rounded-md border border-default p-3"><UCheckbox v-model="form.sendPdfLink" label="Send PDF/public link" /></div>
          <div class="rounded-md border border-default p-3"><UCheckbox v-model="form.fallbackToManualLog" label="Fallback to manual log on provider issue" /></div>
        </div>

        <div class="mt-5 flex justify-end">
          <UButton icon="i-lucide-save" :loading="saving" :disabled="!selectedStoreId" @click="save">Save Provider</UButton>
        </div>
      </div>

      <div class="grid gap-4">
        <div class="garmetix-section-card">
          <p class="garmetix-kicker"><UIcon name="i-lucide-test-tube-2" class="size-4" /> Test message</p>
          <h3 class="text-xl font-semibold text-highlighted">Send Test</h3>
          <div class="mt-4 grid gap-3">
            <UFormField label="Mobile number">
              <UInput v-model="test.customerMobile" placeholder="919999999999" />
            </UFormField>
            <UFormField label="Customer name">
              <UInput v-model="test.customerName" placeholder="Customer" />
            </UFormField>
            <UFormField label="Message">
              <UTextarea v-model="test.message" :rows="3" />
            </UFormField>
            <UButton icon="i-lucide-send" :loading="testing" :disabled="!selectedStoreId || !test.customerMobile.trim()" @click="sendTest">Send Test</UButton>
            <UAlert v-if="testResult" :color="testStatusColor" variant="subtle" icon="i-lucide-info" :title="readText(testResult, ['status'])" :description="readText(testResult, ['errorMessage', 'messageBody'], '')" />
          </div>
        </div>

        <div class="garmetix-section-card">
          <p class="garmetix-kicker"><UIcon name="i-lucide-store" class="size-4" /> Configured stores</p>
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
                  <p class="text-xs text-muted">{{ readText(setting, ['provider']) }} | {{ readText(setting, ['templateName'], 'No template') }}</p>
                </div>
                <UBadge :color="setting.isEnabled ? 'success' : 'neutral'" variant="subtle">{{ setting.isEnabled ? 'Enabled' : 'Disabled' }}</UBadge>
              </div>
            </button>
            <p v-if="!settings.length" class="text-sm text-muted">No WhatsApp provider settings configured yet.</p>
          </div>
        </div>
      </div>
    </section>
  </section>
</template>

<script setup lang="ts">
import { stripServerUrl } from '@garmetix/shared-utils'
import { readId, readText, toRows, type ApiRecord, useCrmApiClient } from '../../utils/crm-api'

const toast = useToast()
const { get, post, put } = useCrmApiClient()

const companies = ref<ApiRecord[]>([])
const stores = ref<ApiRecord[]>([])
const settings = ref<ApiRecord[]>([])
const selectedCompanyId = ref('')
const selectedStoreId = ref('')
const loading = ref(false)
const saving = ref(false)
const testing = ref(false)
const error = ref('')
const testResult = ref<ApiRecord | null>(null)

const form = reactive({
  isEnabled: false,
  autoSendDigitalBills: false,
  provider: 'ManualOnly',
  apiBaseUrl: '',
  apiToken: '',
  phoneNumberId: '',
  senderId: '',
  templateName: '',
  languageCode: 'en',
  messageTemplateText: 'Hello {{customerName}}, thank you for shopping with us. Your latest bill {{invoiceNumber}} is available here: {{publicUrl}}',
  sendPdfLink: true,
  fallbackToManualLog: true,
  retryLimit: 3
})

const test = reactive({
  customerMobile: '',
  customerName: 'Customer',
  message: 'Test WhatsApp from Garmetix Digital CRM.'
})

const providerItems = [
  { label: 'Manual only', value: 'ManualOnly' },
  { label: 'Meta Cloud API', value: 'MetaCloudApi' },
  { label: 'Custom provider', value: 'CustomProvider' }
]

const companyOptions = computed(() => companies.value.map(row => ({ label: readText(row, ['name', 'companyName'], 'Company'), value: readId(row) })))
const visibleStores = computed(() => stores.value.filter(row => !selectedCompanyId.value || readText(row, ['companyId'], '') === selectedCompanyId.value))
const storeOptions = computed(() => visibleStores.value.map(row => ({ label: readText(row, ['name', 'storeName'], 'Store'), value: readId(row) })))
const selectedStore = computed(() => stores.value.find(row => readId(row) === selectedStoreId.value) ?? null)
const selectedSetting = computed(() => settings.value.find(row => readText(row, ['storeId'], '') === selectedStoreId.value) ?? null)
const selectedStoreName = computed(() => selectedStore.value ? readText(selectedStore.value, ['name', 'storeName'], 'Store') : 'Select a store')
const testStatusColor = computed(() => {
  const status = readText(testResult.value, ['status'], '').toLowerCase()
  if (status.includes('failed') || status.includes('skipped')) return 'error' as const
  if (status.includes('sent') || status.includes('queued')) return 'success' as const
  return 'neutral' as const
})

function storeName(storeId: string) {
  const store = stores.value.find(row => readId(row) === storeId)
  return store ? readText(store, ['name', 'storeName'], 'Store') : storeId || 'Store'
}

function resetForm(row?: ApiRecord | null) {
  Object.assign(form, {
    isEnabled: Boolean(row?.isEnabled),
    autoSendDigitalBills: Boolean(row?.autoSendDigitalBills),
    provider: readText(row, ['provider'], 'ManualOnly'),
    apiBaseUrl: readText(row, ['apiBaseUrl'], ''),
    apiToken: '',
    phoneNumberId: readText(row, ['phoneNumberId'], ''),
    senderId: readText(row, ['senderId'], ''),
    templateName: readText(row, ['templateName'], ''),
    languageCode: readText(row, ['languageCode'], 'en'),
    messageTemplateText: readText(row, ['messageTemplateText'], 'Hello {{customerName}}, thank you for shopping with us. Your latest bill {{invoiceNumber}} is available here: {{publicUrl}}'),
    sendPdfLink: row?.sendPdfLink === undefined ? true : Boolean(row.sendPdfLink),
    fallbackToManualLog: row?.fallbackToManualLog === undefined ? true : Boolean(row.fallbackToManualLog),
    retryLimit: Number(row?.retryLimit ?? 3)
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
      get<unknown>('digital-bill-whatsapp-settings')
    ])
    companies.value = toRows(companyRows)
    stores.value = toRows(storeRows)
    settings.value = toRows(settingRows)
    selectedCompanyId.value ||= readId(companies.value[0])
    selectedStoreId.value ||= readId(visibleStores.value[0])
    loadSelectedSetting()
  } catch (caught) {
    error.value = stripServerUrl(caught instanceof Error ? caught.message : 'Could not load WhatsApp settings.')
    toast.add({ title: 'Could not load WhatsApp settings', description: error.value, color: 'error' })
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
    const saved = await put<ApiRecord>(`digital-bill-whatsapp-settings/store/${selectedStoreId.value}`, {
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
    toast.add({ title: 'WhatsApp provider saved', color: 'success' })
  } catch (caught) {
    error.value = stripServerUrl(caught instanceof Error ? caught.message : 'Could not save WhatsApp settings.')
    toast.add({ title: 'Could not save WhatsApp settings', description: error.value, color: 'error' })
  } finally {
    saving.value = false
  }
}

async function sendTest() {
  testing.value = true
  testResult.value = null
  try {
    testResult.value = await post<ApiRecord>('digital-bill-whatsapp-settings/test-send', {
      storeId: selectedStoreId.value,
      customerMobile: test.customerMobile,
      customerName: test.customerName,
      message: test.message
    })
    toast.add({ title: 'Test WhatsApp queued', description: readText(testResult.value, ['status']), color: 'success' })
  } catch (caught) {
    const message = stripServerUrl(caught instanceof Error ? caught.message : 'Could not send WhatsApp test.')
    toast.add({ title: 'Could not send WhatsApp test', description: message, color: 'error' })
  } finally {
    testing.value = false
  }
}

onMounted(refresh)
useHead({ title: 'WhatsApp Settings - Garmetix CRM' })
</script>
