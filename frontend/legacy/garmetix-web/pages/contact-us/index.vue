<script setup lang="ts">
const api = useGarmetixApi()
const feedback = useUiFeedback()
const loading = ref(false)
const serverInfo = ref<any | null>(null)
const loadError = ref('')

useHead({ title: 'Contact | Garmetix' })

const fallbackContacts = [
  { label: 'Business owner', value: 'Amit Kumar', type: 'person', note: 'Primary business owner and authorization contact.' },
  { label: 'Email', value: 'ameetkrsah@gmail.com', type: 'email', note: 'Use for account and support communication.' },
  { label: 'Location', value: 'Dumka / Jamshedpur, Jharkhand, India', type: 'location', note: 'Business operating region.' },
  { label: 'Product', value: 'Garmetix', type: 'product', note: 'Garment store management, billing, purchase, inventory, GST and controlled administration.' }
]

const contacts = computed(() => serverInfo.value?.contacts?.length ? serverInfo.value.contacts : fallbackContacts)

function contactIcon(type: string) {
  if (type === 'email') return 'i-lucide-mail'
  if (type === 'location') return 'i-lucide-map-pin'
  if (type === 'person') return 'i-lucide-user-round'
  return 'i-lucide-box'
}

async function refresh() {
  loading.value = true
  loadError.value = ''
  try {
    serverInfo.value = await api.get<any>('app-info')
  } catch (error) {
    serverInfo.value = null
    loadError.value = feedback.errorMessage(error, 'Contact information could not be loaded. Try again.', 'Contact information load failed')
  } finally {
    loading.value = false
  }
}

onMounted(refresh)
</script>

<template>
  <AppShell title="Contact Us" @refresh="refresh">
    <div class="space-y-6">
      <UiModulePageHeader
        title="Contact Us"
        description="Business ownership, support email, operating region, and product contact details."
        icon="i-lucide-messages-square"
      >
        <template #actions>
          <UButton icon="i-lucide-refresh-cw" variant="subtle" :loading="loading" label="Refresh" @click="refresh" />
        </template>
      </UiModulePageHeader>

      <UAlert
        v-if="loadError"
        color="error"
        variant="subtle"
        icon="i-lucide-circle-alert"
        title="Contact information is unavailable"
        :description="loadError"
        :actions="[{ label: 'Try again', icon: 'i-lucide-refresh-cw', onClick: refresh }]"
      />

      <div v-if="loading && !serverInfo" class="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        <USkeleton v-for="row in 4" :key="row" class="h-44 w-full" />
      </div>

      <div v-else class="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        <UCard v-for="contact in contacts" :key="contact.label">
          <div class="space-y-3">
            <div class="flex items-center gap-3">
              <div class="rounded-xl bg-primary/10 p-2 text-primary">
                <UIcon :name="contactIcon(contact.type)" class="h-5 w-5" />
              </div>
              <h2 class="font-semibold text-slate-950 dark:text-white">{{ contact.label }}</h2>
            </div>
            <p class="break-words text-lg font-bold text-slate-950 dark:text-white">{{ contact.value }}</p>
            <p class="text-sm text-slate-500 dark:text-slate-400">{{ contact.note }}</p>
          </div>
        </UCard>
      </div>

      <UAlert
        color="primary"
        variant="subtle"
        icon="i-lucide-info"
        title="Support reference"
        description="Include the Garmetix version and build code from About Garmetix when reporting an issue."
      />
    </div>
  </AppShell>
</template>
