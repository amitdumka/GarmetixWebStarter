<script setup lang="ts">
const api = useGarmetixApi()
const auth = useAuth()
const feedback = useUiFeedback()

const status = ref<any | null>(null)
const generated = ref<any | null>(null)
const loading = ref(false)
const generating = ref(false)
const activating = ref(false)
const clearing = ref(false)
const loadError = ref('')
const generateError = ref('')
const activateError = ref('')

const generateForm = reactive({
  clientCode: 'AADWIKA-FASHION',
  clientName: 'Aadwika Fashion',
  plan: 'Trial',
  validityDays: 365,
  maxStores: 1,
  maxUsers: 10,
  modules: 'Billing,Inventory,Purchase,Accounting,GST,HR,Payroll',
  issuedBy: 'Garmetix Admin'
})

const activateForm = reactive({
  licenseKey: ''
})

const canManage = computed(() => auth.canSeeAdmin.value)
const isAuthenticated = auth.isAuthenticated
const stateColor = computed(() => status.value?.valid ? 'success' : status.value?.enforcementEnabled ? 'error' : 'warning')
const stateTitle = computed(() => status.value?.valid ? 'License active' : status.value?.enforcementEnabled ? 'Activation required' : 'Not enforced')
const modules = computed(() => status.value?.modules?.length ? status.value.modules.join(', ') : '-')
const requiredModules = computed(() => status.value?.requiredModules?.length ? status.value.requiredModules.join(', ') : '-')

const envSample = computed(() => `LICENSE_ENFORCEMENT_ENABLED=true
LICENSE_REQUIRE_OPERATIONAL_APIS=true
LICENSE_PRODUCT_CODE=GARMETIX-WEB
LICENSE_MASTER_SECRET=your-48-plus-character-private-master-secret
LICENSE_DEFAULT_PLAN=Trial
LICENSE_DEFAULT_VALIDITY_DAYS=365
LICENSE_DEFAULT_MAX_STORES=1
LICENSE_DEFAULT_MAX_USERS=10
LICENSE_REQUIRED_MODULES=Billing,Inventory,Purchase,Accounting,GST,HR,Payroll`)

useHead({ title: 'License Activation | Garmetix' })

async function refresh() {
  if (!auth.isAuthenticated.value || !auth.canSeeAdmin.value) return
  loading.value = true
  loadError.value = ''
  try {
    status.value = await api.get<any>('license/status')
  } catch (error) {
    loadError.value = feedback.errorMessage(error, 'License status could not be loaded.', 'License diagnostics failed')
  } finally {
    loading.value = false
  }
}

function splitModules() {
  return generateForm.modules
    .split(',')
    .map((item) => item.trim())
    .filter(Boolean)
}

async function generateLicense() {
  generateError.value = ''
  generated.value = null
  generating.value = true
  try {
    generated.value = await api.create<any>('license/generate', {
      clientCode: generateForm.clientCode.trim(),
      clientName: generateForm.clientName.trim(),
      plan: generateForm.plan.trim(),
      validityDays: Number(generateForm.validityDays || 365),
      maxStores: Number(generateForm.maxStores || 1),
      maxUsers: Number(generateForm.maxUsers || 1),
      modules: splitModules(),
      issuedBy: generateForm.issuedBy.trim()
    })
    activateForm.licenseKey = generated.value.licenseKey || ''
  } catch (error) {
    generateError.value = feedback.errorMessage(error, 'License key generation failed. Check LICENSE_MASTER_SECRET.', 'License generation failed')
  } finally {
    generating.value = false
  }
}

async function activateLicense() {
  activateError.value = ''
  if (!activateForm.licenseKey.trim()) {
    activateError.value = 'Paste a license key first.'
    return
  }

  activating.value = true
  try {
    status.value = await api.create<any>('license/activate', {
      licenseKey: activateForm.licenseKey.trim()
    })
    activateForm.licenseKey = ''
  } catch (error) {
    activateError.value = feedback.errorMessage(error, 'License activation failed. Check key, product code, expiry and master secret.', 'License activation failed')
  } finally {
    activating.value = false
  }
}

async function clearActivation() {
  clearing.value = true
  activateError.value = ''
  try {
    await $fetch(`${useRuntimeConfig().public.apiBase}/license/activation`, { method: 'DELETE', headers: api.authHeaders() })
  } catch (error) {
    activateError.value = feedback.errorMessage(error, 'Could not remove the activation file.', 'Activation removal failed')
    clearing.value = false
    return
  }
  clearing.value = false
  await refresh()
}

