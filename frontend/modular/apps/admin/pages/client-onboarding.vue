<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between">
        <div>
          <p class="garmetix-dashboard-kicker">
            <UIcon name="i-lucide-user-plus" class="size-4" />
            SaaS onboarding
          </p>
          <h2 class="garmetix-dashboard-title">Client Onboarding</h2>
          <p class="garmetix-dashboard-subtitle">Guided wizard to onboard a new Garmetix client: owner, company, address, store setup and key personnel in one pass.</p>
        </div>
        <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loadingOptions" @click="loadOptions">Refresh</UButton>
      </div>
    </div>

    <UAlert v-if="error" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />

    <section class="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
      <div v-for="card in cards" :key="card.label" class="garmetix-metric-card">
        <p class="garmetix-metric-label">{{ card.label }}</p>
        <p class="garmetix-metric-value">{{ card.value }}</p>
        <p class="garmetix-metric-caption">{{ card.detail }}</p>
      </div>
    </section>

    <!-- Success panel -->
    <section v-if="result" class="garmetix-section-card space-y-4">
      <div class="flex items-center gap-3">
        <UIcon name="i-lucide-check-circle-2" class="size-8 text-success" />
        <div>
          <h3 class="garmetix-panel-title">Onboarding Complete</h3>
          <p class="garmetix-panel-subtitle">{{ result.message }}</p>
        </div>
      </div>
      <div class="grid gap-3 sm:grid-cols-3">
        <div class="garmetix-row-card">
          <p class="text-xs text-muted">Company</p>
          <p class="font-semibold text-highlighted">{{ result.target?.companyName }}</p>
          <p class="text-xs text-muted">Code: {{ result.target?.companyCode }}</p>
        </div>
        <div class="garmetix-row-card">
          <p class="text-xs text-muted">Store Group</p>
          <p class="font-semibold text-highlighted">{{ result.target?.storeGroupName }}</p>
          <p class="text-xs text-muted">Code: {{ result.target?.storeGroupCode }}</p>
        </div>
        <div class="garmetix-row-card">
          <p class="text-xs text-muted">Store</p>
          <p class="font-semibold text-highlighted">{{ result.target?.storeName }}</p>
          <p class="text-xs text-muted">Code: {{ result.target?.storeCode }}</p>
        </div>
      </div>
      <div v-if="result.loginHints?.length" class="space-y-1">
        <p class="text-xs font-semibold uppercase tracking-wider text-muted">Login Hints</p>
        <p v-for="(hint, i) in result.loginHints" :key="i" class="text-sm text-highlighted">- {{ hint }}</p>
      </div>
      <div v-if="result.notes?.length" class="space-y-1">
        <p class="text-xs font-semibold uppercase tracking-wider text-muted">Notes</p>
        <p v-for="(note, i) in result.notes" :key="i" class="text-sm text-muted">- {{ note }}</p>
      </div>
      <div class="flex flex-wrap gap-2 pt-2">
        <UButton color="success" variant="soft" icon="i-lucide-plus-circle" @click="resetWizard">Start Another Onboarding</UButton>
        <UButton color="neutral" variant="ghost" icon="i-lucide-building-2" to="/setup">View In Company Setup</UButton>
      </div>
    </section>

    <!-- Wizard -->
    <section v-else class="grid gap-4 lg:grid-cols-[220px_1fr]">
      <div class="flex flex-row gap-1 overflow-x-auto lg:flex-col lg:overflow-visible">
        <button
          v-for="(step, idx) in steps"
          :key="step.label"
          type="button"
          class="flex shrink-0 items-center gap-3 rounded-md px-3 py-2.5 text-left text-sm transition-colors"
          :class="stepClass(idx)"
          @click="idx < currentStep ? goToStep(idx) : null"
        >
          <span class="flex size-6 shrink-0 items-center justify-center rounded-full text-xs font-bold" :class="stepBadgeClass(idx)">
            <UIcon v-if="idx < currentStep" name="i-lucide-check" class="size-3" />
            <span v-else>{{ idx + 1 }}</span>
          </span>
          <span class="truncate">{{ step.label }}</span>
        </button>
      </div>

      <div class="garmetix-section-card flex flex-col gap-6">
        <!-- Step 0: Owner -->
        <div v-if="currentStep === 0" class="space-y-4">
          <h3 class="garmetix-panel-title">Step 1 - Owner / Client Details</h3>
          <div class="grid gap-4 sm:grid-cols-2">
            <UFormField label="First Name" required>
              <UInput v-model="wizard.client.firstName" placeholder="John" class="w-full" />
            </UFormField>
            <UFormField label="Last Name" required>
              <UInput v-model="wizard.client.lastName" placeholder="Doe" class="w-full" />
            </UFormField>
          </div>
          <div class="grid gap-4 sm:grid-cols-2">
            <UFormField label="Email" required>
              <UInput v-model="wizard.client.email" type="email" placeholder="owner@company.com" class="w-full" />
            </UFormField>
            <UFormField label="Phone Number" required>
              <UInput v-model="wizard.client.phoneNumber" placeholder="+91 9876543210" class="w-full" />
            </UFormField>
          </div>
          <div class="grid gap-4 sm:grid-cols-2">
            <UFormField label="Password" required>
              <UInput v-model="wizard.client.password" type="password" placeholder="Min 8 characters" class="w-full" />
            </UFormField>
            <UFormField label="Date Of Birth">
              <UInput v-model="wizard.client.dateOfBirth" type="date" class="w-full" />
            </UFormField>
          </div>
          <UFormField label="Gender">
            <USelectMenu v-model="wizard.client.gender" :items="options.genders" value-key="value" class="w-full max-w-xs" />
          </UFormField>
        </div>

        <!-- Step 1: Company -->
        <div v-if="currentStep === 1" class="space-y-4">
          <h3 class="garmetix-panel-title">Step 2 - Company Details</h3>
          <UFormField label="Company Name" required>
            <UInput v-model="wizard.company.companyName" placeholder="ABC Enterprises Pvt. Ltd." class="w-full" />
          </UFormField>
          <div class="grid gap-4 sm:grid-cols-2">
            <UFormField label="Company Type">
              <USelectMenu v-model="wizard.company.companyType" :items="options.companyTypes" value-key="value" class="w-full" />
            </UFormField>
            <UFormField label="Store Category">
              <USelectMenu v-model="wizard.company.storeCategory" :items="options.storeCategories" value-key="value" class="w-full" />
            </UFormField>
          </div>
          <div class="grid gap-4 sm:grid-cols-2">
            <UFormField label="GSTIN">
              <UInput v-model="wizard.company.gstin" placeholder="27AAPFU0939F1ZV" class="w-full" />
            </UFormField>
            <UFormField label="PAN">
              <UInput v-model="wizard.company.pan" placeholder="AAPFU0939F" class="w-full" />
            </UFormField>
          </div>
          <div class="grid gap-4 sm:grid-cols-2">
            <UFormField label="Company Email">
              <UInput v-model="wizard.company.companyEmail" type="email" class="w-full" />
            </UFormField>
            <UFormField label="Company Phone">
              <UInput v-model="wizard.company.companyPhoneNumber" class="w-full" />
            </UFormField>
          </div>
          <div class="grid gap-4 sm:grid-cols-2">
            <UFormField label="Date Of Incorporation">
              <UInput v-model="wizard.company.dateOfIncorporation" type="date" class="w-full" />
            </UFormField>
            <UFormField label="CIN">
              <UInput v-model="wizard.company.cin" placeholder="U74999DL2000PTC104430" class="w-full" />
            </UFormField>
          </div>
          <UFormField label="Company Website">
            <UInput v-model="wizard.company.companyWebsite" placeholder="https://example.com" class="w-full" />
          </UFormField>
        </div>

        <!-- Step 2: Address -->
        <div v-if="currentStep === 2" class="space-y-4">
          <h3 class="garmetix-panel-title">Step 3 - Address Details</h3>
          <UFormField label="Street Address" required>
            <UTextarea v-model="wizard.address.streetAddress" placeholder="123, Main Street..." class="w-full" />
          </UFormField>
          <div class="grid gap-4 sm:grid-cols-2">
            <UFormField label="City" required>
              <UInput v-model="wizard.address.city" placeholder="Mumbai" class="w-full" />
            </UFormField>
            <UFormField label="State / Province" required>
              <UInput v-model="wizard.address.stateOrProvince" placeholder="Maharashtra" class="w-full" />
            </UFormField>
          </div>
          <div class="grid gap-4 sm:grid-cols-2">
            <UFormField label="Postal Code" required>
              <UInput v-model="wizard.address.postalCode" placeholder="400001" class="w-full" />
            </UFormField>
            <UFormField label="Country">
              <UInput v-model="wizard.address.country" class="w-full" />
            </UFormField>
          </div>
        </div>

        <!-- Step 3: Configuration -->
        <div v-if="currentStep === 3" class="space-y-4">
          <h3 class="garmetix-panel-title">Step 4 - Configuration</h3>
          <UAlert color="info" variant="subtle" icon="i-lucide-info" title="Short codes" description="These short codes uniquely identify the client, store group and store in the system." />
          <div class="grid gap-4 sm:grid-cols-3">
            <UFormField label="Client Code" required help="e.g. ABC01">
              <UInput v-model="wizard.config.clientCode" placeholder="ABC01" class="w-full" />
            </UFormField>
            <UFormField label="Group Code" required help="e.g. GRP01">
              <UInput v-model="wizard.config.groupCode" placeholder="GRP01" class="w-full" />
            </UFormField>
            <UFormField label="Store Code" required help="e.g. STR01">
              <UInput v-model="wizard.config.storeCode" placeholder="STR01" class="w-full" />
            </UFormField>
          </div>
          <div class="grid gap-4 sm:grid-cols-2">
            <UFormField label="Group Name">
              <UInput v-model="wizard.config.groupName" :placeholder="wizard.company.companyName || 'Main Group'" class="w-full" />
            </UFormField>
            <UFormField label="Store Name">
              <UInput v-model="wizard.config.storeName" placeholder="Main Store" class="w-full" />
            </UFormField>
          </div>
          <UFormField label="Operation Mode">
            <USelectMenu v-model="wizard.config.operationMode" :items="options.appOperations" value-key="value" class="w-full max-w-xs" />
          </UFormField>
          <div class="flex items-center gap-3 rounded-md border border-default bg-elevated p-4">
            <USwitch v-model="wizard.seedBasicStructure" />
            <div>
              <p class="text-sm font-medium text-highlighted">Seed Basic Structure</p>
              <p class="text-xs text-muted">Automatically create default taxes, ledger groups, ledgers, salesman, and product categories.</p>
            </div>
          </div>
        </div>

        <!-- Step 4: Key People -->
        <div v-if="currentStep === 4" class="space-y-4">
          <h3 class="garmetix-panel-title">Step 5 - Key Personnel</h3>
          <div class="space-y-3 rounded-md border border-default p-4">
            <p class="flex items-center gap-2 text-sm font-medium text-highlighted"><UIcon name="i-lucide-user-cog" class="size-4" /> Store Manager</p>
            <div class="grid gap-3 sm:grid-cols-2">
              <UFormField label="Name">
                <UInput v-model="wizard.keyPeople.storeManagerName" class="w-full" />
              </UFormField>
              <UFormField label="Phone">
                <UInput v-model="wizard.keyPeople.storeManagerPhoneNumber" class="w-full" />
              </UFormField>
            </div>
            <UFormField label="Email">
              <UInput v-model="wizard.keyPeople.storeManagerEmail" type="email" class="w-full" />
            </UFormField>
          </div>
          <div class="space-y-3 rounded-md border border-default p-4">
            <p class="flex items-center gap-2 text-sm font-medium text-highlighted"><UIcon name="i-lucide-calculator" class="size-4" /> Accountant</p>
            <div class="grid gap-3 sm:grid-cols-2">
              <UFormField label="Name">
                <UInput v-model="wizard.keyPeople.accountantName" class="w-full" />
              </UFormField>
              <UFormField label="Phone">
                <UInput v-model="wizard.keyPeople.accountantPhoneNumber" class="w-full" />
              </UFormField>
            </div>
            <UFormField label="Email">
              <UInput v-model="wizard.keyPeople.accountantEmail" type="email" class="w-full" />
            </UFormField>
          </div>
          <p class="text-xs text-muted">Leave blank to default to the owner's own name/email/phone from Step 1.</p>
        </div>

        <!-- Step 5: Review -->
        <div v-if="currentStep === 5" class="space-y-4">
          <h3 class="garmetix-panel-title">Step 6 - Review & Submit</h3>
          <div class="divide-y divide-default rounded-md border border-default">
            <div class="grid grid-cols-2 gap-2 p-3 text-sm">
              <span class="text-muted">Owner</span>
              <span class="font-medium text-highlighted">{{ wizard.client.firstName }} {{ wizard.client.lastName }} ({{ wizard.client.email }})</span>
            </div>
            <div class="grid grid-cols-2 gap-2 p-3 text-sm">
              <span class="text-muted">Company</span>
              <span class="font-medium text-highlighted">{{ wizard.company.companyName }}</span>
            </div>
            <div class="grid grid-cols-2 gap-2 p-3 text-sm">
              <span class="text-muted">Location</span>
              <span class="font-medium text-highlighted">{{ wizard.address.city }}, {{ wizard.address.stateOrProvince }}</span>
            </div>
            <div class="grid grid-cols-2 gap-2 p-3 text-sm">
              <span class="text-muted">Codes</span>
              <span class="font-mono text-xs font-medium text-highlighted">Client: {{ wizard.config.clientCode }} - Group: {{ wizard.config.groupCode }} - Store: {{ wizard.config.storeCode }}</span>
            </div>
            <div class="grid grid-cols-2 gap-2 p-3 text-sm">
              <span class="text-muted">Seed Structure</span>
              <UBadge :color="wizard.seedBasicStructure ? 'success' : 'neutral'" variant="subtle" size="sm">{{ wizard.seedBasicStructure ? 'Yes' : 'No' }}</UBadge>
            </div>
          </div>
          <div class="space-y-2">
            <div class="flex items-start gap-3">
              <UCheckbox v-model="wizard.isTermsAccepted" />
              <p class="text-sm text-muted">I accept the Terms and Conditions of using Garmetix.</p>
            </div>
            <div class="flex items-start gap-3">
              <UCheckbox v-model="wizard.isPrivacyPolicyAccepted" />
              <p class="text-sm text-muted">I accept the Privacy Policy.</p>
            </div>
          </div>
        </div>

        <div class="flex items-center justify-between border-t border-default pt-4">
          <UButton v-if="currentStep > 0" variant="ghost" color="neutral" icon="i-lucide-chevron-left" @click="currentStep--">Back</UButton>
          <div v-else />
          <UButton v-if="currentStep < steps.length - 1" color="primary" trailing-icon="i-lucide-chevron-right" @click="nextStep">Next</UButton>
          <UButton
            v-else
            color="success"
            icon="i-lucide-rocket"
            :loading="submitting"
            :disabled="!wizard.isTermsAccepted || !wizard.isPrivacyPolicyAccepted"
            @click="submit"
          >
            Complete Onboarding
          </UButton>
        </div>
      </div>
    </section>
  </section>
