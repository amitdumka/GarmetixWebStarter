<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 xl:flex-row xl:items-start xl:justify-between">
        <div>
          <p class="garmetix-dashboard-kicker">
            <UIcon name="i-lucide-plug-zap" class="size-4" />
            GST & Taxes
          </p>
          <h2 class="garmetix-dashboard-title">GST API Setup</h2>
          <p class="garmetix-dashboard-subtitle">
            Configure GST API providers, credentials, and feature/priority mapping entirely from this page - no environment
            variables or server access needed. Secrets are encrypted at rest and never shown again after saving.
          </p>
        </div>
        <div class="flex flex-col gap-2 sm:flex-row">
          <UButton icon="i-lucide-refresh-cw" color="neutral" variant="soft" :loading="loading" @click="refresh">Refresh</UButton>
          <UButton icon="i-lucide-plus" color="primary" @click="openCreateModal">New Provider</UButton>
        </div>
      </div>
    </div>

    <UAlert v-if="error" color="warning" variant="subtle" icon="i-lucide-triangle-alert" :description="error" />
    <UAlert v-if="message" color="success" variant="subtle" icon="i-lucide-circle-check" :description="message" />

    <section class="garmetix-section-card">
      <h3 class="garmetix-panel-title mb-3">Providers</h3>
      <BooksMasterTable :columns="providerColumns" :rows="providerRows" empty-text="No GST API providers configured yet.">
        <template #actions="{ row }">
          <div class="flex flex-wrap gap-1">
            <UButton size="xs" color="neutral" variant="soft" icon="i-lucide-pencil" @click="openEditModal(row)">Edit</UButton>
            <UButton
              size="xs"
              color="neutral"
              variant="soft"
              :icon="row.isEnabledRaw ? 'i-lucide-pause' : 'i-lucide-play'"
              @click="toggleEnabled(row)"
            >
              {{ row.isEnabledRaw ? 'Disable' : 'Enable' }}
            </UButton>
            <UButton size="xs" color="error" variant="soft" icon="i-lucide-trash-2" @click="openDeleteConfirm(row)">Delete</UButton>
          </div>
        </template>
      </BooksMasterTable>
    </section>

    <UModal v-model:open="providerModalOpen" :title="providerFormMode === 'edit' ? 'Update GST API Provider' : 'New GST API Provider'" :ui="{ content: 'sm:max-w-3xl' }">
      <template #body>
        <div class="space-y-5">
          <form class="grid gap-3 sm:grid-cols-2" @submit.prevent="saveProvider">
            <label class="space-y-1 text-sm">
              <span class="text-muted">Provider Name</span>
              <UInput v-model="providerForm.providerName" placeholder="e.g. ClearTax Production" required />
            </label>
            <label class="space-y-1 text-sm">
              <span class="text-muted">Provider Type</span>
              <USelect v-model="providerForm.providerType" :items="providerTypeItems" />
            </label>
            <label class="space-y-1 text-sm">
              <span class="text-muted">Environment</span>
              <USelect v-model="providerForm.environment" :items="environmentItems" />
            </label>
            <label class="space-y-1 text-sm">
              <span class="text-muted">Priority (lower = tried first, Local Master stays last)</span>
              <UInput v-model.number="providerForm.priority" type="number" />
            </label>
            <label class="space-y-1 text-sm">
              <span class="text-muted">Base URL</span>
              <UInput v-model="providerForm.baseUrl" placeholder="https://api.example.com" />
            </label>
            <label class="space-y-1 text-sm">
              <span class="text-muted">Auth URL</span>
              <UInput v-model="providerForm.authUrl" placeholder="https://api.example.com/auth" />
            </label>
            <label class="space-y-1 text-sm">
              <span class="text-muted">Timeout (seconds)</span>
              <UInput v-model.number="providerForm.timeoutSeconds" type="number" />
            </label>
            <label class="space-y-1 text-sm">
              <span class="text-muted">Max Retries</span>
              <UInput v-model.number="providerForm.maxRetries" type="number" />
            </label>
            <label class="garmetix-row-card flex items-center justify-between gap-2 sm:col-span-2">
              <span>Enabled</span>
              <USwitch v-model="providerForm.isEnabled" />
            </label>
            <label class="garmetix-row-card flex items-center justify-between gap-2 sm:col-span-2">
              <span>Allow fallback to the next-priority provider on failure</span>
              <USwitch v-model="providerForm.fallbackEnabled" />
            </label>
            <label class="space-y-1 text-sm sm:col-span-2">
              <span class="text-muted">Notes</span>
              <UTextarea v-model="providerForm.notes" :rows="2" />
            </label>

            <div class="sm:col-span-2">
              <p class="garmetix-panel-subtitle mb-2">Feature Mapping - which GST features this provider serves</p>
              <div class="grid grid-cols-2 gap-2 text-sm sm:grid-cols-3">
                <label v-for="feature in featureCodes" :key="feature" class="flex items-center gap-2">
                  <UCheckbox v-model="providerForm.features[feature]" />
                  <span>{{ feature }}</span>
                </label>
              </div>
            </div>

            <div class="flex justify-end gap-2 sm:col-span-2">
              <UButton type="button" color="neutral" variant="soft" @click="providerModalOpen = false">Cancel</UButton>
              <UButton type="submit" color="primary" icon="i-lucide-save" :loading="savingProvider">
                {{ providerFormMode === 'edit' ? 'Update Provider' : 'Create Provider' }}
              </UButton>
            </div>
          </form>

          <template v-if="providerFormMode === 'edit' && selectedProviderId">
            <div class="border-t border-default pt-4">
              <p class="garmetix-panel-subtitle mb-2">Credentials - stored encrypted, never shown again after saving</p>
              <div class="space-y-2">
                <div v-for="entry in credentials" :key="entry.credentialKey" class="garmetix-row-card grid grid-cols-1 gap-2 sm:grid-cols-12 sm:items-center">
                  <span class="text-sm font-medium sm:col-span-3">{{ entry.credentialKey }}</span>
                  <span class="text-xs text-muted sm:col-span-3">
                    {{ entry.hasValue ? `Currently: ${entry.maskedDisplayValue}` : 'Not set' }}
                  </span>
                  <UInput v-model="credentialDrafts[entry.credentialKey]" type="password" placeholder="New value" class="sm:col-span-4" />
                  <UButton
                    v-if="entry.hasValue"
                    size="xs"
                    color="error"
                    variant="soft"
                    icon="i-lucide-eraser"
                    class="sm:col-span-2"
                    @click="clearCredential(entry.credentialKey)"
                  >
                    Clear
                  </UButton>
                </div>
              </div>
              <div class="mt-3 flex justify-end">
                <UButton color="primary" variant="soft" icon="i-lucide-key-round" :loading="savingCredentials" @click="saveCredentials">
                  Save New Credential Values
                </UButton>
              </div>
            </div>

            <div class="border-t border-default pt-4">
              <p class="garmetix-panel-subtitle mb-2">Test Tools</p>
              <div class="flex flex-wrap items-center gap-2">
                <UButton size="sm" color="neutral" variant="soft" icon="i-lucide-heart-pulse" :loading="testing.health" @click="testHealth">Test Health</UButton>
                <UInput v-model="testGstinInput" placeholder="GSTIN to test" class="w-48" />
                <UButton size="sm" color="neutral" variant="soft" icon="i-lucide-badge-check" :loading="testing.gstin" @click="testGstin">Test GSTIN Lookup</UButton>
                <UInput v-model="testHsnInput" placeholder="HSN/SAC code to test" class="w-48" />
                <UButton size="sm" color="neutral" variant="soft" icon="i-lucide-package-search" :loading="testing.hsn" @click="testHsn">Test HSN Lookup</UButton>
              </div>
              <ul class="mt-3 space-y-1 text-sm">
                <li v-for="result in testResults" :key="result.key" class="flex items-start gap-2">
                  <UIcon :name="result.success ? 'i-lucide-circle-check' : 'i-lucide-circle-x'" :class="result.success ? 'text-success' : 'text-error'" class="mt-0.5 size-4" />
                  <span>{{ result.message }}</span>
                </li>
              </ul>
            </div>
          </template>
        </div>
      </template>
    </UModal>

    <UModal v-model:open="deleteConfirmOpen" title="Delete GST API Provider" description="Type DELETE PROVIDER to confirm.">
      <template #body>
        <div class="space-y-3">
          <p class="text-sm">
            This permanently removes <strong>{{ providerPendingDelete?.providerName }}</strong>, its feature mapping, and its stored credentials.
          </p>
          <label class="space-y-1 text-sm">
            <span class="text-muted">Confirmation</span>
            <UInput v-model="deleteConfirmText" placeholder="DELETE PROVIDER" />
          </label>
          <div class="flex justify-end gap-2">
            <UButton color="neutral" variant="soft" @click="deleteConfirmOpen = false">Cancel</UButton>
            <UButton color="error" icon="i-lucide-trash-2" :loading="deletingProvider" @click="performDelete">Delete Provider</UButton>
          </div>
        </div>
      </template>
    </UModal>
  </section>
