<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const feedback = useUiFeedback()
const isAuthenticated = auth.isAuthenticated
const canSeeAdmin = auth.canSeeAdmin

const loading = ref(false)
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
  try {
    options.value = await api.get<any>('afss-seeder/options')
    if (!seedForm.companyId && companies.value.length) {
      seedForm.companyId = companies.value[0].id
    }
  } catch (error) {
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
    feedback.success('AF/SS default data seeded')
    seedForm.confirm = false
    await refresh()
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
    title="AF/SS Default Seeder"
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
      <section class="overflow-hidden rounded-3xl border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <div class="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
          <div class="space-y-3">
            <div class="flex items-center gap-3">
              <UIcon name="i-lucide-database-backup" class="h-8 w-8 text-primary" />
              <div>
                <p class="text-xs font-semibold uppercase tracking-[0.3em] text-slate-500">Stage 5F</p>
                <h1 class="text-2xl font-bold text-slate-950 dark:text-white">AF/SS default seeder</h1>
              </div>
            </div>
            <p class="max-w-3xl text-sm text-slate-500 dark:text-slate-400">
              Seed Aadwika Fashion / Samrat-style default data into the selected company using the merged MAUI seeder defaults, updated for current ProductGroup/ProductType/StockMovement models.
            </p>
          </div>
          <UButton icon="i-lucide-refresh-cw" :loading="loading" @click="refresh">
            Refresh
          </UButton>
        </div>
      </section>

      <section class="grid gap-4 lg:grid-cols-[0.8fr_1.2fr]">
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
              <p class="text-slate-500 dark:text-slate-400">Code: {{ selectedCompany.code || '-' }} · GSTIN: {{ selectedCompany.gstin || '-' }}</p>
              <p class="text-slate-500 dark:text-slate-400">Store groups: {{ selectedCompany.storeGroupCount }} · Stores: {{ selectedCompany.storeCount }}</p>
            </div>

            <div v-if="selectedProfile" class="rounded-2xl bg-primary-50 p-4 text-sm text-primary-900 dark:bg-primary-950/30 dark:text-primary-100">
              <p class="font-semibold">{{ selectedProfile.label }}</p>
              <p>{{ selectedProfile.storeName }} · {{ selectedProfile.city }}, {{ selectedProfile.state }}</p>
              <p>Source: {{ selectedProfile.source }}</p>
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
                <UIcon name="i-lucide-git-compare" class="h-5 w-5" />
                <h2 class="font-semibold">Seeder comparison</h2>
              </div>
            </template>
            <div class="grid gap-4 lg:grid-cols-2">
              <div>
                <h3 class="text-sm font-semibold text-slate-950 dark:text-white">Common in both files</h3>
                <ul class="mt-2 list-disc space-y-1 pl-5 text-sm text-slate-600 dark:text-slate-300">
                  <li v-for="item in comparison.commonParts || []" :key="item">{{ item }}</li>
                </ul>
              </div>
              <div class="space-y-4">
                <div>
                  <h3 class="text-sm font-semibold text-slate-950 dark:text-white">Extra in Seeder.cs</h3>
                  <ul class="mt-2 list-disc space-y-1 pl-5 text-sm text-slate-600 dark:text-slate-300">
                    <li v-for="item in comparison.seederCsOnly || []" :key="item">{{ item }}</li>
                  </ul>
                </div>
                <div>
                  <h3 class="text-sm font-semibold text-slate-950 dark:text-white">Extra in seeder2.cs</h3>
                  <ul class="mt-2 list-disc space-y-1 pl-5 text-sm text-slate-600 dark:text-slate-300">
                    <li v-for="item in comparison.seeder2CsOnly || []" :key="item">{{ item }}</li>
                  </ul>
                </div>
              </div>
            </div>
          </UCard>

          <UCard>
            <template #header>
              <div class="flex items-center gap-2">
                <UIcon name="i-lucide-code-2" class="h-5 w-5" />
                <h2 class="font-semibold">Model changes accommodated</h2>
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
