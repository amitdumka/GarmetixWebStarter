<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const feedback = useUiFeedback()
const isAuthenticated = auth.isAuthenticated
const canSeeAdmin = auth.canSeeAdmin

const loading = ref(false)
const loadError = ref('')
const seeding = ref(false)
const options = ref<any | null>(null)
const result = ref<any | null>(null)
const seedForm = reactive({
  companyId: '',
  profileCode: 'AF',
  includeUsers: true,
  includeEmployees: true,
  includeProducts: true,
  resetDefaultUserPasswords: false,
  confirm: false
})

const companies = computed(() => options.value?.companies || [])
const profiles = computed(() => options.value?.profiles || [])
const comparison = computed(() => options.value?.comparison || {})

const companyOptions = computed(() => companies.value.map((company: any) => ({
  label: `${company.name}${company.code ? ` (${company.code})` : ''}`,
  value: company.id
})))

const profileOptions = computed(() => profiles.value.map((profile: any) => ({
  label: profile.label,
  value: profile.code
})))

const selectedCompany = computed(() => companies.value.find((company: any) => company.id === seedForm.companyId))
const selectedProfile = computed(() => profiles.value.find((profile: any) => profile.code === seedForm.profileCode))

const canSeed = computed(() => Boolean(seedForm.companyId && seedForm.profileCode && seedForm.confirm && !seeding.value))

function countEntries(value: any) {
  if (!value || typeof value !== 'object') return []
  return Object.entries(value).map(([key, count]) => ({ key, count }))
}

async function refresh() {
  if (!auth.isAuthenticated.value || !auth.canSeeAdmin.value) {
    return
  }

  loading.value = true
  loadError.value = ''
  try {
    options.value = await api.get<any>('afss-seeder/options')
    if (!seedForm.companyId && companies.value.length) {
      seedForm.companyId = companies.value[0].id
    }
  } catch (error) {
    loadError.value = feedback.errorMessage(error, 'Seeder options could not be loaded. Try again.', 'AF/SS options load failed')
    feedback.failed('AF/SS options load failed', error)
  } finally {
    loading.value = false
  }
}

async function seedDefaults() {
  if (!canSeed.value) return

  seeding.value = true
  result.value = null
  try {
    result.value = await api.create<any>('afss-seeder/seed', {
      companyId: seedForm.companyId,
      profileCode: seedForm.profileCode,
      includeUsers: seedForm.includeUsers,
      includeEmployees: seedForm.includeEmployees,
      includeProducts: seedForm.includeProducts,
      resetDefaultUserPasswords: seedForm.resetDefaultUserPasswords
    })
    feedback.success(result.value?.message || 'AF/SS default data seeded')
    seedForm.confirm = false
    refresh().catch((error) => feedback.failed('Post-seed refresh failed', error))
  } catch (error) {
    feedback.failed('AF/SS seed failed', error)
  } finally {
    seeding.value = false
  }
}

onMounted(refresh)
</script>

