<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const feedback = useUiFeedback()

const isAuthenticated = auth.isAuthenticated
const canSeeAdmin = auth.canSeeAdmin
const loading = ref(false)
const submitting = ref(false)
const options = ref<any | null>(null)
const result = ref<any | null>(null)
const activeStep = ref(0)

const steps = [
  { label: 'Client', icon: 'i-lucide-user-round' },
  { label: 'Company', icon: 'i-lucide-building-2' },
  { label: 'Address', icon: 'i-lucide-map-pin' },
  { label: 'Config', icon: 'i-lucide-settings-2' },
  { label: 'Key People', icon: 'i-lucide-users-round' },
  { label: 'Review', icon: 'i-lucide-check-circle-2' }
]

const today = new Date().toISOString().slice(0, 10)
const eighteenYearsAgo = new Date(new Date().setFullYear(new Date().getFullYear() - 18)).toISOString().slice(0, 10)

const form = reactive({
  clientDetails: {
    firstName: '',
    lastName: '',
    email: '',
    password: 'Admin@1234',
    phoneNumber: '',
    dateOfBirth: eighteenYearsAgo,
    gender: 0
  },
  companyDetails: {
    companyName: '',
    gstin: '',
    pan: '',
    companyType: 0,
    companyEmail: '',
    companyPhoneNumber: '',
    dateOfIncorporation: today,
    storeCategory: 0,
    companyWebsite: '',
    companyDescription: '',
    companyTagline: '',
    cin: 'NA'
  },
  addressDetails: {
    streetAddress: '',
    city: 'Dumka',
    stateOrProvince: 'Jharkhand',
    postalCode: '814101',
    country: 'India'
  },
  companyConfig: {
    clientCode: '',
    groupCode: 'MBO',
    storeCode: '',
    operationMode: 2,
    groupName: '',
    storeName: '',
    baseCompanyUrl: ''
  },
  keyPersonalDetails: {
    storeManagerName: '',
    storeManagerEmail: '',
    storeManagerPhoneNumber: '',
    accountantName: '',
    accountantEmail: '',
    accountantPhoneNumber: ''
  },
  seedBasicStructure: true,
  isTermsAccepted: false,
  isPrivacyPolicyAccepted: false
})

const companyTypeOptions = computed(() => mapEnumOptions(options.value?.companyTypes || []))
const storeCategoryOptions = computed(() => mapEnumOptions(options.value?.storeCategories || []))
const appOperationOptions = computed(() => mapEnumOptions(options.value?.appOperations || []))
const genderOptions = computed(() => mapEnumOptions(options.value?.genders || []))
const summary = computed(() => options.value?.summary || {})
const flowSteps = computed(() => options.value?.flowSteps || [])
const modelMappingNotes = computed(() => options.value?.modelMappingNotes || [])
const canSubmit = computed(() => form.isTermsAccepted && form.isPrivacyPolicyAccepted && !submitting.value)

function mapEnumOptions(values: any[]) {
  return values.map((item) => ({ label: item.label, value: item.value }))
}

function nextStep() {
  activeStep.value = Math.min(activeStep.value + 1, steps.length - 1)
}

function previousStep() {
  activeStep.value = Math.max(activeStep.value - 1, 0)
}

function applyCodeHints() {
  const code = form.companyConfig.clientCode.trim().toUpperCase()
  if (!form.companyConfig.storeCode && code) {
    form.companyConfig.storeCode = `${code}01`.slice(0, 8)
  }
  if (!form.companyConfig.baseCompanyUrl && code) {
    form.companyConfig.baseCompanyUrl = `${code.toLowerCase()}.garmetix.local`
  }
}

async function refresh() {
  if (!auth.isAuthenticated.value || !auth.canSeeAdmin.value) return
  loading.value = true
  try {
    options.value = await api.get<any>('client-onboarding/options')
  } catch (error) {
    feedback.failed('Onboarding options load failed', error)
  } finally {
    loading.value = false
  }
}

async function submitOnboarding() {
  if (!canSubmit.value) return
  submitting.value = true
  result.value = null
  try {
    result.value = await api.create<any>('client-onboarding/submit', form)
    feedback.success(result.value?.message || 'Company onboarded successfully')
    activeStep.value = steps.length - 1
    refresh().catch((error) => feedback.failed('Post-save refresh failed', error))
  } catch (error) {
    feedback.failed('Company onboarding failed', error)
  } finally {
    submitting.value = false
  }
}

function createdEntries(value: any) {
  if (!value || typeof value !== 'object') return []
  return Object.entries(value).map(([key, count]) => ({ key, count }))
}