</template>

<script setup lang="ts">
import { readText, type ApiRecord, useBooksApiClient } from '../utils/books-api'

useHead({ title: 'GST API Setup - Garmetix Books' })

interface GstProviderCatalog {
  providerTypes: string[]
  environments: string[]
  featureCodes: string[]
  credentialKeys: string[]
}

interface GstProviderRow {
  id: string
  providerName: string
  providerType: string
  environment: string
  isEnabled: boolean
  priority: number
  fallbackEnabled: boolean
  features: string[]
}

interface GstProviderDetail extends GstProviderRow {
  baseUrl: string | null
  authUrl: string | null
  timeoutSeconds: number
  maxRetries: number
  notes: string | null
}

interface GstCredentialEntry {
  credentialKey: string
  hasValue: boolean
  maskedDisplayValue: string | null
  updatedAt: string | null
}

interface TestResult {
  key: string
  success: boolean
  message: string
}

interface GstProviderTableRow {
  id: string
  providerName: string
  providerType: string
  environment: string
  isEnabled: string
  isEnabledRaw: boolean
  priority: number
  features: string
}

const { get, post, put, del } = useBooksApiClient()

const loading = ref(true)
const error = ref('')
const message = ref('')
const providers = ref<GstProviderRow[]>([])
const catalog = ref<GstProviderCatalog>({ providerTypes: [], environments: [], featureCodes: [], credentialKeys: [] })