<template>
  <AppShell
    title="AF/SS Defaults"
    :companies="companies"
    @refresh="refresh"
  >
    <div v-if="!isAuthenticated" class="rounded-3xl border border-dashed border-slate-300 p-8 text-center text-sm text-slate-500 dark:border-slate-700 dark:text-slate-400">
      Login as admin to seed AF/SS default data.
    </div>

    <div v-else-if="!canSeeAdmin" class="rounded-3xl border border-dashed border-amber-300 bg-amber-50 p-8 text-center text-sm text-amber-700 dark:border-amber-700 dark:bg-amber-950/40 dark:text-amber-200">
      AF/SS seeding is available only for admin users.
    </div>

    <div v-else class="space-y-6">
      <UiModulePageHeader
        title="AF/SS Defaults"
        description="Add the approved users, employees, product masters, and opening structure to a selected company."
        icon="i-lucide-database-backup"
      >
        <template #actions>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="subtle" :loading="loading" label="Refresh" @click="refresh" />
        </template>
      </UiModulePageHeader>

      <UAlert
        v-if="loadError"
        color="error"
        variant="subtle"
        icon="i-lucide-circle-alert"
        title="Could not load seeder options"
        :description="loadError"
        :actions="[{ label: 'Try again', icon: 'i-lucide-refresh-cw', onClick: refresh }]"
      />

      <div v-else-if="loading" class="grid gap-4 lg:grid-cols-[0.8fr_1.2fr]">
        <USkeleton class="h-[36rem] w-full" />
        <USkeleton class="h-[28rem] w-full" />
      </div>

      <section v-else class="grid gap-4 lg:grid-cols-[0.8fr_1.2fr]">
        <UCard>
          <template #header>
            <div class="flex items-center gap-2">
              <UIcon name="i-lucide-settings-2" class="h-5 w-5" />
              <h2 class="font-semibold">Seed target</h2>
            </div>
          </template>

          <div class="space-y-4">
            <UFormField label="Company to seed" required>
              <USelectMenu v-model="seedForm.companyId" :items="companyOptions" value-key="value" label-key="label" placeholder="Select company" class="w-full" />
            </UFormField>

            <UFormField label="AF/SS profile" required>
              <USelectMenu v-model="seedForm.profileCode" :items="profileOptions" value-key="value" label-key="label" placeholder="Select seed profile" class="w-full" />
            </UFormField>

            <div v-if="selectedCompany" class="rounded-2xl bg-slate-50 p-4 text-sm dark:bg-slate-950/50">
              <p class="font-semibold text-slate-900 dark:text-white">{{ selectedCompany.name }}</p>
              <p class="text-slate-500 dark:text-slate-400">Code: {{ selectedCompany.code || '-' }} | GSTIN: {{ selectedCompany.gstin || '-' }}</p>
              <p class="text-slate-500 dark:text-slate-400">Store groups: {{ selectedCompany.storeGroupCount }} | Stores: {{ selectedCompany.storeCount }}</p>
            </div>

            <div v-if="selectedProfile" class="rounded-2xl bg-primary-50 p-4 text-sm text-primary-900 dark:bg-primary-950/30 dark:text-primary-100">
              <p class="font-semibold">{{ selectedProfile.label }}</p>
              <p>{{ selectedProfile.storeName }} | {{ selectedProfile.city }}, {{ selectedProfile.state }}</p>
            </div>

            <USeparator />

            <div class="space-y-3">
              <UCheckbox v-model="seedForm.includeEmployees" label="Seed employees and Manager salesman" />
              <UCheckbox v-model="seedForm.includeUsers" label="Seed default users" help="Admin, Owner and StoreManager are created only if missing." />
              <UCheckbox v-model="seedForm.includeProducts" label="Seed default product masters and opening stock" />
              <UCheckbox v-model="seedForm.resetDefaultUserPasswords" label="Reset default user passwords" help="Only enable when you intentionally want code-prefixed users reset to Admin@1234 / Owner@1234 / StoreManager@1234." />
            </div>

            <UAlert
              color="warning"
              icon="i-lucide-triangle-alert"
              title="Confirm before seeding"
              description="This action is idempotent, but it writes master data into the selected company. Take a backup before running it on live production data."
            />

            <UCheckbox v-model="seedForm.confirm" label="I have selected the correct company and taken a backup if this is production." />
            <UButton color="primary" icon="i-lucide-sparkles" :loading="seeding" :disabled="!canSeed" block @click="seedDefaults">
              Seed selected company
            </UButton>
          </div>
        </UCard>

        <div class="space-y-4">
          <UCard>
            <template #header>
              <div class="flex items-center gap-2">
                <UIcon name="i-lucide-list-checks" class="h-5 w-5" />
                <h2 class="font-semibold">Included defaults</h2>
              </div>
            </template>
            <ul class="list-disc space-y-1 pl-5 text-sm text-slate-600 dark:text-slate-300">
              <li v-for="item in comparison.modelAdjustmentsApplied || []" :key="item">{{ item }}</li>
            </ul>
          </UCard>
        </div>
      </section>

      <section v-if="result" class="grid gap-4 lg:grid-cols-[0.8fr_1.2fr]">
        <UCard>
          <template #header>
            <div class="flex items-center gap-2">
              <UIcon name="i-lucide-check-circle-2" class="h-5 w-5 text-emerald-600" />
              <h2 class="font-semibold">Seed result</h2>
            </div>
          </template>
          <div class="space-y-3 text-sm text-slate-600 dark:text-slate-300">
            <p class="font-semibold text-slate-900 dark:text-white">{{ result.message }}</p>
            <p>Company: {{ result.target?.companyName }}</p>
            <p>Store group: {{ result.target?.storeGroupName }}</p>
            <p>Store: {{ result.target?.storeName }}</p>
            <ul class="list-disc pl-5">
              <li v-for="note in result.notes || []" :key="note">{{ note }}</li>
            </ul>
          </div>
        </UCard>

        <UCard>
          <template #header>
            <h2 class="font-semibold">Created rows</h2>
          </template>
          <div class="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
            <div v-for="entry in countEntries(result.created)" :key="entry.key" class="rounded-2xl bg-slate-50 p-3 text-sm dark:bg-slate-950/50">
              <p class="text-xs uppercase tracking-wide text-slate-500">{{ entry.key }}</p>
              <p class="mt-1 text-xl font-semibold text-slate-950 dark:text-white">{{ entry.count }}</p>
            </div>
          </div>
        </UCard>
      </section>
    </div>
  </AppShell>
</template>