onMounted(refresh)
</script>

<template>
  <AppShell title="First Company Onboarding" @refresh="refresh">
    <div v-if="!isAuthenticated" class="rounded-3xl border border-dashed border-slate-300 p-8 text-center text-sm text-slate-500 dark:border-slate-700 dark:text-slate-400">
      Login as admin to onboard the first company.
    </div>

    <div v-else-if="!canSeeAdmin" class="rounded-3xl border border-dashed border-amber-300 bg-amber-50 p-8 text-center text-sm text-amber-700 dark:border-amber-700 dark:bg-amber-950/40 dark:text-amber-200">
      Client onboarding is available only for admin users.
    </div>

    <div v-else class="space-y-6">
      <section class="rounded-3xl border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <div class="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
          <div class="space-y-3">
            <div class="flex items-center gap-3">
              <UIcon name="i-lucide-route" class="h-8 w-8 text-primary" />
              <div>
                <p class="text-xs font-semibold uppercase tracking-[0.3em] text-slate-500">Stage 6A</p>
                <h1 class="text-2xl font-bold text-slate-950 dark:text-white">First company onboarding</h1>
              </div>
            </div>
            <p class="max-w-3xl text-sm text-slate-500 dark:text-slate-400">
              Step-by-step setup for the first client company, owner user, store group, store, key people, and basic master structure. This follows the MAUI onboarding flow and writes to the current web data model.
            </p>
          </div>
          <UButton icon="i-lucide-refresh-cw" :loading="loading" @click="refresh">
            Refresh
          </UButton>
        </div>
      </section>

      <section class="grid gap-4 lg:grid-cols-[0.75fr_1.25fr]">
        <div class="space-y-4">
          <UCard>
            <template #header>
              <div class="flex items-center gap-2">
                <UIcon name="i-lucide-list-checks" class="h-5 w-5" />
                <h2 class="font-semibold">Steps</h2>
              </div>
            </template>
            <div class="space-y-2">
              <button
                v-for="(step, index) in steps"
                :key="step.label"
                class="flex w-full items-center gap-3 rounded-2xl px-4 py-3 text-left text-sm transition"
                :class="activeStep === index ? 'bg-primary-50 text-primary-700 dark:bg-primary-950/40 dark:text-primary-200' : 'hover:bg-slate-50 dark:hover:bg-slate-800'"
                @click="activeStep = index"
              >
                <span class="flex h-8 w-8 items-center justify-center rounded-full bg-slate-100 text-xs font-bold dark:bg-slate-800">{{ index + 1 }}</span>
                <UIcon :name="step.icon" class="h-4 w-4" />
                <span>{{ step.label }}</span>
              </button>
            </div>
          </UCard>

          <UCard>
            <template #header>
              <h2 class="font-semibold">Current database</h2>
            </template>
            <div class="space-y-2 text-sm text-slate-600 dark:text-slate-300">
              <p>Companies: <strong>{{ summary.companyCount ?? 0 }}</strong></p>
              <p>Store groups: <strong>{{ summary.storeGroupCount ?? 0 }}</strong></p>
              <p>Stores: <strong>{{ summary.storeCount ?? 0 }}</strong></p>
              <p v-if="summary.firstCompanyName">First company: {{ summary.firstCompanyName }} ({{ summary.firstCompanyCode || '-' }})</p>
            </div>
          </UCard>
        </div>

        <UCard>
          <template #header>
            <div class="flex items-center justify-between gap-3">
              <div class="flex items-center gap-2">
                <UIcon :name="steps[activeStep].icon" class="h-5 w-5" />
                <h2 class="font-semibold">{{ steps[activeStep].label }}</h2>
              </div>
              <UBadge color="primary" variant="soft">Step {{ activeStep + 1 }} / {{ steps.length }}</UBadge>
            </div>
          </template>

          <div class="space-y-5">
            <div v-if="activeStep === 0" class="grid gap-4 md:grid-cols-2">
              <UFormField label="First name" required><UInput v-model="form.clientDetails.firstName" /></UFormField>
              <UFormField label="Last name" required><UInput v-model="form.clientDetails.lastName" /></UFormField>
              <UFormField label="Owner email" required><UInput v-model="form.clientDetails.email" type="email" /></UFormField>
              <UFormField label="Owner password" required><UInput v-model="form.clientDetails.password" type="password" /></UFormField>
              <UFormField label="Phone" required><UInput v-model="form.clientDetails.phoneNumber" /></UFormField>
              <UFormField label="Date of birth"><UInput v-model="form.clientDetails.dateOfBirth" type="date" /></UFormField>
              <UFormField label="Gender"><USelectMenu v-model="form.clientDetails.gender" :items="genderOptions" value-key="value" label-key="label" class="w-full" /></UFormField>
            </div>

            <div v-else-if="activeStep === 1" class="grid gap-4 md:grid-cols-2">
              <UFormField label="Company name" required><UInput v-model="form.companyDetails.companyName" /></UFormField>
              <UFormField label="GSTIN" required><UInput v-model="form.companyDetails.gstin" class="uppercase" /></UFormField>
              <UFormField label="PAN" required><UInput v-model="form.companyDetails.pan" class="uppercase" /></UFormField>
              <UFormField label="Company type"><USelectMenu v-model="form.companyDetails.companyType" :items="companyTypeOptions" value-key="value" label-key="label" class="w-full" /></UFormField>
              <UFormField label="Company email" required><UInput v-model="form.companyDetails.companyEmail" type="email" /></UFormField>
              <UFormField label="Company phone" required><UInput v-model="form.companyDetails.companyPhoneNumber" /></UFormField>
              <UFormField label="Start/Incorporation date"><UInput v-model="form.companyDetails.dateOfIncorporation" type="date" /></UFormField>
              <UFormField label="Store category"><USelectMenu v-model="form.companyDetails.storeCategory" :items="storeCategoryOptions" value-key="value" label-key="label" class="w-full" /></UFormField>
              <UFormField label="Website"><UInput v-model="form.companyDetails.companyWebsite" /></UFormField>
              <UFormField label="CIN"><UInput v-model="form.companyDetails.cin" /></UFormField>
            </div>

            <div v-else-if="activeStep === 2" class="grid gap-4 md:grid-cols-2">
              <UFormField label="Street address" required class="md:col-span-2"><UTextarea v-model="form.addressDetails.streetAddress" /></UFormField>
              <UFormField label="City" required><UInput v-model="form.addressDetails.city" /></UFormField>
              <UFormField label="State" required><UInput v-model="form.addressDetails.stateOrProvince" /></UFormField>
              <UFormField label="Postal code" required><UInput v-model="form.addressDetails.postalCode" /></UFormField>
              <UFormField label="Country" required><UInput v-model="form.addressDetails.country" /></UFormField>
            </div>

            <div v-else-if="activeStep === 3" class="grid gap-4 md:grid-cols-2">
              <UFormField label="Client code" required><UInput v-model="form.companyConfig.clientCode" class="uppercase" @blur="applyCodeHints" /></UFormField>
              <UFormField label="Group code" required><UInput v-model="form.companyConfig.groupCode" class="uppercase" /></UFormField>
              <UFormField label="Store code" required><UInput v-model="form.companyConfig.storeCode" class="uppercase" /></UFormField>
              <UFormField label="Operation mode"><USelectMenu v-model="form.companyConfig.operationMode" :items="appOperationOptions" value-key="value" label-key="label" class="w-full" /></UFormField>
              <UFormField label="Group name"><UInput v-model="form.companyConfig.groupName" /></UFormField>
              <UFormField label="Store name"><UInput v-model="form.companyConfig.storeName" /></UFormField>
              <UFormField label="Base company URL"><UInput v-model="form.companyConfig.baseCompanyUrl" /></UFormField>
              <div class="flex items-center rounded-2xl bg-slate-50 p-4 dark:bg-slate-950/50">
                <UCheckbox v-model="form.seedBasicStructure" label="Seed basic structure" help="Banks, taxes, transactions, ledgers, bank account and category masters." />
              </div>
            </div>

            <div v-else-if="activeStep === 4" class="grid gap-4 md:grid-cols-2">
              <UFormField label="Store manager name" required><UInput v-model="form.keyPersonalDetails.storeManagerName" /></UFormField>
              <UFormField label="Store manager email" required><UInput v-model="form.keyPersonalDetails.storeManagerEmail" type="email" /></UFormField>
              <UFormField label="Store manager phone" required><UInput v-model="form.keyPersonalDetails.storeManagerPhoneNumber" /></UFormField>
              <UFormField label="Accountant name" required><UInput v-model="form.keyPersonalDetails.accountantName" /></UFormField>
              <UFormField label="Accountant email" required><UInput v-model="form.keyPersonalDetails.accountantEmail" type="email" /></UFormField>
              <UFormField label="Accountant phone" required><UInput v-model="form.keyPersonalDetails.accountantPhoneNumber" /></UFormField>
            </div>

            <div v-else class="space-y-5">
              <UAlert color="primary" icon="i-lucide-info" title="Review before creating" description="This will create the company, one store group, one store, users, key employees and optional basic masters." />
              <div class="grid gap-4 lg:grid-cols-2">
                <div class="rounded-2xl bg-slate-50 p-4 text-sm dark:bg-slate-950/50">
                  <p class="font-semibold text-slate-950 dark:text-white">{{ form.companyDetails.companyName || 'Company name' }}</p>
                  <p>{{ form.companyConfig.clientCode || '-' }} · GSTIN {{ form.companyDetails.gstin || '-' }}</p>
                  <p>{{ form.addressDetails.city }}, {{ form.addressDetails.stateOrProvince }}</p>
                </div>
                <div class="rounded-2xl bg-slate-50 p-4 text-sm dark:bg-slate-950/50">
                  <p class="font-semibold text-slate-950 dark:text-white">{{ form.companyConfig.storeName || 'Main Store' }}</p>
                  <p>Group: {{ form.companyConfig.groupCode || '-' }} · Store: {{ form.companyConfig.storeCode || '-' }}</p>
                  <p>Owner: {{ form.clientDetails.firstName }} {{ form.clientDetails.lastName }}</p>
                </div>
              </div>
              <div class="space-y-3">
                <UCheckbox v-model="form.isTermsAccepted" label="I confirm the company and client details are correct." />
                <UCheckbox v-model="form.isPrivacyPolicyAccepted" label="I confirm this data may be used to create users and company masters." />
              </div>
              <UButton color="primary" icon="i-lucide-building-2" :loading="submitting" :disabled="!canSubmit" @click="submitOnboarding">
                Create company and basic structure
              </UButton>
            </div>

            <div class="flex items-center justify-between border-t border-slate-200 pt-5 dark:border-slate-800">
              <UButton variant="soft" icon="i-lucide-arrow-left" :disabled="activeStep === 0" @click="previousStep">Back</UButton>
              <UButton v-if="activeStep < steps.length - 1" trailing-icon="i-lucide-arrow-right" @click="nextStep">Next</UButton>
            </div>
          </div>
        </UCard>
      </section>

      <section v-if="result" class="grid gap-4 lg:grid-cols-[0.85fr_1.15fr]">
        <UCard>
          <template #header>
            <div class="flex items-center gap-2">
              <UIcon name="i-lucide-check-circle-2" class="h-5 w-5 text-emerald-600" />
              <h2 class="font-semibold">Onboarding completed</h2>
            </div>
          </template>
          <div class="space-y-3 text-sm text-slate-600 dark:text-slate-300">
            <p class="font-semibold text-slate-900 dark:text-white">{{ result.message }}</p>
            <p>Company: {{ result.target?.companyName }} ({{ result.target?.companyCode }})</p>
            <p>Store group: {{ result.target?.storeGroupName }} ({{ result.target?.storeGroupCode }})</p>
            <p>Store: {{ result.target?.storeName }} ({{ result.target?.storeCode }})</p>
            <ul class="list-disc pl-5">
              <li v-for="hint in result.loginHints || []" :key="hint">{{ hint }}</li>
            </ul>
          </div>
        </UCard>

        <UCard>
          <template #header><h2 class="font-semibold">Created rows</h2></template>
          <div class="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
            <div v-for="entry in createdEntries(result.created)" :key="entry.key" class="rounded-2xl bg-slate-50 p-3 text-sm dark:bg-slate-950/50">
              <p class="text-xs uppercase tracking-wide text-slate-500">{{ entry.key }}</p>
              <p class="mt-1 text-xl font-semibold text-slate-950 dark:text-white">{{ entry.count }}</p>
            </div>
          </div>
        </UCard>
      </section>

      <section class="grid gap-4 lg:grid-cols-2">
        <UCard>
          <template #header><h2 class="font-semibold">MAUI flow mapped</h2></template>
          <ul class="list-disc space-y-1 pl-5 text-sm text-slate-600 dark:text-slate-300">
            <li v-for="item in flowSteps" :key="item">{{ item }}</li>
          </ul>
        </UCard>
        <UCard>
          <template #header><h2 class="font-semibold">Model changes accommodated</h2></template>
          <ul class="list-disc space-y-1 pl-5 text-sm text-slate-600 dark:text-slate-300">
            <li v-for="item in modelMappingNotes" :key="item">{{ item }}</li>
          </ul>
        </UCard>
      </section>
    </div>
  </AppShell>
</template>