const providerColumns = [
  { key: 'providerName', label: 'Provider' },
  { key: 'providerType', label: 'Type' },
  { key: 'environment', label: 'Environment' },
  { key: 'isEnabled', label: 'Status' },
  { key: 'priority', label: 'Priority' },
  { key: 'features', label: 'Features' }
]
const providerRows = computed(() => providers.value.map(p => ({
  id: p.id,
  providerName: p.providerName,
  providerType: p.providerType,
  environment: p.environment,
  isEnabled: p.isEnabled ? 'Enabled' : 'Disabled',
  isEnabledRaw: p.isEnabled,
  priority: p.priority,
  features: p.features.join(', ') || '-'
})))

const providerTypeItems = computed(() => catalog.value.providerTypes)
const environmentItems = computed(() => catalog.value.environments)
const featureCodes = computed(() => catalog.value.featureCodes)

const providerModalOpen = ref(false)
const providerFormMode = ref<'create' | 'edit'>('create')
const selectedProviderId = ref<string | null>(null)
const savingProvider = ref(false)

function emptyProviderForm() {
  return {
    providerName: '',
    providerType: 'GenericRestProvider',
    environment: 'Sandbox',
    baseUrl: '',
    authUrl: '',
    priority: 100,
    timeoutSeconds: 30,
    maxRetries: 1,
    isEnabled: true,
    fallbackEnabled: true,
    notes: '',
    features: {} as Record<string, boolean>
  }
}