</template>

<script setup lang="ts">
import { reactive, ref, computed, onMounted } from 'vue'
import { readText, type ApiRecord, useAdminApiClient } from '../utils/admin-api'

useHead({ title: 'Client Onboarding - Garmetix Admin' })

const toast = useToast()
const { get, post } = useAdminApiClient()

const loadingOptions = ref(true)
const submitting = ref(false)
const error = ref('')
const currentStep = ref(0)
const result = ref<ApiRecord | null>(null)
const summary = ref<ApiRecord | null>(null)

type SelectOption = { label: string, value: number }
const options = reactive({
  companyTypes: [] as SelectOption[],
  storeCategories: [] as SelectOption[],
  appOperations: [] as SelectOption[],
  genders: [] as SelectOption[]
})

const steps = [
  { label: 'Owner Details' },
  { label: 'Company Info' },
  { label: 'Address' },
  { label: 'Configuration' },
  { label: 'Key People' },
  { label: 'Review' }
]

const wizard = reactive({
  client: {
    firstName: '', lastName: '', email: '', password: '', phoneNumber: '',
    dateOfBirth: '', gender: 0
  },
  company: {
    companyName: '', gstin: '', pan: '', cin: '',
    companyType: 0, storeCategory: 0,
    companyEmail: '', companyPhoneNumber: '',
    dateOfIncorporation: '', companyWebsite: ''
  },
  address: {
    streetAddress: '', city: '', stateOrProvince: 'Maharashtra', postalCode: '', country: 'India'
  },
  config: {
    clientCode: '', groupCode: '', storeCode: '', groupName: '', storeName: '',
    operationMode: 0, baseCompanyUrl: ''
  },
  keyPeople: {
    storeManagerName: '', storeManagerEmail: '', storeManagerPhoneNumber: '',
    accountantName: '', accountantEmail: '', accountantPhoneNumber: ''
  },
  seedBasicStructure: true,
  isTermsAccepted: false,
  isPrivacyPolicyAccepted: false
})

