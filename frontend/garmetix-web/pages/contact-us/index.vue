<script setup lang="ts">
const api = useGarmetixApi()
const feedback = useUiFeedback()
const loading = ref(false)
const serverInfo = ref<any | null>(null)

const fallbackContacts = [
  { label: 'Business owner', value: 'Amit Kumar', type: 'person', note: 'Primary owner/contact for deployment decisions.' },
  { label: 'Email', value: 'ameetkrsah@gmail.com', type: 'email', note: 'Use for account, deployment and support communication.' },
  { label: 'Location', value: 'Dumka / Jamshedpur, Jharkhand, India', type: 'location', note: 'Business operating region.' },
  { label: 'Product', value: 'Garmetix Web', type: 'product', note: 'Garment store management, billing, purchase, inventory, GST and SaaS admin.' }
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
  try {
    serverInfo.value = await api.get<any>('app-info')
  } catch (error) {
    serverInfo.value = null
    feedback.failed('Contact information load failed', error)
  } finally {
    loading.value = false
  }
}

onMounted(refresh)
</script>

<template>
  <AppShell title="Contact Us" @refresh="refresh">
    <div class="space-y-6">
      <section class="rounded-3xl border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <div class="flex items-center justify-between gap-4">
          <div class="space-y-2">
            <p class="text-xs font-semibold uppercase tracking-[0.3em] text-slate-500">Support & communication</p>
            <h1 class="text-3xl font-bold text-slate-950 dark:text-white">Contact Us</h1>
            <p class="max-w-3xl text-sm text-slate-500 dark:text-slate-400">
              Use this page to find the current business and support contact details linked with this deployment.
            </p>
          </div>
          <UButton icon="i-lucide-refresh-cw" variant="subtle" :loading="loading" @click="refresh">Refresh</UButton>
        </div>
      </section>

      <div class="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
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
        title="Deployment note"
        description="For production, replace these defaults with your official business support email, phone, website and address when you finalize branding."
      />
    </div>
  </AppShell>
</template>
