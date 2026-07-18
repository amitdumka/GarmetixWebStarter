<template>
  <section class="garmetix-page-stack">
    <div class="garmetix-dashboard-hero">
      <div class="flex flex-col gap-3 xl:flex-row xl:items-start xl:justify-between">
        <div>
          <p class="garmetix-dashboard-kicker">
            <UIcon name="i-lucide-server-cog" class="size-4" />
            Communication & Mail
          </p>
          <h2 class="garmetix-dashboard-title">Email Providers</h2>
          <p class="garmetix-dashboard-subtitle">
            Configure Brevo API or SMTP providers (Brevo SMTP, GoDaddy, Microsoft 365, Gmail, custom, or a local Postfix
            relay) entirely from this page - no environment variables or server access needed. Secrets are encrypted at
            rest and never shown again after saving. With nothing configured, the module falls back to a Local Master
            provider that queues emails honestly without pretending to send them.
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
      <CommunicationMasterTable :columns="providerColumns" :rows="providerRows" empty-text="No email providers configured yet - Local Master fallback is in effect.">
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
            <UButton v-if="!row.isDefaultRaw" size="xs" color="neutral" variant="soft" icon="i-lucide-star" @click="setDefault(row)">Set Default</UButton>
            <UButton size="xs" color="error" variant="soft" icon="i-lucide-trash-2" @click="openDeleteConfirm(row)">Delete</UButton>
          </div>
        </template>
      </CommunicationMasterTable>
    </section>

    <UModal v-model:open="providerModalOpen" :title="providerFormMode === 'edit' ? 'Update Email Provider' : 'New Email Provider'" :ui="{ content: 'sm:max-w-3xl' }">
      <template #body>
        <div class="space-y-5">
          <form class="grid gap-3 sm:grid-cols-2" @submit.prevent="saveProvider">
            <label class="space-y-1 text-sm">
              <span class="text-muted">Provider Name</span>
              <UInput v-model="providerForm.providerName" placeholder="e.g. Brevo Production" required />
            </label>
            <label class="space-y-1 text-sm">
              <span class="text-muted">Provider Type</span>
              <USelect v-model="providerForm.providerType" :items="catalog.providerTypes" />
            </label>

            <template v-if="providerForm.providerType === 'Smtp'">
              <label class="space-y-1 text-sm">
                <span class="text-muted">SMTP Preset</span>
                <USelect v-model="providerForm.smtpPresetKey" :items="smtpPresetKeys" @update:model-value="applyPreset" />
              </label>
              <label class="space-y-1 text-sm">
                <span class="text-muted">Host</span>
                <UInput v-model="providerForm.host" placeholder="smtp-relay.brevo.com" />
              </label>
              <label class="space-y-1 text-sm">
                <span class="text-muted">Port</span>
                <UInput v-model.number="providerForm.port" type="number" />
              </label>
              <label class="garmetix-row-card flex items-center justify-between gap-2">
                <span>Use STARTTLS</span>
                <USwitch v-model="providerForm.useStartTls" />
              </label>
              <label class="garmetix-row-card flex items-center justify-between gap-2">
                <span>Enable SSL (implicit TLS if STARTTLS is off)</span>
                <USwitch v-model="providerForm.enableSsl" />
              </label>
            </template>
            <label v-else-if="providerForm.providerType === 'BrevoApi'" class="space-y-1 text-sm sm:col-span-2">
              <span class="text-muted">Base URL override (optional - leave blank for the default Brevo API)</span>
              <UInput v-model="providerForm.host" placeholder="https://api.brevo.com" />
            </label>

            <label class="space-y-1 text-sm">
              <span class="text-muted">From Email</span>
              <UInput v-model="providerForm.fromEmail" placeholder="no-reply@yourdomain.com" required />
            </label>
            <label class="space-y-1 text-sm">
              <span class="text-muted">From Name</span>
              <UInput v-model="providerForm.fromName" placeholder="Garmetix" />
            </label>
            <label class="space-y-1 text-sm">
              <span class="text-muted">Reply-To Email</span>
              <UInput v-model="providerForm.replyToEmail" placeholder="support@yourdomain.com" />
            </label>
            <label class="space-y-1 text-sm">
              <span class="text-muted">Priority (lower = tried first)</span>
              <UInput v-model.number="providerForm.priority" type="number" />
            </label>
            <label class="space-y-1 text-sm">
              <span class="text-muted">Timeout (seconds)</span>
              <UInput v-model.number="providerForm.timeoutSeconds" type="number" />
            </label>
            <label class="space-y-1 text-sm">
              <span class="text-muted">Max Retries</span>
              <UInput v-model.number="providerForm.maxRetries" type="number" />
            </label>
            <label class="space-y-1 text-sm">
              <span class="text-muted">Daily Rate Limit (optional)</span>
              <UInput v-model.number="providerForm.dailyRateLimit" type="number" placeholder="Unlimited" />
            </label>
            <label class="space-y-1 text-sm">
              <span class="text-muted">Per-Minute Rate Limit (optional)</span>
              <UInput v-model.number="providerForm.perMinuteRateLimit" type="number" placeholder="Unlimited" />
            </label>
            <label class="garmetix-row-card flex items-center justify-between gap-2">
              <span>Enabled</span>
              <USwitch v-model="providerForm.isEnabled" />
            </label>
            <label class="garmetix-row-card flex items-center justify-between gap-2">
              <span>Set as default for its scope</span>
              <USwitch v-model="providerForm.isDefault" />
            </label>

            <details class="sm:col-span-2 rounded-lg border border-default p-3 text-sm">
              <summary class="cursor-pointer text-muted">Advanced: scope this provider to one Company/Store/Group (optional)</summary>
              <div class="mt-3 grid gap-3 sm:grid-cols-3">
                <label class="space-y-1 text-sm">
                  <span class="text-muted">Company Id</span>
                  <UInput v-model="providerForm.companyId" placeholder="Leave blank for global" />
                </label>
                <label class="space-y-1 text-sm">
                  <span class="text-muted">Store Group Id</span>
                  <UInput v-model="providerForm.storeGroupId" placeholder="Leave blank for global" />
                </label>
                <label class="space-y-1 text-sm">
                  <span class="text-muted">Store Id</span>
                  <UInput v-model="providerForm.storeId" placeholder="Leave blank for global" />
                </label>
              </div>
            </details>

            <label class="space-y-1 text-sm sm:col-span-2">
              <span class="text-muted">Notes</span>
              <UTextarea v-model="providerForm.notes" :rows="2" />
            </label>

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
                <UButton size="sm" color="neutral" variant="soft" icon="i-lucide-plug-zap" :loading="testing.connection" @click="testConnection">Test Connection</UButton>
                <UInput v-model="testSendEmailInput" placeholder="Send a test email to..." class="w-56" />
                <UButton size="sm" color="neutral" variant="soft" icon="i-lucide-send" :loading="testing.send" @click="sendTestEmail">Send Test Email</UButton>
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

    <UModal v-model:open="deleteConfirmOpen" title="Delete Email Provider" description="Type DELETE PROVIDER to confirm.">
      <template #body>
        <div class="space-y-3">
          <p class="text-sm">
            This permanently removes <strong>{{ providerPendingDelete?.providerName }}</strong> and its stored credentials.
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
import CommunicationMasterTable from '../components/CommunicationMasterTable.vue'
import { readText, type ApiRecord, useCommunicationApiClient } from '../utils/communication-api'