const cards = computed(() => [
  { label: 'Companies', value: readText(summary.value, ['companyCount'], '0'), detail: 'Total registered companies' },
  { label: 'Store Groups', value: readText(summary.value, ['storeGroupCount'], '0'), detail: 'Total store groups' },
  { label: 'Stores', value: readText(summary.value, ['storeCount'], '0'), detail: 'Total stores' },
  { label: 'First Company', value: readText(summary.value, ['firstCompanyName'], 'None yet'), detail: readText(summary.value, ['firstCompanyCode'], '') }
])

function unwrapValue(value: unknown) {
  return (value as { value?: unknown })?.value ?? value
}

function stepClass(idx: number) {
  if (currentStep.value === idx) return 'bg-primary/10 font-semibold text-primary'
  if (idx < currentStep.value) return 'text-success cursor-pointer hover:bg-elevated'
  return 'text-muted cursor-not-allowed opacity-60'
}

function stepBadgeClass(idx: number) {
  if (currentStep.value === idx) return 'bg-primary text-inverted'
  if (idx < currentStep.value) return 'bg-success text-inverted'
  return 'bg-elevated text-muted'
}

function goToStep(idx: number) {
  currentStep.value = idx
}

function toDateOrNull(value: string) {
  return value ? value : null
}

function toEnumOptions(list: unknown): SelectOption[] {
  if (!Array.isArray(list)) return []
  return list.map(item => ({
    label: readText(item as ApiRecord, ['label'], readText(item as ApiRecord, ['name'], 'Option')),
    value: Number((item as ApiRecord)?.value ?? 0)
  }))
}

