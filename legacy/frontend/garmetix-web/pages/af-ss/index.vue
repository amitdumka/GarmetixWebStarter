<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const feedback = useUiFeedback()
const config = useRuntimeConfig()
const isAuthenticated = auth.isAuthenticated
const canSeeAdmin = auth.canSeeAdmin

const loading = ref(false)
const loadError = ref('')
const seeding = ref(false)
const options = ref<any | null>(null)
const result = ref<any | null>(null)
const importingPortable = ref(false)
const portableImportResult = ref<any | null>(null)
const mergeLoading = ref(false)
const mergePreview = ref<any | null>(null)
const mergeResult = ref<any | null>(null)
const mergeConfirm = ref(false)
const mergeReason = ref('Merge Smart Menswear into Aadwika Fashion MBO as per production seed structure.')
const seederVerificationLoading = ref(false)
const seederVerification = ref<any | null>(null)
const seedForm = reactive({
  companyId: '',
  createNewCompany: true,
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

const canSeed = computed(() => Boolean(seedForm.profileCode && seedForm.confirm && !seeding.value))

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
    if (!seedForm.companyId && companies.value.length && !seedForm.createNewCompany) {
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
      companyId: seedForm.createNewCompany ? null : seedForm.companyId,
      createNewCompany: seedForm.createNewCompany,
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

async function exportPortableSeeder() {
  try {
    const response = await fetch(`${config.public.apiBase}/portable-seeder/export`, {
      headers: api.authHeaders()
    })
    if (!response.ok) {
      throw new Error(await response.text())
    }

    const blob = await response.blob()
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = `garmetix-portable-seeder-${new Date().toISOString().slice(0, 10)}.json`
    document.body.appendChild(link)
    link.click()
    link.remove()
    URL.revokeObjectURL(url)
    feedback.notify('Portable JSON seeder exported', 'Keep this file safely with your backup files.', 'success')
  } catch (error) {
    feedback.failed('Portable seeder export failed', error)
  }
}

async function importPortableSeeder(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return

  importingPortable.value = true
  portableImportResult.value = null
  try {
    const text = await file.text()
    const payload = JSON.parse(text)
    portableImportResult.value = await api.create<any>('portable-seeder/import', payload)
    feedback.success(`Portable seeder imported: ${portableImportResult.value?.rowsProcessed || 0} row(s)`)
    await refresh()
  } catch (error) {
    feedback.failed('Portable seeder import failed', error)
  } finally {
    importingPortable.value = false
    input.value = ''
  }
}

async function previewAfSmartMerge() {
  mergeLoading.value = true
  mergeResult.value = null
  try {
    mergePreview.value = await api.get<any>('company-merge/af-smart/preview')
    feedback.notify('Merge preview ready', mergePreview.value?.status || 'Preview loaded.', 'success')
  } catch (error) {
    feedback.failed('Merge preview failed', error)
  } finally {
    mergeLoading.value = false
  }
}

async function applyAfSmartMerge() {
  if (!mergeConfirm.value) {
    feedback.notify('Confirm required', 'Tick confirmation before applying the merge.', 'warning')
    return
  }

  mergeLoading.value = true
  try {
    mergeResult.value = await api.create<any>('company-merge/af-smart/apply', {
      confirm: true,
      reason: mergeReason.value
    })
    feedback.success(`Merge applied: ${mergeResult.value?.rowsUpdated || 0} row(s) updated`)
    mergeConfirm.value = false
    await previewAfSmartMerge()
    await refresh()
  } catch (error) {
    feedback.failed('Merge apply failed', error)
  } finally {
    mergeLoading.value = false
  }
}

async function refreshSeederVerification() {
  seederVerificationLoading.value = true
  try {
    seederVerification.value = await api.get<any>('seeder-verification/status')
    feedback.notify('Seeder verification refreshed', seederVerification.value?.ready ? 'Seeder structure is ready.' : 'Seeder structure needs action.', seederVerification.value?.ready ? 'success' : 'warning')
  } catch (error) {
    feedback.failed('Seeder verification failed', error)
  } finally {
    seederVerificationLoading.value = false
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
        description="Create a full company/store seed, export current data as portable JSON, or import a saved JSON seeder after crash/migration."
        icon="i-lucide-database-backup"
      >
        <template #actions>
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="subtle" :loading="loading" label="Refresh" @click="refresh" />
          <UButton icon="i-lucide-download" color="primary" variant="subtle" label="Export portable JSON" @click="exportPortableSeeder" />
          <UButton icon="i-lucide-shield-check" color="success" variant="subtle" :loading="seederVerificationLoading" label="Verify seeder" @click="refreshSeederVerification" />
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
            <UCheckbox v-model="seedForm.createNewCompany" label="Create company/store group/store automatically from selected seed profile" help="Recommended for crash recovery, new system migration, and fresh seed." />

            <UFormField v-if="!seedForm.createNewCompany" label="Existing company to update">
              <USelectMenu v-model="seedForm.companyId" :items="companyOptions" value-key="value" label-key="label" placeholder="Select existing company" class="w-full" />
            </UFormField>

            <UAlert
              v-if="seedForm.createNewCompany"
              color="primary"
              icon="i-lucide-building-2"
              title="Seeder will create the company"
              description="No need to create company first. The selected AF/SS profile will create company, store group, store, Indian accounting defaults, users, employees and products as selected."
            />

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
              description="This action is idempotent. It can create a new company/store data set or update existing default rows. Take a backup before running it on live production data."
            />

            <UCheckbox v-model="seedForm.confirm" label="I confirm this seeder/export/import operation and have taken a backup if this is production." />
            <UButton color="primary" icon="i-lucide-sparkles" :loading="seeding" :disabled="!canSeed" block @click="seedDefaults">
              Run selected seed
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

          <UCard>
            <template #header>
              <div class="flex items-center gap-2">
                <UIcon name="i-lucide-file-json-2" class="h-5 w-5" />
                <h2 class="font-semibold">Portable JSON Seeder</h2>
              </div>
            </template>
            <div class="space-y-3 text-sm text-slate-600 dark:text-slate-300">
              <p>Export current database data into a portable JSON file. Import it on a new/crashed system to recreate data by upsert.</p>
              <div class="flex flex-wrap gap-2">
                <UButton icon="i-lucide-download" color="primary" variant="soft" label="Create seeder file from current data" @click="exportPortableSeeder" />
                <label class="inline-flex cursor-pointer items-center gap-2 rounded-xl border border-slate-200 px-3 py-2 text-sm font-medium dark:border-slate-700">
                  <UIcon name="i-lucide-upload" />
                  <span>{{ importingPortable ? 'Importing...' : 'Import JSON seeder' }}</span>
                  <input type="file" accept="application/json,.json" class="hidden" :disabled="importingPortable" @change="importPortableSeeder" />
                </label>
              </div>
              <UAlert
                v-if="portableImportResult"
                color="success"
                icon="i-lucide-check-circle-2"
                title="Portable seeder imported"
                :description="`${portableImportResult.rowsProcessed || 0} row(s), ${portableImportResult.tablesProcessed || 0} table(s). Run Data Consistency after import.`"
              />
            </div>
          </UCard>

          <UCard>
            <template #header>
              <div class="flex items-center gap-2">
                <UIcon name="i-lucide-git-merge" class="h-5 w-5" />
                <h2 class="font-semibold">Aadwika + Smart Menswear Merge</h2>
              </div>
            </template>
            <div class="space-y-3 text-sm text-slate-600 dark:text-slate-300">
              <p>Merge any existing separate Smart/Samrat Menswear company/store-group data into Aadwika Fashion → Aadwika Fashion MBO → Smart Menswear. Shalini profile is excluded.</p>
              <div class="flex flex-wrap gap-2">
                <UButton icon="i-lucide-search-check" color="primary" variant="soft" :loading="mergeLoading" label="Preview merge" @click="previewAfSmartMerge" />
                <UButton icon="i-lucide-git-merge" color="warning" variant="soft" :loading="mergeLoading" :disabled="!mergePreview || !mergeConfirm" label="Apply merge" @click="applyAfSmartMerge" />
              </div>
              <UCheckbox v-model="mergeConfirm" label="I confirm this merge should move Smart Menswear/Samrat rows under Aadwika Fashion MBO." />
              <UFormField label="Reason / note">
                <UTextarea v-model="mergeReason" :rows="2" />
              </UFormField>
              <UAlert
                v-if="mergePreview"
                :color="mergePreview.status === 'Ready' ? 'warning' : 'success'"
                icon="i-lucide-info"
                :title="mergePreview.status"
                :description="`Preview rows: ${(mergePreview.tables || []).reduce((sum, row) => sum + (row.rowCount || 0), 0)}. Target company: ${mergePreview.targetCompanyId || 'will be created'}.`"
              />
              <div v-if="mergePreview?.tables?.length" class="rounded-2xl border border-slate-200 p-3 dark:border-slate-700">
                <div v-for="row in mergePreview.tables.slice(0, 8)" :key="`${row.table}-${row.column}-${row.action}`" class="flex justify-between gap-3 border-b border-slate-100 py-2 last:border-0 dark:border-slate-800">
                  <span>{{ row.table }}.{{ row.column }}</span>
                  <strong>{{ row.rowCount }}</strong>
                </div>
              </div>
              <UAlert
                v-if="mergeResult"
                color="success"
                icon="i-lucide-check-circle-2"
                title="Merge applied"
                :description="`${mergeResult.rowsUpdated || 0} row(s) updated in ${mergeResult.tablesUpdated || 0} table(s). Run Data Consistency after merge.`"
              />
            </div>
          </UCard>

          <UCard>
            <template #header>
              <div class="flex items-center gap-2">
                <UIcon name="i-lucide-shield-check" class="h-5 w-5" />
                <h2 class="font-semibold">Seeder/Merge Verification</h2>
              </div>
            </template>
            <div class="space-y-3 text-sm text-slate-600 dark:text-slate-300">
              <p>Verify Aadwika Fashion, Aadwika Fashion MBO, Smart Menswear, Shalini separation and protected default accounting masters after seed/import/merge.</p>
              <UButton icon="i-lucide-refresh-cw" color="success" variant="soft" :loading="seederVerificationLoading" label="Run verification" @click="refreshSeederVerification" />
              <UAlert
                v-if="seederVerification"
                :color="seederVerification.ready ? 'success' : 'warning'"
                :icon="seederVerification.ready ? 'i-lucide-check-circle-2' : 'i-lucide-circle-alert'"
                :title="seederVerification.ready ? 'Seeder structure ready' : 'Seeder structure needs action'"
                :description="`${(seederVerification.checks || []).filter((check) => check.passed).length}/${(seederVerification.checks || []).length} check(s) passed.`"
              />
              <div v-if="seederVerification?.checks?.length" class="space-y-2">
                <div v-for="check in seederVerification.checks" :key="check.key" class="rounded-2xl border border-slate-200 p-3 dark:border-slate-700">
                  <div class="flex items-start justify-between gap-3">
                    <div>
                      <p class="font-semibold text-slate-900 dark:text-white">{{ check.label }}</p>
                      <p class="text-xs text-slate-500">{{ check.message }}</p>
                    </div>
                    <UBadge :color="check.passed ? 'success' : 'warning'" :label="check.status" variant="subtle" />
                  </div>
                </div>
              </div>
            </div>
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
