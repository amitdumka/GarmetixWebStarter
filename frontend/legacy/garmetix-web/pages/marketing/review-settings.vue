<script setup lang="ts">
const api = useGarmetixApi()
const feedback = useUiFeedback()
const settings = ref<any[]>([])
const stores = ref<any[]>([])
const selectedStoreId = ref('')
const loading = ref(false)
const saving = ref(false)
const loadError = ref('')
const form = reactive<any>({
  googleReviewUrl: '', instagramUrl: '', facebookUrl: '', whatsAppSupportNumber: '',
  enableGoogleReview: true, enableInstagram: true, enableFacebook: false, enableWhatsappSupport: true, enablePrivateFeedback: true,
  reviewButtonText: 'Share your honest Google review', feedbackButtonText: 'Share private feedback'
})

const selectedStore = computed(() => stores.value.find((item) => item.id === selectedStoreId.value))
const storeOptions = computed(() => stores.value.map((item) => ({ label: `${item.name || item.storeCode} (${item.storeCode || 'store'})`, value: item.id })))

async function refresh() {
  loading.value = true
  loadError.value = ''
  try {
    const [storeRows, settingRows] = await Promise.all([api.list<any>('stores'), api.get<any[]>('digital-bill-review-settings')])
    stores.value = storeRows || []
    settings.value = settingRows || []
    selectedStoreId.value ||= stores.value[0]?.id || ''
    loadSelected()
  } catch (error) {
    loadError.value = feedback.errorMessage(error, 'Please check API service and database migration.', 'Review settings load failed')
  } finally {
    loading.value = false
  }
}

function loadSelected() {
  const existing = settings.value.find((item) => item.storeId === selectedStoreId.value)
  Object.assign(form, {
    googleReviewUrl: existing?.googleReviewUrl || '', instagramUrl: existing?.instagramUrl || '', facebookUrl: existing?.facebookUrl || '', whatsAppSupportNumber: existing?.whatsAppSupportNumber || '',
    enableGoogleReview: existing?.enableGoogleReview ?? true, enableInstagram: existing?.enableInstagram ?? true, enableFacebook: existing?.enableFacebook ?? false, enableWhatsappSupport: existing?.enableWhatsappSupport ?? true, enablePrivateFeedback: existing?.enablePrivateFeedback ?? true,
    reviewButtonText: existing?.reviewButtonText || 'Share your honest Google review', feedbackButtonText: existing?.feedbackButtonText || 'Share private feedback'
  })
}

async function save() {
  const store = selectedStore.value
  if (!store) return
  saving.value = true
  try {
    await api.update<any>('digital-bill-review-settings/store', store.id, {
      id: settings.value.find((item) => item.storeId === store.id)?.id,
      companyId: store.companyId,
      storeGroupId: store.storeGroupId,
      storeId: store.id,
      ...form
    })
    feedback.notify('Review and social settings saved', undefined, 'success')
    await refresh()
  } catch (error) {
    feedback.failed('Could not save review settings', error)
  } finally {
    saving.value = false
  }
}

watch(selectedStoreId, loadSelected)
onMounted(refresh)
</script>

<template>
  <AppShell title="Review Settings" @refresh="refresh">
    <UiModulePageHeader
      title="Review & Social Settings"
      description="Configure store-wise Google review, Instagram, Facebook, WhatsApp support, and private feedback buttons for public digital bills."
      icon="i-lucide-star"
      primary-label="Save Settings"
      primary-icon="i-lucide-save"
      @primary="save"
    />

    <UiRegisterPanel class="mt-4" title="Store review settings" description="Store-wise links used by public invoice page." :loading="loading" :error="loadError" :empty="stores.length === 0" empty-title="No stores" empty-description="Create a store before configuring review links." empty-icon="i-lucide-store" @retry="refresh">
      <div class="grid gap-4 lg:grid-cols-2">
        <UFormField label="Store"><USelect v-model="selectedStoreId" :items="storeOptions" /></UFormField>
        <div />
        <UFormField label="Google Review URL"><UInput v-model="form.googleReviewUrl" placeholder="https://g.page/r/..." /></UFormField>
        <UFormField label="Instagram URL"><UInput v-model="form.instagramUrl" placeholder="https://instagram.com/..." /></UFormField>
        <UFormField label="Facebook URL"><UInput v-model="form.facebookUrl" placeholder="https://facebook.com/..." /></UFormField>
        <UFormField label="WhatsApp Support Number"><UInput v-model="form.whatsAppSupportNumber" placeholder="919999999999" /></UFormField>
        <UFormField label="Review button text"><UInput v-model="form.reviewButtonText" /></UFormField>
        <UFormField label="Feedback button text"><UInput v-model="form.feedbackButtonText" /></UFormField>
      </div>

      <div class="mt-4 grid gap-3 md:grid-cols-2 xl:grid-cols-5">
        <UCheckbox v-model="form.enableGoogleReview" label="Google review" />
        <UCheckbox v-model="form.enableInstagram" label="Instagram" />
        <UCheckbox v-model="form.enableFacebook" label="Facebook" />
        <UCheckbox v-model="form.enableWhatsappSupport" label="WhatsApp support" />
        <UCheckbox v-model="form.enablePrivateFeedback" label="Private feedback" />
      </div>

      <div class="mt-5 flex justify-end">
        <UButton icon="i-lucide-save" :loading="saving" label="Save Settings" @click="save" />
      </div>
    </UiRegisterPanel>
  </AppShell>
</template>