function validateCurrentStep(): string | null {
  if (currentStep.value === 0) {
    if (!wizard.client.firstName || !wizard.client.lastName) return 'First and last name are required.'
    if (!wizard.client.email) return 'Email is required.'
    if (!wizard.client.password || wizard.client.password.length < 8) return 'Password must be at least 8 characters.'
    if (!wizard.client.phoneNumber) return 'Phone number is required.'
  }
  if (currentStep.value === 1) {
    if (!wizard.company.companyName) return 'Company name is required.'
  }
  if (currentStep.value === 2) {
    if (!wizard.address.streetAddress || !wizard.address.city || !wizard.address.stateOrProvince || !wizard.address.postalCode) {
      return 'All address fields are required.'
    }
  }
  if (currentStep.value === 3) {
    if (!wizard.config.clientCode || !wizard.config.groupCode || !wizard.config.storeCode) {
      return 'Client code, Group code and Store code are required.'
    }
  }
  return null
}

function nextStep() {
  const validationError = validateCurrentStep()
  if (validationError) {
    toast.add({ title: 'Validation', description: validationError, color: 'warning' })
    return
  }
  currentStep.value++
}

function resetWizard() {
  result.value = null
  currentStep.value = 0
  Object.assign(wizard, {
    client: { firstName: '', lastName: '', email: '', password: '', phoneNumber: '', dateOfBirth: '', gender: options.genders[0]?.value ?? 0 },
    company: { companyName: '', gstin: '', pan: '', cin: '', companyType: options.companyTypes[0]?.value ?? 0, storeCategory: options.storeCategories[0]?.value ?? 0, companyEmail: '', companyPhoneNumber: '', dateOfIncorporation: '', companyWebsite: '' },
    address: { streetAddress: '', city: '', stateOrProvince: 'Maharashtra', postalCode: '', country: 'India' },
    config: { clientCode: '', groupCode: '', storeCode: '', groupName: '', storeName: '', operationMode: options.appOperations[0]?.value ?? 0, baseCompanyUrl: '' },
    keyPeople: { storeManagerName: '', storeManagerEmail: '', storeManagerPhoneNumber: '', accountantName: '', accountantEmail: '', accountantPhoneNumber: '' },
    seedBasicStructure: true,
    isTermsAccepted: false,
    isPrivacyPolicyAccepted: false
  })
  loadOptions()
}

