<template>
  <div class="flex flex-col h-full p-6 gap-5">

    <!-- Header -->
    <div class="flex items-center justify-between">
      <div>
        <h1 class="text-2xl font-bold text-gray-900 dark:text-white">Client Onboarding</h1>
        <p class="text-sm text-gray-500 dark:text-gray-400 mt-0.5">Full wizard to onboard a new Garmetix client with company, store group, store and users</p>
      </div>
      <UButton variant="soft" color="neutral" icon="i-lucide-refresh-cw" :loading="loadingOptions" @click="loadOptions">Refresh</UButton>
    </div>

    <!-- Summary cards -->
    <div class="grid grid-cols-4 gap-4">
      <div class="rounded-xl border border-gray-200 dark:border-gray-800 bg-gray-50 dark:bg-gray-900 p-4">
        <p class="text-xs text-gray-500 dark:text-gray-400 uppercase tracking-wider">Companies</p>
        <p class="text-3xl font-bold text-gray-900 dark:text-white mt-1">{{ summary?.companyCount ?? 0 }}</p>
      </div>
      <div class="rounded-xl border border-gray-200 dark:border-gray-800 bg-gray-50 dark:bg-gray-900 p-4">
        <p class="text-xs text-gray-500 dark:text-gray-400 uppercase tracking-wider">Store Groups</p>
        <p class="text-3xl font-bold text-gray-900 dark:text-white mt-1">{{ summary?.storeGroupCount ?? 0 }}</p>
      </div>
      <div class="rounded-xl border border-gray-200 dark:border-gray-800 bg-gray-50 dark:bg-gray-900 p-4">
        <p class="text-xs text-gray-500 dark:text-gray-400 uppercase tracking-wider">Stores</p>
        <p class="text-3xl font-bold text-gray-900 dark:text-white mt-1">{{ summary?.storeCount ?? 0 }}</p>
      </div>
      <div class="rounded-xl border border-gray-200 dark:border-gray-800 p-4 flex flex-col justify-between">
        <p class="text-xs text-gray-500 dark:text-gray-400 uppercase tracking-wider">First Company</p>
        <p class="text-sm font-semibold text-gray-700 dark:text-gray-300 mt-1 truncate">{{ summary?.firstCompanyName ?? 'None yet' }}</p>
        <p class="text-xs text-gray-400">{{ summary?.firstCompanyCode || '' }}</p>
      </div>
    </div>

    <!-- Result Panel (after successful onboarding) -->
    <div v-if="result" class="rounded-xl border border-green-200 dark:border-green-800 bg-green-50 dark:bg-green-950 p-5 space-y-4">
      <div class="flex items-center gap-3">
        <UIcon name="i-lucide-check-circle-2" class="size-8 text-green-500" />
        <div>
          <h3 class="font-bold text-green-800 dark:text-green-200 text-lg">Onboarding Complete!</h3>
          <p class="text-green-700 dark:text-green-300 text-sm">{{ result.message }}</p>
        </div>
      </div>
      <div class="grid grid-cols-3 gap-4 text-sm">
        <div class="rounded-lg bg-white dark:bg-green-900 p-3 border border-green-200 dark:border-green-700">
          <p class="text-xs text-gray-500 mb-1">Company</p>
          <p class="font-semibold">{{ result.target?.companyName }}</p>
          <p class="text-xs text-gray-400">Code: {{ result.target?.companyCode }}</p>
        </div>
        <div class="rounded-lg bg-white dark:bg-green-900 p-3 border border-green-200 dark:border-green-700">
          <p class="text-xs text-gray-500 mb-1">Store Group</p>
          <p class="font-semibold">{{ result.target?.storeGroupName }}</p>
          <p class="text-xs text-gray-400">Code: {{ result.target?.storeGroupCode }}</p>
        </div>
        <div class="rounded-lg bg-white dark:bg-green-900 p-3 border border-green-200 dark:border-green-700">
          <p class="text-xs text-gray-500 mb-1">Store</p>
          <p class="font-semibold">{{ result.target?.storeName }}</p>
          <p class="text-xs text-gray-400">Code: {{ result.target?.storeCode }}</p>
        </div>
      </div>
      <div v-if="result.loginHints?.length" class="space-y-1">
        <p class="text-xs font-semibold text-green-700 dark:text-green-300 uppercase tracking-wider">Login Hints</p>
        <p v-for="(hint, i) in result.loginHints" :key="i" class="text-sm text-green-800 dark:text-green-200">• {{ hint }}</p>
      </div>
      <div class="flex gap-3 pt-2">
        <UButton color="success" variant="soft" icon="i-lucide-plus-circle" @click="resetWizard">Start Another Onboarding</UButton>
        <UButton color="neutral" variant="ghost" :to="`/setup`">View in Company Setup</UButton>
      </div>
    </div>

    <!-- Wizard -->
    <div v-else class="flex gap-6 flex-1 min-h-0">

      <!-- Step Navigator Sidebar -->
      <div class="w-52 shrink-0 flex flex-col gap-1">
        <button v-for="(step, idx) in steps" :key="idx"
          class="flex items-center gap-3 px-3 py-2.5 rounded-lg text-left transition-all"
          :class="currentStep === idx
            ? 'bg-primary-50 dark:bg-primary-950 text-primary-700 dark:text-primary-300 font-semibold'
            : idx < currentStep
            ? 'text-green-600 dark:text-green-400'
            : 'text-gray-400 dark:text-gray-600 cursor-not-allowed'"
          @click="idx < currentStep ? goToStep(idx) : null"
        >
          <span class="size-6 rounded-full flex items-center justify-center text-xs font-bold shrink-0"
            :class="currentStep === idx
              ? 'bg-primary-600 text-white'
              : idx < currentStep
              ? 'bg-green-500 text-white'
              : 'bg-gray-200 dark:bg-gray-700 text-gray-500'"
          >
            <UIcon v-if="idx < currentStep" name="i-lucide-check" class="size-3" />
            <span v-else>{{ idx + 1 }}</span>
          </span>
          <span class="text-sm truncate">{{ step.label }}</span>
        </button>
      </div>

      <!-- Step Content -->
      <div class="flex-1 rounded-xl border border-gray-200 dark:border-gray-800 bg-white dark:bg-gray-900 overflow-auto">
        <div class="p-6">

          <!-- STEP 0: Client Details -->
          <div v-if="currentStep === 0">
            <h2 class="text-lg font-bold mb-4">Step 1 — Owner / Client Details</h2>
            <div class="space-y-4 max-w-2xl">
              <div class="grid grid-cols-2 gap-4">
                <UFormField label="First Name" required>
                  <UInput v-model="wizard.client.firstName" placeholder="John" class="w-full" />
                </UFormField>
                <UFormField label="Last Name" required>
                  <UInput v-model="wizard.client.lastName" placeholder="Doe" class="w-full" />
                </UFormField>
              </div>
              <div class="grid grid-cols-2 gap-4">
                <UFormField label="Email" required>
                  <UInput v-model="wizard.client.email" type="email" placeholder="owner@company.com" class="w-full" />
                </UFormField>
                <UFormField label="Phone Number" required>
                  <UInput v-model="wizard.client.phoneNumber" placeholder="+91 9876543210" class="w-full" />
                </UFormField>
              </div>
              <div class="grid grid-cols-2 gap-4">
                <UFormField label="Password" required>
                  <UInput v-model="wizard.client.password" type="password" placeholder="Min 8 characters" class="w-full" />
                </UFormField>
                <UFormField label="Date of Birth">
                  <UInput v-model="wizard.client.dateOfBirth" type="date" class="w-full" />
                </UFormField>
              </div>
              <UFormField label="Gender">
                <USelectMenu v-model="wizard.client.gender" :items="options.genders" value-key="value" class="w-full max-w-xs" />
              </UFormField>
            </div>
          </div>

          <!-- STEP 1: Company Details -->
          <div v-if="currentStep === 1">
            <h2 class="text-lg font-bold mb-4">Step 2 — Company Details</h2>
            <div class="space-y-4 max-w-2xl">
              <UFormField label="Company Name" required>
                <UInput v-model="wizard.company.companyName" placeholder="ABC Enterprises Pvt. Ltd." class="w-full" />
              </UFormField>
              <div class="grid grid-cols-2 gap-4">
                <UFormField label="Company Type">
                  <USelectMenu v-model="wizard.company.companyType" :items="options.companyTypes" value-key="value" class="w-full" />
                </UFormField>
                <UFormField label="Store Category">
                  <USelectMenu v-model="wizard.company.storeCategory" :items="options.storeCategories" value-key="value" class="w-full" />
                </UFormField>
              </div>
              <div class="grid grid-cols-2 gap-4">
                <UFormField label="GSTIN">
                  <UInput v-model="wizard.company.gstin" placeholder="27AAPFU0939F1ZV" class="w-full" />
                </UFormField>
                <UFormField label="PAN">
                  <UInput v-model="wizard.company.pan" placeholder="AAPFU0939F" class="w-full" />
                </UFormField>
              </div>
              <div class="grid grid-cols-2 gap-4">
                <UFormField label="Company Email">
                  <UInput v-model="wizard.company.companyEmail" type="email" class="w-full" />
                </UFormField>
                <UFormField label="Company Phone">
                  <UInput v-model="wizard.company.companyPhoneNumber" class="w-full" />
                </UFormField>
              </div>
              <div class="grid grid-cols-2 gap-4">
                <UFormField label="Date of Incorporation">
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
          </div>

          <!-- STEP 2: Address -->
          <div v-if="currentStep === 2">
            <h2 class="text-lg font-bold mb-4">Step 3 — Address Details</h2>
            <div class="space-y-4 max-w-2xl">
              <UFormField label="Street Address" required>
                <UTextarea v-model="wizard.address.streetAddress" placeholder="123, Main Street..." class="w-full" />
              </UFormField>
              <div class="grid grid-cols-2 gap-4">
                <UFormField label="City" required>
                  <UInput v-model="wizard.address.city" placeholder="Mumbai" class="w-full" />
                </UFormField>
                <UFormField label="State / Province" required>
                  <UInput v-model="wizard.address.stateOrProvince" placeholder="Maharashtra" class="w-full" />
                </UFormField>
              </div>
              <div class="grid grid-cols-2 gap-4">
                <UFormField label="Postal Code" required>
                  <UInput v-model="wizard.address.postalCode" placeholder="400001" class="w-full" />
                </UFormField>
                <UFormField label="Country">
                  <UInput v-model="wizard.address.country" class="w-full" />
                </UFormField>
              </div>
            </div>
          </div>

          <!-- STEP 3: Config -->
          <div v-if="currentStep === 3">
            <h2 class="text-lg font-bold mb-4">Step 4 — Configuration</h2>
            <div class="space-y-4 max-w-2xl">
              <UAlert color="info" variant="subtle" icon="i-lucide-info" title="Short codes"
                description="These short codes uniquely identify the client, store group, and store in the system." />
              <div class="grid grid-cols-3 gap-4">
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
              <div class="grid grid-cols-2 gap-4">
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
              <div class="flex items-center gap-3 p-4 rounded-lg border border-gray-200 dark:border-gray-800">
                <USwitch v-model="wizard.seedBasicStructure" />
                <div>
                  <p class="font-semibold text-sm">Seed Basic Structure</p>
                  <p class="text-xs text-gray-500">Automatically create default taxes, ledger groups, ledgers, salesman, and product categories</p>
                </div>
              </div>
            </div>
          </div>

          <!-- STEP 4: Key People -->
          <div v-if="currentStep === 4">
            <h2 class="text-lg font-bold mb-4">Step 5 — Key Personnel</h2>
            <div class="space-y-6 max-w-2xl">
              <div class="rounded-lg border border-gray-200 dark:border-gray-800 p-4 space-y-3">
                <p class="font-semibold text-sm flex items-center gap-2"><UIcon name="i-lucide-user-cog" class="size-4" /> Store Manager</p>
                <div class="grid grid-cols-2 gap-3">
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
              <div class="rounded-lg border border-gray-200 dark:border-gray-800 p-4 space-y-3">
                <p class="font-semibold text-sm flex items-center gap-2"><UIcon name="i-lucide-calculator" class="size-4" /> Accountant</p>
                <div class="grid grid-cols-2 gap-3">
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
            </div>
          </div>

          <!-- STEP 5: Review -->
          <div v-if="currentStep === 5">
            <h2 class="text-lg font-bold mb-4">Step 6 — Review & Submit</h2>
            <div class="space-y-4 max-w-2xl">
              <div class="rounded-lg border border-gray-200 dark:border-gray-800 divide-y divide-gray-100 dark:divide-gray-800">
                <div class="grid grid-cols-2 gap-2 p-3 text-sm">
                  <span class="text-gray-500">Owner</span>
                  <span class="font-medium">{{ wizard.client.firstName }} {{ wizard.client.lastName }} ({{ wizard.client.email }})</span>
                </div>
                <div class="grid grid-cols-2 gap-2 p-3 text-sm">
                  <span class="text-gray-500">Company</span>
                  <span class="font-medium">{{ wizard.company.companyName }}</span>
                </div>
                <div class="grid grid-cols-2 gap-2 p-3 text-sm">
                  <span class="text-gray-500">Location</span>
                  <span class="font-medium">{{ wizard.address.city }}, {{ wizard.address.stateOrProvince }}</span>
                </div>
                <div class="grid grid-cols-2 gap-2 p-3 text-sm">
                  <span class="text-gray-500">Codes</span>
                  <span class="font-medium font-mono text-xs">Client: {{ wizard.config.clientCode }} · Group: {{ wizard.config.groupCode }} · Store: {{ wizard.config.storeCode }}</span>
                </div>
                <div class="grid grid-cols-2 gap-2 p-3 text-sm">
                  <span class="text-gray-500">Seed Structure</span>
                  <UBadge :color="wizard.seedBasicStructure ? 'success' : 'neutral'" variant="subtle" size="sm">{{ wizard.seedBasicStructure ? 'Yes' : 'No' }}</UBadge>
                </div>
              </div>

              <div class="space-y-2 mt-4">
                <div class="flex items-start gap-3">
                  <UCheckbox v-model="wizard.isTermsAccepted" />
                  <p class="text-sm text-gray-600 dark:text-gray-400">I accept the Terms and Conditions of using Garmetix.</p>
                </div>
                <div class="flex items-start gap-3">
                  <UCheckbox v-model="wizard.isPrivacyPolicyAccepted" />
                  <p class="text-sm text-gray-600 dark:text-gray-400">I accept the Privacy Policy.</p>
                </div>
              </div>
            </div>
          </div>

        </div>

        <!-- Navigation Footer -->
        <div class="border-t border-gray-200 dark:border-gray-800 px-6 py-4 flex justify-between items-center bg-gray-50 dark:bg-gray-950">
          <UButton v-if="currentStep > 0" variant="ghost" icon="i-lucide-chevron-left" @click="currentStep--">Back</UButton>
          <div v-else />

          <div v-if="currentStep < steps.length - 1">
            <UButton color="primary" trailing-icon="i-lucide-chevron-right" @click="nextStep">Next</UButton>
          </div>
          <div v-else>
            <UButton
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
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'

