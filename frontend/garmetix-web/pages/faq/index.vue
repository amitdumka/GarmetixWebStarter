<script setup lang="ts">
const api = useGarmetixApi()
const feedback = useUiFeedback()
const loading = ref(false)
const serverInfo = ref<any | null>(null)
const search = ref('')
const selectedCategory = ref('')

const fallbackFaqs = [
  { question: 'How do I know which code version is running?', answer: 'Open About us and check the version, release, build date and build code. The same data is also available from /api/app-info/version.', category: 'Version' },
  { question: 'When should the version number change?', answer: 'Every packaged code change should update the central version constants in backend AppInfoEndpoints and frontend appVersion.ts.', category: 'Version' },
  { question: 'Where do I onboard the first company?', answer: 'Use Admin > Onboarding.', category: 'Onboarding' },
  { question: 'Where do I seed AF/SS default data?', answer: 'Use Admin > AF/SS.', category: 'Seeder' },
  { question: 'Where can I see success or failure messages?', answer: 'Use Data > Message Logs.', category: 'Logs' }
]

const faqs = computed(() => serverInfo.value?.faqs?.length ? serverInfo.value.faqs : fallbackFaqs)
const categoryOptions = computed(() => [
  { label: 'All categories', value: '' },
  ...Array.from(new Set(faqs.value.map((item: any) => item.category))).map((category) => ({ label: category, value: category }))
])

const filteredFaqs = computed(() => {
  const term = search.value.trim().toLowerCase()
  return faqs.value.filter((item: any) => {
    if (selectedCategory.value && item.category !== selectedCategory.value) return false
    if (term) {
      const text = `${item.question} ${item.answer} ${item.category}`.toLowerCase()
      if (!text.includes(term)) return false
    }
    return true
  })
})

async function refresh() {
  loading.value = true
  try {
    serverInfo.value = await api.get<any>('app-info')
  } catch (error) {
    serverInfo.value = null
    feedback.failed('FAQ load failed', error)
  } finally {
    loading.value = false
  }
}

onMounted(refresh)
</script>

<template>
  <AppShell title="FAQ" @refresh="refresh">
    <div class="space-y-6">
      <section class="rounded-3xl border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <div class="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
          <div class="space-y-2">
            <p class="text-xs font-semibold uppercase tracking-[0.3em] text-slate-500">Help center</p>
            <h1 class="text-2xl font-bold text-slate-950 dark:text-white">Frequently asked questions</h1>
            <p class="max-w-3xl text-sm text-slate-500 dark:text-slate-400">
              Common questions for version identity, onboarding, seeding, logs and deployment.
            </p>
          </div>
          <UButton icon="i-lucide-refresh-cw" variant="subtle" :loading="loading" @click="refresh">Refresh</UButton>
        </div>
      </section>

      <UCard>
        <div class="grid gap-4 md:grid-cols-[1fr_260px]">
          <UFormField label="Search"><UInput v-model="search" placeholder="Search question or answer" icon="i-lucide-search" /></UFormField>
          <UFormField label="Category"><USelect v-model="selectedCategory" :items="categoryOptions" /></UFormField>
        </div>
      </UCard>

      <div v-if="filteredFaqs.length" class="space-y-3">
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
