<script setup lang="ts">
const api = useGarmetixApi()
const feedback = useUiFeedback()
const stores = ref<any[]>([])
const settings = ref<any[]>([])
const selectedStoreId = ref('')
const loading = ref(false)
const saving = ref(false)
const testing = ref(false)
const loadError = ref('')
const testMobile = ref('')

const providerOptions = [
  { label: 'Manual only / no API', value: 'ManualOnly' },
  { label: 'Meta WhatsApp Cloud API', value: 'MetaCloudApi' },
  { label: 'Gupshup placeholder', value: 'Gupshup' },
  { label: 'Interakt placeholder', value: 'Interakt' },
  { label: 'AiSensy placeholder', value: 'AiSensy' },
  { label: 'Wati placeholder', value: 'Wati' },
  { label: 'Twilio placeholder', value: 'Twilio' }
]

const form = reactive<any>({
  isEnabled: false,
  autoSendDigitalBills: false,
  provider: 'ManualOnly',
  apiBaseUrl: '',
  apiToken: '',
  phoneNumberId: '',
  senderId: '',
  templateName: '',
  languageCode: 'en',
  messageTemplateText: 'Hello {{customerName}}, thank you for shopping at Aadwika Fashion. Your invoice {{invoiceNumber}} of ₹{{amount}} is ready. View / download your bill: {{publicUrl}}. Team Aadwika Fashion, Dumka.',
  sendPdfLink: true,
  fallbackToManualLog: true,
  retryLimit: 3
})

const selectedStore = computed(() => stores.value.find((item) => item.id === selectedStoreId.value))
const selectedSetting = computed(() => settings.value.find((item) => item.storeId === selectedStoreId.value))
const storeOptions = computed(() => stores.value.map((item) => ({ label: `${item.name || item.storeCode} (${item.storeCode || 'store'})`, value: item.id })))

async function refresh() {
  loading.value = true
  loadError.value = ''
  try {
    const [storeRows, settingRows] = await Promise.all([
      api.list<any>('stores'),
      api.get<any[]>('digital-bill-whatsapp-settings')
    ])
    stores.value = storeRows || []
    settings.value = settingRows || []
    selectedStoreId.value ||= stores.value[0]?.id || ''
    loadSelected()
  } catch (error) {
    loadError.value = feedback.errorMessage(error, 'Please check API service and database migration.', 'WhatsApp settings load failed')
  } finally {
    loading.value = false
  }
}

function loadSelected() {
  const existing = selectedSetting.value
  Object.assign(form, {
    isEnabled: existing?.isEnabled ?? false,
    autoSendDigitalBills: existing?.autoSendDigitalBills ?? false,
    provider: existing?.provider || 'ManualOnly',
    apiBaseUrl: existing?.apiBaseUrl || '',
    apiToken: '',
    phoneNumberId: existing?.phoneNumberId || '',
    senderId: existing?.senderId || '',
    templateName: existing?.templateName || '',
    languageCode: existing?.languageCode || 'en',
    messageTemplateText: existing?.messageTemplateText || 'Hello {{customerName}}, thank you for shopping at Aadwika Fashion. Your invoice {{invoiceNumber}} of ₹{{amount}} is ready. View / download your bill: {{publicUrl}}. Team Aadwika Fashion, Dumka.',
    sendPdfLink: existing?.sendPdfLink ?? true,
    fallbackToManualLog: existing?.fallbackToManualLog ?? true,
    retryLimit: existing?.retryLimit ?? 3
  })
}

async function save() {
  const store = selectedStore.value
  if (!store) return
  saving.value = true
  try {
    await api.update<any>('digital-bill-whatsapp-settings/store', store.id, {
      id: selectedSetting.value?.id,
      companyId: store.companyId,
      storeGroupId: store.storeGroupId,
      storeId: store.id,
      ...form
    })
    feedback.notify('WhatsApp settings saved', 'Auto-send will run after sale invoice finalization only when enabled.', 'success')
    await refresh()
  } catch (error) {
    feedback.failed('Could not save WhatsApp settings', error)
  } finally {
    saving.value = false
  }
}

async function testSend() {
  if (!selectedStoreId.value || !testMobile.value.trim()) {
    feedback.notify('Select a store and enter test mobile number.', undefined, 'warning')
    return
  }
  testing.value = true
  try {
    const response = await api.create<any>('digital-bill-whatsapp-settings/test-send', {
      storeId: selectedStoreId.value,
      customerMobile: testMobile.value,
      customerName: 'Test Customer'
    })
    feedback.notify(`Test result: ${response.status}`, response.errorMessage || response.messageBody, response.status === 'Sent' ? 'success' : 'warning')
  } catch (error) {
    feedback.failed('WhatsApp test send failed', error)
  } finally {
    testing.value = false
  }
}