const providerForm = ref(emptyProviderForm())

const credentials = ref<GstCredentialEntry[]>([])
const credentialDrafts = ref<Record<string, string>>({})
const savingCredentials = ref(false)

const testGstinInput = ref('')
const testHsnInput = ref('')
const testResults = ref<TestResult[]>([])
const testing = reactive({ health: false, gstin: false, hsn: false })

const deleteConfirmOpen = ref(false)
const deleteConfirmText = ref('')
const providerPendingDelete = ref<GstProviderTableRow | null>(null)
const deletingProvider = ref(false)

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const [providerData, catalogData] = await Promise.all([
      get<GstProviderRow[]>('/gst/providers'),
      get<GstProviderCatalog>('/gst/providers/catalog')
    ])
    providers.value = providerData ?? []
    catalog.value = catalogData ?? { providerTypes: [], environments: [], featureCodes: [], credentialKeys: [] }
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to load GST API providers.'
  } finally {
    loading.value = false
  }
}

function openCreateModal() {
  providerFormMode.value = 'create'
  selectedProviderId.value = null
  providerForm.value = emptyProviderForm()
  credentials.value = []
  credentialDrafts.value = {}
  testResults.value = []
  providerModalOpen.value = true
}

async function openEditModal(row: GstProviderTableRow) {
  providerFormMode.value = 'edit'
  selectedProviderId.value = row.id
  testResults.value = []
  try {
    const detail = await get<GstProviderDetail>(`/gst/providers/${row.id}`)
    const featureMap: Record<string, boolean> = {}
    for (const code of catalog.value.featureCodes) featureMap[code] = detail.features.includes(code)
    providerForm.value = {
      providerName: detail.providerName,
      providerType: detail.providerType,
      environment: detail.environment,
      baseUrl: detail.baseUrl ?? '',
      authUrl: detail.authUrl ?? '',
      priority: detail.priority,
      timeoutSeconds: detail.timeoutSeconds,
      maxRetries: detail.maxRetries,
      isEnabled: detail.isEnabled,
      fallbackEnabled: detail.fallbackEnabled,
      notes: detail.notes ?? '',
      features: featureMap
    }
    credentialDrafts.value = {}
    credentials.value = await get<GstCredentialEntry[]>(`/gst/providers/${row.id}/credentials`)
    providerModalOpen.value = true
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to load the provider.'
  }
}

async function saveProvider() {
  savingProvider.value = true
  error.value = ''
  message.value = ''
  try {
    const payload = {
      providerName: providerForm.value.providerName,
      providerType: providerForm.value.providerType,
      environment: providerForm.value.environment,
      baseUrl: providerForm.value.baseUrl || null,
      authUrl: providerForm.value.authUrl || null,
      isEnabled: providerForm.value.isEnabled,
      priority: providerForm.value.priority,
      fallbackEnabled: providerForm.value.fallbackEnabled,
      timeoutSeconds: providerForm.value.timeoutSeconds,
      maxRetries: providerForm.value.maxRetries,
      notes: providerForm.value.notes || null,
      features: Object.entries(providerForm.value.features).filter(([, enabled]) => enabled).map(([code]) => code)
    }

    if (providerFormMode.value === 'edit' && selectedProviderId.value) {
      await put(`/gst/providers/${selectedProviderId.value}`, payload)
      message.value = 'Provider updated.'
    } else {
      await post('/gst/providers', payload)
      message.value = 'Provider created.'
    }

    providerModalOpen.value = false
    await refresh()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to save the provider.'
  } finally {
    savingProvider.value = false
  }
}