onMounted(refresh)
</script>

<template>
  <AppShell title="License Activation" @refresh="refresh">
    <div v-if="!isAuthenticated" class="rounded-3xl border border-dashed border-slate-300 p-8 text-center text-sm text-slate-500 dark:border-slate-700 dark:text-slate-400">
      Login as admin to manage SaaS licensing and activation.
    </div>

    <div v-else-if="!canManage" class="rounded-3xl border border-dashed border-amber-300 bg-amber-50 p-8 text-center text-sm text-amber-700 dark:border-amber-700 dark:bg-amber-950/40 dark:text-amber-200">
      License Activation is available only for admin or owner users.
    </div>

    <div v-else class="space-y-6">
      <UiModulePageHeader
        title="License Activation"
        description="Generate and activate signed client licenses for SaaS/client installations. Enforcement is optional until production mode is enabled. Keep the master secret only in .env.production."
        icon="i-lucide-key-round"
      >
        <template #actions>
          <div class="flex flex-wrap items-center gap-3">
            <UBadge :color="stateColor" variant="soft" size="lg">{{ stateTitle }}</UBadge>
            <UButton icon="i-lucide-refresh-cw" :loading="loading" @click="refresh">Refresh</UButton>
          </div>
        </template>
      </UiModulePageHeader>

      <UAlert
        v-if="loadError"
        color="error"
        variant="subtle"
        icon="i-lucide-circle-alert"
        title="License status unavailable"
        :description="loadError"
      />

      <section class="grid gap-4 md:grid-cols-4">
        <UCard>
          <p class="text-sm text-slate-500 dark:text-slate-400">State</p>
          <p class="mt-2 text-xl font-semibold text-slate-950 dark:text-white">{{ status?.state || '-' }}</p>
        </UCard>
        <UCard>
          <p class="text-sm text-slate-500 dark:text-slate-400">Client</p>
          <p class="mt-2 text-xl font-semibold text-slate-950 dark:text-white">{{ status?.clientCode || '-' }}</p>
        </UCard>
        <UCard>
          <p class="text-sm text-slate-500 dark:text-slate-400">Plan</p>
          <p class="mt-2 text-xl font-semibold text-slate-950 dark:text-white">{{ status?.plan || '-' }}</p>
        </UCard>
        <UCard>
          <p class="text-sm text-slate-500 dark:text-slate-400">Days remaining</p>
          <p class="mt-2 text-xl font-semibold" :class="status?.daysRemaining !== null && status?.daysRemaining < 15 ? 'text-amber-600' : 'text-slate-950 dark:text-white'">{{ status?.daysRemaining ?? '-' }}</p>
        </UCard>
      </section>

      <UCard>
        <template #header>
          <div class="flex items-center justify-between gap-3">
            <div>
              <h2 class="font-semibold">Activation status</h2>
              <p class="text-sm text-slate-500 dark:text-slate-400">Operational API enforcement starts only when LICENSE_ENFORCEMENT_ENABLED=true.</p>
            </div>
            <UBadge :color="status?.valid ? 'success' : status?.enforcementEnabled ? 'error' : 'warning'" variant="soft">{{ status?.message || 'Not loaded' }}</UBadge>
          </div>
        </template>

        <div class="grid gap-4 md:grid-cols-2">
          <div class="space-y-2 text-sm text-slate-600 dark:text-slate-300">
            <p><strong>Product:</strong> {{ status?.productCode || '-' }}</p>
            <p><strong>Client:</strong> {{ status?.clientName || '-' }}</p>
            <p><strong>Max stores/users:</strong> {{ status?.maxStores ?? '-' }} / {{ status?.maxUsers ?? '-' }}</p>
            <p><strong>Expires:</strong> {{ status?.expiresAtUtc || '-' }}</p>
            <p><strong>Fingerprint:</strong> {{ status?.licenseFingerprint || '-' }}</p>
            <p><strong>Machine:</strong> {{ status?.machineName || '-' }}</p>
            <p><strong>Activation file:</strong> {{ status?.activationFilePath || '-' }}</p>
            <p><strong>Modules:</strong> {{ modules }}</p>
            <p><strong>Required modules:</strong> {{ requiredModules }}</p>
          </div>
          <div class="space-y-3">
            <UAlert
              v-if="status?.issues?.length"
              color="warning"
              variant="subtle"
              icon="i-lucide-triangle-alert"
              title="License setup needs attention"
              description="Fix these items before enabling license enforcement in production."
            />
            <UAlert
              v-else
              color="success"
              variant="subtle"
              icon="i-lucide-key-round"
              title="License status looks good"
              description="Keep a backup of the activation file together with production backup notes."
            />
            <ul v-if="status?.issues?.length" class="list-disc space-y-1 pl-5 text-sm text-amber-600 dark:text-amber-300">
              <li v-for="issue in status.issues" :key="issue">{{ issue }}</li>
            </ul>
          </div>
        </div>
      </UCard>

      <section class="grid gap-4 xl:grid-cols-[1fr_1fr]">
        <UCard>
          <template #header>
            <div class="flex items-center gap-2">
              <UIcon name="i-lucide-badge-plus" class="h-5 w-5" />
              <h2 class="font-semibold">Generate client license</h2>
            </div>
          </template>
          <form class="space-y-3" @submit.prevent="generateLicense">
            <UFormField label="Client code" required>
              <UInput v-model="generateForm.clientCode" placeholder="AADWIKA-FASHION" />
            </UFormField>
            <UFormField label="Client name" required>
              <UInput v-model="generateForm.clientName" placeholder="Aadwika Fashion" />
            </UFormField>
            <div class="grid gap-3 md:grid-cols-3">
              <UFormField label="Plan">
                <UInput v-model="generateForm.plan" />
              </UFormField>
              <UFormField label="Validity days">
                <UInput v-model="generateForm.validityDays" type="number" min="1" />
              </UFormField>
              <UFormField label="Max stores">
                <UInput v-model="generateForm.maxStores" type="number" min="1" />
              </UFormField>
            </div>
            <UFormField label="Max users">
              <UInput v-model="generateForm.maxUsers" type="number" min="1" />
            </UFormField>
            <UFormField label="Modules">
              <UInput v-model="generateForm.modules" />
            </UFormField>
            <UFormField label="Issued by">
              <UInput v-model="generateForm.issuedBy" />
            </UFormField>
            <UAlert v-if="generateError" color="error" variant="subtle" icon="i-lucide-circle-alert" title="Generation failed" :description="generateError" />
            <UButton type="submit" icon="i-lucide-key-round" :loading="generating">Generate license key</UButton>
          </form>
        </UCard>

        <UCard>
          <template #header>
            <div class="flex items-center gap-2">
              <UIcon name="i-lucide-key-square" class="h-5 w-5" />
              <h2 class="font-semibold">Activate installation</h2>
            </div>
          </template>
          <form class="space-y-3" @submit.prevent="activateLicense">
            <UFormField label="License key" required>
              <UTextarea v-model="activateForm.licenseKey" :rows="8" placeholder="Paste GARMETIX-LIC-v1... key here" />
            </UFormField>
            <UAlert v-if="activateError" color="error" variant="subtle" icon="i-lucide-circle-alert" title="Activation failed" :description="activateError" />
            <div class="flex flex-wrap gap-3">
              <UButton type="submit" icon="i-lucide-check" :loading="activating">Activate license</UButton>
              <UButton color="neutral" variant="outline" icon="i-lucide-trash-2" :loading="clearing" @click="clearActivation">Remove activation</UButton>
            </div>
          </form>
        </UCard>
      </section>

      <UCard v-if="generated">
        <template #header>
          <div>
            <h2 class="font-semibold">Generated license key</h2>
            <p class="text-sm text-slate-500 dark:text-slate-400">Copy this key to the client installation and activate it. Do not expose LICENSE_MASTER_SECRET.</p>
          </div>
        </template>
        <UTextarea :model-value="generated.licenseKey" :rows="6" readonly />
        <div class="mt-3 grid gap-2 text-sm text-slate-600 dark:text-slate-300 md:grid-cols-3">
          <p><strong>Fingerprint:</strong> {{ generated.licenseFingerprint }}</p>
          <p><strong>Client:</strong> {{ generated.payload?.clientCode }}</p>
          <p><strong>Expires:</strong> {{ generated.payload?.expiresAtUtc }}</p>
        </div>
      </UCard>

      <UCard>
        <template #header>
          <div>
            <h2 class="font-semibold">Production .env keys</h2>
            <p class="text-sm text-slate-500 dark:text-slate-400">Enable enforcement only after an admin account exists and a valid license has been activated.</p>
          </div>
        </template>
        <pre class="overflow-x-auto rounded-2xl bg-slate-950 p-4 text-xs text-slate-100">{{ envSample }}</pre>
      </UCard>
    </div>
  </AppShell>
</template>