useHead({ title: 'Client Onboarding - Garmetix Admin' })

const config = useRuntimeConfig()
const toast  = useToast()

// ── State ──────────────────────────────────────────────────────────────────
const loadingOptions = ref(false)
const submitting     = ref(false)
const currentStep    = ref(0)
const result         = ref<any>(null)
const summary        = ref<any>(null)

const options = reactive({
  companyTypes:  [] as any[],
  storeCategories: [] as any[],
  appOperations: [] as any[],
  genders:       [] as any[]
})

const steps = [
  { label: 'Owner Details' },
  { label: 'Company Info'  },
  { label: 'Address'       },
  { label: 'Configuration' },
  { label: 'Key People'    },
  { label: 'Review'        },
]

// ── Wizard form ────────────────────────────────────────────────────────────
const wizard = reactive({
  client: {
    firstName: '', lastName: '', email: '', password: '', phoneNumber: '',
    dateOfBirth: '', gender: 0
  },
  company: {
    companyName: '', gstin: '', pan: '', cin: '',
    companyType: 0, storeCategory: 6,
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

// ── Helpers ────────────────────────────────────────────────────────────────
function getHeaders() {
  if (!import.meta.client) return {}
  const token = localStorage.getItem('garmetix.token')
  return token ? { Authorization: `Bearer ${token}` } : {}
}

function enumToOptions(list: any[]) {
  return list.map((e: any) => ({ label: e.label, value: e.value }))
}

function goToStep(idx: number) {
  currentStep.value = idx
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
    if (!wizard.address.streetAddress || !wizard.address.city || !wizard.address.stateOrProvince || !wizard.address.postalCode)
      return 'All address fields are required.'
  }
  if (currentStep.value === 3) {
    if (!wizard.config.clientCode || !wizard.config.groupCode || !wizard.config.storeCode)
      return 'Client code, Group code and Store code are required.'
  }
  return null
}

function nextStep() {
  const error = validateCurrentStep()
  if (error) {
    toast.add({ title: 'Validation', description: error, color: 'warning' })
    return
  }
  currentStep.value++
}

function resetWizard() {
  result.value  = null
  currentStep.value = 0
  loadOptions()
}

// ── Load options ───────────────────────────────────────────────────────────
async function loadOptions() {
  loadingOptions.value = true
  try {
    const [optData, sumData] = await Promise.allSettled([
      $fetch(config.public.apiBaseUrl + '/client-onboarding/options', { headers: getHeaders() }) as Promise<any>,
      $fetch(config.public.apiBaseUrl + '/client-onboarding/summary', { headers: getHeaders() }) as Promise<any>
    ])
    if (optData.status === 'fulfilled' && optData.value) {
      const o = optData.value
      options.companyTypes    = enumToOptions(o.companyTypes   || [])
      options.storeCategories = enumToOptions(o.storeCategories|| [])
      options.appOperations   = enumToOptions(o.appOperations  || [])
      options.genders         = enumToOptions(o.genders        || [])
      // Also load summary from inside options if available
      if (o.summary) summary.value = o.summary
    }
    if (sumData.status === 'fulfilled' && sumData.value) {
      summary.value = sumData.value
    }
  } catch (e) {
    console.error('loadOptions:', e)
  } finally {
    loadingOptions.value = false
  }
}

// ── Submit ─────────────────────────────────────────────────────────────────
async function submit() {
  if (!wizard.isTermsAccepted || !wizard.isPrivacyPolicyAccepted) {
    toast.add({ title: 'Terms Required', description: 'Please accept the terms and privacy policy.', color: 'warning' })
    return
  }
  submitting.value = true
  try {
    const clientGender = wizard.client.gender?.valueOf != null ? Number(wizard.client.gender) : wizard.client.gender
    const companyType  = wizard.company.companyType?.valueOf != null ? Number(wizard.company.companyType) : wizard.company.companyType
    const storeCategory= wizard.company.storeCategory?.valueOf != null ? Number(wizard.company.storeCategory) : wizard.company.storeCategory
    const opMode       = wizard.config.operationMode?.valueOf != null ? Number(wizard.config.operationMode) : wizard.config.operationMode

    const body = {
      clientDetails: {
        firstName:   wizard.client.firstName,
        lastName:    wizard.client.lastName,
        email:       wizard.client.email,
        password:    wizard.client.password,
        phoneNumber: wizard.client.phoneNumber,
        dateOfBirth: wizard.client.dateOfBirth || null,
        gender:      clientGender
      },
      companyDetails: {
        companyName:         wizard.company.companyName,
        gSTIN:               wizard.company.gstin,
        pAN:                 wizard.company.pan,
        cIN:                 wizard.company.cin,
        companyType:         companyType,
        storeCategory:       storeCategory,
        companyEmail:        wizard.company.companyEmail,
        companyPhoneNumber:  wizard.company.companyPhoneNumber,
        dateOfIncorporation: wizard.company.dateOfIncorporation || null,
        companyWebsite:      wizard.company.companyWebsite || null
      },
      addressDetails: {
        streetAddress:   wizard.address.streetAddress,
        city:            wizard.address.city,
        stateOrProvince: wizard.address.stateOrProvince,
        postalCode:      wizard.address.postalCode,
        country:         wizard.address.country
      },
      companyConfig: {
        clientCode:    wizard.config.clientCode,
        groupCode:     wizard.config.groupCode,
        storeCode:     wizard.config.storeCode,
        groupName:     wizard.config.groupName || wizard.company.companyName,
        storeName:     wizard.config.storeName || 'Main Store',
        operationMode: opMode,
        baseCompanyUrl: wizard.config.baseCompanyUrl || null
      },
      keyPersonalDetails: {
        storeManagerName:        wizard.keyPeople.storeManagerName || wizard.client.firstName + ' ' + wizard.client.lastName,
        storeManagerEmail:       wizard.keyPeople.storeManagerEmail || wizard.client.email,
        storeManagerPhoneNumber: wizard.keyPeople.storeManagerPhoneNumber || wizard.client.phoneNumber,
        accountantName:          wizard.keyPeople.accountantName || wizard.client.firstName + ' ' + wizard.client.lastName,
        accountantEmail:         wizard.keyPeople.accountantEmail || wizard.client.email,
        accountantPhoneNumber:   wizard.keyPeople.accountantPhoneNumber || wizard.client.phoneNumber
      },
      seedBasicStructure:      wizard.seedBasicStructure,
      isTermsAccepted:         wizard.isTermsAccepted,
      isPrivacyPolicyAccepted: wizard.isPrivacyPolicyAccepted
    }

    const res = await $fetch(config.public.apiBaseUrl + '/client-onboarding/submit', {
      method: 'POST', headers: getHeaders(), body
    }) as any

    result.value = res
    toast.add({ title: 'Onboarding Complete!', description: res.message, color: 'success' })
    await loadOptions()
  } catch (e: any) {
    const msg = e.data?.message || e.data?.errors ? JSON.stringify(e.data?.errors) : 'Onboarding failed.'
    toast.add({ title: 'Error', description: msg, color: 'error' })
  } finally {
    submitting.value = false
  }
}

onMounted(loadOptions)
</script>