async function toggleEnabled(row: GstProviderTableRow) {
  error.value = ''
  try {
    await post(`/gst/providers/${row.id}/${row.isEnabledRaw ? 'disable' : 'enable'}`)
    await refresh()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to update the provider status.'
  }
}

function openDeleteConfirm(row: GstProviderTableRow) {
  providerPendingDelete.value = row
  deleteConfirmText.value = ''
  deleteConfirmOpen.value = true
}

async function performDelete() {
  if (deleteConfirmText.value !== 'DELETE PROVIDER') {
    error.value = 'Type DELETE PROVIDER exactly to confirm.'
    return
  }
  if (!providerPendingDelete.value) return

  deletingProvider.value = true
  try {
    await del(`/gst/providers/${providerPendingDelete.value.id}`)
    deleteConfirmOpen.value = false
    message.value = 'Provider deleted.'
    await refresh()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to delete the provider.'
  } finally {
    deletingProvider.value = false
  }
}

async function saveCredentials() {
  if (!selectedProviderId.value) return
  const entries = Object.entries(credentialDrafts.value)
    .filter(([, value]) => value && value.trim() !== '')
    .map(([credentialKey, value]) => ({ credentialKey, value, clear: false }))

  if (entries.length === 0) {
    error.value = 'Enter at least one credential value to save.'
    return
  }

  savingCredentials.value = true
  error.value = ''
  try {
    await put(`/gst/providers/${selectedProviderId.value}/credentials`, { entries })
    credentialDrafts.value = {}
    credentials.value = await get<GstCredentialEntry[]>(`/gst/providers/${selectedProviderId.value}/credentials`)
    message.value = 'Credentials saved.'
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to save credentials.'
  } finally {
    savingCredentials.value = false
  }
}

async function clearCredential(key: string) {
  if (!selectedProviderId.value) return
  try {
    await put(`/gst/providers/${selectedProviderId.value}/credentials`, { entries: [{ credentialKey: key, value: null, clear: true }] })
    credentials.value = await get<GstCredentialEntry[]>(`/gst/providers/${selectedProviderId.value}/credentials`)
    message.value = `${key} cleared.`
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to clear the credential.'
  }
}

async function testHealth() {
  if (!selectedProviderId.value) return
  testing.health = true
  try {
    const result = await post<ApiRecord>(`/gst/providers/${selectedProviderId.value}/test-health`)
    testResults.value.unshift({ key: `health-${Date.now()}`, success: Boolean(result.success), message: readText(result, ['message']) })
  } catch (err) {
    testResults.value.unshift({ key: `health-${Date.now()}`, success: false, message: err instanceof Error ? err.message : 'Health test failed.' })
  } finally {
    testing.health = false
  }
}

async function testGstin() {
  if (!selectedProviderId.value) return
  testing.gstin = true
  try {
    const result = await post<ApiRecord>(`/gst/providers/${selectedProviderId.value}/test-gstin`, { gstin: testGstinInput.value })
    testResults.value.unshift({ key: `gstin-${Date.now()}`, success: Boolean(result.success), message: readText(result, ['message']) })
  } catch (err) {
    testResults.value.unshift({ key: `gstin-${Date.now()}`, success: false, message: err instanceof Error ? err.message : 'GSTIN test failed.' })
  } finally {
    testing.gstin = false
  }
}

async function testHsn() {
  if (!selectedProviderId.value) return
  testing.hsn = true
  try {
    const result = await post<ApiRecord>(`/gst/providers/${selectedProviderId.value}/test-hsn`, { hsnCode: testHsnInput.value })
    testResults.value.unshift({ key: `hsn-${Date.now()}`, success: Boolean(result.success), message: readText(result, ['message']) })
  } catch (err) {
    testResults.value.unshift({ key: `hsn-${Date.now()}`, success: false, message: err instanceof Error ? err.message : 'HSN test failed.' })
  } finally {
    testing.hsn = false
  }
}

onMounted(refresh)
</script>