async function loadOptions() {
  loadingOptions.value = true
  error.value = ''
  try {
    const data = await get<ApiRecord>('client-onboarding/options')
    options.companyTypes = toEnumOptions(data?.companyTypes)
    options.storeCategories = toEnumOptions(data?.storeCategories)
    options.appOperations = toEnumOptions(data?.appOperations)
    options.genders = toEnumOptions(data?.genders)
    if (data?.summary && typeof data.summary === 'object') summary.value = data.summary as ApiRecord
    if (!wizard.company.companyType && options.companyTypes.length) wizard.company.companyType = options.companyTypes[0].value
    if (!wizard.company.storeCategory && options.storeCategories.length) wizard.company.storeCategory = options.storeCategories[0].value
    if (!wizard.config.operationMode && options.appOperations.length) wizard.config.operationMode = options.appOperations[0].value
    if (!wizard.client.gender && options.genders.length) wizard.client.gender = options.genders[0].value
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'Unable to load client onboarding options.'
  } finally {
    loadingOptions.value = false
  }
}

async function submit() {
  if (!wizard.isTermsAccepted || !wizard.isPrivacyPolicyAccepted) {
    toast.add({ title: 'Terms Required', description: 'Please accept the terms and privacy policy.', color: 'warning' })
    return
  }
  submitting.value = true
  error.value = ''
  try {
    const body = {
      clientDetails: {
        firstName: wizard.client.firstName,
        lastName: wizard.client.lastName,
        email: wizard.client.email,
        password: wizard.client.password,
        phoneNumber: wizard.client.phoneNumber,
        dateOfBirth: toDateOrNull(wizard.client.dateOfBirth),
        gender: Number(unwrapValue(wizard.client.gender))
      },
      companyDetails: {
        companyName: wizard.company.companyName,
        gSTIN: wizard.company.gstin,
        pAN: wizard.company.pan,
        cIN: wizard.company.cin || null,
        companyType: Number(unwrapValue(wizard.company.companyType)),
        storeCategory: Number(unwrapValue(wizard.company.storeCategory)),
        companyEmail: wizard.company.companyEmail,
        companyPhoneNumber: wizard.company.companyPhoneNumber,
        dateOfIncorporation: toDateOrNull(wizard.company.dateOfIncorporation),
        companyWebsite: wizard.company.companyWebsite || null
      },
      addressDetails: {
        streetAddress: wizard.address.streetAddress,
        city: wizard.address.city,
        stateOrProvince: wizard.address.stateOrProvince,
        postalCode: wizard.address.postalCode,
        country: wizard.address.country
      },
      companyConfig: {
        clientCode: wizard.config.clientCode,
        groupCode: wizard.config.groupCode,
        storeCode: wizard.config.storeCode,
        operationMode: Number(unwrapValue(wizard.config.operationMode)),
        groupName: wizard.config.groupName || wizard.company.companyName,
        storeName: wizard.config.storeName || 'Main Store',
        baseCompanyUrl: wizard.config.baseCompanyUrl || null
      },
      keyPersonalDetails: {
        storeManagerName: wizard.keyPeople.storeManagerName || `${wizard.client.firstName} ${wizard.client.lastName}`.trim(),
        storeManagerEmail: wizard.keyPeople.storeManagerEmail || wizard.client.email,
        storeManagerPhoneNumber: wizard.keyPeople.storeManagerPhoneNumber || wizard.client.phoneNumber,
        accountantName: wizard.keyPeople.accountantName || `${wizard.client.firstName} ${wizard.client.lastName}`.trim(),
        accountantEmail: wizard.keyPeople.accountantEmail || wizard.client.email,
        accountantPhoneNumber: wizard.keyPeople.accountantPhoneNumber || wizard.client.phoneNumber
      },
      seedBasicStructure: wizard.seedBasicStructure,
      isTermsAccepted: wizard.isTermsAccepted,
      isPrivacyPolicyAccepted: wizard.isPrivacyPolicyAccepted
    }

    const response = await post<ApiRecord>('client-onboarding/submit', body)
    result.value = response
    toast.add({ title: 'Onboarding Complete', description: readText(response, ['message'], 'Client onboarded successfully.'), color: 'success' })
  } catch (caught) {
    toast.add({ title: 'Error', description: caught instanceof Error ? caught.message : 'Onboarding failed.', color: 'error' })
  } finally {
    submitting.value = false
  }
}

onMounted(loadOptions)
</script>