watch(selectedStoreId, loadSelected)
onMounted(refresh)
</script>

<template>
  <AppShell title="WhatsApp Settings" @refresh="refresh">
    <UiModulePageHeader
      title="WhatsApp Settings"
      description="Configure store-wise Digital Bill CRM WhatsApp delivery. Invoice Meta template now sends 4 variables: customer, invoice number, amount and bill link."
      icon="i-lucide-message-circle"
      primary-label="Save Settings"
      primary-icon="i-lucide-save"
      @primary="save"
    />

    <UAlert
      class="mt-4"
      color="info"
      variant="subtle"
      icon="i-lucide-webhook"
      title="Meta WhatsApp webhook"
      description="For delivery/read status, set your Meta callback URL to /api/public/digital-bill-whatsapp/webhook/meta and set WhatsApp__MetaWebhookVerifyToken in production env."
    />

    <UiRegisterPanel class="mt-4" title="Provider setup" description="Auto-send invoice links after sale finalization." :loading="loading" :error="loadError" :empty="stores.length === 0" empty-title="No stores" empty-description="Create a store before configuring WhatsApp." empty-icon="i-lucide-store" @retry="refresh">
      <div class="grid gap-4 lg:grid-cols-2">
        <UFormField label="Store"><USelect v-model="selectedStoreId" :items="storeOptions" /></UFormField>
        <UFormField label="Provider"><USelect v-model="form.provider" :items="providerOptions" /></UFormField>
        <UFormField label="Meta Graph API base URL"><UInput v-model="form.apiBaseUrl" placeholder="Leave blank or use https://graph.facebook.com/v20.0" /><p class="mt-1 text-xs text-muted">Do not enter your Garmetix/public website URL here.</p></UFormField>
        <UFormField label="API token"><UInput v-model="form.apiToken" type="password" :placeholder="selectedSetting?.hasApiToken ? 'Token saved. Leave blank to keep existing.' : 'Paste provider token'" /></UFormField>
        <UFormField label="Phone number ID"><UInput v-model="form.phoneNumberId" placeholder="Meta phone_number_id" /></UFormField>
        <UFormField label="Sender ID"><UInput v-model="form.senderId" placeholder="Optional sender / channel id" /></UFormField>
        <UFormField label="Template name"><UInput v-model="form.templateName" placeholder="garmetix_invoice_link" /></UFormField>
        <UFormField label="Language code"><UInput v-model="form.languageCode" placeholder="en" /></UFormField>
        <UFormField class="lg:col-span-2" label="Message template">
          <UTextarea v-model="form.messageTemplateText" :rows="4" />
          <p class="mt-1 text-xs text-muted">For Meta invoice template use 4 body variables only: <code>&#123;&#123;1&#125;&#125;</code> customer name, <code>&#123;&#123;2&#125;&#125;</code> invoice number, <code>&#123;&#123;3&#125;&#125;</code> amount, <code>&#123;&#123;4&#125;&#125;</code> bill link. Store name can stay fixed in the approved Meta template text. Manual message preview supports <code>&#123;&#123;customerName&#125;&#125;</code>, <code>&#123;&#123;invoiceNumber&#125;&#125;</code>, <code>&#123;&#123;amount&#125;&#125;</code>, <code>&#123;&#123;publicUrl&#125;&#125;</code>.</p>
        </UFormField>
      </div>

      <div class="mt-4 grid gap-3 md:grid-cols-2 xl:grid-cols-4">
        <UCheckbox v-model="form.isEnabled" label="Provider enabled" />
        <UCheckbox v-model="form.autoSendDigitalBills" label="Auto-send after sale" />
        <UCheckbox v-model="form.fallbackToManualLog" label="Fallback to manual log" />
        <UCheckbox v-model="form.sendPdfLink" label="Include invoice link" />
      </div>

      <div class="mt-4 grid gap-4 lg:grid-cols-[1fr_220px_auto_auto]">
        <UInput v-model="testMobile" placeholder="Test mobile number" />
        <UInput v-model="form.retryLimit" type="number" min="0" max="10" placeholder="Retry limit" />
        <UButton icon="i-lucide-send" color="neutral" variant="subtle" :loading="testing" label="Test Send" @click="testSend" />
        <UButton icon="i-lucide-save" :loading="saving" label="Save Settings" @click="save" />
      </div>

      <UAlert v-if="selectedSetting?.lastError" class="mt-4" color="warning" variant="subtle" icon="i-lucide-triangle-alert" title="Last provider response" :description="selectedSetting.lastError" />
    </UiRegisterPanel>
  </AppShell>
</template>
