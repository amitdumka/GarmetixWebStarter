<script setup lang="ts">
const api = useGarmetixApi()
const feedback = useUiFeedback()
const ALL_CATEGORIES = '__all__'
const loading = ref(false)
const serverInfo = ref<any | null>(null)
const search = ref('')
const selectedCategory = ref(ALL_CATEGORIES)
const loadError = ref('')

useHead({ title: 'Help & FAQ | Garmetix' })

const fallbackFaqs = [
  { question: 'How do I know which version is running?', answer: 'Open About Garmetix and check the version, release, build date, and build code.', category: 'Version' },
  { question: 'Where do I create the first company?', answer: 'Use Admin > Company Onboarding to create the company, store group, store, and initial users.', category: 'Company' },
  { question: 'Where do I prepare AF/SS defaults?', answer: 'Use Admin > AF/SS Defaults and select the target company before applying the defaults.', category: 'Company' },
  { question: 'Where can I see success or failure messages?', answer: 'Use Data > Message Logs.', category: 'Logs' }
]

const faqs = computed(() => serverInfo.value?.faqs?.length ? serverInfo.value.faqs : fallbackFaqs)
const categoryOptions = computed(() => [
  { label: 'All categories', value: ALL_CATEGORIES },
  ...Array.from(new Set(faqs.value.map((item: any) => item.category))).map((category) => ({ label: category, value: category }))
])

const filteredFaqs = computed(() => {
  const term = search.value.trim().toLowerCase()
  return faqs.value.filter((item: any) => {
    if (selectedCategory.value !== ALL_CATEGORIES && item.category !== selectedCategory.value) return false
    if (term) {
      const text = `${item.question} ${item.answer} ${item.category}`.toLowerCase()
      if (!text.includes(term)) return false
    }
    return true
  })
})

async function refresh() {
  loading.value = true
  loadError.value = ''
  try {
    serverInfo.value = await api.get<any>('app-info')
  } catch (error) {
    serverInfo.value = null
    loadError.value = feedback.errorMessage(error, 'Help topics could not be loaded. Try again.', 'FAQ load failed')
  } finally {
    loading.value = false
  }
}

onMounted(refresh)
</script>

<template>
  <AppShell title="Help & FAQ" @refresh="refresh">
    <div class="space-y-6">
      <UiModulePageHeader
        title="Help & FAQ"
        description="Find guidance for accounts, company setup, daily operations, documents, Off Book records, and support."
        icon="i-lucide-circle-help"
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
        title="Help topics are unavailable"
        :description="loadError"
        :actions="[{ label: 'Try again', icon: 'i-lucide-refresh-cw', onClick: refresh }]"
      />

      <UCard>
        <div class="grid gap-4 md:grid-cols-[1fr_260px]">
          <UFormField label="Search"><UInput v-model="search" placeholder="Search question or answer" icon="i-lucide-search" /></UFormField>
          <UFormField label="Category"><USelect v-model="selectedCategory" :items="categoryOptions" /></UFormField>
        </div>
      </UCard>

      <div v-if="loading && !serverInfo" class="space-y-3">
        <USkeleton v-for="row in 5" :key="row" class="h-28 w-full" />
      </div>
      <div v-else-if="filteredFaqs.length" class="space-y-3">
        <UCard v-for="item in filteredFaqs" :key="item.question">
          <div class="space-y-2">
            <div class="flex flex-wrap items-center gap-2">
              <UBadge color="primary" variant="subtle">{{ item.category }}</UBadge>
              <h2 class="font-semibold text-slate-950 dark:text-white">{{ item.question }}</h2>
            </div>
            <p class="text-sm leading-6 text-slate-600 dark:text-slate-300">{{ item.answer }}</p>
          </div>
        </UCard>
      </div>
      <UiCrudEmptyState v-else title="No FAQ matched" description="Change the search text or category filter." icon="i-lucide-circle-help" />
    </div>
  </AppShell>
</template>