useHead({ title: 'Providers - Garmetix Communication & Mail' })

interface SmtpPresetDefaults {
  host: string
  port: number
  enableSsl: boolean
  useStartTls: boolean
  description: string
}

interface EmailProviderCatalog {
  providerTypes: string[]
  credentialKeys: string[]
  smtpPresets: Record<string, SmtpPresetDefaults>
}

interface EmailProviderRow {
  id: string
  providerName: string
  providerType: string
  smtpPresetKey: string | null
  isEnabled: boolean
  isDefault: boolean
  priority: number
  companyId: string | null
  storeGroupId: string | null
  storeId: string | null
}

interface EmailProviderDetail extends EmailProviderRow {
  host: string | null
  port: number | null
  enableSsl: boolean
  useStartTls: boolean
  fromEmail: string
  fromName: string
  replyToEmail: string | null
  timeoutSeconds: number
  maxRetries: number
  dailyRateLimit: number | null
  perMinuteRateLimit: number | null
  notes: string | null
}

interface EmailCredentialEntry {
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

interface EmailProviderTableRow {
  id: string
  providerName: string
  providerType: string
  isEnabled: string
  isEnabledRaw: boolean
  isDefaultRaw: boolean
  priority: number
  fromEmail: string
}

const { get, post, put, del } = useCommunicationApiClient()

const loading = ref(true)
const error = ref('')
const message = ref('')
const providers = ref<EmailProviderRow[]>([])
const catalog = ref<EmailProviderCatalog>({ providerTypes: [], credentialKeys: [], smtpPresets: {} })

const providerColumns = [
  { key: 'providerName', label: 'Provider' },
  { key: 'providerType', label: 'Type' },
  { key: 'isEnabled', label: 'Status' },
  { key: 'priority', label: 'Priority' },
  { key: 'fromEmail', label: 'From' }
]
const providerRows = computed(() => providers.value.map(p => ({
  id: p.id,
  providerName: p.isDefault ? `${p.providerName} (Default)` : p.providerName,
  providerType: p.providerType,
  isEnabled: p.isEnabled ? 'Enabled' : 'Disabled',
  isEnabledRaw: p.isEnabled,
  isDefaultRaw: p.isDefault,
  priority: p.priority
})))

const smtpPresetKeys = computed(() => Object.keys(catalog.value.smtpPresets))

const providerModalOpen = ref(false)
const providerFormMode = ref<'create' | 'edit'>('create')
const selectedProviderId = ref<string | null>(null)
const savingProvider = ref(false)

function emptyProviderForm() {
  return {
    providerName: '',
    providerType: 'Smtp',
    smtpPresetKey: '',
    host: '',
    port: 587 as number | null,
    enableSsl: true,
    useStartTls: true,
    fromEmail: '',
    fromName: 'Garmetix',
    replyToEmail: '',
    priority: 100,
    timeoutSeconds: 30,
    maxRetries: 3,
    dailyRateLimit: null as number | null,
    perMinuteRateLimit: null as number | null,
    isEnabled: true,
    isDefault: false,
    companyId: '',
    storeGroupId: '',
    storeId: '',
    notes: ''
  }
}

const providerForm = ref(emptyProviderForm())

function applyPreset(presetKey: string) {
  const preset = catalog.value.smtpPresets[presetKey]
  if (!preset) return
  providerForm.value.host = preset.host
  providerForm.value.port = preset.port
  providerForm.value.enableSsl = preset.enableSsl
  providerForm.value.useStartTls = preset.useStartTls
}

const credentials = ref<EmailCredentialEntry[]>([])
const credentialDrafts = ref<Record<string, string>>({})
const savingCredentials = ref(false)

const testSendEmailInput = ref('')
const testResults = ref<TestResult[]>([])
const testing = reactive({ connection: false, send: false })

const deleteConfirmOpen = ref(false)
const deleteConfirmText = ref('')
const providerPendingDelete = ref<EmailProviderTableRow | null>(null)
const deletingProvider = ref(false)

async function refresh() {
  loading.value = true
  error.value = ''
  try {
    const [providerData, catalogData] = await Promise.all([
      get<EmailProviderRow[]>('/communication/providers'),
      get<EmailProviderCatalog>('/communication/providers/catalog')
    ])
    providers.value = providerData ?? []
    catalog.value = catalogData ?? { providerTypes: [], credentialKeys: [], smtpPresets: {} }
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to load email providers.'
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

async function openEditModal(row: EmailProviderTableRow) {
  providerFormMode.value = 'edit'
  selectedProviderId.value = row.id
  testResults.value = []
  try {
    const detail = await get<EmailProviderDetail>(`/communication/providers/${row.id}`)
    providerForm.value = {
      providerName: detail.providerName,
      providerType: detail.providerType,
      smtpPresetKey: detail.smtpPresetKey ?? '',
      host: detail.host ?? '',
      port: detail.port,
      enableSsl: detail.enableSsl,
      useStartTls: detail.useStartTls,
      fromEmail: detail.fromEmail,
      fromName: detail.fromName,
      replyToEmail: detail.replyToEmail ?? '',
      priority: detail.priority,
      timeoutSeconds: detail.timeoutSeconds,
      maxRetries: detail.maxRetries,
      dailyRateLimit: detail.dailyRateLimit,
      perMinuteRateLimit: detail.perMinuteRateLimit,
      isEnabled: detail.isEnabled,
      isDefault: detail.isDefault,
      companyId: detail.companyId ?? '',
      storeGroupId: detail.storeGroupId ?? '',
      storeId: detail.storeId ?? '',
      notes: detail.notes ?? ''
    }
    credentialDrafts.value = {}
    credentials.value = await get<EmailCredentialEntry[]>(`/communication/providers/${row.id}/credentials`)
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
      smtpPresetKey: providerForm.value.smtpPresetKey || null,
      host: providerForm.value.host || null,
      port: providerForm.value.port,
      enableSsl: providerForm.value.enableSsl,
      useStartTls: providerForm.value.useStartTls,
      fromEmail: providerForm.value.fromEmail,
      fromName: providerForm.value.fromName,
      replyToEmail: providerForm.value.replyToEmail || null,
      isEnabled: providerForm.value.isEnabled,
      isDefault: providerForm.value.isDefault,
      priority: providerForm.value.priority,
      timeoutSeconds: providerForm.value.timeoutSeconds,
      maxRetries: providerForm.value.maxRetries,
      dailyRateLimit: providerForm.value.dailyRateLimit,
      perMinuteRateLimit: providerForm.value.perMinuteRateLimit,
      companyId: providerForm.value.companyId || null,
      storeGroupId: providerForm.value.storeGroupId || null,
      storeId: providerForm.value.storeId || null,
      notes: providerForm.value.notes || null
    }

    if (providerFormMode.value === 'edit' && selectedProviderId.value) {
      await put(`/communication/providers/${selectedProviderId.value}`, payload)
      message.value = 'Provider updated.'
    } else {
      await post('/communication/providers', payload)
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

async function toggleEnabled(row: EmailProviderTableRow) {
  error.value = ''
  try {
    await post(`/communication/providers/${row.id}/${row.isEnabledRaw ? 'disable' : 'enable'}`)
    await refresh()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to update the provider status.'
  }
}

async function setDefault(row: EmailProviderTableRow) {
  error.value = ''
  try {
    await post(`/communication/providers/${row.id}/set-default`)
    message.value = `${row.providerName} set as default.`
    await refresh()
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to set the default provider.'
  }
}

function openDeleteConfirm(row: EmailProviderTableRow) {
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
    await del(`/communication/providers/${providerPendingDelete.value.id}`)
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
    await put(`/communication/providers/${selectedProviderId.value}/credentials`, { entries })
    credentialDrafts.value = {}
    credentials.value = await get<EmailCredentialEntry[]>(`/communication/providers/${selectedProviderId.value}/credentials`)
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
    await put(`/communication/providers/${selectedProviderId.value}/credentials`, { entries: [{ credentialKey: key, value: null, clear: true }] })
    credentials.value = await get<EmailCredentialEntry[]>(`/communication/providers/${selectedProviderId.value}/credentials`)
    message.value = `${key} cleared.`
  } catch (err) {
    error.value = err instanceof Error ? err.message : 'Failed to clear the credential.'
  }
}

async function testConnection() {
  if (!selectedProviderId.value) return
  testing.connection = true
  try {
    const result = await post<ApiRecord>(`/communication/providers/${selectedProviderId.value}/test-connection`)
    testResults.value.unshift({ key: `conn-${Date.now()}`, success: Boolean(result.isSuccess), message: readText(result, ['message']) })
  } catch (err) {
    testResults.value.unshift({ key: `conn-${Date.now()}`, success: false, message: err instanceof Error ? err.message : 'Connection test failed.' })
  } finally {
    testing.connection = false
  }
}

async function sendTestEmail() {
  if (!selectedProviderId.value) return
  if (!testSendEmailInput.value.trim()) {
    error.value = 'Enter a recipient email address for the test send.'
    return
  }
  testing.send = true
  try {
    const result = await post<ApiRecord>(`/communication/providers/${selectedProviderId.value}/send-test`, { toEmail: testSendEmailInput.value.trim() })
    testResults.value.unshift({
      key: `send-${Date.now()}`,
      success: Boolean(result.isSuccess),
      message: result.isSuccess ? 'Test email sent successfully.' : readText(result, ['errorMessage'], 'Test email failed.')
    })
  } catch (err) {
    testResults.value.unshift({ key: `send-${Date.now()}`, success: false, message: err instanceof Error ? err.message : 'Test email failed.' })
  } finally {
    testing.send = false
  }
}

onMounted(refresh)
</script>
